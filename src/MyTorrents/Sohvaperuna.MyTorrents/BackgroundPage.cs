using System;
using System.Diagnostics;
using System.Drawing;
using System.Net;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace Sohvaperuna.MyTorrents
{
	/// <summary>
	/// 2026 (1.3.45): "Background" tab - keep working when MediaPortal is closed (the background helper), and the
	/// web page for the laptop / desktop / phone.
	/// </summary>
	public class BackgroundPage : TabPage
	{
		public const string RunValue = "MyTorrents (keeps working when MediaPortal is closed)";
		private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

		private readonly CheckBox _helper = new CheckBox { AutoSize = true, Text = "Keep working when MediaPortal is closed (starts with Windows, small icon by the clock)" };
		private readonly Label _helperState = new Label { AutoSize = true, ForeColor = Color.DimGray };
		private readonly CheckBox _web = new CheckBox { AutoSize = true, Text = "Let me use MyTorrents from other computers and phones on my home network" };
		private readonly NumericUpDown _port = new NumericUpDown { Minimum = 1024, Maximum = 65535, Width = 70 };
		private readonly TextBox _password = new TextBox { Width = 160, UseSystemPasswordChar = true };
		private readonly Label _address = new Label { AutoSize = true, Font = new Font("Microsoft Sans Serif", 9f, FontStyle.Bold) };
		private readonly Button _firewall = new Button { Text = "Allow it through Windows Firewall...", AutoSize = true };
		private readonly Button _open = new Button { Text = "Open the web page", AutoSize = true };

		private static string HelperExe => System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MyTorrents Configuration.exe");

		public BackgroundPage()
		{
			Text = "Background";
			Padding = new Padding(3);
			UseVisualStyleBackColor = true;
			Font = new Font("Microsoft Sans Serif", 8.25f);

			GroupBox h = new GroupBox { Text = "When MediaPortal is closed", Location = new Point(8, 8), Size = new Size(548, 118) };
			Controls.Add(h);
			_helper.Location = new Point(12, 22);
			_helper.Checked = RunEntry() != null;
			h.Controls.Add(_helper);
			h.Controls.Add(new Label
			{
				Text = "Then the Auto grabber, Wanted movies, your Trakt list and copying finished downloads into your library keep going while MediaPortal is shut. " +
					"While MediaPortal is open it does the work itself - only one of them works at a time.",
				Location = new Point(30, 44), Size = new Size(505, 44), ForeColor = Color.DimGray
			});
			_helperState.Location = new Point(30, 92);
			h.Controls.Add(_helperState);

			GroupBox w = new GroupBox { Text = "Web page (laptop, desktop, phone)", Location = new Point(8, 132), Size = new Size(548, 196) };
			Controls.Add(w);
			_web.Location = new Point(12, 22);
			_web.Checked = WebServer.On;
			w.Controls.Add(_web);
			w.Controls.Add(new Label { Text = "Port", Location = new Point(30, 51), AutoSize = true });
			_port.Location = new Point(120, 48);
			_port.Value = WebServer.Port;
			w.Controls.Add(_port);
			w.Controls.Add(new Label { Text = "Password", Location = new Point(30, 79), AutoSize = true });
			_password.Location = new Point(120, 76);
			_password.Text = WebServer.Password;
			w.Controls.Add(_password);
			w.Controls.Add(new Label { Text = "(empty = none; the user name is mytorrents)", Location = new Point(_password.Right + 8, 79), AutoSize = true, ForeColor = Color.DimGray });
			w.Controls.Add(new Label { Text = "Address", Location = new Point(30, 107), AutoSize = true });
			_address.Location = new Point(120, 106);
			w.Controls.Add(_address);
			_firewall.Location = new Point(30, 136);
			_open.Location = new Point(250, 136);
			w.Controls.Add(_firewall);
			w.Controls.Add(_open);
			w.Controls.Add(new Label { Text = "Home network only - it doesn't answer the internet.", Location = new Point(30, 170), AutoSize = true, ForeColor = Color.DimGray });
			_firewall.Click += (s, e) => Run("/web-firewall " + (int)_port.Value);
			// 2026 (1.3.47): the web page comes from the background helper - if it isn't running yet, save and start it
			// first (no need to press Ok and open Configuration again), then wait for the page to answer.
			_open.Click += (s, e) => OpenWebPage();
			_web.CheckedChanged += (s, e) => { if (_web.Checked) _helper.Checked = true; Refresh(); };
			_helper.CheckedChanged += (s, e) => { if (!_helper.Checked) _web.Checked = false; Refresh(); };
			_port.ValueChanged += (s, e) => Refresh();
			VisibleChanged += (s, e) => { if (Visible) Refresh(); };
			// 2026 (1.3.50): turned on but not running (e.g. stopped by the installer to update MyTorrents) - start it again
			if (_helper.Checked && !HelperRunning() && System.IO.File.Exists(HelperExe)) Run("/helper");
			Refresh();
		}

		public new void Refresh()
		{
			_address.Text = "http://" + Dns.GetHostName().ToLowerInvariant() + ":" + (int)_port.Value;
			_port.Enabled = _password.Enabled = _firewall.Enabled = _web.Checked;
			bool running = HelperRunning();
			_helperState.Text = running ? "The background helper is running." : _helper.Checked ? "It starts when you click OK." : "Off.";
			_open.Enabled = _web.Checked;
		}

		private void OpenWebPage()
		{
			int port = (int)_port.Value;
			if (!HelperRunning() || WebServer.On != _web.Checked || WebServer.Port != port || WebServer.Password != (_password.Text ?? ""))
			{
				Save(); // saves the web settings and (re)starts the background helper
				_helperState.Text = "Starting the background helper...";
			}
			Cursor.Current = Cursors.WaitCursor;
			bool up = false;
			for (int i = 0; i < 40 && !up; i++)
			{
				up = Answers(port);
				if (!up)
				{
					Application.DoEvents();
					Thread.Sleep(500);
				}
			}
			Cursor.Current = Cursors.Default;
			Refresh();
			if (!up)
			{
				MessageBox.Show(this, HelperRunning()
						? "The background helper is running but the web page didn't answer on port " + port + ". Another program may be using that port - try another port number (e.g. 9120), then Open the web page again."
						: "The background helper didn't start, so there is no web page yet.\r\n\r\nThe reason is written at the end of MyTorrents.log (MediaPortal's log folder) - send it to Claude.",
					"Web page", MessageBoxButtons.OK, MessageBoxIcon.Warning);
				return;
			}
			try
			{
				Process.Start(new ProcessStartInfo("http://localhost:" + port + "/") { UseShellExecute = true });
			}
			catch (Exception ex)
			{
				MessageBox.Show(this, "Could not open the browser: " + ex.Message + "\r\n\r\nType http://localhost:" + port + " into your browser instead.", "Web page");
			}
		}

		private static bool Answers(int port)
		{
			try
			{
				using (System.Net.Sockets.TcpClient c = new System.Net.Sockets.TcpClient())
				{
					IAsyncResult r = c.BeginConnect("127.0.0.1", port, null, null);
					bool ok = r.AsyncWaitHandle.WaitOne(400) && c.Connected;
					return ok;
				}
			}
			catch
			{
				return false;
			}
		}

		public static bool HelperRunning()
		{
			try
			{
				using (Mutex m = Mutex.OpenExisting("MyTorrentsHelper"))
				{
					return true;
				}
			}
			catch
			{
				return false;
			}
		}

		private static string RunEntry()
		{
			try
			{
				using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey))
				{
					return k?.GetValue(RunValue) as string;
				}
			}
			catch
			{
				return null;
			}
		}

		/// 2026 (1.3.57): MediaPortal starts the background helper if it is switched on but not running - after an
		/// update the installer stops it, and nobody should have to open MyTorrents Configuration to get it going again
		public static void EnsureHelperRunning()
		{
			try
			{
				if (RunEntry() == null || HelperRunning() || !System.IO.File.Exists(HelperExe)) return;
				Process.Start(new ProcessStartInfo(HelperExe, "/helper") { UseShellExecute = true, WorkingDirectory = System.IO.Path.GetDirectoryName(HelperExe) });
				Log.Instance().Print("Background helper wasn't running - started it");
			}
			catch (Exception ex)
			{
				Log.Instance().Print("Background helper could not be started: " + ex.Message);
			}
		}

		private static void Run(string args)
		{
			try
			{
				Process.Start(new ProcessStartInfo(HelperExe, args) { UseShellExecute = true });
			}
			catch (Exception ex)
			{
				MessageBox.Show("Could not start it: " + ex.Message, "MyTorrents");
			}
		}

		/// Stops the helper (it saves first).
		public static void StopHelper()
		{
			try
			{
				using (EventWaitHandle e = EventWaitHandle.OpenExisting("MyTorrentsHelperStop"))
				{
					e.Set();
				}
				for (int i = 0; i < 40 && HelperRunning(); i++) Thread.Sleep(250);
			}
			catch
			{
			}
		}

		public void Save()
		{
			bool webBefore = WebServer.On;
			int portBefore = WebServer.Port;
			string pwBefore = WebServer.Password;
			Configuration.SetExtra("Web.On", _web.Checked ? "true" : "false");
			Configuration.SetExtra("Web.Port", ((int)_port.Value).ToString());
			Configuration.SetExtra("Web.Password", _password.Text ?? "");
			try
			{
				using (RegistryKey k = Registry.CurrentUser.CreateSubKey(RunKey))
				{
					if (_helper.Checked) k.SetValue(RunValue, "\"" + HelperExe + "\" /helper");
					else k.DeleteValue(RunValue, false);
				}
			}
			catch
			{
			}
			bool changed = webBefore != _web.Checked || portBefore != (int)_port.Value || pwBefore != (_password.Text ?? "");
			if (!_helper.Checked)
			{
				if (HelperRunning()) StopHelper();
				return;
			}
			if (HelperRunning() && changed) StopHelper(); // restarted so it picks up the web settings
			if (!HelperRunning()) Run("/helper");
		}
	}
}
