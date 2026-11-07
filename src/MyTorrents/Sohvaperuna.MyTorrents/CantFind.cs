using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MediaPortal.Configuration;

namespace Sohvaperuna.MyTorrents
{
	/// <summary>
	/// 2026 (1.3.42): the "Can't find" list - episodes the Auto grabber and movies Wanted movies have looked for a long
	/// time (setting "AutoGrabber.CantFindDays", 14) without a copy that fits. They are still looked for, once a day.
	/// From the list: look now, search all torrents, or stop looking.
	/// Episodes are kept in MyTorrents.cant-find.txt (key, show, first looked, last looked, why, state);
	/// movies come from Wanted movies (when it was added, its last look and note).
	/// </summary>
	public static class CantFind
	{
		public sealed class Item
		{
			public string Key = "", ShowId = "", Name = "", Episode = "", Why = "", State = "";
			public DateTime First = DateTime.Now, Last = DateTime.MinValue;
			public bool IsMovie;
			public string Year = "";
			public string What => IsMovie ? Name + (Year.Length > 0 ? " (" + Year + ")" : "") : Name + " " + Episode;
			public string Since => DaysText(First);
		}

		private static readonly object Lock = new object();
		private static Dictionary<string, Item> _items;

		private static string File => Config.GetFile(Config.Dir.Config, "MyTorrents.cant-find.txt");

		public static int Days
		{
			get
			{
				int d;
				return int.TryParse(Configuration.GetExtra("AutoGrabber.CantFindDays", "14"), out d) && d > 0 ? d : 14;
			}
		}

		public static readonly int[] DayChoices = { 3, 7, 14, 30, 60 };

		public static string DaysText(DateTime first)
		{
			int d = (int)(DateTime.Now - first).TotalDays;
			return d <= 0 ? "today" : d == 1 ? "1 day" : d + " days";
		}

		private static Dictionary<string, Item> Items
		{
			get
			{
				if (_items != null) return _items;
				_items = new Dictionary<string, Item>();
				try
				{
					if (System.IO.File.Exists(File))
					{
						foreach (string line in System.IO.File.ReadAllLines(File))
						{
							string[] f = line.Split('\t');
							if (f.Length < 5) continue;
							Item x = new Item { Key = f[0], Name = f[1], Why = f[4], State = f.Length > 5 ? f[5] : "" };
							int bar = f[0].IndexOf('|');
							x.ShowId = bar > 0 ? f[0].Substring(0, bar) : "";
							x.Episode = bar > 0 ? f[0].Substring(bar + 1) : "";
							DateTime d;
							if (DateTime.TryParseExact(f[2], "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out d)) x.First = d;
							if (DateTime.TryParseExact(f[3], "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out d)) x.Last = d;
							_items[x.Key] = x;
						}
					}
				}
				catch
				{
				}
				return _items;
			}
		}

		private static void Save()
		{
			try
			{
				System.IO.File.WriteAllLines(File, Items.Values.Select(x => string.Join("\t", x.Key, Clean(x.Name), x.First.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
					x.Last == DateTime.MinValue ? "" : x.Last.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture), Clean(x.Why), x.State)));
			}
			catch
			{
			}
		}

		private static string Clean(string s) => (s ?? "").Replace("\t", " ").Replace("\r", " ").Replace("\n", " ");

		/// The Auto grabber looked and got nothing (why = "nothing found" / "only too big (smallest 1013 MB)" ...).
		public static void NotFound(string showId, string showName, string episode, string why)
		{
			if (string.IsNullOrEmpty(showId) || string.IsNullOrEmpty(episode)) return;
			lock (Lock)
			{
				string key = showId + "|" + episode.ToUpperInvariant();
				Item x;
				if (!Items.TryGetValue(key, out x)) Items[key] = x = new Item { Key = key, ShowId = showId, Episode = episode.ToUpperInvariant(), First = DateTime.Now };
				x.Name = showName ?? "";
				x.Last = DateTime.Now;
				x.Why = why ?? "";
				Save();
			}
		}

		/// Got it - off the list.
		public static void Found(string showId, string episode)
		{
			lock (Lock)
			{
				if (Items.Remove(showId + "|" + (episode ?? "").ToUpperInvariant())) Save();
			}
		}

		/// On the list (looked for longer than the set days)?
		private static bool Listed(Item x) => x.State != "skip" && DateTime.Now - x.First > TimeSpan.FromDays(Days);

		/// The Auto grabber leaves it for now: you said stop looking, or it's on the list and was looked at today.
		public static bool Resting(string showId, string episode)
		{
			lock (Lock)
			{
				Item x;
				if (!Items.TryGetValue(showId + "|" + (episode ?? "").ToUpperInvariant(), out x)) return false;
				if (x.State == "skip") return true;
				return Listed(x) && DateTime.Now - x.Last < TimeSpan.FromHours(24);
			}
		}

		/// Look at it at the next check (not tomorrow).
		public static void LookNow(string key)
		{
			lock (Lock)
			{
				_items = null; // re-read (MyTorrents Configuration may have changed it)
				Item x;
				if (Items.TryGetValue(key, out x))
				{
					x.Last = DateTime.MinValue;
					if (x.State == "skip") x.State = "";
					Save();
				}
			}
		}

		/// Stop looking for this episode (you can look again from the list).
		public static void StopLooking(string key)
		{
			lock (Lock)
			{
				_items = null;
				Item x;
				if (Items.TryGetValue(key, out x))
				{
					x.State = "skip";
					Save();
				}
			}
		}

		/// MediaPortal: forget episodes you now have (or shows no longer in the Auto grabber).
		public static void Prune(Func<string, string, bool> stillWanted)
		{
			lock (Lock)
			{
				_items = null;
				List<string> gone = Items.Values.Where(x => !stillWanted(x.ShowId, x.Episode)).Select(x => x.Key).ToList();
				foreach (string k in gone) Items.Remove(k);
				if (gone.Count > 0) Save();
			}
		}

		/// Everything on the list - episodes and Wanted movies - longest first. Stopped ones are included (State "skip").
		public static List<Item> All()
		{
			List<Item> list = new List<Item>();
			lock (Lock)
			{
				_items = null; // always fresh from the file (MediaPortal and Configuration both use it)
				list.AddRange(Items.Values.Where(x => x.State == "skip" || Listed(x)).Select(x => x));
			}
			foreach (WantedFilms.Film f in WantedFilms.Read().Where(f => f.State == "wanted" && f.LastLook != DateTime.MinValue && DateTime.Now - f.Added > TimeSpan.FromDays(Days)))
			{
				list.Add(new Item { IsMovie = true, Key = "movie|" + f.Title + "|" + f.Year, Name = f.Title, Year = f.Year, First = f.Added, Last = f.LastLook, Why = f.Note.Length > 0 ? f.Note : "no good copy yet" });
			}
			return list.OrderByDescending(x => x.State != "skip").ThenBy(x => x.First).ToList();
		}

		/// 2026 (1.3.44): every episode the Auto grabber looked for and didn't find (however long ago it started) -
		/// for "Save the list".
		public static List<Item> AllNotFound()
		{
			lock (Lock)
			{
				_items = null;
				return Items.Values.Where(x => x.State != "skip").OrderBy(x => x.Name).ThenBy(x => x.Episode).ToList();
			}
		}

		/// For Wanted movies: a movie on the list is looked for once a day instead of every few hours.
		public static bool MovieResting(WantedFilms.Film f) => DateTime.Now - f.Added > TimeSpan.FromDays(Days) && DateTime.Now - f.LastLook < TimeSpan.FromHours(24);
	}
}
