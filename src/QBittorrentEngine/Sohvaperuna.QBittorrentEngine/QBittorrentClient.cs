using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Web;
using System.Web.Script.Serialization;
using Sohvaperuna.TorrentEngine;

namespace Sohvaperuna.QBittorrentEngine
{
	/// <summary>
	/// qBittorrent Web UI API v2 backend (qBittorrent 4.1+ and 5.x).
	/// Enable it in qBittorrent: Tools > Options > Web UI.
	/// Labels in MyTorrents map to qBittorrent categories.
	/// </summary>
	public class QBittorrentClient : ITorrentClient, ITorrentTagging, ISaveFolder
	{
		private readonly object _lock = new object();
		private readonly JavaScriptSerializer _js = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
		private readonly string _baseUrl;
		private readonly string _username;
		private readonly string _password;
		private readonly int _timerSeconds;
		private CookieContainer _cookies = new CookieContainer();
		private bool? _v5Api; // qBittorrent 5 renamed pause/resume to stop/start
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

		public QBittorrentClient(string host, int port, string username, string password, string outtimer)
		{
			_baseUrl = BuildBaseUrl(host, port);
			_username = (username ?? "").Trim(); // MediaPortal saves empty settings as a single space
			_password = (password ?? "").Trim();
			int.TryParse(outtimer, out _timerSeconds);
			ClientSupport.EnableModernTls();
		}

		/// Accepts "localhost", "192.168.1.10", "http://nas" or "https://nas:8443".
		internal static string BuildBaseUrl(string host, int port)
		{
			string h = ClientSupport.LocalHostToIPv4(host).TrimEnd('/');
			if (!h.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !h.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
			{
				h = "http://" + h;
			}
			Uri u = new Uri(h);
			bool explicitPort = h.IndexOf(':', h.IndexOf("//", StringComparison.Ordinal) + 2) > 0;
			UriBuilder b = new UriBuilder(u) { Port = explicitPort ? u.Port : port };
			return b.Uri.GetLeftPart(UriPartial.Authority) + u.AbsolutePath.TrimEnd('/');
		}

		// ---------------------------------------------------------------- connection

		public bool Connect()
		{
			if (!Login())
			{
				return false;
			}
			string version = Request("GET", "/api/v2/app/webapiVersion", null, null, retry: false);
			ClientSupport.Log("qBittorrent Web API version " + version);
			Version v;
			if (version != null && Version.TryParse(version.Trim(), out v))
			{
				_v5Api = v >= new Version(2, 11, 0);
			}
			return UpdateData();
		}

		public void Disconnect()
		{
			try
			{
				Request("POST", "/api/v2/auth/logout", "", null, retry: false);
			}
			catch
			{
			}
		}

		private bool Login()
		{
			_cookies = new CookieContainer();
			string body = "username=" + HttpUtility.UrlEncode(_username) + "&password=" + HttpUtility.UrlEncode(_password);
			string result = Request("POST", "/api/v2/auth/login", body, "application/x-www-form-urlencoded", retry: false);
			if (result == null)
			{
				ClientSupport.Log("qBittorrent: cannot reach Web UI at " + _baseUrl);
				return false;
			}
			if (result.Trim() == "Fails.")
			{
				ClientSupport.Log("qBittorrent: login failed - check username/password");
				return false;
			}
			return true; // "Ok." or empty when auth is bypassed for localhost
		}

		// 2026 (1.3.56): qBittorrent not answering on this computer (after a restart it often isn't running yet) -
		// start it, minimised, and say so once every few minutes instead of on every call (700 lines on 6 Nov)
		private static DateTime _lastStart = DateTime.MinValue, _lastSaid = DateTime.MinValue;
		private static readonly object _startLock = new object();

		/// true when the "can't connect" was dealt with here (logged now and then, qBittorrent started if needed)
		private bool NotRunning()
		{
			Uri u;
			if (!Uri.TryCreate(_baseUrl, UriKind.Absolute, out u) || !(u.IsLoopback || string.Equals(u.Host, Environment.MachineName, StringComparison.OrdinalIgnoreCase))) return false;
			lock (_startLock)
			{
				bool running = System.Diagnostics.Process.GetProcessesByName("qbittorrent").Length > 0;
				if (!running && DateTime.Now - _lastStart > TimeSpan.FromMinutes(5))
				{
					_lastStart = DateTime.Now;
					string exe = FindQBittorrent();
					if (exe != null)
					{
						try
						{
							System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exe) { WindowStyle = System.Diagnostics.ProcessWindowStyle.Minimized, UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(exe) });
							ClientSupport.Log("qBittorrent wasn't running - started it (" + exe + ")");
							_lastSaid = DateTime.Now;
							return true;
						}
						catch (Exception ex)
						{
							ClientSupport.Log("qBittorrent wasn't running and could not be started: " + ex.Message);
						}
					}
				}
				if (DateTime.Now - _lastSaid > TimeSpan.FromMinutes(5))
				{
					_lastSaid = DateTime.Now;
					ClientSupport.Log(running
						? "qBittorrent is running but its Web UI at " + _baseUrl + " isn't answering (Tools > Options > Web UI) - trying again"
						: "qBittorrent isn't running - trying again every few minutes");
				}
				return true;
			}
		}

		private static string FindQBittorrent()
		{
			try
			{
				foreach (Microsoft.Win32.RegistryView view in new[] { Microsoft.Win32.RegistryView.Registry64, Microsoft.Win32.RegistryView.Registry32 })
					using (Microsoft.Win32.RegistryKey hk = Microsoft.Win32.RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.LocalMachine, view))
					using (Microsoft.Win32.RegistryKey k = hk.OpenSubKey(@"SOFTWARE\qBittorrent"))
					{
						string dir = k?.GetValue("InstallLocation") as string;
						if (!string.IsNullOrEmpty(dir) && System.IO.File.Exists(Path.Combine(dir, "qbittorrent.exe"))) return Path.Combine(dir, "qbittorrent.exe");
					}
			}
			catch
			{
			}
			foreach (string pf in new[] { Environment.GetEnvironmentVariable("ProgramW6432"), Environment.GetEnvironmentVariable("ProgramFiles"), Environment.GetEnvironmentVariable("ProgramFiles(x86)") })
			{
				if (string.IsNullOrEmpty(pf)) continue;
				string exe = Path.Combine(pf, "qBittorrent", "qbittorrent.exe");
				if (System.IO.File.Exists(exe)) return exe;
			}
			return null;
		}

		/// <summary>Low level HTTP call. Returns null on network error.</summary>
		private string Request(string method, string path, object body, string contentType, bool retry = true)
		{
			try
			{
				HttpWebRequest req = (HttpWebRequest)WebRequest.Create(_baseUrl + path);
				req.Method = method;
				req.CookieContainer = _cookies;
				req.Timeout = 15000;
				req.KeepAlive = false;
				req.Referer = _baseUrl; // qBittorrent CSRF protection checks Referer/Origin
				req.Headers["Origin"] = _baseUrl;
				req.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
				if (body != null)
				{
					byte[] data = body as byte[] ?? Encoding.UTF8.GetBytes((string)body);
					req.ContentType = contentType ?? "application/x-www-form-urlencoded";
					req.ContentLength = data.Length;
					using (Stream s = req.GetRequestStream())
					{
						s.Write(data, 0, data.Length);
					}
				}
				using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
				using (StreamReader r = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
				{
					return r.ReadToEnd();
				}
			}
			catch (WebException ex)
			{
				HttpWebResponse resp = ex.Response as HttpWebResponse;
				if (resp != null && resp.StatusCode == HttpStatusCode.Forbidden && retry && Login())
				{
					return Request(method, path, body, contentType, retry: false);
				}
				if (resp != null && resp.StatusCode == HttpStatusCode.NotFound)
				{
					throw new NotSupportedException(path);
				}
				if (resp != null && resp.StatusCode == HttpStatusCode.Conflict)
				{
					return ""; // e.g. category already exists
				}
				if (ex.Status == WebExceptionStatus.ConnectFailure && NotRunning()) return null; // 2026 (1.3.56)
				ClientSupport.Log("qBittorrent request " + path + " failed: " + ex.Message);
				return null;
			}
			catch (NotSupportedException)
			{
				throw;
			}
			catch (Exception ex)
			{
				ClientSupport.Log("qBittorrent request " + path + " failed: " + ex.Message);
				return null;
			}
		}

		private string Post(string path, params string[] keyValues)
		{
			StringBuilder sb = new StringBuilder();
			for (int i = 0; i + 1 < keyValues.Length; i += 2)
			{
				if (sb.Length > 0) sb.Append('&');
				sb.Append(keyValues[i]).Append('=').Append(HttpUtility.UrlEncode(keyValues[i + 1] ?? ""));
			}
			return Request("POST", path, sb.ToString(), "application/x-www-form-urlencoded");
		}

		// ---------------------------------------------------------------- polling

		public bool UpdateData()
		{
			string json;
			try
			{
				json = Request("GET", "/api/v2/torrents/info", null, null);
			}
			catch (NotSupportedException)
			{
				ClientSupport.Log("qBittorrent: Web API v2 not found - qBittorrent 4.1 or newer is required");
				return false;
			}
			if (json == null)
			{
				return false;
			}
			ArrayList items;
			try
			{
				items = _js.Deserialize<ArrayList>(json);
			}
			catch (Exception ex)
			{
				ClientSupport.Log("qBittorrent: bad torrent list response: " + ex.Message);
				return false;
			}

			TorrentList current = new TorrentList();
			foreach (object o in items)
			{
				IDictionary d = o as IDictionary;
				if (d != null)
				{
					current.Add(ToTorrent(d));
				}
			}

			TorrentLabelList labels = new TorrentLabelList();
			labels.Add(new TorrentLabel { Name = "", Count = current.Count(t => t.Label == "") });
			try
			{
				string catJson = Request("GET", "/api/v2/torrents/categories", null, null);
				IDictionary cats = catJson == null ? null : _js.Deserialize<Dictionary<string, object>>(catJson);
				if (cats != null)
				{
					foreach (string name in cats.Keys)
					{
						labels.Add(new TorrentLabel { Name = name, Count = current.Count(t => t.Label == name) });
					}
				}
			}
			catch (Exception)
			{
			}
			foreach (string name in current.Select(t => t.Label).Distinct())
			{
				if (!labels.Exists(l => l.Name == name))
				{
					labels.Add(new TorrentLabel { Name = name, Count = current.Count(t => t.Label == name) });
				}
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
			string state = ClientSupport.ToStr(ClientSupport.Get(d, "state"));
			long size = ClientSupport.ToLong(ClientSupport.Get(d, "size"));
			long left = ClientSupport.ToLong(ClientSupport.Get(d, "amount_left"));
			Torrent t = new Torrent(this);
			t.Hash = ClientSupport.ToStr(ClientSupport.Get(d, "hash")).ToUpperInvariant();
			t.Name = ClientSupport.ToStr(ClientSupport.Get(d, "name"));
			t.Size = size;
			t.Progress = Math.Round(ClientSupport.ToDouble(ClientSupport.Get(d, "progress")) * 100.0, 2);
			t.Downloaded = ClientSupport.ToLong(ClientSupport.Get(d, "downloaded"));
			t.Uploaded = ClientSupport.ToLong(ClientSupport.Get(d, "uploaded"));
			t.Ratio = (long)Math.Round(ClientSupport.ToDouble(ClientSupport.Get(d, "ratio")) * 1000.0); // per-mille like uTorrent
			t.UploadSpeed = ClientSupport.ToLong(ClientSupport.Get(d, "upspeed"));
			t.DownloadSpeed = ClientSupport.ToLong(ClientSupport.Get(d, "dlspeed"));
			long eta = ClientSupport.ToLong(ClientSupport.Get(d, "eta"));
			t.ETA = eta >= 8640000 ? -1 : (int)eta; // 8640000 = qBittorrent's "infinity"
			t.Label = ClientSupport.ToStr(ClientSupport.Get(d, "category"));
			t.PeersConnected = ClientSupport.ToInt(ClientSupport.Get(d, "num_leechs"));
			t.PeersInSwarm = ClientSupport.ToInt(ClientSupport.Get(d, "num_incomplete"));
			t.SeedsConnected = ClientSupport.ToInt(ClientSupport.Get(d, "num_seeds"));
			t.SeedsInSwarm = ClientSupport.ToInt(ClientSupport.Get(d, "num_complete"));
			t.Availability = (int)Math.Round(ClientSupport.ToDouble(ClientSupport.Get(d, "availability")) * 1000.0);
			t.TorrentQueueOrder = ClientSupport.ToInt(ClientSupport.Get(d, "priority"));
			t.Remaining = left;
			t.DownloadURL = "";
			t.RSSFeedURL = "";
			t.StreamID = "";
			t.AppUpdateURL = "";
			t.AddedOn = ClientSupport.ToInt(ClientSupport.Get(d, "added_on"));
			t.CompletedOn = Math.Max(0, ClientSupport.ToInt(ClientSupport.Get(d, "completion_on")));
			t.TorrentSavePath = ClientSupport.ToStr(ClientSupport.Get(d, "save_path"));
			t.Tags = ClientSupport.ToStr(ClientSupport.Get(d, "tags"));
			MapState(state, t);
			return t;
		}

		private static void MapState(string state, Torrent t)
		{
			int s = ClientSupport.Loaded | ClientSupport.Checked;
			string msg;
			switch (state)
			{
			case "downloading": case "forcedDL": case "metaDL": case "forcedMetaDL":
				s |= ClientSupport.Started; msg = state.Contains("meta") ? "Fetching metadata" : "Downloading"; break;
			case "stalledDL":
				s |= ClientSupport.Started; msg = "Downloading (stalled)"; break;
			case "uploading": case "forcedUP":
				s |= ClientSupport.Started; msg = "Seeding"; break;
			case "stalledUP":
				s |= ClientSupport.Started; msg = "Seeding (idle)"; break;
			case "pausedDL": case "stoppedDL":
				s |= ClientSupport.Paused; msg = "Paused"; break;
			case "pausedUP": case "stoppedUP":
				msg = "Finished"; break;
			case "queuedDL": case "queuedUP":
				s |= ClientSupport.Started | ClientSupport.Queued; msg = "Queued"; break;
			case "checkingDL": case "checkingUP": case "checkingResumeData": case "allocating": case "moving":
				s |= ClientSupport.Checking; msg = "Checking"; break;
			case "error": case "missingFiles":
				s |= ClientSupport.Error; msg = state == "missingFiles" ? "Missing files" : "Error"; break;
			default:
				msg = state; break;
			}
			t.Status = s;
			t.StatusMessage = msg;
		}

		// ---------------------------------------------------------------- actions

		private static string Hashes(IEnumerable<string> hashes)
		{
			return string.Join("|", hashes.Select(h => h.ToLowerInvariant()).ToArray());
		}

		/// qBittorrent 5 uses /stop and /start, 4.x uses /pause and /resume.
		private bool StopOrStart(bool stop, string hashes)
		{
			string v5 = stop ? "/api/v2/torrents/stop" : "/api/v2/torrents/start";
			string v4 = stop ? "/api/v2/torrents/pause" : "/api/v2/torrents/resume";
			try
			{
				if (_v5Api != false)
				{
					string r = Post(v5, "hashes", hashes);
					_v5Api = true;
					return r != null;
				}
			}
			catch (NotSupportedException)
			{
				_v5Api = false;
			}
			try
			{
				return Post(v4, "hashes", hashes) != null;
			}
			catch (NotSupportedException)
			{
				return false;
			}
		}

		private static string HashesOf(TorrentList torrents)
		{
			return Hashes(torrents.Select(t => t.Hash));
		}

		public void Pause(TorrentList torrents) { StopOrStart(true, HashesOf(torrents)); }
		public void Resume(TorrentList torrents) { StopOrStart(false, HashesOf(torrents)); }
		public void Start(TorrentList torrents) { StopOrStart(false, HashesOf(torrents)); }
		public void Stop(TorrentList torrents) { StopOrStart(true, HashesOf(torrents)); }
		public bool Pause(string Hash) { return StopOrStart(true, Hashes(new[] { Hash })); }
		public bool Resume(string Hash) { return StopOrStart(false, Hashes(new[] { Hash })); }
		public bool Start(string Hash) { return StopOrStart(false, Hashes(new[] { Hash })); }
		public bool Stop(string Hash) { return StopOrStart(true, Hashes(new[] { Hash })); }

		public bool SetSaveFolder(string folder)
		{
			string json = new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(new Dictionary<string, object> { { "save_path", folder } });
			return Post("/api/v2/app/setPreferences", "json", json) != null;
		}

		public bool AddTag(string hash, string tag)
		{
			return Post("/api/v2/torrents/addTags", "hashes", Hashes(new[] { hash }), "tags", tag) != null;
		}

		public bool Remove(string Hash, bool removeData)
		{
			return Post("/api/v2/torrents/delete", "hashes", Hashes(new[] { Hash }), "deleteFiles", removeData ? "true" : "false") != null;
		}

		public void ChangeLabel(string torrentHash, string labelName)
		{
			if (!string.IsNullOrEmpty(labelName))
			{
				Post("/api/v2/torrents/createCategory", "category", labelName); // 409 if it exists - fine
			}
			Post("/api/v2/torrents/setCategory", "hashes", Hashes(new[] { torrentHash }), "category", labelName ?? "");
		}

		public List<Sohvaperuna.TorrentEngine.File> GetFiles(string torrentHash)
		{
			List<Sohvaperuna.TorrentEngine.File> list = new List<Sohvaperuna.TorrentEngine.File>();
			string json = Request("GET", "/api/v2/torrents/files?hash=" + torrentHash.ToLowerInvariant(), null, null);
			if (json == null)
			{
				return list;
			}
			foreach (object o in _js.Deserialize<ArrayList>(json))
			{
				IDictionary d = o as IDictionary;
				if (d == null) continue;
				long size = ClientSupport.ToLong(ClientSupport.Get(d, "size"));
				Sohvaperuna.TorrentEngine.File f = new Sohvaperuna.TorrentEngine.File(this);
				f.Filename = ClientSupport.ToStr(ClientSupport.Get(d, "name"));
				f.Filesize = size;
				f.Downloaded = (long)Math.Round(size * ClientSupport.ToDouble(ClientSupport.Get(d, "progress")));
				f.Priority = ClientSupport.ToInt(ClientSupport.Get(d, "priority"));
				list.Add(f);
			}
			return list;
		}

		public Dictionary<string, int> FillPriorities()
		{
			return new Dictionary<string, int>
			{
				{ "Maximum", 7 },
				{ "High", 6 },
				{ "Normal", 1 },
				{ "Do Not Download", 0 }
			};
		}

		public bool SetPriority(string hash, int index, int priority)
		{
			return Post("/api/v2/torrents/filePrio", "hash", hash.ToLowerInvariant(), "id", index.ToString(), "priority", priority.ToString()) != null;
		}

		public List<DownloadDir> ListDirs()
		{
			try
			{
				string path = Request("GET", "/api/v2/app/defaultSavePath", null, null);
				if (!string.IsNullOrEmpty(path))
				{
					return new List<DownloadDir> { new DownloadDir { path = path, available = 0 } };
				}
			}
			catch
			{
			}
			return null;
		}

		public string GetSettings(string element)
		{
			try
			{
				string json = Request("GET", "/api/v2/app/preferences", null, null);
				Dictionary<string, object> prefs = json == null ? null : _js.Deserialize<Dictionary<string, object>>(json);
				return prefs != null && prefs.ContainsKey(element) ? ClientSupport.ToStr(prefs[element]) : null;
			}
			catch
			{
				return null;
			}
		}

		// ---------------------------------------------------------------- adding torrents

		private bool Add(ClientSupport.Multipart form, string label, string folder)
		{
			if (!string.IsNullOrEmpty(label))
			{
				form.Field("category", label);
			}
			if (!string.IsNullOrEmpty(folder))
			{
				form.Field("savepath", folder);
				form.Field("autoTMM", "false");
			}
			string r = Request("POST", "/api/v2/torrents/add", form.ToArray(), form.ContentType);
			if (r == null || r.Trim() == "Fails.")
			{
				ClientSupport.Log("qBittorrent: add torrent failed (" + r + ")");
				return false;
			}
			return true;
		}

		public bool StartDownload(byte[] content, string filename, string label, string hash, string option)
		{
			return StartDownloadToFolder(content, filename, label, hash, option, null);
		}

		public bool StartDownloadToFolder(byte[] content, string filename, string label, string hash, string option, string folder)
		{
			ClientSupport.Multipart form = new ClientSupport.Multipart();
			form.File("torrents", string.IsNullOrEmpty(filename) ? "file.torrent" : filename + ".torrent", content);
			return Add(form, label, folder);
		}

		public bool StartDownload(Uri uri, string label, string hash, string option)
		{
			return StartDownloadToFolder(uri, label, hash, option, null);
		}

		public bool StartDownloadToFolder(Uri uri, string label, string hash, string option, string folder)
		{
			ClientSupport.Multipart form = new ClientSupport.Multipart();
			form.Field("urls", uri.OriginalString);
			return Add(form, label, folder);
		}

		// ---------------------------------------------------------------- legacy interface members

		public string GetResponse(string request)
		{
			return Request("GET", request, null, null);
		}

		public string GetResponse(string request, byte[] postData, string boundary, object parameters)
		{
			return Request("POST", request, postData, "multipart/form-data; boundary=" + boundary);
		}

		public bool ValidateUTorrentResponse(string response)
		{
			return response != null;
		}
	}
}
