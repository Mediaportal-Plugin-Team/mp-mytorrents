using System;
using System.Collections.Generic;
using System.Linq;
using MediaPortal.Dialogs;
using MediaPortal.GUI.Library;
using Sohvaperuna.TorrentSearch;

namespace Sohvaperuna.MyTorrents
{
	/// <summary>
	/// 2026: when the Auto grabber finds an episode but every copy is bigger than the limit (or not the video type
	/// asked for), it waits a while - then tells you once and asks what to do.
	/// </summary>
	public static class TooBig
	{
		private sealed class Case
		{
			public SeriesItem Show;
			public string Episode;
			public DateTime Since;
			public TorrentMatch Smallest;
			public int Count;
			public bool Told;
			public bool Asked;
		}

		private static readonly object Lock = new object();
		private static Dictionary<string, Case> _cases;

		// 2026: kept in MyTorrents.waiting.txt (next to MediaPortal.xml) so the hours carry on after a restart
		private static string File => MediaPortal.Configuration.Config.GetFile(MediaPortal.Configuration.Config.Dir.Config, "MyTorrents.waiting.txt");

		private static Dictionary<string, Case> Cases
		{
			get
			{
				if (_cases != null) return _cases;
				_cases = new Dictionary<string, Case>();
				try
				{
					if (System.IO.File.Exists(File))
					{
						foreach (string line in System.IO.File.ReadAllLines(File))
						{
							// key <tab> since (ticks) <tab> told <tab> asked
							string[] f = line.Split('\t');
							long ticks;
							if (f.Length < 4 || !long.TryParse(f[1], out ticks)) continue;
							Case c = new Case { Since = new DateTime(ticks), Told = f[2] == "1", Asked = f[3] == "1" };
							if (DateTime.Now - c.Since > TimeSpan.FromDays(30)) continue; // long gone
							string[] k = f[0].Split('|');
							c.Episode = k.Length > 1 ? k[1] : "";
							_cases[f[0]] = c;
						}
					}
				}
				catch (Exception ex)
				{
					Log.Instance().Print("Auto grabber waiting list: " + ex.Message);
				}
				return _cases;
			}
		}

		private static void Save()
		{
			try
			{
				System.IO.File.WriteAllLines(File, Cases.Select(kv => kv.Key + "\t" + kv.Value.Since.Ticks + "\t" + (kv.Value.Told ? "1" : "0") + "\t" + (kv.Value.Asked ? "1" : "0")));
			}
			catch (Exception ex)
			{
				Log.Instance().Print("Auto grabber waiting list: " + ex.Message);
			}
		}

		/// Hours to wait before asking (setting "AutoGrabber.AskAfterHours", 0 = never ask)
		public static int AskAfterHours
		{
			get
			{
				int h;
				return int.TryParse(Configuration.GetExtra("AutoGrabber.AskAfterHours", "6"), out h) && h >= 0 ? h : 6;
			}
		}

		public static readonly int[] HourChoices = { 1, 3, 6, 12, 24, 48, 0 };

		public static string HoursText(int h) => h == 0 ? "Never ask - just keep waiting" : "Ask me after " + h + (h == 1 ? " hour" : " hours");

		private static string Key(SeriesItem s, string episode) => (s.show?.ID ?? s.show?.Name ?? "") + "|" + episode;

		public static void Seen(SeriesItem show, string episode, List<TorrentMatch> found)
		{
			int hours = AskAfterHours;
			if (hours == 0 || found.Count == 0) return;
			// for testing: hidden setting "AutoGrabber.AskAfterMinutes" (e.g. 0 = ask at once) overrides the hours
			TimeSpan wait = TimeSpan.FromHours(hours);
			int minutes;
			if (int.TryParse(Configuration.GetExtra("AutoGrabber.AskAfterMinutes", ""), out minutes) && minutes >= 0) wait = TimeSpan.FromMinutes(minutes);
			lock (Lock)
			{
				Case c;
				if (!Cases.TryGetValue(Key(show, episode), out c))
				{
					c = new Case { Since = DateTime.Now };
					Cases[Key(show, episode)] = c;
					Save();
				}
				c.Show = show;
				c.Episode = episode;
				c.Count = found.Count;
				// the smallest copy with a known size (0 = the site did not say)
				// the best fallback: a copy someone is seeding, smallest first (0 seeders = it would probably never start)
				c.Smallest = found.Where(m => m.Seed > 0 && m.Size > 0).OrderBy(m => m.Size).FirstOrDefault()
					?? found.OrderByDescending(m => m.Seed).First();
				if (c.Told || DateTime.Now - c.Since < wait) return;
				if (!MyTorrents.InMediaPortal) return; // 2026 (1.3.45): the background helper can't ask - MediaPortal will
				c.Told = true;
				Save();
			}
			Log.Instance().Print($"[{show.show.Name}] {episode}: only copies that do not fit the settings for {(int)wait.TotalMinutes} minutes - asking");
			GUIWindowManager.SendThreadCallback((p1, p2, data) =>
			{
				try
				{
					if (!GUIGraphicsContext.IsFullScreenVideo)
					{
						GUIDialogNotify n = (GUIDialogNotify)GUIWindowManager.GetWindow(2016);
						n.Reset();
						n.SetHeading("Auto grabber");
						n.SetText(show.show.Name + " " + episode + ": no copy fits your settings\n(size / seeders). Open My Torrents to choose.");
						n.TimeOut = 10;
						n.DoModal(GUIWindowManager.ActiveWindow);
						Menus.Check(n);
					}
				}
				catch
				{
				}
				return 0;
			}, 0, 0, null);
		}

		public static void Clear(SeriesItem show, string episode)
		{
			lock (Lock)
			{
				if (Cases.Remove(Key(show, episode))) Save();
			}
		}

		/// Called when My Torrents opens: ask about each waiting episode once.
		public static void AskIfDue()
		{
			if (!MyTorrents.InMediaPortal) return; // 2026 (1.3.45): only MediaPortal can ask
			List<Case> due;
			lock (Lock)
			{
				// a case loaded after a restart waits until the next search has found the copies again
				due = Cases.Values.Where(c => c.Told && !c.Asked && c.Show != null && c.Smallest != null).ToList();
				foreach (Case c in due) c.Asked = true;
				if (due.Count > 0) Save();
			}
			if (due.Count == 0) return;
			GUIWindowManager.SendThreadCallback((p1, p2, data) =>
			{
				foreach (Case c in due)
				{
					try
					{
						Ask(c);
					}
					catch (Exception ex)
					{
						Log.Instance().Print("Auto grabber question: " + ex.Message);
					}
				}
				return 0;
			}, 0, 0, null);
		}

		private static void Ask(Case c)
		{
			GUIDialogMenu menu = (GUIDialogMenu)GUIWindowManager.GetWindow(2012);
			if (menu == null || c.Smallest == null) return;
			long mb = c.Smallest.Size / 1024 / 1024;
			long limit = SeriesItem.MaxSizeMB;
			long raiseTo = SeriesItem.SizeChoices.Where(x => x > 0 && x >= mb).DefaultIfEmpty(0).Min();
			int hours = (int)Math.Max(1, (DateTime.Now - c.Since).TotalHours);
			menu.Reset();
			menu.SetHeading(c.Show.show.Name + " " + c.Episode + " - " + c.Count + " found, none fit (" + hours + " h)");
			List<string> actions = new List<string>();
			menu.Add("Download one now (" + (mb > 0 ? mb + " MB" : "size unknown") + ", " + c.Smallest.Seed + " seeders) - just this once");
			actions.Add("download");
			if (raiseTo > limit && limit > 0)
			{
				menu.Add("Raise the limit to " + raiseTo + " MB for every show (now " + limit + " MB)");
				actions.Add("raise");
			}
			menu.Add("Keep waiting for a smaller copy");
			actions.Add("wait");
			menu.DoModal(GUIWindowManager.ActiveWindow);
			Menus.Check(menu);
			int sel = menu.SelectedLabel;
			string act = sel >= 0 && sel < actions.Count ? actions[sel] : "wait";
			if (act == "download")
			{
				string hash;
				bool ok = c.Show.Download(c.Smallest, c.Episode, out hash);
				Log.Instance().Print($"[{c.Show.show.Name}] {c.Episode}: downloading the smallest copy by choice - {c.Smallest.Title} ({(ok ? "started" : "failed")})");
				if (ok)
				{
					c.Show.Downloaded(c.Episode);
					Clear(c.Show, c.Episode);
				}
				MyTorrents.Instance()?.AfterDownloadStarted(ok, c.Smallest.Title);
			}
			else if (act == "raise")
			{
				Configuration.SetExtra("AutoGrabber.MaxSizeMB", raiseTo.ToString());
				Log.Instance().Print("Auto grabber: biggest episode raised to " + raiseTo + " MB");
				Clear(c.Show, c.Episode);
				c.Show.LastCheck = DateTime.Now.AddMinutes(-31); // look again straight away
			}
		}
	}
}
