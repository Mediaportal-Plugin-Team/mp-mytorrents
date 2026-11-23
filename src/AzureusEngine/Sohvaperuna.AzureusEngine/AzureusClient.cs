using System;
using System.Collections.Generic;
using AzureusEngine.AzCommunication;
using Sohvaperuna.TorrentEngine;

namespace Sohvaperuna.AzureusEngine
{
	public class AzureusClient : ITorrentClient
	{
		private Dictionary<string, Download> torrentMapping = new Dictionary<string, Download>();

		private object lockUpdate = new object();

		private string _address;

		private int _port;

		private string _username;

		private string _password;

		private TorrentList _torrentsAll = new TorrentList();

		private TorrentList _torrentsActive = new TorrentList();

		private TorrentLabelList _labels = new TorrentLabelList();

		private Az_Protocol prot;

		public TorrentLabelList Labels => _labels;

		public TorrentList TorrentsAll
		{
			get
			{
				TorrentList torrentList = new TorrentList();
				torrentList.AddRange(_torrentsAll);
				return torrentList;
			}
		}

		public TorrentList TorrentsActive
		{
			get
			{
				TorrentList torrentList = new TorrentList();
				torrentList.AddRange(_torrentsActive);
				return torrentList;
			}
		}

		public event TorrentAdded TorrentAdded;

		public event TorrentRemoved TorrentRemoved;

		public event TorrentCompleted TorrentCompleted;

		public Dictionary<string, int> FillPriorities()
		{
			Dictionary<string, int> dictionary = new Dictionary<string, int>();
			dictionary.Add("High", 3);
			dictionary.Add("Normal", 2);
			dictionary.Add("Low", 1);
			dictionary.Add("Do Not Download", 0);
			return dictionary;
		}

		public AzureusClient(string address, int port, string username, string password)
		{
			_address = address;
			_port = port;
			_username = username;
			_password = password;
			prot = new Az_Protocol(address, port.ToString());
		}

		public bool Connect()
		{
			return prot.InitConnection();
		}

		public bool ValidateUTorrentResponse(string response)
		{
			return true;
		}

		public void Disconnect()
		{
			_address = null;
		}

		private Sohvaperuna.TorrentEngine.Torrent toUtorrent(Download download)
		{
			Sohvaperuna.TorrentEngine.Torrent torrent = new Sohvaperuna.TorrentEngine.Torrent(this);
			torrent.Availability = (int)download.Stats.Availabilty;
			torrent.Downloaded = (long)download.Stats.Downloaded * 1048576;
			torrent.DownloadSpeed = (long)download.Stats.DownloadAverage;
			if (download.Stats.ETA == "")
			{
				torrent.ETA = 0;
			}
			else if (download.State == DownloadState.ST_DOWNLOADING)
			{
				string[] array = download.Stats.ETA.Split(' ');
				int days = 0;
				int hours = 0;
				int minutes = 0;
				int seconds = 0;
				string[] array2 = array;
				foreach (string text in array2)
				{
					if (text.Contains("d"))
					{
						days = Convert.ToInt32(text.Replace("d", ""));
					}
					if (text.Contains("m"))
					{
						minutes = Convert.ToInt32(text.Replace("m", ""));
					}
					if (text.Contains("h"))
					{
						hours = Convert.ToInt32(text.Replace("h", ""));
					}
					if (text.Contains("s"))
					{
						seconds = Convert.ToInt32(text.Replace("s", ""));
					}
				}
				TimeSpan timeSpan = new TimeSpan(days, hours, minutes, seconds, 0);
				torrent.ETA = timeSpan.Days * 24 * 3600 + 3600 * timeSpan.Hours + timeSpan.Minutes * 60 + timeSpan.Seconds;
			}
			else
			{
				torrent.ETA = 0;
			}
			if (download.State == DownloadState.ST_DOWNLOADING || download.State == DownloadState.ST_SEEDING)
			{
				torrent.Status = 1;
			}
			if (download.State == DownloadState.ST_PREPARING)
			{
				torrent.Status = 2;
			}
			if (download.State == DownloadState.ST_QUEUED)
			{
				torrent.Status = 64;
			}
			if (download.State == DownloadState.ST_READY)
			{
				torrent.Status = 8;
			}
			if (download.State == DownloadState.ST_STOPPED || DownloadState.ST_STOPPING == download.State)
			{
				torrent.Status = 32;
			}
			if (download.State == DownloadState.ST_ERROR)
			{
				torrent.Status = 16;
			}
			torrent.Hash = download.Torrent.Hash;
			torrent.Label = download.Category;
			torrent.Name = download.Torrent.Name;
			torrent.PeersConnected = download.Scrape_Result.Seeders + download.Scrape_Result.Leechers;
			torrent.Progress = download.Stats.Completed;
			torrent.Ratio = (long)download.Stats.Share_Ratio * 100;
			torrent.Remaining = download.Remaining;
			torrent.SeedsConnected = download.Scrape_Result.Seeders;
			torrent.SeedsInSwarm = 0;
			torrent.Size = (long)download.Torrent.Size * 1048576;
			torrent.Uploaded = (long)download.Stats.Uploaded * 1048576;
			torrent.UploadSpeed = (long)download.Stats.UploadAverage;
			return torrent;
		}

		public bool UpdateData()
		{
			lock (lockUpdate)
			{
				prot.DownloadManager.Update();
				TorrentList torrentList = new TorrentList();
				TorrentList torrentList2 = new TorrentList();
				Dictionary<string, Download> dictionary = new Dictionary<string, Download>();
				Dictionary<string, int> dictionary2 = new Dictionary<string, int>();
				foreach (Download download in prot.DownloadManager.Downloads)
				{
					Sohvaperuna.TorrentEngine.Torrent torrent = toUtorrent(download);
					if (!dictionary2.ContainsKey(torrent.Label))
					{
						dictionary2.Add(torrent.Label, 1);
					}
					else
					{
						dictionary2[torrent.Label] = dictionary2[torrent.Label] + 1;
					}
					torrentList.Add(torrent);
					if (!torrentMapping.ContainsKey(torrent.Hash))
					{
						torrentMapping.Add(torrent.Hash, download);
						if (this.TorrentAdded != null)
						{
							this.TorrentAdded(torrent);
						}
					}
					if (torrent.Status == 1)
					{
						torrentList2.Add(torrent);
					}
				}
				foreach (Sohvaperuna.TorrentEngine.Torrent item in torrentList)
				{
					Sohvaperuna.TorrentEngine.Torrent torrent2 = _torrentsAll.TorrentByHash(item.Hash);
					if (item != null)
					{
						_torrentsAll.Remove(item);
					}
				}
				_torrentsActive = torrentList2;
				_torrentsAll = torrentList;
				TorrentLabelList torrentLabelList = new TorrentLabelList();
				foreach (string key in dictionary2.Keys)
				{
					TorrentLabel torrentLabel = new TorrentLabel();
					torrentLabel.Name = key;
					torrentLabel.Count = dictionary2[key];
					torrentLabelList.Add(torrentLabel);
				}
				_labels = torrentLabelList;
			}
			return true;
		}

		public string GetResponse(string request)
		{
			string key = request.Substring(request.IndexOf("hash=")).Replace("hash=", "");
			if (request.Contains("remove"))
			{
				if (request.Contains("removedata&") && torrentMapping.ContainsKey(key))
				{
					torrentMapping[key].Remove(RemoveData: true);
				}
				if (request.Contains("remove&") && torrentMapping.ContainsKey(key))
				{
					torrentMapping[key].Remove(RemoveData: false);
				}
			}
			if (request.Contains("start") && torrentMapping.ContainsKey(key))
			{
				torrentMapping[key].Start();
			}
			if (request.Contains("stop") && torrentMapping.ContainsKey(key))
			{
				torrentMapping[key].Stop();
			}
			if (request.Contains("pause") && torrentMapping.ContainsKey(key))
			{
				torrentMapping[key].Pause();
			}
			return "";
		}

		public string GetResponse(string request, byte[] postData, string boundary, object parameters)
		{
			return "";
		}

		public bool StartDownloadToFolder(Uri uri, string label, string hash, string option, string folder)
		{
			return StartDownload(uri, label, hash, option);
		}

		public bool StartDownloadToFolder(byte[] content, string filename, string label, string hash, string option, string folder)
		{
			return StartDownload(content, filename, label, hash, option);
		}

		public bool StartDownload(byte[] content, string filename, string label, string hash, string option)
		{
			try
			{
				global::AzureusEngine.AzCommunication.Torrent torrent = prot.TorrentManager.createFromBEncodedData(content);
				Download download = prot.DownloadManager.AddDownload(torrent);
				download.Category = label;
			}
			catch (Exception)
			{
				return false;
			}
			return true;
		}

		public TorrentList TorrentsByLabel(TorrentLabel _selectedLabel)
		{
			TorrentList torrentList = new TorrentList();
			foreach (Sohvaperuna.TorrentEngine.Torrent item in _torrentsAll)
			{
				if (_selectedLabel.Name.CompareTo(item.Label) == 0)
				{
					torrentList.Add(item);
				}
			}
			return torrentList;
		}

		public void ChangeLabel(string torrentHash, string labelName)
		{
			if (torrentMapping.ContainsKey(torrentHash))
			{
				Download download = torrentMapping[torrentHash];
				download.Category = labelName;
			}
		}

		public List<File> GetFiles(string torrentHash)
		{
			List<File> list = new List<File>();
			if (!torrentMapping.ContainsKey(torrentHash))
			{
				return null;
			}
			Download download = torrentMapping[torrentHash];
			Sohvaperuna.TorrentEngine.Torrent torrent = toUtorrent(download);
			File file = new File(this);
			file.Filename = torrent.Name;
			file.Filesize = torrent.Size;
			file.Downloaded = torrent.Downloaded;
			file.Priority = download.Rank;
			list.Add(file);
			return list;
		}

		public bool StartDownload(Uri uri, string label, string hash, string option)
		{
			TorrentDownloader uRLDownloader = prot.TorrentManager.GetURLDownloader(uri.ToString());
			global::AzureusEngine.AzCommunication.Torrent torrent = uRLDownloader.Download();
			Download download = prot.DownloadManager.AddDownload(torrent);
			if (download == null)
			{
				return false;
			}
			if (label != "")
			{
				download.Category = label;
			}
			this.TorrentAdded(toUtorrent(download));
			return true;
		}

		public List<DownloadDir> ListDirs()
		{
			return null;
		}

		public string GetSettings(string element)
		{
			return null;
		}

		public void Start(TorrentList torrents)
		{
			foreach (Sohvaperuna.TorrentEngine.Torrent torrent in torrents)
			{
				if (torrentMapping.ContainsKey(torrent.Hash))
				{
					Download download = torrentMapping[torrent.Hash];
					download.Start();
				}
			}
		}

		public void Stop(TorrentList torrents)
		{
			foreach (Sohvaperuna.TorrentEngine.Torrent torrent in torrents)
			{
				if (torrentMapping.ContainsKey(torrent.Hash))
				{
					Download download = torrentMapping[torrent.Hash];
					download.Stop();
				}
			}
		}

		public void Pause(TorrentList torrents)
		{
			foreach (Sohvaperuna.TorrentEngine.Torrent torrent in torrents)
			{
				if (torrentMapping.ContainsKey(torrent.Hash))
				{
					Download download = torrentMapping[torrent.Hash];
					download.Pause();
				}
			}
		}

		public void Resume(TorrentList torrents)
		{
			foreach (Sohvaperuna.TorrentEngine.Torrent torrent in torrents)
			{
				if (torrentMapping.ContainsKey(torrent.Hash))
				{
					Download download = torrentMapping[torrent.Hash];
					download.Start();
				}
			}
		}

		public bool Pause(string Hash)
		{
			return true;
		}

		public bool Resume(string Hash)
		{
			return true;
		}

		public bool Remove(string Hash, bool removeData)
		{
			return true;
		}

		public bool Start(string Hash)
		{
			return true;
		}

		public bool Stop(string Hash)
		{
			return true;
		}

		public bool SetPriority(string hash, int index, int priority)
		{
			return true;
		}
	}
}
