using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using MediaPortal.Configuration;
using Sohvaperuna.TorrentSearch;

namespace Sohvaperuna.MyTorrents
{
	/// <summary>
	/// 2026 (1.3.50): the drop folder ("where would I put a movie or episode file so MyTorrents picks
	/// it up and processes it?"). A movie or episode put there - a file, or a folder with one in it - is treated
	/// exactly like a finished download: worked out, named your way, filed into the right show / season or Movies /
	/// collection folder, .nfo, MP-TVSeries / Moving Pictures told, in the check-up. It is MOVED out of the drop
	/// folder. Only once it has stopped growing (2 minutes untouched), so a file still being copied in is left alone.
	/// What can't be worked out stays where it is, with the reason (MyTorrents.drop-left.txt, shown in the check-up).
	/// Works in MediaPortal and in the background helper. Setting "Finished.DropFolder" ("" = off).
	/// </summary>
	public static class DropFolder
	{
		public static string Folder => (Configuration.GetExtra("Finished.DropFolder", "") ?? "").Trim();

		private static readonly HashSet<string> VideoExt = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".mkv", ".mp4", ".avi", ".m4v", ".ts", ".m2ts", ".wmv", ".mov", ".mpg", ".mpeg" };
		private static string LeftFile => Config.GetFile(Config.Dir.Config, "MyTorrents.drop-left.txt");
		private static DateTime _last = DateTime.MinValue;
		private static int _busy;
		// things already tried that couldn't be filed: path -> size|time, so they aren't tried again until they change
		private static readonly Dictionary<string, string> Tried = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

		/// Called with the finished-downloads check (every few seconds); looks every 2 minutes.
		public static void Check()
		{
			string folder = Folder;
			if (folder.Length == 0 || (DateTime.Now - _last).TotalMinutes < 2) return;
			_last = DateTime.Now;
			if (System.Threading.Interlocked.CompareExchange(ref _busy, 1, 0) != 0) return;
			System.Threading.ThreadPool.QueueUserWorkItem(_ =>
			{
				try
				{
					Run(folder);
				}
				catch (Exception ex)
				{
					Log.Instance().Print("Drop folder: " + ex.Message);
				}
				finally
				{
					_busy = 0;
				}
			});
		}

		private static void Run(string folder)
		{
			if (!Directory.Exists(folder)) return;
			Dictionary<string, string> left = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			foreach (string item in Directory.GetFileSystemEntries(folder))
			{
				string name = Path.GetFileName(item);
				if (name.StartsWith(".") || name.EndsWith(".mytorrents-part", StringComparison.OrdinalIgnoreCase)) continue;
				List<string> videos = Videos(item);
				if (videos.Count == 0) continue;
				// still arriving? (anything in it written to in the last 2 minutes)
				if (videos.Any(v => (DateTime.Now - SafeTime(v)).TotalMinutes < 2)) continue;
				string stamp = videos.Sum(v => SafeSize(v)) + "|" + videos.Max(v => SafeTime(v)).Ticks;
				if (Tried.TryGetValue(item, out string was) && was == stamp)
				{
					string why0 = LeftReason(item);
					if (why0.Length > 0) left[item] = why0;
					continue;
				}
				string why = FileIt(item, name, videos);
				if (why.Length > 0)
				{
					Tried[item] = stamp;
					left[item] = why;
					Log.Instance().Print("Drop folder: " + name + " left where it is - " + why);
				}
				else
				{
					Tried.Remove(item);
					// a folder that's now empty apart from artwork / info files goes too
					try
					{
						if (Directory.Exists(item) && !Directory.EnumerateFiles(item, "*", SearchOption.AllDirectories).Any(f => VideoExt.Contains(Path.GetExtension(f)) || new FileInfo(f).Length > 20L * 1024 * 1024))
							Directory.Delete(item, true);
					}
					catch
					{
					}
				}
			}
			SaveLeft(left);
		}

		/// "" = filed, otherwise why not.
		private static string FileIt(string item, string name, List<string> videos)
		{
			string kind = "", s, code, title;
			int n;
			if (MediaNames.TryEpisode(name, out s, out n, out code) || videos.Any(v => MediaNames.TryEpisode(Path.GetFileName(v), out s, out n, out code))) kind = SearchContext.TV;
			else if (MediaNames.TryMovie(name, out title, out n) || videos.Any(v => MediaNames.TryMovie(Path.GetFileName(v), out title, out n))) kind = SearchContext.Movie;
			if (kind == "") return "can't tell what it is - name it like \"Show S01E02\" or \"Movie Title (2024)\"";
			string root = kind == SearchContext.TV ? PostProcessor.TVFolder : PostProcessor.MovieFolder;
			if (string.IsNullOrEmpty(root)) return "no " + (kind == SearchContext.TV ? "TV" : "movie") + " folder set (When finished tab)";
			if (!Directory.Exists(root)) return "can't reach " + root;
			if (item.StartsWith(root.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase)) return "the drop folder is inside your library folder - pick one outside it (When finished tab)";
			Log.Instance().Print("Drop folder: filing " + name + " (" + (kind == SearchContext.TV ? "TV" : "movie") + ")");
			PostProcessor.SkipNotes = new List<string>();
			try
			{
				List<string> done = PostProcessor.FileVideos(root, kind, "", name, videos, true);
				string notes = string.Join("; ", PostProcessor.SkipNotes);
				if (done.Count == 0) return notes.Length > 0 ? notes : "couldn't work out a name for it";
				int still = videos.Count(v => SafeExists(v));
				if (still > 0) return notes.Length > 0 ? notes : still + " of its files stayed (already in your library, or no name could be worked out)";
				return "";
			}
			finally
			{
				PostProcessor.SkipNotes = null;
			}
		}

		private static List<string> Videos(string item)
		{
			try
			{
				List<string> all = System.IO.File.Exists(item) ? new List<string> { item } :
					Directory.Exists(item) ? Directory.EnumerateFiles(item, "*", SearchOption.AllDirectories).ToList() : new List<string>();
				List<string> videos = all.Where(f => VideoExt.Contains(Path.GetExtension(f)) && !Regex.IsMatch(Path.GetFileNameWithoutExtension(f), @"(^|[\W_])(sample|trailer)([\W_]|$)|^(backdrop\d*|folder|fanart\d*|poster)(-x265)?$", RegexOptions.IgnoreCase)).ToList();
				return videos;
			}
			catch
			{
				return new List<string>();
			}
		}

		private static DateTime SafeTime(string f)
		{
			try
			{
				FileInfo i = new FileInfo(f);
				return i.LastWriteTime > i.CreationTime ? i.LastWriteTime : i.CreationTime;
			}
			catch
			{
				return DateTime.Now;
			}
		}

		private static long SafeSize(string f)
		{
			try
			{
				return new FileInfo(f).Length;
			}
			catch
			{
				return 0;
			}
		}

		private static bool SafeExists(string f)
		{
			try
			{
				return System.IO.File.Exists(f);
			}
			catch
			{
				return false;
			}
		}

		// ------------------------------------------------------------------ what's left (for the check-up)

		public static Dictionary<string, string> Left()
		{
			Dictionary<string, string> d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			try
			{
				if (!System.IO.File.Exists(LeftFile)) return d;
				foreach (string line in System.IO.File.ReadAllLines(LeftFile))
				{
					int t = line.IndexOf('\t');
					if (t > 0) d[line.Substring(0, t)] = line.Substring(t + 1);
				}
			}
			catch
			{
			}
			return d;
		}

		private static string LeftReason(string item) => Left().TryGetValue(item, out string why) ? why : "";

		private static void SaveLeft(Dictionary<string, string> left)
		{
			try
			{
				System.IO.File.WriteAllLines(LeftFile, left.Select(kv => kv.Key + "\t" + kv.Value));
			}
			catch
			{
			}
		}
	}
}
