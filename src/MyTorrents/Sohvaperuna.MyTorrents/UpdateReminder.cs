using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using MediaPortal.Configuration;
using MediaPortal.GUI.Library;

namespace Sohvaperuna.MyTorrents
{
	/// <summary>
	/// 2026: once a month, quietly asks "MyTorrents Configuration.exe /check-updates" whether the torrent program, Jackett
	/// or FlareSolverr have updates, and mentions it once the next time MyTorrents is opened in MediaPortal.
	/// </summary>
	public static class UpdateReminder
	{
		private static string File => Config.GetFile(Config.Dir.Config, "MyTorrents.updates.txt");
		private static string _pending;

		/// Called at start-up: a background check when the last one is a month old.
		public static void Start()
		{
			ThreadPool.QueueUserWorkItem(_ =>
			{
				try
				{
					Thread.Sleep(TimeSpan.FromMinutes(2)); // never slow MediaPortal's start-up
					if (DaysSinceCheck() >= 999)
					{
						// 2026: first start after setup - the assistant has just installed the latest versions
						System.IO.File.AppendAllText(File, "checked=" + DateTime.Now.ToString("yyyy-MM-dd") + Environment.NewLine);
					}
					else if (DaysSinceCheck() >= 30)
					{
						IntPtr before = GetForegroundWindow();
						string exe = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MyTorrents Configuration.exe");
						if (System.IO.File.Exists(exe))
						{
							Log.Instance().Print("Checking for updates to the torrent program, Jackett and FlareSolverr");
							using (Process p = Process.Start(new ProcessStartInfo(exe, "/check-updates") { UseShellExecute = false, CreateNoWindow = true }))
							{
								p.WaitForExit(10 * 60 * 1000);
							}
							KeepMediaPortalInFront(before);
						}
					}
					string[] updates = Read().Where(l => l.StartsWith("update=")).Select(l => l.Substring(7)).ToArray();
					if (updates.Length > 0 && !Read().Contains("shown=" + string.Join(",", updates)))
					{
						_pending = string.Join(", ", updates);
					}
				}
				catch (Exception ex)
				{
					Log.Instance().Print("Update check: " + ex.Message);
				}
			});
		}

		[System.Runtime.InteropServices.DllImport("user32.dll")]
		private static extern IntPtr GetForegroundWindow();

		[System.Runtime.InteropServices.DllImport("user32.dll")]
		private static extern bool SetForegroundWindow(IntPtr hWnd);

		/// 2026: a background job must never leave the remote talking to another window - if MediaPortal was in
		/// front before and is not now, put it back.
		public static void KeepMediaPortalInFront(IntPtr before)
		{
			try
			{
				IntPtr mp = GUIGraphicsContext.form != null ? GUIGraphicsContext.form.Handle : IntPtr.Zero;
				if (mp == IntPtr.Zero || before != mp || GetForegroundWindow() == mp) return;
				Log.Instance().Print("Another window took the front - putting MediaPortal back");
				GUIWindowManager.SendThreadCallback((p1, p2, data) =>
				{
					try
					{
						SetForegroundWindow(mp);
						GUIGraphicsContext.form.Activate();
					}
					catch
					{
					}
					return 0;
				}, 0, 0, null);
			}
			catch
			{
			}
		}

		/// Called when MyTorrents opens: show the reminder once.
		public static void ShowIfDue()
		{
			string text = _pending;
			if (text == null) return;
			_pending = null;
			try
			{
				System.IO.File.AppendAllText(File, "shown=" + text.Replace(", ", ",") + Environment.NewLine);
			}
			catch
			{
			}
			GUIWindowManager.SendThreadCallback((p1, p2, data) =>
			{
				MyTorrents.Error("Updates available",
					text + "\nOn this PC open MyTorrents Configuration (desktop icon) and click \"Check for updates\".");
				return 0;
			}, 0, 0, null);
		}

		private static string[] Read()
		{
			try
			{
				return System.IO.File.Exists(File) ? System.IO.File.ReadAllLines(File) : new string[0];
			}
			catch
			{
				return new string[0];
			}
		}

		private static double DaysSinceCheck()
		{
			string line = Read().FirstOrDefault(l => l.StartsWith("checked="));
			DateTime d;
			return line != null && DateTime.TryParse(line.Substring(8), out d) ? (DateTime.Now - d).TotalDays : 999;
		}
	}
}
