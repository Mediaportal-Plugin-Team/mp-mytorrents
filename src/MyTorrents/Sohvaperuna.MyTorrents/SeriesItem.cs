using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Sohvaperuna.TorrentSearch;

namespace Sohvaperuna.MyTorrents
{
	public class SeriesItem : WatchItem
	{
		public object lockobject = new object();

		public Series show { get; set; }

		public string quality { get; set; }

		public string source { get; set; }

		public string tracker { get; set; }

		public string type { get; set; }

		public string folder { get; set; }

		public bool includespecials { get; set; }

		public override string GetLabel1()
		{
			return show.Name;
		}

		public override string GetLabel2()
		{
			return Quality; // 2026: the same for every show
		}

		/// 2026 (1.3.42): at most 2 shows search at the same time (they all started together before, which slowed the
		/// HTPC and made sites time out)
		private static SemaphoreSlim _gate;
		private static int _gateSize;

		/// How many shows search at the same time (setting "AutoGrabber.SearchesAtOnce", 2).
		public static int SearchesAtOnce
		{
			get
			{
				int n;
				return int.TryParse(Configuration.GetExtra("AutoGrabber.SearchesAtOnce", "2"), out n) && n >= 1 && n <= 8 ? n : 2;
			}
		}

		public static readonly int[] SearchesAtOnceChoices = { 1, 2, 3, 4 };

		private static SemaphoreSlim Gate
		{
			get
			{
				int n = SearchesAtOnce;
				if (_gate == null || _gateSize != n)
				{
					_gate = new SemaphoreSlim(n); // a changed setting takes effect for the next searches
					_gateSize = n;
				}
				return _gate;
			}
		}

		/// 2026 (1.3.42): you asked for this show now (Check now, Grab the missing ones, Search again, Look now...) -
		/// it doesn't wait in the queue and runs at normal priority.
		[System.Xml.Serialization.XmlIgnore]
		public bool Urgent;

		public override void DoSearch()
		{
			ThreadStart work = delegate
			{
				try
				{
					lock (lockobject)
					{
						Log.Instance().Print($"Automatic search started [{show.Name}]");
						// 2026 (1.3.38): plain words instead of "unlocal"
						Log.Instance().Print($"[{show.Name}] Episodes you don't have yet: {(string.IsNullOrEmpty(show.AllUnlocal) ? "none" : show.AllUnlocal.Trim('|').Replace("|", ", "))}");
						Log.Instance().Print($"[{show.Name}] Next to get: {(string.IsNullOrEmpty(show.FirstUnlocal) ? "nothing yet" : show.FirstUnlocal)}");
						TorrentSearchEngine torrentSearchEngine = new TorrentSearchEngine(Configuration.Instance().Settings["TorrentSearch.Config"] as string);
						SeriesItem watch = this;
						string hash = string.Empty;
						if ((show.FirstUnlocal == "" && watch.type == "New episodes") || (show.AllUnlocal == "" && watch.type == "Missing episodes"))
						{
							// 2026: no site is searched for this, so look again at the normal time (30 minutes) instead of
							// tomorrow - MP-TVSeries may learn about a new episode at any time (its list is re-read hourly)
							// 2026 (1.3.38): nothing is searched for these, and MP-TVSeries rarely learns of a new episode
							// suddenly - look again in 3 hours instead of every 30 minutes (once it lists one, the air-date
							// wait takes over)
							Log.Instance().Print($"[{show.Name}] nothing announced yet - will start looking once TV Series lists the next episode (checks again in 3 hours)");
							watch.LastCheck = DateTime.Now;
							watch.NextCheck = DateTime.Now.AddHours(3);
						}
						else
						{
							// 2026: not out yet - wait until about 4 hours after it airs (torrents need a little while to appear),
							// then the normal check every 30 minutes
							if (watch.type == "New episodes" && LookFromUtc(show) > DateTime.UtcNow)
							{
								DateTime from = LookFromUtc(show);
								Log.Instance().Print($"[{show.Name}] {show.FirstUnlocal} airs {show.NextAirsUtc.ToLocalTime():ddd d MMM h:mm tt} - looking from {from.ToLocalTime():ddd d MMM h:mm tt}");
								watch.LastCheck = DateTime.Now;
								watch.NextCheck = from.ToLocalTime();
								return;
							}
							// 2026: every show searches all your sites (Jackett + your own sites)
							ITorrentSearch torrentSearch = torrentSearchEngine.Engines.OfType<TorrentSearch_Combiner>().FirstOrDefault(c => c.Name == TorrentSearchEngine.TVSeriesEngineName && c.Engines.Count > 0);
							if (torrentSearch == null)
							{
								Log.Instance().Print($"[{show.Name}] no search sites - add sites in MyTorrents Configuration");
								watch.LastCheck = DateTime.Now;
								return;
							}
							string text = Regex.Replace(watch.show.OriginalName ?? watch.show.Name ?? "", "[^0-9a-zA-Z ]+", " ").Trim();
							text = Regex.Replace(text, @"\s+", " ");
							// 2026: search for the episode itself ("Gotham S03E21") - searching just the show's name only
							// brings back the sites' newest / most popular uploads, which often do not include it.
							// If that finds nothing, the show's name on its own is tried as well.
							TorrentMatchList byName = null;
							Func<string, IEnumerable<TorrentMatch>> find = ep =>
							{
								if (torrentSearch.IsTVDBIDSearchSupported)
								{
									return byName ?? (byName = torrentSearch.SearchByTVDBID(Convert.ToInt32(watch.show.ID), "Default"));
								}
								TorrentMatchList r = torrentSearch.Search(text + " " + ep, "Default", null) ?? new TorrentMatchList(null);
								Log.Instance().Print($"Search \"{text} {ep}\" on {torrentSearch.Name}: {r.Count} results");
								if (r.Count > 0) return r;
								if (byName == null)
								{
									byName = torrentSearch.Search(text, "Default", null) ?? new TorrentMatchList(null);
									Log.Instance().Print($"Search \"{text}\" on {torrentSearch.Name}: {byName.Count} results");
								}
								return byName;
							};
							switch (watch.type)
							{
							case "New episodes":
							{
								if (string.IsNullOrEmpty(show.FirstUnlocal))
								{
									Log.Instance().Print($"[{show.Name}] no new episode to look for yet");
									break;
								}
								if (CantFind.Resting(show.ID, show.FirstUnlocal))
								{
									Log.Instance().Print($"[{show.Name}] {show.FirstUnlocal} is on the Can't find list - looked at today, next look tomorrow");
									break;
								}
								Log.Instance().Print($"Searching for episode [{show.FirstUnlocal}] of show [{show.Name}]");
								if (Grab(Grabbed.Recently(this, show.FirstUnlocal) ? new List<TorrentMatch>() : find(show.FirstUnlocal), show.FirstUnlocal, ref hash))
								{
									GetNextUnlocal();
								}
								break;
							}
							case "Missing episodes":
							{
								if (string.IsNullOrEmpty(show.AllUnlocal))
								{
									Log.Instance().Print($"No missing episodes available for show [{show.Name}]");
									break;
								}
								List<string> eps = show.AllUnlocal.Split('|').Where(x => x.Length > 0).ToList();
								// 2026: only ones that have aired (no point searching for episodes that are not out yet)
								if (show.AiredMissing != null && show.AiredMissing.Count > 0)
								{
									HashSet<string> aired = new HashSet<string>(show.AiredMissing.Select(x => x.Split('|')[0]));
									eps = eps.Where(aired.Contains).ToList();
								}
								if (!watch.includespecials) eps = eps.Where(x => !x.StartsWith("S00")).ToList();
								// 2026 (1.3.41): season first - a season that has finished airing (last episode 2+ days ago)
								// with enough of it missing (SeasonPackPercent, at least 2) is searched for once as a whole
								// season; none good -> its episodes one by one straight away; the whole season is tried
								// again a day later. A season still airing can't be out as a whole - episodes only.
								if (SeasonPacks)
								{
									foreach (IGrouping<string, string> season in eps.Where(x => !Grabbed.Recently(this, x)).GroupBy(x => x.Substring(0, Math.Min(3, x.Length))).Where(g => g.Count() >= 2 && EnoughForPack(g.Key, g.Count())).ToList())
									{
										int sn;
										if (!int.TryParse(season.Key.Substring(1), out sn) || sn == 0) continue;
										if (!SeasonFinished(sn)) continue; // still airing - episodes one by one below
										string packKey = show.ID + "|" + sn;
										DateTime tried;
										if (_packTried.TryGetValue(packKey, out tried) && DateTime.Now - tried < TimeSpan.FromHours(24)) continue;
										_packTried[packKey] = DateTime.Now;
										List<string> wantedHere = season.ToList();
										if (GrabSeasonPack(torrentSearch, text, sn, wantedHere, ref hash))
										{
											foreach (string ep in wantedHere) RemoveUnlocal(ep);
											eps = eps.Where(x => !wantedHere.Contains(x)).ToList();
										}
										else Log.Instance().Print($"[{show.Name}] no good whole season {sn} - getting the episodes one by one (whole season tried again tomorrow)");
									}
								}
								// a few episodes each time (every 30 minutes), so the sites are not flooded with searches
								foreach (string ep in eps.Where(x => !Grabbed.Recently(this, x) && !CantFind.Resting(show.ID, x)).Take(5))
								{
									if (Grab(find(ep), ep, ref hash))
									{
										RemoveUnlocal(ep);
									}
								}
								break;
							}
							}
						}
					}
				}
				catch (Exception e)
				{
					Log.Instance().Print(e);
				}
			};
			bool urgent = Urgent;
			Urgent = false;
			Thread thread = new Thread(() =>
			{
				if (urgent)
				{
					work(); // you asked for it - straight away
					return;
				}
				// 2026 (1.3.50): too many downloads going already - look again in half an hour
				if (Busy.TooBusy())
				{
					LastCheck = DateTime.Now;
					NextCheck = DateTime.Now.AddMinutes(30);
					return;
				}
				SemaphoreSlim gate = Gate;
				gate.Wait();
				try
				{
					work();
				}
				finally
				{
					gate.Release();
				}
			});
			thread.IsBackground = true;
			thread.Priority = urgent ? ThreadPriority.Normal : ThreadPriority.BelowNormal; // what you're watching comes first
			thread.Name = "MyTorrents Autograbber";
			thread.Start();
		}

		// ------------------------------------------------------------ 2026 (1.3.41): season packs

		private static readonly Dictionary<string, DateTime> _packTried = new Dictionary<string, DateTime>();

		/// Look for whole seasons first (setting "AutoGrabber.SeasonPacks", on).
		public static bool SeasonPacks => Configuration.GetExtra("AutoGrabber.SeasonPacks", "true") != "false";

		/// 2026 (1.3.41): look for a whole season only when at least this share of it is missing
		/// (setting "AutoGrabber.SeasonPackPercent", 50). 100 = only when the whole season is missing.
		public static readonly int[] SeasonPackPercentChoices = { 25, 50, 75, 100 };

		public static int SeasonPackPercent
		{
			get
			{
				int p;
				return int.TryParse(Configuration.GetExtra("AutoGrabber.SeasonPackPercent", "50"), out p) && p > 0 && p <= 100 ? p : 50;
			}
		}

		public static string SeasonPackPercentText(int p) => p >= 100 ? "the whole season is missing" : p + "% or more of the season is missing";

		/// Episodes in a season ("S02") as MP-TVSeries lists them - 0 when not known.
		private int SeasonSize(int season)
		{
			int n;
			return show?.SeasonSizes != null && show.SeasonSizes.TryGetValue(season, out n) ? n : 0;
		}

		private bool EnoughForPack(string seasonKey, int missing)
		{
			int sn;
			if (!int.TryParse(seasonKey.Substring(1), out sn)) return false;
			int total = SeasonSize(sn);
			if (total <= 0) return true; // not known - try the season (as before)
			bool enough = missing * 100 >= total * SeasonPackPercent;
			if (!enough) Log.Instance().Print($"[{show.Name}] season {sn}: {missing} of {total} episodes missing - under {SeasonPackPercent}%, getting them one by one");
			return enough;
		}

		/// A season has finished airing when MP-TVSeries has an air date for every episode and the last one was 2+ days
		/// ago (whole seasons usually turn up within a day or two). Not known -> treated as finished (as before).
		private bool SeasonFinished(int season)
		{
			DateTime last;
			if (show?.SeasonLastAired == null || !show.SeasonLastAired.TryGetValue(season, out last)) return true;
			bool done = last != DateTime.MaxValue && last.Date <= DateTime.Today.AddDays(-2);
			if (!done) Log.Instance().Print($"[{show.Name}] season {season} is still airing - getting the episodes one by one");
			return done;
		}

		/// Optional hard cap for a whole season in GB (setting "AutoGrabber.MaxSeasonGB", 0 = none).
		public static double MaxSeasonGB
		{
			get
			{
				double g;
				return double.TryParse(Configuration.GetExtra("AutoGrabber.MaxSeasonGB", "0"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out g) && g >= 0 ? g : 0;
			}
		}

		/// Favourite release names for season packs (setting "AutoGrabber.SeasonFavourites", e.g. "GalaxyTV").
		public static string[] SeasonFavourites => Configuration.GetExtra("AutoGrabber.SeasonFavourites", "")
			.Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries).Select(f => f.Trim()).Where(f => f.Length > 0).ToArray();

		/// Searches "Show S02" / "Show Season 2" and downloads the best whole-season copy: quality, seeds, and in size
		/// up to (biggest episode x episodes in the season) - only the season's total counts - and the optional cap.
		private bool GrabSeasonPack(ITorrentSearch search, string text, int season, List<string> wanted, ref string hash)
		{
			string s2 = season.ToString("00");
			List<TorrentMatch> all = new List<TorrentMatch>();
			foreach (string q in new[] { text + " S" + s2, text + " Season " + season })
			{
				try
				{
					TorrentMatchList r = search.Search(q, "Default", null);
					if (r != null) all.AddRange(r);
					Log.Instance().Print($"[{show.Name}] season pack search \"{q}\": {(r?.Count ?? 0)} results");
				}
				catch (Exception ex)
				{
					Log.Instance().Print($"[{show.Name}] season pack search failed: {ex.Message}");
				}
			}
			Regex pack = new Regex(@"(\bS0?" + season + @"\b(?![\s._-]*E\d)|\bSeason[\s._-]*0?" + season + @"\b|\bS0?" + season + @"[\s._-]*Complete\b)", RegexOptions.IgnoreCase);
			Regex single = new Regex(@"\bS0?" + season + @"[\s._-]*E\d", RegexOptions.IgnoreCase);
			int episodes = Math.Max(SeasonSize(season), wanted.Select(x => { int n; return int.TryParse(Regex.Match(x, @"E(\d+)").Groups[1].Value, out n) ? n : 0; }).DefaultIfEmpty(0).Max());
			long perEpisode = MaxSizeMB * 1024L * 1024L;
			long max = perEpisode > 0 ? perEpisode * episodes : 0;
			if (MaxSeasonGB > 0) max = max > 0 ? Math.Min(max, (long)(MaxSeasonGB * 1024 * 1024 * 1024)) : (long)(MaxSeasonGB * 1024 * 1024 * 1024);
			string key = MediaNames.Key(text);
			string relaxKey = show.ID + "|S" + s2;
			string relaxNote;
			int seeds = Math.Max(1, Relax.SeedsFor(relaxKey, out relaxNote));
			Func<string, long, List<TorrentMatch>> fitting = (q2, lim) => all.Where(m =>
			{
				string t = m.Title ?? "";
				if (!MediaNames.Key(t).StartsWith(key)) return false;
				if (!pack.IsMatch(t) || single.IsMatch(t)) return false;
				if (q2 != "Any" && !Naming.QualityOf(t).Equals(q2, StringComparison.OrdinalIgnoreCase)) return false;
				if (lim > 0 && m.Size > lim) return false;
				if (MinSizeMB > 0 && m.Size > 0 && m.Size < MinSizeMB * 1024L * 1024L * 2) return false;
				if (!Sound.Allowed(m, Sound.Tv, true)) return false;
				return m.Seed >= seeds;
			}).GroupBy(m => m.Title).Select(g => g.First()).ToList();
			List<TorrentMatch> fit = fitting(Quality, max);
			bool used1080 = false;
			if (fit.Count == 0 && Quality == "720p" && Relax.FallbackPercent > 0)
			{
				// 2026 (1.3.42): 1080p fallback for whole seasons too
				fit = fitting("1080p", max > 0 ? max + max * Relax.FallbackPercent / 100 : 0);
				used1080 = fit.Count > 0;
			}
			if (relaxNote.Length > 0) Log.Instance().Print($"[{show.Name}] season {season}: {relaxNote}");
			if (fit.Count == 0)
			{
				Log.Instance().Print($"[{show.Name}] season {season}: no whole-season copy fits ({(max > 0 ? max / 1024 / 1024 + " MB" : "any size")}) - getting the episodes one by one");
				LastLook = DateTime.Now.ToString("h:mm tt") + ": season " + season + " - no whole-season copy yet";
				return false;
			}
			string[] favs = SeasonFavourites;
			bool x265 = (Codec ?? "").IndexOf("265", StringComparison.OrdinalIgnoreCase) >= 0;
			List<TorrentMatch> ordered = fit.OrderByDescending(m => favs.Any(f => (m.Title ?? "").IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0))
				.ThenByDescending(m => x265 && CodecOf(m.Title) == "x265")
				.ThenByDescending(m => Sound.Rank(m, Sound.Tv))
				.ThenByDescending(m => m.Seed).ToList();
			// 2026 (1.3.42): a dead link (the site answers "not found") - try the next best copy, up to 3
			TorrentMatch best = null;
			bool ok = false;
			foreach (TorrentMatch m in ordered.Take(3))
			{
				best = m;
				ok = Download(m, out hash);
				Log.Instance().Print($"[{show.Name}] season {season}: whole season {m.Title} ({(m.Size > 0 ? m.Size / 1024 / 1024 + " MB" : "size?")}) {(ok ? "started" : "could not be downloaded - trying the next copy")}{(used1080 && ok ? " - 1080p used, no good 720p" : "")}");
				if (ok) break;
			}
			if (!ok) return false;
			Relax.Done(relaxKey);
			foreach (string ep in wanted)
			{
				Grabbed.Add(this, ep, hash, best.Title, Link(best));
				CantFind.Found(show.ID, ep);
			}
			LastLook = DateTime.Now.ToString("h:mm tt") + ": got season " + season + " (" + wanted.Count + " episodes)";
			return true;
		}

		/// Hours after an episode airs before the Auto grabber starts looking for it.
		public const int HoursAfterAiring = 4;

		/// When to start looking for the next episode (UTC); MinValue = now (air date unknown or already aired).
		public static DateTime LookFromUtc(Series s)
		{
			return s == null || s.NextAirsUtc == DateTime.MinValue ? DateTime.MinValue : s.NextAirsUtc.AddHours(HoursAfterAiring);
		}

		/// "next S01E04 - airs Wed 28 Oct 11:30 AM, looking from 3:30 PM" for the show's page
		public static string NextText(Series s)
		{
			if (s == null || string.IsNullOrEmpty(s.FirstUnlocal)) return "nothing new yet";
			if (s.NextAirsUtc == DateTime.MinValue) return s.FirstUnlocal + " - looking every 30 minutes";
			DateTime from = LookFromUtc(s).ToLocalTime();
			if (from <= DateTime.Now) return s.FirstUnlocal + " is out - looking every 30 minutes";
			return s.FirstUnlocal + " airs " + s.NextAirsUtc.ToLocalTime().ToString("ddd d MMM h:mm tt") + ", looking from " + from.ToString("h:mm tt");
		}

		// 2026: ONE set of Auto grabber defaults, used for every show (MyTorrents Configuration > Auto grabber,
		// or the menu button on the Auto grabber list in MediaPortal)

		public static readonly string[] QualityChoices = { "720p", "1080p", "2160p", "480p", "Any" };

		/// Resolution every show downloads (setting "AutoGrabber.Quality")
		public static string Quality
		{
			get { return Configuration.GetExtra("AutoGrabber.Quality", "720p"); }
		}

		/// Biggest episode in MB, 0 = no limit (setting "AutoGrabber.MaxSizeMB")
		public static long MaxSizeMB
		{
			get
			{
				long mb;
				return long.TryParse(Configuration.GetExtra("AutoGrabber.MaxSizeMB", "750"), out mb) && mb >= 0 ? mb : 750;
			}
		}

		public static readonly long[] SizeChoices = { 300, 400, 500, 750, 1000, 1500, 2000, 4000, 0 };

		/// 2026: smallest episode in MB - smaller copies are fakes or samples (setting "AutoGrabber.MinSizeMB", 0 = off)
		public static long MinSizeMB
		{
			get
			{
				long mb;
				return long.TryParse(Configuration.GetExtra("AutoGrabber.MinSizeMB", "100"), out mb) && mb >= 0 ? mb : 100;
			}
		}

		public static readonly long[] MinSizeChoices = { 0, 50, 100, 150, 200 };

		/// 2026: leave out placeholder episodes ("S03E99") and ones with no air date (setting "AutoGrabber.SkipPlaceholders")
		public static bool SkipPlaceholders
		{
			get { return !Configuration.GetExtra("AutoGrabber.SkipPlaceholders", "true").Equals("false", StringComparison.OrdinalIgnoreCase); }
		}

		/// true for a placeholder: numbered 99 or more, or no air date
		public static bool IsPlaceholder(int episode, string firstAired)
		{
			return episode >= 99 || string.IsNullOrWhiteSpace(firstAired);
		}

		/// 2026: copies with fewer seeders than this are skipped (setting "AutoGrabber.MinSeeders")
		public static int MinSeeders
		{
			get
			{
				int n;
				return int.TryParse(Configuration.GetExtra("AutoGrabber.MinSeeders", "4"), out n) && n >= 0 ? n : 4;
			}
		}

		public static readonly int[] SeederChoices = { 1, 2, 4, 6, 10, 20 };

		/// What new shows look for (setting "AutoGrabber.Type")
		public static string NewShowType
		{
			get { return Configuration.GetExtra("AutoGrabber.Type", "New episodes"); }
		}

		/// "720p · up to 500 MB · prefer x265"
		public static string DefaultsText()
		{
			string c = Codec;
			return Quality + " · " + (MaxSizeMB > 0 ? "up to " + MaxSizeMB + " MB" : "any size")
				+ (c == "any" ? "" : " · " + (c.StartsWith("only-") ? "only " : "prefer ") + c.Substring(c.IndexOf('-') + 1))
				+ (MinSeeders > 1 ? " · " + MinSeeders + "+ seeders" : "")
				+ (Favourites.Length > 0 ? " · " + (FavouritesMode == "only" ? "only " : "prefer ") + string.Join(", ", Favourites) : "");
		}

		/// 2026: what the last search for this show found (in memory, for the show's page)
		public string LastLook = "";

		/// 2026: default video type for the Auto grabber (setting "AutoGrabber.Codec"): any, prefer-x265, only-x265, prefer-x264, only-x264
		public static string Codec
		{
			get { return Configuration.GetExtra("AutoGrabber.Codec", "any"); }
		}

		public static readonly string[] CodecChoices = { "any", "prefer-x265", "only-x265", "prefer-x264", "only-x264" };

		public static string CodecText(string codec)
		{
			switch (codec)
			{
			case "prefer-x265": return "Prefer x265 (HEVC) - smaller files";
			case "only-x265": return "Only x265 (HEVC)";
			case "prefer-x264": return "Prefer x264 - plays on older devices";
			case "only-x264": return "Only x264";
			default: return "Any";
			}
		}

		private static readonly System.Text.RegularExpressions.Regex X265 = new System.Text.RegularExpressions.Regex(@"[xh][ .]?265|hevc", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
		private static readonly System.Text.RegularExpressions.Regex X264 = new System.Text.RegularExpressions.Regex(@"[xh][ .]?264|\bavc\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

		/// "x265", "x264" or "other"
		public static string CodecOf(string title) => IsCodec(title, "x265") ? "x265" : IsCodec(title, "x264") ? "x264" : "other";

		/// true when the title is the wanted video type
		public static bool IsCodec(string title, string codec)
		{
			if (codec.EndsWith("x265")) return X265.IsMatch(title ?? "");
			if (codec.EndsWith("x264")) return X264.IsMatch(title ?? "") && !X265.IsMatch(title ?? "");
			return true;
		}

		/// 2026: favourite release groups / sites (setting "AutoGrabber.Favourites", e.g. "MeGusta, EZTV, PSA").
		/// A copy is a favourite when its name contains one of them (e.g. "...x265-MeGusta") or it came from a site
		/// with that name.
		public static string[] Favourites
		{
			get
			{
				return Configuration.GetExtra("AutoGrabber.Favourites", "")
					.Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries)
					.Select(f => f.Trim()).Where(f => f.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
			}
		}

		/// "prefer" = favourites first, then the others;  "only" = favourites only (setting "AutoGrabber.FavouritesMode")
		public static string FavouritesMode
		{
			get { return Configuration.GetExtra("AutoGrabber.FavouritesMode", "prefer") == "only" ? "only" : "prefer"; }
		}

		public static readonly string[] FavouritesModeChoices = { "prefer", "only" };

		public static string FavouritesModeText(string mode) => mode == "only" ? "Only my favourites" : "Prefer my favourites";

		/// true when the copy is one of the favourites. Names are matched as whole words at their start, so
		/// "PSA" matches "-PSA" and "[PSA]" but not "PSALM"; "EZTV" also matches "[EZTVx.to]".
		public static bool IsFavourite(TorrentMatch m, string[] favourites)
		{
			if (m == null || favourites == null || favourites.Length == 0) return false;
			foreach (string f in favourites)
			{
				if (string.IsNullOrWhiteSpace(f)) continue;
				if (!string.IsNullOrEmpty(m.tracker) && m.tracker.IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0) return true;
				if (Regex.IsMatch(m.Title ?? "", @"(?<![A-Za-z0-9])" + Regex.Escape(f) + @"(?![a-wyzA-WYZ0-9])", RegexOptions.IgnoreCase)) return true;
			}
			return false;
		}

		/// 2026: downloads the ONE best copy of an episode - right quality and source, not bigger than the size limit,
		/// most seeders. (It used to download every copy it found.)
		/// 2026 (1.3.57): the name before "S01E08" must be this show's - "Identity" used to take "The New Negroes ...
		/// S01E01 Identity" and "Manhattan" took "The Wil Wheaton Project S01E08 Wil Takes Manhattan" (4-5 Nov).
		/// A year or a country after the name is fine ("Manhattan 2014", "The Office US").
		private bool ForThisShow(string title, string episode)
		{
			int at = title.IndexOf(episode, StringComparison.OrdinalIgnoreCase);
			if (at <= 0) return false;
			// a site's name in front is fine: "www.UIndex.org - Law.and.Order...", "www.torrenting.com - People.Just..."
			string front = Regex.Replace(title.Substring(0, at), @"^\s*(\[[^\]]*\]\s*)*(www\.)?[a-z0-9-]+\.(org|com|net|to|io|cc|se|me|ru|xyz|info)\s*-\s*", "", RegexOptions.IgnoreCase);
			string before = MediaNames.Key(Regex.Replace(front, @"[\s._\-\(\)\[\]]*((19|20)\d{2})?[\s._\-\(\)\[\]]*$", ""));
			foreach (string name in new[] { show?.Name, SearchTerm })
			{
				string key = MediaNames.Key(name);
				if (key.Length == 0) continue;
				if (before == key) return true;
				if (before.StartsWith(key) && Regex.IsMatch(before.Substring(key.Length), @"^(us|uk|au|nz|ca|(19|20)\d{2})+$")) return true;
			}
			return false;
		}

		private bool Grab(IEnumerable<TorrentMatch> found, string episode, ref string hash)
		{
			if (Grabbed.Recently(this, episode))
			{
				Log.Instance().Print($"[{show.Name}] {episode}: already downloaded on {Grabbed.When(this, episode):d MMM} - waiting for it to reach the library");
				return true; // counts as found, so the Auto grabber moves on to the next episode
			}
			List<TorrentMatch> all = found.Where(m => m.Title != null && !Grabbed.IsDead(this, episode, Link(m)) && m.Title.IndexOf(episode, StringComparison.OrdinalIgnoreCase) >= 0 && ForThisShow(m.Title, episode)
				&& (Quality == "Any" || m.Title.IndexOf(Quality, StringComparison.OrdinalIgnoreCase) >= 0)).ToList();
			// 2026: far too small to be the episode (a fake or a sample) - never used (size 0 = the site did not say)
			long min = MinSizeMB * 1024L * 1024L;
			List<TorrentMatch> matches = all.Where(m => m.Size <= 0 || m.Size >= min).ToList();
			int tooSmall = all.Count - matches.Count;
			long max = MaxSizeMB * 1024L * 1024L;
			string relaxKey = show.ID + "|" + episode;
			string relaxNote;
			int minSeeds = Relax.SeedsFor(relaxKey, out relaxNote); // 2026 (1.3.42): relaxes when nothing turns up for long
			int fewSeeds = matches.Count(m => m.Seed < minSeeds);
			List<TorrentMatch> fits = (max > 0 ? matches.Where(m => m.Size <= max) : matches).Where(m => m.Seed >= minSeeds).ToList(); // size 0 = site did not say
			string codec = Codec;
			int before = fits.Count;
			if (codec.StartsWith("only-")) fits = fits.Where(m => IsCodec(m.Title, codec)).ToList();
			string sound = Sound.Tv; // 2026 (1.3.45)
			fits = fits.Where(m => Sound.Allowed(m, sound, true)).ToList();
			// 2026: favourite release groups / sites come first (or are the only ones allowed)
			string[] favourites = Favourites;
			bool onlyFavourites = favourites.Length > 0 && FavouritesMode == "only";
			int beforeFav = fits.Count;
			if (onlyFavourites) fits = fits.Where(m => IsFavourite(m, favourites)).ToList();
			int favCount = favourites.Length == 0 ? 0 : matches.Count(m => IsFavourite(m, favourites));
			TorrentMatch best = fits.OrderByDescending(m => IsFavourite(m, favourites) ? 1 : 0)
				.ThenByDescending(m => codec.StartsWith("prefer-") && IsCodec(m.Title, codec) ? 1 : 0).ThenByDescending(m => Sound.Rank(m, sound)).ThenByDescending(m => m.Seed).FirstOrDefault();
			// 2026 (1.3.42): 1080p fallback - no good 720p copy: a 1080p one up to the size limit plus a percentage
			string fallbackNote = "";
			if (best == null && Quality == "720p" && Relax.FallbackPercent > 0)
			{
				long max2 = max > 0 ? max + max * Relax.FallbackPercent / 100 : 0;
				List<TorrentMatch> alt = found.Where(m => m.Title != null && !Grabbed.IsDead(this, episode, Link(m)) && m.Title.IndexOf(episode, StringComparison.OrdinalIgnoreCase) >= 0 && ForThisShow(m.Title, episode)
					&& m.Title.IndexOf("1080p", StringComparison.OrdinalIgnoreCase) >= 0 && (m.Size <= 0 || m.Size >= min)
					&& (max2 <= 0 || m.Size <= max2) && m.Seed >= minSeeds).ToList();
				if (codec.StartsWith("only-")) alt = alt.Where(m => IsCodec(m.Title, codec)).ToList();
				alt = alt.Where(m => Sound.Allowed(m, sound, true)).ToList();
				if (onlyFavourites) alt = alt.Where(m => IsFavourite(m, favourites)).ToList();
				best = alt.OrderByDescending(m => IsFavourite(m, favourites) ? 1 : 0)
					.ThenByDescending(m => codec.StartsWith("prefer-") && IsCodec(m.Title, codec) ? 1 : 0).ThenByDescending(m => m.Seed).FirstOrDefault();
				if (best != null) fallbackNote = " - 1080p used, no good 720p";
			}
			// 2026: the smallest copy that someone is seeding - tells you what the size limit would need to be
			TorrentMatch smallest = matches.Where(m => m.Size > 0 && m.Seed >= Math.Max(1, minSeeds)).OrderBy(m => m.Size).FirstOrDefault()
				?? matches.Where(m => m.Size > 0).OrderBy(m => m.Size).FirstOrDefault();
			string smallText = smallest == null ? "" : $"smallest {smallest.Size / 1024 / 1024} MB ({(IsCodec(smallest.Title, "x265") ? "x265" : IsCodec(smallest.Title, "x264") ? "x264" : "other")}, {smallest.Seed} seeders)";
			Log.Instance().Print($"[{show.Name}] {episode}: {matches.Count} found, {matches.Count(m => max > 0 && m.Size > max)} too big (over {MaxSizeMB} MB), {fewSeeds} with under {minSeeds} seeders"
				+ (tooSmall > 0 ? $", {tooSmall} too small to be real (under {MinSizeMB} MB, skipped)" : "")
				+ (codec.StartsWith("only-") ? $", {before - beforeFav} not {codec.Substring(5)}" : "")
				+ (favourites.Length > 0 ? $", {favCount} from favourites" + (onlyFavourites ? $" ({beforeFav - fits.Count} others skipped)" : "") : "")
				+ (best != null && IsFavourite(best, favourites) ? " - favourite" : "")
				+ (smallText.Length > 0 ? ", " + smallText : "")
				+ (relaxNote.Length > 0 ? " - " + relaxNote : "")
				+ (best != null ? ", downloading " + best.Title + fallbackNote : ""));
			// shown on the show's page ("Last look: ...")
			LastLook = DateTime.Now.ToString("h:mm tt") + ": " + (matches.Count == 0 ? "no copies found yet"
				: best != null ? "downloading " + (best.Size > 0 ? best.Size / 1024 / 1024 + " MB" : "a copy") + (fallbackNote.Length > 0 ? " (1080p)" : "")
				: matches.Count + " found, none fit" + (smallText.Length > 0 ? " - " + smallText : ""));
			// 2026: size record (the smallest real copy seeded enough, and the one downloaded)
			TorrentMatch realSmallest = matches.Where(m => m.Size > 0 && m.Seed >= Math.Max(1, minSeeds)).OrderBy(m => m.Size).FirstOrDefault();
			try
			{
				SizeRecord.Note(show.ID, show.Name, episode, realSmallest == null ? 0 : realSmallest.Size / 1024 / 1024, realSmallest == null ? "" : CodecOf(realSmallest.Title),
					best != null && best.Size > 0 ? best.Size / 1024 / 1024 : 0, best?.Title);
			}
			catch
			{
			}
			if (best == null)
			{
				// 2026 (1.3.42): for the Can't find list
				CantFind.NotFound(show.ID, show.Name, episode, matches.Count == 0 ? "nothing found" : matches.Count + " found, none fit" + (smallText.Length > 0 ? " - " + smallText : ""));
				// 2026: copies exist but none fit the settings - after a while, ask (once per episode)
				if (matches.Count > 0) TooBig.Seen(this, episode, matches);
				return false;
			}
			TooBig.Clear(this, episode);
			bool started = Download(best, episode, out hash);
			if (!started && fallbackNote.Length == 0)
			{
				// 2026 (1.3.42): a dead link - try the next best copies (up to 2 more)
				foreach (TorrentMatch m in fits.Where(x => x != best).OrderByDescending(x => IsFavourite(x, favourites) ? 1 : 0)
					.ThenByDescending(x => codec.StartsWith("prefer-") && IsCodec(x.Title, codec) ? 1 : 0).ThenByDescending(x => x.Seed).Take(2))
				{
					Log.Instance().Print($"[{show.Name}] {episode}: that copy could not be downloaded - trying {m.Title}");
					started = Download(m, episode, out hash);
					if (started) break;
				}
			}
			if (started)
			{
				Relax.Done(relaxKey);
				CantFind.Found(show.ID, episode);
			}
			return started;
		}

		/// The copy's own link (used to remember copies that turned out dead).
		public static string Link(TorrentMatch m) => !string.IsNullOrEmpty(m.magnet) ? m.magnet : (m.Url ?? m.Title ?? "");

		/// Starts one download for this show and remembers it for "copy to library".
		public bool Download(TorrentMatch match, out string hash)
		{
			bool ok = TorrentEngine.Instance().StartDownloadingToFolder(out hash, string.IsNullOrEmpty(match.Url) ? match.magnet : match.Url, "", force: true, match.cookie, match.Id, "", "", folder, string.IsNullOrEmpty(match.Url) ? "magnet" : "torrent");
			if (ok) PostProcessor.Remember(hash, SearchContext.TV, show.Name, match.Title);
			return ok;
		}

		/// Starts a download and remembers the episode, so it is not downloaded again while it is still on its way.
		public bool Download(TorrentMatch match, string episode, out string hash)
		{
			bool ok = Download(match, out hash);
			if (ok) Grabbed.Add(this, episode, hash, match.Title, Link(match));
			return ok;
		}

		/// 2026: "Get this episode now - just this once": downloads the chosen copy whatever its size, and the Auto grabber
		/// moves on as if it had found it (settings unchanged).
		public bool GetNow(TorrentMatch m, string episode, string how)
		{
			string hash;
			bool ok = Download(m, episode, out hash);
			Log.Instance().Print($"[{show.Name}] {episode}: get now ({how}) - {m.Title} ({(m.Size > 0 ? m.Size / 1024 / 1024 + " MB" : "size?")}) {(ok ? "started" : "failed")}");
			if (!ok) return false;
			TooBig.Clear(this, episode);
			Relax.Done(show.ID + "|" + episode);
			CantFind.Found(show.ID, episode);
			try
			{
				SizeRecord.Note(show.ID, show.Name, episode, 0, "", m.Size > 0 ? m.Size / 1024 / 1024 : 0, m.Title);
			}
			catch
			{
			}
			Downloaded(episode);
			LastLook = DateTime.Now.ToString("h:mm tt") + ": got " + episode + " now (" + (m.Size > 0 ? m.Size / 1024 / 1024 + " MB" : "a copy") + ")";
			return true;
		}

		/// After a download chosen by hand from the "only bigger copies" question.
		public void Downloaded(string episode)
		{
			if (type == "Missing episodes") RemoveUnlocal(episode);
			else if (show.FirstUnlocal == episode) GetNextUnlocal();
		}

		public void GetNextUnlocal()
		{
			string[] array = show.AllUnlocal.Split('|');
			int num = array.ToList().IndexOf(show.FirstUnlocal) + 1;
			show.FirstUnlocal = num < array.Length ? array[num] : "";
			show.NextAirsUtc = show.AirsOf(show.FirstUnlocal); // 2026 (1.3.39): the date goes with the episode
			Log.Instance().Print($"next unlocal episode for series[{show.Name}] is [{show.FirstUnlocal}]");
		}

		public void RemoveUnlocal(string episode)
		{
			List<string> list = show.AllUnlocal.Split('|').ToList();
			if (list.Remove(episode))
			{
				show.AllUnlocal = string.Join("|", list.ToArray());
				Log.Instance().Print($"New unlocal Episodes are [{show.AllUnlocal}]");
			}
		}
	}
}
