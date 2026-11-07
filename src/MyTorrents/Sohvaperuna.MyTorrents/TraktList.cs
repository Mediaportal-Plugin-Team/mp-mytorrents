using System;
using System.Collections.Generic;
using System.Linq;
using MediaPortal.Configuration;
using TraktAPI.DataModels;

namespace Sohvaperuna.MyTorrents
{
	/// <summary>
	/// 2026 (1.3.42): your own Trakt list - MediaPortal reads it every few hours through the
	/// Trakt plugin's login:
	///  - a movie goes into Wanted movies; once it is safely in the movies folder it is taken off the list
	///  - a movie you already have comes straight off the list
	///  - a TV show goes into the Auto grabber (the whole series, or missing episodes if you have some) and comes off
	/// The movies it added are remembered in MyTorrents.trakt-list.txt (so only those are taken off when done).
	/// </summary>
	public static class TraktList
	{
		private static DateTime _last = DateTime.MinValue;
		private static int _busy;

		/// The list's name on Trakt (setting "Trakt.ListName", "MCM"); "" = off.
		public static string ListName => (Configuration.GetExtra("Trakt.ListName", "MCM") ?? "").Trim();

		public static bool On => Configuration.GetExtra("Trakt.ListOn", "true") != "false" && ListName.Length > 0;

		/// 2026 (1.3.45): a movie you take off Wanted movies is taken off your Trakt list too (setting
		/// "Trakt.ListRemoveDropped", on).
		public static bool RemoveDropped => Configuration.GetExtra("Trakt.ListRemoveDropped", "true") != "false";

		/// Hours between looks (setting "Trakt.ListEveryHours", 3).
		public static int EveryHours
		{
			get
			{
				int h;
				return int.TryParse(Configuration.GetExtra("Trakt.ListEveryHours", "3"), out h) && h > 0 ? h : 3;
			}
		}

		private static string File => Config.GetFile(Config.Dir.Config, "MyTorrents.trakt-list.txt");

		/// Movies this list put into Wanted movies: "traktId \t title \t year"
		private static List<string[]> Added()
		{
			try
			{
				return System.IO.File.Exists(File) ? System.IO.File.ReadAllLines(File).Select(l => l.Split('\t')).Where(p => p.Length >= 3).ToList() : new List<string[]>();
			}
			catch
			{
				return new List<string[]>();
			}
		}

		private static void SaveAdded(List<string[]> list)
		{
			try
			{
				System.IO.File.WriteAllLines(File, list.Select(p => string.Join("\t", p)));
			}
			catch
			{
			}
		}

		/// MediaPortal: called with the Auto grabber's checks; does the work every few hours (or now).
		public static void Check(bool now = false)
		{
			if (!On) return;
			if (!now && DateTime.Now - _last < TimeSpan.FromHours(EveryHours)) return;
			if (System.Threading.Interlocked.Exchange(ref _busy, 1) == 1) return;
			try
			{
				_last = DateTime.Now;
				Run();
			}
			catch (Exception ex)
			{
				Log.Instance().Print("Trakt list: " + ex.Message);
			}
			finally
			{
				System.Threading.Interlocked.Exchange(ref _busy, 0);
			}
		}

		private static void Run()
		{
			string name = ListName;
			TraktListDetail list = (global::TraktAPI.TraktAPI.GetUserLists("me") ?? Enumerable.Empty<TraktListDetail>())
				.FirstOrDefault(l => string.Equals((l.Name ?? "").Trim(), name, StringComparison.OrdinalIgnoreCase) || string.Equals(l.Ids?.Slug, name, StringComparison.OrdinalIgnoreCase));
			if (list == null)
			{
				// 2026 (1.3.56): the background helper isn't signed in to Trakt (only MediaPortal's Trakt plugin is) -
				// it just leaves the list to MediaPortal instead of saying so every hour
				if (Log.Prefix.Length > 0) return;
				Log.Instance().Print("Trakt list: no list called \"" + name + "\" on your Trakt account (or the Trakt plugin is not signed in)");
				return;
			}
			string id = list.Ids?.Slug ?? list.Ids?.Trakt?.ToString();
			List<TraktListItem> items = (global::TraktAPI.TraktAPI.GetUserListItems("me", id, "min", 1, 500)?.Items ?? Enumerable.Empty<TraktListItem>()).ToList();
			List<string[]> added = Added();
			// movies this list added that you have since taken off Wanted movies
			HashSet<string> stillWanted = new HashSet<string>(WantedFilms.Read().Select(f => MediaNames.Key(f.Title) + "|" + f.Year));
			List<TraktMovie> removeMovies = new List<TraktMovie>();
			List<TraktShow> removeShows = new List<TraktShow>();
			int newMovies = 0, newShows = 0;
			foreach (TraktListItem item in items)
			{
				try
				{
					if (item.Movie != null)
					{
						TraktMovie m = item.Movie;
						int year = m.Year ?? 0;
						string tid = m.Ids?.Trakt?.ToString() ?? m.Title;
						bool ours = added.Any(p => p[0] == tid);
						if (ours ? LibraryFolders.HaveFilmSafely(m.Title, year) : LibraryFolders.HaveFilm(m.Title, year))
						{
							// in the movies folder (ours: safely - Moving Pictures has it, or the file is complete)
							removeMovies.Add(m);
							added.RemoveAll(p => p[0] == tid);
							Log.Instance().Print("Trakt list: " + m.Title + " (" + year + ") is in your movies - taken off \"" + name + "\"");
							continue;
						}
						if (ours && RemoveDropped && !stillWanted.Contains(MediaNames.Key(m.Title) + "|" + (year > 0 ? year.ToString() : "")) &&
							!stillWanted.Contains(MediaNames.Key(m.Title) + "|"))
						{
							removeMovies.Add(m);
							added.RemoveAll(p => p[0] == tid);
							Log.Instance().Print("Trakt list: " + m.Title + " (" + year + ") was taken off Wanted movies - taken off \"" + name + "\" too");
							continue;
						}
						if (!ours)
						{
							WantedFilms.Add(m.Title, year > 0 ? year.ToString() : "");
							added.Add(new[] { tid, m.Title, year.ToString() });
							newMovies++;
							Log.Instance().Print("Trakt list: " + m.Title + " (" + year + ") added to Wanted movies");
						}
					}
					else if (item.Show != null)
					{
						TraktShowSummary s = item.Show;
						// 2026 (1.3.50): a show handled before (its removal from the list didn't go through) - not again:
						// every run used to add the same 28 shows and start their searches over
						string sid = "show:" + (s.Ids?.Trakt?.ToString() ?? s.Title);
						if (added.Any(p => p[0] == sid))
						{
							removeShows.Add(s);
							continue;
						}
						bool done;
						Series have = MyTorrents.WL.allseries.FirstOrDefault(x => x != null && (x.ID == (s.Ids?.Tvdb?.ToString() ?? "-") ||
							MediaNames.Key(x.Name) == MediaNames.Key(s.Title)));
						if (have != null)
						{
							// you have some of it - missing episodes (and new ones as they come)
							Extras.GrabMissing(have.ID, have.Name);
							done = true;
						}
						else done = WatchListView.AddWholeSeries(s);
						if (done)
						{
							removeShows.Add(s);
							added.Add(new[] { sid, s.Title, (s.Year ?? 0).ToString() });
							newShows++;
							Log.Instance().Print("Trakt list: " + s.Title + " added to the Auto grabber - taken off \"" + name + "\"");
						}
					}
				}
				catch (Exception ex)
				{
					Log.Instance().Print("Trakt list: " + ex.Message);
				}
			}
			// forget ones that are no longer on the list (taken off by hand)
			HashSet<string> onList = new HashSet<string>(items.Where(i => i.Movie != null).Select(i => i.Movie.Ids?.Trakt?.ToString() ?? i.Movie.Title)
				.Concat(items.Where(i => i.Show != null).Select(i => "show:" + (i.Show.Ids?.Trakt?.ToString() ?? i.Show.Title))));
			added.RemoveAll(p => !onList.Contains(p[0]));
			SaveAdded(added);
			if (removeMovies.Count > 0 || removeShows.Count > 0)
			{
				// 2026 (1.3.50): only the ids, a few at a time - the whole records in one go were turned down by Trakt
				// ("400 Bad Request"), so nothing ever came off the list
				List<TraktMovie> ms = removeMovies.Where(m => m.Ids != null).Select(m => new TraktMovie { Ids = m.Ids }).ToList();
				List<TraktShow> ss = removeShows.Where(x => x.Ids != null).Select(x => new TraktShow { Ids = x.Ids }).ToList();
				int failed = 0;
				for (int i = 0; i < Math.Max(ms.Count, ss.Count); i += 50)
				{
					try
					{
						var r = global::TraktAPI.TraktAPI.RemoveItemsFromList("me", id, new TraktSyncAll { Movies = ms.Skip(i).Take(50).ToList(), Shows = ss.Skip(i).Take(50).ToList() });
						if (r == null) failed++;
					}
					catch (Exception ex)
					{
						failed++;
						Log.Instance().Print("Trakt list: could not take items off the list: " + ex.Message);
					}
				}
				if (failed > 0) Log.Instance().Print("Trakt list: Trakt didn't take " + (failed == 1 ? "some" : "all") + " of them off \"" + name + "\" - they're remembered, so they aren't added again");
			}
			Log.Instance().Print($"Trakt list \"{name}\": {items.Count} on it, {newMovies} movie(s) added to Wanted movies, {newShows} show(s) to the Auto grabber, {removeMovies.Count + removeShows.Count} taken off");
			if (newMovies > 0) WantedFilms.Check(true);
		}
	}
}
