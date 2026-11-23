using System;
using System.Collections.Generic;

namespace Sohvaperuna.MyTorrents
{
	public class Series
	{
		public string ID { get; set; }

		public string Name { get; set; }

		public string OriginalName { get; set; }

		public string Description { get; set; }

		public string Rating { get; set; }

		public string Genres { get; set; }

		public string Poster { get; set; }

		public string FirstUnlocal { get; set; }

		public string AllUnlocal { get; set; }

		/// 2026: episodes that have aired but are not on disk (and not watched): "S01E04|2026-10-04" entries
		public List<string> AiredMissing { get; set; } = new List<string>();

		/// 2026 (1.3.39): when each episode you don't have airs ("S28E05" -> UTC), so the date moves on with the
		/// next episode after a download (it used to keep the old episode's date)
		public Dictionary<string, DateTime> AirsByCode { get; set; } = new Dictionary<string, DateTime>();

		/// 2026 (1.3.41): how many episodes each season has (season number -> episodes), from MP-TVSeries
		public Dictionary<int, int> SeasonSizes { get; set; } = new Dictionary<int, int>();

		/// 2026 (1.3.41): when each season's last episode aired (DateTime.MaxValue = still airing / dates not known yet)
		public Dictionary<int, DateTime> SeasonLastAired { get; set; } = new Dictionary<int, DateTime>();

		/// The air date of an episode code (DateTime.MinValue when unknown).
		public DateTime AirsOf(string code) => code != null && AirsByCode.TryGetValue(code, out DateTime d) ? d : DateTime.MinValue;

		/// 2026: when the next episode (FirstUnlocal) airs, in UTC - DateTime.MinValue when MP-TVSeries does not know
		public DateTime NextAirsUtc { get; set; } = DateTime.MinValue;

		/// 2026: MP-TVSeries' status for the show ("Continuing", "Ended", ...)
		public string Status { get; set; } = "";

		/// 2026: one of the show's files (tells which library folder the show is in) - "" when none
		public string OneFile { get; set; } = "";

		/// MP-TVSeries air date ("2026-10-28") and the show's air time ("21:00" / "9:00 PM") -> UTC.
		/// The time is taken as US Eastern time (most shows on the sites are American); without a time, 9 PM is used.
		public static DateTime AirsUtc(string date, string time)
		{
			DateTime day;
			if (string.IsNullOrWhiteSpace(date) || !DateTime.TryParseExact(date.Trim(), "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out day))
			{
				return DateTime.MinValue;
			}
			TimeSpan at = new TimeSpan(21, 0, 0);
			DateTime t;
			if (!string.IsNullOrWhiteSpace(time) && DateTime.TryParse(time.Trim(), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.NoCurrentDateDefault, out t))
			{
				at = t.TimeOfDay;
			}
			DateTime eastern = day.Date + at;
			try
			{
				TimeZoneInfo et = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
				return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(eastern, DateTimeKind.Unspecified), et);
			}
			catch
			{
				return eastern.AddHours(5);
			}
		}
	}
}
