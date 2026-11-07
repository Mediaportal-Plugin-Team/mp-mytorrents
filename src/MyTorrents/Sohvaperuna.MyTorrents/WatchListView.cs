using System;
using System.Collections.Generic;
using System.Linq;
using MediaPortal.Dialogs;
using MediaPortal.GUI.Library;
using Sohvaperuna.RSSReader;
using Sohvaperuna.TorrentEngine;
using Sohvaperuna.TorrentSearch;
using TraktAPI.DataModels;

namespace Sohvaperuna.MyTorrents
{
	internal class WatchListView
	{
		private List<GUIListItem> torrentList = new List<GUIListItem>();

		private SeriesItem SelectedWatch = new SeriesItem();

		private string folder = string.Empty;

		private void SetNames()
		{
			GUIPropertyManager.SetProperty("#MyTorrents.TVSeriesCollector.SeriesName", "");
			GUIPropertyManager.SetProperty("#MyTorrents.TVSeriesCollector.SeriesPoster", "");
			GUIPropertyManager.SetProperty("#MyTorrents.TVSeriesCollector.SeriesInfo", "");
			GUIPropertyManager.SetProperty("#MyTorrents.TVSeriesCollector.SeriesGenres", "");
			GUIPropertyManager.SetProperty("#MyTorrents.TVSeriesCollector.SeriesRating", "");
		}

		public List<GUIListItem> UpdateWatches()
		{
			if (MyTorrents.WL.MPTVSeriesWorker.IsBusy)
			{
			}
			torrentList.Clear();
			// 2026: A-Z (ignoring "The" / "A"), not in the order they were added
			foreach (WatchItem watchItem in MyTorrents.WL._watchItemList.ToList().OrderBy(w => SortName(w.GetLabel1())))
			{
				GUIListItem gUIListItem = new GUIListItem();
				gUIListItem.Label = watchItem.GetLabel1();
				gUIListItem.Label2 = watchItem.GetLabel2();
				gUIListItem.AlbumInfoTag = watchItem;
				torrentList.Add(gUIListItem);
			}
			if (torrentList.Count == 0)
			{
				GUIListItem gUIListItem = new GUIListItem();
				gUIListItem.Label = "No watchlist items.";
				torrentList.Add(gUIListItem);
			}
			return torrentList;
		}

		public void ContextMenu(object info)
		{
			WatchItem watchItem = info as WatchItem;
			GUIDialogMenu gUIDialogMenu = (GUIDialogMenu)GUIWindowManager.GetWindow(2012);
			if (gUIDialogMenu == null)
			{
				return;
			}
			gUIDialogMenu.Reset();
			gUIDialogMenu.SetHeading(watchItem is SeriesItem ? (watchItem as SeriesItem).show?.Name ?? "Auto grabber" : "Auto grabber");
			if (watchItem is SeriesItem) gUIDialogMenu.Add("Show details"); // 2026 (1.3.42): OK on a show opens this menu
			gUIDialogMenu.Add("Auto grabber settings");
			gUIDialogMenu.Add("Add Series");
			gUIDialogMenu.Add("Search Series");
			gUIDialogMenu.Add("Add Movie");
			gUIDialogMenu.Add("Add custom");
			gUIDialogMenu.Add("Wanted movies...");
			gUIDialogMenu.Add("Missing episodes report...");
			gUIDialogMenu.Add("Can't find...");
			gUIDialogMenu.Add("Trakt lists...");
			gUIDialogMenu.Add(CheckUp.Summary() + "..."); // 2026 (1.3.49)
			if (watchItem != null)
			{
				if ((object)watchItem.GetType() == typeof(SeriesItem))
				{
					// 2026: one episode now, whatever its size - settings unchanged
					string next = AutoGrabberStatus.NextEpisode(watchItem as SeriesItem);
					if (next.Length > 0) gUIDialogMenu.Add("Get " + next + " now - just this once...");
					gUIDialogMenu.Add("Search again...");
					gUIDialogMenu.Add("What's missing...");
					gUIDialogMenu.Add("Series Actions");
				}
				gUIDialogMenu.Add("Force Search");
				gUIDialogMenu.Add("Remove Item");
				gUIDialogMenu.Add("Clear list");
				gUIDialogMenu.Add("Force RSS update");
			}
			gUIDialogMenu.DoModal(GUIWindowManager.ActiveWindow);
			Menus.Check(gUIDialogMenu);
			if ((gUIDialogMenu.SelectedLabelText ?? "").StartsWith("Get ") && watchItem is SeriesItem)
			{
				GetNowMenu(watchItem as SeriesItem);
				return;
			}
			switch (gUIDialogMenu.SelectedLabelText)
			{
			case "Show details":
				MyTorrents.Instance().ShowSeriesDetails();
				break;
			case "Auto grabber settings":
				DefaultsMenu();
				break;
			case "Add Series":
				AddSeries();
				MyTorrents.Instance().ShowTorrentWatch();
				break;
			case "Search Series":
				SearchSeries();
				break;
			case "Series Actions":
				SeriesActions(watchItem as SeriesItem);
				break;
			case "Search again...":
				Extras.SearchAgain(watchItem as SeriesItem);
				break;
			case "What's missing...":
				Extras.WhatsMissing(watchItem as SeriesItem);
				break;
			case "Wanted movies...":
				Extras.WantedFilmsMenu();
				break;
			case "Can't find...":
				Extras.CantFindMenu();
				break;
			case var c when c != null && c.StartsWith("Check-up"):
				Extras.CheckUpMenu();
				break;
			case "Missing episodes report...":
				Extras.MissingReport();
				break;
			case "Trakt lists...":
				Extras.TraktLists(null, (watchItem as SeriesItem)?.show?.Name);
				MyTorrents.Instance().ShowTorrentWatch();
				break;
			case "Force Search":
				MyTorrents.WL.SearchTorrent();
				break;
			case "Add custom":
			{
				VirtualKeyboard virtualKeyboard = (VirtualKeyboard)GUIWindowManager.GetWindow(1002);
				virtualKeyboard.Reset();
				virtualKeyboard.Text = "";
				virtualKeyboard.DoModal(GUIWindowManager.ActiveWindow);
				Menus.Check(virtualKeyboard);
				if (!virtualKeyboard.IsConfirmed)
				{
					return;
				}
				watchItem = new WatchItem();
				watchItem.SearchTerm = virtualKeyboard.Text;
				TorrentLabel torrentLabel = DialogAskLabel.Ask();
				if (torrentLabel != null)
				{
					watchItem.Label = torrentLabel.Name;
					MyTorrents.WL.AddWatch(watchItem);
					Log.Instance().Print($"Watch [{watchItem.SearchTerm}] added.");
				}
				break;
			}
			case "Clear list":
			{
				// 2026: asks first - it removes every show (and they are not added back by themselves)
				GUIDialogYesNo sure = (GUIDialogYesNo)GUIWindowManager.GetWindow(100);
				if (sure == null) break;
				sure.Reset();
				sure.SetHeading("Clear the whole list?");
				sure.SetLine(1, "All " + MyTorrents.WL._watchItemList.Count + " items are removed from the Auto grabber.");
				sure.SetLine(2, "Shows are not added back by themselves.");
				sure.SetLine(3, "Nothing you have is deleted.");
				sure.SetDefaultToYes(false);
				sure.DoModal(GUIWindowManager.ActiveWindow);
				Menus.Check(sure);
				if (sure.IsConfirmed) MyTorrents.WL.Clear();
				break;
			}
			case "Remove Item":
				MyTorrents.WL.RemoveWatch(watchItem);
				break;
			case "Force RSS update":
				RSSChannelManager.Instance().UpdateChannels(force: true);
				break;
			}
			MyTorrents.Instance().ShowTorrentWatch();
		}

		/// 2026: "Get SxxEyy now - just this once": the copies there are, smallest first; the one picked is downloaded
		/// whatever the size limit says, and nothing in the settings changes.
		public void GetNowMenu(SeriesItem s)
		{
			string episode = AutoGrabberStatus.NextEpisode(s);
			if (s?.show == null || episode.Length == 0) return;
			GUIWaitCursor.Show();
			new System.Threading.Thread(() =>
			{
				string error = null;
				List<TorrentMatch> copies = new List<TorrentMatch>();
				try
				{
					copies = EpisodeCopies.Find(s.show.ID, s.show.OriginalName ?? s.show.Name, episode, out error);
				}
				catch (System.Exception ex)
				{
					error = ex.Message;
				}
				GUIWindowManager.SendThreadCallback((p1, p2, data) =>
				{
					GUIWaitCursor.Hide();
					if (copies.Count == 0)
					{
						Notify("Get " + episode + " now", error ?? (s.show.Name + " " + episode + ": no copies found yet\n(none seeded, or only fakes)."));
						return 0;
					}
					GUIDialogMenu menu = (GUIDialogMenu)GUIWindowManager.GetWindow(2012);
					if (menu == null) return 0;
					menu.Reset();
					menu.SetHeading(s.show.Name + " " + episode + " - pick a copy (smallest first)");
					List<TorrentMatch> shown = copies.Take(12).ToList();
					foreach (TorrentMatch m in shown) menu.Add(EpisodeCopies.Describe(m));
					menu.DoModal(GUIWindowManager.ActiveWindow);
					Menus.Check(menu);
					int pick = menu.SelectedId;
					if (pick < 1 || pick > shown.Count) return 0;
					TorrentMatch chosen = shown[pick - 1];
					new System.Threading.Thread(() =>
					{
						bool ok = false;
						try
						{
							ok = s.GetNow(chosen, episode, "chosen in MediaPortal");
						}
						catch (System.Exception ex)
						{
							Log.Instance().Print("Get now: " + ex.Message);
						}
						GUIWindowManager.SendThreadCallback((q1, q2, d2) =>
						{
							Notify("Get " + episode + " now", ok ? "Started: " + chosen.Title : "Could not start " + chosen.Title);
							MyTorrents.Instance().ShowTorrentWatch();
							return 0;
						}, 0, 0, null);
					}) { IsBackground = true }.Start();
					return 0;
				}, 0, 0, null);
			}) { IsBackground = true }.Start();
		}

		private static void Notify(string heading, string text)
		{
			try
			{
				GUIDialogNotify n = (GUIDialogNotify)GUIWindowManager.GetWindow(2016);
				if (n == null) return;
				n.Reset();
				n.SetHeading(heading);
				n.SetText(text);
				n.TimeOut = 8;
				n.DoModal(GUIWindowManager.ActiveWindow);
				Menus.Check(n);
			}
			catch
			{
			}
		}

		public void SeriesActions(SeriesItem series)
		{
			GUIDialogMenu gUIDialogMenu = (GUIDialogMenu)GUIWindowManager.GetWindow(2012);
			if (gUIDialogMenu != null)
			{
				gUIDialogMenu.Reset();
				gUIDialogMenu.SetHeading("Series Actions");
				gUIDialogMenu.Add("Download");
				gUIDialogMenu.Add("Search Series");
				gUIDialogMenu.Add("Add Movie");
				gUIDialogMenu.Add("Add custom");
				gUIDialogMenu.Add("Force Search");
				gUIDialogMenu.Add("Remove Item");
				gUIDialogMenu.Add("Clear list");
				gUIDialogMenu.Add("Force RSS update");
				gUIDialogMenu.DoModal(GUIWindowManager.ActiveWindow);
				Menus.Check(gUIDialogMenu);
			}
		}

		/// "The Killing (US)" -> "killing (us)" so the list is A-Z the way people look for shows
		internal static string SortName(string name)
		{
			string n = (name ?? "").Trim();
			if (n.StartsWith("The ", System.StringComparison.OrdinalIgnoreCase)) n = n.Substring(4);
			else if (n.StartsWith("A ", System.StringComparison.OrdinalIgnoreCase) && n.Length > 2) n = n.Substring(2);
			return n.ToLowerInvariant();
		}

		/// 2026: shows from MP-TVSeries, A-Z (ignoring "The"), without the ones already in the Auto grabber.
		/// A long list is split by letter first, so it is quick with a remote.
		public void AddSeries()
		{
			GUIDialogMenu menu = (GUIDialogMenu)GUIWindowManager.GetWindow(2012);
			if (menu == null)
			{
				return;
			}
			HashSet<string> have = new HashSet<string>(MyTorrents.WL.GetSeriesList().Where(w => w.show != null).Select(w => w.show.ID ?? ""));
			List<Series> shows = MyTorrents.WL.allseries
				.Where(x => x != null && !string.IsNullOrEmpty(x.Name) && !have.Contains(x.ID ?? ""))
				.OrderBy(x => SortName(x.Name)).ToList();
			if (shows.Count == 0)
			{
				WatchList wl = MyTorrents.WL;
				if (wl.allseries.Count == 0)
				{
					// nothing read from MP-TVSeries yet (still starting, or it failed) - read again in the background
					if (!wl.IsMPTVSeriesAvailable())
					{
						MyTorrents.Error("Add a show", "MP-TVSeries is not running,\nso there are no TV shows to add.");
						return;
					}
					System.Threading.ThreadPool.QueueUserWorkItem(_ => wl.ReadShows());
					MyTorrents.Error("Add a show", "Reading your shows from MP-TVSeries.\nTry again in a minute.");
					return;
				}
				MyTorrents.Error("Add a show", "Every show in MP-TVSeries is\nalready in the Auto grabber.");
				return;
			}
			List<Series> pickFrom = shows;
			if (shows.Count > 25)
			{
				// letter groups that have shows in them: "A - C", "D - F", ... and "0 - 9" for the rest
				string[] groups = { "ABC", "DEF", "GHI", "JKL", "MNO", "PQR", "STU", "VWXYZ" };
				Func<Series, string> groupOf = x =>
				{
					char c = char.ToUpperInvariant(SortName(x.Name).FirstOrDefault());
					string g = groups.FirstOrDefault(gr => gr.IndexOf(c) >= 0);
					return g ?? "#";
				};
				List<string> used = groups.Where(g => shows.Any(x => groupOf(x) == g)).ToList();
				if (shows.Any(x => groupOf(x) == "#")) used.Add("#");
				menu.Reset();
				menu.SetHeading("Add a show - pick its first letter");
				foreach (string g in used)
				{
					int n = shows.Count(x => groupOf(x) == g);
					menu.Add((g == "#" ? "0 - 9 and others" : g[0] + " - " + g[g.Length - 1]) + "   (" + n + ")");
				}
				menu.DoModal(GUIWindowManager.ActiveWindow);
				Menus.Check(menu);
				if (menu.SelectedId < 1 || menu.SelectedId > used.Count) return;
				string chosen = used[menu.SelectedId - 1];
				pickFrom = shows.Where(x => groupOf(x) == chosen).ToList();
			}
			menu.Reset();
			menu.SetHeading("Choose a show to add");
			foreach (Series item in pickFrom)
			{
				menu.Add(item.Name);
			}
			menu.DoModal(GUIWindowManager.ActiveWindow);
			Menus.Check(menu);
			if (menu.SelectedId < 1 || menu.SelectedId > pickFrom.Count)
			{
				return;
			}
			AddToAutoGrabber(pickFrom[menu.SelectedId - 1], SeriesItem.NewShowType);
		}

		/// 2026: puts an MP-TVSeries show in the Auto grabber (used by Add Series and the My TV Series menu)
		public static void AddToAutoGrabber(Series series, string type)
		{
			SeriesItem seriesItem = MakeItem(series, type);
			AutoAdd.Unskip(series.ID); // added by you - allowed again
			seriesItem.LastCheck = System.DateTime.Now.AddMinutes(-26.0); // first look in about 5 minutes
			MyTorrents.WL._watchItemList.Add(seriesItem);
			MyTorrents.WL.Save();
			Log.Instance().Print("Auto grabber: added " + series.Name + " (" + type + ")");
		}

		/// 2026: an Auto grabber entry for an MP-TVSeries show (not added to the list)
		public static SeriesItem MakeItem(Series series, string type)
		{
			SeriesItem seriesItem = new SeriesItem();
			seriesItem.Added = System.DateTime.Now;
			seriesItem.quality = SeriesItem.Quality;
			seriesItem.source = "";
			seriesItem.type = type;
			seriesItem.show = new Series();
			seriesItem.show.Name = series.Name;
			seriesItem.show.OriginalName = series.OriginalName;
			seriesItem.show.ID = series.ID;
			seriesItem.show.AllUnlocal = series.AllUnlocal;
			seriesItem.show.FirstUnlocal = series.FirstUnlocal;
			seriesItem.show.NextAirsUtc = series.NextAirsUtc;
			seriesItem.show.AirsByCode = series.AirsByCode; // 2026 (1.3.39)
			seriesItem.show.SeasonSizes = series.SeasonSizes; // 2026 (1.3.41)
			seriesItem.show.SeasonLastAired = series.SeasonLastAired;
			seriesItem.show.AiredMissing = series.AiredMissing;
			seriesItem.show.Poster = series.Poster;
			seriesItem.show.Description = series.Description;
			seriesItem.show.Genres = series.Genres;
			seriesItem.show.Rating = series.Rating;
			seriesItem.show.Status = series.Status;
			seriesItem.tracker = TorrentSearchEngine.TVSeriesEngineName;
			return seriesItem;
		}

		/// 2026: opened from a show in My TV Series - ask what to do instead of searching the show's name
		/// (which lists every episode, including all the ones already in the library).
		/// Returns false when it is not a known show, so the normal search runs.
		public bool FromTVSeries(string searchText)
		{
			WatchList wl = MyTorrents.WL;
			string key = MediaNames.Key(searchText);
			Series series = wl.allseries.FirstOrDefault(x => x != null && (MediaNames.Key(x.Name) == key || MediaNames.Key(x.OriginalName) == key));
			if (series == null)
			{
				// the full list may still be loading (just after start-up) - read this one show now
				try
				{
					series = wl.ReadOneShow(searchText);
				}
				catch (Exception ex)
				{
					Log.Instance().Print("From My TV Series: could not read " + searchText + ": " + ex.Message);
				}
			}
			if (series == null) return false;
			SeriesItem already = wl.GetSeriesList().FirstOrDefault(x => x.show != null && x.show.ID == series.ID);
			List<string> missing = series.AiredMissing ?? new List<string>();
			GUIDialogMenu menu = (GUIDialogMenu)GUIWindowManager.GetWindow(2012);
			if (menu == null) return false;
			menu.Reset();
			menu.SetHeading(series.Name);
			List<string> acts = new List<string>();
			if (missing.Count > 0)
			{
				menu.Add("Missing episodes (" + missing.Count + ")");
				acts.Add("missing");
			}
			else
			{
				menu.Add("No missing episodes");
				acts.Add("none");
			}
			if (already == null)
			{
				menu.Add("Auto grab new episodes");
				acts.Add("new");
				if (missing.Count > 0)
				{
					menu.Add("Auto grab new + missing");
					acts.Add("all");
				}
			}
			else
			{
				menu.Add("In Auto grabber - open");
				acts.Add("open");
			}
			menu.Add("Search all torrents");
			acts.Add("everything");
			menu.DoModal(GUIWindowManager.ActiveWindow);
			Menus.Check(menu);
			int sel = menu.SelectedLabel;
			string act = sel >= 0 && sel < acts.Count ? acts[sel] : "";
			string name = System.Text.RegularExpressions.Regex.Replace(series.OriginalName ?? series.Name ?? "", "[^0-9a-zA-Z ]+", " ").Trim();
			name = System.Text.RegularExpressions.Regex.Replace(name, @"\s+", " ");
			switch (act)
			{
			case "missing":
			{
				menu.Reset();
				menu.SetHeading(series.Name + " - missing");
				foreach (string m in missing.Take(60))
				{
					string[] f = m.Split('|');
					System.DateTime d;
					menu.Add(f[0] + (f.Length > 1 && System.DateTime.TryParse(f[1], out d) ? "  " + d.ToString("d MMM yy") : ""));
				}
				menu.DoModal(GUIWindowManager.ActiveWindow);
				Menus.Check(menu);
				int pick = menu.SelectedLabel;
				if (pick < 0 || pick >= Math.Min(60, missing.Count)) return true;
				string ep = missing[pick].Split('|')[0];
				MyTorrents.Instance().SearchFor(name + " " + ep, SearchContext.TV);
				return true;
			}
			case "new":
				AddToAutoGrabber(series, "New episodes");
				MyTorrents.Error("Auto grabber", series.Name + " added -\nnew episodes are fetched from now on.");
				return true;
			case "all":
				AddToAutoGrabber(series, "Missing episodes");
				MyTorrents.Error("Auto grabber", series.Name + " added - its " + missing.Count + "\nmissing episodes are fetched, a few at a time.");
				return true;
			case "open":
				MyTorrents.Instance().ShowTorrentWatch();
				return true;
			case "everything":
				MyTorrents.Instance().SearchFor(name, SearchContext.TV);
				return true;
			}
			return true; // "none" or Back: stay on My Torrents
		}

		public void SearchSeries()
		{
			VirtualKeyboard virtualKeyboard = (VirtualKeyboard)GUIWindowManager.GetWindow(1002);
			virtualKeyboard.Reset();
			virtualKeyboard.Text = "";
			virtualKeyboard.DoModal(GUIWindowManager.ActiveWindow);
			Menus.Check(virtualKeyboard);
			if (!virtualKeyboard.IsConfirmed)
			{
				return;
			}
			// Trakt API v2 (TraktPlugin 6.x): search returns wrapped results
			IList<TraktShowSummary> list;
			try
			{
				list = (global::TraktAPI.TraktAPI.SearchShows(virtualKeyboard.Text) ?? Enumerable.Empty<TraktSearchResult>())
					.Where(r => r.Show != null).Select(r => r.Show).ToList();
			}
			catch (System.Exception ex)
			{
				Log.Instance().Print("Trakt search failed: " + ex.Message);
				return;
			}
			if (list.Count == 0)
			{
				return;
			}
			GUIDialogMenu gUIDialogMenu = (GUIDialogMenu)GUIWindowManager.GetWindow(2012);
			if (gUIDialogMenu == null)
			{
				return;
			}
			gUIDialogMenu.Reset();
			gUIDialogMenu.SetHeading("Choose TV Series to add");
			foreach (TraktShowSummary item in list)
			{
				gUIDialogMenu.Add($"{item.Title} - ({item.Year})");
			}
			gUIDialogMenu.DoModal(GUIWindowManager.ActiveWindow);
			Menus.Check(gUIDialogMenu);
			if (gUIDialogMenu.SelectedId == -1)
			{
				return;
			}
			TraktShowSummary val = list[gUIDialogMenu.SelectedId - 1];
			AddNewSeries(val);
		}

		/// 2026 (1.3.40): add a show you have no episodes of yet - choose what to get, a folder is made for it in
		/// your TV folder with its TVDB id file (so MP-TVSeries matches the right show), and it goes in the Auto grabber.
		internal void AddNewSeries(TraktShowSummary val)
		{
			string tvdb = val.Ids?.Tvdb?.ToString() ?? "";
			WatchList wl = MyTorrents.WL;
			if (tvdb.Length > 0 && wl.GetSeriesList().Any(x => x.show != null && x.show.ID == tvdb))
			{
				MyTorrents.Error("Add a new series", val.Title + " is already in the Auto grabber.");
				return;
			}
			string traktId = val.Ids?.Slug ?? val.Ids?.Trakt?.ToString();
			List<TraktEpisodeSummary> episodes = new List<TraktEpisodeSummary>();
			try
			{
				foreach (TraktSeasonSummary season in global::TraktAPI.TraktAPI.GetShowSeasons(traktId, "episodes") ?? Enumerable.Empty<TraktSeasonSummary>())
				{
					if (season.Number == 0 || season.Episodes == null) continue; // skip specials
					episodes.AddRange(season.Episodes);
				}
			}
			catch (System.Exception ex)
			{
				Log.Instance().Print("Trakt season lookup failed: " + ex.Message);
			}
			Func<TraktEpisodeSummary, DateTime> aired = ep =>
			{
				DateTime d;
				return !string.IsNullOrEmpty(ep.FirstAired) && DateTime.TryParse(ep.FirstAired, System.Globalization.CultureInfo.InvariantCulture,
					System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out d) ? d : DateTime.MinValue;
			};

			// what to get
			GUIDialogMenu menu = (GUIDialogMenu)GUIWindowManager.GetWindow(2012);
			if (menu == null) return;
			menu.Reset();
			menu.SetHeading(val.Title + (val.Year.HasValue ? " (" + val.Year + ")" : "") + " - what should it get?");
			menu.Add("Only new episodes from now on");
			menu.Add("The whole series from S01E01");
			List<int> seasons = episodes.Select(x => x.Season).Distinct().OrderBy(x => x).ToList();
			if (seasons.Count > 0) menu.Add("Just one season...");
			menu.DoModal(GUIWindowManager.ActiveWindow);
			Menus.Check(menu);
			int choice = menu.SelectedId;
			if (choice < 1) return;
			List<TraktEpisodeSummary> wanted;
			string type;
			if (choice == 1)
			{
				wanted = episodes.Where(x => aired(x) == DateTime.MinValue || aired(x) > DateTime.UtcNow).ToList();
				type = "New episodes";
			}
			else if (choice == 2)
			{
				wanted = episodes;
				type = "Missing episodes";
			}
			else
			{
				menu.Reset();
				menu.SetHeading(val.Title + " - which season?");
				foreach (int sn in seasons) menu.Add("Season " + sn + "  (" + episodes.Count(x => x.Season == sn) + " episodes)");
				menu.DoModal(GUIWindowManager.ActiveWindow);
				Menus.Check(menu);
				if (menu.SelectedId < 1) return;
				int pickSeason = seasons[menu.SelectedId - 1];
				wanted = episodes.Where(x => x.Season == pickSeason).ToList();
				type = "Missing episodes";
			}
			Create(val, tvdb, wanted, type, true);
		}

		/// 2026 (1.3.42): every episode of a show from Trakt (specials left out); empty when Trakt can't be reached.
		internal static List<TraktEpisodeSummary> TraktEpisodes(TraktShowSummary val)
		{
			List<TraktEpisodeSummary> episodes = new List<TraktEpisodeSummary>();
			try
			{
				foreach (TraktSeasonSummary season in global::TraktAPI.TraktAPI.GetShowSeasons(val.Ids?.Slug ?? val.Ids?.Trakt?.ToString(), "episodes") ?? Enumerable.Empty<TraktSeasonSummary>())
				{
					if (season.Number == 0 || season.Episodes == null) continue;
					episodes.AddRange(season.Episodes);
				}
			}
			catch (System.Exception ex)
			{
				Log.Instance().Print("Trakt season lookup failed: " + ex.Message);
			}
			return episodes;
		}

		/// 2026 (1.3.42): add a show you have none of, with no questions (the whole series) - for the Trakt list.
		internal static bool AddWholeSeries(TraktShowSummary val)
		{
			string tvdb = val.Ids?.Tvdb?.ToString() ?? "";
			WatchList wl = MyTorrents.WL;
			if (tvdb.Length > 0 && wl.GetSeriesList().Any(x => x.show != null && x.show.ID == tvdb)) return true;
			List<TraktEpisodeSummary> episodes = TraktEpisodes(val);
			if (episodes.Count == 0) return false;
			Create(val, tvdb, episodes, "Missing episodes", false);
			return true;
		}

		private static void Create(TraktShowSummary val, string tvdb, List<TraktEpisodeSummary> wanted, string type, bool tell)
		{
			WatchList wl = MyTorrents.WL;
			Func<TraktEpisodeSummary, DateTime> aired = ep =>
			{
				DateTime d;
				return !string.IsNullOrEmpty(ep.FirstAired) && DateTime.TryParse(ep.FirstAired, System.Globalization.CultureInfo.InvariantCulture,
					System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out d) ? d : DateTime.MinValue;
			};
			SeriesItem seriesItem = new SeriesItem();
			seriesItem.quality = SeriesItem.Quality;
			seriesItem.show = new Series();
			seriesItem.show.Name = val.Title;
			seriesItem.show.OriginalName = val.Title;
			seriesItem.show.Description = val.Overview;
			seriesItem.show.Genres = "";
			if (val.Genres != null)
			{
				foreach (string genre in val.Genres)
				{
					seriesItem.show.Genres += "|" + genre;
				}
			}
			seriesItem.show.Rating = (val.Rating ?? 0.0).ToString("0.0");
			seriesItem.show.Poster = "";
			seriesItem.show.ID = tvdb;
			seriesItem.show.AllUnlocal = "";
			foreach (TraktEpisodeSummary episode in wanted.OrderBy(x => x.Season).ThenBy(x => x.Number))
			{
				string code = "S" + episode.Season.ToString("00") + "E" + episode.Number.ToString("00");
				seriesItem.show.AllUnlocal += code + "|";
				DateTime a = aired(episode);
				if (a != DateTime.MinValue)
				{
					seriesItem.show.AirsByCode[code] = a;
					if (a < DateTime.UtcNow) seriesItem.show.AiredMissing.Add(code + "|" + a.ToString("yyyy-MM-dd"));
				}
			}
			seriesItem.show.FirstUnlocal = seriesItem.show.AllUnlocal.Split('|').First();
			seriesItem.show.NextAirsUtc = seriesItem.show.AirsOf(seriesItem.show.FirstUnlocal);
			seriesItem.source = "";
			seriesItem.type = type;
			seriesItem.tracker = TorrentSearchEngine.TVSeriesEngineName;
			wl._watchItemList.Add(seriesItem);
			try
			{
				wl.Save();
			}
			catch
			{
			}

			// its folder in the TV folder, with the TVDB id file so MP-TVSeries matches the right show
			string folderNote = "";
			try
			{
				string root = PostProcessor.TVFolder;
				if (!string.IsNullOrEmpty(root) && System.IO.Directory.Exists(root))
				{
					string name = val.Title + (val.Year.HasValue ? " (" + val.Year + ")" : "");
					string folder = MediaNames.ShowFolder(root, name);
					System.IO.Directory.CreateDirectory(folder);
					if (tvdb.Length > 0)
					{
						string idFile = System.IO.Path.Combine(folder, tvdb + ".tvdb");
						if (!System.IO.File.Exists(idFile)) System.IO.File.WriteAllText(idFile, "");
					}
					folderNote = "Folder: " + folder;
				}
			}
			catch (System.Exception ex)
			{
				Log.Instance().Print("Add a new series: could not make the folder: " + ex.Message);
			}
			Log.Instance().Print($"Add a new series: {val.Title} (TVDB {tvdb}) - {type}, {wanted.Count} episode(s). {folderNote}");
			GUIDialogOK ok = tell ? (GUIDialogOK)GUIWindowManager.GetWindow(2002) : null;
			if (ok != null)
			{
				ok.SetHeading("Added " + val.Title);
				ok.SetLine(1, type == "New episodes" ? "It gets new episodes as they come out." : "Looking for " + wanted.Count + " episode(s) - the first check is within a few minutes.");
				ok.SetLine(2, folderNote);
				ok.DoModal(GUIWindowManager.ActiveWindow);
				Menus.Check(ok);
			}
			if (type == "Missing episodes") wl.CheckNow(tvdb.Length > 0 ? tvdb : null, "new series added");
		}

		public void SelectTracker(SeriesItem item)
		{
			GUIDialogMenu gUIDialogMenu = (GUIDialogMenu)GUIWindowManager.GetWindow(2012);
			if (gUIDialogMenu == null)
			{
				return;
			}
			gUIDialogMenu.Reset();
			gUIDialogMenu.SetHeading("Choose TV Series to add");
			TorrentSearchEngine torrentSearchEngine = null;
			try
			{
				torrentSearchEngine = new TorrentSearchEngine(Configuration.Instance().Settings["TorrentSearch.Config"] as string);
			}
			catch
			{
				Log.Instance().Print("MyTorrents.xml config file loading failed!");
			}
			foreach (ITorrentSearch engine in torrentSearchEngine.Engines)
			{
				gUIDialogMenu.Add(engine.Name);
			}
			gUIDialogMenu.DoModal(GUIWindowManager.ActiveWindow);
			Menus.Check(gUIDialogMenu);
			if (gUIDialogMenu.SelectedId != -1)
			{
				(from e in MyTorrents.WL.GetSeriesList()
					where e.show.ID == item.show.ID
					select e).First().tracker = gUIDialogMenu.SelectedLabelText;
				GUIPropertyManager.SetProperty("#MyTorrents.SeriesInfo.Tracker", gUIDialogMenu.SelectedLabelText);
			}
		}

		public void SelectSource(SeriesItem item)
		{
			GUIDialogMenu gUIDialogMenu = (GUIDialogMenu)GUIWindowManager.GetWindow(2012);
			if (gUIDialogMenu == null)
			{
				return;
			}
			gUIDialogMenu.Reset();
			gUIDialogMenu.SetHeading("Choose Source");
			gUIDialogMenu.Add("Any");
			gUIDialogMenu.Add("HDTV");
			gUIDialogMenu.Add("VHSRip");
			gUIDialogMenu.Add("TVRip");
			gUIDialogMenu.Add("DVDRip");
			gUIDialogMenu.Add("BDRip");
			gUIDialogMenu.Add("BluRay");
			gUIDialogMenu.Add("WEB");
			gUIDialogMenu.Add("DVD");
			gUIDialogMenu.DoModal(GUIWindowManager.ActiveWindow);
			Menus.Check(gUIDialogMenu);
			if (gUIDialogMenu.SelectedId != -1)
			{
				(from e in MyTorrents.WL.GetSeriesList()
					where e.show.ID == item.show.ID
					select e).First().source = gUIDialogMenu.SelectedLabelText == "Any" ? "" : gUIDialogMenu.SelectedLabelText;
				GUIPropertyManager.SetProperty("#MyTorrents.SeriesInfo.Source", gUIDialogMenu.SelectedLabelText);
			}
		}

		public void SelectQuality(SeriesItem item)
		{
			GUIDialogMenu gUIDialogMenu = (GUIDialogMenu)GUIWindowManager.GetWindow(2012);
			if (gUIDialogMenu == null)
			{
				return;
			}
			gUIDialogMenu.Reset();
			gUIDialogMenu.SetHeading("Choose Resolution");
			gUIDialogMenu.Add("SD");
			gUIDialogMenu.Add("720p");
			gUIDialogMenu.Add("1080i");
			gUIDialogMenu.Add("1080p");
			gUIDialogMenu.Add("480p");
			gUIDialogMenu.DoModal(GUIWindowManager.ActiveWindow);
			Menus.Check(gUIDialogMenu);
			if (gUIDialogMenu.SelectedId != -1)
			{
				(from e in MyTorrents.WL.GetSeriesList()
					where e.show.ID == item.show.ID
					select e).First().quality = gUIDialogMenu.SelectedLabelText;
				GUIPropertyManager.SetProperty("#MyTorrents.SeriesInfo.Quality", gUIDialogMenu.SelectedLabelText);
			}
		}

		public void ChooseType(SeriesItem item)
		{
			GUIDialogMenu gUIDialogMenu = (GUIDialogMenu)GUIWindowManager.GetWindow(2012);
			if (gUIDialogMenu == null)
			{
				return;
			}
			gUIDialogMenu.Reset();
			gUIDialogMenu.SetHeading("Choose Watching Type");
			gUIDialogMenu.Add("New episodes");
			gUIDialogMenu.Add("Missing episodes");
			gUIDialogMenu.DoModal(GUIWindowManager.ActiveWindow);
			Menus.Check(gUIDialogMenu);
			if (gUIDialogMenu.SelectedId != -1)
			{
				(from e in MyTorrents.WL.GetSeriesList()
					where e.show.ID == item.show.ID
					select e).First().type = gUIDialogMenu.SelectedLabelText;
				GUIPropertyManager.SetProperty("#MyTorrents.SeriesInfo.Type", gUIDialogMenu.SelectedLabelText);
			}
		}

		public void ChooseFolder(SeriesItem item)
		{
			MyTorrents.ListType = "SeriesFolderBrowser";
			GUIPropertyManager.SetProperty("#MyTorrents.SeriesInfo", "no");
			MyTorrents.Instance().ShowFolderBrowser();
		}

		public void OnToggleSpecials(SeriesItem item, bool includespecials)
		{
			(from e in MyTorrents.WL.GetSeriesList()
				where e.show.ID == item.show.ID
				select e).First().includespecials = includespecials;
			GUIPropertyManager.SetProperty("#MyTorrents.SeriesInfo.Specials", includespecials.ToString());
		}

		public void OnClicked(GUIControl control)
		{
			int getID = control.GetID;
			if (getID == 302)
			{
				SelectQuality(SelectedWatch);
			}
			if (getID == 303)
			{
				SelectSource(SelectedWatch);
			}
			if (getID == 304)
			{
				SelectTracker(SelectedWatch);
			}
			if (getID == 305)
			{
				ChooseType(SelectedWatch);
			}
			if (getID == 306)
			{
				OnToggleSpecials(SelectedWatch, control.Selected);
			}
			if (getID == 307)
			{
				ChooseFolder(SelectedWatch);
			}
			if (getID == 308)
			{
				RemoveShow(SelectedWatch);
				return;
			}
			if (getID != 307)
			{
				GUIControl.FocusControl(5678, control.GetID);
			}
		}

		/// 2026: "Remove this show" on the show's page
		private void RemoveShow(SeriesItem item)
		{
			if (item == null) return;
			GUIDialogYesNo dlg = (GUIDialogYesNo)GUIWindowManager.GetWindow(100);
			if (dlg == null) return;
			dlg.SetHeading("Remove this show?");
			dlg.SetLine(1, item.show?.Name ?? "");
			dlg.SetLine(2, "The Auto grabber stops looking for it.");
			dlg.SetLine(3, "Nothing you have is deleted.");
			dlg.SetDefaultToYes(false);
			dlg.DoModal(GUIWindowManager.ActiveWindow);
			Menus.Check(dlg);
			if (!dlg.IsConfirmed) return;
			MyTorrents.WL.RemoveWatch(item);
			MyTorrents.Instance().CloseSeriesInfo();
		}

		/// 2026: the Auto grabber defaults, changed with the remote (menu button on the Auto grabber list)
		public void DefaultsMenu()
		{
			while (true)
			{
				GUIDialogMenu menu = (GUIDialogMenu)GUIWindowManager.GetWindow(2012);
				if (menu == null) return;
				menu.Reset();
				menu.SetHeading("Auto grabber settings (every show)");
				menu.Add("Quality:  " + SeriesItem.Quality);
				menu.Add("Biggest episode:  " + (SeriesItem.MaxSizeMB > 0 ? SeriesItem.MaxSizeMB + " MB" : "no limit"));
				menu.Add("Video type:  " + SeriesItem.CodecText(SeriesItem.Codec));
				menu.Add("Minimum seeders:  " + SeriesItem.MinSeeders);
				menu.Add("New shows look for:  " + SeriesItem.NewShowType);
				menu.Add("When no copy fits:  " + TooBig.HoursText(TooBig.AskAfterHours));
				menu.Add("Favourites:  " + (SeriesItem.Favourites.Length > 0 ? string.Join(", ", SeriesItem.Favourites) : "none"));
				menu.Add("Use favourites:  " + SeriesItem.FavouritesModeText(SeriesItem.FavouritesMode));
				menu.Add("How MyTorrents works:  " + (AutoAdd.Enabled ? "Automatic" : "Manual"));
				menu.Add("Smallest episode:  " + (SeriesItem.MinSizeMB > 0 ? SeriesItem.MinSizeMB + " MB (smaller = fake)" : "no minimum"));
				menu.Add("Skip placeholder episodes:  " + (SeriesItem.SkipPlaceholders ? "Yes" : "No"));
				menu.Add("Size report - what size limit fits...");
				menu.Add("Movie quality:  " + Films.Quality); // 2026 (1.3.40)
				menu.Add("Biggest movie:  " + Films.SizeText(Films.MaxSizeMB));
				menu.Add("Whole seasons first:  " + (SeriesItem.SeasonPacks ? "Yes" : "No")); // 2026 (1.3.41)
				menu.Add("Whole season when:  " + SeriesItem.SeasonPackPercentText(SeriesItem.SeasonPackPercent));
				menu.Add("Biggest season:  " + (SeriesItem.MaxSeasonGB > 0 ? SeriesItem.MaxSeasonGB + " GB" : "biggest episode x episodes"));
				menu.Add("Favourite season groups:  " + (SeriesItem.SeasonFavourites.Length > 0 ? string.Join(", ", SeriesItem.SeasonFavourites) : "none"));
				menu.Add("1080p if no good 720p:  " + Relax.FallbackText(Relax.FallbackPercent)); // 2026 (1.3.42)
				menu.Add("Fewer seeders over time:  " + Relax.RelaxText());
				menu.Add("Stuck downloads:  " + Grabbed.StuckText(Grabbed.StuckHours));
				menu.Add("My Trakt list:  " + (TraktList.On ? TraktList.ListName : "Off")); // 2026 (1.3.42)
				menu.Add("Can't find list after:  " + CantFind.Days + " days");
				menu.Add("Shows searching at once:  " + SeriesItem.SearchesAtOnce);
				menu.Add("Look for wanted movies:  every " + WantedFilms.LookEveryHours + " hours"); // 2026 (1.3.43)
				menu.Add("New movies:  " + (MovieFiling.FileForMe ? "filed for me" : MovieFiling.Mode == "ask" ? "filed for me, shown first" : "I file them myself")); // 2026 (1.3.44)
				menu.Add("Movies folder:  " + (MovieFiling.MoviesFolder.Length > 0 ? MovieFiling.MoviesFolder : "not set"));
				menu.Add("Collections folder:  " + (MovieFiling.CollectionsFolder.Length > 0 ? MovieFiling.CollectionsFolder : "not set"));
				menu.Add("Movie .nfo files:  " + (MovieFiling.WriteNfo ? "Yes" : "No"));
				menu.Add("Removed wanted movies come off my Trakt list:  " + (TraktList.RemoveDropped ? "Yes" : "No")); // 2026 (1.3.45)
				menu.Add("Sound for TV:  " + Sound.Text(Sound.Tv));
				menu.Add("Sound for movies:  " + Sound.Text(Sound.Movies));
				menu.Add("Movie favourites:  " + (Films.Favourites.Length > 0 ? string.Join(", ", Films.Favourites) : "none"));
				menu.Add("5.1 upgrade - delete the stereo copy:  " + (SoundUpgrades.DeleteOld ? "Yes" : "No, keep both")); // 2026 (1.3.46)
				menu.Add("Look for 5.1 copies by itself:  " + (SoundUpgrades.Auto ? SoundUpgrades.AutoPerDay + " a day" : "No"));
				menu.Add("MP-TVSeries scan:  " + NightScan.Text(NightScan.Mode)); // 2026 (1.3.47)
				menu.Add("Smallest movie:  " + Films.MinSizeText(Films.MinSizeSetting));
				menu.Add("Movie names:  " + Naming.MovieExample(Naming.MoviePattern, Naming.MovieFolderPattern).Split('\\').Last()); // 2026 (1.3.49)
				menu.Add("Movie folders:  " + (Naming.MovieFolderStyles.FirstOrDefault(x => x[1] == Naming.MovieFolderPattern)?[0] ?? Naming.MovieFolderPattern));
				menu.Add("New show folders:  " + Naming.ShowFolderExample(Naming.ShowFolderPattern));
				menu.Add("Drop folder:  " + (DropFolder.Folder.Length > 0 ? DropFolder.Folder : "off")); // 2026 (1.3.50)
				menu.Add("Downloads at once:  " + Busy.Text(Busy.Max));
				menu.Add("Nearly finished (99%+, no seeds):  " + PostProcessor.NearlyDoneText(PostProcessor.NearlyDoneHours)); // 2026 (1.3.53)
				menu.DoModal(GUIWindowManager.ActiveWindow);
				Menus.Check(menu);
				int pick = menu.SelectedId;
				if (pick < 1) return;
				if (pick == 7)
				{
					// 2026: favourite release groups / sites - typed with the on-screen keyboard
					VirtualKeyboard keyboard = (VirtualKeyboard)GUIWindowManager.GetWindow(1002);
					if (keyboard == null) continue;
					keyboard.Reset();
					keyboard.Text = string.Join(", ", SeriesItem.Favourites);
					keyboard.DoModal(GUIWindowManager.ActiveWindow);
					Menus.Check(keyboard);
					if (keyboard.IsConfirmed)
					{
						string favs = string.Join(", ", (keyboard.Text ?? "").Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries).Select(f => f.Trim()).Where(f => f.Length > 0));
						Configuration.SetExtra("AutoGrabber.Favourites", favs);
						Log.Instance().Print("Auto grabber setting AutoGrabber.Favourites = " + favs);
					}
					continue;
				}
				if (pick == 41)
				{
					// 2026 (1.3.50): the drop folder - typed with the on-screen keyboard (empty = off)
					VirtualKeyboard keyboard = (VirtualKeyboard)GUIWindowManager.GetWindow(1002);
					if (keyboard == null) continue;
					keyboard.Reset();
					keyboard.Text = DropFolder.Folder;
					keyboard.DoModal(GUIWindowManager.ActiveWindow);
					Menus.Check(keyboard);
					if (keyboard.IsConfirmed)
					{
						Configuration.SetExtra("Finished.DropFolder", (keyboard.Text ?? "").Trim());
						Log.Instance().Print("Auto grabber setting Finished.DropFolder = " + (keyboard.Text ?? "").Trim());
					}
					continue;
				}
				if (pick == 33)
				{
					VirtualKeyboard keyboard = (VirtualKeyboard)GUIWindowManager.GetWindow(1002);
					if (keyboard == null) continue;
					keyboard.Reset();
					keyboard.Text = string.Join(", ", Films.Favourites);
					keyboard.DoModal(GUIWindowManager.ActiveWindow);
					Menus.Check(keyboard);
					if (keyboard.IsConfirmed)
						Configuration.SetExtra("Films.Favourites", string.Join(", ", (keyboard.Text ?? "").Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries).Select(f => f.Trim()).Where(f => f.Length > 0)));
					continue;
				}
				if (pick == 27 || pick == 28)
				{
					// 2026 (1.3.44): pick the folder from Moving Pictures' folders
					List<string> folders = LibraryFolders.Movies().Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList();
					menu.Reset();
					menu.SetHeading(pick == 27 ? "Movies folder" : "Collections folder");
					foreach (string f in folders) menu.Add(f);
					menu.Add("Not set");
					menu.DoModal(GUIWindowManager.ActiveWindow);
					Menus.Check(menu);
					if (menu.SelectedId >= 1)
					{
						string v = menu.SelectedId <= folders.Count ? folders[menu.SelectedId - 1] : "";
						Configuration.SetExtra(pick == 27 ? "Films.MoviesFolder" : "Films.CollectionsFolder", v);
						Log.Instance().Print("Movies setting " + (pick == 27 ? "Films.MoviesFolder" : "Films.CollectionsFolder") + " = " + v);
					}
					continue;
				}
				if (pick == 22)
				{
					// 2026 (1.3.42): your own Trakt list (e.g. MCM)
					menu.Reset();
					menu.SetHeading("My Trakt list - movies go into Wanted movies, shows into the Auto grabber");
					menu.Add("Use the list called:  " + (TraktList.ListName.Length > 0 ? TraktList.ListName : "(none)"));
					menu.Add(TraktList.On ? "Turn it off" : "Turn it on");
					menu.Add("Look at the list now");
					menu.DoModal(GUIWindowManager.ActiveWindow);
					Menus.Check(menu);
					if (menu.SelectedId == 1)
					{
						VirtualKeyboard keyboard = (VirtualKeyboard)GUIWindowManager.GetWindow(1002);
						if (keyboard != null)
						{
							keyboard.Reset();
							keyboard.Text = TraktList.ListName;
							keyboard.DoModal(GUIWindowManager.ActiveWindow);
							Menus.Check(keyboard);
							if (keyboard.IsConfirmed) Configuration.SetExtra("Trakt.ListName", (keyboard.Text ?? "").Trim());
						}
					}
					else if (menu.SelectedId == 2) Configuration.SetExtra("Trakt.ListOn", TraktList.On ? "false" : "true");
					else if (menu.SelectedId == 3) System.Threading.ThreadPool.QueueUserWorkItem(_ => TraktList.Check(true));
					continue;
				}
				if (pick == 20)
				{
					// 2026 (1.3.42): fewer seeders over time - on/off and when
					menu.Reset();
					menu.SetHeading("Nothing good for a while - accept fewer seeders?");
					menu.Add("Off - always " + SeriesItem.MinSeeders + "+ seeders");
					foreach (int h in Relax.AfterChoices) menu.Add("After " + h + " hours, then 1 less every " + Relax.StepHours + " hours");
					menu.Add("Never fewer than:  " + Relax.LowestSeeders);
					menu.DoModal(GUIWindowManager.ActiveWindow);
					Menus.Check(menu);
					int c = menu.SelectedId;
					if (c == 1) Configuration.SetExtra("AutoGrabber.RelaxSeeds", "false");
					else if (c >= 2 && c <= Relax.AfterChoices.Length + 1)
					{
						Configuration.SetExtra("AutoGrabber.RelaxSeeds", "true");
						Configuration.SetExtra("AutoGrabber.RelaxAfterHours", Relax.AfterChoices[c - 2].ToString());
					}
					else if (c == Relax.AfterChoices.Length + 2)
					{
						menu.Reset();
						menu.SetHeading("Never fewer than");
						foreach (int n in Relax.LowestChoices) menu.Add(n + (n == 1 ? " seeder" : " seeders"));
						menu.DoModal(GUIWindowManager.ActiveWindow);
						Menus.Check(menu);
						if (menu.SelectedId >= 1) Configuration.SetExtra("AutoGrabber.LowestSeeders", Relax.LowestChoices[menu.SelectedId - 1].ToString());
					}
					if (c >= 1) Log.Instance().Print("Auto grabber setting: fewer seeders over time = " + Relax.RelaxText());
					continue;
				}
				if (pick == 18)
				{
					// 2026 (1.3.41): favourite release names for whole seasons (e.g. GalaxyTV)
					VirtualKeyboard keyboard = (VirtualKeyboard)GUIWindowManager.GetWindow(1002);
					if (keyboard == null) continue;
					keyboard.Reset();
					keyboard.Text = string.Join(", ", SeriesItem.SeasonFavourites);
					keyboard.DoModal(GUIWindowManager.ActiveWindow);
					Menus.Check(keyboard);
					if (keyboard.IsConfirmed)
					{
						string favs = string.Join(", ", (keyboard.Text ?? "").Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries).Select(f => f.Trim()).Where(f => f.Length > 0));
						Configuration.SetExtra("AutoGrabber.SeasonFavourites", favs);
						Log.Instance().Print("Auto grabber setting AutoGrabber.SeasonFavourites = " + favs);
					}
					continue;
				}
				if (pick == 12)
				{
					// 2026: size report - sizes downloaded and how many episodes fit at each limit
					menu.Reset();
					menu.SetHeading("Size report (limit now " + (SeriesItem.MaxSizeMB > 0 ? SeriesItem.MaxSizeMB + " MB" : "none") + ")");
					foreach (string line in SizeRecord.Report(SeriesItem.MaxSizeMB)) menu.Add(line.Trim());
					menu.DoModal(GUIWindowManager.ActiveWindow);
					Menus.Check(menu);
					continue;
				}
				if (pick == 2)
				{
					// 2026: any size - the usual ones, or "Type a size..." with the on-screen keyboard
					menu.Reset();
					menu.SetHeading("Biggest episode (now " + (SeriesItem.MaxSizeMB > 0 ? SeriesItem.MaxSizeMB + " MB" : "no limit") + ")");
					foreach (long x in SeriesItem.SizeChoices) menu.Add(x > 0 ? x + " MB" : "No limit");
					menu.Add("Type a size...");
					menu.DoModal(GUIWindowManager.ActiveWindow);
					Menus.Check(menu);
					int s = menu.SelectedId;
					long mb = -1;
					if (s >= 1 && s <= SeriesItem.SizeChoices.Length)
					{
						mb = SeriesItem.SizeChoices[s - 1];
					}
					else if (s == SeriesItem.SizeChoices.Length + 1)
					{
						VirtualKeyboard keyboard = (VirtualKeyboard)GUIWindowManager.GetWindow(1002);
						if (keyboard != null)
						{
							keyboard.Reset();
							keyboard.Text = SeriesItem.MaxSizeMB.ToString();
							keyboard.DoModal(GUIWindowManager.ActiveWindow);
							Menus.Check(keyboard);
							long typed;
							if (keyboard.IsConfirmed && long.TryParse(new string((keyboard.Text ?? "").Where(char.IsDigit).ToArray()), out typed)) mb = Math.Min(100000, typed);
						}
					}
					if (mb >= 0)
					{
						Configuration.SetExtra("AutoGrabber.MaxSizeMB", mb.ToString());
						Log.Instance().Print("Auto grabber setting AutoGrabber.MaxSizeMB = " + mb);
					}
					continue;
				}
				string[] labels;
				string[] values;
				string key;
				string heading;
				switch (pick)
				{
				case 1:
					key = "AutoGrabber.Quality"; heading = "Quality";
					values = SeriesItem.QualityChoices; labels = values;
					break;
				case 2:
					key = "AutoGrabber.MaxSizeMB"; heading = "Biggest episode";
					values = SeriesItem.SizeChoices.Select(x => x.ToString()).ToArray();
					labels = SeriesItem.SizeChoices.Select(x => x > 0 ? x + " MB" : "No limit").ToArray();
					break;
				case 3:
					key = "AutoGrabber.Codec"; heading = "Video type";
					values = SeriesItem.CodecChoices; labels = values.Select(SeriesItem.CodecText).ToArray();
					break;
				case 4:
					key = "AutoGrabber.MinSeeders"; heading = "Minimum seeders";
					values = SeriesItem.SeederChoices.Select(x => x.ToString()).ToArray();
					labels = SeriesItem.SeederChoices.Select(x => x + (x == 1 ? " seeder" : " seeders")).ToArray();
					break;
				case 5:
					key = "AutoGrabber.Type"; heading = "New shows look for";
					values = new[] { "New episodes", "Missing episodes" }; labels = values;
					break;
				case 10:
					key = "AutoGrabber.MinSizeMB"; heading = "Smallest episode (smaller copies are fakes / samples)";
					values = SeriesItem.MinSizeChoices.Select(x => x.ToString()).ToArray();
					labels = SeriesItem.MinSizeChoices.Select(x => x > 0 ? x + " MB" : "No minimum").ToArray();
					break;
				case 11:
					key = "AutoGrabber.SkipPlaceholders"; heading = "Skip placeholder episodes (numbered 99, or no air date)";
					values = new[] { "true", "false" }; labels = new[] { "Yes", "No" };
					break;
				case 9:
					key = "AutoGrabber.AutoAddRunning"; heading = "How should MyTorrents work?";
					values = new[] { "true", "false" }; labels = new[] { AutoAdd.ModeText(true) + " (new episodes, never ended shows)", AutoAdd.ModeText(false) };
					break;
				case 13:
					key = "Films.Quality"; heading = "Movie quality";
					values = SeriesItem.QualityChoices; labels = values;
					break;
				case 14:
					key = "Films.MaxSizeMB"; heading = "Biggest movie";
					values = Films.SizeChoices.Select(x => x.ToString()).ToArray();
					labels = Films.SizeChoices.Select(Films.SizeText).ToArray();
					break;
				case 26:
					key = "Films.FileInto"; heading = "New movies";
					values = new[] { "new", "library", "ask" }; labels = new[] { "Leave them in the new-movies folder - I file them", "File them for me (Movies / Collections)", "File them for me, but show me first" };
					break;
				case 31:
				case 32:
					key = pick == 31 ? "AutoGrabber.Sound" : "Films.Sound"; heading = pick == 31 ? "Sound for TV" : "Sound for movies";
					values = Sound.Choices; labels = Sound.Choices.Select(Sound.Text).ToArray();
					break;
				case 43:
					key = "Finished.NearlyDoneHours"; heading = "Stuck at 99%+ with no seeds - check the video and copy it anyway";
					values = PostProcessor.NearlyDoneChoices.Select(x => x.ToString()).ToArray(); labels = PostProcessor.NearlyDoneChoices.Select(PostProcessor.NearlyDoneText).ToArray();
					break;
				case 42:
					key = "AutoGrabber.MaxDownloading"; heading = "Most downloads at once (ones you ask for never wait)";
					values = Busy.Choices.Select(x => x.ToString()).ToArray(); labels = Busy.Choices.Select(Busy.Text).ToArray();
					break;
				case 40:
					key = "Finished.ShowFolderPattern"; heading = "New show folders (shows you already have keep theirs)";
					values = Naming.ShowFolderStyles.Select(x => x[1]).ToArray(); labels = Naming.ShowFolderStyles.Select(x => x[0]).ToArray();
					break;
				case 38:
					key = "Films.NamePattern"; heading = "Movie names";
					values = Naming.MovieStyles.Select(x => x[1]).ToArray(); labels = Naming.MovieStyles.Select(x => x[0]).ToArray();
					break;
				case 39:
					key = "Films.FolderPattern"; heading = "Movie folders";
					values = Naming.MovieFolderStyles.Select(x => x[1]).ToArray(); labels = Naming.MovieFolderStyles.Select(x => x[0]).ToArray();
					break;
				case 37:
					key = "Films.MinSizeMB"; heading = "Smallest movie (smaller = a poor copy or a fake)";
					values = Films.MinSizeChoices.Select(x => x.ToString()).ToArray(); labels = Films.MinSizeChoices.Select(Films.MinSizeText).ToArray();
					break;
				case 36:
					key = "TV.NightScan"; heading = "MP-TVSeries scan (2-5 am, never while something plays)";
					values = NightScan.Choices; labels = NightScan.Choices.Select(NightScan.Text).ToArray();
					break;
				case 34:
					key = "Films.UpgradeDeleteOld"; heading = "Once the 5.1 copy is in place, delete the stereo one";
					values = new[] { "true", "false" }; labels = new[] { "Yes", "No - keep both" };
					break;
				case 35:
					key = "Films.UpgradeAuto"; heading = "Look for 5.1 copies of stereo-only movies by itself (" + SoundUpgrades.AutoPerDay + " a day)";
					values = new[] { "true", "false" }; labels = new[] { "Yes", "No - only when I ask" };
					break;
				case 30:
					key = "Trakt.ListRemoveDropped"; heading = "When I remove a wanted movie, take it off my Trakt list too";
					values = new[] { "true", "false" }; labels = new[] { "Yes", "No" };
					break;
				case 29:
					key = "Films.WriteNfo"; heading = "Write a .nfo with the IMDb id next to new movies";
					values = new[] { "true", "false" }; labels = new[] { "Yes", "No" };
					break;
				case 25:
					key = "Films.LookEveryHours"; heading = "Look for wanted movies";
					values = MoviesPage.LookChoices.Select(x => x.ToString()).ToArray(); labels = MoviesPage.LookChoices.Select(x => "every " + x + (x == 1 ? " hour" : " hours")).ToArray();
					break;
				case 24:
					key = "AutoGrabber.SearchesAtOnce"; heading = "Shows searching at the same time (ones you ask for now never wait)";
					values = SeriesItem.SearchesAtOnceChoices.Select(x => x.ToString()).ToArray(); labels = SeriesItem.SearchesAtOnceChoices.Select(x => x + (x == 1 ? " show" : " shows")).ToArray();
					break;
				case 23:
					key = "AutoGrabber.CantFindDays"; heading = "Put on the Can't find list after looking for";
					values = CantFind.DayChoices.Select(x => x.ToString()).ToArray(); labels = CantFind.DayChoices.Select(x => x + " days").ToArray();
					break;
				case 21:
					key = "AutoGrabber.StuckHours"; heading = "Stuck with no seeds - delete it and try another copy";
					values = Grabbed.StuckChoices.Select(x => x.ToString()).ToArray(); labels = Grabbed.StuckChoices.Select(Grabbed.StuckText).ToArray();
					break;
				case 19:
					key = "AutoGrabber.Fallback1080Percent"; heading = "No good 720p copy - allow 1080p?";
					values = Relax.FallbackChoices.Select(x => x.ToString()).ToArray(); labels = Relax.FallbackChoices.Select(Relax.FallbackText).ToArray();
					break;
				case 15:
					key = "AutoGrabber.SeasonPacks"; heading = "Look for whole seasons first (2 or more episodes missing)";
					values = new[] { "true", "false" }; labels = new[] { "Yes", "No" };
					break;
				case 16:
					key = "AutoGrabber.SeasonPackPercent"; heading = "Look for the whole season when";
					values = SeriesItem.SeasonPackPercentChoices.Select(x => x.ToString()).ToArray(); labels = SeriesItem.SeasonPackPercentChoices.Select(SeriesItem.SeasonPackPercentText).ToArray();
					break;
				case 17:
					key = "AutoGrabber.MaxSeasonGB"; heading = "Biggest season";
					values = new[] { "0", "5", "10", "15", "20", "30", "50" }; labels = new[] { "Biggest episode x episodes", "5 GB", "10 GB", "15 GB", "20 GB", "30 GB", "50 GB" };
					break;
				case 8:
					key = "AutoGrabber.FavouritesMode"; heading = "Use favourites";
					values = SeriesItem.FavouritesModeChoices; labels = values.Select(SeriesItem.FavouritesModeText).ToArray();
					break;
				default:
					key = "AutoGrabber.AskAfterHours"; heading = "When no copy fits the settings";
					values = TooBig.HourChoices.Select(x => x.ToString()).ToArray();
					labels = TooBig.HourChoices.Select(TooBig.HoursText).ToArray();
					break;
				}
				menu.Reset();
				menu.SetHeading(heading);
				foreach (string l in labels) menu.Add(l);
				menu.DoModal(GUIWindowManager.ActiveWindow);
				Menus.Check(menu);
				if (menu.SelectedId >= 1 && menu.SelectedId <= values.Length)
				{
					Configuration.SetExtra(key, values[menu.SelectedId - 1]);
					Log.Instance().Print("Auto grabber setting " + key + " = " + values[menu.SelectedId - 1]);
				}
			}
		}

		public void ShowSeries(object info)
		{
			SelectedWatch = info as SeriesItem;
			if (SelectedWatch?.show == null) return;
			Series series = MyTorrents.WL.allseries.Where((Series e) => e != null && e.ID == SelectedWatch.show.ID).FirstOrDefault();
			if (series != null)
			{
				SelectedWatch.show = series;
			}
			GUIPropertyManager.SetProperty("#MyTorrents.SeriesInfo", "yes");
			GUIPropertyManager.SetProperty("#MyTorrents.SeriesInfo.SeriesName", SelectedWatch.show.Name);
			GUIPropertyManager.SetProperty("#MyTorrents.SeriesInfo.SeriesPoster", SelectedWatch.show.Poster);
			GUIPropertyManager.SetProperty("#MyTorrents.SeriesInfo.SeriesInfo", SelectedWatch.show.Description);
			GUIPropertyManager.SetProperty("#MyTorrents.SeriesInfo.SeriesGenres", SelectedWatch.show.Genres);
			GUIPropertyManager.SetProperty("#MyTorrents.SeriesInfo.SeriesRating", SelectedWatch.show.Rating);
			GUIPropertyManager.SetProperty("#MyTorrents.SeriesInfo.Quality", SeriesItem.Quality);
			GUIPropertyManager.SetProperty("#MyTorrents.SeriesInfo.Defaults", SeriesItem.DefaultsText() + " · from all my sites");
			GUIPropertyManager.SetProperty("#MyTorrents.SeriesInfo.Tracker", SelectedWatch.tracker == TorrentSearchEngine.TVSeriesEngineName ? "All my sites" : SelectedWatch.tracker);
			GUIPropertyManager.SetProperty("#MyTorrents.SeriesInfo.Type", SelectedWatch.type);
			GUIPropertyManager.SetProperty("#MyTorrents.SeriesInfo.Next", SelectedWatch.type == "New episodes"
				? (SelectedWatch.LastLook.Length > 0
					? "Next: " + SelectedWatch.show.FirstUnlocal + "   ·   last look " + SelectedWatch.LastLook
					: "Next: " + SeriesItem.NextText(SelectedWatch.show) + "   ·   change the settings with the menu button on the list")
				: "Same settings for every show - change them with the menu button on the Auto grabber list");
			GUIPropertyManager.SetProperty("#MyTorrents.SeriesInfo.Source", string.IsNullOrEmpty(SelectedWatch.source) ? "Any" : SelectedWatch.source);
			GUIPropertyManager.SetProperty("#MyTorrents.SeriesInfo.Folder", string.IsNullOrEmpty(SelectedWatch.folder) ? "(normal)" : SelectedWatch.folder);
			GUIPropertyManager.SetProperty("#MyTorrents.SeriesInfo.Specials", SelectedWatch.includespecials.ToString());
		}
	}
}
