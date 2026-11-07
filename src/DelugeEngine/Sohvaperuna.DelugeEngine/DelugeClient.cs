using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Web.Script.Serialization;
using Sohvaperuna.TorrentEngine;

namespace Sohvaperuna.DelugeEngine
{
	public class DelugeClient : ITorrentClient
	{
		private object lockUpdate = new object();

		public static JavaScriptSerializer js = new JavaScriptSerializer();

		private TorrentLabelList _labels;

		private TorrentList _torrentsAll;

		private static string _address = "localhost";

		private static int _port = 8112;

		private static string _password = "deluge";

		private string _outtimer;

		public static int i = 1;

		public static string url = $"http://{_address}:{_port}/json";

		private static CookieCollection auth = new CookieCollection();

		public Dictionary<string, int> Priorities = new Dictionary<string, int>();

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

		public event TorrentAdded TorrentAdded;

		public event TorrentRemoved TorrentRemoved;

		public event TorrentCompleted TorrentCompleted;

		public Dictionary<string, int> FillPriorities()
		{
			Dictionary<string, int> dictionary = new Dictionary<string, int>();
			dictionary.Add("Highest", 7);
			dictionary.Add("High", 5);
			dictionary.Add("Normal", 1);
			dictionary.Add("Do Not Download", 0);
			return dictionary;
		}

		public DelugeClient(string address, int port, string password, string outtimer)
		{
			_address = address;
			_port = port;
			_password = password;
			_outtimer = outtimer;
			url = $"http://{_address}:{_port}/json"; // was only set once with the defaults
		}

		public bool Connect()
		{
			bool flag = js.Deserialize<bool>(GetResponse("auth.login", DelugeAPI.AuthParams));
			if (flag)
			{
				UpdateData();
			}
			return flag;
		}

		public void Disconnect()
		{
		}

		public List<DownloadDir> ListDirs()
		{
			return null;
		}

		public bool UpdateData()
		{
			lock (lockUpdate)
			{
				string response = GetResponse("web.update_ui", DelugeAPI.UpdateParams);
				if (response == null || response.Length == 0)
				{
					return false;
				}
				TorrentLabelList torrentLabelList = new TorrentLabelList();
				UpdateResponse updateResponse = js.Deserialize<UpdateResponse>(response);
				object[] label = updateResponse.filters.label;
				for (int i = 0; i < label.Length; i++)
				{
					object[] array = (object[])label[i];
					TorrentLabel torrentLabel = new TorrentLabel();
					torrentLabel.Name = (string)array[0];
					torrentLabel.Count = (int)array[1];
					torrentLabelList.Add(torrentLabel);
				}
				_labels = torrentLabelList;
				TorrentList torrentsAll = _torrentsAll;
				TorrentList torrentList = new TorrentList();
				foreach (KeyValuePair<string, DelugeTorrent> torrent3 in updateResponse.torrents)
				{
					DelugeTorrent value = torrent3.Value;
					Torrent torrent = new Torrent(this);
					try
					{
						torrent.Hash = torrent3.Key;
						torrent.Status = 0;
						torrent.Name = value.name;
						torrent.Size = value.total_size;
						torrent.Progress = value.progress;
						torrent.Downloaded = value.total_done;
						torrent.Uploaded = value.total_uploaded;
						torrent.Ratio = (long)Math.Round(value.ratio);
						torrent.UploadSpeed = (long)Math.Round(value.upload_payload_rate);
						torrent.DownloadSpeed = (long)Math.Round(value.download_payload_rate);
						torrent.ETA = (int)Math.Round(value.eta);
						torrent.Label = value.label;
						torrent.PeersConnected = value.num_peers;
						torrent.PeersInSwarm = value.total_peers;
						torrent.SeedsConnected = value.num_seeds;
						torrent.SeedsInSwarm = value.total_seeds;
						torrent.Availability = (int)Math.Round(value.distributed_copies);
						torrent.TorrentQueueOrder = (int)Math.Round(value.queue);
						torrent.Remaining = value.total_size - value.total_done;
						torrent.DownloadURL = "";
						torrent.RSSFeedURL = "";
						torrent.StatusMessage = value.state;
						torrent.StreamID = "";
						torrent.AddedOn = (int)Math.Round(value.time_added);
						torrent.CompletedOn = 0;
						torrent.AppUpdateURL = "";
						torrent.TorrentSavePath = value.save_path;
					}
					catch
					{
					}
					torrentList.Add(torrent);
				}
				TorrentList torrentList2 = new TorrentList();
				foreach (Torrent item in torrentList)
				{
					if (item.HasStatus(1))
					{
						torrentList2.Add(item);
					}
					if (torrentsAll == null)
					{
						continue;
					}
					Torrent torrent2 = torrentsAll.TorrentByHash(item.Hash);
					if (torrent2 == null)
					{
						int num = (int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
						if (this.TorrentAdded != null && num - item.AddedOn < Math.Max(60, Convert.ToInt32(_outtimer) * 2))
						{
							this.TorrentAdded(item);
						}
					}
					else
					{
						if (item.Progress == 100.0 && torrent2.Progress < 100.0)
						{
							this.TorrentCompleted(item);
						}
						torrentsAll.Remove(torrent2);
					}
				}
				if (torrentsAll != null && torrentList != null && torrentsAll.Count != 0 && (double)(torrentList.Count / torrentsAll.Count) > 0.95)
				{
					foreach (Torrent item2 in torrentsAll)
					{
						if (this.TorrentRemoved != null)
						{
							this.TorrentRemoved(item2);
						}
					}
				}
				TorrentLabel torrentLabel2 = new TorrentLabel();
				torrentLabel2.Name = "";
				foreach (Torrent item3 in torrentList)
				{
					if (item3.Label == "")
					{
						item3.Label = torrentLabel2.Name;
						torrentLabel2.Count++;
					}
				}
				if (!_labels.Exists((TorrentLabel e) => e.Name == ""))
				{
					_labels.Insert(0, torrentLabel2);
				}
				if (_torrentsAll == null)
				{
					_torrentsAll = new TorrentList();
				}
				_torrentsAll.Clear();
				foreach (Torrent item4 in torrentList)
				{
					_torrentsAll.Add(item4);
				}
			}
			return true;
		}

		public static string RequestData(string url, string postData)
		{
			string result = "";
			try
			{
				HttpWebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create(url);
				httpWebRequest.CookieContainer = new CookieContainer();
				if (auth != null)
				{
					httpWebRequest.CookieContainer.Add(auth);
				}
				httpWebRequest.KeepAlive = false;
				httpWebRequest.Method = "POST";
				httpWebRequest.ContentType = "application/json";
				httpWebRequest.ProtocolVersion = HttpVersion.Version11;
				httpWebRequest.Headers.Add(HttpRequestHeader.AcceptEncoding, "gzip, deflate");
				httpWebRequest.AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip;
				ASCIIEncoding aSCIIEncoding = new ASCIIEncoding();
				if (postData != null)
				{
					byte[] bytes = aSCIIEncoding.GetBytes(postData);
					httpWebRequest.ContentLength = bytes.Length;
					using (Stream stream = httpWebRequest.GetRequestStream())
					{
						stream.Write(bytes, 0, bytes.Length);
						stream.Close();
					}
				}
				HttpWebResponse httpWebResponse = (HttpWebResponse)httpWebRequest.GetResponse();
				httpWebResponse.Cookies = httpWebRequest.CookieContainer.GetCookies(httpWebRequest.RequestUri);
				if (httpWebResponse.Cookies != null)
				{
					lock (auth)
					{
						auth.Add(httpWebResponse.Cookies);
					}
				}
				using (StreamReader streamReader = new StreamReader(httpWebResponse.GetResponseStream(), Encoding.UTF8))
				{
					result = streamReader.ReadToEnd();
					streamReader.Close();
				}
				return result;
			}
			catch (Exception e)
			{
				Log.Instance().Print(e);
				return null;
			}
		}

		public void ChangeLabel(string torrentHash, string labelName)
		{
			string response = GetResponse("label.set_torrent", new string[2] { torrentHash, labelName });
		}

		public List<Sohvaperuna.TorrentEngine.File> GetFiles(string torrentHash)
		{
			Dictionary<string, DelugeFile> dictionary = new Dictionary<string, DelugeFile>();
			List<Sohvaperuna.TorrentEngine.File> list = new List<Sohvaperuna.TorrentEngine.File>();
			string response = GetResponse("web.get_torrent_files", new string[1] { torrentHash });
			DelugeContent delugeContent = js.Deserialize<DelugeContent>(response);
			if (delugeContent.contents.Values.First().type == "dir")
			{
				string input = js.Serialize(delugeContent.contents.Values.First().contents);
				dictionary = js.Deserialize<Dictionary<string, DelugeFile>>(input);
			}
			else
			{
				dictionary = delugeContent.contents;
			}
			foreach (KeyValuePair<string, DelugeFile> item in dictionary)
			{
				Sohvaperuna.TorrentEngine.File file = new Sohvaperuna.TorrentEngine.File(this);
				file.Filename = item.Key;
				file.Filesize = item.Value.size;
				file.Downloaded = (long)Math.Round((double)item.Value.size * item.Value.progress);
				file.Priority = item.Value.priority;
				list.Add(file);
			}
			return list;
		}

		public string GetResponse(string method, object parameters)
		{
			DelugeBasicRequest delugeBasicRequest = new DelugeBasicRequest();
			delugeBasicRequest.method = method;
			delugeBasicRequest.@params = parameters;
			delugeBasicRequest.id = ++i;
			string postData = js.Serialize(delugeBasicRequest);
			string input = RequestData(url, postData);
			DelugeBasicResponse delugeBasicResponse = js.Deserialize<DelugeBasicResponse>(input);
			if (delugeBasicResponse.error != null)
			{
				ValidateError(delugeBasicResponse.error);
			}
			return js.Serialize(delugeBasicResponse.result);
		}

		public string GetResponse(string request, byte[] postData, string boundary, object parameters)
		{
			return GetResponse(request, parameters);
		}

		public string GetResponse(string request)
		{
			return null;
		}

		public string GetSettings(string element)
		{
			return null;
		}

		public void Pause(TorrentList torrents)
		{
			string response;
			if (torrents.Count == TorrentsAll.Count)
			{
				response = GetResponse("core.pause_all_torrents", null);
				return;
			}
			List<string> list = new List<string>();
			foreach (Torrent torrent in torrents)
			{
				list.Add(torrent.Hash);
			}
			response = GetResponse("core.pause_torrent", list);
		}

		public void Resume(TorrentList torrents)
		{
			string response;
			if (torrents.Count == TorrentsAll.Count)
			{
				response = GetResponse("core.resume_all_torrents", null);
				return;
			}
			List<string> list = new List<string>();
			foreach (Torrent torrent in torrents)
			{
				list.Add(torrent.Hash);
			}
			response = GetResponse("core.resume_torrent", list);
		}

		public void Start(TorrentList torrents)
		{
			string response;
			if (torrents.Count == TorrentsAll.Count)
			{
				response = GetResponse("core.start_all_torrents", null);
				return;
			}
			List<string> list = new List<string>();
			foreach (Torrent torrent in torrents)
			{
				list.Add(torrent.Hash);
			}
			response = GetResponse("core.start_torrent", list);
		}

		public void Stop(TorrentList torrents)
		{
			string response;
			if (torrents.Count == TorrentsAll.Count)
			{
				response = GetResponse("core.stop_all_torrents", null);
				return;
			}
			List<string> list = new List<string>();
			foreach (Torrent torrent in torrents)
			{
				list.Add(torrent.Hash);
			}
			response = GetResponse("core.stop_torrent", list);
		}

		public bool Pause(string Hash)
		{
			string response = GetResponse("core.pause_torrent", new string[1] { Hash });
			return true;
		}

		public bool Resume(string Hash)
		{
			string response = GetResponse("core.resume_torrent", new string[1] { Hash });
			return true;
		}

		public bool Remove(string Hash, bool removeData)
		{
			string response = GetResponse("core.remove_torrent", new object[2] { Hash, removeData });
			return ValidateUTorrentResponse(response);
		}

		public bool Start(string Hash)
		{
			string response = GetResponse("core.start_torrent", new string[1] { Hash });
			return true;
		}

		public bool Stop(string Hash)
		{
			string response = GetResponse("core.stop_torrent", new string[1] { Hash });
			return true;
		}

		public bool StartDownloadToFolder(byte[] content, string filename, string label, string hash, string option, string folder)
		{
			return false;
		}

		public bool StartDownload(byte[] content, string filename, string label, string hash, string option)
		{
			return false;
		}

		public bool StartDownload(Uri uri, string label, string hash, string option)
		{
			string response = GetResponse("web.download_torrent_from_url", new string[1] { uri.ToString() });
			return false;
		}

		public bool SetPriority(string hash, int index, int priority)
		{
			List<Sohvaperuna.TorrentEngine.File> files = GetFiles(hash);
			int[] array = files.Select((Sohvaperuna.TorrentEngine.File e) => e.Priority).ToArray();
			array[index] = priority;
			string response = GetResponse("core.set_torrent_file_priorities", new object[2] { hash, array });
			return true;
		}

		public void ValidateError(DelugeError error)
		{
			if (error.message == "Not authenticated")
			{
				Connect();
			}
		}

		public bool ValidateUTorrentResponse(string response)
		{
			DelugeBasicResponse delugeBasicResponse = js.Deserialize<DelugeBasicResponse>(response);
			if (delugeBasicResponse.error == null)
			{
				return true;
			}
			return false;
		}

		public bool StartDownloadToFolder(Uri uri, string label, string hash, string option, string folder)
		{
			return StartDownload(uri, label, hash, option);
		}
	}
}
