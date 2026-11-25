using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;

namespace Sohvaperuna.TorrentSearch
{
	/// <summary>
	/// 2026: works out a site's search address from just its name, e.g. "ext.to" -> "https://ext.to/search/?q=%search%".
	/// First looks for the search box on the home page, then tries the usual address patterns, and keeps the first
	/// one that returns torrents. Sites behind a "checking your browser" page are reported as blocked.
	/// </summary>
	public static class SiteFinder
	{
		public sealed class Result
		{
			public bool Found;
			public bool Blocked;
			public string Name = "";
			public string SearchUrl = "";
			public string Message = "";
			public int Results;
		}

		private static readonly string[] Patterns =
		{
			"/search/?q=%search%", "/search?q=%search%", "/search/%search%/1/", "/search/%search%/", "/search/%search%",
			"/?s=%search%", "/browse/?q=%search%", "/browse?q=%search%", "/search.php?q=%search%", "/torrents/?search=%search%",
			"/torrents.php?search=%search%", "/index.php?q=%search%", "/?q=%search%", "/s/?q=%search%", "/usearch/%search%/"
		};

		private static readonly string[] TestWords = { "2024", "ubuntu" };

		/// "ext.to", "www.ext.to", "https://ext.to/anything" -> "https://ext.to"
		public static string BaseAddress(string text)
		{
			string t = (text ?? "").Trim();
			if (t.Length == 0) return "";
			if (!Regex.IsMatch(t, "^https?://", RegexOptions.IgnoreCase)) t = "https://" + t;
			Uri u;
			return Uri.TryCreate(t, UriKind.Absolute, out u) ? u.GetLeftPart(UriPartial.Authority) : "";
		}

		public static string HostName(string text)
		{
			Uri u;
			string b = BaseAddress(text);
			return b.Length > 0 && Uri.TryCreate(b, UriKind.Absolute, out u) ? Regex.Replace(u.Host, "^www\\.", "", RegexOptions.IgnoreCase) : "";
		}

		public static Result Find(string address, Action<string> progress)
		{
			Result r = new Result { Name = HostName(address) };
			string baseUrl = BaseAddress(address);
			if (baseUrl.Length == 0)
			{
				r.Message = "That does not look like a web address. Type it like  ext.to  or  https://ext.to";
				return r;
			}
			// 1. the home page: reachable? blocked? does it have a search box?
			progress?.Invoke("opening " + baseUrl + " ...");
			string home;
			try
			{
				home = SimpleSiteSearchProvider.FetchPage(baseUrl + "/");
			}
			catch (Exception ex)
			{
				if (baseUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
				{
					try
					{
						baseUrl = "http://" + baseUrl.Substring(8);
						home = SimpleSiteSearchProvider.FetchPage(baseUrl + "/");
					}
					catch
					{
						r.Message = "Could not open " + r.Name + ": " + ex.Message;
						return r;
					}
				}
				else
				{
					r.Message = "Could not open " + r.Name + ": " + ex.Message;
					return r;
				}
			}
			if (SimpleSiteSearchProvider.IsBlockedPage(home))
			{
				r.Blocked = true;
				r.Message = r.Name + " shows a \"checking your browser\" page to programs, so MyTorrents cannot search it directly. " +
					"If Jackett has it, tick it in the list." +
					" You can still use it yourself: search it in your web browser and click a magnet link - qBittorrent picks it up and MyTorrents copies it to your library when it finishes.";
				return r;
			}
			List<string> candidates = FormSearchUrls(baseUrl, home);
			candidates.AddRange(Patterns.Select(p => baseUrl + p));
			candidates = candidates.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
			// 2. try each address until one returns torrents
			bool sawBlocked = false;
			foreach (string c in candidates)
			{
				foreach (string word in TestWords)
				{
					progress?.Invoke("trying " + c.Replace("%search%", word) + " ...");
					SimpleSiteSearchProvider site = new SimpleSiteSearchProvider { Name = r.Name, SearchUrl = c, Enabled = "true" };
					TorrentMatchList found;
					try
					{
						found = site.Search(word, "Default", null);
					}
					catch
					{
						continue;
					}
					if (found != null && found.Count > 0)
					{
						r.Found = true;
						r.SearchUrl = c;
						r.Results = found.Count;
						r.Message = "Found it: " + found.Count + " results for \"" + word + "\" using  " + c;
						return r;
					}
					if ((site.LastError ?? "").IndexOf("checking your browser", StringComparison.OrdinalIgnoreCase) >= 0) sawBlocked = true;
					if (site.LastError != null && site.LastError.StartsWith("Could not open")) break; // that address does not exist - next pattern
				}
			}
			r.Blocked = sawBlocked;
			r.Message = sawBlocked
				? r.Name + " blocks programs on its search pages (\"checking your browser\"), so it only works through Jackett if Jackett has it. You can still use it yourself: search it in your web browser and click a magnet link - qBittorrent picks it up and MyTorrents copies it to your library when it finishes."
				: "Could not work out how to search " + r.Name + ". Search for something on the site in your web browser, copy the address, " +
				  "and paste it into My sites with the search words replaced by %search%.";
			return r;
		}

		/// Search boxes on the page: <form action="/search" method="get"> ... <input name="q"> -> https://site/search?q=%search%
		private static List<string> FormSearchUrls(string baseUrl, string html)
		{
			List<string> urls = new List<string>();
			foreach (Match f in Regex.Matches(html ?? "", "<form\\b([^>]*)>(.*?)</form>", RegexOptions.IgnoreCase | RegexOptions.Singleline))
			{
				string attrs = f.Groups[1].Value, body = f.Groups[2].Value;
				string method = Attr(attrs, "method");
				if (method.Length > 0 && !method.Equals("get", StringComparison.OrdinalIgnoreCase)) continue;
				string field = null;
				foreach (Match input in Regex.Matches(body, "<input\\b([^>]*)>", RegexOptions.IgnoreCase))
				{
					string type = Attr(input.Groups[1].Value, "type").ToLowerInvariant();
					string name = Attr(input.Groups[1].Value, "name");
					if (name.Length > 0 && (type == "" || type == "text" || type == "search"))
					{
						field = name;
						break;
					}
				}
				if (field == null) continue;
				string action = WebUtility.HtmlDecode(Attr(attrs, "action"));
				Uri target;
				if (!Uri.TryCreate(new Uri(baseUrl + "/"), action.Length > 0 ? action : "/", out target)) continue;
				if (!target.Host.EndsWith(new Uri(baseUrl).Host.Replace("www.", ""), StringComparison.OrdinalIgnoreCase)) continue;
				string u = target.GetLeftPart(UriPartial.Path);
				urls.Add(u + (target.Query.Length > 1 ? target.Query + "&" : "?") + Uri.EscapeDataString(field) + "=%search%");
			}
			return urls;
		}

		private static string Attr(string attrs, string name)
		{
			Match m = Regex.Match(attrs, "\\b" + name + "\\s*=\\s*(\"([^\"]*)\"|'([^']*)'|([^\\s>]+))", RegexOptions.IgnoreCase);
			return m.Success ? (m.Groups[2].Success ? m.Groups[2].Value : m.Groups[3].Success ? m.Groups[3].Value : m.Groups[4].Value).Trim() : "";
		}
	}
}
