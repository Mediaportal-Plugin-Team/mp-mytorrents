using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Web.Script.Serialization;

namespace Sohvaperuna.TorrentSearch
{
	internal class BTNSearchProvider : ITorrentSearch
	{
		private Hashtable _parameters = new Hashtable(StringComparer.CurrentCultureIgnoreCase);

		private static PostData data = new PostData();

		private static BTNRequest btn = new BTNRequest();

		public Dictionary<string, int> categories = new Dictionary<string, int>
		{
			{ "Movie", 1 },
			{ "TV", 2 },
			{ "Documentary", 3 },
			{ "Music", 4 },
			{ "Sport", 5 },
			{ "Audio Track", 6 },
			{ "XXX", 7 },
			{ "Misc", 8 }
		};

		public Dictionary<string, int> mediums = new Dictionary<string, int>
		{
			{ "Blu-Ray", 1 },
			{ "HD DVD", 2 },
			{ "Encode", 3 },
			{ "Capture", 4 },
			{ "Remux", 5 }
		};

		public Dictionary<string, int> codecs = new Dictionary<string, int>
		{
			{ "H.264", 1 },
			{ "MPEG-2", 2 },
			{ "VC-1", 3 },
			{ "XviD", 4 }
		};

		public Hashtable Parameters => _parameters;

		public string Name
		{
			get
			{
				return "BTN API";
			}
			set
			{
				value = "BTN API";
			}
		}

		public string Type => "API";

		public object Info { get; set; }

		public string Enabled { get; set; }

		public bool IsTVDBIDSearchSupported => true;

		public bool IsIMDBIDSearchSupported
		{
			get
			{
				throw new NotImplementedException();
			}
		}

		public bool HasOption(string option)
		{
			return false;
		}

		public void DoLogin()
		{
			data.passkey = Parameters["passkey"] as string;
		}

		public HttpWebResponse Login()
		{
			return null;
		}

		public string RawSearch(string searchString, string order, object id)
		{
			return "";
		}

		public List<string> SortMethods()
		{
			List<string> list = new List<string>();
			list.Add("Default");
			list.Add("Name");
			list.Add("Date");
			list.Add("Size");
			list.Add("Seeds");
			list.Add("Leech");
			return list;
		}

		public TorrentMatchList SearchByTVDBID(int tvdb, string order)
		{
			return null;
		}

		public TorrentMatchList Search(string searchString, string order, object id)
		{
			TorrentMatchList torrentMatchList = new TorrentMatchList(null);
			List<BTNTorrent> list = SearchByTerm(searchString);
			foreach (BTNTorrent t in list)
			{
				TorrentMatch torrentMatch = new TorrentMatch();
				torrentMatch.Title = t.ReleaseName;
				Dictionary<string, int> source = categories;
				Func<KeyValuePair<string, int>, bool> predicate = (KeyValuePair<string, int> e) => e.Value.ToString() == t.Category;
				torrentMatch.Category = source.FirstOrDefault(predicate).Key;
				torrentMatch.Date = new DateTime(1970, 1, 1, 0, 0, 0, 0).AddSeconds(Convert.ToDouble(t.time));
				torrentMatch.Id = t.TorrentID;
				torrentMatch.Icon = "https://broadcasthe.net/favicon.ico";
				torrentMatch.Leech = Convert.ToInt32(t.Leechers);
				torrentMatch.Seed = Convert.ToInt32(t.Seeders);
				torrentMatch.SubCategory = mediums.FirstOrDefault((KeyValuePair<string, int> e) => e.Value.ToString() == t.Codec).Key;
				torrentMatch.tracker = "BTN";
				torrentMatch.Url = $"https://broadcasthe.net/torrents.php?action=download&id={t.TorrentID}&apikey={data.passkey}";
				torrentMatch.tvdb = t.TvdbID;
				torrentMatchList.Add(torrentMatch);
			}
			return torrentMatchList;
		}

		public List<BTNTorrent> GetLatest()
		{
			return BaseRequest();
		}

		public List<BTNTorrent> SearchByTerm(string term)
		{
			data.search = term;
			return BaseRequest();
		}

		public List<BTNTorrent> SearchByImdb(string imdbid)
		{
			data.imdb = new IMDBObject();
			data.imdb.id = imdbid.Replace("tt", "");
			return BaseRequest();
		}

		public List<BTNTorrent> searchByTvdb(int tvdbid, int? season, int? episode)
		{
			data.tvdb = new TVDBObject();
			data.tvdb.id = tvdbid;
			data.tvdb.season = season;
			data.tvdb.episode = episode;
			return BaseRequest();
		}

		public List<BTNTorrent> BaseRequest()
		{
			DoLogin();
			JavaScriptSerializer javaScriptSerializer = new JavaScriptSerializer();
			BTNRequest bTNRequest = new BTNRequest();
			bTNRequest.id = 1;
			bTNRequest.method = "getTorrents";
			bTNRequest.@params = new object[3] { data.search, data.passkey, 25 };
			string content = GetContent("http://api.broadcasthe.net/", Encoding.UTF8, javaScriptSerializer.Serialize(bTNRequest));
			BTNResponse bTNResponse = javaScriptSerializer.Deserialize<BTNResponse>(content);
			return bTNResponse.result.torrents.Values.ToList();
		}

		public string GetContent(string url, Encoding correctencoding, string PostData)
		{
			HttpWebRequest httpWebRequest = WebRequest.Create(url) as HttpWebRequest;
			httpWebRequest.Method = "POST";
			byte[] bytes = correctencoding.GetBytes(PostData);
			httpWebRequest.ContentLength = bytes.Length;
			httpWebRequest.UserAgent = "MediaPortal/MyTorrents0.70";
			httpWebRequest.ProtocolVersion = HttpVersion.Version11;
			httpWebRequest.AutomaticDecompression = DecompressionMethods.GZip;
			using (Stream stream = httpWebRequest.GetRequestStream())
			{
				stream.Write(bytes, 0, bytes.Length);
				stream.Close();
			}
			WebResponse response = httpWebRequest.GetResponse();
			StreamReader streamReader = new StreamReader(response.GetResponseStream());
			return streamReader.ReadToEnd();
		}
	}
}
