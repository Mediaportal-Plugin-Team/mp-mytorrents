using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web;
using System.Xml;

namespace Sohvaperuna.TorrentSearch
{
	/// <summary>
	/// "Simple site": a torrent site added by the user with just a name and a search address,
	/// e.g. https://example.org/search?q=%search%
	/// The results page is scanned for magnet links and .torrent links - no regex needed.
	/// If the results page has no magnet links, the result pages it links to are opened
	/// (up to MaxFollow) and scanned instead.
	/// Configured in MyTorrents.xml:
	///   &lt;sites&gt;&lt;site&gt;&lt;name&gt;..&lt;/name&gt;&lt;url&gt;..%search%..&lt;/url&gt;&lt;enabled&gt;true&lt;/enabled&gt;&lt;/site&gt;&lt;/sites&gt;
	/// Added 2026.
	/// </summary>
	public class SimpleSiteSearchProvider : ITorrentSearch
	{
		private const int MaxFollow = 15;

		private static readonly List<string> Sorts = new List<string> { "Default", "Seeds", "Size", "Name" };

		private static readonly Regex MagnetRx = new Regex(@"magnet:\?[^""'<>\s]+", RegexOptions.IgnoreCase | RegexOptions.Compiled);
		private static readonly Regex AnchorRx = new Regex(@"<a\s[^>]*?href\s*=\s*[""']([^""']+)[""'][^>]*>(.*?)</a>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
		private static readonly Regex TagRx = new Regex(@"<[^>]+>", RegexOptions.Compiled);
		private static readonly Regex SizeRx = new Regex(@"(\d+(?:[.,]\d+)?)\s*(TiB|GiB|MiB|KiB|TB|GB|MB|KB)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
		private static readonly Regex CellRx = new Regex(@"<td[^>]*>(.*?)</td>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
		private static readonly Regex HashRx = new Regex(@"urn:btih:([A-Za-z0-9]{32,40})", RegexOptions.IgnoreCase | RegexOptions.Compiled);

		private readonly Hashtable _parameters = new Hashtable(StringComparer.CurrentCultureIgnoreCase);

		public Hashtable Parameters => _parameters;

		public string Name { get; set; }

		/// Search address containing %search% where the search words go.
		public string SearchUrl { get; set; }

		public string Type => "Simple";

		public string Enabled { get; set; }

		public object Info { get; set; }

		public bool IsTVDBIDSearchSupported => false;

		public bool IsIMDBIDSearchSupported => false;

		/// Last problem found while searching (shown by the settings "Test" button).
		public string LastError { get; private set; }

		/// Reads all &lt;site&gt; entries from MyTorrents.xml.
		public static List<SimpleSiteSearchProvider> LoadAll(XmlDocument config)
		{
			List<SimpleSiteSearchProvider> list = new List<SimpleSiteSearchProvider>();
			if (config == null)
			{
				return list;
			}
			foreach (XmlNode n in config.SelectNodes("config/sites/site"))
			{
				string name = n.SelectSingleNode("name")?.InnerText?.Trim();
				string url = n.SelectSingleNode("url")?.InnerText?.Trim();
				if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(url))
				{
					continue;
				}
				list.Add(new SimpleSiteSearchProvider
				{
					Name = name,
					SearchUrl = url,
					Enabled = (n.SelectSingleNode("enabled")?.InnerText ?? "true").Trim()
				});
			}
			return list;
		}

		public TorrentMatchList Search(string searchString, string order, object id)
		{
			LastError = null;
			TorrentMatchList result = new TorrentMatchList(null);
			if (string.IsNullOrEmpty(SearchUrl) || string.IsNullOrEmpty(searchString))
			{
				return result;
			}
			string url = BuildUrl(SearchUrl, searchString);
			string html;
			try
			{
				html = Fetch(url);
			}
			catch (Exception ex)
			{
				LastError = "Could not open the site: " + ex.Message;
				Log.Instance().Print($"[{Name}] {LastError} ({url})");
				return result;
			}
			if (LooksBlocked(html))
			{
				LastError = "The site blocks programs (\"checking your browser\" page) and FlareSolverr could not get through - is FlareSolverr running? (MyTorrents setup installs it)";
				Log.Instance().Print($"[{Name}] {LastError}");
				return result;
			}

			Uri baseUri = new Uri(url);
			Dictionary<string, TorrentMatch> found = new Dictionary<string, TorrentMatch>(StringComparer.OrdinalIgnoreCase);
			ScanPage(html, baseUri, null, found);

			if (found.Count == 0)
			{
				// No magnets on the results page - open the result pages it links to.
				List<KeyValuePair<string, string>> candidates = FindResultLinks(html, baseUri, searchString);
				Log.Instance().Print($"[{Name}] no magnet links on results page, opening {candidates.Count} result pages");
				object sync = new object();
				int pending = candidates.Count;
				using (ManualResetEvent done = new ManualResetEvent(pending == 0))
				{
					foreach (KeyValuePair<string, string> c in candidates)
					{
						ThreadPool.QueueUserWorkItem(_ =>
						{
							try
							{
								string page = Fetch(c.Key);
								Dictionary<string, TorrentMatch> local = new Dictionary<string, TorrentMatch>(StringComparer.OrdinalIgnoreCase);
								ScanPage(page, new Uri(c.Key), c.Value, local);
								lock (sync)
								{
									// one result per page: the first magnet/torrent link, titled from the link text
									foreach (TorrentMatch m in local.Values.Take(1))
									{
										if (!found.ContainsKey(m.Id)) found[m.Id] = m;
									}
								}
							}
							catch
							{
							}
							finally
							{
								if (Interlocked.Decrement(ref pending) == 0) done.Set();
							}
						});
					}
					done.WaitOne(TimeSpan.FromSeconds(60));
				}
			}

			if (found.Count == 0 && LastError == null)
			{
				LastError = "No magnet or .torrent links found for that search.";
			}
			IEnumerable<TorrentMatch> sorted;
			switch (order)
			{
			case "Size": sorted = found.Values.OrderByDescending(m => m.Size); break;
			case "Name": sorted = found.Values.OrderBy(m => m.Title); break;
			default: sorted = found.Values.OrderByDescending(m => m.Seed); break;
			}
			result.AddRange(sorted.ToList());
			return result;
		}

		internal static string BuildUrl(string template, string search)
		{
			string encoded = Uri.EscapeDataString(search.Trim());
			string plus = HttpUtility.UrlEncode(search.Trim());
			string url = template.Replace("%search+%", plus)
				.Replace("%searchstring%", encoded)
				.Replace("%search%", encoded)
				.Replace("%page%", "1");
			if (url == template)
			{
				url = template + encoded; // no placeholder: assume the search words go on the end
			}
			return url;
		}

		private static bool LooksBlocked(string html)
		{
			return html.Contains("cf-chl-") || html.Contains("challenge-platform") || html.Contains("<title>Just a moment") || html.Contains("Checking your browser");
		}

		/// Finds magnet links and .torrent links on a page and adds them to 'found'.
		private void ScanPage(string html, Uri pageUri, string titleHint, Dictionary<string, TorrentMatch> found)
		{
			foreach (Match mm in MagnetRx.Matches(html))
			{
				string magnet = HttpUtility.HtmlDecode(mm.Value);
				Match h = HashRx.Match(magnet);
				if (!h.Success) continue;
				string hash = h.Groups[1].Value.ToUpperInvariant();
				if (found.ContainsKey(hash)) continue;
				TorrentMatch m = NewMatch(hash);
				m.magnet = magnet;
				m.Title = DisplayName(magnet) ?? titleHint ?? hash;
				FillRowDetails(html, mm.Index, m);
				found[hash] = m;
			}
			foreach (Match a in AnchorRx.Matches(html))
			{
				string href = HttpUtility.HtmlDecode(a.Groups[1].Value);
				if (!href.Split('?')[0].EndsWith(".torrent", StringComparison.OrdinalIgnoreCase)) continue;
				Uri u;
				if (!Uri.TryCreate(pageUri, href, out u)) continue;
				string key = u.ToString();
				if (found.ContainsKey(key)) continue;
				string text = CleanText(a.Groups[2].Value);
				TorrentMatch m = NewMatch(key);
				m.Url = key;
				m.Title = titleHint ?? (text.Length > 3 ? text : Path.GetFileNameWithoutExtension(u.AbsolutePath));
				FillRowDetails(html, a.Index, m);
				found[key] = m;
			}
		}

		private TorrentMatch NewMatch(string id)
		{
			return new TorrentMatch
			{
				Id = id,
				tracker = Name,
				Category = "",
				Url = "",
				magnet = "",
				Icon = "",
				Date = DateTime.Today,
				imdb = "",
				tvdb = ""
			};
		}

		/// Looks at the table row around a link for a size and seed/leech numbers.
		private static void FillRowDetails(string html, int index, TorrentMatch m)
		{
			int start = html.LastIndexOf("<tr", Math.Max(0, index), StringComparison.OrdinalIgnoreCase);
			int end = html.IndexOf("</tr>", index, StringComparison.OrdinalIgnoreCase);
			if (start < 0 || end < 0 || end - start > 20000)
			{
				start = Math.Max(0, index - 1500);
				end = Math.Min(html.Length, index + 1500);
			}
			string row = html.Substring(start, end - start);
			Match s = SizeRx.Match(TagRx.Replace(row, " "));
			if (s.Success)
			{
				m.Size = ParseSize(s.Groups[1].Value, s.Groups[2].Value);
			}
			// Seeds and leechers are usually the first two cells that hold only a number
			List<int> numbers = new List<int>();
			foreach (Match c in CellRx.Matches(row))
			{
				string t = CleanText(c.Groups[1].Value).Replace(",", "");
				int n;
				if (t.Length > 0 && t.Length < 8 && int.TryParse(t, out n))
				{
					numbers.Add(n);
				}
			}
			if (numbers.Count >= 2)
			{
				m.Seed = numbers[numbers.Count - 2];
				m.Leech = numbers[numbers.Count - 1];
			}
			else if (numbers.Count == 1)
			{
				m.Seed = numbers[0];
			}
		}

		private static long ParseSize(string number, string unit)
		{
			double v;
			if (!double.TryParse(number.Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v)) return 0;
			switch (unit.ToUpperInvariant())
			{
			case "TB": case "TIB": return (long)(v * 1099511627776.0);
			case "GB": case "GIB": return (long)(v * 1073741824.0);
			case "MB": case "MIB": return (long)(v * 1048576.0);
			case "KB": case "KIB": return (long)(v * 1024.0);
			default: return (long)v;
			}
		}

		private static string DisplayName(string magnet)
		{
			int q = magnet.IndexOf('?');
			if (q < 0) return null;
			foreach (string part in magnet.Substring(q + 1).Split('&'))
			{
				if (part.StartsWith("dn=", StringComparison.OrdinalIgnoreCase))
				{
					string dn = HttpUtility.UrlDecode(part.Substring(3)).Trim();
					return dn.Length > 0 ? dn : null;
				}
			}
			return null;
		}

		private static string CleanText(string html)
		{
			return Regex.Replace(HttpUtility.HtmlDecode(TagRx.Replace(html, " ")), @"\s+", " ").Trim();
		}

		/// Links on the results page whose text contains most of the search words (= the results).
		private static List<KeyValuePair<string, string>> FindResultLinks(string html, Uri baseUri, string search)
		{
			string[] words = Regex.Split(search.ToLowerInvariant(), @"[^\p{L}\p{N}]+").Where(w => w.Length > 1).ToArray();
			List<KeyValuePair<string, string>> list = new List<KeyValuePair<string, string>>();
			HashSet<string> seen = new HashSet<string>();
			if (words.Length == 0)
			{
				return list;
			}
			foreach (Match a in AnchorRx.Matches(html))
			{
				string text = CleanText(a.Groups[2].Value);
				string lower = text.ToLowerInvariant();
				int hits = words.Count(w => lower.Contains(w));
				if (hits * 2 < words.Length + 1 || text.Length < 4) continue; // needs more than half the words
				Uri u;
				if (!Uri.TryCreate(baseUri, HttpUtility.HtmlDecode(a.Groups[1].Value), out u)) continue;
				if (u.Host != baseUri.Host || u.Scheme.StartsWith("http") == false) continue;
				if (u.PathAndQuery == baseUri.PathAndQuery) continue;
				if (seen.Add(u.ToString()))
				{
					list.Add(new KeyValuePair<string, string>(u.ToString(), text));
				}
				if (list.Count >= MaxFollow) break;
			}
			return list;
		}

		/// 2026: for SiteFinder - a page as the site serves it to a browser.
		public static string FetchPage(string url) => Fetch(url);

		public static bool IsBlockedPage(string html) => LooksBlocked(html);

		/// 2026: FlareSolverr (installed by the MyTorrents setup, on this PC) gets past "checking your browser" pages.
		public static string FlareSolverrUrl = "http://127.0.0.1:8191/v1";

		private static string Fetch(string url)
		{
			string html = FetchDirect(url);
			if (LooksBlocked(html))
			{
				string solved = ViaFlareSolverr(url);
				if (solved != null && !LooksBlocked(solved)) return solved;
			}
			return html;
		}

		/// Asks FlareSolverr to open the page in its own browser. Null when FlareSolverr is not running or fails.
		private static string ViaFlareSolverr(string url)
		{
			try
			{
				var js = new System.Web.Script.Serialization.JavaScriptSerializer { MaxJsonLength = int.MaxValue };
				byte[] body = Encoding.UTF8.GetBytes(js.Serialize(new Dictionary<string, object> { { "cmd", "request.get" }, { "url", url }, { "maxTimeout", 60000 } }));
				HttpWebRequest req = (HttpWebRequest)WebRequest.Create(FlareSolverrUrl);
				req.Method = "POST";
				req.ContentType = "application/json";
				req.Timeout = 75000;
				req.ReadWriteTimeout = 75000;
				using (System.IO.Stream st = req.GetRequestStream())
				{
					st.Write(body, 0, body.Length);
				}
				string reply;
				using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
				using (System.IO.StreamReader sr = new System.IO.StreamReader(resp.GetResponseStream(), Encoding.UTF8))
				{
					reply = sr.ReadToEnd();
				}
				var d = js.DeserializeObject(reply) as Dictionary<string, object>;
				var solution = d != null && d.ContainsKey("solution") ? d["solution"] as Dictionary<string, object> : null;
				string html = solution != null && solution.ContainsKey("response") ? solution["response"] as string : null;
				if (!string.IsNullOrEmpty(html)) Log.Instance().Print("FlareSolverr opened " + url);
				return html;
			}
			catch (Exception ex)
			{
				Log.Instance().Print("FlareSolverr could not open " + url + ": " + ex.Message);
				return null;
			}
		}

		private static string FetchDirect(string url)
		{
			ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
			HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
			req.Timeout = 20000;
			req.ReadWriteTimeout = 20000;
			req.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0 Safari/537.36";
			req.Accept = "text/html,application/xhtml+xml,*/*";
			req.Headers[HttpRequestHeader.AcceptLanguage] = "en-AU,en;q=0.9";
			req.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
			req.CookieContainer = new CookieContainer();
			try
			{
				using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
				{
					return ReadBody(resp);
				}
			}
			catch (WebException ex) when (ex.Response is HttpWebResponse r)
			{
				// Cloudflare and similar answer 403/503 with a challenge page - return it so it can be recognised
				using (r)
				{
					string body = ReadBody(r);
					if (LooksBlocked(body)) return body;
				}
				throw;
			}
		}

		private static string ReadBody(HttpWebResponse resp)
		{
			Encoding enc = Encoding.UTF8;
			try
			{
				if (!string.IsNullOrEmpty(resp.CharacterSet)) enc = Encoding.GetEncoding(resp.CharacterSet);
			}
			catch
			{
			}
			using (StreamReader r = new StreamReader(resp.GetResponseStream(), enc))
			{
				return r.ReadToEnd();
			}
		}

		public TorrentMatchList SearchByTVDBID(int tvdbID, string order)
		{
			return new TorrentMatchList(null);
		}

		public string RawSearch(string searchString, string order, object id)
		{
			return string.Empty;
		}

		public List<string> SortMethods()
		{
			return new List<string>(Sorts);
		}

		public bool HasOption(string option)
		{
			return false;
		}

		public HttpWebResponse Login()
		{
			return null;
		}

		public string GetContent(string url, Encoding correctencoding, string PostData)
		{
			return Fetch(url);
		}
	}
}
