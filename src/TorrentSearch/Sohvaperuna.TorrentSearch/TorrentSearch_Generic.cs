using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using MediaPortal.Configuration;
using MediaPortal.Database;
using SQLite.NET;

namespace Sohvaperuna.TorrentSearch
{
	internal class TorrentSearch_Generic : ITorrentSearch
	{
		private string _name;

		private Hashtable _parameters;

		private CookieCollection _cookies = new CookieCollection();

		private CookieContainer _cookieContainer = new CookieContainer();

		private Encoding CorEncoding;

		private Uri myUri;

		public Hashtable Parameters => _parameters;

		public string Name
		{
			get
			{
				return _name;
			}
			set
			{
				value = _name;
			}
		}

		public string Type => "XML";

		public string Enabled { get; set; }

		public object Info { get; set; }

		public bool IsTVDBIDSearchSupported => false;

		public bool IsIMDBIDSearchSupported
		{
			get
			{
				throw new NotImplementedException();
			}
		}

		public TorrentSearch_Generic(string name)
		{
			_name = name;
			_parameters = new Hashtable(StringComparer.CurrentCultureIgnoreCase);
			_cookies = new CookieCollection();
			_cookieContainer = new CookieContainer();
		}

		public TorrentMatchList SearchByTVDBID(int tvdb, string order)
		{
			return null;
		}

		public bool HasOption(string option)
		{
			string[] array = (_parameters["Options"] as string).Split(",".ToCharArray());
			option = option.ToLower();
			string[] array2 = array;
			foreach (string text in array2)
			{
				if (text.ToLower().CompareTo(option) == 0)
				{
					return true;
				}
			}
			return false;
		}

		public List<string> SortMethods()
		{
			List<string> list = new List<string>();
			Regex regex = new Regex("Sort.(.*)");
			foreach (DictionaryEntry parameter in _parameters)
			{
				Match match = regex.Match(parameter.Key as string);
				if (match.Success)
				{
					list.Add(match.Groups[1].Value);
				}
			}
			return list;
		}

		public TorrentMatchList Search(string searchString, string sort, object id)
		{
			TorrentMatchList matchList = new TorrentMatchList(null);
			if (!TableExists("Cookies"))
			{
				CreateCookieTable();
			}
			string key = "Sort." + sort;
			string text = _parameters["Sort.Default"] as string;
			if (_parameters.ContainsKey(key))
			{
				text = _parameters[key] as string;
			}
			text = text.Replace("%searchstring%", searchString);
			string[] array = (_parameters["Pages"] as string).Split(",".ToCharArray());
			try
			{
				if (_parameters.ContainsKey("Authentication.Method"))
				{
					Login();
				}
				if (!string.IsNullOrEmpty(_parameters["CodePage"] as string))
				{
					CorEncoding = Encoding.GetEncoding(_parameters["CodePage"] as string);
					Log.Instance().Print(string.Concat(CorEncoding, " is used"));
				}
				else
				{
					CorEncoding = GetCorrectEncoding(_parameters["TrackerURL"] as string);
					Log.Instance().Print(string.Concat(CorEncoding, "is detected as page encoding"));
				}
				string[] array2 = array;
				foreach (string newValue in array2)
				{
					string text2 = text.Replace("%page%", newValue);
					Log.Instance().Print("search url is " + text2);
					string content = GetContent(text2, CorEncoding, null);
					if (content.Length > 100)
					{
						Log.Instance().Print("page downloaded");
					}
					else
					{
						Log.Instance().Print("page unlikely to be downloaded, it's - " + content);
					}
					ParseContent(content, ref matchList);
				}
			}
			catch (Exception)
			{
			}
			try
			{
				if (matchList.Count > 0)
				{
					foreach (TorrentMatch item in matchList)
					{
						foreach (Cookie cookie in _cookies)
						{
							item.cookie = item.cookie + "; " + cookie.ToString();
						}
						if (_parameters["Cookie"] as string != "")
						{
							item.cookie = item.cookie + ";" + (_parameters["Cookie"] as string).Replace("%id%", item.Id);
						}
					}
				}
			}
			catch (Exception)
			{
			}
			return matchList;
		}

		public string RawSearch(string searchString, string sort, object id)
		{
			string text = "";
			string key = "Sort." + sort;
			string text2 = _parameters["Sort.Default"] as string;
			if (_parameters.ContainsKey(key))
			{
				text2 = _parameters[key] as string;
			}
			text2 = text2.Replace("%searchstring%", searchString);
			string[] array = (_parameters["Pages"] as string).Split(",".ToCharArray());
			try
			{
				if (_parameters.ContainsKey("Authentication.Method"))
				{
					Login();
				}
				string[] array2 = array;
				foreach (string newValue in array2)
				{
					string url = text2.Replace("%page%", newValue);
					string content = GetContent(url, GetCorrectEncoding(url), null);
					text += content;
				}
			}
			catch (Exception)
			{
			}
			return text;
		}

		public HttpWebResponse Login()
		{
			if (!_parameters.ContainsKey("Authentication.LoginUrl"))
			{
				return null;
			}
			string requestUriString = _parameters["Authentication.LoginUrl"] as string;
			string s = _parameters["Authentication.Post"] as string;
			HttpWebRequest httpWebRequest = WebRequest.Create(requestUriString) as HttpWebRequest;
			httpWebRequest.Proxy = new WebProxy("94.130.117.199", 1080);
			httpWebRequest.AllowAutoRedirect = false;
			httpWebRequest.Method = "POST";
			httpWebRequest.ContentType = "application/x-www-form-urlencoded";
			httpWebRequest.Headers.Add("Accept-Charset: ISO-8859-1,utf-8;q=0.7,*;q=0.7");
			httpWebRequest.Headers.Add("Origin: " + _parameters["TrackerURL"]);
			httpWebRequest.UserAgent = "Mozilla/5.0 (Windows NT 5.1) AppleWebKit/535.1 (KHTML, like Gecko) Chrome/14.0.835.186 Safari/535.1";
			httpWebRequest.Accept = "*/*";
			httpWebRequest.KeepAlive = true;
			httpWebRequest.CookieContainer = _cookieContainer;
			httpWebRequest.ProtocolVersion = HttpVersion.Version11;
			httpWebRequest.Referer = _parameters["TrackerURL"] as string;
			ASCIIEncoding aSCIIEncoding = new ASCIIEncoding();
			byte[] bytes = aSCIIEncoding.GetBytes(s);
			httpWebRequest.ContentLength = bytes.Length;
			Stream requestStream = httpWebRequest.GetRequestStream();
			requestStream.Write(bytes, 0, bytes.Length);
			requestStream.Close();
			HttpWebResponse httpWebResponse = httpWebRequest.GetResponse() as HttpWebResponse;
			if (httpWebResponse.Cookies != null)
			{
				foreach (Cookie cookie in httpWebResponse.Cookies)
				{
					_cookies.Add(cookie);
				}
			}
			return httpWebResponse;
		}

		private void ParseContent(string content, ref TorrentMatchList matchList)
		{
			Log.Instance().Print("Beginning content parsing");
			MatchCollection matchCollection = null;
			string[] array = (_parameters["DateFormat"] as string).Split("|".ToCharArray());
			if (_parameters.ContainsKey("ParseDate"))
			{
				Regex regex = new Regex(_parameters["ParseDate"] as string, RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Singleline);
				matchCollection = regex.Matches(content);
			}
			Regex regex2 = new Regex(_parameters["Parse"] as string, RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Singleline);
			MatchCollection matchCollection2 = regex2.Matches(content);
			Hashtable hashtable = new Hashtable();
			hashtable.Add("B", 1L);
			hashtable.Add("KB", 1024L);
			hashtable.Add("MB", 1048576L);
			hashtable.Add("GB", 1073741824L);
			hashtable.Add("TB", 1099511627776L);
			hashtable.Add("KiB", 1024L);
			hashtable.Add("MiB", 1048576L);
			hashtable.Add("GiB", 1073741824L);
			hashtable.Add("TiB", 1099511627776L);
			foreach (Match item in matchCollection2)
			{
				TorrentMatch torrentMatch = new TorrentMatch();
				torrentMatch.Category = item.Groups["TCategory"].Value;
				torrentMatch.SubCategory = item.Groups["TSubCategory"].Value;
				if (_name == "KAT")
				{
					torrentMatch.Id = item.Groups["TId"].Value.ToUpper();
				}
				else
				{
					torrentMatch.Id = item.Groups["TId"].Value;
				}
				torrentMatch.Url = (_parameters["Download"] as string).Replace("%id%", torrentMatch.Id);
				if (_parameters["Magnet"] as string == "true")
				{
					torrentMatch.magnet = item.Groups["TMagnet"].Value;
				}
				torrentMatch.Title = Regex.Replace(item.Groups["TTitle"].Value, "<[^>]*>", "");
				torrentMatch.tracker = _name;
				torrentMatch.Icon = (_parameters["TrackerURL"] as string) + "/favicon.ico";
				string value = item.Groups["TSizeT"].Value;
				string text = item.Groups["TSize"].Value.Replace(",", ".");
				int num = text.Count((char e) => e == '.');
				if (num > 1)
				{
					text = text.Remove(text.IndexOf("."), 1);
				}
				NumberFormatInfo numberFormatInfo = new NumberFormatInfo();
				numberFormatInfo.NumberDecimalSeparator = ".";
				torrentMatch.Size = (long)(Convert.ToDouble(text, numberFormatInfo) * (double)(long)hashtable[value]);
				try
				{
					torrentMatch.Seed = Convert.ToInt32(item.Groups["TSeed"].Value);
				}
				catch
				{
					torrentMatch.Seed = 0;
				}
				try
				{
					torrentMatch.Leech = Convert.ToInt32(item.Groups["TLeech"].Value);
				}
				catch
				{
					torrentMatch.Leech = 0;
				}
				string value2 = item.Groups["TDate"].Value;
				if (value2 == "" && matchCollection != null)
				{
					foreach (Match item2 in matchCollection)
					{
						if (item2.Index < item.Index)
						{
							value2 = item2.Groups["TDate"].Value;
							continue;
						}
						break;
					}
				}
				torrentMatch.Date = DateTime.Now;
				if (value2 != "")
				{
					string[] array2 = array;
					foreach (string text2 in array2)
					{
						if (text2 == "")
						{
							continue;
						}
						try
						{
							torrentMatch.Date = DateTime.ParseExact(value2, text2, new CultureInfo("en-GB"));
						}
						catch (Exception)
						{
							continue;
						}
						break;
					}
				}
				matchList.Add(torrentMatch);
			}
			Log.Instance().Print("Content parsing finished");
		}

		private string ReplaceHtmlEntities(string content)
		{
			content = content.Replace("&amp;", "&");
			content = content.Replace("&gt;", ">");
			content = content.Replace("&lt;", "<");
			content = content.Replace("&nbsp;", " ");
			content = content.Replace("&quot;", "\"");
			content = content.Replace("&apos;", "'");
			content = content.Replace("&copy;", "©");
			content = content.Replace("&reg;", "®");
			return content;
		}

		public string GetContent(string url, Encoding correctencoding, string PostData)
		{
			HttpWebRequest httpWebRequest = WebRequest.Create(url) as HttpWebRequest;
			httpWebRequest.Proxy = new WebProxy("94.130.117.199", 1080);
			if (PostData == null)
			{
				httpWebRequest.Method = "GET";
			}
			else
			{
				httpWebRequest.Method = "POST";
			}
			string text = "";
			httpWebRequest.Headers.Add("Accept-: */*");
			httpWebRequest.Headers.Add("Accept-Language: q=0.8,en-US;q=0.6,en;q=0.4");
			httpWebRequest.Headers.Add("Accept-Charset: utf-8;q=0.7,*;q=0.7");
			httpWebRequest.Headers.Add("Accept-Charset: ISO-8859-1,utf-8;q=0.7,*;q=0.7");
			httpWebRequest.AllowAutoRedirect = false;
			httpWebRequest.KeepAlive = true;
			httpWebRequest.UserAgent = "Mozilla/5.0 (Windows NT 5.1) AppleWebKit/535.1 (KHTML, like Gecko) Chrome/14.0.835.186 Safari/535.1";
			httpWebRequest.ProtocolVersion = HttpVersion.Version11;
			httpWebRequest.AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip;
			foreach (Cookie cookie in _cookies)
			{
				text = cookie.ToString() + "; " + text;
			}
			httpWebRequest.Headers.Add("Cookie", text);
			WebResponse response = httpWebRequest.GetResponse();
			StreamReader streamReader = new StreamReader(response.GetResponseStream(), CorEncoding);
			return ReplaceHtmlEntities(streamReader.ReadToEnd());
		}

		private Encoding GetCorrectEncoding(string url)
		{
			Encoding encoding = null;
			string text = "";
			WebRequest webRequest = WebRequest.Create(url);
			HttpWebResponse httpWebResponse = (HttpWebResponse)webRequest.GetResponse();
			string characterSet = httpWebResponse.CharacterSet;
			encoding = Encoding.GetEncoding(characterSet);
			using (StreamReader streamReader = new StreamReader(httpWebResponse.GetResponseStream(), encoding))
			{
				text = streamReader.ReadToEnd();
				streamReader.Close();
			}
			int num = text.IndexOf("charset=");
			if (num > 0)
			{
				num += 8;
				int num2 = text.IndexOfAny(new char[3] { ' ', '"', ';' }, num);
				string text2 = text.Substring(num, num2 - num);
				if (text2 != characterSet)
				{
					encoding = Encoding.GetEncoding(text2);
				}
			}
			return encoding;
		}

		private Cookie GetCookie(string host)
		{
			return null;
		}

		private bool SaveCookie(Cookie cook)
		{
			return true;
		}

		public void CreateCookieTable()
		{
			SQLiteClient sQLiteClient = new SQLiteClient(Config.GetFile(Config.Dir.Database, "MyTorrents.db3"));
			DatabaseUtility.SetPragmas(sQLiteClient);
			SQLiteResultSet sQLiteResultSet = sQLiteClient.Execute("CREATE TABLE Cookies (\r\n                Name CHAR(4096),\r\n                Value CHAR (4096),\r\n                Domain CHAR (4096),\r\n                Path CHAR (4096),\r\n                Port CHAR (4096),\r\n                Secure CHAR (4096),\r\n                Timestamp CHAR (4096),\r\n                Expires CHAR (4096),\r\n                Expired BOOL (4096),\r\n                Discard CHAR (4096),\r\n                Comment CHAR (4096),\r\n                CommentUri CHAR (4096),\r\n                Version CHAR (4096))");
			sQLiteClient.Close();
		}

		public bool TableExists(string Name)
		{
			SQLiteClient sQLiteClient = new SQLiteClient(Config.GetFile(Config.Dir.Database, "MyTorrents.db3"));
			DatabaseUtility.SetPragmas(sQLiteClient);
			if (DatabaseUtility.TableExists(sQLiteClient, Name))
			{
				sQLiteClient.Close();
				return true;
			}
			sQLiteClient.Close();
			return false;
		}
	}
}
