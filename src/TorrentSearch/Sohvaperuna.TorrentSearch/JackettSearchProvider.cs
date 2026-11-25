using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Web.Script.Serialization;

namespace Sohvaperuna.TorrentSearch
{
	/// <summary>
	/// One Jackett indexer (or "all" indexers) as a MyTorrents search engine.
	/// 2026: URL-encodes the query, null-safe results, sorting support, no NotImplementedException.
	/// </summary>
	public class JackettSearchProvider : ITorrentSearch
	{
		private static readonly List<string> Sorts = new List<string> { "Default", "Seeds", "Leech", "Size", "Date", "Name" };

		private Hashtable _parameters = new Hashtable(StringComparer.CurrentCultureIgnoreCase);

		private JavaScriptSerializer js = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };

		public Hashtable Parameters => _parameters;

		public string Name { get; set; }

		public string Type => "API";

		public string Enabled { get; set; }

		public object Info { get; set; }

		public bool IsTVDBIDSearchSupported { get; set; }

		public bool IsIMDBIDSearchSupported { get; set; }

		/// How long one site may take. "All indexers" asks every site at once, so one slow or broken site
		/// can no longer hold up (or time out) the whole search.
		public int TimeoutMs = 90000;

		private const int PerSiteTimeoutMs = 30000;

		/// 2026: set when this one-site search failed or timed out (for the site report)
		public bool Failed;

		public TorrentMatchList Search(string searchString, string order, object id)
		{
			JackettEngine engine = Info as JackettEngine;
			if (engine == null || string.IsNullOrEmpty(searchString))
			{
				return new TorrentMatchList(null);
			}
			List<JackettEngine> sites = JackettAggregateEngine.ConfiguredIndexers;
			if (engine.id == "all" && sites != null && sites.Count > 1)
			{
				return SearchEachSite(sites, searchString, order);
			}
			string url = engine.id == "all"
				? $"{JackettAggregateEngine.apiUrl}indexers/all/results?apikey={Uri.EscapeDataString(JackettAggregateEngine.jSettings.api_key ?? "")}&Query={Uri.EscapeDataString(searchString)}"
				: $"{JackettAggregateEngine.apiUrl}indexers/{Uri.EscapeDataString(engine.id)}/results?apikey={Uri.EscapeDataString(JackettAggregateEngine.jSettings.api_key ?? "")}&Query={Uri.EscapeDataString(searchString)}";
			if (SearchContext.JackettCategory != null)
			{
				url += "&Category%5B%5D=" + SearchContext.JackettCategory; // TV or Movies only (2026)
			}
			string text;
			try
			{
				text = JackettAggregateEngine.BaseRequest(url, TimeoutMs);
			}
			catch (Exception ex)
			{
				Log.Instance().Print($"Jackett search on {engine.name} failed: {ex.Message}");
				Failed = true;
				return new TorrentMatchList(null);
			}
			if (string.IsNullOrEmpty(text))
			{
				return new TorrentMatchList(null);
			}
			JackettResultResponse response = js.Deserialize<JackettResultResponse>(text);
			return Sort(ConvertToTorrentMatch(response), order);
		}

		/// Every site at the same time, at most ~30 seconds each; whatever has arrived is shown.
		private TorrentMatchList SearchEachSite(List<JackettEngine> sites, string searchString, string order)
		{
			string kind = SearchContext.Kind;
			try
			{
				// .NET Framework allows only 2 connections at a time to one address unless told otherwise
				ServicePointManager.FindServicePoint(new Uri(JackettAggregateEngine.apiUrl)).ConnectionLimit = Math.Max(64, sites.Count + 4);
			}
			catch
			{
			}
			JackettSearchProvider[] ones = sites.Select(site => new JackettSearchProvider { Name = site.name, Info = site, TimeoutMs = PerSiteTimeoutMs }).ToArray();
			var tasks = ones.Select(one => System.Threading.Tasks.Task.Run(() =>
			{
				SearchContext.Kind = kind; // carry TV / film into each site's search
				return one.Search(searchString, order, null);
			})).ToArray();
			try
			{
				System.Threading.Tasks.Task.WaitAll(tasks, PerSiteTimeoutMs + 5000);
			}
			catch (AggregateException)
			{
			}
			TorrentMatchList all = new TorrentMatchList(null);
			int answered = 0;
			for (int i = 0; i < tasks.Length; i++)
			{
				if (tasks[i].Status == System.Threading.Tasks.TaskStatus.RanToCompletion && tasks[i].Result != null)
				{
					answered++;
					all.AddRange(tasks[i].Result);
				}
			}
			// 2026: per-site results for the site report (-1 = failed / not in time)
			var outcomes = new List<KeyValuePair<string, int>>();
			for (int i = 0; i < tasks.Length; i++)
			{
				bool ok = tasks[i].Status == System.Threading.Tasks.TaskStatus.RanToCompletion && tasks[i].Result != null && !ones[i].Failed;
				outcomes.Add(new KeyValuePair<string, int>(sites[i].name, ok ? tasks[i].Result.Count : -1));
			}
			try
			{
				SiteStats.Record(outcomes);
			}
			catch
			{
			}
			answered = outcomes.Count(o => o.Value >= 0);
			Log.Instance().Print($"Jackett: {answered} of {sites.Count} sites answered in time, {all.Count} results - "
				+ string.Join(", ", outcomes.Select(o => o.Key + " " + (o.Value < 0 ? "x" : o.Value.ToString()))));
			return Sort(all, order);
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
			return JackettAggregateEngine.BaseRequest(url);
		}

		private static TorrentMatchList Sort(TorrentMatchList list, string order)
		{
			IEnumerable<TorrentMatch> sorted;
			switch (order)
			{
			case "Leech": sorted = list.OrderByDescending(m => m.Leech); break;
			case "Size": sorted = list.OrderByDescending(m => m.Size); break;
			case "Date": sorted = list.OrderByDescending(m => m.Date); break;
			case "Name": sorted = list.OrderBy(m => m.Title); break;
			default: sorted = list.OrderByDescending(m => m.Seed); break; // Default & Seeds: best-seeded first
			}
			TorrentMatchList result = new TorrentMatchList(null);
			result.AddRange(sorted.ToList());
			return result;
		}

		private TorrentMatchList ConvertToTorrentMatch(JackettResultResponse jr)
		{
			JackettEngine engine = Info as JackettEngine;
			List<Category> caps = engine?.caps ?? new List<Category>();
			TorrentMatchList list = new TorrentMatchList(null);
			if (jr?.Results == null)
			{
				return list;
			}
			foreach (JackettResult r in jr.Results)
			{
				try
				{
					string catId = r.Category?.FirstOrDefault().ToString() ?? "";
					Category category = caps.FirstOrDefault(c => c.ID == catId);
					TorrentMatch m = new TorrentMatch();
					m.Category = category?.Name ?? "";
					m.Title = r.Title ?? "";
					m.tracker = engine?.id == "all" ? (r.Tracker ?? engine.name) : engine?.name;
					m.Icon = string.IsNullOrEmpty(engine?.site_link) ? "" : engine.site_link.TrimEnd('/') + "/favicon.ico";
					m.imdb = r.Imdb?.ToString() ?? "";
					m.Date = r.PublishDate;
					m.Size = r.Size ?? 0;
					m.Seed = r.Seeders ?? 0;
					m.Leech = r.Peers.HasValue ? Math.Max(0, r.Peers.Value - (r.Seeders ?? 0)) : 0; // Jackett "Peers" includes seeders
					m.tvdb = r.TVDBId?.ToString() ?? "";
					m.Url = r.Link?.ToString() ?? "";
					m.magnet = r.MagnetUri?.ToString() ?? "";
					m.Id = r.InfoHash;
					list.Add(m);
				}
				catch (Exception ex)
				{
					Log.Instance().Print("Jackett: skipped a result: " + ex.Message);
				}
			}
			return list;
		}
	}
}
