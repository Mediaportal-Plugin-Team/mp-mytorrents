using System;
using System.Collections.Generic;
using System.Linq;
using MediaPortal.Configuration;

namespace Sohvaperuna.MyTorrents
{
	/// <summary>
	/// 2026: running / continuing MP-TVSeries shows are put in the Auto grabber by themselves (new episodes only).
	/// Ended shows never are. A show you are well behind on (its next episode aired more than a month ago) is left
	/// out, so old seasons are not downloaded one after another - add those yourself if you want them.
	/// A show you remove from the Auto grabber is remembered (MyTorrents.autoadd-skip.txt) and not added again.
	/// </summary>
	public static class AutoAdd
	{
		/// on = "Automatic" (keeps running shows up to date), off = "Manual" (only what you search for or add) -
		/// setting "AutoGrabber.AutoAddRunning". 2026 (1.3.40): when never set, Manual for a new install, Automatic
		/// for one that has already been running the Auto grabber
		public static bool Enabled
		{
			get
			{
				string v = Configuration.GetExtra("AutoGrabber.AutoAddRunning", "");
				if (v.Length == 0)
				{
					try
					{
						return System.IO.File.Exists(Config.GetFile(Config.Dir.Config, "MyTorrents.autograbber.txt"));
					}
					catch
					{
						return false;
					}
				}
				return !v.Equals("false", StringComparison.OrdinalIgnoreCase);
			}
		}

		public static string ModeText(bool automatic) => automatic ? "Automatic - keeps my running shows up to date" : "Manual - only what I search for or add";

		/// a show whose next episode aired longer ago than this is "behind" (hidden setting "AutoGrabber.AutoAddBehindDays")
		public static int BehindDays
		{
			get
			{
				int d;
				return int.TryParse(Configuration.GetExtra("AutoGrabber.AutoAddBehindDays", "30"), out d) && d > 0 ? d : 30;
			}
		}

		/// only shows in the TV library folder ("copy to library" on the Finished downloads tab) - setting
		/// "AutoGrabber.AutoAddOnlyLibrary", on unless switched off
		public static bool OnlyLibraryFolder
		{
			get { return !Configuration.GetExtra("AutoGrabber.AutoAddOnlyLibrary", "true").Equals("false", StringComparison.OrdinalIgnoreCase); }
		}

		/// true when the show's files are in that folder (or there is no such folder set)
		public static bool InLibraryFolder(Series s, string folder)
		{
			if (string.IsNullOrWhiteSpace(folder)) return true;
			string f = folder.Trim().Replace('/', '\\').TrimEnd('\\') + "\\";
			string file = (s.OneFile ?? "").Replace('/', '\\');
			return file.StartsWith(f, StringComparison.OrdinalIgnoreCase);
		}

		private static readonly object Lock = new object();
		private static HashSet<string> _skip;
		private static readonly HashSet<string> _toldBehind = new HashSet<string>();

		private static string SkipFile => Config.GetFile(Config.Dir.Config, "MyTorrents.autoadd-skip.txt");

		private static HashSet<string> SkipList
		{
			get
			{
				if (_skip != null) return _skip;
				_skip = new HashSet<string>();
				try
				{
					if (System.IO.File.Exists(SkipFile))
					{
						foreach (string line in System.IO.File.ReadAllLines(SkipFile))
						{
							string id = line.Split('\t')[0].Trim();
							if (id.Length > 0) _skip.Add(id);
						}
					}
				}
				catch
				{
				}
				return _skip;
			}
		}

		private static void SaveSkip(Dictionary<string, string> names)
		{
			try
			{
				System.IO.File.WriteAllLines(SkipFile, SkipList.Select(id => id + "\t" + (names != null && names.ContainsKey(id) ? names[id] : "")));
			}
			catch
			{
			}
		}

		/// "Continuing" (TheTVDB), "Returning Series" / "In Production" (TMDb) ... count as running.
		/// Ended / cancelled shows and shows with no status are left alone.
		public static bool IsRunning(string status)
		{
			string s = (status ?? "").Trim().ToLowerInvariant();
			if (s.Length == 0) return false;
			return !(s.StartsWith("ended") || s.StartsWith("cancel") || s.StartsWith("canceled"));
		}

		/// 2026 (1.3.38): the shows you removed (id, name) - read straight from the file, for MyTorrents Configuration.
		public static List<string[]> RemovedShows()
		{
			List<string[]> result = new List<string[]>();
			try
			{
				if (System.IO.File.Exists(SkipFile))
				{
					foreach (string line in System.IO.File.ReadAllLines(SkipFile))
					{
						string[] p = line.Split('\t');
						if (p[0].Trim().Length > 0) result.Add(new[] { p[0].Trim(), p.Length > 1 && p[1].Trim().Length > 0 ? p[1].Trim() : p[0].Trim() });
					}
				}
			}
			catch
			{
			}
			return result;
		}

		/// A show you removed: not added again by itself.
		public static void Skip(SeriesItem item)
		{
			string id = item?.show?.ID;
			if (string.IsNullOrEmpty(id)) return;
			lock (Lock)
			{
				if (SkipList.Add(id))
				{
					try
					{
						System.IO.File.AppendAllText(SkipFile, id + "\t" + item.show.Name + Environment.NewLine);
					}
					catch
					{
					}
				}
			}
		}

		/// A show you added yourself: allowed again.
		public static void Unskip(string id)
		{
			if (string.IsNullOrEmpty(id)) return;
			lock (Lock)
			{
				if (SkipList.Remove(id)) SaveSkip(null);
			}
		}

		/// true when the show's next missing episode aired too long ago
		public static bool IsBehind(Series s, out int airedMissing)
		{
			airedMissing = s.AiredMissing?.Count ?? 0;
			return !string.IsNullOrEmpty(s.FirstUnlocal) && s.NextAirsUtc != DateTime.MinValue && s.NextAirsUtc < DateTime.UtcNow.AddDays(-BehindDays);
		}

		/// Called after each read of MP-TVSeries' shows. Returns the shows to add (the caller adds them).
		public static List<Series> Pick(IEnumerable<Series> allShows, IEnumerable<SeriesItem> inAutoGrabber)
		{
			List<Series> add = new List<Series>();
			if (!Enabled) return add;
			HashSet<string> have = new HashSet<string>(inAutoGrabber.Where(i => i.show != null).Select(i => i.show.ID));
			string folder = OnlyLibraryFolder ? PostProcessor.TVFolder : "";
			List<string> behind = new List<string>();
			int behindCount = 0;
			lock (Lock)
			{
				foreach (Series s in allShows)
				{
					if (s == null || string.IsNullOrEmpty(s.ID) || !IsRunning(s.Status) || have.Contains(s.ID) || SkipList.Contains(s.ID)) continue;
					if (!InLibraryFolder(s, folder)) continue;
					int missing;
					if (IsBehind(s, out missing))
					{
						behindCount++;
						if (_toldBehind.Add(s.ID)) behind.Add(s.Name + " (" + missing + " missing)");
						continue;
					}
					add.Add(s);
				}
			}
			if (behind.Count > 0)
			{
				Log.Instance().Print($"Auto add: {behindCount} running show(s) not added - more than {BehindDays} days behind, add them yourself if you want them: " + string.Join(", ", behind));
			}
			return add;
		}
	}
}
