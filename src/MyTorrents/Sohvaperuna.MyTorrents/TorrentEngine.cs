using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Timers;
using MediaPortal.Configuration;
using MediaPortal.Database;
using Sohvaperuna.AzureusEngine;
using Sohvaperuna.DelugeEngine;
using Sohvaperuna.QBittorrentEngine;
using Sohvaperuna.TransmissionEngine;
using Sohvaperuna.TorrentEngine;
using Sohvaperuna.TorrentSearch;
using Sohvaperuna.UTorrentEngine;
using SQLite.NET;

namespace Sohvaperuna.MyTorrents
{
	internal class TorrentEngine
	{
		private static TorrentEngine _instance;

		private ITorrentClient _torrentSession;

		private Timer _updateTimer;

		public BackgroundWorker _bgWorker;

		private List<string> _hashQueue;

		private ContentCache _contentCache;

		private bool _isConnected;

		public bool IsConnected
		{
			get
			{
				return _isConnected;
			}
			set
			{
				_isConnected = value;
			}
		}

		public double UpdateTimer
		{
			set
			{
				_updateTimer.Stop();
				_updateTimer.Interval = value;
				_updateTimer.Start();
			}
		}

		public ITorrentClient TorrentSession => _torrentSession;

		public List<Torrent> TorrentsAll
		{
			get
			{
				List<Torrent> list = new List<Torrent>();
				foreach (Torrent item in _torrentSession.TorrentsAll)
				{
					list.Add(item);
				}
				return list;
			}
		}

		public event SearchCompleted SearchCompleted;

		public event TorrentAdded TorrentAdded;

		public event TorrentCompleted TorrentCompleted;

		public event TorrentRemoved TorrentRemoved;

		public event TorrentsUpdated TorrentsUpdated;

		public List<Torrent> GetTorrents(string Status = "All", string SortOrder = "Default", string LabelFilter = null)
		{
			List<Torrent> list = new List<Torrent>();
			List<Torrent> result = new List<Torrent>();
			foreach (Torrent item in _torrentSession.TorrentsAll)
			{
				int num = 0;
				num = 0;
				num = ((!(Status == "All")) ? num : (++num));
				num = ((!(Status == "Downloading")) ? num : ((!(item.Progress < 100.0) || item.HasStatus(32)) ? num : (++num))); // 2026: not the paused ones
				num = ((!(Status == "Seeding")) ? num : ((!item.HasStatus(1) || item.Progress < 100.0) ? num : (++num))); // 2026: finished ones only (downloads are "started" too)
				num = ((!(Status == "Active")) ? num : ((item.UploadSpeed == 0 && item.DownloadSpeed == 0) ? num : (++num)));
				num = ((!(Status == "Finished")) ? num : ((item.Progress != 100.0) ? num : (++num)));
				num = ((!(Status == "Paused")) ? num : ((!item.HasStatus(32)) ? num : (++num)));
				num = ((!(Status == "Inactive")) ? num : ((item.UploadSpeed != 0 || item.DownloadSpeed != 0) ? num : (++num)));
				num = ((!(Status == "Stopped")) ? num : (item.HasStatus(1) ? num : (++num)));
				num = ((!(Status == "Errors")) ? num : ((!item.HasStatus(16)) ? num : (++num)));
				if (num > 0 && (LabelFilter == null || item.Label == LabelFilter))
				{
					list.Add(item);
				}
			}
			switch (SortOrder)
			{
			case "Default":
				result = list;
				break;
			case "Name ASC":
				result = list.OrderBy((Torrent a) => a.Name).ToList();
				break;
			case "Name DESC":
				result = list.OrderByDescending((Torrent a) => a.Name).ToList();
				break;
			case "Size ASC":
				result = list.OrderBy((Torrent a) => a.Size).ToList();
				break;
			case "Size DESC":
				result = list.OrderByDescending((Torrent a) => a.Size).ToList();
				break;
			case "Date Added ASC":
				result = list.OrderBy((Torrent a) => a.AddedOn).ToList();
				break;
			case "Date Added DESC":
				result = list.OrderByDescending((Torrent a) => a.AddedOn).ToList();
				break;
			}
			return result;
		}

		/// 2026: a connection to the torrent program on its own (no timers, no events) - used by the
		/// Downloads tab in MyTorrents Configuration, which runs without MediaPortal.
		public static ITorrentClient CreateClient(string host, int port, string username, string password, Backend backend)
		{
			string timer = Configuration.Instance().Settings["Torrent.Timer.Outside"] as string;
			switch (backend)
			{
			case Backend.Azureus:
				return new AzureusClient(host, port, username, password);
			case Backend.uTorrent:
				return new UTorrentClient(host, port, username, password, timer);
			case Backend.Deluge:
				return new DelugeClient(host, port, password, timer);
			case Backend.qBittorrent:
				return new QBittorrentClient(host, port, username, password, timer);
			default:
				return new TransmissionClient(host, port, username, password, timer);
			}
		}

		private TorrentEngine(string host, int port, string username, string password, Backend backend)
		{
			_torrentSession = CreateClient(host, port, username, password, backend);
			Log.Instance().Print("backend is " + backend);
			_torrentSession.TorrentAdded += _torrentSession_TorrentAdded;
			_torrentSession.TorrentRemoved += _torrentSession_TorrentRemoved;
			_torrentSession.TorrentCompleted += _torrentSession_TorrentCompleted;
			_hashQueue = new List<string>();
			_contentCache = new ContentCache(100, new TimeSpan(0, 30, 0));
			_updateTimer = new Timer(10000.0);
			_updateTimer.Elapsed += _updateTimer_Elapsed;
			_updateTimer.Start();
		}

		private void _updateTimer_Elapsed(object sender, ElapsedEventArgs e)
		{
			try
			{
				if (_torrentSession != null)
				{
					if (!_torrentSession.UpdateData())
					{
						IsConnected = false;
					}
					else
					{
						IsConnected = true;
					}
					if (this.TorrentsUpdated != null)
					{
						this.TorrentsUpdated();
					}
				}
			}
			catch (Exception e2)
			{
				Log.Instance().Print("timer elapsed");
				Log.Instance().Print(e2);
			}
		}

		private void _torrentSession_TorrentRemoved(Torrent torrent)
		{
			if (this.TorrentRemoved != null)
			{
				this.TorrentRemoved(torrent);
			}
		}

		private void _torrentSession_TorrentCompleted(Torrent torrent)
		{
			if (this.TorrentCompleted != null)
			{
				this.TorrentCompleted(torrent);
			}
		}

		private void _torrentSession_TorrentAdded(Torrent torrent)
		{
			foreach (string item in _hashQueue)
			{
				if (torrent.Hash.ToLower().CompareTo(item.ToLower()) == 0)
				{
					AddToAlreadyDownloaded(item);
					_hashQueue.Remove(item);
					break;
				}
			}
			if (this.TorrentAdded != null)
			{
				this.TorrentAdded(torrent);
			}
		}

		public static bool Initialize(string host, int port, string username, string password, Backend backend)
		{
			try
			{
				_instance = new TorrentEngine(host, port, username, password, backend);
				_instance._torrentSession.Connect();
				_instance.IsConnected = true;
				Log.Instance().Print("instance connected");
				return true;
			}
			catch (Exception e)
			{
				Log.Instance().Print("Could not connect to " + host + ":" + port);
				Log.Instance().Print(e);
				_instance.IsConnected = false;
				return false;
			}
		}

		public static TorrentEngine Instance()
		{
			return _instance;
		}

		public void SearchTorrent(string searchEngine, string searchString, string sortOrder, object id, string kind = "")
		{
			Log.Instance().Print($"search torrents {searchEngine} {searchString} {sortOrder}" + (string.IsNullOrEmpty(kind) ? "" : $" (kind {kind})"));
			TorrentSearchParameters argument = new TorrentSearchParameters(searchEngine, searchString, sortOrder, null) { Kind = kind ?? "" };
			_bgWorker = new BackgroundWorker();
			_bgWorker.DoWork += _bgWorker_DoWork;
			_bgWorker.RunWorkerCompleted += _bgWorker_RunWorkerCompleted;
			_bgWorker.ProgressChanged += _bgWorker_ProgressChanged;
			_bgWorker.WorkerSupportsCancellation = true;
			_bgWorker.RunWorkerAsync(argument);
		}

		private void _bgWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
		{
			TorrentMatchList torrentMatchList = e.Result as TorrentMatchList;
			if (this.SearchCompleted != null)
			{
				this.SearchCompleted(torrentMatchList);
				Log.Instance().Print("search complete :" + torrentMatchList.Count);
			}
		}

		private void _bgWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
		{
		}

		private void _bgWorker_DoWork(object sender, DoWorkEventArgs e)
		{
			TorrentSearchParameters torrentSearchParameters = e.Argument as TorrentSearchParameters;
			TorrentSearchEngine torrentSearchEngine = null;
			try
			{
				torrentSearchEngine = new TorrentSearchEngine(Configuration.Instance().Settings["TorrentSearch.Config"] as string);
			}
			catch
			{
				Log.Instance().Print("Mytorrents.xml cofig file loading failed!");
			}
			TorrentMatchList result = null;
			try
			{
				ITorrentSearch torrentSearch = torrentSearchEngine?.Create(torrentSearchParameters.SearchEngine);
				if (torrentSearch == null)
				{
					Log.Instance().Print("Search engine not found: " + torrentSearchParameters.SearchEngine);
				}
				else
				{
					SearchContext.Kind = torrentSearchParameters.Kind; // TV / Movie: Jackett category + result filter
					result = torrentSearch.Search(torrentSearchParameters.SearchString, torrentSearchParameters.SortOrder, null);
					result = SearchContext.Filter(result);
					if (torrentSearchParameters.Kind == SearchContext.Movie) result = Films.Apply(result); // 2026 (1.3.40): film quality / size
				}
			}
			catch (Exception ex)
			{
				Log.Instance().Print("Search failed: " + ex.Message);
			}
			e.Result = result ?? new TorrentMatchList(null);
		}

		public bool StartDownloadingToFolder(out string hash, string url, string label, bool force, string cookies, string id, string Downoption, string option, string folder, string linktype)
		{
			switch (linktype)
			{
			case "torrent":
				return StartTorrentDownloading(out hash, url, label, force, cookies, id, Downoption, option, folder);
			case "magnet":
				return StartMagnetDownloading(out hash, url, label, force, cookies, id, Downoption, option, folder);
			default:
				hash = string.Empty;
				return false;
			}
		}

		public bool StartMagnetDownloading(out string hash, string url, string label, bool force, string cookies, string id, string Downoption, string option, string folder)
		{
			hash = MagnetHash(url);
			if (string.IsNullOrEmpty(folder))
			{
				return _torrentSession.StartDownload(new Uri(url), label, hash, option);
			}
			return _torrentSession.StartDownloadToFolder(new Uri(url), label, hash, option, folder);
		}

		public bool StartTorrentDownloading(out string hash, string url, string label, bool force, string cookies, string id, string Downoption, string option, string folder)
		{
			TorrentFile torrentFile = null;
			byte[] array = null;
			try
			{
				array = TorrentData(url, cookies, Downoption);
				MemoryStream memoryStream = new MemoryStream(array);
				if (memoryStream == null)
				{
					throw new Exception();
				}
				torrentFile = new TorrentFile(memoryStream);
				if (torrentFile == null)
				{
					throw new Exception();
				}
			}
			catch (MagnetRedirectException redirect)
			{
				// Jackett/indexer link that resolves to a magnet link instead of a .torrent file
				Log.Instance().Print($"Link [{url}] redirected to a magnet link.");
				return StartMagnetDownloading(out hash, redirect.Magnet, label, force, cookies, id, Downoption, option, folder);
			}
			catch (Exception e)
			{
				MyTorrents.Error("Error", "Could not download the torrent file.");
				Log.Instance().Print($"Could not download torrent [{url}].");
				Log.Instance().Print(e);
				hash = string.Empty;
				return false;
			}
			hash = torrentFile.Hash;
			if (TorrentByHash(hash) != null)
			{
				MyTorrents.Error("Error", "Torrent already exists.");
				hash = string.Empty;
				return false;
			}
			string name = torrentFile.Name;
			torrentFile.Close();
			if (!force && IsAlreadyDownloaded(hash))
			{
				Log.Instance().Print("Already downloaded!");
				return false;
			}
			Log.Instance().Print($"Download [{url}] started.");
			bool flag = false;
			flag = ((!(folder == "")) ? _torrentSession.StartDownloadToFolder(array, name, label, hash, option, folder) : _torrentSession.StartDownload(array, name, label, hash, option));
			if (!flag)
			{
				MyTorrents.Error("Error", "An unknown error has occurred.");
			}
			if (flag && !force)
			{
				_hashQueue.Add("");
			}
			return flag;
		}

		public TorrentFile GetTorrentFile(string url, string cookies, string Downoption)
		{
			byte[] buffer = TorrentData(url, cookies, Downoption);
			MemoryStream memoryStream = new MemoryStream(buffer);
			if (memoryStream == null)
			{
				return null;
			}
			return new TorrentFile(memoryStream);
		}

		/// Thrown by TorrentData when a download link redirects to a magnet: URI.
		public class MagnetRedirectException : Exception
		{
			public string Magnet { get; private set; }

			public MagnetRedirectException(string magnet)
				: base("Redirected to magnet link")
			{
				Magnet = magnet;
			}
		}

		/// Extracts the info-hash (upper-case hex) from a magnet link. Handles hex and base32 hashes.
		public static string MagnetHash(string magnet)
		{
			System.Text.RegularExpressions.Match m = System.Text.RegularExpressions.Regex.Match(magnet ?? "", "urn:btih:([A-Za-z0-9]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
			if (!m.Success)
			{
				return string.Empty;
			}
			string h = m.Groups[1].Value;
			if (h.Length == 32)
			{
				// base32 -> hex
				const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
				System.Text.StringBuilder hex = new System.Text.StringBuilder();
				int buffer = 0, bits = 0;
				foreach (char c in h.ToUpperInvariant())
				{
					int v = alphabet.IndexOf(c);
					if (v < 0) return string.Empty;
					buffer = (buffer << 5) | v;
					bits += 5;
					if (bits >= 8)
					{
						bits -= 8;
						hex.Append(((buffer >> bits) & 0xFF).ToString("X2"));
					}
				}
				return hex.ToString();
			}
			return h.ToUpperInvariant();
		}

		public byte[] TorrentData(string url, string cookies, string Downoption)
		{
			ClientSupport.EnableModernTls();
			string current = url;
			for (int redirects = 0; redirects < 8; redirects++)
			{
				if (current.StartsWith("magnet:", StringComparison.OrdinalIgnoreCase))
				{
					throw new MagnetRedirectException(current);
				}
				HttpWebRequest request = (HttpWebRequest)WebRequest.Create(current);
				request.AllowAutoRedirect = false; // we follow redirects ourselves so magnet: targets can be caught
				request.Timeout = 60000;
				request.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0 Safari/537.36";
				request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
				if (!string.IsNullOrEmpty(cookies))
				{
					request.Headers.Add("Cookie", cookies);
				}
				if (!string.IsNullOrEmpty(Downoption))
				{
					request.Headers.Add(Downoption);
				}
				Log.Instance().Print("downloading torrent file " + current);
				HttpWebResponse response;
				try
				{
					response = (HttpWebResponse)request.GetResponse();
				}
				catch (WebException ex) when (ex.Response is HttpWebResponse r && (int)r.StatusCode >= 300 && (int)r.StatusCode < 400)
				{
					response = r;
				}
				using (response)
				{
					int code = (int)response.StatusCode;
					if (code >= 300 && code < 400)
					{
						string location = response.Headers[HttpResponseHeader.Location];
						if (string.IsNullOrEmpty(location))
						{
							throw new WebException("Redirect without location");
						}
						current = location.StartsWith("magnet:", StringComparison.OrdinalIgnoreCase) ? location : new Uri(new Uri(current), location).ToString();
						continue;
					}
					using (MemoryStream ms = new MemoryStream())
					{
						response.GetResponseStream().CopyTo(ms);
						return ms.ToArray();
					}
				}
			}
			throw new WebException("Too many redirects");
		}

		private void AddToAlreadyDownloaded(string hash)
		{
			SQLiteClient sQLiteClient = new SQLiteClient(Config.GetFile(Config.Dir.Database, "MyTorrents.db3"));
			DatabaseUtility.SetPragmas(sQLiteClient);
			if (!DatabaseUtility.TableExists(sQLiteClient, "Downloaded"))
			{
				SQLiteResultSet sQLiteResultSet = sQLiteClient.Execute("CREATE TABLE Downloaded (Hash CHAR(4096) );");
			}
			sQLiteClient.Execute($"INSERT INTO Downloaded VALUES (\"{hash}\");");
			sQLiteClient.Close();
		}

		public bool IsAlreadyDownloaded(string hash)
		{
			SQLiteClient sQLiteClient = new SQLiteClient(Config.GetFile(Config.Dir.Database, "MyTorrents.db3"));
			DatabaseUtility.SetPragmas(sQLiteClient);
			if (!DatabaseUtility.TableExists(sQLiteClient, "Downloaded"))
			{
				return false;
			}
			SQLiteResultSet sQLiteResultSet = sQLiteClient.Execute($"SELECT * FROM Downloaded WHERE Hash=\"{hash}\";");
			sQLiteClient.Close();
			return sQLiteResultSet.Rows.Count >= 1;
		}

		internal Torrent TorrentByHash(string hash)
		{
			return _torrentSession.TorrentsAll.TorrentByHash(hash);
		}

		internal void ChangeLabel(string torrentHash, string labelName)
		{
			_torrentSession.ChangeLabel(torrentHash, labelName);
		}
	}
}
