using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace Sohvaperuna.MyTorrentsConfig
{
	/// <summary>
	/// "MyTorrents Configuration" - opens the MyTorrents settings without starting MediaPortal
	/// or MediaPortal Configuration (like the Trakt / Moving Pictures / MP-TVSeries config tools).
	/// Lives in the MediaPortal folder; the plugin DLLs are found in plugins\windows.
	/// </summary>
	internal static class Program
	{
		/// Arguments:
		///   (none)               the settings window
		///   /setup               setup assistant - full setup the first time, then only update checks
		///   /setup full          setup assistant, full setup
		///   /sites               add Jackett sites (the "Add sites..." button)
		///   /check-updates       no windows: note available updates in MyTorrents.updates.txt (monthly, from MediaPortal)
		///   /admin tasks.txt     the steps that need administrator rights (started by the assistant)
		///   /start-flaresolverr  starts FlareSolverr hidden (run at Windows sign-in)
		///   /downloads           just the torrent list
		[STAThread]
		private static int Main(string[] args)
		{
			AppDomain.CurrentDomain.AssemblyResolve += ResolveFromMediaPortal;
			string mode = args.Length > 0 ? args[0].TrimStart('/', '-').ToLowerInvariant() : "";
			if (mode == "start-flaresolverr")
			{
				try
				{
					SetupCore.StartFlareSolverr();
				}
				catch
				{
				}
				return 0;
			}
			if (mode == "check-updates")
			{
				return SetupCore.CheckUpdatesQuietly();
			}
			if (mode == "helper") // 2026 (1.3.45): the background helper
			{
				Application.EnableVisualStyles();
				return HelperHost.Run();
			}
			if (mode == "web-firewall") // 2026 (1.3.45): let the laptop reach the web page (asks Windows for permission)
			{
				SetupCore.RunAdmin(new System.Collections.Generic.List<string> { "firewall-web|" + (args.Length > 1 ? args[1] : "9118") });
				return 0;
			}
			if (mode == "admin" && args.Length > 1)
			{
				return AdminTasks.RunFile(args[1]);
			}
			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);
			if (mode == "sites")
			{
				try
				{
					return SitePicker.RunStandalone();
				}
				catch (Exception ex)
				{
					MessageBox.Show("Could not add sites:\r\n\r\n" + ex.Message, "Add search sites", MessageBoxButtons.OK, MessageBoxIcon.Error);
					return 1;
				}
			}
			if (mode == "remove") // 2026 (1.3.42): "Remove MyTorrents"
			{
				try
				{
					RunRemove();
				}
				catch (Exception ex)
				{
					MessageBox.Show("Could not start Uninstall MyTorrents:\r\n\r\n" + ex.Message, "Uninstall MyTorrents", MessageBoxButtons.OK, MessageBoxIcon.Error);
					return 1;
				}
				return 0;
			}
			if (mode == "setup")
			{
				try
				{
					bool full = args.Length > 1 && args[1].TrimStart('/', '-').Equals("full", StringComparison.OrdinalIgnoreCase);
					RunSetup(full);
				}
				catch (Exception ex)
				{
					MessageBox.Show("MyTorrents setup could not start:\r\n\r\n" + ex.Message, "MyTorrents setup", MessageBoxButtons.OK, MessageBoxIcon.Error);
					return 1;
				}
				return 0;
			}
			try
			{
				// 2026: the settings have their own file now (MyTorrents.settings.xml), so they can be changed while
				// MediaPortal is running - it picks the change up straight away. "/downloads" opens just the torrent list.
				if (mode == "downloads" && !ShowDownloads(false))
				{
					return 0;
				}
				ShowSettings();
				return 0;
			}
			catch (Exception ex)
			{
				MessageBox.Show("MyTorrents Configuration could not start:\r\n\r\n" + ex.Message +
					"\r\n\r\nIt must be in the MediaPortal folder, with MyTorrents installed.", "MyTorrents Configuration", MessageBoxButtons.OK, MessageBoxIcon.Error);
				return 1;
			}
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private static void RunRemove()
		{
			using (RemoveForm form = new RemoveForm())
			{
				Application.Run(form);
			}
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private static void RunSetup(bool full)
		{
			bool done = Sohvaperuna.MyTorrents.Configuration.GetExtra("Setup.Done", "") == "true";
			using (SetupForm form = new SetupForm(done && !full))
			{
				Application.Run(form);
			}
		}

		// Kept separate so MyTorrents.dll is only loaded after the assembly resolver is in place.
		[MethodImpl(MethodImplOptions.NoInlining)]
		private static void ShowSettings()
		{
			Sohvaperuna.MyTorrents.Configuration configuration = Sohvaperuna.MyTorrents.Configuration.Instance();
			using (Sohvaperuna.MyTorrents.SettingsForm form = new Sohvaperuna.MyTorrents.SettingsForm(ref configuration))
			{
				form.Text = "MyTorrents Configuration";
				form.StartPosition = FormStartPosition.CenterScreen;
				form.ShowInTaskbar = true;
				try
				{
					form.Icon = Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location);
				}
				catch
				{
				}
				if (form.ShowDialog() == DialogResult.OK)
				{
					configuration.Save();
				}
			}
		}

		/// The torrent list on its own. Returns true when "Settings..." was clicked.
		[MethodImpl(MethodImplOptions.NoInlining)]
		private static bool ShowDownloads(bool mediaPortalRunning)
		{
			bool settings = false;
			using (Form form = new Form())
			{
				form.Text = "MyTorrents Downloads";
				form.StartPosition = FormStartPosition.CenterScreen;
				form.ClientSize = new Size(900, 520);
				form.MinimumSize = new Size(600, 300);
				form.Font = new Font("Microsoft Sans Serif", 8.25f);
				try
				{
					form.Icon = Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location);
				}
				catch
				{
				}
				TabControl tabs = new TabControl { Dock = DockStyle.Fill };
				tabs.TabPages.Add(new Sohvaperuna.MyTorrents.DownloadsPage());
				Panel bottom = new Panel { Dock = DockStyle.Bottom, Height = 36 };
				Button open = new Button { Text = "Settings...", Size = new Size(100, 23), Location = new Point(8, 7) };
				open.Click += (s, e) => { settings = true; form.Close(); };
				Button close = new Button { Text = "Close", Size = new Size(80, 23), Anchor = AnchorStyles.Top | AnchorStyles.Right };
				close.Location = new Point(bottom.Width - close.Width - 8, 7);
				close.Click += (s, e) => form.Close();
				bottom.Resize += (s, e) => close.Left = bottom.ClientSize.Width - close.Width - 8;
				Label note = new Label
				{
					Text = "",
					AutoSize = true, ForeColor = Color.DimGray, Location = new Point(open.Right + 12, 12)
				};
				bottom.Controls.Add(open);
				bottom.Controls.Add(note);
				bottom.Controls.Add(close);
				form.Controls.Add(tabs);
				form.Controls.Add(bottom);
				form.CancelButton = close;
				Application.Run(form);
			}
			return settings;
		}

		private static Assembly ResolveFromMediaPortal(object sender, ResolveEventArgs args)
		{
			string name = new AssemblyName(args.Name).Name + ".dll";
			string baseDir = AppDomain.CurrentDomain.BaseDirectory;
			foreach (string dir in new[] { baseDir, Path.Combine(baseDir, "plugins", "windows"), Path.Combine(baseDir, "plugins", "process") })
			{
				string path = Path.Combine(dir, name);
				if (File.Exists(path))
				{
					return Assembly.LoadFrom(path);
				}
			}
			return null;
		}
	}
}
