using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Web.Script.Serialization;

namespace Sohvaperuna.TorrentSearch
{
	internal class HDBitsSearchProvider : ITorrentSearch
	{
		private Hashtable _parameters = new Hashtable(StringComparer.CurrentCultureIgnoreCase);

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

		private static PostData data = new PostData();

		public Hashtable Parameters => _parameters;

		public string Name
		{
			get
			{
				return "HDBits API";
			}
			set
			{
				value = "HDBits API";
			}
		}

		public string Enabled { get; set; }

		public bool IsTVDBIDSearchSupported => true;

		public string Type => "API";

		public object Info { get; set; }

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
			data.username = Parameters["username"] as string;
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

		public void ClearParams()
		{
			data.search = null;
			data.category = null;
			data.codec = null;
			data.medium = null;
			data.imdb = null;
			data.tvdb = null;
		}

		public TorrentMatchList SearchByTVDBID(int tvdb, string order)
		{
			List<HDBTorrent> list = searchByTvdb(tvdb, null, null);
			return ConvertToMatchList(list);
		}

		public TorrentMatchList Search(string searchString, string order, object id)
		{
			List<HDBTorrent> list = SearchByTerm(searchString);
			return ConvertToMatchList(list);
		}

		public List<HDBTorrent> GetLatest()
		{
			ClearParams();
			return BaseRequest();
		}

		public List<HDBTorrent> SearchByTerm(string term)
		{
			ClearParams();
			data.search = term;
			return BaseRequest();
		}

		public List<HDBTorrent> SearchByImdb(string imdbid)
		{
			ClearParams();
			data.imdb = new IMDBObject();
			data.imdb.id = imdbid.Replace("tt", "");
			return BaseRequest();
		}

		public List<HDBTorrent> searchByTvdb(int tvdbid, int? season, int? episode)
		{
			ClearParams();
			data.tvdb = new TVDBObject();
			data.tvdb.id = tvdbid;
			data.tvdb.season = season;
			data.tvdb.episode = episode;
			return BaseRequest();
		}

		public TorrentMatchList ConvertToMatchList(List<HDBTorrent> list)
		{
			TorrentMatchList torrentMatchList = new TorrentMatchList(null);
			foreach (HDBTorrent t in list)
			{
				TorrentMatch torrentMatch = new TorrentMatch();
				torrentMatch.Title = t.name;
				Dictionary<string, int> source = categories;
				Func<KeyValuePair<string, int>, bool> predicate = (KeyValuePair<string, int> e) => e.Value.ToString() == t.type_category;
				torrentMatch.Category = source.FirstOrDefault(predicate).Key;
				torrentMatch.Date = new DateTime(1970, 1, 1, 0, 0, 0, 0).AddSeconds(Convert.ToDouble(t.utadded));
				torrentMatch.Id = t.id;
				torrentMatch.Icon = "http://hdbits.org/favicon.ico";
				torrentMatch.Leech = Convert.ToInt32(t.leechers);
				torrentMatch.Seed = Convert.ToInt32(t.seeders);
				torrentMatch.Size = Convert.ToInt64(t.size);
				torrentMatch.SubCategory = mediums.FirstOrDefault((KeyValuePair<string, int> e) => e.Value.ToString() == t.type_medium).Key;
				torrentMatch.tracker = "HDBits";
				torrentMatch.Url = $"https://hdbits.org/download.php/?id={t.id}&passkey={data.passkey}";
				torrentMatch.imdb = ((t.imdb != null) ? t.imdb.id : "");
				torrentMatch.tvdb = ((t.tvdb != null) ? t.tvdb.id.ToString() : "");
				torrentMatchList.Add(torrentMatch);
			}
			return torrentMatchList;
		}

		public List<HDBTorrent> BaseRequest()
		{
			ServicePointManager.ServerCertificateValidationCallback = (object param0, X509Certificate param1, X509Chain param2, SslPolicyErrors param3) => true;
			DoLogin();
			JavaScriptSerializer javaScriptSerializer = new JavaScriptSerializer();
			string postData = javaScriptSerializer.Serialize(data);
			string content = GetContent("https://hdbits.org/api/torrents", Encoding.UTF8, postData);
			HDBResponce hDBResponce = javaScriptSerializer.Deserialize<HDBResponce>(content);
			return hDBResponce.data;
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
