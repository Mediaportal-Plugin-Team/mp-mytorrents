using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Web.Script.Serialization;
using Sohvaperuna.TorrentEngine;

namespace Sohvaperuna.TransmissionEngine
{
	/// <summary>
	/// Transmission RPC backend. Used for:
	///  - BiglyBT, via its "Web Remote" plugin (Tools > Plugins > Installation Wizard, default port 9091)
	///  - Transmission itself (Web interface / RPC enabled, default port 9091)
	/// Labels in MyTorrents map to Transmission labels / BiglyBT tags.
	/// </summary>
	public class TransmissionClient : ITorrentClient, ITorrentTagging, ISaveFolder
	{
		private static readonly string[] Fields =
		{
			"hashString", "name", "totalSize", "percentDone", "downloadedEver", "uploadedEver", "uploadRatio",
			"rateUpload", "rateDownload", "eta", "labels", "peersConnected", "peersSendingToUs", "peersGettingFromUs",
			"status", "error", "errorString", "addedDate", "doneDate", "downloadDir", "leftUntilDone", "queuePosition",
			"isFinished", "trackerStats"
		};

		private readonly object _lock = new object();
		private readonly JavaScriptSerializer _js = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
		private readonly string _rpcUrl;
		private readonly string _username;
		private readonly string _password;
		private readonly int _timerSeconds;
		private string _sessionId = "";
		private bool _sendLogin;
		private string _lastProblem;
		private TorrentList _torrentsAll;
		private TorrentLabelList _labels = new TorrentLabelList();

		public TorrentLabelList Labels => _labels;

		public TorrentList TorrentsAll
		{
			get
			{
				lock (_lock)
				{
					return _torrentsAll == null ? new TorrentList() : new TorrentList(_torrentsAll);
				}
			}
		}

		public event TorrentAdded TorrentAdded;
		public event TorrentRemoved TorrentRemoved;
		public event TorrentCompleted TorrentCompleted;

		public TransmissionClient(string host, int port, string username, string password, string outtimer)
		{
			string h = ClientSupport.LocalHostToIPv4(host).TrimEnd('/');
			if (!h.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !h.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
			{
				h = "http://" + h;
			}
			Uri u = new Uri(h);
			bool explicitPort = h.IndexOf(':', h.IndexOf("//", StringComparison.Ordinal) + 2) > 0;
			string path = u.AbsolutePath.TrimEnd('/');
			if (string.IsNullOrEmpty(path))
			{
				path = "/transmission/rpc";
			}
			_rpcUrl = new UriBuilder(u) { Port = explicitPort ? u.Port : port, Path = path }.Uri.ToString();
			// MediaPortal saves empty settings as a single space - treat blanks as "no login"
			_username = (username ?? "").Trim();
			_password = (password ?? "").Trim();
			_sendLogin = _username.Length > 0 || _password.Length > 0;
			int.TryParse(outtimer, out _timerSeconds);
			ClientSupport.EnableModernTls();
		}

		// ---------------------------------------------------------------- RPC

		/// Calls an RPC method. Returns the "arguments" object, or null on failure.
		private Dictionary<string, object> Call(string method, Dictionary<string, object> arguments, int attempt = 0)
		{
			string body = _js.Serialize(new Dictionary<string, object> { { "method", method }, { "arguments", arguments ?? new Dictionary<string, object>() } });
			try
			{
				HttpWebRequest req = (HttpWebRequest)WebRequest.Create(_rpcUrl);
				req.Method = "POST";
				req.ContentType = "application/json";
				req.Timeout = 15000;
				req.KeepAlive = false;
				req.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
				req.Headers["X-Transmission-Session-Id"] = _sessionId;
				if (_sendLogin)
				{
					req.Headers[HttpRequestHeader.Authorization] = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(_username + ":" + _password));
				}
				byte[] data = Encoding.UTF8.GetBytes(body);
				req.ContentLength = data.Length;
				using (Stream s = req.GetRequestStream())
				{
					s.Write(data, 0, data.Length);
				}
				string text;
				using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
				using (StreamReader r = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
				{
					text = r.ReadToEnd();
				}
				if (_lastProblem != null)
				{
					ClientSupport.Log("BiglyBT/Transmission: connected" + (_sendLogin ? "" : " (no login needed)"));
					_lastProblem = null;
				}
				Dictionary<string, object> result = _js.Deserialize<Dictionary<string, object>>(text);
				string status = ClientSupport.ToStr(result.ContainsKey("result") ? result["result"] : null);
				if (status != "success")
				{
					ClientSupport.Log($"BiglyBT/Transmission: {method} returned '{status}'");
					return null;
				}
				return result.ContainsKey("arguments") ? result["arguments"] as Dictionary<string, object> ?? new Dictionary<string, object>() : new Dictionary<string, object>();
			}
			catch (WebException ex)
			{
				HttpWebResponse resp = ex.Response as HttpWebResponse;
				if (resp != null && (int)resp.StatusCode == 409 && attempt < 3)
				{
					// Session id handshake (CSRF protection) - retry with the id the server gave us
					_sessionId = resp.Headers["X-Transmission-Session-Id"] ?? "";
					return Call(method, arguments, attempt + 1);
				}
				if (resp != null && resp.StatusCode == HttpStatusCode.Unauthorized)
				{
					if (_sendLogin && attempt < 3)
					{
						// Password may be switched off in BiglyBT/Transmission - try once without a login
						_sendLogin = false;
						return Call(method, arguments, attempt + 1);
					}
					string realm = resp.Headers["WWW-Authenticate"] ?? "";
					string reply = "";
					try
					{
						using (StreamReader r = new StreamReader(resp.GetResponseStream()))
						{
							reply = r.ReadToEnd();
						}
					}
					catch
					{
					}
					reply = System.Text.RegularExpressions.Regex.Replace(System.Text.RegularExpressions.Regex.Replace(reply, "<[^>]+>", " "), @"\s+", " ").Trim();
					LogOnce($"BiglyBT/Transmission refused {_rpcUrl} (401) {realm} {(reply.Length > 200 ? reply.Substring(0, 200) : reply)}");
					_lastProblem = null;
					LogOnce(_username.Length > 0
						? "BiglyBT/Transmission: login refused - check the username/password in MyTorrents Configuration"
						: "BiglyBT/Transmission asks for a login - enter its username/password in MyTorrents Configuration, or untick 'Enable password' in BiglyBT's Web Remote settings");
					if (_username.Length > 0 || _password.Length > 0)
					{
						_sendLogin = true; // try the login again next time
					}
				}
				else
				{
					LogOnce($"BiglyBT/Transmission: cannot reach {_rpcUrl}: {ex.Message} - is BiglyBT running with the Web Remote plugin enabled?");
				}
				return null;
			}
			catch (Exception ex)
			{
				ClientSupport.Log($"BiglyBT/Transmission: {method} failed: {ex.Message}");
				return null;
			}
		}

		/// Logs a problem once instead of every few seconds.
		private void LogOnce(string message)
		{
			if (message != _lastProblem)
			{
				_lastProblem = message;
				ClientSupport.Log(message);
			}
		}

		private static Dictionary<string, object> Args(params object[] kv)
		{
			Dictionary<string, object> d = new Dictionary<string, object>();
			for (int i = 0; i + 1 < kv.Length; i += 2)
			{
				d[(string)kv[i]] = kv[i + 1];
			}
			return d;
		}

		private static string[] Ids(IEnumerable<string> hashes)
		{
			return hashes.Select(h => h.ToLowerInvariant()).ToArray();
		}

		// ---------------------------------------------------------------- connection / polling

		public bool Connect()
		{
			Dictionary<string, object> session = Call("session-get", null);
			if (session == null)
			{
				return false;
			}
			ClientSupport.Log("Connected to " + ClientSupport.ToStr(ClientSupport.Get(session, "version")) + " (RPC " + ClientSupport.ToStr(ClientSupport.Get(session, "rpc-version")) + ")");
			return UpdateData();
		}

		public void Disconnect()
		{
		}

		public bool UpdateData()
		{
			Dictionary<string, object> args = Call("torrent-get", Args("fields", Fields));
			ArrayList list = args == null ? null : ClientSupport.Get(args, "torrents") as ArrayList;
			if (list == null)
			{
				return false;
			}
			TorrentList current = new TorrentList();
			foreach (object o in list)
			{
				IDictionary d = o as IDictionary;
				if (d != null)
				{
					current.Add(ToTorrent(d));
				}
			}
			TorrentLabelList labels = new TorrentLabelList();
			labels.Add(new TorrentLabel { Name = "", Count = current.Count(t => t.Label == "") });
			foreach (IGrouping<string, Torrent> g in current.Where(t => t.Label != "").GroupBy(t => t.Label).OrderBy(g => g.Key))
			{
				labels.Add(new TorrentLabel { Name = g.Key, Count = g.Count() });
			}
			TorrentList previous;
			lock (_lock)
			{
				previous = _torrentsAll;
				_torrentsAll = current;
				_labels = labels;
			}
			ClientSupport.RaiseChanges(this, previous, current, _timerSeconds, TorrentAdded, TorrentCompleted, TorrentRemoved);
			return true;
		}

		private Torrent ToTorrent(IDictionary d)
		{
			Torrent t = new Torrent(this);
			t.Hash = ClientSupport.ToStr(ClientSupport.Get(d, "hashString")).ToUpperInvariant();
			t.Name = ClientSupport.ToStr(ClientSupport.Get(d, "name"));
			t.Size = ClientSupport.ToLong(ClientSupport.Get(d, "totalSize"));
			t.Progress = Math.Round(ClientSupport.ToDouble(ClientSupport.Get(d, "percentDone")) * 100.0, 2);
			t.Downloaded = ClientSupport.ToLong(ClientSupport.Get(d, "downloadedEver"));
			t.Uploaded = ClientSupport.ToLong(ClientSupport.Get(d, "uploadedEver"));
			t.Ratio = (long)Math.Round(Math.Max(0, ClientSupport.ToDouble(ClientSupport.Get(d, "uploadRatio"))) * 1000.0);
			t.UploadSpeed = ClientSupport.ToLong(ClientSupport.Get(d, "rateUpload"));
			t.DownloadSpeed = ClientSupport.ToLong(ClientSupport.Get(d, "rateDownload"));
			t.ETA = ClientSupport.ToInt(ClientSupport.Get(d, "eta"));
			ArrayList labels = ClientSupport.Get(d, "labels") as ArrayList;
			t.Label = labels != null && labels.Count > 0 ? ClientSupport.ToStr(labels[0]) : "";
			t.PeersConnected = ClientSupport.ToInt(ClientSupport.Get(d, "peersGettingFromUs"));
			t.SeedsConnected = ClientSupport.ToInt(ClientSupport.Get(d, "peersSendingToUs"));
			int seeders = 0, leechers = 0;
			ArrayList trackers = ClientSupport.Get(d, "trackerStats") as ArrayList;
			if (trackers != null)
			{
				foreach (IDictionary tr in trackers.OfType<IDictionary>())
				{
					seeders = Math.Max(seeders, ClientSupport.ToInt(ClientSupport.Get(tr, "seederCount")));
					leechers = Math.Max(leechers, ClientSupport.ToInt(ClientSupport.Get(tr, "leecherCount")));
				}
			}
			t.SeedsInSwarm = seeders;
			t.PeersInSwarm = leechers;
			t.Availability = 0;
			t.TorrentQueueOrder = ClientSupport.ToInt(ClientSupport.Get(d, "queuePosition"));
			t.Remaining = ClientSupport.ToLong(ClientSupport.Get(d, "leftUntilDone"));
			t.DownloadURL = "";
			t.RSSFeedURL = "";
			t.StreamID = "";
			t.AppUpdateURL = "";
			t.AddedOn = ClientSupport.ToInt(ClientSupport.Get(d, "addedDate"));
			t.CompletedOn = ClientSupport.ToInt(ClientSupport.Get(d, "doneDate"));
			t.TorrentSavePath = ClientSupport.ToStr(ClientSupport.Get(d, "downloadDir"));

			int status = ClientSupport.ToInt(ClientSupport.Get(d, "status"));
			int error = ClientSupport.ToInt(ClientSupport.Get(d, "error"));
			int bits = ClientSupport.Loaded | ClientSupport.Checked;
			string msg;
			switch (status)
			{
			case 0: // stopped
				if (t.Progress >= 100.0) { msg = "Finished"; } else { bits |= ClientSupport.Paused; msg = "Paused"; }
				break;
			case 1: case 2: bits |= ClientSupport.Checking; msg = "Checking"; break;
			case 3: bits |= ClientSupport.Started | ClientSupport.Queued; msg = "Queued"; break;
			case 4: bits |= ClientSupport.Started; msg = "Downloading"; break;
			case 5: bits |= ClientSupport.Started | ClientSupport.Queued; msg = "Queued for seeding"; break;
			case 6: bits |= ClientSupport.Started; msg = "Seeding"; break;
			default: msg = status.ToString(); break;
			}
			if (error != 0)
			{
				bits |= ClientSupport.Error;
				msg = "Error: " + ClientSupport.ToStr(ClientSupport.Get(d, "errorString"));
			}
			t.Status = bits;
			t.StatusMessage = msg;
			return t;
		}

		// ---------------------------------------------------------------- actions

		private bool Action(string method, IEnumerable<string> hashes)
		{
			return Call(method, Args("ids", Ids(hashes))) != null;
		}

		public void Pause(TorrentList torrents) { Action("torrent-stop", torrents.Select(t => t.Hash)); }
		public void Resume(TorrentList torrents) { Action("torrent-start", torrents.Select(t => t.Hash)); }
		public void Start(TorrentList torrents) { Action("torrent-start", torrents.Select(t => t.Hash)); }
		public void Stop(TorrentList torrents) { Action("torrent-stop", torrents.Select(t => t.Hash)); }
		public bool Pause(string Hash) { return Action("torrent-stop", new[] { Hash }); }
		public bool Resume(string Hash) { return Action("torrent-start", new[] { Hash }); }
		public bool Start(string Hash) { return Action("torrent-start", new[] { Hash }); }
		public bool Stop(string Hash) { return Action("torrent-stop", new[] { Hash }); }

		/// BiglyBT: "tagAdd" adds a BiglyBT tag (plain Transmission ignores it; MyTorrents also keeps its own list).
		public bool SetSaveFolder(string folder)
		{
			return Call("session-set", Args("download-dir", folder)) != null;
		}

		public bool AddTag(string hash, string tag)
		{
			return Call("torrent-set", Args("ids", Ids(new[] { hash }), "tagAdd", new[] { tag })) != null;
		}

		public bool Remove(string Hash, bool removeData)
		{
			return Call("torrent-remove", Args("ids", Ids(new[] { Hash }), "delete-local-data", removeData)) != null;
		}

		public void ChangeLabel(string torrentHash, string labelName)
		{
			string[] labels = string.IsNullOrEmpty(labelName) ? new string[0] : new[] { labelName };
			Call("torrent-set", Args("ids", Ids(new[] { torrentHash }), "labels", labels));
		}

		public List<Sohvaperuna.TorrentEngine.File> GetFiles(string torrentHash)
		{
			List<Sohvaperuna.TorrentEngine.File> result = new List<Sohvaperuna.TorrentEngine.File>();
			Dictionary<string, object> args = Call("torrent-get", Args("ids", Ids(new[] { torrentHash }), "fields", new[] { "files", "fileStats" }));
			ArrayList torrents = args == null ? null : ClientSupport.Get(args, "torrents") as ArrayList;
			IDictionary t = torrents != null && torrents.Count > 0 ? torrents[0] as IDictionary : null;
			if (t == null)
			{
				return result;
			}
			ArrayList files = ClientSupport.Get(t, "files") as ArrayList ?? new ArrayList();
			ArrayList stats = ClientSupport.Get(t, "fileStats") as ArrayList ?? new ArrayList();
			for (int i = 0; i < files.Count; i++)
			{
				IDictionary f = files[i] as IDictionary;
				IDictionary st = i < stats.Count ? stats[i] as IDictionary : null;
				if (f == null) continue;
				Sohvaperuna.TorrentEngine.File file = new Sohvaperuna.TorrentEngine.File(this);
				file.Filename = ClientSupport.ToStr(ClientSupport.Get(f, "name"));
				file.Filesize = ClientSupport.ToLong(ClientSupport.Get(f, "length"));
				file.Downloaded = ClientSupport.ToLong(ClientSupport.Get(f, "bytesCompleted"));
				bool wanted = st == null || !(ClientSupport.Get(st, "wanted") is bool) || (bool)ClientSupport.Get(st, "wanted");
				int prio = st == null ? 0 : ClientSupport.ToInt(ClientSupport.Get(st, "priority")); // -1 low, 0 normal, 1 high
				file.Priority = !wanted ? 0 : prio + 2; // 0 skip, 1 low, 2 normal, 3 high
				result.Add(file);
			}
			return result;
		}

		public Dictionary<string, int> FillPriorities()
		{
			return new Dictionary<string, int>
			{
				{ "High", 3 },
				{ "Normal", 2 },
				{ "Low", 1 },
				{ "Do Not Download", 0 }
			};
		}

		public bool SetPriority(string hash, int index, int priority)
		{
			int[] idx = { index };
			Dictionary<string, object> a = Args("ids", Ids(new[] { hash }));
			if (priority <= 0)
			{
				a["files-unwanted"] = idx;
			}
			else
			{
				a["files-wanted"] = idx;
				a[priority >= 3 ? "priority-high" : priority == 1 ? "priority-low" : "priority-normal"] = idx;
			}
			return Call("torrent-set", a) != null;
		}

		public List<DownloadDir> ListDirs()
		{
			Dictionary<string, object> s = Call("session-get", null);
			string dir = s == null ? null : ClientSupport.ToStr(ClientSupport.Get(s, "download-dir"));
			return string.IsNullOrEmpty(dir) ? null : new List<DownloadDir> { new DownloadDir { path = dir } };
		}

		public string GetSettings(string element)
		{
			Dictionary<string, object> s = Call("session-get", null);
			return s == null ? null : ClientSupport.ToStr(ClientSupport.Get(s, element));
		}

		// ---------------------------------------------------------------- adding torrents

		private bool Add(Dictionary<string, object> a, string label, string folder)
		{
			if (!string.IsNullOrEmpty(folder))
			{
				a["download-dir"] = folder;
			}
			if (!string.IsNullOrEmpty(label))
			{
				a["labels"] = new[] { label };
			}
			Dictionary<string, object> r = Call("torrent-add", a);
			if (r == null && a.ContainsKey("labels"))
			{
				a.Remove("labels"); // older servers reject the labels argument
				r = Call("torrent-add", a);
			}
			return r != null;
		}

		public bool StartDownload(byte[] content, string filename, string label, string hash, string option)
		{
			return StartDownloadToFolder(content, filename, label, hash, option, null);
		}

		public bool StartDownloadToFolder(byte[] content, string filename, string label, string hash, string option, string folder)
		{
			return Add(Args("metainfo", Convert.ToBase64String(content)), label, folder);
		}

		public bool StartDownload(Uri uri, string label, string hash, string option)
		{
			return StartDownloadToFolder(uri, label, hash, option, null);
		}

		public bool StartDownloadToFolder(Uri uri, string label, string hash, string option, string folder)
		{
			return Add(Args("filename", uri.OriginalString), label, folder);
		}

		// ---------------------------------------------------------------- legacy interface members

		public string GetResponse(string request)
		{
			Dictionary<string, object> r = Call(request, null);
			return r == null ? null : _js.Serialize(r);
		}

		public string GetResponse(string request, byte[] postData, string boundary, object parameters)
		{
			return GetResponse(request);
		}

		public bool ValidateUTorrentResponse(string response)
		{
			return response != null;
		}
	}
}
