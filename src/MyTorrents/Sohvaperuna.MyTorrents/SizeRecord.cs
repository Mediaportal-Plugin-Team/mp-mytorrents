using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MediaPortal.Configuration;

namespace Sohvaperuna.MyTorrents
{
	/// <summary>
	/// 2026: a record of episode sizes (MyTorrents.sizes.txt next to MediaPortal.xml), to help choose a size limit:
	/// for every episode the Auto grabber looked for, the smallest real copy that was there, and the size of the copy
	/// it downloaded. Kept for a year.
	/// </summary>
	public static class SizeRecord
	{
		public sealed class Entry
		{
			public DateTime When;
			public string Show = "", Episode = "", SmallestCodec = "", ChosenTitle = "";
			public long SmallestMB, ChosenMB;
		}

		private static readonly object Lock = new object();

		/// set in tests
		public static string FileOverride;

		private static string File => FileOverride ?? Config.GetFile(Config.Dir.Config, "MyTorrents.sizes.txt");

		private static string Clean(string s) => (s ?? "").Replace("\t", " ").Replace("\r", " ").Replace("\n", " ");

		public static Dictionary<string, Entry> Load()
		{
			Dictionary<string, Entry> d = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
			try
			{
				if (!System.IO.File.Exists(File)) return d;
				foreach (string line in System.IO.File.ReadAllLines(File))
				{
					// key <tab> ticks <tab> show <tab> episode <tab> smallest MB <tab> codec <tab> chosen MB <tab> chosen title
					string[] f = line.Split('\t');
					long ticks, small, chosen;
					if (f.Length < 7 || !long.TryParse(f[1], out ticks)) continue;
					long.TryParse(f[4], out small);
					long.TryParse(f[6], out chosen);
					Entry e = new Entry { When = new DateTime(ticks), Show = f[2], Episode = f[3], SmallestMB = small, SmallestCodec = f[5], ChosenMB = chosen, ChosenTitle = f.Length > 7 ? f[7] : "" };
					if (DateTime.Now - e.When < TimeSpan.FromDays(365)) d[f[0]] = e;
				}
			}
			catch
			{
			}
			return d;
		}

		private static void Save(Dictionary<string, Entry> d)
		{
			try
			{
				System.IO.File.WriteAllLines(File, d.Select(kv => string.Join("\t", kv.Key, kv.Value.When.Ticks.ToString(CultureInfo.InvariantCulture), Clean(kv.Value.Show), Clean(kv.Value.Episode),
					kv.Value.SmallestMB.ToString(CultureInfo.InvariantCulture), Clean(kv.Value.SmallestCodec), kv.Value.ChosenMB.ToString(CultureInfo.InvariantCulture), Clean(kv.Value.ChosenTitle))));
			}
			catch
			{
			}
		}

		/// Notes what a search found: the smallest real copy (0 = none), and the downloaded one (0 MB = not downloaded).
		public static void Note(string showId, string show, string episode, long smallestMB, string smallestCodec, long chosenMB, string chosenTitle)
		{
			if (string.IsNullOrEmpty(episode)) return;
			lock (Lock)
			{
				Dictionary<string, Entry> d = Load();
				string key = Key(show, episode);
				Entry e;
				if (!d.TryGetValue(key, out e)) d[key] = e = new Entry();
				e.When = DateTime.Now;
				e.Show = show ?? "";
				e.Episode = episode.ToUpperInvariant();
				if (smallestMB > 0 && (e.SmallestMB <= 0 || smallestMB < e.SmallestMB))
				{
					e.SmallestMB = smallestMB;
					e.SmallestCodec = smallestCodec ?? "";
				}
				if (chosenMB > 0)
				{
					e.ChosenMB = chosenMB;
					e.ChosenTitle = chosenTitle ?? "";
				}
				Save(d);
			}
		}

		private static long MinMB()
		{
			try
			{
				return SeriesItem.MinSizeMB;
			}
			catch
			{
				return 100;
			}
		}

		private static string Key(string show, string episode) => (show ?? "").Trim().ToLowerInvariant() + "|" + (episode ?? "").ToUpperInvariant();

		private static readonly System.Text.RegularExpressions.Regex SearchLine = new System.Text.RegularExpressions.Regex(
			@"\] \[(?<show>.+?)\] (?<ep>S\d+E\d+): \d+ found.*?smallest (?<mb>\d+) MB \((?<codec>\w+), \d+ seeders\)(?:.*?downloading (?<title>.+))?$");
		private static readonly System.Text.RegularExpressions.Regex CopyLine = new System.Text.RegularExpressions.Regex(
			@"Finished downloads: copying (?<name>.+?) \((?<mb>[\d.]+) MB\)");

		/// The first time (no record yet): what MyTorrents.log already shows, so the report has something in it at once.
		public static void StartFromLog(string logFile)
		{
			lock (Lock)
			{
				try
				{
					if (System.IO.File.Exists(File) || !System.IO.File.Exists(logFile)) return;
					string[] lines;
					using (System.IO.FileStream fs = new System.IO.FileStream(logFile, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.ReadWrite))
					using (System.IO.StreamReader r = new System.IO.StreamReader(fs))
					{
						lines = r.ReadToEnd().Split('\n');
					}
					Dictionary<string, Entry> d = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
					Dictionary<string, string> titleToKey = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
					foreach (string raw in lines)
					{
						string line = raw.TrimEnd('\r');
						System.Text.RegularExpressions.Match m = SearchLine.Match(line);
						if (m.Success)
						{
							string key = Key(m.Groups["show"].Value, m.Groups["ep"].Value);
							Entry e;
							if (!d.TryGetValue(key, out e)) d[key] = e = new Entry { Show = m.Groups["show"].Value, Episode = m.Groups["ep"].Value.ToUpperInvariant() };
							e.When = DateTime.Now;
							long mb = long.Parse(m.Groups["mb"].Value);
							// a fake or a sample (logged before there was a smallest size) is not counted as the smallest copy
							if (mb >= MinMB() && (e.SmallestMB <= 0 || mb < e.SmallestMB))
							{
								e.SmallestMB = mb;
								e.SmallestCodec = m.Groups["codec"].Value;
							}
							if (m.Groups["title"].Success) titleToKey[Simple(m.Groups["title"].Value)] = key;
							continue;
						}
						m = CopyLine.Match(line);
						if (m.Success)
						{
							string name = Simple(m.Groups["name"].Value);
							string key = titleToKey.Where(t => name.StartsWith(t.Key) || t.Key.StartsWith(name)).Select(t => t.Value).FirstOrDefault();
							if (key != null)
							{
								d[key].ChosenMB = (long)Math.Round(double.Parse(m.Groups["mb"].Value, CultureInfo.InvariantCulture));
								d[key].ChosenTitle = m.Groups["name"].Value;
							}
						}
					}
					if (d.Count > 0) Save(d);
				}
				catch
				{
				}
			}
		}

		/// letters and digits only, lower case, without the file extension ("Tulsa.King.S04E02...mkv" -> "tulsakings04e02...")
		private static string Simple(string s)
		{
			s = System.Text.RegularExpressions.Regex.Replace(s ?? "", @"\.(mkv|mp4|avi)$", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
			s = System.Text.RegularExpressions.Regex.Replace(s, @"\[.*?\]", "");
			return new string(s.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
		}

		public static readonly long[] ReportLimits = { 300, 350, 400, 450, 500, 550, 600, 700, 800, 1000, 1500 };

		private static long Median(List<long> v)
		{
			if (v.Count == 0) return 0;
			v = v.OrderBy(x => x).ToList();
			return v.Count % 2 == 1 ? v[v.Count / 2] : (v[v.Count / 2 - 1] + v[v.Count / 2]) / 2;
		}

		/// The report, a line at a time (MediaPortal shows it as a list, MyTorrents Configuration as text).
		public static List<string> Report(long currentLimitMB)
		{
			Dictionary<string, Entry> d;
			lock (Lock)
			{
				d = Load();
			}
			List<string> lines = new List<string>();
			if (d.Count == 0)
			{
				lines.Add("No episodes recorded yet - the Auto grabber notes sizes as it searches.");
				return lines;
			}
			List<Entry> got = d.Values.Where(e => e.ChosenMB > 0).ToList();
			if (got.Count > 0)
			{
				lines.Add($"Downloaded: {got.Count} episode(s), average {(long)got.Average(e => e.ChosenMB)} MB, middle {Median(got.Select(e => e.ChosenMB).ToList())} MB ({got.Min(e => e.ChosenMB)}-{got.Max(e => e.ChosenMB)} MB)");
			}
			long min = MinMB();
			List<Entry> seen = d.Values.Where(e => e.SmallestMB > 0 && e.SmallestMB >= min).ToList();
			foreach (string codec in new[] { "x265", "x264" })
			{
				List<long> v = seen.Where(e => e.SmallestCodec == codec).Select(e => e.SmallestMB).ToList();
				if (v.Count > 0) lines.Add($"Smallest copy, {codec}: middle {Median(v)} MB ({v.Min()}-{v.Max()} MB, {v.Count} episodes)");
			}
			if (seen.Count > 0)
			{
				lines.Add($"Episodes with a copy that fits ({seen.Count} episodes looked for):");
				foreach (long limit in ReportLimits)
				{
					int fit = seen.Count(e => e.SmallestMB <= limit);
					lines.Add($"   {limit,5} MB:  {fit} of {seen.Count}  ({100 * fit / seen.Count}%)" + (limit == currentLimitMB ? "   <- your limit" : ""));
				}
				if (currentLimitMB > 0 && !ReportLimits.Contains(currentLimitMB))
				{
					int fit = seen.Count(e => e.SmallestMB <= currentLimitMB);
					lines.Add($"   {currentLimitMB,5} MB:  {fit} of {seen.Count}  ({100 * fit / seen.Count}%)   <- your limit");
				}
			}
			return lines;
		}
	}
}
