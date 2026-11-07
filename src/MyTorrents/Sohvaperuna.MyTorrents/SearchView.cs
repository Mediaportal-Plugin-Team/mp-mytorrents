using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using MediaPortal.Dialogs;
using MediaPortal.GUI.Library;
using Sohvaperuna.TorrentEngine;
using Sohvaperuna.TorrentSearch;

namespace Sohvaperuna.MyTorrents
{
	internal class SearchView
	{
		public List<GUIListItem> SearchResults = new List<GUIListItem>();

		private string _searchString;

		private ProgressUpdater _progressDlg;

		private ITorrentSearch _searchEngine;

		private string _filter = "";

		private TorrentMatchList _matches;

		public TorrentLabel label = null;

		public string SearchString
		{
			get
			{
				return _searchString;
			}
			set
			{
				_searchString = value;
			}
		}

		private void _torrentEngine_SearchCompleted(TorrentMatchList matches)
		{
			try
			{
				_matches = matches ?? new TorrentMatchList(null);
				UpdateSearchResults();
				if (_matches.Count == 0)
				{
					// 2026: say so, instead of just closing the progress window
					string words = _searchString;
					GUIWindowManager.SendThreadCallback((p1, p2, data) =>
					{
						MyTorrents.Error("Nothing found", "No results for \"" + words + "\".\nTry fewer words - for example just the show's name - or check the sites in MyTorrents Configuration > Search sites.");
						return 0;
					}, 0, 0, null);
				}
				RemindFewSites();
			}
			catch (Exception ex)
			{
				Log.Instance().Print("Showing search results failed: " + ex.Message);
				try
				{
					((GUIDialogProgress)GUIWindowManager.GetWindow(101))?.Close(); // never leave "Fetching torrents..." open
				}
				catch
				{
				}
			}
		}

		private static bool _remindedFewSites;

		/// 2026: Jackett with only one or two sites finds little - say how to add more (once per MediaPortal session).
		private static void RemindFewSites()
		{
			int n = JackettAggregateEngine.IndexerCount;
			if (_remindedFewSites || n < 0 || n >= 3 || !JackettAggregateEngine.Enabled) return;
			_remindedFewSites = true;
			MyTorrents.Error(n == 0 ? "No search sites in Jackett" : "Only " + n + " search site" + (n == 1 ? "" : "s"),
				"More sites = more results to choose from.\nOn this PC open MyTorrents Configuration (desktop icon) > Search sites > Add sites... and tick the ones you want.");
		}

		private void UpdateSearchResults()
		{
			try
			{
				SearchResults.Clear();
			}
			catch (Exception)
			{
			}
			IconMapping iconMapping = Configuration.Instance().Settings["MyTorrents.GlobalIconMapping"] as IconMapping;
			Regex regex = null;
			if (_filter != "")
			{
				regex = new Regex(_filter, RegexOptions.IgnoreCase);
			}
			foreach (TorrentMatch match in _matches)
			{
				if (regex == null || regex.IsMatch(match.Title))
				{
					GUIListItem gUIListItem = new GUIListItem();
					gUIListItem.PinImage = match.Icon;
					gUIListItem.IconImage = match.Icon;
					gUIListItem.IconImageBig = match.Icon;
					// 2026: title on the left, "size · seeds · site" on the right. Label3 is left empty -
					// skins place it differently and it was drawn over the start of the title.
					gUIListItem.Label = match.Title;
					string details = match.Size > 0 ? UnitConvert.SizeToString(match.Size) : "";
					details += (details.Length > 0 ? " · " : "") + $"{match.Seed} seeds";
					if (!string.IsNullOrEmpty(match.tracker))
					{
						details += " · " + match.tracker;
					}
					gUIListItem.Label2 = details;
					gUIListItem.Label3 = "";
					gUIListItem.OnItemSelected += OnSearchResultSelected;
					gUIListItem.AlbumInfoTag = match;
					SearchResults.Add(gUIListItem);
				}
			}
			GUIDialogProgress gUIDialogProgress = (GUIDialogProgress)GUIWindowManager.GetWindow(101);
			gUIDialogProgress.Close();
			_progressDlg = null;
			GUIPropertyManager.SetProperty("#MyTorrents.Count", $"{SearchResults.Count}");
		}

		protected void OnPageLoad()
		{
			string text = null;
			FieldInfo field = typeof(GUIWindow).GetField("_loadParameter", BindingFlags.Instance | BindingFlags.NonPublic);
			if ((object)field != null)
			{
				text = (string)field.GetValue(this);
			}
			if (!string.IsNullOrEmpty(text))
			{
				_searchString = text;
				TorrentSearchEngine torrentSearchEngine = new TorrentSearchEngine(Configuration.Instance().Settings["TorrentSearch.Config"] as string);
				_searchEngine = PickTVSeriesEngine(torrentSearchEngine);
				SearchKind = SearchContext.LooksLikeEpisode(text) ? SearchContext.TV : SearchContext.Any;
				TorrentEngine.Instance().SearchCompleted -= _torrentEngine_SearchCompleted;
				TorrentEngine.Instance().SearchCompleted += _torrentEngine_SearchCompleted;
				UpdateSearchResults("Default");
			}
			else
			{
				StartSearch();
			}
		}

		/// Engine used when another plugin (e.g. MP-TVSeries) opens MyTorrents with a search:
		/// the "@mptvseries" combiner if configured, otherwise Jackett (all indexers), otherwise the first enabled engine.
		/// Engine used when another plugin (e.g. MP-TVSeries) opens MyTorrents with a search:
		/// "mptvseries" = Jackett (all indexers) + your enabled sites; if neither is set up, the first enabled engine.
		private static ITorrentSearch PickTVSeriesEngine(TorrentSearchEngine engines)
		{
			TorrentSearch_Combiner tv = engines.Engines.OfType<TorrentSearch_Combiner>().FirstOrDefault(e => e.Name == TorrentSearchEngine.TVSeriesEngineName);
			if (tv != null && tv.Engines.Count > 0)
			{
				return tv;
			}
			return engines.Engines.FirstOrDefault(e => !(e is TorrentSearch_Combiner) && string.Equals(e.Enabled, "true", StringComparison.OrdinalIgnoreCase));
		}

		/// What the current search is for (TV / Movie / anything), decided by where the search came from.
		public string SearchKind = SearchContext.Any;

		public void StartParametricSearch(string param, string kind = "")
		{
			SearchKind = kind ?? SearchContext.Any;
			_searchString = param;
			TorrentSearchEngine torrentSearchEngine = new TorrentSearchEngine(Configuration.Instance().Settings["TorrentSearch.Config"] as string);
			try
			{
				_searchEngine = PickTVSeriesEngine(torrentSearchEngine);
				// 2026 fix: searches started by other plugins (MP-TVSeries etc.) never received their results
				TorrentEngine.Instance().SearchCompleted -= _torrentEngine_SearchCompleted;
				TorrentEngine.Instance().SearchCompleted += _torrentEngine_SearchCompleted;
				MyTorrents.Instance().torrentList.Clear();
				MyTorrents.Instance().torrentList.Visible = true;
				MyTorrents.Instance().torrentList.ListItems = SearchResults;
				UpdateSearchResults("Default");
			}
			catch (Exception e)
			{
				Log.Instance().Print(e);
			}
		}

		public void StartSearch()
		{
			SearchKind = SearchContext.Any;
			MyTorrents.Instance().torrentList.Visible = false;
			TorrentEngine.Instance().SearchCompleted -= _torrentEngine_SearchCompleted; // never subscribe twice
			TorrentEngine.Instance().SearchCompleted += _torrentEngine_SearchCompleted;
			VirtualKeyboard virtualKeyboard = (VirtualKeyboard)GUIWindowManager.GetWindow(1002);
			virtualKeyboard.Reset();
			virtualKeyboard.Text = ((SearchString == null) ? "" : SearchString);
			virtualKeyboard.DoModal(GUIWindowManager.ActiveWindow);
			Menus.Check(virtualKeyboard);
			if (!virtualKeyboard.IsConfirmed)
			{
				MyTorrents.Instance().torrentList.Visible = true;
				MyTorrents.ListType = "Torrents";
				return;
			}
			SearchString = virtualKeyboard.Text;
			GUIDialogMenu gUIDialogMenu = (GUIDialogMenu)GUIWindowManager.GetWindow(2012);
			if (gUIDialogMenu == null)
			{
				return;
			}
			// 2026 (1.3.40): what are you looking for? Films use the film quality / size
			gUIDialogMenu.Reset();
			gUIDialogMenu.SetHeading("What are you looking for?");
			gUIDialogMenu.Add(SearchContext.LooksLikeEpisode(SearchString) ? "TV (episode)" : "TV");
			gUIDialogMenu.Add("A movie (" + Films.Quality + ", up to " + Films.SizeText(Films.MaxSizeMB) + ")");
			gUIDialogMenu.Add("Anything");
			gUIDialogMenu.DoModal(GUIWindowManager.ActiveWindow);
			Menus.Check(gUIDialogMenu);
			if (gUIDialogMenu.SelectedId == 1) SearchKind = SearchContext.TV;
			else if (gUIDialogMenu.SelectedId == 2) SearchKind = SearchContext.Movie;
			else if (gUIDialogMenu.SelectedId == 3) SearchKind = SearchContext.Any;
			else
			{
				MyTorrents.Instance().torrentList.Visible = true;
				MyTorrents.ListType = "Torrents";
				return;
			}
			gUIDialogMenu.Reset();
			gUIDialogMenu.SetHeading("Which engine to use?");
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
				GUIListItem gUIListItem = new GUIListItem();
				gUIListItem.Label = engine.Name;
				gUIDialogMenu.Add(gUIListItem);
			}
			gUIDialogMenu.DoModal(GUIWindowManager.ActiveWindow);
			Menus.Check(gUIDialogMenu);
			if (gUIDialogMenu.SelectedLabel == -1)
			{
				MyTorrents.Instance().torrentList.Visible = true;
				MyTorrents.ListType = "Torrents";
				return;
			}
			_searchEngine = torrentSearchEngine.Engines[gUIDialogMenu.SelectedLabel];
			MyTorrents.Instance().torrentList.Clear();
			MyTorrents.Instance().torrentList.Visible = true;
			MyTorrents.Instance().torrentList.ListItems = SearchResults;
			UpdateSearchResults("Default");
		}

		public void UpdateSearchResults(string sortOrder)
		{
			if (_searchEngine == null)
			{
				MyTorrents.Error("Search", "No search sites set up yet. Add Jackett or your own sites in MyTorrents Configuration > Search sites.");
				return;
			}
			GUIDialogProgress gUIDialogProgress = (GUIDialogProgress)GUIWindowManager.GetWindow(101);
			gUIDialogProgress.Reset();
			gUIDialogProgress.SetHeading("Torrents");
			gUIDialogProgress.SetLine(1, "Fetching torrents...");
			gUIDialogProgress.SetPercentage(0);
			gUIDialogProgress.ShowProgressBar(DisplayBar: true);
			gUIDialogProgress.StartModal(GUIWindowManager.ActiveWindow);
			_progressDlg = new ProgressUpdater();
			_progressDlg.SetAutoUpdate(1000.0, 20.0);
			TorrentEngine.Instance().SearchTorrent(_searchEngine.Name, _searchString, sortOrder, gUIDialogProgress, SearchKind);
		}

		private void OnSearchResultSelected(GUIListItem item, GUIControl parent)
		{
		}

		public void SearchContextMenu(TorrentMatch match)
		{
			GUIDialogMenu gUIDialogMenu = (GUIDialogMenu)GUIWindowManager.GetWindow(2012);
			if (gUIDialogMenu == null)
			{
				return;
			}
			gUIDialogMenu.Reset();
			gUIDialogMenu.SetHeading("Torrent menu");
			gUIDialogMenu.Add("Search again");
			gUIDialogMenu.Add("Sort by >>");
			gUIDialogMenu.Add("Filter results");
			gUIDialogMenu.Add("Watch this torrent for change");
			gUIDialogMenu.DoModal(GUIWindowManager.ActiveWindow);
			Menus.Check(gUIDialogMenu);
			switch (gUIDialogMenu.SelectedLabelText)
			{
			case "Filter results":
			{
				VirtualKeyboard virtualKeyboard = (VirtualKeyboard)GUIWindowManager.GetWindow(1002);
				virtualKeyboard.Reset();
				virtualKeyboard.Text = _filter;
				virtualKeyboard.DoModal(GUIWindowManager.ActiveWindow);
				Menus.Check(virtualKeyboard);
				if (virtualKeyboard.IsConfirmed)
				{
					_filter = virtualKeyboard.Text;
					UpdateSearchResults();
				}
				break;
			}
			case "Search again":
				StartSearch();
				break;
			case "Sort by >>":
				gUIDialogMenu.Reset();
				gUIDialogMenu.SetHeading("Sort by...");
				foreach (string item in _searchEngine.SortMethods())
				{
					gUIDialogMenu.Add(item);
				}
				gUIDialogMenu.DoModal(GUIWindowManager.ActiveWindow);
				Menus.Check(gUIDialogMenu);
				if (gUIDialogMenu.SelectedLabel != -1)
				{
					UpdateSearchResults(gUIDialogMenu.SelectedLabelText);
				}
				break;
			case "Watch this torrent for change":
				if (match != null)
				{
					AddWatchFromSearch(match, "", "");
					MyTorrents.Error("Watch list", "Added - MyTorrents downloads it again if the site replaces this torrent.");
				}
				break;
			}
		}

		public void SearchItemClicked(TorrentMatch match)
		{
			string empty = string.Empty;
			string empty2 = string.Empty;
			string hash = string.Empty;
			string folder = string.Empty;
			if (string.IsNullOrEmpty(match.magnet))
			{
				empty = match.Url;
				empty2 = "torrent";
			}
			else
			{
				empty = match.magnet;
				empty2 = "magnet";
			}
			GUIDialogYesNo gUIDialogYesNo = (GUIDialogYesNo)GUIWindowManager.GetWindow(100);
			gUIDialogYesNo.SetHeading("Download this torrent?");
			gUIDialogYesNo.SetLine(1, match.Title);
			gUIDialogYesNo.SetLine(2, UnitConvert.SizeToString(match.Size));
			gUIDialogYesNo.DoModal(GUIWindowManager.ActiveWindow);
			Menus.Check(gUIDialogYesNo);
			if (!gUIDialogYesNo.IsConfirmed)
			{
				return;
			}
			label = DialogAskLabel.Ask();
			if (label == null)
			{
				return;
			}
			Log.Instance().Print("label is " + label.Name);
			List<DownloadDir> list = TorrentEngine.Instance().TorrentSession.ListDirs();
			if (list == null)
			{
			}
			GUIDialogMenu gUIDialogMenu = (GUIDialogMenu)GUIWindowManager.GetWindow(2012);
			if (gUIDialogMenu == null)
			{
				return;
			}
			int selectedId = 1;
			string selectedLabelText = "No change";
			// 2026: the "Download options" question is off by default - downloads go to the folder shown at the top
			// ("Saving to"); turn it back on with the setting "Search.AskFolder" = true
			if (Configuration.GetExtra("Search.AskFolder", "false") == "true")
			{
				gUIDialogMenu.Reset();
				gUIDialogMenu.SetHeading("Download options");
				gUIDialogMenu.Add("No change");
				gUIDialogMenu.Add("Browse Folders");
				gUIDialogMenu.DoModal(GUIWindowManager.ActiveWindow);
				Menus.Check(gUIDialogMenu);
				if (gUIDialogMenu.SelectedId == -1)
				{
					return;
				}
				selectedId = gUIDialogMenu.SelectedId;
				selectedLabelText = gUIDialogMenu.SelectedLabelText;
			}
			Log.Instance().Print("download option is " + selectedLabelText);
			bool flag = false;
			string downoption = "";
			if ((object)_searchEngine.GetType() == typeof(TorrentSearch_Combiner))
			{
				Log.Instance().Print("Selected engine is Combiner");
				TorrentSearch_Combiner torrentSearch_Combiner = (TorrentSearch_Combiner)_searchEngine;
				if (_searchEngine.Parameters.ContainsKey("Options"))
				{
					downoption = torrentSearch_Combiner.Engines.Where((ITorrentSearch e) => e.Name == match.tracker).First().Parameters["Options"].ToString().Replace("%id%", match.Id);
				}
			}
			else if (_searchEngine.Parameters.ContainsKey("Options"))
			{
				downoption = _searchEngine.Parameters["Options"].ToString().Replace("%id%", match.Id);
			}
			switch (selectedLabelText)
			{
			case "Browse Folders":
				// the download starts when a folder is chosen (FolderBrowser.FolderClickMenu)
				Log.Instance().Print("Loading file browser");
				MyTorrents.ListType = "FolderBrowser";
				folder = MyTorrents.Instance().ShowFolderBrowser();
				return;
			case "No change":
				if (label != null)
				{
					Log.Instance().Print("Starting torrent file downloading");
					flag = TorrentEngine.Instance().StartDownloadingToFolder(out hash, empty, label.Name, force: true, match.cookie, match.Id, downoption, "", "", empty2);
				}
				break;
			default:
				if (label != null)
				{
					Log.Instance().Print("Starting torrent file downloading");
					flag = TorrentEngine.Instance().StartDownloadingToFolder(out hash, empty, label.Name, force: true, match.cookie, match.Id, downoption, $"&download_dir={selectedId - 2}", "", empty2);
				}
				break;
			}
			// 2026: say what happened (it used to ask "watch for changes?" every time - that is in the menu now)
			if (flag) PostProcessor.Remember(hash, SearchKind, _searchString, match.Title);
			MyTorrents.Instance().AfterDownloadStarted(flag, match.Title);
		}

		private void AddWatchFromSearch(TorrentMatch match, string folder, string hash)
		{
			UrlItem urlItem = new UrlItem();
			urlItem.Name = match.Title;
			urlItem.Url = match.Url;
			urlItem.Tracker = match.tracker;
			urlItem.Id = match.Id;
			urlItem.Cookies = match.cookie;
			urlItem.LastHash = hash;
			urlItem.Folder = folder;
			urlItem.Label = label?.Name ?? "";
			urlItem.SearchTerm = _searchString;
			MyTorrents.WL.AddUrlWatch(urlItem);
		}
	}
}
