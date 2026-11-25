using System;
using System.IO;
using System.Linq;
using MediaPortal.Configuration;

namespace Sohvaperuna.MyTorrents
{
	/// <summary>
	/// 1.3.74: "Start fresh" (Check-up tab, Configuration and the web page). MyTorrents forgets what it has tried -
	/// what it downloaded and is waiting for, dead copies, the Can't find list, relaxed seeders, too-big waits, retries,
	/// stuck downloads, the missing report, the check-up notes - so it looks for everything again with the settings
	/// as they are now. Settings, the show list, Wanted movies, hidden / ignore lists, moved-aside duplicates and the
	/// episode sizes are kept. The files are moved into "MyTorrents start-fresh backup" so it can be undone.
	/// MediaPortal, the background helper and Configuration each keep these lists in memory: a stamp file
	/// (MyTorrents.fresh.txt) tells the others to drop theirs (see Changed).
	/// </summary>
	public static class StartFresh
	{
		public static readonly string[] Names = { "grabbed", "deadcopies", "cant-find", "first-looked", "retry", "stalled", "waiting", "missing", "requests", "drop-left", "checkup" };

		private static string Folder => Path.GetDirectoryName(Config.GetFile(Config.Dir.Config, "MyTorrents.grabbed.txt"));
		private static string FileOf(string name) => Path.Combine(Folder, "MyTorrents." + name + ".txt");
		private static string StampFile => Path.Combine(Folder, "MyTorrents.fresh.txt");
		public static string BackupFolder => Path.Combine(Folder, "MyTorrents start-fresh backup");

		/// What is still downloading - start fresh while these go and the same episodes may be downloaded again.
		public static int Downloading()
		{
			try
			{
				return (TorrentEngine.Instance()?.TorrentsAll ?? new System.Collections.Generic.List<Sohvaperuna.TorrentEngine.Torrent>()).Count(t => t != null && t.Progress < 100);
			}
			catch
			{
				return 0;
			}
		}

		public static bool CanUndo => Directory.Exists(BackupFolder) && Directory.GetFiles(BackupFolder, "MyTorrents.*.txt").Length > 0;

		public static DateTime When
		{
			get
			{
				try { return Directory.Exists(BackupFolder) ? Directory.GetLastWriteTime(BackupFolder) : DateTime.MinValue; } catch { return DateTime.MinValue; }
			}
		}

		public const string WhatText = "MyTorrents forgets what it has tried and looks for everything again with your settings as they are now:\n" +
			"what it downloaded and is waiting for, dead copies, the Can't find list, relaxed seeders, too-big waits, stuck downloads and the check-up notes.\n" +
			"Kept: your settings, show list, Wanted movies, hidden items and moved-aside duplicates. Your files and the torrent program are not touched.";

		/// Does it. Returns what to tell the user.
		public static string Do()
		{
			try
			{
				if (Directory.Exists(BackupFolder)) Directory.Delete(BackupFolder, true); // only the last one is kept
				Directory.CreateDirectory(BackupFolder);
				int moved = 0;
				foreach (string n in Names)
				{
					string f = FileOf(n);
					if (!File.Exists(f)) continue;
					File.Move(f, Path.Combine(BackupFolder, Path.GetFileName(f)));
					moved++;
				}
				Directory.SetLastWriteTime(BackupFolder, DateTime.Now);
				Stamp();
				Log.Instance().Print("Start fresh: MyTorrents forgot what it had tried (" + moved + " lists, kept in \"" + BackupFolder + "\") - looking for everything again");
				return "Started fresh - MyTorrents looks for everything again. You can undo this from the Check-up.";
			}
			catch (Exception ex)
			{
				Log.Instance().Print("Start fresh: " + ex.Message);
				return "Couldn't start fresh: " + ex.Message;
			}
		}

		/// Puts the lists back as they were before Start fresh.
		public static string Undo()
		{
			try
			{
				if (!CanUndo) return "There's nothing to undo.";
				foreach (string f in Directory.GetFiles(BackupFolder, "MyTorrents.*.txt"))
				{
					string to = Path.Combine(Folder, Path.GetFileName(f));
					if (File.Exists(to)) File.Delete(to);
					File.Move(f, to);
				}
				Directory.Delete(BackupFolder, true);
				Stamp();
				Log.Instance().Print("Start fresh: undone - the lists from before are back");
				return "Undone - MyTorrents remembers everything from before again.";
			}
			catch (Exception ex)
			{
				Log.Instance().Print("Start fresh undo: " + ex.Message);
				return "Couldn't undo: " + ex.Message;
			}
		}

		private static void Stamp()
		{
			long t = DateTime.Now.Ticks;
			try { File.WriteAllText(StampFile, t.ToString()); } catch { }
			_stamp = t;
			_checked = DateTime.Now;
		}

		private static long _stamp;
		private static DateTime _checked = DateTime.MinValue;

		private static long Current
		{
			get
			{
				if ((DateTime.Now - _checked).TotalSeconds >= 10)
				{
					_checked = DateTime.Now;
					try
					{
						long t;
						if (File.Exists(StampFile) && long.TryParse(File.ReadAllText(StampFile).Trim(), out t)) _stamp = t;
					}
					catch
					{
					}
				}
				return _stamp;
			}
		}

		/// True once after a Start fresh (here or in another MyTorrents program) - the caller drops what it keeps in memory.
		public static bool Changed(ref long seen)
		{
			long now = Current;
			if (now == seen) return false;
			seen = now;
			return true;
		}
	}
}
