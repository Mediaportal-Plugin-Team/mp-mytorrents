using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Web.Script.Serialization;

namespace Sohvaperuna.TorrentSearch
{
	internal class KATSearchProvider : ITorrentSearch
	{
		public class KATResponse
		{
			public string title { get; set; }

			public string link { get; set; }

			public string description { get; set; }

			public string language { get; set; }

			public int ttl { get; set; }

			public string total_results { get; set; }

			public KATTorrent[] list { get; set; }
		}

		public class KATTorrent
		{
			public string title { get; set; }

			public string category { get; set; }

			public string link { get; set; }

			public string guid { get; set; }

			public string pubDate { get; set; }

			public string torrentLink { get; set; }

			public int files { get; set; }

			public int comments { get; set; }

			public string hash { get; set; }

			public int peers { get; set; }

			public int seeds { get; set; }

			public int leechs { get; set; }

			public int size { get; set; }

			public int votes { get; set; }

			public int verified { get; set; }
		}

		private Hashtable _parameters = new Hashtable(StringComparer.CurrentCultureIgnoreCase);

		public Hashtable Parameters => _parameters;

		public string Name
		{
			get
			{
				return "KAT API";
			}
			set
			{
				value = "KAT API";
			}
		}

		public string Type => "API";

		public object Info { get; set; }

		public string Enabled { get; set; }

		public bool IsTVDBIDSearchSupported => false;

		public bool IsIMDBIDSearchSupported
		{
			get
			{
				throw new NotImplementedException();
			}
		}

		public List<string> Categories()
		{
			List<string> list = new List<string>();
			list.Add("Movies");
			list.Add("TV");
			list.Add("Music");
			list.Add("Books");
			list.Add("Games");
			list.Add("Applications");
			list.Add("XXX");
			return list;
		}

		public TorrentMatchList Search(string searchString, string order, object id)
		{
			throw new NotImplementedException();
		}

		public KATResponse Search(string term)
		{
			string content = GetContent("http://kickass.to/json.php?q=" + term, Encoding.UTF8, null);
			JavaScriptSerializer javaScriptSerializer = new JavaScriptSerializer();
			return javaScriptSerializer.Deserialize<KATResponse>(content);
		}

		public TorrentMatchList SearchByTVDBID(int tvdbID, string order)
		{
			throw new NotImplementedException();
		}

		public string RawSearch(string searchString, string order, object id)
		{
			throw new NotImplementedException();
		}

		public List<string> SortMethods()
		{
			List<string> list = new List<string>();
			list.Add("Default");
			list.Add("Name");
			return list;
		}

		public bool HasOption(string option)
		{
			throw new NotImplementedException();
		}

		public HttpWebResponse Login()
		{
			throw new NotImplementedException();
		}

		public string GetContent(string url, Encoding correctencoding, string PostData)
		{
			HttpWebRequest httpWebRequest = WebRequest.Create(url) as HttpWebRequest;
			httpWebRequest.Method = "GET";
			httpWebRequest.UserAgent = "MediaPortal/MyTorrents0.70";
			httpWebRequest.ProtocolVersion = HttpVersion.Version11;
			httpWebRequest.AutomaticDecompression = DecompressionMethods.GZip;
			WebResponse response = httpWebRequest.GetResponse();
			StreamReader streamReader = new StreamReader(response.GetResponseStream());
			return streamReader.ReadToEnd();
		}
	}
}
