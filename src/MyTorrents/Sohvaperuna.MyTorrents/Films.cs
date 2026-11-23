using System;
using System.Collections.Generic;
using System.Linq;
using Sohvaperuna.TorrentSearch;

namespace Sohvaperuna.MyTorrents
{
	/// <summary>
	/// 2026 (1.3.40): film settings - their own quality and size limit (TV keeps its own) - and the rules a film
	/// search's results go through.
	/// </summary>
	public static class Films
	{
		public static readonly long[] SizeChoices = { 1500, 2000, 2500, 3000, 4000, 6000, 8000, 0 };

		/// Resolution for films (setting "Films.Quality", 1080p unless changed).
		public static string Quality => Configuration.GetExtra("Films.Quality", "1080p");

		/// Biggest film in MB, 0 = no limit (setting "Films.MaxSizeMB", 2500 unless changed).
		public static long MaxSizeMB
		{
			get
			{
				long mb;
				return long.TryParse(Configuration.GetExtra("Films.MaxSizeMB", "2500"), out mb) && mb >= 0 ? mb : 2500;
			}
		}

		/// 2026 (1.3.47): smallest film in MB (a 700 MB "1080p" Elephant Man). -1 = automatic: depends on the
		/// movie quality (720p 500 MB, 1080p 1.2 GB, 2160p 3 GB). 0 = no minimum. Setting "Films.MinSizeMB".
		public static readonly long[] MinSizeChoices = { -1, 0, 500, 800, 1000, 1200, 1500, 2000, 3000 };

		public static long MinSizeSetting
		{
			get
			{
				long mb;
				return long.TryParse(Configuration.GetExtra("Films.MinSizeMB", "-1"), out mb) && mb >= -1 ? mb : -1;
			}
		}

		public static long MinSizeMB => MinSizeFor(Quality, false);

		/// 2026 (1.3.49): x265 / HEVC copies are about half the size for the same picture, so their minimum is lower
		/// (720p 300 MB, 1080p 700 MB, 4K 1.8 GB; others 500 MB, 1.2 GB, 3 GB). Sizes are for a 2-hour film.
		public static long MinSizeFor(string quality, bool hevc)
		{
			long s = MinSizeSetting;
			if (s >= 0) return s;
			string q = (quality ?? "").ToLowerInvariant();
			int lines = q.Contains("2160") || q.Contains("4k") ? 2160 : q.Contains("1080") ? 1080 : q.Contains("720") ? 720 : 0;
			if (hevc) return lines == 2160 ? 1800 : lines == 1080 ? 700 : lines == 720 ? 300 : 0;
			return lines == 2160 ? 3000 : lines == 1080 ? 1200 : lines == 720 ? 500 : 0;
		}

		public static bool IsHevc(string name) => System.Text.RegularExpressions.Regex.IsMatch(name ?? "", @"x265|h\.?265|hevc", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

		/// The smallest a copy may be, by its own name (x265 or not).
		public static long MinBytesFor(TorrentMatch m) => MinSizeFor(Quality, IsHevc(m?.Title)) * 1024L * 1024L;

		public static string MinSizeText(long setting) => setting < 0 ? "Automatic (1080p 1.2 GB, x265 700 MB; 720p half; 4K 3 GB)" : setting == 0 ? "No minimum" : SizeText(setting);

		/// 2026 (1.3.45): favourite release groups / sites for movies (setting "Films.Favourites"; the TV ones differ).
		public static string[] Favourites => (Configuration.GetExtra("Films.Favourites", "") ?? "")
			.Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries).Select(f => f.Trim()).Where(f => f.Length > 0).ToArray();

		public static bool IsFavourite(TorrentMatch m, string[] favs) => SeriesItem.IsFavourite(m, favs);

		public static string SizeText(long mb) => mb > 0 ? (mb >= 1000 ? (mb / 1000.0).ToString("0.#") + " GB" : mb + " MB") : "no limit";

		/// Film search results: the copies that fit the film quality and size come first (most seeds first); when
		/// none fit, everything is shown so you can still choose.
		public static TorrentMatchList Apply(TorrentMatchList results)
		{
			if (results == null || results.Count == 0) return results;
			string q = Quality;
			long max = MaxSizeMB * 1024L * 1024L;
			Func<TorrentMatch, bool> fits = m =>
				(q == "Any" || Naming.QualityOf(m.Title).Equals(q, StringComparison.OrdinalIgnoreCase)) &&
				(max <= 0 || m.Size <= 0 || m.Size <= max) && (m.Size <= 0 || m.Size >= MinBytesFor(m));
			string sound = Sound.Movies; // 2026 (1.3.45)
			string[] favs = Favourites;
			List<TorrentMatch> good = results.Where(fits).Where(m => Sound.Allowed(m, sound))
				.OrderByDescending(m => IsFavourite(m, favs)).ThenByDescending(m => Sound.Rank(m, sound)).ThenByDescending(m => m.Seed).ToList();
			Log.Instance().Print($"Movie search: {good.Count} of {results.Count} copies fit {q}, {SizeText(MinSizeMB)} to {SizeText(MaxSizeMB)}");
			if (good.Count == 0) return results;
			TorrentMatchList kept = new TorrentMatchList(null);
			kept.AddRange(good);
			return kept;
		}
	}
}
