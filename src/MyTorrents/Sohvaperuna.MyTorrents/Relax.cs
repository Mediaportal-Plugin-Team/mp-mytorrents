using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MediaPortal.Configuration;

namespace Sohvaperuna.MyTorrents
{
	/// <summary>
	/// 2026 (1.3.42): when nothing good turns up for a long time -
	///  - minimum seeders relax step by step (4 -> 3 -> 2 -> 1), never below the lowest; back to normal for the next episode
	///  - 1080p fallback: with 720p chosen and no good 720p copy, a 1080p copy within the size limit plus a percentage
	/// When the Auto grabber first looked for each episode / season is kept in MyTorrents.first-looked.txt.
	/// </summary>
	public static class Relax
	{
		private static readonly object Lock = new object();
		private static Dictionary<string, DateTime> _first;

		private static string File => Config.GetFile(Config.Dir.Config, "MyTorrents.first-looked.txt");

		private static int Int(string key, int def, int min)
		{
			int n;
			return int.TryParse(Configuration.GetExtra(key, def.ToString()), out n) && n >= min ? n : def;
		}

		/// Lower the minimum seeders over time (setting "AutoGrabber.RelaxSeeds", on).
		public static bool SeedsOn => Configuration.GetExtra("AutoGrabber.RelaxSeeds", "true") != "false";

		/// Never below this many seeders (setting "AutoGrabber.LowestSeeders", 1).
		public static int LowestSeeders => Math.Max(1, Int("AutoGrabber.LowestSeeders", 1, 1));

		/// Start lowering after this many hours of looking (setting "AutoGrabber.RelaxAfterHours", 24).
		public static int AfterHours => Int("AutoGrabber.RelaxAfterHours", 24, 1);

		/// Then one seeder less every this many hours (setting "AutoGrabber.RelaxStepHours", 12).
		public static int StepHours => Int("AutoGrabber.RelaxStepHours", 12, 1);

		public static readonly int[] AfterChoices = { 6, 12, 24, 48, 72 };
		public static readonly int[] StepChoices = { 6, 12, 24 };
		public static readonly int[] LowestChoices = { 1, 2, 3 };

		/// 1080p allowed this much over the size limit when no good 720p copy is found (setting
		/// "AutoGrabber.Fallback1080Percent", 0 = off).
		public static int FallbackPercent => Int("AutoGrabber.Fallback1080Percent", 0, 0);

		public static readonly int[] FallbackChoices = { 0, 10, 20, 30, 50 };

		public static string FallbackText(int p) => p <= 0 ? "Off" : "Yes - up to " + p + "% over the size limit";

		public static string RelaxText() => SeedsOn
			? "after " + AfterHours + " hours, 1 less every " + StepHours + " hours, never under " + LowestSeeders
			: "Off";

		private static void Load()
		{
			if (_first != null) return;
			_first = new Dictionary<string, DateTime>();
			try
			{
				if (!System.IO.File.Exists(File)) return;
				foreach (string line in System.IO.File.ReadAllLines(File))
				{
					string[] f = line.Split('\t');
					DateTime d;
					if (f.Length == 2 && DateTime.TryParseExact(f[1], "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out d)) _first[f[0]] = d;
				}
			}
			catch
			{
			}
		}

		private static void Save()
		{
			try
			{
				// forget ones looked for more than 60 days ago
				foreach (string k in _first.Where(p => DateTime.Now - p.Value > TimeSpan.FromDays(60)).Select(p => p.Key).ToList()) _first.Remove(k);
				System.IO.File.WriteAllLines(File, _first.Select(p => p.Key + "\t" + p.Value.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)));
			}
			catch
			{
			}
		}

		/// When the Auto grabber first looked for this (noted now if it never did).
		public static DateTime FirstLooked(string key)
		{
			lock (Lock)
			{
				Load();
				DateTime d;
				if (_first.TryGetValue(key, out d)) return d;
				_first[key] = DateTime.Now;
				Save();
				return DateTime.Now;
			}
		}

		/// Found and downloading - the next one starts from the normal settings again.
		public static void Done(string key)
		{
			lock (Lock)
			{
				Load();
				if (_first.Remove(key)) Save();
			}
		}

		/// The minimum seeders to use now; note = "" or e.g. "nothing with 4+ seeds for a day - now accepting 2+".
		public static int SeedsFor(string key, out string note)
		{
			note = "";
			int normal = SeriesItem.MinSeeders;
			DateTime first = FirstLooked(key);
			if (!SeedsOn || normal <= LowestSeeders) return normal;
			double hours = (DateTime.Now - first).TotalHours;
			if (hours < AfterHours) return normal;
			int steps = 1 + (int)((hours - AfterHours) / StepHours);
			int now = Math.Max(LowestSeeders, normal - steps);
			if (now < normal) note = "nothing with " + normal + "+ seeds for " + Since(first) + " - now accepting " + now + "+";
			return now;
		}

		private static string Since(DateTime first)
		{
			TimeSpan t = DateTime.Now - first;
			if (t.TotalHours < 36) return (int)t.TotalHours + " hours";
			return (int)Math.Round(t.TotalDays) + " days";
		}
	}
}
