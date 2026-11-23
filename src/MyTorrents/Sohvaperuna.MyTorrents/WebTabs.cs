using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

namespace Sohvaperuna.MyTorrents
{
	/// <summary>
	/// 2026 (1.3.69): the web page's settings, tab by tab, the same as MyTorrents Configuration (Mark, 13 Nov: "I prefer
	/// to use the config or webui and I would like more in the webui"). Each tab is a list of fields built fresh on
	/// every page (so it always shows what is saved now); saving goes through the same list, so only the settings that
	/// tab shows can be changed from it. The setting keys and choices are the ones the Configuration tabs use.
	/// </summary>
	public static partial class WebServer
	{
		private sealed class Field
		{
			public string Key = "", Label = "", Kind = "txt", Current = "", Hint = "", Group = "";
			public string[] Values = new string[0], Labels = new string[0];
			public string Datalist = "";
		}

		private static Field Sel(string group, string key, string label, string current, string[] values, string[] labels, string hint = "") =>
			new Field { Group = group, Key = key, Label = label, Kind = "sel", Current = current, Values = values, Labels = labels, Hint = hint };

		private static Field Num(string group, string key, string label, long current, string hint = "") =>
			new Field { Group = group, Key = key, Label = label, Kind = "num", Current = current.ToString(), Hint = hint };

		private static Field Txt(string group, string key, string label, string current, string hint = "", string datalist = "") =>
			new Field { Group = group, Key = key, Label = label, Kind = "txt", Current = current ?? "", Hint = hint, Datalist = datalist };

		private static Field Chk(string group, string key, string label, bool current, string hint = "") =>
			new Field { Group = group, Key = key, Label = label, Kind = "chk", Current = current ? "true" : "false", Hint = hint };

		/// A choice of styles plus "or type your own" - for the name patterns.
		private static Field Pattern(string group, string key, string label, string current, string[][] styles, string hint = "") =>
			new Field { Group = group, Key = key, Label = label, Kind = "pattern", Current = current ?? "", Values = styles.Select(s => s[1]).ToArray(), Labels = styles.Select(s => s[0]).ToArray(), Hint = hint };

		private static string[] Ints(IEnumerable<int> v) => v.Select(x => x.ToString()).ToArray();
		private static string[] Longs(IEnumerable<long> v) => v.Select(x => x.ToString()).ToArray();

		/// The settings of one tab ("movies", "tv", "finished", "client").
		private static List<Field> TabFields(string tab)
		{
			string[] q = SeriesItem.QualityChoices;
			List<Field> l = new List<Field>();
			switch (tab)
			{
			case "movies":
			{
				const string g1 = "Movies", g2 = "Movies to improve";
				l.Add(Sel(g1, "Films.Quality", "Movie quality", Films.Quality, q, q));
				l.Add(Num(g1, "Films.MaxSizeMB", "Biggest movie (MB)", Films.MaxSizeMB, "0 = no limit"));
				l.Add(Sel(g1, "Films.MinSizeMB", "Smallest", Films.MinSizeSetting.ToString(), Longs(Films.MinSizeChoices), Films.MinSizeChoices.Select(Films.MinSizeText).ToArray()));
				l.Add(Sel(g1, "Films.LookEveryHours", "Look for wanted movies", WantedFilms.LookEveryHours.ToString(), Ints(MoviesPage.LookChoices), MoviesPage.LookChoices.Select(h => "every " + h + (h == 1 ? " hour" : " hours")).ToArray()));
				l.Add(Chk(g1, "Trakt.ListOn", "Get what's on my Trakt list", Configuration.GetExtra("Trakt.ListOn", "true") != "false"));
				l.Add(Txt(g1, "Trakt.ListName", "Trakt list name", TraktList.ListName, "taken off the list once it's in your movies"));
				l.Add(Chk(g1, "Trakt.ListRemoveDropped", "When I remove a movie from Wanted movies, take it off my Trakt list too", TraktList.RemoveDropped));
				l.Add(Sel(g1, "Films.Sound", "Sound", Sound.Movies, Sound.Choices, Sound.Choices.Select(Sound.Text).ToArray()));
				l.Add(Txt(g1, "Films.Favourites", "Favourites", string.Join(", ", Films.Favourites), "release groups / sites, with commas"));
				l.Add(Chk(g2, "Films.UpgradeDeleteOld", "Delete the old copy once the new one is in", SoundUpgrades.DeleteOld));
				l.Add(Chk(g2, "Films.UpgradeAuto", "Movie upgrades on - look for 5.1 / better copies by itself (" + SoundUpgrades.AutoPerDay + " stereo movies a day), even in Manual", SoundUpgrades.Auto));
				break;
			}
			case "tv":
			{
				const string g1 = "Used for every show", g2 = "Favourite release groups and sites", g3 = "When nothing good turns up", g4 = "Shows and episodes", g5 = "Whole seasons";
				l.Add(Sel(g1, "AutoGrabber.Quality", "Quality", SeriesItem.Quality, q, q));
				l.Add(Num(g1, "AutoGrabber.MaxSizeMB", "Biggest episode (MB)", SeriesItem.MaxSizeMB, "0 = no limit.  720p x265: 300-500 MB,  x264: 700-1500 MB"));
				l.Add(Sel(g1, "AutoGrabber.Codec", "Video type", SeriesItem.Codec, SeriesItem.CodecChoices, SeriesItem.CodecChoices.Select(SeriesItem.CodecText).ToArray(), "Prefer = that type when there is one, else the best other copy"));
				l.Add(Sel(g1, "AutoGrabber.Sound", "Sound", Sound.Tv, Sound.Choices, Sound.Choices.Select(Sound.Text).ToArray()));
				l.Add(Num(g1, "AutoGrabber.MinSeeders", "Minimum seeders", SeriesItem.MinSeeders, "fewer are skipped"));
				l.Add(Txt(g2, "AutoGrabber.Favourites", "Favourites", string.Join(", ", SeriesItem.Favourites), "part of the copy's or site's name, with commas - e.g. MeGusta, PSA"));
				l.Add(Sel(g2, "AutoGrabber.FavouritesMode", "Use them", SeriesItem.FavouritesMode, SeriesItem.FavouritesModeChoices, SeriesItem.FavouritesModeChoices.Select(SeriesItem.FavouritesModeText).ToArray()));
				l.Add(Sel(g3, "AutoGrabber.Fallback1080Percent", "1080p if no good 720p", Relax.FallbackPercent.ToString(), Ints(Relax.FallbackChoices), Relax.FallbackChoices.Select(Relax.FallbackText).ToArray(), "only with 720p chosen"));
				l.Add(Chk(g3, "AutoGrabber.RelaxSeeds", "Fewer seeders when nothing turns up for a while", Relax.SeedsOn));
				l.Add(Sel(g3, "AutoGrabber.RelaxAfterHours", "... starting after", Relax.AfterHours.ToString(), Ints(Relax.AfterChoices), Relax.AfterChoices.Select(h => h + " hrs").ToArray()));
				l.Add(Sel(g3, "AutoGrabber.RelaxStepHours", "... then 1 less every", Relax.StepHours.ToString(), Ints(Relax.StepChoices), Relax.StepChoices.Select(h => h + " hrs").ToArray()));
				l.Add(Sel(g3, "AutoGrabber.LowestSeeders", "... never under", Relax.LowestSeeders.ToString(), Ints(Relax.LowestChoices), Ints(Relax.LowestChoices)));
				l.Add(Sel(g3, "AutoGrabber.CantFindDays", "Can't find list after", CantFind.Days.ToString(), Ints(CantFind.DayChoices), CantFind.DayChoices.Select(d => d + " days of looking").ToArray(), "then looked for once a day"));
				l.Add(Num(g4, "AutoGrabber.MinSizeMB", "Smallest episode (MB)", SeriesItem.MinSizeMB, "smaller = a fake or a sample;  0 = no minimum"));
				l.Add(Sel(g4, "AutoGrabber.SearchesAtOnce", "Shows searching at once", SeriesItem.SearchesAtOnce.ToString(), Ints(SeriesItem.SearchesAtOnceChoices), Ints(SeriesItem.SearchesAtOnceChoices), "fewer = lighter on the PC; ones you ask for now never wait"));
				l.Add(Sel(g4, "AutoGrabber.AskAfterHours", "No copy fits?", TooBig.AskAfterHours.ToString(), Ints(TooBig.HourChoices), TooBig.HourChoices.Select(TooBig.HoursText).ToArray()));
				l.Add(Sel(g4, "AutoGrabber.Type", "New shows look for", SeriesItem.NewShowType, AutoGrabberPage.TypeChoices, AutoGrabberPage.TypeChoices));
				l.Add(Chk(g4, "AutoGrabber.SkipPlaceholders", "Skip placeholder episodes (numbered 99, or no air date yet)", SeriesItem.SkipPlaceholders));
				l.Add(Chk(g5, "AutoGrabber.SeasonPacks", "Look for the whole season first when a lot of it is missing", SeriesItem.SeasonPacks));
				l.Add(Sel(g5, "AutoGrabber.SeasonPackPercent", "Whole season when", SeriesItem.SeasonPackPercent.ToString(), Ints(SeriesItem.SeasonPackPercentChoices), SeriesItem.SeasonPackPercentChoices.Select(SeriesItem.SeasonPackPercentText).ToArray()));
				l.Add(Num(g5, "AutoGrabber.MaxSeasonGB", "Biggest season (GB)", (long)SeriesItem.MaxSeasonGB, "0 = biggest episode x the number of episodes"));
				l.Add(Txt(g5, "AutoGrabber.SeasonFavourites", "Favourite season groups", string.Join(", ", SeriesItem.SeasonFavourites)));
				break;
			}
			case "finished":
			{
				const string g1 = "Copy into my library", gm = "New movies - filed into Movies or Collections", g2 = "Names and folders", g3 = "More";
				l.Add(Chk(g1, "Finished.Copy", "Copy finished downloads into my library (the torrent keeps seeding)", PostProcessor.Enabled));
				l.Add(Txt(g1, "Finished.TVFolder", "TV episodes go to", PostProcessor.TVFolder, "", "tvfolders"));
				l.Add(Txt(g1, "Finished.MovieFolder", "Movies go to", PostProcessor.MovieFolder, "the lists show the folders MP-TVSeries and Moving Pictures already scan", "moviefolders"));
				l.Add(Sel(gm, "Films.FileInto", "New movies", MovieFiling.Mode, new[] { "new", "library", "ask" },
					new[] { "Leave them in my new-movies folder - I file them myself", "File them for me - into Movies, or Collections\\[collection]\\", "File them for me, but show me first" }));
				l.Add(Txt(gm, "Films.MoviesFolder", "Movies folder", MovieFiling.MoviesFolder, "", "moviefolders"));
				l.Add(Txt(gm, "Films.CollectionsFolder", "Collections folder", MovieFiling.CollectionsFolder, "", "moviefolders"));
				l.Add(Chk(gm, "Films.WriteNfo", "Write a small .nfo with the movie's IMDb id next to it (Moving Pictures then picks the exact movie)", MovieFiling.WriteNfo));
				l.Add(Chk(g2, "Finished.Sort", "Rename and sort into folders", PostProcessor.Sort));
				l.Add(Pattern(g2, "Finished.NamePattern", "Episode names", Naming.Pattern, Naming.Styles, "pieces: " + Naming.Pieces));
				l.Add(Sel(g2, "Finished.SeasonPattern", "Season folders", Naming.SeasonPattern, Naming.SeasonStyles.Select((s, i) => i == 0 ? "" : s[1]).ToArray(), Naming.SeasonStyles.Select(s => s[0]).ToArray()));
				l.Add(Sel(g2, "Finished.ShowFolderPattern", "New show folders", Naming.ShowFolderPattern, Naming.ShowFolderStyles.Select(s => s[1]).ToArray(), Naming.ShowFolderStyles.Select(s => s[0]).ToArray(), "shows you already have keep their folder"));
				l.Add(Pattern(g2, "Films.NamePattern", "Movie names", Naming.MoviePattern, Naming.MovieStyles, "pieces: " + Naming.MoviePieces));
				l.Add(Sel(g2, "Films.FolderPattern", "Movie folders", Naming.MovieFolderPattern, Naming.MovieFolderStyles.Select(s => s[1]).ToArray(), Naming.MovieFolderStyles.Select(s => s[0]).ToArray()));
				l.Add(Txt(g3, "Finished.DropFolder", "Drop folder", DropFolder.Folder, "put a movie or episode here and it's named and filed like a download. Empty = off."));
				l.Add(Sel(g3, "TV.NightScan", "MP-TVSeries scan", NightScan.Mode, NightScan.Choices, NightScan.Choices.Select(NightScan.Text).ToArray(), "between 2 and 5 am, never while something plays"));
				break;
			}
			case "downloading": // 1.3.71: from the TV shows and When finished tabs
			{
				const string g1 = "Downloading";
				l.Add(Sel(g1, "AutoGrabber.MaxDownloading", "Downloads at once", Busy.Max.ToString(), Ints(Busy.Choices), Busy.Choices.Select(Busy.Text).ToArray(), "TV and movies. Ones you ask for never wait."));
				l.Add(Sel(g1, "AutoGrabber.StuckHours", "Stuck downloads", Grabbed.StuckHours.ToString(), Ints(Grabbed.StuckChoices), Grabbed.StuckChoices.Select(Grabbed.StuckText).ToArray(), "then delete it and try another copy"));
				l.Add(Sel(g1, "Finished.NearlyDoneHours", "Nearly finished (99%+, no seeds)", PostProcessor.NearlyDoneHours.ToString(), Ints(PostProcessor.NearlyDoneChoices), PostProcessor.NearlyDoneChoices.Select(PostProcessor.NearlyDoneText).ToArray(), "then check the video is whole and copy it anyway"));
				l.Add(Chk(g1, "Finished.CleanUp", "Clean up after seeding: remove a copied torrent and its leftover download once it has stopped seeding (files in your library are never deleted)", PostProcessor.CleanUp));
				break;
			}
			case "client":
			{
				const string g1 = "Torrent program";
				Configuration c = Configuration.Instance();
				string S(string k) => (c.Settings[k] as string ?? "").Trim();
				l.Add(Sel(g1, "uTorrent.Backend", "Program", S("uTorrent.Backend").ToLowerInvariant(), ClientKeys, ClientNames));
				l.Add(Txt(g1, "uTorrent.Server.IP", "Address", S("uTorrent.Server.IP"), "localhost or 127.0.0.1 when it's on this PC"));
				l.Add(Txt(g1, "uTorrent.Server.Port", "Port", S("uTorrent.Server.Port")));
				l.Add(Txt(g1, "uTorrent.Username", "User name", S("uTorrent.Username"), "empty = none"));
				l.Add(new Field { Group = g1, Key = "uTorrent.Password", Label = "Password", Kind = "pw", Current = S("uTorrent.Password"), Hint = "empty = none" });
				const string g2 = "Update frequency";
				l.Add(Txt(g2, "Torrent.Timer.Inside", "Inside MyTorrents (seconds)", S("Torrent.Timer.Inside")));
				l.Add(Txt(g2, "Torrent.Timer.Outside", "Outside MyTorrents (seconds)", S("Torrent.Timer.Outside")));
				l.Add(Chk(g2, "Torrent.Notifications", "Enable notifications", S("Torrent.Notifications").Equals("true", StringComparison.OrdinalIgnoreCase)));
				break;
			}
			}
			return l;
		}

		private static readonly string[] ClientKeys = { "utorrent", "azureus", "deluge", "qbittorrent", "biglybt" };
		private static readonly string[] ClientNames = { "uTorrent", "Azureus / Vuze (XML over HTTP)", "Deluge (Web UI)", "qBittorrent (Web UI)", "BiglyBT (Web Remote) / Transmission" };

		/// One tab's settings as a form, grouped like the Configuration tab.
		private static void SettingsForm(StringBuilder b, string tab, string title, string note = "")
		{
			List<Field> fields = TabFields(tab);
			b.Append("<form method=post><input type=hidden name=a value=savetab><input type=hidden name=tab value=\"" + tab + "\">");
			if (note.Length > 0) b.Append("<p class=dim>" + note + "</p>");
			foreach (IGrouping<string, Field> g in fields.GroupBy(x => x.Group))
			{
				b.Append("<fieldset><legend>" + H(g.Key) + "</legend><table class=form>");
				foreach (Field x in g) b.Append(FieldRow(x));
				b.Append("</table></fieldset>");
			}
			b.Append(Datalists(fields));
			b.Append("<p><button class=main>Save " + H(title) + " settings</button></p></form>");
		}

		private static string FieldRow(Field x)
		{
			string name = H(x.Key), hint = x.Hint.Length > 0 ? "<div class=\"dim small\">" + H(x.Hint) + "</div>" : "";
			string help = Help.For(x.Key); // 1.3.80: the same help as Configuration's tooltips - tap or hover the "?"
			if (help.Length > 0) hint = "<span class=help tabindex=0 title=\"" + H(help) + "\" data-h=\"" + H(help) + "\">?</span>" + hint;
			switch (x.Kind)
			{
			case "chk":
				return "<tr><td colspan=2><input type=hidden name=\"" + name + "\" value=false><label><input type=checkbox name=\"" + name + "\" value=true" + (x.Current == "true" ? " checked" : "") + "> " + H(x.Label) + "</label>" + hint + "</td></tr>";
			case "sel":
			{
				List<string> v = x.Values.ToList(), lab = x.Labels.ToList();
				if (!v.Contains(x.Current)) // something set in an older version or by hand - show it, don't lose it
				{
					v.Insert(0, x.Current);
					lab.Insert(0, x.Current.Length > 0 ? x.Current : "(not set)");
				}
				return "<tr><td>" + H(x.Label) + "</td><td>" + Select(x.Key, x.Current, v.ToArray(), lab.ToArray()) + hint + "</td></tr>";
			}
			case "pattern":
			{
				bool own = !x.Values.Contains(x.Current);
				List<string> v = x.Values.ToList(), lab = x.Labels.ToList();
				v.Add("__own");
				lab.Add("My own pattern (below)");
				return "<tr><td>" + H(x.Label) + "</td><td>" + Select(x.Key, own ? "__own" : x.Current, v.ToArray(), lab.ToArray()) +
					"<div><input name=\"" + name + "__own\" size=60 placeholder=\"or type your own pattern\" value=\"" + (own ? H(x.Current) : "") + "\"></div>" + hint + "</td></tr>";
			}
			case "num":
				return "<tr><td>" + H(x.Label) + "</td><td><input name=\"" + name + "\" size=8 inputmode=numeric value=\"" + H(x.Current) + "\">" + hint + "</td></tr>";
			case "pw":
				return "<tr><td>" + H(x.Label) + "</td><td><input type=password name=\"" + name + "\" size=30 value=\"" + H(x.Current) + "\">" + hint + "</td></tr>";
			default:
				return "<tr><td>" + H(x.Label) + "</td><td><input name=\"" + name + "\" size=60 value=\"" + H(x.Current) + "\"" + (x.Datalist.Length > 0 ? " list=" + x.Datalist : "") + ">" + hint + "</td></tr>";
			}
		}

		/// The library folders MP-TVSeries / Moving Pictures scan, offered as you type a folder.
		private static string Datalists(List<Field> fields)
		{
			StringBuilder s = new StringBuilder();
			foreach (string id in fields.Select(f => f.Datalist).Where(d => d.Length > 0).Distinct())
			{
				List<string> folders = new List<string>();
				try
				{
					folders = (id == "tvfolders" ? LibraryFolders.TV() : LibraryFolders.Movies()).Where(f => !string.IsNullOrWhiteSpace(f)).Distinct().ToList();
				}
				catch
				{
				}
				s.Append("<datalist id=" + id + ">" + string.Join("", folders.Select(f => "<option value=\"" + H(f) + "\">")) + "</datalist>");
			}
			return s.ToString();
		}

		/// Saves one tab's form; only that tab's settings are touched. Returns the message to show.
		private static string SaveTab(string tab, Dictionary<string, string> f)
		{
			List<Field> fields = TabFields(tab);
			if (fields.Count == 0) return "Nothing to save";
			int n = 0;
			bool client = false;
			foreach (Field x in fields)
			{
				if (!f.ContainsKey(x.Key)) continue;
				string v = (Get(f, x.Key) ?? "").Trim();
				if (x.Kind == "pattern" && v == "__own") v = (Get(f, x.Key + "__own") ?? "").Trim();
				if (x.Kind == "pattern" && v.Length == 0) continue; // an empty own pattern - keep what was there
				if (x.Kind == "num")
				{
					long num;
					if (!long.TryParse(v, out num) || num < 0) continue; // not a number - keep what was there
					v = num.ToString();
				}
				if (x.Kind == "sel" && !x.Values.Contains(v) && v != x.Current) continue; // only the offered choices
				if (x.Key.EndsWith("Favourites")) v = string.Join(", ", v.Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).Where(s => s.Length > 0));
				if (string.Equals(v, x.Current, StringComparison.OrdinalIgnoreCase)) continue;
				if (x.Key.StartsWith("Torrent.Timer.") && !int.TryParse(v, out int secs)) continue; // seconds only
				if (x.Key == "Torrent.Notifications") v = v == "true" ? "True" : "False"; // as Configuration saves it
				if (x.Key.StartsWith("uTorrent.") || x.Key.StartsWith("Torrent."))
				{
					Configuration.Instance().Settings[x.Key] = v;
					client = true;
				}
				else Configuration.SetExtra(x.Key, v);
				n++;
			}
			if (client)
			{
				Configuration.Instance().Save();
				_client = null; // connect again with the new details
			}
			return n == 0 ? "No changes" : "Saved (" + n + (n == 1 ? " change)" : " changes)");
		}
	}
}
