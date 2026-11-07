using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MediaPortal.Configuration;

namespace Sohvaperuna.TorrentSearch
{
	/// <summary>
	/// 2026: how each search site has done over its last 30 searches (MyTorrents.sitestats.txt next to
	/// MediaPortal.xml), so sites that never find anything or are always too slow can be spotted and removed.
	/// </summary>
	public static class SiteStats
	{
		private const int Keep = 30;

		/// Searches a site needs before it gets a verdict
		public const int Enough = 8;

		private static readonly object Lock = new object();
		private static Dictionary<string, List<int>> _sites;

		private static string File
		{
			get
			{
				try
				{
					return Config.GetFile(Config.Dir.Config, "MyTorrents.sitestats.txt");
				}
				catch
				{
					return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MyTorrents.sitestats.txt");
				}
			}
		}

		private static Dictionary<string, List<int>> Sites
		{
			get
			{
				if (_sites != null) return _sites;
				_sites = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
				try
				{
					if (System.IO.File.Exists(File))
					{
						foreach (string line in System.IO.File.ReadAllLines(File))
						{
							// name <tab> results,results,-1,...   (-1 = failed / too slow)
							string[] f = line.Split('\t');
							if (f.Length < 2 || f[0].Length == 0) continue;
							_sites[f[0]] = f[1].Split(',').Select(x => { int n; return int.TryParse(x, out n) ? n : (int?)null; })
								.Where(x => x.HasValue).Select(x => x.Value).ToList();
						}
					}
				}
				catch
				{
				}
				return _sites;
			}
		}

		/// Re-read the file next time (the settings window reads what MediaPortal wrote).
		public static void Reload()
		{
			lock (Lock)
			{
				_sites = null;
			}
		}

		/// results = how many it found, or -1 when it failed or did not answer in time
		public static void Record(IEnumerable<KeyValuePair<string, int>> outcomes)
		{
			lock (Lock)
			{
				foreach (KeyValuePair<string, int> o in outcomes)
				{
					if (string.IsNullOrEmpty(o.Key)) continue;
					List<int> list;
					if (!Sites.TryGetValue(o.Key, out list)) Sites[o.Key] = list = new List<int>();
					list.Add(o.Value);
					if (list.Count > Keep) list.RemoveRange(0, list.Count - Keep);
				}
				try
				{
					System.IO.File.WriteAllLines(File, Sites.Select(kv => kv.Key.Replace("\t", " ") + "\t" + string.Join(",", kv.Value)));
				}
				catch
				{
				}
			}
		}

		/// "found something in 18 of 20 searches, 2 too slow" - "" when there is nothing recorded yet
		public static string Summary(string site)
		{
			List<int> list;
			lock (Lock)
			{
				if (!Sites.TryGetValue(site ?? "", out list) || list.Count == 0) return "";
				list = list.ToList();
			}
			int n = list.Count, found = list.Count(x => x > 0), failed = list.Count(x => x < 0);
			string text = "found something in " + found + " of " + n + " search" + (n == 1 ? "" : "es") + (failed > 0 ? ", " + failed + " too slow / failed" : "");
			return text;
		}

		/// true when the site has had enough searches and is not pulling its weight
		public static bool NotWorthIt(string site, out string why)
		{
			why = "";
			List<int> list;
			lock (Lock)
			{
				if (!Sites.TryGetValue(site ?? "", out list) || list.Count < Enough) return false;
				list = list.ToList();
			}
			int n = list.Count, found = list.Count(x => x > 0), failed = list.Count(x => x < 0);
			if (found == 0) why = "never found anything";
			else if (failed * 2 >= n) why = "too slow / failing most of the time";
			else if (found * 5 < n) why = "rarely finds anything";
			return why.Length > 0;
		}
	}
}
