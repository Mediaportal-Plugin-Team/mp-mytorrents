using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace Sohvaperuna.MyTorrents
{
	/// <summary>
	/// 1.3.80: one plain help text per setting - shown as a tooltip in MyTorrents Configuration (held up for 30 seconds,
	/// like MP-TVSeries) and as a "?" next to the same setting on the web page, so both always say the same thing.
	/// </summary>
	public static class Help
	{
		private static readonly Dictionary<string, string> Texts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
		{
			// ---- TV shows
			["AutoGrabber.Quality"] = "The picture size MyTorrents looks for first. 720p is a good balance of quality and size; 1080p is sharper but bigger.",
			["AutoGrabber.MaxSizeMB"] = "Copies of one episode bigger than this are skipped. 0 means no limit. Raise it if good copies keep being skipped as too big.",
			["AutoGrabber.MinSizeMB"] = "Copies smaller than this are skipped - tiny files are usually fakes or samples. 0 means no minimum.",
			["AutoGrabber.Codec"] = "The kind of video. HEVC (x265) is smaller for the same quality but some older players can't play it; x264 plays everywhere.",
			["AutoGrabber.Sound"] = "Which sound to prefer: any, or surround (5.1) first. Copies in another language are always left out.",
			["AutoGrabber.MinSeeders"] = "How many people must be sharing a copy before it's downloaded. Fewer seeders means slow or stuck downloads.",
			["AutoGrabber.Favourites"] = "Release groups or sites you trust, with commas between. Their copies are preferred when they fit your other settings.",
			["AutoGrabber.FavouritesMode"] = "Prefer your favourites when there's a choice, or only ever take copies from them.",
			["AutoGrabber.Fallback1080Percent"] = "When no good 720p copy turns up, take a 1080p one - as long as it isn't more than this much bigger. Only used with 720p chosen.",
			["AutoGrabber.RelaxSeeds"] = "When nothing turns up for a while, accept copies with fewer seeders, step by step, so rare episodes still come in.",
			["AutoGrabber.RelaxAfterHours"] = "How long to wait before accepting fewer seeders.",
			["AutoGrabber.RelaxStepHours"] = "After that, how often to lower the number of seeders by one.",
			["AutoGrabber.LowestSeeders"] = "The fewest seeders it will ever accept.",
			["AutoGrabber.CantFindDays"] = "Episodes still not found after this long go on the Can't find list and are then only looked for once a day, to save searching.",
			["AutoGrabber.SearchesAtOnce"] = "How many shows search at the same time. Fewer is lighter on the PC and the search sites. Anything you ask for yourself never waits.",
			["AutoGrabber.AskAfterHours"] = "When copies exist but none fit your settings, ask you to pick one after this long (in Not found), instead of waiting for ever.",
			["AutoGrabber.Type"] = "What new shows look for: Missing episodes fills every gap, old ones too; New episodes only gets ones newer than the latest you have.",
			["AutoGrabber.SkipPlaceholders"] = "Don't look for placeholder episodes - ones numbered 99, or with no air date yet - that aren't real episodes yet.",
			["AutoGrabber.SeasonPacks"] = "When a lot of a season is missing, look for the whole season in one download first - quicker, and usually better shared.",
			["AutoGrabber.SeasonPackPercent"] = "How much of a season has to be missing before the whole season is looked for.",
			["AutoGrabber.MaxSeasonGB"] = "Whole seasons bigger than this are skipped. 0 means your biggest episode size times the number of episodes.",
			["AutoGrabber.SeasonFavourites"] = "Release groups you trust for whole seasons, with commas between.",
			["AutoGrabber.AutoAddRunning"] = "Automatic: MyTorrents adds your running shows and looks for new and missing episodes and wanted movies by itself. Manual: it stops looking by itself - your show list is kept - and only looks when you press Check now, Grab the missing ones, Look now or Search, or add a movie.",
			// ---- Movies
			["Films.Quality"] = "The picture size MyTorrents looks for when getting a movie.",
			["Films.MaxSizeMB"] = "Movie copies bigger than this are skipped. 0 means no limit.",
			["Films.MinSizeMB"] = "Movie copies smaller than this are skipped - tiny files are usually fakes or samples.",
			["Films.LookEveryHours"] = "How often wanted movies are looked for again. Movies you've just added are looked for straight away.",
			["Films.Sound"] = "Which sound to prefer for movies: any, or surround (5.1) first.",
			["Films.Favourites"] = "Release groups or sites you trust for movies, with commas between.",
			["Films.UpgradeAuto"] = "Movie upgrades: each day a few stereo-only movies are added to the upgrade list, and 5.1 or better copies are looked for by themselves - in Manual too. Off: upgrades are only looked for when you ask.",
			["Films.UpgradeDeleteOld"] = "Once the better copy is in your library, delete the old one. Off: the old copy is kept for you to delete.",
			["Films.FileInto"] = "What happens to new movies in the new-movies folder: leave them for you, file them into Movies or Collections, or file them after you've said OK.",
			["Films.MoviesFolder"] = "Where your movies are filed. The list shows folders Moving Pictures already scans.",
			["Films.CollectionsFolder"] = "Where movies that are part of a collection (like a trilogy) are filed, each collection in its own folder.",
			["Films.WriteNfo"] = "Write a small .nfo file with the movie's IMDb number next to each movie, so Moving Pictures picks exactly the right movie.",
			["Trakt.ListOn"] = "Add the movies on your Trakt list to Wanted movies, and check the list every few hours.",
			["Trakt.ListName"] = "The name of your Trakt list. Leave it as watchlist to use your Trakt watchlist.",
			["Trakt.ListRemoveDropped"] = "When you take a movie off Wanted movies, take it off your Trakt list too, so it doesn't come back.",
			// ---- File management
			["Finished.Copy"] = "Copy finished downloads into your library. The torrent keeps seeding from the download folder.",
			["Finished.TVFolder"] = "Where finished TV episodes are put. Shows you already have go into their own folder.",
			["Finished.MovieFolder"] = "Where finished movies are put (your new-movies folder).",
			["Finished.Sort"] = "Name episodes and movies your way and sort them into show, season and movie folders. Off: they keep their download names.",
			["Finished.NamePattern"] = "How episode files are named. Pick a style or type your own.",
			["Finished.SeasonPattern"] = "How season folders are named, e.g. Show Season 01.",
			["Finished.ShowFolderPattern"] = "How new show folders are named. Shows you already have keep their folder.",
			["Films.NamePattern"] = "How movie files and folders are named. Pick a style or type your own.",
			["Films.FolderPattern"] = "How movie folders are named - usually the same as the movie's name.",
			["Finished.DropFolder"] = "Put a movie or episode here and it's named and filed like a download, then moved out. Empty means off. Not inside your TV or movie folder.",
			["TV.NightScan"] = "A scan of your TV folders by MP-TVSeries between 2 and 5 am, never while something is playing. Catches files you add, rename or delete by hand.",
			// ---- Downloading
			["AutoGrabber.MaxDownloading"] = "How many torrents may download at the same time. While this many are going, new searches wait.",
			["AutoGrabber.StuckHours"] = "A download that hasn't moved for this long is removed and another copy is tried.",
			["Finished.NearlyDoneHours"] = "A download stuck at 99% or more with nobody sharing it is given up on after this long, and another copy is tried.",
			["Finished.CleanUp"] = "Once a copied torrent has stopped seeding, remove it and its leftover download. Files in your library are never deleted.",
			// ---- Torrent program
			["uTorrent.Backend"] = "The torrent program MyTorrents sends downloads to: qBittorrent, BiglyBT, Deluge, Transmission, uTorrent or Vuze. Changing it takes effect straight away.",
			["uTorrent.Server.IP"] = "The address of the torrent program's web interface. localhost means this PC.",
			["uTorrent.Server.Port"] = "The port of the torrent program's web interface (shown in its web interface settings).",
			["uTorrent.Username"] = "The user name for the torrent program's web interface, if it asks for one.",
			["uTorrent.Password"] = "The password for the torrent program's web interface, if it asks for one.",
			["Torrent.Timer.Inside"] = "How often the downloads list is refreshed while you're in MyTorrents.",
			["Torrent.Timer.Outside"] = "How often downloads are checked while you're elsewhere in MediaPortal.",
			["Torrent.Notifications"] = "Show a message in MediaPortal when a download finishes.",
			// ---- Background
			["Helper.On"] = "Keep MyTorrents working when MediaPortal is closed: it starts with Windows and sits as a small icon by the clock. It pauses while MediaPortal is open.",
			["Web.On"] = "Let you use MyTorrents from a browser on other computers, tablets and phones on your home network. It doesn't answer the internet.",
			["Web.Port"] = "The port number of the web page. Only change it if another program already uses this one.",
			["Web.Password"] = "A password for the web page. Empty means no password. The user name is mytorrents.",
			// ---- buttons
			["btn.StartFresh"] = "MyTorrents forgets what it has tried and looks for everything again with your settings. Your settings, show list and Wanted movies are kept, and it can be undone.",
			["btn.UndoFresh"] = "Puts back everything MyTorrents remembered before you started fresh.",
			["btn.Duplicates"] = "Finds episodes and movies you have more than once, so you can keep the best copy.",
			["btn.CheckNow"] = "Runs the check-up now instead of waiting for the next day.",
			["btn.TidyMovies"] = "Renames the movies you already have to match your movie naming style. You see every change first.",
			["btn.Firewall"] = "Lets the web page through Windows Firewall so other computers and phones can reach it.",
			["btn.OpenWeb"] = "Opens the MyTorrents web page in your browser.",
			["btn.Settings"] = "Downloads at once, stuck downloads, nearly-finished downloads and clean-up.",
		};

		/// The help text for a setting (or "btn.*" button), "" when there is none.
		public static string For(string key) => key != null && Texts.TryGetValue(key, out string t) ? t : "";

		// Configuration: which field on which tab is which setting
		private static readonly Dictionary<string, string> Fields = new Dictionary<string, string>
		{
			["AutoGrabberPage._quality"] = "AutoGrabber.Quality", ["AutoGrabberPage._size"] = "AutoGrabber.MaxSizeMB", ["AutoGrabberPage._minSize"] = "AutoGrabber.MinSizeMB",
			["AutoGrabberPage._codec"] = "AutoGrabber.Codec", ["AutoGrabberPage._tvSound"] = "AutoGrabber.Sound", ["AutoGrabberPage._seeds"] = "AutoGrabber.MinSeeders",
			["AutoGrabberPage._favourites"] = "AutoGrabber.Favourites", ["AutoGrabberPage._favMode"] = "AutoGrabber.FavouritesMode", ["AutoGrabberPage._fallback"] = "AutoGrabber.Fallback1080Percent",
			["AutoGrabberPage._relax"] = "AutoGrabber.RelaxSeeds", ["AutoGrabberPage._relaxAfter"] = "AutoGrabber.RelaxAfterHours", ["AutoGrabberPage._relaxStep"] = "AutoGrabber.RelaxStepHours",
			["AutoGrabberPage._relaxLowest"] = "AutoGrabber.LowestSeeders", ["AutoGrabberPage._cantFind"] = "AutoGrabber.CantFindDays", ["AutoGrabberPage._atOnce"] = "AutoGrabber.SearchesAtOnce",
			["AutoGrabberPage._ask"] = "AutoGrabber.AskAfterHours", ["AutoGrabberPage._type"] = "AutoGrabber.Type", ["AutoGrabberPage._skipPlaceholders"] = "AutoGrabber.SkipPlaceholders",
			["AutoGrabberPage._packs"] = "AutoGrabber.SeasonPacks", ["AutoGrabberPage._packPercent"] = "AutoGrabber.SeasonPackPercent", ["AutoGrabberPage._packGB"] = "AutoGrabber.MaxSeasonGB",
			["AutoGrabberPage._packFavourites"] = "AutoGrabber.SeasonFavourites",
			["MoviesPage._quality"] = "Films.Quality", ["MoviesPage._size"] = "Films.MaxSizeMB", ["MoviesPage._minSize"] = "Films.MinSizeMB", ["MoviesPage._look"] = "Films.LookEveryHours",
			["MoviesPage._sound"] = "Films.Sound", ["MoviesPage._traktOn"] = "Trakt.ListOn", ["MoviesPage._traktList"] = "Trakt.ListName",
			["MoviesPage._traktRemove"] = "Trakt.ListRemoveDropped", ["MoviesPage._iFile"] = "Films.FileInto", ["MoviesPage._fileForMe"] = "Films.FileInto", ["MoviesPage._askFirst"] = "Films.FileInto",
			["MoviesPage._moviesFolder"] = "Films.MoviesFolder", ["MoviesPage._collectionsFolder"] = "Films.CollectionsFolder", ["MoviesPage._nfo"] = "Films.WriteNfo",
			["FinishedPage._copy"] = "Finished.Copy", ["FinishedPage._tv"] = "Finished.TVFolder", ["FinishedPage._movies"] = "Finished.MovieFolder", ["FinishedPage._sort"] = "Finished.Sort",
			["FinishedPage._seasonStyle"] = "Finished.SeasonPattern",
			["FinishedPage._showStyle"] = "Finished.ShowFolderPattern", 
			["FinishedPage._movieFolderStyle"] = "Films.FolderPattern", ["FinishedPage._drop"] = "Finished.DropFolder", ["FinishedPage._nightScan"] = "TV.NightScan", 
			["DownloadsPage._mode"] = "AutoGrabber.AutoAddRunning", ["DownloadsPage._settings"] = "btn.Settings",
			["BackgroundPage._helper"] = "Helper.On", ["BackgroundPage._web"] = "Web.On", ["BackgroundPage._port"] = "Web.Port", ["BackgroundPage._password"] = "Web.Password",
			["BackgroundPage._firewall"] = "btn.Firewall", ["BackgroundPage._open"] = "btn.OpenWeb",
			["CheckUpPage._fresh"] = "btn.StartFresh", ["CheckUpPage._undoFresh"] = "btn.UndoFresh", ["CheckUpPage._dups"] = "btn.Duplicates", ["CheckUpPage._now"] = "btn.CheckNow",
			["SettingsForm.combo_Backend"] = "uTorrent.Backend", ["SettingsForm.textBox_IPAddress"] = "uTorrent.Server.IP", ["SettingsForm.numeric_Port"] = "uTorrent.Server.Port",
			["SettingsForm.textBox_Username"] = "uTorrent.Username", ["SettingsForm.textBox_Password"] = "uTorrent.Password", ["SettingsForm.numeric_InsideTimer"] = "Torrent.Timer.Inside",
			["SettingsForm.numeric_OutsideTimer"] = "Torrent.Timer.Outside", ["SettingsForm.checkBoxNotifications"] = "Torrent.Notifications",
		};

		/// Configuration: puts the help on every known setting of a tab (and on the label just left of it).
		public static void Apply(object page)
		{
			try
			{
				ToolTip tip = new ToolTip { AutoPopDelay = 30000, InitialDelay = 500, ReshowDelay = 200, ShowAlways = true };
				string cls = page.GetType().Name;
				foreach (FieldInfo f in page.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
				{
					Control c = f.GetValue(page) as Control;
					string key;
					if (c == null || !Fields.TryGetValue(cls + "." + f.Name, out key)) continue;
					string text = For(key);
					if (text.Length == 0) continue;
					tip.SetToolTip(c, Wrap(text));
					Label left = LabelLeftOf(c);
					if (left != null && tip.GetToolTip(left).Length == 0) tip.SetToolTip(left, Wrap(text));
				}
			}
			catch
			{
			}
		}

		/// Configuration: help on one control (for windows built without fields, e.g. the Downloading settings).
		public static void On(Control c, string key)
		{
			string text = For(key);
			if (c == null || text.Length == 0) return;
			ToolTip tip = new ToolTip { AutoPopDelay = 30000, InitialDelay = 500, ReshowDelay = 200, ShowAlways = true };
			tip.SetToolTip(c, Wrap(text));
			Label left = LabelLeftOf(c);
			if (left != null) tip.SetToolTip(left, Wrap(text));
		}

		private static Label LabelLeftOf(Control c)
		{
			if (c.Parent == null) return null;
			return c.Parent.Controls.OfType<Label>()
				.Where(l => l.Right <= c.Left + 2 && c.Left - l.Right < 60 && Math.Abs((l.Top + l.Height / 2) - (c.Top + c.Height / 2)) < 12)
				.OrderByDescending(l => l.Right).FirstOrDefault();
		}

		private static string Wrap(string text)
		{
			// tooltips don't wrap by themselves - break at about 70 characters
			List<string> lines = new List<string>();
			string line = "";
			foreach (string w in text.Split(' '))
			{
				if (line.Length + w.Length + 1 > 70 && line.Length > 0) { lines.Add(line); line = w; }
				else line = line.Length == 0 ? w : line + " " + w;
			}
			if (line.Length > 0) lines.Add(line);
			return string.Join("\r\n", lines);
		}
	}
}
