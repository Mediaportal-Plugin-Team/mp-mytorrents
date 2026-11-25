using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Sohvaperuna.MyTorrents
{
	/// <summary>
	/// 1.3.76: what is already in the torrent program - added by another program that uses the same torrent program
	/// (Sonarr, Radarr, MyTorrents on another PC...) or by hand. MyTorrents doesn't download the same episode, season
	/// or movie again while it's there. Counted: still downloading, or added in the last 3 days (finished ones being
	/// filed by the other program).
	/// </summary>
	public static class InTorrentProgram
	{
		public static List<string> Names()
		{
			try
			{
				List<Sohvaperuna.TorrentEngine.Torrent> all = TorrentEngine.Instance()?.TorrentsAll;
				if (all == null) return new List<string>();
				long since = DateTimeOffset.UtcNow.AddDays(-3).ToUnixTimeSeconds();
				return all.Where(t => t != null && !string.IsNullOrEmpty(t.Name) && (t.Progress < 100 || t.AddedOn >= since))
					.Select(t => t.Name).ToList();
			}
			catch
			{
				return new List<string>();
			}
		}

		/// A movie: its name and year are in the torrent's name ("The.Thing.1982.1080p...").
		public static string Movie(string title, string year)
		{
			string key = MediaNames.Key(title);
			if (key.Length < 3) return null;
			foreach (string n in Names())
			{
				Match y = Regex.Match(n, @"(?:^|[\s._\-\(\[])((?:19|20)\d{2})(?:[\s._\-\)\]]|$)");
				if (!y.Success) continue;
				if (year.Length > 0 && y.Groups[1].Value != year) continue;
				if (MediaNames.Key(n.Substring(0, y.Index)) == key) return n;
			}
			return null;
		}
	}
}
