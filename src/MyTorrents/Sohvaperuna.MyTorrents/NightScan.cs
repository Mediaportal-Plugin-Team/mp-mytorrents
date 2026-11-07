using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using MediaPortal.Configuration;
using MediaPortal.GUI.Library;
using MediaPortal.Player;

namespace Sohvaperuna.MyTorrents
{
	/// <summary>
	/// 2026 (1.3.47): one quiet MP-TVSeries scan at night. MyTorrents hands MP-TVSeries every episode it
	/// files, so MP-TVSeries' scan at start-up and its "scan remote shares every few minutes" can be turned off; this
	/// catches what was done by hand (files copied in, renamed, moved or deleted). Only between 2 and 5 am, only when
	/// nothing is playing, and only the folder scan (the online update runs at its own time). Folders with Scan
	/// unticked in MP-TVSeries (4.8.0.5) are skipped and keep their episodes.
	/// </summary>
	public static class NightScan
	{
		public static readonly string[] Choices = { "nightly", "weekly", "never" };

		public static string Mode
		{
			get
			{
				string m = Configuration.GetExtra("TV.NightScan", "nightly");
				return Choices.Contains(m) ? m : "nightly";
			}
		}

		public static string Text(string mode) => mode == "weekly" ? "Once a week (Sunday night)" : mode == "never" ? "Never - I scan in MP-TVSeries myself" : "Every night";

		private static string File => Config.GetFile(Config.Dir.Config, "MyTorrents.night-scan.txt");

		private static DateTime _lastTry = DateTime.MinValue;

		/// Called from the Auto grabber's regular round (inside MediaPortal only).
		public static void Check()
		{
			try
			{
				if (!MyTorrents.InMediaPortal || Mode == "never") return;
				DateTime now = DateTime.Now;
				if (now.Hour < 2 || now.Hour >= 5) return;
				if (Mode == "weekly" && now.DayOfWeek != DayOfWeek.Monday) return; // early Monday = Sunday night
				if ((now - _lastTry).TotalMinutes < 15) return;
				_lastTry = now;
				if (LastScan().Date == now.Date) return;
				if (g_Player.Playing) return; // never while you're watching
				if (Ask()) System.IO.File.WriteAllText(File, now.ToString("o"));
			}
			catch (Exception ex)
			{
				Log.Instance().Print("Night scan: " + ex.Message);
			}
		}

		private static DateTime LastScan()
		{
			try
			{
				return System.IO.File.Exists(File) ? DateTime.Parse(System.IO.File.ReadAllText(File).Trim(), null, System.Globalization.DateTimeStyles.RoundtripKind) : DateTime.MinValue;
			}
			catch
			{
				return DateTime.MinValue;
			}
		}

		/// Asks MP-TVSeries for a folder scan (no online update). True when it was queued.
		private static bool Ask()
		{
			Assembly asm = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "MP-TVSeries");
			Type paramsType = asm?.GetType("WindowPlugins.GUITVSeries.CParsingParameters");
			object plugin = GUIWindowManager.GetWindow(9811);
			FieldInfo queueField = plugin?.GetType().GetField("m_parserUpdaterQueue", BindingFlags.NonPublic | BindingFlags.Instance);
			if (paramsType == null || queueField == null) return false;
			object p = Activator.CreateInstance(paramsType, true, false);
			object queue = queueField.GetValue(plugin);
			lock (queue)
			{
				if (((IList)queue).Count > 0) return false; // something else is waiting - try again later tonight
				((IList)queue).Add(p);
			}
			Log.Instance().Print("Night scan: asked MP-TVSeries to scan its folders (" + Text(Mode).ToLowerInvariant() + ")");
			return true;
		}
	}
}
