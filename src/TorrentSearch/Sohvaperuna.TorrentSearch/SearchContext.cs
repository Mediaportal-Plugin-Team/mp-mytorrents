using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

namespace Sohvaperuna.TorrentSearch
{
	/// <summary>
	/// What kind of thing is being searched for, so searches started from MP-TVSeries find TV episodes
	/// and searches from Moving Pictures find movies (2026).
	/// Flows automatically to the threads/tasks a search uses.
	/// </summary>
	public static class SearchContext
	{
		public const string Any = "";
		public const string TV = "TV";
		public const string Movie = "Movie";

		private static readonly AsyncLocal<string> _kind = new AsyncLocal<string>();

		private static readonly Regex EpisodePattern = new Regex(@"\bS\d{1,2}\s?E\d{1,3}\b|\b\d{1,2}x\d{2}\b|\bS\d{1,2}\b|\bSeason\s?\d+|\bComplete\s+Series\b|\bEpisode\s?\d+", RegexOptions.IgnoreCase | RegexOptions.Compiled);

		public static string Kind
		{
			get { return _kind.Value ?? Any; }
			set { _kind.Value = value ?? Any; }
		}

		/// Jackett/Torznab category for the current kind (5000 = TV, 2000 = Movies), or null for any.
		public static string JackettCategory
		{
			get
			{
				switch (Kind)
				{
				case TV: return "5000";
				case Movie: return "2000";
				default: return null;
				}
			}
		}

		/// True when a search text looks like a TV episode ("Show S01E02", "Show 1x02").
		public static bool LooksLikeEpisode(string text)
		{
			return !string.IsNullOrEmpty(text) && Regex.IsMatch(text, @"\bS\d{1,2}\s?E\d{1,3}\b|\b\d{1,2}x\d{2}\b", RegexOptions.IgnoreCase);
		}

		/// Keeps only results that fit the current kind. Results with a category (Jackett) are judged by it,
		/// others (your own sites) by their title.
		public static TorrentMatchList Filter(TorrentMatchList results)
		{
			if (results == null || Kind == Any)
			{
				return results;
			}
			TorrentMatchList kept = new TorrentMatchList(null);
			kept.AddRange(results.Where(Fits));
			Sohvaperuna.TorrentSearch.Log.Instance().Print($"Search kind {Kind}: kept {kept.Count} of {results.Count} results");
			return kept;
		}

		private static bool Fits(TorrentMatch m)
		{
			string cat = (m.Category ?? "").Trim();
			bool tvTitle = EpisodePattern.IsMatch(m.Title ?? "");
			if (Kind == TV)
			{
				if (cat.Length > 0)
				{
					return cat.StartsWith("TV", System.StringComparison.OrdinalIgnoreCase) || cat.IndexOf("Series", System.StringComparison.OrdinalIgnoreCase) >= 0 || cat.IndexOf("Anime", System.StringComparison.OrdinalIgnoreCase) >= 0;
				}
				return tvTitle;
			}
			// Movie
			if (cat.Length > 0)
			{
				return cat.StartsWith("Movie", System.StringComparison.OrdinalIgnoreCase);
			}
			return !tvTitle;
		}
	}
}
