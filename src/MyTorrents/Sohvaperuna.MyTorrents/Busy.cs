using System;
using System.Linq;

namespace Sohvaperuna.MyTorrents
{
	/// <summary>
	/// 2026 (1.3.50): the most downloads MyTorrents starts by itself at the same time (50 downloading,
	/// most stuck "Fetching details", after the Trakt list added 28 shows and 161 movies at once). While that many are
	/// still downloading, the Auto grabber and Wanted movies wait - nothing is lost, they carry on when some finish.
	/// What you ask for yourself (Get now, Look now, Search) never waits. Setting "AutoGrabber.MaxDownloading" (10).
	/// </summary>
	public static class Busy
	{
		public static readonly int[] Choices = { 5, 10, 15, 20, 30, 0 };

		public static int Max
		{
			get
			{
				int n;
				return int.TryParse(Configuration.GetExtra("AutoGrabber.MaxDownloading", "10"), out n) && n >= 0 ? n : 10;
			}
		}

		public static string Text(int n) => n == 0 ? "No limit" : n + " at a time";

		private static DateTime _lastSaid = DateTime.MinValue;

		/// True when the automatic searches should wait.
		public static bool TooBusy()
		{
			int max = Max;
			if (max <= 0) return false;
			try
			{
				int downloading = (TorrentEngine.Instance()?.TorrentsAll ?? new System.Collections.Generic.List<Sohvaperuna.TorrentEngine.Torrent>())
					.Count(t => t != null && t.Progress < 100);
				if (downloading < max) return false;
				if ((DateTime.Now - _lastSaid).TotalMinutes > 30)
				{
					_lastSaid = DateTime.Now;
					Log.Instance().Print("Busy: " + downloading + " downloads going (most at a time: " + max + ") - the Auto grabber and Wanted movies wait until some finish");
				}
				return true;
			}
			catch
			{
				return false;
			}
		}
	}
}
