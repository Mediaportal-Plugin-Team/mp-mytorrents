using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using Sohvaperuna.TorrentEngine;

namespace Sohvaperuna.UTorrentEngine
{
	public class UTorrentClient : ITorrentClient
	{
		private object lockUpdate = new object();

		private string _address;

		private int _port;

		private string _username;

		private string _password;

		private string _outtimer;

		private TorrentList _torrentsAll;

		private TorrentLabelList _labels;

		private ActionController _actionController;

		public static UTorrentBaseResponse baseresponse = null;

		public static string token;

		public TorrentLabelList Labels => _labels;

		public string FullUrlWithAuth => $"http://{_username}:{_password}@{_address}:{_port}/";

		public TorrentList TorrentsAll
		{
			get
			{
				TorrentList torrentList = new TorrentList();
				if (_torrentsAll != null)
				{
					torrentList.AddRange(_torrentsAll);
				}
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

		public UTorrentClient(string address, int port, string username, string password, string outtimer)
		{
			_address = address;
			_port = port;
			_username = username;
			_password = password;
			_outtimer = outtimer;
			_actionController = new ActionController();
		}

		public bool Connect()
		{
			token = ParseResponse(GetData("token.html", null, "", null)).Split('<', '>')[4];
			string response = ParseResponse(GetData(string.Format("?token={0}&{1}", token, ""), null, "", null));
			if (ValidateUTorrentResponse(response))
			{
				UpdateData();
				return true;
			}
			return false;
		}

		public bool ValidateUTorrentResponse(string response)
		{
			Regex regex = new Regex("\"build\":([0-9]+)", RegexOptions.None);
			Match match = regex.Match(response);
			try
			{
				int num = Convert.ToInt32(match.Groups[1].Value);
				if (response.IndexOf("error") != -1 || response.IndexOf("Unable to load") != -1)
				{
					return false;
				}
				return num > 0;
			}
			catch (Exception)
			{
				return false;
			}
		}

		public void Disconnect()
		{
			_address = null;
		}

		public bool UpdateData()
		{
			lock (lockUpdate)
			{
				string response = GetResponse("list=1");
				if (response == null || response.Length == 0)
				{
					return false;
				}
				TorrentLabelList torrentLabelList = new TorrentLabelList();
				JavaScriptSerializer javaScriptSerializer = new JavaScriptSerializer();
				baseresponse = javaScriptSerializer.Deserialize<UTorrentBaseResponse>(response);
				foreach (object[] item in baseresponse.label)
				{
					TorrentLabel torrentLabel = new TorrentLabel();
					torrentLabel.Name = (string)item[0];
					torrentLabel.Count = (int)item[1];
					torrentLabelList.Add(torrentLabel);
				}
				_labels = torrentLabelList;
				TorrentList torrentsAll = _torrentsAll;
				TorrentList torrentList = new TorrentList();
				foreach (object[] torrent3 in baseresponse.torrents)
				{
					Torrent torrent = new Torrent(this);
					try
					{
						torrent.Hash = (string)torrent3[0];
						torrent.Status = (int)torrent3[1];
						torrent.Name = (string)torrent3[2];
						torrent.Size = Convert.ToInt64(torrent3[3]);
						torrent.Progress = (double)Convert.ToInt64(torrent3[4]) / 10.0;
						torrent.Downloaded = Convert.ToInt64(torrent3[5]);
						torrent.Uploaded = Convert.ToInt64(torrent3[6]);
						torrent.Ratio = Convert.ToInt64(torrent3[7]);
						torrent.UploadSpeed = Convert.ToInt64(torrent3[8]);
						torrent.DownloadSpeed = Convert.ToInt64(torrent3[9]);
						torrent.ETA = (int)torrent3[10];
						torrent.Label = (string)torrent3[11];
						torrent.PeersConnected = (int)torrent3[12];
						torrent.PeersInSwarm = (int)torrent3[13];
						torrent.SeedsConnected = (int)torrent3[14];
						torrent.SeedsInSwarm = (int)torrent3[15];
						torrent.Availability = (int)torrent3[16];
						torrent.TorrentQueueOrder = (int)torrent3[17];
						torrent.Remaining = Convert.ToInt64(torrent3[18]);
						torrent.DownloadURL = (string)torrent3[19];
						torrent.RSSFeedURL = (string)torrent3[20];
						torrent.StatusMessage = (string)torrent3[21];
						torrent.StreamID = (string)torrent3[22];
						torrent.AddedOn = (int)torrent3[23];
						torrent.CompletedOn = (int)torrent3[24];
						torrent.AppUpdateURL = (string)torrent3[25];
						torrent.TorrentSavePath = (string)torrent3[26];
					}
					catch
					{
					}
					torrentList.Add(torrent);
				}
				TorrentList torrentList2 = new TorrentList();
				foreach (Torrent item2 in torrentList)
				{
					if (item2.HasStatus(1))
					{
						torrentList2.Add(item2);
					}
					if (torrentsAll == null)
					{
						continue;
					}
					Torrent torrent2 = torrentsAll.TorrentByHash(item2.Hash);
					if (torrent2 == null)
					{
						int num = (int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
						if (this.TorrentAdded != null && num - item2.AddedOn < Math.Max(60, Convert.ToInt32(_outtimer) * 2))
						{
							this.TorrentAdded(item2);
						}
					}
					else
					{
						if (item2.Progress == 100.0 && torrent2.Progress < 100.0)
						{
							this.TorrentCompleted(item2);
						}
						torrentsAll.Remove(torrent2);
					}
				}
				if (torrentsAll != null && torrentList != null && torrentsAll.Count != 0 && (double)(torrentList.Count / torrentsAll.Count) > 0.95)
				{
					foreach (Torrent item3 in torrentsAll)
					{
						if (this.TorrentRemoved != null)
						{
							this.TorrentRemoved(item3);
						}
					}
				}
				TorrentLabel torrentLabel2 = new TorrentLabel();
				torrentLabel2.Name = "";
				foreach (Torrent item4 in torrentList)
				{
					if (item4.Label == "")
					{
						item4.Label = torrentLabel2.Name;
						torrentLabel2.Count++;
					}
				}
				_labels.Insert(0, torrentLabel2);
				if (_torrentsAll == null)
				{
					_torrentsAll = new TorrentList();
				}
				_torrentsAll.Clear();
				foreach (Torrent item5 in torrentList)
				{
					_torrentsAll.Add(item5);
				}
			}
			return true;
		}

		public string GetResponse(string request)
		{
			return GetResponse(request, null, "", null);
		}

		public string GetResponse(string request, byte[] postData, string boundary)
		{
			return GetResponse(request, postData, boundary, null);
		}

		public string GetResponse(string request, byte[] postData, string boundary, object parameters)
		{
			string text = "";
			HttpWebResponse httpWebResponse = null;
			httpWebResponse = GetData($"?token={token}&{request}", postData, boundary, parameters);
			if (httpWebResponse == null || string.IsNullOrEmpty(token) || httpWebResponse.StatusCode == HttpStatusCode.Ambiguous)
			{
				httpWebResponse = GetData("token.html", null, "", null);
				text = ParseResponse(GetData("token.html", null, "", null));
				token = text.Split('<', '>')[4];
				httpWebResponse = GetData($"?token={token}&{request}", postData, boundary, parameters);
			}
			return ParseResponse(httpWebResponse);
		}

		public string ParseResponse(HttpWebResponse resp)
		{
			if (resp == null)
			{
				return null;
			}
			string text = "";
			StreamReader streamReader = new StreamReader(resp.GetResponseStream(), Encoding.UTF8);
			return streamReader.ReadToEnd();
		}

		public HttpWebResponse GetData(string request, byte[] postData, string boundary, object parameters)
		{
			try
			{
				HttpWebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create($"http://{_address}:{_port}/gui/{request}");
				httpWebRequest.KeepAlive = false;
				httpWebRequest.Headers.Add("Authorization: Basic " + Convert.ToBase64String(Encoding.ASCII.GetBytes(_username + ":" + _password)));
				httpWebRequest.Method = "POST";
				httpWebRequest.ContentType = "multipart/form-data; boundary=" + boundary;
				httpWebRequest.ProtocolVersion = HttpVersion.Version10;
				ASCIIEncoding aSCIIEncoding = new ASCIIEncoding();
				if (postData != null)
				{
					httpWebRequest.ContentLength = postData.Length;
					Stream requestStream = httpWebRequest.GetRequestStream();
					requestStream.Write(postData, 0, postData.Length);
					requestStream.Close();
				}
				return httpWebRequest.GetResponse() as HttpWebResponse;
			}
			catch (Exception)
			{
				return null;
			}
		}

		public bool StartDownloadToFolder(Uri uri, string label, string hash, string option, string folder)
		{
			string settings = GetSettings("dir_active_download");
			string response = GetResponse($"action=setsetting&s=dir_active_download&v={folder}");
			string response2 = GetResponse("action=setsetting&s=dir_active_download_flag&v=1");
			bool result = StartDownload(uri, label, hash, option);
			string response3 = GetResponse($"action=setsetting&s=dir_active_download&v={settings}");
			return result;
		}

		public bool StartDownloadToFolder(byte[] content, string filename, string label, string hash, string option, string folder)
		{
			string settings = GetSettings("dir_active_download");
			string response = GetResponse($"action=setsetting&s=dir_active_download&v={folder}");
			string response2 = GetResponse("action=setsetting&s=dir_active_download_flag&v=1");
			bool result = StartDownload(content, filename, label, hash, option);
			string response3 = GetResponse($"action=setsetting&s=dir_active_download&v={settings}");
			return result;
		}

		public bool StartDownload(byte[] content, string filename, string label, string hash, string option)
		{
			try
			{
				string text = "mytorrentsboundary";
				string text2 = "\r\n--" + text + "\r\n";
				text2 = text2 + "Content-Disposition: form-data; name=\"torrent_file\"; filename=\"" + filename + "\"\r\n";
				text2 += "Content-Type: application/x-bittorrent\r\n\r\n";
				byte[] bytes = Encoding.ASCII.GetBytes(text2);
				byte[] bytes2 = Encoding.ASCII.GetBytes("\r\n--" + text + "--\r\n");
				byte[] array = new byte[bytes.Length + bytes2.Length + content.Length];
				Buffer.BlockCopy(bytes, 0, array, 0, bytes.Length);
				Buffer.BlockCopy(content, 0, array, bytes.Length, content.Length);
				Buffer.BlockCopy(bytes2, 0, array, bytes.Length + content.Length, bytes2.Length);
				string response = GetResponse($"action=add-file{option}", array, text);
				string text3 = Encoding.UTF8.GetString(array);
				Log.Instance().Print("utorrent response is " + response);
				if (ValidateUTorrentResponse(response))
				{
					Action_ChangeLabel action = new Action_ChangeLabel(this, hash, label);
					_actionController.Invoke(action, 5000L, ownThread: false);
					return true;
				}
				return false;
			}
			catch
			{
				return false;
			}
		}

		public TorrentList TorrentsByLabel(TorrentLabel _selectedLabel)
		{
			TorrentList torrentList = new TorrentList();
			foreach (Torrent item in _torrentsAll)
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
			GetResponse($"action=setprops&hash={torrentHash}&s=label&v={labelName}");
		}

		public List<Sohvaperuna.TorrentEngine.File> GetFiles(string torrentHash)
		{
			string response = GetResponse($"action=getfiles&hash={torrentHash}");
			JavaScriptSerializer javaScriptSerializer = new JavaScriptSerializer();
			UTorrentFileBaseResponce uTorrentFileBaseResponce = javaScriptSerializer.Deserialize<UTorrentFileBaseResponce>(response);
			object[] array = (object[])uTorrentFileBaseResponce.files[1];
			List<Sohvaperuna.TorrentEngine.File> list = new List<Sohvaperuna.TorrentEngine.File>();
			for (int i = 0; i < array.Length; i++)
			{
				object[] array2 = (object[])array[i];
				Sohvaperuna.TorrentEngine.File file = new Sohvaperuna.TorrentEngine.File(this);
				try
				{
					file.Filename = (string)array2[0];
					file.Filesize = Convert.ToInt64(array2[1]);
					file.Downloaded = Convert.ToInt64(array2[2]);
					file.Priority = (int)array2[3];
					file.FirstPiece = (int)array2[4];
					file.NumPieces = (int)array2[5];
					file.Streamable = (bool)array2[6];
					file.EncodedRate = (int)array2[7];
					file.Duration = (int)array2[8];
					file.Width = (int)array2[9];
					file.Height = (int)array2[10];
					file.StreamETA = (int)array2[11];
					file.Streamability = (int)array2[12];
				}
				catch
				{
				}
				list.Add(file);
			}
			return list;
		}

		public bool StartDownload(Uri uri, string label, string hash, string option)
		{
			string response = GetResponse($"action=add-url&s={uri.AbsoluteUri}");
			if (ValidateUTorrentResponse(response))
			{
				Action_ChangeLabel action = new Action_ChangeLabel(this, hash, label);
				_actionController.Invoke(action, 5000L, ownThread: false);
				return true;
			}
			return false;
		}

		public List<DownloadDir> ListDirs()
		{
			Dictionary<string, int> dictionary = new Dictionary<string, int>();
			List<DownloadDir> list = new List<DownloadDir>();
			string request = "action=list-dirs";
			string text = "";
			try
			{
				text = GetResponse(request);
			}
			catch
			{
			}
			if (string.IsNullOrEmpty(text))
			{
				return list;
			}
			JavaScriptSerializer javaScriptSerializer = new JavaScriptSerializer();
			Dictionary<object, object> dictionary2 = javaScriptSerializer.Deserialize<Dictionary<object, object>>(text);
			object[] array = (object[])dictionary2["download-dirs"];
			object[] array2 = array;
			foreach (object obj2 in array2)
			{
				Dictionary<string, object> dictionary3 = (Dictionary<string, object>)obj2;
				DownloadDir downloadDir = new DownloadDir();
				downloadDir.path = (string)dictionary3["path"];
				downloadDir.available = Convert.ToInt64(dictionary3["available"]);
				list.Add(downloadDir);
			}
			return list;
		}

		public List<UTorrentSettings> GetSettings()
		{
			string response = GetResponse("action=getsettings");
			JavaScriptSerializer javaScriptSerializer = new JavaScriptSerializer();
			Dictionary<string, object> dictionary = javaScriptSerializer.Deserialize<Dictionary<string, object>>(response);
			ArrayList arrayList = (ArrayList)dictionary["settings"];
			List<UTorrentSettings> list = new List<UTorrentSettings>();
			foreach (ArrayList item in arrayList)
			{
				UTorrentSettings uTorrentSettings = new UTorrentSettings();
				uTorrentSettings.Name = Convert.ToString(item[0]);
				uTorrentSettings.Type = Convert.ToInt32(item[1]);
				uTorrentSettings.Value = Convert.ToString(item[2]);
				if (item.Count > 3)
				{
					uTorrentSettings.access = item[3];
				}
				list.Add(uTorrentSettings);
			}
			return list;
		}

		public string GetSettings(string element)
		{
			string result = "";
			List<UTorrentSettings> settings = GetSettings();
			foreach (UTorrentSettings item in settings)
			{
				if (item.Name == element)
				{
					result = item.Value;
				}
			}
			return result;
		}

		public void Start(TorrentList torrents)
		{
			string text = "action=start";
			foreach (Torrent torrent in torrents)
			{
				text = text + "&hash=" + torrent.Hash;
			}
			string response = GetResponse(text);
		}

		public void Stop(TorrentList torrents)
		{
			string text = "action=stop";
			foreach (Torrent torrent in torrents)
			{
				text = text + "&hash=" + torrent.Hash;
			}
			string response = GetResponse(text);
		}

		public void Pause(TorrentList torrents)
		{
			string text = "action=pause";
			foreach (Torrent torrent in torrents)
			{
				text = text + "&hash=" + torrent.Hash;
			}
			string response = GetResponse(text);
		}

		public void Resume(TorrentList torrents)
		{
			string text = "action=unpause";
			foreach (Torrent torrent in torrents)
			{
				text = text + "&hash=" + torrent.Hash;
			}
			string response = GetResponse(text);
		}

		public bool Pause(string Hash)
		{
			string response = GetResponse($"action=pause&hash={Hash}");
			return ValidateUTorrentResponse(response);
		}

		public bool Resume(string Hash)
		{
			string response = GetResponse($"action=unpause&hash={Hash}");
			return ValidateUTorrentResponse(response);
		}

		public bool Remove(string Hash, bool removeData)
		{
			string response = ((!removeData) ? GetResponse($"action=remove&hash={Hash}") : GetResponse($"action=removedata&hash={Hash}"));
			return ValidateUTorrentResponse(response);
		}

		public bool Start(string Hash)
		{
			string response = GetResponse($"action=start&hash={Hash}");
			return ValidateUTorrentResponse(response);
		}

		public bool Stop(string Hash)
		{
			string response = GetResponse($"action=stop&hash={Hash}");
			return ValidateUTorrentResponse(response);
		}

		public bool SetPriority(string hash, int index, int priority)
		{
			string response = GetResponse($"action=setprio&hash={hash}&p={priority}&f={index}");
			return ValidateUTorrentResponse(response);
		}
	}
}
