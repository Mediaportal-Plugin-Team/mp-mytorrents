using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Timers;
using MediaPortal.Configuration;
using MediaPortal.Database;
using MediaPortal.GUI.Library;
using Sohvaperuna.RSSReader;
using SQLite.NET;
using WindowPlugins.GUITVSeries;

namespace Sohvaperuna.MyTorrents
{
	public class WatchList
	{
		private SQLiteClient _sqlClient;

		public List<Series> allseries = new List<Series>();

		/// 2026 (1.3.45): the background helper pauses while MediaPortal runs (only one of them does the work).
		public static volatile bool Paused;

		public BackgroundWorker MPTVSeriesWorker = new BackgroundWorker();

		public List<WatchItem> _watchItemList { get; set; }

		public WatchList()
		{
			_watchItemList = new List<WatchItem>();
			_sqlClient = new SQLiteClient(Config.GetFile(Config.Dir.Database, "MyTorrents.db3"));
			DatabaseUtility.SetPragmas(_sqlClient);
			if (!DatabaseUtility.TableExists(_sqlClient, "WatchList"))
			{
				SQLiteResultSet sQLiteResultSet = _sqlClient.Execute("CREATE TABLE WatchList (Search CHAR(256), Added BIGINT, Label CHAR(256) );");
			}
			if (!DatabaseUtility.TableExists(_sqlClient, "SeriesWatchList"))
			{
				SQLiteResultSet sQLiteResultSet = _sqlClient.Execute("CREATE TABLE SeriesWatchList (SeriesName CHAR(256), OriginalSeriesName CHAR(256), SeriesID CHAR(256), Added BIGINT, Quality CHAR(256), Tracker CHAR(256), Type CHAR(256), Source CHAR(256), Folder CHAR(256), IncludeSpecials CHAR (256));");
			}
			if (!DatabaseUtility.TableExists(_sqlClient, "UrlWatchList"))
			{
				SQLiteResultSet sQLiteResultSet = _sqlClient.Execute("CREATE TABLE UrlWatchList (Name CHAR(256), Url CHAR(256), Added BIGINT, Tracker CHAR(256), LastHash CHAR(256), Folder CHAR(256));");
			}
			// 2026: columns the "watch this torrent" items need but the old table never had
			// 2026 (1.3.56): only add the ones that are missing - asking for one that is there wrote
			// "duplicate column name" into the error logs every time MediaPortal or Configuration started
			HashSet<string> have = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			try
			{
				SQLiteResultSet cols = _sqlClient.Execute("PRAGMA table_info(UrlWatchList);");
				for (int i = 0; i < cols.Rows.Count; i++) have.Add(DatabaseUtility.Get(cols, i, "name"));
			}
			catch
			{
			}
			foreach (string column in new[] { "SearchTerm", "Id", "Cookies", "Label" })
			{
				if (have.Count > 0 && have.Contains(column)) continue;
				try
				{
					_sqlClient.Execute("ALTER TABLE UrlWatchList ADD COLUMN " + column + " CHAR(256);");
				}
				catch
				{
					// already there
				}
			}
			RSSChannelManager rSSChannelManager = RSSChannelManager.Instance();
			rSSChannelManager.RSSChannelUpdated += channelManager_RSSChannelUpdated;
			System.Timers.Timer timer = new System.Timers.Timer(300000.0);
			timer.Elapsed += SearchTimer_Elapsed;
			timer.Start();
			// 2026: every 20 seconds - downloads chosen in MyTorrents Configuration, copies removed with "look for another"
			Grabbed.LookAgainSoon += key =>
			{
				string showId = key.Split('|')[0];
				string episode = key.Contains("|") ? key.Substring(key.IndexOf('|') + 1) : "";
				foreach (SeriesItem s in GetSeriesList().Where(x => x.show != null && x.show.ID == showId))
				{
					// 2026 (1.3.42): the episode is wanted again straight away (not only after the next hourly re-read)
					if (System.Text.RegularExpressions.Regex.IsMatch(episode, @"^S\d+E\d+$"))
					{
						List<string> eps = (s.show.AllUnlocal ?? "").Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries).ToList();
						if (!eps.Contains(episode))
						{
							eps.Add(episode);
							s.show.AllUnlocal = string.Join("|", eps.OrderBy(x => x)) + "|";
						}
						if (string.IsNullOrEmpty(s.show.FirstUnlocal) || string.CompareOrdinal(episode, s.show.FirstUnlocal) < 0)
						{
							s.show.FirstUnlocal = episode;
							s.show.NextAirsUtc = s.show.AirsOf(episode);
						}
					}
					s.LastCheck = DateTime.Now.AddMinutes(-31.0);
					s.NextCheck = DateTime.MinValue;
				}
			};
			System.Timers.Timer quick = new System.Timers.Timer(20000.0);
			quick.Elapsed += (s, e) =>
			{
				if (Paused) return;
				try
				{
					Grabbed.TakeRetries();
					GrabRequests.Process(this);
				}
				catch (Exception ex)
				{
					Log.Instance().Print("Auto grabber: " + ex.Message);
				}
			};
			quick.Start();
			// 2026: always start - the worker waits for MP-TVSeries itself (it may load after MyTorrents)
			{
				MPTVSeriesWorker.DoWork += MPTVSeriesWorker_DoWork;
				MPTVSeriesWorker.RunWorkerCompleted += MPTVSeriesWorker_Completed;
				MPTVSeriesWorker.RunWorkerAsync();
			}
		}

		/// 2026 (1.3.45): re-read the shows from MP-TVSeries at the next check (the background helper, after MediaPortal closed).
		public void PullSeriesSoon()
		{
			_lastSeriesPull = DateTime.MinValue;
		}

		public List<SeriesItem> GetSeriesList()
		{
			return (from w in _watchItemList
				where w is SeriesItem
				select w as SeriesItem).ToList();
		}

		private void channelManager_RSSChannelUpdated(IRSSChannel channel)
		{
			foreach (WatchItem watchItem in _watchItemList)
			{
				watchItem.LastCheck = DateTime.Now;
				foreach (RSSItem item in channel.Items)
				{
					Regex regex = new Regex(watchItem.SearchTerm, RegexOptions.IgnoreCase);
					if (regex.IsMatch(item.Title))
					{
						Log.Instance().Print($"[{watchItem.SearchTerm}] matches to [{item.Title}], downloading.");
						DownloadTorrent(item, watchItem.Label);
					}
				}
			}
		}

		private void DownloadTorrent(RSSItem rssItem, string label)
		{
			string hash = string.Empty;
			if (!TorrentEngine.Instance().StartDownloadingToFolder(out hash, rssItem.Link, label, force: true, "", "0", "", "", "", "torrent"))
			{
				Log.Instance().Print($"Could not start downloading {rssItem.Link}.");
			}
		}

		private void SearchTimer_Elapsed(object sender, ElapsedEventArgs e)
		{
			if (Paused) return;
			try
			{
				Grabbed.CheckForDeadCopies(); // 2026: copies that never started are replaced
				WantedFilms.CheckDead(); // 2026 (1.3.50): and the wanted movies' ones
			}
			catch (Exception ex)
			{
				Log.Instance().Print("Auto grabber: dead copy check: " + ex.Message);
			}
			SearchTorrent();
		}

		private DateTime _lastSeriesPull = DateTime.Now;

		public void SearchTorrent()
		{
			// 2026: re-read MP-TVSeries' episode lists every hour, so newly listed episodes are noticed without a restart
			if (DateTime.Now - _lastSeriesPull > TimeSpan.FromHours(1) && _watchItemList.OfType<SeriesItem>().Any())
			{
				_lastSeriesPull = DateTime.Now;
				try
				{
					GetSeries();
					MatchShows();
					AddRunningShows();
				}
				catch (Exception ex)
				{
					Log.Instance().Print("Auto grabber: could not re-read MP-TVSeries: " + ex.Message);
				}
			}
			try
			{
				AutoGrabberStatus.Write(GetSeriesList()); // 2026: for MyTorrents Configuration's Shows tab
				Extras.WriteReport(allseries); // 2026 (1.3.41): the missing-episodes report
				// 2026 (1.3.42): Can't find list - drop episodes you now have
				List<SeriesItem> grabber = GetSeriesList();
				if (grabber.Any(s => s.show != null && !string.IsNullOrEmpty(s.show.AllUnlocal))) CantFind.Prune((id, ep) => grabber.Any(s => s.show != null && s.show.ID == id && ("|" + (s.show.AllUnlocal ?? "")).Contains("|" + ep + "|")));
			}
			catch
			{
			}
			try
			{
				LibraryFolders.WarmMovies(); // 2026 (1.3.42): the movies on the NAS, read in the background
				TraktList.Check(); // 2026 (1.3.42): your own Trakt list (MCM), every few hours
				SoundUpgrades.AutoAdd(); // 2026 (1.3.46): a few stereo movies a day, when turned on
				NightScan.Check(); // 2026 (1.3.47): one quiet MP-TVSeries folder scan at night
				CheckUp.Daily(); // 2026 (1.3.49): the daily check-up, in the background
				WantedFilms.Check(); // 2026 (1.3.41): a few wanted films at a time
			}
			catch (Exception ex)
			{
				Log.Instance().Print("Wanted movies: " + ex.Message);
			}
			foreach (WatchItem watchItem in _watchItemList.ToList())
			{
				if (watchItem is SeriesItem)
				{
					// 2026: was "> DateTime.Now", which stopped all checks 30 minutes after MediaPortal started
					if (watchItem.LastCheck.AddMinutes(30.0) < DateTime.Now && watchItem.NextCheck < DateTime.Now)
					{
						watchItem.LastCheck = DateTime.Now;
						(watchItem as SeriesItem).DoSearch();
					}
				}
				else
				{
					watchItem.DoSearch();
				}
			}
		}

		public void AddWatch(WatchItem watch)
		{
			watch.Added = DateTime.Now;
			_watchItemList.Add(watch);
		}

		public void AddSeriesWatch(SeriesItem series)
		{
			series.Added = DateTime.Now;
			series.LastCheck = DateTime.Now;
			_watchItemList.Add(series);
		}

		public void AddUrlWatch(UrlItem item)
		{
			item.Added = DateTime.Now;
			item.LastCheck = DateTime.Now;
			_watchItemList.Add(item);
		}

		/// 2026 (1.3.38): look for the due episodes now instead of at the next check - one show (id) or all (null).
		public void CheckNow(string showId, string why)
		{
			List<SeriesItem> shows = GetSeriesList().Where(s => s.show != null && (showId == null || s.show.ID == showId)).ToList();
			foreach (SeriesItem s in shows)
			{
				s.LastCheck = DateTime.Now.AddMinutes(-31.0);
				s.NextCheck = DateTime.MinValue;
				if (showId != null) s.Urgent = true; // 2026 (1.3.42): one show asked for now - doesn't wait its turn
			}
			Log.Instance().Print($"Auto grabber: check now ({(showId == null ? "all " + shows.Count + " shows" : shows.FirstOrDefault()?.show.Name ?? showId)}) - {why}");
			System.Threading.ThreadPool.QueueUserWorkItem(_ =>
			{
				try
				{
					SearchTorrent();
				}
				catch (Exception ex)
				{
					Log.Instance().Print("Auto grabber: check now: " + ex.Message);
				}
			});
		}

		/// 2026 (1.3.38): remove a show asked for in MyTorrents Configuration.
		public void RemoveById(string showId, string name)
		{
			SeriesItem s = GetSeriesList().FirstOrDefault(x => x.show != null && x.show.ID == showId);
			if (s == null)
			{
				Log.Instance().Print("Auto grabber: " + name + " was not in the Auto grabber");
				return;
			}
			RemoveWatch(s);
			Save();
			Log.Instance().Print("Auto grabber: removed " + name + " (asked for in MyTorrents Configuration) - it is not added again by itself");
		}

		/// 2026 (1.3.38): a removed show may be added again - read your shows again soon so it comes back.
		public void PutBack(string showId, string name)
		{
			AutoAdd.Unskip(showId);
			_lastSeriesPull = DateTime.MinValue;
			Log.Instance().Print("Auto grabber: " + name + " put back - added again at the next check if it is still running");
		}

		public void RemoveWatch(WatchItem watch)
		{
			_watchItemList.Remove(watch);
			if ((object)watch.GetType() == typeof(SeriesItem))
			{
				AutoAdd.Skip((SeriesItem)watch); // 2026: removed by you - not added again by itself
				_watchItemList.Remove((SeriesItem)watch);
			}
			if ((object)watch.GetType() == typeof(UrlItem))
			{
				_watchItemList.Remove((UrlItem)watch);
			}
		}

		public void Clear()
		{
			foreach (SeriesItem s in _watchItemList.OfType<SeriesItem>().ToList()) AutoAdd.Skip(s);
			_watchItemList.Clear();
		}

		/// Reads MP-TVSeries' shows (never throws). Returns a message for the user when it went wrong.
		public string ReadShows()
		{
			try
			{
				GetSeries();
				MatchShows();
				_lastSeriesPull = DateTime.Now;
				AddRunningShows();
				return null;
			}
			catch (Exception ex)
			{
				Log.Instance().Print("Auto grabber: could not read MP-TVSeries: " + ex);
				return ex.Message;
			}
		}

		/// "3x21" for every episode a file on disk covers (both parts of a double-episode file)
		private static HashSet<string> FilesOnDisk(string seriesId, out string oneFile)
		{
			HashSet<string> set = new HashSet<string>();
			oneFile = "";
			try
			{
				int id;
				if (!int.TryParse(seriesId, out id)) return set;
				SQLite.NET.SQLiteResultSet r = DBTVSeries.Execute("select SeasonIndex, EpisodeIndex, EpisodeIndex2, EpisodeFilename from local_episodes where SeriesID = " + id);
				foreach (SQLite.NET.SQLiteResultSet.Row row in r.Rows)
				{
					int season, ep, ep2;
					if (oneFile.Length == 0 && row.fields.Count > 3 && !string.IsNullOrEmpty(row.fields[3])) oneFile = row.fields[3];
					if (!int.TryParse(row.fields[0], out season) || !int.TryParse(row.fields[1], out ep)) continue;
					set.Add(season + "x" + ep);
					if (int.TryParse(row.fields[2], out ep2) && ep2 > 0)
					{
						for (int i = Math.Min(ep, ep2); i <= Math.Max(ep, ep2) && i - Math.Min(ep, ep2) < 6; i++) set.Add(season + "x" + i);
					}
				}
			}
			catch (Exception ex)
			{
				Log.Instance().Print("Auto grabber: could not read MP-TVSeries file list: " + ex.Message);
			}
			return set;
		}

		/// 2026: one MP-TVSeries show as MyTorrents needs it (null = hidden / not a library show)
		private Series BuildSeries(DBOnlineSeries item)
		{
				DBSeries correspondingSeries = Helper.getCorrespondingSeries(item["ID"]);
				if (correspondingSeries == null)
				{
					return null;
				}
				string text = correspondingSeries["Hidden"];
				if (!(text != "1"))
				{
					return null;
				}
				Series series = new Series();
				series.ID = item["ID"];
				series.Name = item["Pretty_Name"];
				series.OriginalName = item["origName"];
				series.Description = item["Summary"];
				series.Rating = item["Rating"];
				try
				{
					series.Status = item["Status"];
				}
				catch
				{
					series.Status = "";
				}
				series.Genres = item["Genre"];
				series.Poster = Config.GetFolder(Config.Dir.Thumbs) + "\\MPTVSeriesBanners\\" + item["CurrentBannerFileName"].ToString().Replace("/", "\\");
				DBEpisode dBEpisode = null;
				DBEpisode dBEpisode2 = null;
				DBSeries correspondingSeries2 = Helper.getCorrespondingSeries(item["ID"]);
				SQLCondition sQLCondition = new SQLCondition();
				sQLCondition.Add(new DBOnlineEpisode(), "SeriesID", item["ID"], SQLConditionType.Equal);
				List<DBEpisode> list = DBEpisode.Get(sQLCondition, includeStdCond: false);
				list.Sort();
				// 2026: placeholder episodes ("S03E99", no air date) are not looked for
				if (SeriesItem.SkipPlaceholders)
				{
					list = list.Where(e => !SeriesItem.IsPlaceholder((int)e["EpisodeIndex"], e["FirstAired"])).ToList();
				}
				// 2026: episodes on disk, read from the file list itself - a double-episode file ("S03E21-E22") covers
				// both episodes even when MP-TVSeries linked it to only one of them
				string oneFile;
				HashSet<string> onDisk = FilesOnDisk(item["ID"], out oneFile);
				series.OneFile = oneFile;
				try
				{
					foreach (IGrouping<int, DBEpisode> g in list.GroupBy(e => (int)e["SeasonIndex"]))
					{
						series.SeasonSizes[g.Key] = g.Count();
						DateTime last = DateTime.MinValue;
						foreach (DBEpisode e in g)
						{
							DateTime d;
							if (!DateTime.TryParseExact((string)e["FirstAired"] ?? "", "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out d) || d.Date >= DateTime.Today) { last = DateTime.MaxValue; break; }
							if (d > last) last = d;
						}
						series.SeasonLastAired[g.Key] = last;
					}
				}
				catch
				{
				}
				Func<DBEpisode, bool> have = e2 => e2.IsAvailableLocally || onDisk.Contains((int)e2["SeasonIndex"] + "x" + (int)e2["EpisodeIndex"]);
				List<DBEpisode> list2 = list.Where((DBEpisode e) => !have(e) && e["Watched"] != (DBValue)"1").ToList();
				StringBuilder stringBuilder = new StringBuilder();
				series.AllUnlocal = "";
				foreach (DBEpisode item2 in list2)
				{
					string code = "S" + ((int)item2["SeasonIndex"]).ToString("00") + "E" + ((int)item2["EpisodeIndex"]).ToString("00");
					series.AllUnlocal += code + "|";
					try { series.AirsByCode[code] = Series.AirsUtc(item2["FirstAired"], item["AirsTime"]); } catch { }
					// 2026: aired ones, for "search a missing episode" from MP-TVSeries
					string aired = item2["FirstAired"];
					DateTime airedOn;
					if ((int)item2["SeasonIndex"] > 0 && DateTime.TryParseExact(aired ?? "", "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out airedOn) && airedOn.Date < DateTime.Today)
					{
						series.AiredMissing.Add(code + "|" + aired);
					}
				}
				IEnumerable<DBEpisode> source = list.Where((DBEpisode e) => have(e) || e["Watched"] == (DBValue)"1");
				try
				{
					dBEpisode = source.Last();
				}
				catch
				{
					dBEpisode = null;
				}
				try
				{
					int index = list.IndexOf(dBEpisode) + 1;
					dBEpisode2 = list[index];
					series.FirstUnlocal = "S" + ((int)dBEpisode2["SeasonIndex"]).ToString("00") + "E" + ((int)dBEpisode2["EpisodeIndex"]).ToString("00");
					try
					{
						series.NextAirsUtc = Series.AirsUtc(dBEpisode2["FirstAired"], item["AirsTime"]);
					}
					catch
					{
						series.NextAirsUtc = DateTime.MinValue;
					}
				}
				catch (Exception)
				{
					dBEpisode2 = null;
					series.FirstUnlocal = "";
				}
			return series;
		}

		/// 2026: reads just one show from MP-TVSeries (by name) - quick, for when the full list is not read yet
		public Series ReadOneShow(string name)
		{
			string key = MediaNames.Key(name);
			foreach (DBOnlineSeries item in DBOnlineSeries.getAllSeries())
			{
				string n = item["Pretty_Name"], o = item["origName"];
				if (MediaNames.Key(n) != key && MediaNames.Key(o) != key) continue;
				return BuildSeries(item);
			}
			return null;
		}

		/// 2026: running / continuing shows go into the Auto grabber by themselves (see AutoAdd)
		private void AddRunningShows()
		{
			try
			{
				List<Series> add = AutoAdd.Pick(allseries, _watchItemList.OfType<SeriesItem>().ToList());
				if (add.Count == 0) return;
				int n = 0;
				foreach (Series s in add)
				{
					SeriesItem item = WatchListView.MakeItem(s, "New episodes");
					// first looks spread over 25 minutes, so the sites are not asked about every show at once
					item.LastCheck = DateTime.Now.AddMinutes(-25.0 + (n % 25));
					_watchItemList.Add(item);
					n++;
				}
				Save();
				Log.Instance().Print($"Auto add: {add.Count} running show(s) added for new episodes: " + string.Join(", ", add.Select(s => s.Name)));
			}
			catch (Exception ex)
			{
				Log.Instance().Print("Auto add: " + ex.Message);
			}
		}

		public void GetSeries()
		{
			Log.Instance().Print("Pulling series list from MP-TVSeries started");
			List<Series> fresh = new List<Series>(); // 2026: replace the list instead of adding to it
			int skipped = 0; // 2026: one show with odd data no longer stops all the others
			List<DBOnlineSeries> allSeries = DBOnlineSeries.getAllSeries();
			foreach (DBOnlineSeries item in allSeries)
			{
				try
				{
					Series series = BuildSeries(item);
					if (series == null)
					{
						continue;
					}
					fresh.Add(series);
				}
				catch (Exception ex)
				{
					skipped++;
					if (skipped <= 3) Log.Instance().Print("Auto grabber: skipped show " + (item?["Pretty_Name"] ?? "?") + " - " + ex.Message);
				}
			}
			allseries = fresh;
			try
			{
				SizeRecord.StartFromLog(Config.GetFile(Config.Dir.Log, "MyTorrents.log")); // 2026: first time only
			}
			catch
			{
			}
			Log.Instance().Print("Pulling series list from MP-TVSeries finished: " + fresh.Count + " shows" + (skipped > 0 ? ", " + skipped + " skipped" : ""));
		}

		public void MPTVSeriesWorker_DoWork(object sender, DoWorkEventArgs e)
		{
			MPTVSeriesWorker = sender as BackgroundWorker;
			// 2026: wait (up to 5 minutes) until MP-TVSeries has loaded - MediaPortal may start it after MyTorrents
			for (int i = 0; i < 60 && !IsMPTVSeriesAvailable(); i++)
			{
				Thread.Sleep(5000);
			}
			if (!IsMPTVSeriesAvailable())
			{
				Log.Instance().Print("Auto grabber: MP-TVSeries is not running - TV shows cannot be added");
				return;
			}
			if (GUIWindowManager.GetWindow(9811) == null)
			{
				Thread.Sleep(30000);
			}
			ReadShows();
		}

		/// Links each Auto grabber show to MP-TVSeries' data (episodes you have / do not have). 2026: a show no longer
		/// in MP-TVSeries is skipped (it used to stop all the others being linked).
		private void MatchShows()
		{
			foreach (SeriesItem item in _watchItemList.OfType<SeriesItem>())
			{
				Series found = allseries.FirstOrDefault(y => y.ID == item.show.ID);
				if (found != null)
				{
					item.show = found;
				}
				else if (_notInLibrary.Add(DateTime.Today.ToString("yyyyMMdd") + "|" + item.show.ID)) // 2026 (1.3.56): once a day, not every pull
				{
					Log.Instance().Print($"Auto grabber: [{item.show.Name}] is not in MP-TVSeries (ID {item.show.ID}) - skipped");
				}
			}
		}

		private static readonly HashSet<string> _notInLibrary = new HashSet<string>();

		public void MPTVSeriesWorker_Completed(object sender, RunWorkerCompletedEventArgs e)
		{
		}

		public void Save()
		{
			if (DatabaseUtility.TableExists(_sqlClient, "WatchList"))
			{
				_sqlClient.Execute("DELETE FROM WatchList;");
				_sqlClient.Execute("DELETE FROM SeriesWatchList;");
				_sqlClient.Execute("DELETE FROM UrlWatchList;");
				foreach (WatchItem watchItem in _watchItemList)
				{
					if (watchItem is SeriesItem)
					{
						SeriesItem seriesItem = watchItem as SeriesItem;
						_sqlClient.Execute($"INSERT INTO SeriesWatchList (SeriesName, OriginalSeriesName, SeriesID, Added, Quality, Tracker, Type, Source, Folder, IncludeSpecials) VALUES (\"{Sql(seriesItem.show.Name)}\",\"{Sql(seriesItem.show.OriginalName)}\",{seriesItem.show.ID},\"{seriesItem.Added.Ticks}\",\"{seriesItem.quality}\",\"{seriesItem.tracker}\",\"{seriesItem.type}\",\"{seriesItem.source}\",\"{Sql(seriesItem.folder)}\",\"{seriesItem.includespecials.ToString()}\");");
					}
					// 2026: only plain search watches go here; every Auto grabber show used to be written here too,
					// which left empty entries in the list
					if (watchItem != null && watchItem.GetType() == typeof(WatchItem) && !string.IsNullOrEmpty(watchItem.SearchTerm))
					{
						_sqlClient.Execute($"INSERT INTO WatchList (Search, Added, Label) VALUES (\"{watchItem.SearchTerm}\", {watchItem.Added.Ticks}, \"{watchItem.Label}\");");
					}
					if (watchItem is UrlItem)
					{
						UrlItem urlItem = watchItem as UrlItem;
						_sqlClient.Execute($"INSERT INTO UrlWatchList (Name, Url, Added, Tracker, LastHash, Folder, SearchTerm, Id, Cookies, Label) VALUES (\"{Sql(urlItem.Name)}\", \"{Sql(urlItem.Url)}\", {urlItem.Added.Ticks}, \"{Sql(urlItem.Tracker)}\", \"{Sql(urlItem.LastHash)}\", \"{Sql(urlItem.Folder)}\", \"{Sql(urlItem.SearchTerm)}\", \"{Sql(urlItem.Id)}\", \"{Sql(urlItem.Cookies)}\", \"{Sql(urlItem.Label)}\");");
					}
				}
			}
			// 2026: the connection stays open - closing it here made every later save in the session do nothing
		}

		private static string Sql(string s)
		{
			return (s ?? "").Replace("\"", "\"\"");
		}

		private static string Field(SQLiteResultSet rs, SQLiteResultSet.Row row, string column)
		{
			object i = rs.ColumnIndices[column];
			return i == null ? "" : (row.fields[(int)i] ?? "");
		}

		public bool Load()
		{
			_watchItemList.Clear();
			if (DatabaseUtility.TableExists(_sqlClient, "WatchList"))
			{
				SQLiteResultSet sQLiteResultSet = _sqlClient.Execute("SELECT * FROM WatchList;");
				foreach (SQLiteResultSet.Row row in sQLiteResultSet.Rows)
				{
					WatchItem watchItem = new WatchItem();
					watchItem.SearchTerm = row.fields[(int)sQLiteResultSet.ColumnIndices["Search"]];
					if (string.IsNullOrEmpty(watchItem.SearchTerm)) continue; // 2026: empty leftovers from the old save bug
					watchItem.Added = new DateTime(Convert.ToInt64(row.fields[(int)sQLiteResultSet.ColumnIndices["Added"]]));
					watchItem.Label = row.fields[(int)sQLiteResultSet.ColumnIndices["Label"]];
					_watchItemList.Add(watchItem);
				}
			}
			if (DatabaseUtility.TableExists(_sqlClient, "SeriesWatchList"))
			{
				SQLiteResultSet sQLiteResultSet = _sqlClient.Execute("SELECT * FROM SeriesWatchList;");
				foreach (SQLiteResultSet.Row row2 in sQLiteResultSet.Rows)
				{
					SeriesItem seriesItem = new SeriesItem();
					seriesItem.show = new Series();
					seriesItem.show.Name = row2.fields[(int)sQLiteResultSet.ColumnIndices["SeriesName"]];
					seriesItem.show.OriginalName = row2.fields[(int)sQLiteResultSet.ColumnIndices["OriginalSeriesName"]];
					seriesItem.show.ID = row2.fields[(int)sQLiteResultSet.ColumnIndices["SeriesID"]];
					seriesItem.Added = new DateTime(Convert.ToInt64(row2.fields[(int)sQLiteResultSet.ColumnIndices["Added"]]));
					seriesItem.quality = row2.fields[(int)sQLiteResultSet.ColumnIndices["Quality"]];
					seriesItem.tracker = row2.fields[(int)sQLiteResultSet.ColumnIndices["Tracker"]];
					seriesItem.type = row2.fields[(int)sQLiteResultSet.ColumnIndices["Type"]];
					seriesItem.source = row2.fields[(int)sQLiteResultSet.ColumnIndices["Source"]];
					seriesItem.folder = row2.fields[(int)sQLiteResultSet.ColumnIndices["Folder"]];
					seriesItem.includespecials = Convert.ToBoolean(row2.fields[(int)sQLiteResultSet.ColumnIndices["IncludeSpecials"]]);
					seriesItem.LastCheck = DateTime.Now.AddMinutes(-26.0); // 2026: first check about 5 minutes after start-up
					_watchItemList.Add(seriesItem);
				}
			}
			if (DatabaseUtility.TableExists(_sqlClient, "UrlWatchList"))
			{
				SQLiteResultSet sQLiteResultSet = _sqlClient.Execute("SELECT * FROM UrlWatchList;");
				foreach (SQLiteResultSet.Row row3 in sQLiteResultSet.Rows)
				{
					UrlItem urlItem = new UrlItem();
					urlItem.Name = Field(sQLiteResultSet, row3, "Name");
					urlItem.Url = Field(sQLiteResultSet, row3, "Url");
					urlItem.SearchTerm = Field(sQLiteResultSet, row3, "SearchTerm");
					urlItem.Id = Field(sQLiteResultSet, row3, "Id");
					urlItem.LastHash = Field(sQLiteResultSet, row3, "LastHash");
					urlItem.Cookies = Field(sQLiteResultSet, row3, "Cookies");
					urlItem.Label = Field(sQLiteResultSet, row3, "Label");
					long ticks;
					urlItem.Added = long.TryParse(Field(sQLiteResultSet, row3, "Added"), out ticks) && ticks > 0 ? new DateTime(ticks) : DateTime.Now;
					urlItem.Tracker = Field(sQLiteResultSet, row3, "Tracker");
					urlItem.Folder = Field(sQLiteResultSet, row3, "Folder");
					urlItem.LastCheck = DateTime.Now;
					if (string.IsNullOrEmpty(urlItem.Name) && string.IsNullOrEmpty(urlItem.Url)) continue;
					_watchItemList.Add(urlItem);
				}
			}
			return true;
		}

		public bool IsMPTVSeriesAvailable()
		{
			Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
			try
			{
				if (assemblies.Where((Assembly e) => e.GetName().Name == "MP-TVSeries").Count() > 0)
				{
					return true;
				}
			}
			catch
			{
				return false;
			}
			return false;
		}
	}
}
