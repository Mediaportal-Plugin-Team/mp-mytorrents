using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using MediaPortal.Configuration;
using Sohvaperuna.TorrentSearch;

namespace Sohvaperuna.MyTorrents
{
	/// <summary>
	/// 2026: "Get this episode now - just this once": the copies of one episode, smallest first, ignoring the size limit
	/// (fakes under the smallest size, dead copies and copies nobody seeds are still left out). Used by MediaPortal and
	/// by MyTorrents Configuration.
	/// </summary>
	public static class EpisodeCopies
	{
		public static List<TorrentMatch> Find(string showId, string searchName, string episode, out string error)
		{
			error = null;
			Configuration c = Configuration.Instance();
			if (!(c.Settings["TorrentSearch.Config"] is string))
			{
				c.Settings["TorrentSearch.Config"] = Config.GetFile(Config.Dir.Config, "MyTorrents.xml");
			}
			TorrentSearchEngine engine = new TorrentSearchEngine(c.Settings["TorrentSearch.Config"] as string);
			ITorrentSearch sites = engine.Engines.OfType<TorrentSearch_Combiner>().FirstOrDefault(x => x.Name == TorrentSearchEngine.TVSeriesEngineName && x.Engines.Count > 0);
			if (sites == null)
			{
				error = "No search sites - add sites in MyTorrents Configuration.";
				return new List<TorrentMatch>();
			}
			string text = Regex.Replace(searchName ?? "", "[^0-9a-zA-Z ]+", " ").Trim();
			text = Regex.Replace(text, @"\s+", " ");
			List<TorrentMatch> found = (sites.Search(text + " " + episode, "Default", null) ?? new TorrentMatchList(null)).ToList();
			Log.Instance().Print($"Get now: \"{text} {episode}\" on {sites.Name}: {found.Count} results");
			if (found.Count == 0)
			{
				found = (sites.Search(text, "Default", null) ?? new TorrentMatchList(null)).ToList();
			}
			string quality = SeriesItem.Quality;
			long min = SeriesItem.MinSizeMB * 1024L * 1024L;
			return found.Where(m => m.Title != null && m.Title.IndexOf(episode, StringComparison.OrdinalIgnoreCase) >= 0
					&& (quality == "Any" || m.Title.IndexOf(quality, StringComparison.OrdinalIgnoreCase) >= 0)
					&& !Grabbed.IsDead(showId, episode, SeriesItem.Link(m))
					&& (m.Size <= 0 || m.Size >= min) && m.Seed > 0)
				.GroupBy(SeriesItem.Link).Select(g => g.OrderByDescending(m => m.Seed).First())
				.OrderBy(m => m.Size > 0 ? 0 : 1).ThenBy(m => m.Size).ThenByDescending(m => m.Seed)
				.ToList();
		}

		/// "871 MB · x264 · 3 seeders · JFF"  (+ " · favourite", " · over your limit")
		public static string Describe(TorrentMatch m)
		{
			string group = "";
			Match g = Regex.Match(m.Title ?? "", @"-([A-Za-z0-9]+)(?:\s*\[.*\])?(?:\.(?:mkv|mp4))?\s*$");
			if (g.Success) group = " · " + g.Groups[1].Value;
			long max = SeriesItem.MaxSizeMB * 1024L * 1024L;
			return (m.Size > 0 ? m.Size / 1024 / 1024 + " MB" : "size?") + " · " + SeriesItem.CodecOf(m.Title) + " · " + m.Seed + " seeders" + group
				+ (SeriesItem.IsFavourite(m, SeriesItem.Favourites) ? " · favourite" : "")
				+ (max > 0 && m.Size > max ? " · over your limit" : "");
		}
	}

	/// <summary>
	/// 2026: what the Auto grabber is looking for (MyTorrents.autograbber.txt), written by MediaPortal for MyTorrents
	/// Configuration's "Shows" tab.
	/// </summary>
	public static class AutoGrabberStatus
	{
		public sealed class Show
		{
			public string Id = "", Name = "", SearchName = "", Type = "", Next = "", LastLook = "", Airs = "";
			public DateTime AirsAt = DateTime.MaxValue, LookedAt = DateTime.MinValue; // 2026 (1.3.38): for sorting
			public string Missing = ""; // 2026 (1.3.41): aired episodes you don't have, "S01E03,S02E01"
			public int MissingCount => Missing.Length == 0 ? 0 : Missing.Split(',').Length;
		}

		private static string File => Config.GetFile(Config.Dir.Config, "MyTorrents.autograbber.txt");

		private static string Clean(string s) => (s ?? "").Replace("\t", " ").Replace("\r", " ").Replace("\n", " ");

		/// The episode the Auto grabber looks for next ("" = none yet).
		public static string NextEpisode(SeriesItem s)
		{
			if (s?.show == null) return "";
			if (s.type == "Missing episodes") return (s.show.AllUnlocal ?? "").Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
			return s.show.FirstUnlocal ?? "";
		}

		public static void Write(IEnumerable<SeriesItem> shows)
		{
			try
			{
				System.IO.File.WriteAllLines(File, shows.Where(s => s?.show != null).Select(s => string.Join("\t", Clean(s.show.ID), Clean(s.show.Name), Clean(s.show.OriginalName ?? s.show.Name), Clean(s.type), Clean(NextEpisode(s)), Clean(s.LastLook),
					s.show.NextAirsUtc == DateTime.MinValue ? "" : s.show.NextAirsUtc.ToLocalTime().ToString("ddd d MMM", CultureInfo.InvariantCulture),
					s.show.NextAirsUtc == DateTime.MinValue ? "" : s.show.NextAirsUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
					s.LastCheck == DateTime.MinValue ? "" : s.LastCheck.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
					string.Join(",", (s.show.AiredMissing ?? new List<string>()).Select(x => x.Split('|')[0]).Where(x => x.Length > 0)))));
			}
			catch
			{
			}
		}

		public static DateTime Written
		{
			get
			{
				try
				{
					return System.IO.File.Exists(File) ? System.IO.File.GetLastWriteTime(File) : DateTime.MinValue;
				}
				catch
				{
					return DateTime.MinValue;
				}
			}
		}

		public static List<Show> Read()
		{
			List<Show> list = new List<Show>();
			try
			{
				if (!System.IO.File.Exists(File)) return list;
				foreach (string line in System.IO.File.ReadAllLines(File))
				{
					string[] f = line.Split('\t');
					if (f.Length < 6) continue;
					Show s = new Show { Id = f[0], Name = f[1], SearchName = f[2], Type = f[3], Next = f[4], LastLook = f[5], Airs = f.Length > 6 ? f[6] : "" };
					DateTime d;
					if (f.Length > 7 && DateTime.TryParseExact(f[7], "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out d)) s.AirsAt = d;
					else if (s.Airs.Length > 4 && DateTime.TryParseExact(s.Airs.Substring(s.Airs.IndexOf(' ') + 1).Trim(), "d MMM", CultureInfo.InvariantCulture, DateTimeStyles.None, out d)) // weekday left out: it would have to match the year
					{
						// a list written by an older MyTorrents: "Thu 21 Jan" has no year - take the one nearest today
						d = new DateTime(DateTime.Today.Year, d.Month, d.Day);
						if (d < DateTime.Today.AddDays(-183)) d = d.AddYears(1);
						else if (d > DateTime.Today.AddDays(183)) d = d.AddYears(-1);
						s.AirsAt = d;
					}
					if (f.Length > 8 && DateTime.TryParseExact(f[8], "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out d)) s.LookedAt = d;
					if (f.Length > 9) s.Missing = f[9];
					list.Add(s);
				}
			}
			catch
			{
			}
			return list;
		}
	}

	/// <summary>
	/// 2026: a copy chosen in MyTorrents Configuration, handed to MediaPortal (MyTorrents.requests.txt) so the download
	/// is started, remembered and later copied to the library exactly like one the Auto grabber started.
	/// </summary>
	public static class GrabRequests
	{
		private static readonly object Lock = new object();

		private static string File => Config.GetFile(Config.Dir.Config, "MyTorrents.requests.txt");

		private static string Clean(string s) => (s ?? "").Replace("\t", " ").Replace("\r", " ").Replace("\n", " ");

		public static void Add(string showId, string showName, string episode, TorrentMatch m)
		{
			System.IO.File.AppendAllText(File, string.Join("\t", "grab", Clean(showId), Clean(showName), Clean(episode), Clean(m.Title), Clean(m.Url), Clean(m.magnet), Clean(m.cookie), Clean(m.Id),
				m.Size.ToString(CultureInfo.InvariantCulture), m.Seed.ToString(CultureInfo.InvariantCulture)) + Environment.NewLine);
		}

		/// 2026 (1.3.38): "Check now" from MyTorrents Configuration - one show (its id) or "*" for all.
		public static void AddCheck(string showId, string showName)
		{
			System.IO.File.AppendAllText(File, string.Join("\t", "check", Clean(showId), Clean(showName)) + Environment.NewLine);
		}

		/// 2026 (1.3.38): take a show out of the Auto grabber from MyTorrents Configuration (not added again by itself).
		public static void AddRemove(string showId, string showName)
		{
			System.IO.File.AppendAllText(File, string.Join("\t", "remove", Clean(showId), Clean(showName)) + Environment.NewLine);
		}

		/// 2026 (1.3.38): put a removed show back (it is added again when MediaPortal next reads your shows).
		public static void AddPutBack(string showId, string showName)
		{
			System.IO.File.AppendAllText(File, string.Join("\t", "putback", Clean(showId), Clean(showName)) + Environment.NewLine);
		}

		/// 2026 (1.3.41): "Grab the missing ones" from MyTorrents Configuration.
		public static void AddGrabMissing(string showId, string showName)
		{
			System.IO.File.AppendAllText(File, string.Join("\t", "grabmissing", Clean(showId), Clean(showName)) + Environment.NewLine);
		}

		/// 2026 (1.3.42): "Try a different copy" from MyTorrents Configuration.
		public static void AddDifferentCopy(string hash, string title, bool deleteFiles)
		{
			System.IO.File.AppendAllText(File, string.Join("\t", "differentcopy", Clean(hash), Clean(title), deleteFiles ? "delete" : "keep") + Environment.NewLine);
		}

		/// 2026 (1.3.43): "Read my movies again" from MyTorrents Configuration.
		public static void AddRereadMovies()
		{
			System.IO.File.AppendAllText(File, string.Join("\t", "rereadmovies", "-", "-") + Environment.NewLine);
		}

		/// 2026 (1.3.49): "Fix it" on the check-up - MP-TVSeries imports this episode at MediaPortal's next round.
		public static void AddImport(string file)
		{
			System.IO.File.AppendAllText(File, string.Join("\t", "import", Clean(file), "-") + Environment.NewLine);
		}

		/// MediaPortal: starts the downloads asked for in MyTorrents Configuration.
		public static void Process(WatchList list)
		{
			string[] lines;
			lock (Lock)
			{
				try
				{
					if (!System.IO.File.Exists(File)) return;
					lines = System.IO.File.ReadAllLines(File);
					System.IO.File.Delete(File);
				}
				catch
				{
					return;
				}
			}
			foreach (string line in lines)
			{
				string[] f = line.Split('\t');
				if (f.Length >= 3 && (f[0] == "check" || f[0] == "remove" || f[0] == "putback" || f[0] == "grabmissing" || f[0] == "differentcopy" || f[0] == "rereadmovies" || f[0] == "import"))
				{
					try
					{
						if (f[0] == "check") list.CheckNow(f[1] == "*" ? null : f[1], "asked for in MyTorrents Configuration");
						else if (f[0] == "remove") list.RemoveById(f[1], f[2]);
						else if (f[0] == "grabmissing") Extras.GrabMissing(f[1], f[2]);
						else if (f[0] == "rereadmovies") LibraryFolders.RereadLibrary();
						else if (f[0] == "import") MovieFiling.ImportTv(new List<string> { f[1] });
						else if (f[0] == "differentcopy") Grabbed.TryDifferentCopy(f[1], f.Length > 3 && f[3] == "delete");
						else list.PutBack(f[1], f[2]);
					}
					catch (Exception ex)
					{
						Log.Instance().Print("Auto grabber: " + f[0] + " " + f[2] + ": " + ex.Message);
					}
					continue;
				}
				if (f.Length < 11 || f[0] != "grab") continue;
				SeriesItem show = list.GetSeriesList().FirstOrDefault(s => s.show != null && (s.show.ID == f[1] || string.Equals(s.show.Name, f[2], StringComparison.OrdinalIgnoreCase)));
				if (show == null)
				{
					Log.Instance().Print($"Get now: {f[2]} is not in the Auto grabber any more - {f[4]} not started");
					continue;
				}
				long size;
				int seeds;
				long.TryParse(f[9], NumberStyles.Integer, CultureInfo.InvariantCulture, out size);
				int.TryParse(f[10], NumberStyles.Integer, CultureInfo.InvariantCulture, out seeds);
				TorrentMatch m = new TorrentMatch { Title = f[4], Url = f[5], magnet = f[6], cookie = f[7], Id = f[8], Size = size, Seed = seeds };
				show.GetNow(m, f[3], "chosen in MyTorrents Configuration");
			}
		}
	}
}
