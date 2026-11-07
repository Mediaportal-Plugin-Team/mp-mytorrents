using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using MediaPortal.Dialogs;
using MediaPortal.GUI.Library;
using Sohvaperuna.RSSReader;
using Sohvaperuna.TorrentEngine;
using Sohvaperuna.TorrentSearch;

namespace Sohvaperuna.MyTorrents
{
	public class MyTorrents : GUIWindow, ISetupForm
	{
		[SkinControl(200)]
		protected GUIActionGroup HiddenMenu = null;

		[SkinControl(202)]
		protected GUIButtonControl button_SwitchView = null;

		[SkinControl(203)]
		protected GUIButtonControl button_Sort = null;

		[SkinControl(204)]
		protected GUIButtonControl button_SearchTorrents = null;

		[SkinControl(205)]
		protected GUIButtonControl button_watchItemList = null;

		[SkinControl(206)]
		protected GUIButtonControl button_RSS = null;

		[SkinControl(207)]
		protected GUIButtonControl button_Log = null;

		[SkinControl(208)]
		public GUIListControl torrentList = null;

		[SkinControl(210)]
		protected GUIButtonControl button_Labels = null;

		[SkinControl(302)]
		protected GUIButtonControl button_Quality = null;

		[SkinControl(303)]
		protected GUIButtonControl button_Source = null;

		[SkinControl(304)]
		protected GUIButtonControl button_Tracker = null;

		[SkinControl(305)]
		protected GUIButtonControl button_WatchingType = null;

		[SkinControl(306)]
		private GUICheckButton button_IncludeSpecials = null;

		[SkinControl(307)]
		protected GUIButtonControl button_Folder = null;

		[SkinControl(501)]
		protected GUICheckListControl FolderList = null;

		[SkinControl(502)]
		protected GUIListControl FileList = null;

		public static string ListType = "";

		public static string SortOrder = "";

		public static string TorrentView = "";

		public static string LabelFilter = null;

		public WatchList _torrentWatchlist = new WatchList();

		private static MyTorrents _instance = null;

		public WindowSpy _windowSpy;

		private RSSUpdater _rssUpdater;

		private IconMapping _icons = new IconMapping();

		private TorrentListView ViewTorrents = new TorrentListView();

		private RSSView ViewRSS = new RSSView();

		private SearchView ViewSearch = new SearchView();

		private FolderBrowser ViewFolders = new FolderBrowser();

		internal WatchListView ViewAutoGrabber = new WatchListView();

		private TorrentDetailsView ViewDetails = new TorrentDetailsView();

		private ConfigData _config;

		public object UpdateLocker = new object();

		public override int GetID
		{
			get
			{
				return 5678;
			}
			set
			{
			}
		}

		public override bool SupportsDelayedLoad => false;

		public string PluginName()
		{
			return "MyTorrents";
		}

		public string Description()
		{
			return "Control BiglyBT, qBittorrent, Deluge or uTorrent from MediaPortal; search sites and Jackett for torrents.";
		}

		public string Author()
		{
			return "Sohvaperuna, Edalex (2026 update: Mark Willmer)";
		}

		public void ShowPlugin()
		{
			Configuration configuration = Configuration.Instance();
			SettingsForm settingsForm = new SettingsForm(ref configuration);
			if (settingsForm.ShowDialog() == DialogResult.OK)
			{
				configuration.Save();
			}
		}

		public bool CanEnable()
		{
			return true;
		}

		public int GetWindowId()
		{
			return 5678;
		}

		public bool DefaultEnabled()
		{
			return true;
		}

		public bool HasSetup()
		{
			return true;
		}

		public bool GetHome(out string strButtonText, out string strButtonImage, out string strButtonImageFocus, out string strPictureImage)
		{
			strButtonText = Configuration.Instance().Settings["Torrent.PluginName"] as string;
			strButtonImage = string.Empty;
			strButtonImageFocus = string.Empty;
			strPictureImage = "hover_mytorrents.png";
			return true;
		}

		public override string GetModuleName()
		{
			return "MyTorrents";
		}

		private bool EstablishConnection(ConfigData _config)
		{
			bool result = TorrentEngine.Initialize(_config.Server, Convert.ToInt32(_config.Port), _config.User, _config.Password, _config.Backend);
			SetBackendBackdrop(_config.Backend);
			DisplayTorrents(new List<Torrent>());
			return result;
		}

		private bool InitWindowSpy()
		{
			_windowSpy = new WindowSpy();
			_windowSpy.Start();
			return true;
		}

		private bool LoadTorrentWatch()
		{
			return _torrentWatchlist.Load();
		}

		private bool StartRss()
		{
			_rssUpdater = new RSSUpdater();
			_rssUpdater.Start(300000L);
			return true;
		}

		private void DisplayTorrents(List<Torrent> TorrentsAll)
		{
			long num = 0L;
			int num2 = 0;
			long num3 = 0L;
			long num4 = 0L;
			double num5 = 0.0;
			int num6 = 0;
			int num7 = 0;
			foreach (Torrent item in TorrentsAll)
			{
				if (item.Started())
				{
					if (item.DownloadSpeed > 0 || item.UploadSpeed > 0)
					{
						num7++;
					}
					num += item.DownloadSpeed;
					num3 += item.UploadSpeed;
					if (item.Progress >= 100.0)
					{
						num4++;
					}
					else
					{
						num2++;
					}
				}
				if (item.Progress < 100.0)
				{
					num5 += item.Progress;
					num6++;
				}
			}
			UpdateScreen();
			GUIPropertyManager.SetProperty("#MyTorrents.IsConnected", TorrentEngine.Instance().IsConnected.ToString());
			GUIPropertyManager.SetProperty("#MyTorrents.CombinedDownloadSpeed", UnitConvert.TransferSpeedToString(num));
			GUIPropertyManager.SetProperty("#MyTorrents.CombinedUploadSpeed", UnitConvert.TransferSpeedToString(num3));
			GUIPropertyManager.SetProperty("#MyTorrents.Downloads.Count", $"{num2}");
			GUIPropertyManager.SetProperty("#MyTorrents.Uploads.Count", $"{num4}");
			GUIPropertyManager.SetProperty("#MyTorrents.Active.Count", $"{num7}");
			if (Notifier.Instance() != null && Notifier.Instance().IsNotificationBarAvailable() && _config.Notify)
			{
				Notifier.Instance().ChangeText();
			}
		}

		private void _TorrentsUpdated()
		{
			DisplayTorrents(TorrentEngine.Instance().TorrentsAll);
		}

		public void _RSSUpdated(IRSSChannel channel)
		{
			if (ListType == "RSS" || ListType == "SelectedRSS")
			{
				UpdateScreen();
			}
		}

		public void DisplayError(string ErrorHeading, string ErrorMessage)
		{
			// 2026 (1.3.42): from the Auto grabber, Wanted movies or the Trakt list (background work) only log it -
			// a pop-up drawn from a background thread showed doubled, garbled writing on the screen
			System.Threading.Thread t = System.Threading.Thread.CurrentThread;
			if (t.IsBackground || t.IsThreadPoolThread || (t.Name ?? "").StartsWith("MyTorrents Autograbber"))
			{
				Log.Instance().Print(ErrorHeading + ": " + ErrorMessage + " (in the background - no pop-up)");
				return;
			}
			GUIDialogNotify gUIDialogNotify = (GUIDialogNotify)GUIWindowManager.GetWindow(2016);
			gUIDialogNotify.Reset();
			gUIDialogNotify.SetHeading(ErrorHeading);
			gUIDialogNotify.SetText(ErrorMessage);
			gUIDialogNotify.DoModal(GUIWindowManager.ActiveWindow);
			Menus.Check(gUIDialogNotify);
		}

		private static bool _connectionHelpShown;

		/// 2026: if the torrent program cannot be reached, say what to do (e.g. after it was updated or reinstalled).
		private void CheckConnectionOnce()
		{
			if (_connectionHelpShown) return;
			System.Threading.ThreadPool.QueueUserWorkItem(_ =>
			{
				for (int i = 0; i < 10 && !TorrentEngine.Instance().IsConnected; i++)
				{
					System.Threading.Thread.Sleep(1000); // the first connection may still be on its way
				}
				if (TorrentEngine.Instance().IsConnected || _connectionHelpShown) return;
				_connectionHelpShown = true;
				string backend = (Configuration.Instance().Settings["uTorrent.Backend"] as string ?? "").ToLowerInvariant();
				string name = backend == "qbittorrent" ? "qBittorrent" : backend == "biglybt" ? "BiglyBT" : "the torrent program";
				Log.Instance().Print("Cannot reach " + name + " - showing help");
				GUIWindowManager.SendThreadCallback((p1, p2, data) =>
				{
					DisplayError("Can't reach " + name,
						"1. Make sure " + name + " is running.\n2. If you have just updated or reinstalled it: on this PC open MyTorrents Configuration (desktop icon) > Setup assistant... > Run the full setup again.");
					return 0;
				}, 0, 0, null);
			});
		}

		public string ViewSearchKind => ViewSearch?.SearchKind ?? "";

		public string ViewSearchText => ViewSearch?.SearchString ?? "";

		/// 2026: tell the user a download was sent to the torrent program, and offer to show the torrent list.
		public void AfterDownloadStarted(bool ok, string title)
		{
			if (!ok)
			{
				DisplayError("Download failed", "The torrent program did not accept it. See MyTorrents.log for details.");
				return;
			}
			Log.Instance().Print("Download started: " + title);
			GUIDialogYesNo dlg = (GUIDialogYesNo)GUIWindowManager.GetWindow(100);
			if (dlg == null)
			{
				return;
			}
			dlg.Reset();
			dlg.SetHeading("Download started");
			dlg.SetLine(1, Ellipsis(title ?? "", 60));
			dlg.SetLine(2, "");
			dlg.SetLine(3, "Show your torrents now?");
			dlg.DoModal(GUIWindowManager.ActiveWindow);
			Menus.Check(dlg);
			if (dlg.IsConfirmed)
			{
				ListType = "Torrents";
				GUIPropertyManager.SetProperty("#MyTorrents.FolderBrowser.Visible", "no");
				torrentList.Visible = true;
				UpdateScreen();
				GiveFocus(torrentList);
			}
		}

		public void UpdateScreen()
		{
			if (ListType != "SeriesInfo" && GUIPropertyManager.GetProperty("#MyTorrents.SeriesInfo") == "yes")
			{
				// 2026: a show's Auto grabber page must not stay drawn over other lists
				GUIPropertyManager.SetProperty("#MyTorrents.SeriesInfo", "no");
				UnfocusSeries();
				torrentList.Visible = true;
			}
			switch (ListType)
			{
			case "Search":
			case "WatchList":
			case "Settings":
				break;
			case "Torrents":
				ShowTorrents();
				break;
			case "RSS":
				torrentList.ListItems = ViewRSS.ShowRSS();
				break;
			case "SelectedRSS":
				torrentList.ListItems = ViewRSS.UpdateSelectedRSS();
				break;
			case "Log":
				ShowLog();
				break;
			case "TorrentDetails":
				ShowTorrentDetails();
				break;
			}
		}

		public string Ellipsis(string str, int len)
		{
			if (str.Length > len)
			{
				return str.Substring(0, len) + "...";
			}
			return str;
		}

		public void ShowTorrents()
		{
			if (torrentList == null)
			{
				return;
			}
			List<GUIListItem> list = new List<GUIListItem>();
			GUIPropertyManager.SetProperty("#MyTorrents.View", string.IsNullOrEmpty(TorrentView) ? "All" : TorrentView);
			List<Torrent> torrents = TorrentEngine.Instance().GetTorrents(TorrentView, SortOrder, LabelFilter);
			foreach (Torrent item in torrents)
			{
				GUIListItem gUIListItem = new GUIListItem($"{Ellipsis(item.Name, 75)}");
				gUIListItem.AlbumInfoTag = item;
				list.Add(gUIListItem);
				if (gUIListItem.Label2 != $"{UnitConvert.SizeToString(item.Size)} ({item.Progress:F2}%)")
				{
					gUIListItem.Label2 = $"{UnitConvert.SizeToString(item.Size)} ({item.Progress:F2}%)";
				}
			}
			torrentList.ListItems = list;
			if (torrentList.ListItems.Count == 0)
			{
				GUIPropertyManager.SetProperty("#MyTorrents.Count", "0");
				GUIListItem gUIListItem = new GUIListItem();
				gUIListItem.Label = "No items.";
				torrentList.Add(gUIListItem);
			}
			else
			{
				GUIPropertyManager.SetProperty("#MyTorrents.Count", $"{torrentList.ListItems.Count}");
			}
		}

		public void ShowTorrentDetails()
		{
			lock (UpdateLocker)
			{
				FileList.ListItems = ViewDetails.GetTorrentDetails(torrentList.SelectedListItem.AlbumInfoTag as Torrent);
			}
			torrentList.Visible = false;
			GUIPropertyManager.SetProperty("#MyTorrents.TorrentDetails.Visible", "yes");
			GiveFocus(FileList);
		}

		public void ShowSearch()
		{
			ViewSearch.StartSearch();
		}

		public string ShowFolderBrowser()
		{
			string folder = string.Empty;
			FolderList.Clear();
			FolderList.ListItems = ViewFolders.ShowFolderBrowser(ref folder);
			FolderList.Visible = true;
			GiveFocus(FolderList);
			GUIPropertyManager.SetProperty("#MyTorrents.FolderBrowser.Visible", "yes");
			return folder;
		}

		public void ShowLog()
		{
			torrentList.Clear();
			foreach (string line in Log.Instance().Lines)
			{
				GUIListItem gUIListItem = new GUIListItem();
				gUIListItem.Label = line;
				torrentList.Add(gUIListItem);
			}
		}

		public void ShowTorrentWatch()
		{
			torrentList.Clear();
			torrentList.ListItems = ViewAutoGrabber.UpdateWatches();
			if (_torrentWatchlist._watchItemList.Count == 0)
			{
				OnShowContextMenu();
			}
		}

		public void ShowSettingsDialog()
		{
			GUICheckListDialog gUICheckListDialog = (GUICheckListDialog)GUIWindowManager.GetWindow(2102);
			if (gUICheckListDialog == null)
			{
				return;
			}
			gUICheckListDialog.Reset();
			gUICheckListDialog.SetHeading("Select settings group");
			TorrentSearchEngine torrentSearchEngine = new TorrentSearchEngine(Configuration.Instance().Settings["TorrentSearch.Config"] as string);
			foreach (ITorrentSearch engine in torrentSearchEngine.Engines)
			{
				GUIListItem gUIListItem = new GUIListItem();
				gUIListItem.Label = engine.Name;
				gUIListItem.Selected = Convert.ToBoolean(engine.Enabled);
				gUICheckListDialog.Add(gUIListItem);
			}
			gUICheckListDialog.DoModal(GUIWindowManager.ActiveWindow);
			Menus.Check(gUICheckListDialog);
			torrentSearchEngine.SaveEnabledState(Configuration.Instance().Settings["TorrentSearch.Config"] as string);
		}

		public static MyTorrents Instance()
		{
			return _instance;
		}

		// 2026 (1.3.45): the work can also run in the background helper (MyTorrents Configuration.exe /helper),
		// when MediaPortal is closed - there is no MyTorrents window there
		public static WatchList HelperWatchlist;

		/// The Auto grabber list - MediaPortal's, or the background helper's.
		public static WatchList WL => _instance != null ? _instance._torrentWatchlist : HelperWatchlist;

		/// True inside MediaPortal (screens and pop-ups can be shown).
		public static bool InMediaPortal => _instance != null;

		/// A message: a pop-up in MediaPortal, the log in the background helper.
		public static void Error(string heading, string message)
		{
			if (_instance != null) _instance.DisplayError(heading, message);
			else Log.Instance().Print(heading + ": " + (message ?? "").Replace("\n", " "));
		}

		public override bool Init()
		{
			Sohvaperuna.TorrentEngine.ClientSupport.EnableModernTls();
			SkinSetup.EnsureAllSkins(); // universal screens for skins without MyTorrents screens
			_config = Configuration.Instance().LoadConfiguration();
			_instance = this;
			BackgroundPage.EnsureHelperRunning(); // 2026 (1.3.57)
			LoadTorrentWatch();
			_icons.LoadIconMappings(_config);
			ViewRSS.LoadRssChannels(_config);
			StartRss();
			InitWindowSpy();
			if (EstablishConnection(_config))
			{
				Log.Instance().Print("connection established");
			}
			else
			{
				Log.Instance().Print("connection is not established");
			}
			if (_config.Notify)
			{
				Notifier.Initialize();
			}
			PostProcessor.Start(); // 2026: copy finished downloads into the library
			SaveFolder.Refresh();
			UpdateReminder.Start(); // 2026: monthly, in the background
			base.Init();
			return Load(Path.Combine(GUIGraphicsContext.Skin, "MP-MyTorrents.xml"));
		}

		public override void DeInit()
		{
			_torrentWatchlist.Save();
		}

		protected override void OnPageDestroy(int new_windowId)
		{
			TorrentEngine.Instance().TorrentsUpdated -= _TorrentsUpdated;
			base.OnPageDestroy(new_windowId);
		}

		protected override void OnPageLoad()
		{
			SaveFolder.Refresh(); // the folder may have been changed in the torrent program
			CheckConnectionOnce();
			UpdateReminder.ShowIfDue();
			TooBig.AskIfDue();
			if (GUIWindowManager.GetPreviousActiveWindow() == 803)
			{
				Configuration.Instance().ClearSettings();
				_config = Configuration.Instance().LoadConfiguration();
				EstablishConnection(_config);
			}
			if (!TorrentEngine.Instance().IsConnected)
			{
				Log.Instance().Print("IsConnected = false");
				DisplayError("Unable to connect", "Connection error. Torrent functionality disabled.");
			}
			ListType = "Torrents";
			TorrentView = Configuration.Instance().Settings["TorrentView"] as string;
			SortOrder = Configuration.Instance().Settings["SortOrder"] as string;
			FolderList.Visible = false;
			TorrentEngine.Instance().TorrentsUpdated += _TorrentsUpdated;
			UpdateScreen();
			GUIPropertyManager.SetProperty("#MyTorrents.SeriesInfo", "no");
			GUIPropertyManager.SetProperty("#MyTorrents.TorrentDetails.Visible", "no");
			GUIPropertyManager.SetProperty("#MyTorrents.FolderBrowser.Visible", "no");
			GUIPropertyManager.SetProperty("#currentmodule", Configuration.Instance().Settings["Torrent.PluginName"] as string);
			GUIPropertyManager.SetProperty("#MyTorrents.Backdrop", "");
			base.OnPageLoad();
			if (_loadParameter != null)
			{
				string param = _loadParameter;
				// 2026: from a show in My TV Series (not an episode) - ask what to do instead of listing everything
				int fromWindow = GUIWindowManager.GetPreviousActiveWindow();
				Log.Instance().Print($"Opened with '{param}' from window {fromWindow}");
				// 2026 (1.3.40): the "Torrent Search" button in the left menu of TV Series / Moving Pictures
				if (param.StartsWith("mytorrents:", StringComparison.OrdinalIgnoreCase))
				{
					string what = param.Substring("mytorrents:".Length).Trim().ToLowerInvariant();
					GUIWindowManager.SendThreadCallback((p1, p2, data) =>
					{
						try
						{
							LeftMenuSearch(what);
						}
						catch (Exception ex)
						{
							Log.Instance().Print("Torrent Search button: " + ex.Message);
						}
						return 0;
					}, 0, 0, null);
					return;
				}
				if (fromWindow == 9811 && !SearchContext.LooksLikeEpisode(param) && _torrentWatchlist.IsMPTVSeriesAvailable())
				{
					GUIWindowManager.SendThreadCallback((p1, p2, data) =>
					{
						try
						{
							if (!ViewAutoGrabber.FromTVSeries(param)) SearchFor(param, SearchKindFromCaller(param));
						}
						catch (Exception ex)
						{
							Log.Instance().Print("From My TV Series: " + ex.Message);
							SearchFor(param, SearchContext.TV);
						}
						return 0;
					}, 0, 0, null);
				}
				else
				{
					SearchFor(param, SearchKindFromCaller(param));
				}
			}
		}

		/// 2026 (1.3.40): "tv" - search for the highlighted show, or add a new series; "film" - search for the
		/// highlighted film, or another one. Back from the choice goes back where you came from.
		private void LeftMenuSearch(string what)
		{
			// 2026 (1.3.42): the Torrent Search button on the Trakt plugin's own screens
			if (what.StartsWith("trakt"))
			{
				if (!Extras.FromTraktScreen(what)) GUIWindowManager.ShowPreviousWindow();
				return;
			}
			GUIDialogMenu menu = (GUIDialogMenu)GUIWindowManager.GetWindow(2012);
			if (menu == null) return;
			menu.Reset();
			if (what == "film")
			{
				string title = GUIPropertyManager.GetProperty("#MovingPictures.SelectedMovie.title") ?? "";
				string year = GUIPropertyManager.GetProperty("#MovingPictures.SelectedMovie.year") ?? "";
				if (title.StartsWith("#")) title = "";
				if (year.StartsWith("#") || year == "0") year = "";
				string film = (title + " " + year).Trim();
				menu.SetHeading("Torrent Search - movies (" + Films.Quality + ", up to " + Films.SizeText(Films.MaxSizeMB) + ")");
				if (film.Length > 0) menu.Add("Search for " + film);
				menu.Add("Search for another movie...");
				menu.Add("Add a movie to Wanted movies...");
				menu.Add("Wanted movies list...");
				menu.Add("Trakt: recommended, watchlist, similar...");
				menu.DoModal(GUIWindowManager.ActiveWindow);
				Menus.Check(menu);
				string pick = menu.SelectedLabelText ?? "";
				if (pick.StartsWith("Add a movie to Wanted")) { Extras.AddFilm(null); GUIWindowManager.ShowPreviousWindow(); return; }
				if (pick.StartsWith("Wanted movies list")) { Extras.WantedFilmsMenu(); if (ListType != "Search") GUIWindowManager.ShowPreviousWindow(); return; }
				if (pick.StartsWith("Trakt:")) { Extras.TraktLists(title.Length > 0 ? film : null, null); if (ListType != "Search") GUIWindowManager.ShowPreviousWindow(); return; }
				if (pick.StartsWith("Search for another"))
				{
					string typed = Keyboard("");
					if (typed.Length > 0) SearchFor(typed, SearchContext.Movie);
					else GUIWindowManager.ShowPreviousWindow();
				}
				else if (pick.StartsWith("Search for ")) SearchFor(film, SearchContext.Movie);
				else GUIWindowManager.ShowPreviousWindow();
				return;
			}
			string show = GUIPropertyManager.GetProperty("#TVSeries.Series.Pretty_Name") ?? "";
			if (show.StartsWith("#")) show = "";
			menu.SetHeading("Torrent Search - TV");
			if (show.Length > 0) menu.Add("Search for " + show);
			menu.Add("Add a new series...");
			menu.Add("What's missing in my library...");
			menu.Add("Trakt: recommended, watchlist, similar...");
			menu.DoModal(GUIWindowManager.ActiveWindow);
			Menus.Check(menu);
			string chosen = menu.SelectedLabelText ?? "";
			if (chosen.StartsWith("What's missing")) { ListType = "WatchList"; ShowTorrentWatch(); Extras.MissingReport(); return; }
			if (chosen.StartsWith("Trakt:")) { ListType = "WatchList"; ShowTorrentWatch(); Extras.TraktLists(null, show.Length > 0 ? show : null); return; }
			if (chosen.StartsWith("Add a new series"))
			{
				ListType = "WatchList";
				ShowTorrentWatch();
				ViewAutoGrabber.SearchSeries();
			}
			else if (chosen.StartsWith("Search for "))
			{
				if (!(_torrentWatchlist.IsMPTVSeriesAvailable() && ViewAutoGrabber.FromTVSeries(show))) SearchFor(show, SearchContext.TV);
			}
			else GUIWindowManager.ShowPreviousWindow();
		}

		private static string Keyboard(string text)
		{
			VirtualKeyboard keyboard = (VirtualKeyboard)GUIWindowManager.GetWindow(1002);
			if (keyboard == null) return "";
			keyboard.Reset();
			keyboard.Text = text ?? "";
			keyboard.DoModal(GUIWindowManager.ActiveWindow);
			Menus.Check(keyboard);
			return keyboard.IsConfirmed ? (keyboard.Text ?? "").Trim() : "";
		}

		/// Starts a search and shows its results (also used by the My TV Series menu).
		public void SearchFor(string text, string kind)
		{
			ViewSearch.StartParametricSearch(text, kind);
			ListType = "Search";
			UnfocusMenu();
			GiveFocus(torrentList);
		}

		/// 2026: decide what to search for from where the search came from.
		/// MP-TVSeries (window 9811) or an episode code like S01E02 -> TV; Moving Pictures (96742) -> movies.
		private static string SearchKindFromCaller(string searchText)
		{
			int from = GUIWindowManager.GetPreviousActiveWindow();
			string kind;
			if (from == 9811 || SearchContext.LooksLikeEpisode(searchText))
			{
				kind = SearchContext.TV;
			}
			else if (from == 96742)
			{
				kind = SearchContext.Movie;
			}
			else
			{
				kind = SearchContext.Any;
			}
			Log.Instance().Print($"Search '{searchText}' from window {from}: searching {(kind == "" ? "everything" : kind)}");
			return kind;
		}

		/// The details page of the highlighted Auto grabber show (was: OK on the show).
		public void ShowSeriesDetails()
		{
			if (!(torrentList.SelectedListItem?.AlbumInfoTag is SeriesItem))
			{
				return; // 2026: only Auto grabber shows have a details page (other items used to crash here)
			}
			ListType = "SeriesInfo";
			UnfocusMenu();
			torrentList.Visible = false;
			GiveFocus(button_WatchingType);
			ViewAutoGrabber.ShowSeries(torrentList.SelectedListItem.AlbumInfoTag);
			button_IncludeSpecials.Selected = (torrentList.SelectedListItem.AlbumInfoTag as SeriesItem).includespecials;
			button_IncludeSpecials.IsFocused = false;
		}

		protected override void OnShowContextMenu()
		{
			if (ListType == "Torrents")
			{
				Torrent uTorrent = torrentList.SelectedListItem.AlbumInfoTag as Torrent;
				ViewTorrents.TorrentsContextMenu(uTorrent);
			}
			if (ListType == "TorrentDetails")
			{
				lock (UpdateLocker)
				{
					int selectedListItemIndex = FileList.SelectedListItemIndex;
					Sohvaperuna.TorrentEngine.File file = FileList.SelectedListItem.AlbumInfoTag as Sohvaperuna.TorrentEngine.File;
					ViewDetails.TorrentDetailsContextMenu(file, selectedListItemIndex);
				}
			}
			if (ListType == "RSS")
			{
				IRSSChannel iRSSChannel = ViewRSS.RSSContextMenu(torrentList.SelectedListItem.AlbumInfoTag as IRSSChannel);
				if (iRSSChannel != null)
				{
					ListType = "SelectedRSS";
					torrentList.ListItems = ViewRSS.ShowSelectedRSS(iRSSChannel);
				}
			}
			if (ListType == "SelectedRSS")
			{
			}
			if (ListType == "Search")
			{
				ViewSearch.SearchContextMenu(torrentList.SelectedListItem.AlbumInfoTag as TorrentMatch);
			}
			if (ListType == "WatchList")
			{
				ViewAutoGrabber.ContextMenu(torrentList.SelectedListItem.AlbumInfoTag);
			}
		}

		protected override void OnClicked(int controlId, GUIControl control, MediaPortal.GUI.Library.Action.ActionType actionType)
		{
			if (!TorrentEngine.Instance().IsConnected)
			{
				base.OnClicked(controlId, control, actionType);
				return;
			}
			if (controlId > 201 && controlId < 208 && controlId == 210)
			{
				GiveFocus(torrentList);
			}
			// 2026: leaving a show's Auto grabber page from the menu used to leave the page drawn over the next list
			if (ListType == "SeriesInfo" && control.GetID >= 202 && control.GetID <= 211)
			{
				UnfocusSeries();
				torrentList.Visible = true;
				GUIPropertyManager.SetProperty("#MyTorrents.SeriesInfo", "no");
				_torrentWatchlist.Save();
				ListType = "WatchList";
			}
			switch (control.GetID)
			{
			case 202:
				UnfocusMenu();
				ViewTorrents.ViewButtonHandler();
				// 2026 (1.3.38): switching view from the details page, the folder browser or the log left the
				// torrent list hidden, so every view looked empty - show the torrent list itself
				ListType = "Torrents";
				FolderList.Visible = false;
				GUIPropertyManager.SetProperty("#MyTorrents.FolderBrowser.Visible", "no");
				GUIPropertyManager.SetProperty("#MyTorrents.TorrentDetails.Visible", "no");
				GUIPropertyManager.SetProperty("#MyTorrents.SeriesInfo", "no");
				torrentList.Visible = true;
				GiveFocus(torrentList);
				UpdateScreen();
				Log.Instance().Print($"View: {(string.IsNullOrEmpty(TorrentView) ? "All" : TorrentView)} - {TorrentEngine.Instance().GetTorrents(TorrentView, SortOrder, LabelFilter).Count} torrent(s)");
				break;
			case 203:
				UnfocusMenu();
				ViewTorrents.SortButtonHandler();
				GiveFocus(torrentList);
				UpdateScreen();
				break;
			case 204:
				ListType = "Search";
				UnfocusMenu();
				ShowSearch();
				GiveFocus(torrentList);
				break;
			case 205:
				ListType = "WatchList";
				UnfocusMenu();
				ShowTorrentWatch();
				GiveFocus(torrentList);
				break;
			case 206:
				ListType = "RSS";
				UnfocusMenu();
				UpdateScreen();
				GiveFocus(torrentList);
				RSSChannelManager.Instance().UpdateChannels(force: true);
				break;
			case 207:
				ListType = "Log";
				ShowLog();
				UnfocusMenu();
				GiveFocus(torrentList);
				break;
			case 209:
			{
				UnfocusMenu();
				GUIDialogMenu gUIDialogMenu = (GUIDialogMenu)GUIWindowManager.GetWindow(2012);
				if (gUIDialogMenu == null)
				{
					return;
				}
				gUIDialogMenu.Reset();
				gUIDialogMenu.SetHeading("Choose setting");
				gUIDialogMenu.Add("General settings");
				gUIDialogMenu.Add("Search engines settings");
				gUIDialogMenu.Add("Torrent client settings");
				gUIDialogMenu.DoModal(GUIWindowManager.ActiveWindow);
				Menus.Check(gUIDialogMenu);
				switch (gUIDialogMenu.SelectedLabelText)
				{
				case "General settings":
					GUIWindowManager.ActivateWindow(803, "4e2af33d-c719-4478-95f9-231f7ddd3e33");
					break;
				case "Search engines settings":
					ShowSettingsDialog();
					break;
				}
				break;
			}
			case 211: // 2026: "Download folder"
				UnfocusMenu();
				SaveFolder.ChangeFromMenu();
				GiveFocus(torrentList);
				break;
			case 210:
				ViewTorrents.LabelButtonHandler();
				UnfocusMenu();
				GiveFocus(torrentList);
				UpdateScreen();
				break;
			case 208:
				if (actionType != MediaPortal.GUI.Library.Action.ActionType.ACTION_SELECT_ITEM)
				{
					break;
				}
				switch (ListType)
				{
				case "Torrents":
					if (torrentList.SelectedListItem.AlbumInfoTag is Torrent)
					{
						ListType = "TorrentDetails";
						UpdateScreen();
					}
					break;
				case "Search":
					ViewSearch.SearchItemClicked(torrentList.SelectedListItem.AlbumInfoTag as TorrentMatch);
					break;
				case "SelectedRSS":
					ViewRSS.RSSDownload(torrentList.SelectedListItem.AlbumInfoTag as RSSItem);
					break;
				case "RSS":
					ListType = "SelectedRSS";
					ViewRSS.ShowSelectedRSS(torrentList.SelectedListItem.AlbumInfoTag as IRSSChannel);
					UpdateScreen();
					break;
				case "WatchList":
					// 2026 (1.3.42): OK / left-click on an Auto grabber item opens its menu (Show details, Search again,
					// What's missing...) - right-click is "back" in MediaPortal, so the menu was hard to find
					ViewAutoGrabber.ContextMenu(torrentList.SelectedListItem?.AlbumInfoTag);
					break;
				}
				break;
			case 501:
				if (actionType == MediaPortal.GUI.Library.Action.ActionType.ACTION_SELECT_ITEM)
				{
					if (!FolderList.SelectedListItem.Selected)
					{
						FolderList.ListItems = ViewFolders.GetGUIFolders(FolderList.SelectedListItem);
						FolderList.SelectedListItemIndex = 0;
					}
					else
					{
						ViewFolders.FolderClickMenu(FolderList.SelectedListItem, torrentList.SelectedListItem, ViewSearch.label);
					}
				}
				break;
			case 502:
				if (ListType == "TorrentDetails")
				{
					lock (UpdateLocker)
					{
						int selectedListItemIndex = FileList.SelectedListItemIndex;
						Sohvaperuna.TorrentEngine.File file = FileList.SelectedListItem.AlbumInfoTag as Sohvaperuna.TorrentEngine.File;
						ViewDetails.TorrentDetailsOnClick(file, selectedListItemIndex);
					}
				}
				break;
			case 302:
			case 303:
			case 304:
			case 305:
			case 306:
			case 307:
			case 308:
				ViewAutoGrabber.OnClicked(control);
				break;
			}
			base.OnClicked(controlId, control, actionType);
		}

		public void GiveFocus(GUIControl control)
		{
			torrentList.Focus = false;
			FolderList.Focus = false;
			FileList.Focus = false;
			control.Focus = true;
		}

		public void HideFolderBrowser()
		{
			if (ListType == "SeriesFolderBrowser")
			{
				ListType = "SeriesInfo";
				GUIPropertyManager.SetProperty("#MyTorrents.SeriesInfo", "yes");
			}
			else if (ListType == "FolderBrowser")
			{
				ListType = "Search";
				GiveFocus(torrentList);
			}
			FolderList.Visible = false;
			GUIPropertyManager.SetProperty("#MyTorrents.FolderBrowser.Visible", "no");
		}

		public override void OnAction(MediaPortal.GUI.Library.Action action)
		{
			if (action.wID == MediaPortal.GUI.Library.Action.ActionType.ACTION_MOVE_LEFT || action.wID == MediaPortal.GUI.Library.Action.ActionType.ACTION_MOVE_RIGHT)
			{
				// 2026: only when the highlight is NOT in the side menu - clearing it there left nothing highlighted,
				// so the remote's arrows did nothing until the mouse was used
				int focused = GetFocusControlId();
				if (focused < 202 || focused > 211)
				{
					UnfocusMenu();
				}
			}
			if (ListType == "FolderBrowser" || ListType == "SeriesFolderBrowser")
			{
				switch (action.wID)
				{
				case MediaPortal.GUI.Library.Action.ActionType.ACTION_MOVE_RIGHT:
					FolderList.SelectedListItem.Selected = true;
					break;
				case MediaPortal.GUI.Library.Action.ActionType.ACTION_MOVE_LEFT:
					FolderList.SelectedListItem.Selected = false;
					break;
				case MediaPortal.GUI.Library.Action.ActionType.ACTION_SELECT_ITEM:
					OnClicked(501, FolderList, MediaPortal.GUI.Library.Action.ActionType.ACTION_SELECT_ITEM);
					break;
				case MediaPortal.GUI.Library.Action.ActionType.ACTION_PREVIOUS_MENU:
					HideFolderBrowser();
					break;
				default:
					base.OnAction(action);
					break;
				}
			}
			else if (action.wID == MediaPortal.GUI.Library.Action.ActionType.ACTION_PREVIOUS_MENU)
			{
				switch (ListType)
				{
				case "SelectedRSS":
					ListType = "RSS";
					UpdateScreen();
					break;
				case "FolderBrowser":
					ListType = "Search";
					GiveFocus(torrentList);
					GUIPropertyManager.SetProperty("#MyTorrents.FolderBrowser.Visible", "no");
					break;
				case "TorrentDetails":
					ListType = "Torrents";
					torrentList.Visible = true;
					GiveFocus(torrentList);
					GUIPropertyManager.SetProperty("#MyTorrents.TorrentDetails.Visible", "no");
					break;
				case "SeriesInfo":
					ListType = "WatchList";
					UnfocusSeries();
					torrentList.Visible = true;
					GiveFocus(torrentList);
					GUIPropertyManager.SetProperty("#MyTorrents.SeriesInfo", "no");
					_torrentWatchlist.Save();
					break;
				case "Torrents":
					base.OnAction(action);
					break;
				default:
					ListType = "Torrents";
					GUIPropertyManager.SetProperty("#MyTorrents.TorrentDetails.Visible", "no");
					GUIPropertyManager.SetProperty("#MyTorrents.FolderBrowser.Visible", "no");
					UpdateScreen();
					break;
				}
			}
			else
			{
				if ((!(ListType != "SeriesFolderBrowser") && !(ListType != "SeriesInfo") && !(ListType != "WatchList")) || GUIWaitCursor.ElapsedEventRunning())
				{
				}
				base.OnAction(action);
			}
		}

		public void UnfocusMenu()
		{
			// 2026: works with any skin layout (menu buttons 202-210 anywhere on the screen)
			foreach (GUIControl item in controlList.Where((GUIControl e) => e != null && e.GetID >= 202 && e.GetID <= 210))
			{
				item.IsFocused = false;
			}
			if (HiddenMenu == null)
			{
				return;
			}
			foreach (GUIGroup group in HiddenMenu.Children.OfType<GUIGroup>())
			{
				foreach (GUIControl item in group.Children.Where((GUIControl e) => e.Type == "button"))
				{
					item.IsFocused = false;
				}
			}
		}

		/// 2026: back from a show's page to the Auto grabber list
		public void CloseSeriesInfo()
		{
			ListType = "WatchList";
			UnfocusSeries();
			GUIPropertyManager.SetProperty("#MyTorrents.SeriesInfo", "no");
			torrentList.Visible = true;
			_torrentWatchlist.Save();
			ShowTorrentWatch();
			GiveFocus(torrentList);
		}

		public void UnfocusSeries()
		{
			List<GUIControl> list = (from y in GUIWindowManager.GetWindow(5678).controlList
				where y.Type == "button"
				where y.GetID > 301 && y.GetID < 400
				select y).ToList();
			foreach (GUIControl item in list)
			{
				item.IsFocused = false;
			}
		}

		private void SetBackendBackdrop(Backend backend)
		{
			string tagvalue = string.Empty;
			switch (backend)
			{
			case Backend.Azureus:
				tagvalue = "mytorrents_azureus.jpg";
				break;
			case Backend.Deluge:
				tagvalue = "mytorrents_deluge.jpg";
				break;
			case Backend.uTorrent:
				tagvalue = "mytorrents_utorrent.png";
				break;
			case Backend.qBittorrent:
				tagvalue = "mytorrents_qbittorrent.png";
				break;
			case Backend.BiglyBT:
				tagvalue = "mytorrents_biglybt.png";
				break;
			}
			GUIPropertyManager.SetProperty("#MyTorrents.Backdrop", tagvalue);
		}
	}
}
