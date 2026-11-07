using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace Sohvaperuna.MyTorrentsConfig
{
	/// <summary>
	/// 2026 (1.3.45): "MyTorrents Configuration.exe /helper" - the background helper. Starts with Windows (when turned
	/// on), sits as a small tray icon, and does MyTorrents' work while MediaPortal is closed: the Auto grabber, Wanted
	/// movies, the Trakt list, copying finished downloads into the library. While MediaPortal runs it pauses (only one
	/// of them works at a time) - but keeps serving the web page.
	/// </summary>
	internal static class HelperHost
	{
		public const string RunValue = "MyTorrents (keeps working when MediaPortal is closed)";
		public const string StopEventName = "MyTorrentsHelperStop";

		public static int Run()
		{
			bool first;
			using (Mutex one = new Mutex(true, "MyTorrentsHelper", out first))
			{
				if (!first) return 0; // already running
				using (EventWaitHandle stop = new EventWaitHandle(false, EventResetMode.ManualReset, StopEventName))
				{
					Start();
					NotifyIcon tray = new NotifyIcon { Text = "MyTorrents - working in the background", Visible = true };
					try
					{
						tray.Icon = Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location);
					}
					catch
					{
						tray.Icon = SystemIcons.Application;
					}
					ContextMenuStrip menu = new ContextMenuStrip();
					ToolStripItem state = menu.Items.Add("Starting...");
					state.Enabled = false;
					menu.Items.Add("Open the web page", null, (s, e) => Open("http://localhost:" + Web.Port + "/"));
					menu.Items.Add("MyTorrents Configuration", null, (s, e) => Open(Assembly.GetExecutingAssembly().Location));
					menu.Items.Add(new ToolStripSeparator());
					menu.Items.Add("Stop the background helper", null, (s, e) => stop.Set());
					tray.ContextMenuStrip = menu;
					System.Windows.Forms.Timer t = new System.Windows.Forms.Timer { Interval = 5000 };
					bool? wasRunning = null;
					t.Tick += (s, e) =>
					{
						if (stop.WaitOne(0))
						{
							t.Stop();
							tray.Visible = false;
							Shutdown();
							Application.Exit();
							return;
						}
						bool mp = Process.GetProcessesByName("MediaPortal").Length > 0;
						if (wasRunning != mp)
						{
							wasRunning = mp;
							Watch(mp);
							state.Text = mp ? "MediaPortal is running - it does the work" : "MediaPortal is closed - working in the background";
							tray.Text = mp ? "MyTorrents - waiting (MediaPortal is running)" : "MyTorrents - working in the background";
						}
					};
					t.Start();
					Application.Run();
					tray.Dispose();
				}
			}
			return 0;
		}

		private static string LogFolder()
		{
			string pd = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
			string dir = Path.Combine(pd, "Team MediaPortal", "MediaPortal", "log");
			return Directory.Exists(dir) ? dir : AppDomain.CurrentDomain.BaseDirectory;
		}

		private static void Open(string what)
		{
			try
			{
				Process.Start(new ProcessStartInfo(what) { UseShellExecute = true });
			}
			catch
			{
			}
		}

		// kept separate so MyTorrents.dll loads after the assembly resolver is in place
		[MethodImpl(MethodImplOptions.NoInlining)]
		private static void Start()
		{
			Sohvaperuna.MyTorrents.Log.Prefix = "[helper] ";
			Sohvaperuna.MyTorrents.Log.Instance().Print("Background helper started");
			Sohvaperuna.TorrentEngine.ClientSupport.EnableModernTls();
			// MP-TVSeries (the TV library) and the Trakt plugin's sign-in, as MediaPortal would load them
			try
			{
				Assembly.Load("MP-TVSeries");
			}
			catch (Exception ex)
			{
				Sohvaperuna.MyTorrents.Log.Instance().Print("Background helper: MP-TVSeries could not be loaded - TV shows are left to MediaPortal: " + ex.Message);
			}
			try
			{
				Type ts = Assembly.Load("TraktPlugin").GetType("TraktPlugin.TraktSettings");
				MethodInfo load = ts?.GetMethods(BindingFlags.Public | BindingFlags.Static).FirstOrDefault(m => m.Name == "LoadSettings");
				if (load != null) load.Invoke(null, load.GetParameters().Select(p => p.ParameterType == typeof(bool) ? (object)false : null).ToArray());
			}
			catch (Exception ex)
			{
				Sohvaperuna.MyTorrents.Log.Instance().Print("Background helper: no Trakt sign-in (" + (ex.InnerException ?? ex).Message + ") - the Trakt list waits for MediaPortal");
			}
			Sohvaperuna.MyTorrents.ConfigData config = Sohvaperuna.MyTorrents.Configuration.Instance().LoadConfiguration();
			try
			{
				Sohvaperuna.MyTorrents.TorrentEngine.Initialize(config.Server, Convert.ToInt32(config.Port), config.User, config.Password, config.Backend);
			}
			catch (Exception ex)
			{
				Sohvaperuna.MyTorrents.Log.Instance().Print("Background helper: can't reach the torrent program yet: " + ex.Message);
			}
			Sohvaperuna.MyTorrents.WatchList.Paused = Process.GetProcessesByName("MediaPortal").Length > 0;
			Sohvaperuna.MyTorrents.WatchList wl = new Sohvaperuna.MyTorrents.WatchList();
			Sohvaperuna.MyTorrents.MyTorrents.HelperWatchlist = wl;
			wl.Load();
			Sohvaperuna.MyTorrents.PostProcessor.Start();
			Sohvaperuna.MyTorrents.WebServer.Start();
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private static void Watch(bool mediaPortalRunning)
		{
			Sohvaperuna.MyTorrents.WatchList wl = Sohvaperuna.MyTorrents.MyTorrents.HelperWatchlist;
			if (mediaPortalRunning)
			{
				if (!Sohvaperuna.MyTorrents.WatchList.Paused)
				{
					try
					{
						wl?.Save(); // MediaPortal reads the list as it starts
					}
					catch
					{
					}
					Sohvaperuna.MyTorrents.Log.Instance().Print("Background helper: MediaPortal started - it does the work now");
				}
				Sohvaperuna.MyTorrents.WatchList.Paused = true;
				return;
			}
			if (Sohvaperuna.MyTorrents.WatchList.Paused)
			{
				try
				{
					wl?.Load(); // what MediaPortal changed
					wl?.PullSeriesSoon();
				}
				catch
				{
				}
				Sohvaperuna.MyTorrents.Log.Instance().Print("Background helper: MediaPortal is closed - working in the background");
			}
			Sohvaperuna.MyTorrents.WatchList.Paused = false;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private static void Shutdown()
		{
			try
			{
				if (!Sohvaperuna.MyTorrents.WatchList.Paused) Sohvaperuna.MyTorrents.MyTorrents.HelperWatchlist?.Save();
				Sohvaperuna.MyTorrents.WebServer.Stop();
				Sohvaperuna.MyTorrents.Log.Instance().Print("Background helper stopped");
			}
			catch
			{
			}
		}

		private static class Web
		{
			public static int Port => Sohvaperuna.MyTorrents.WebServer.Port;
		}
	}
}
