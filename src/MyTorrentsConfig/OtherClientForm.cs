using System;
using System.Drawing;
using System.Windows.Forms;

namespace Sohvaperuna.MyTorrentsConfig
{
	/// 2026 (1.3.58): "I already use a different torrent program" - pick it, and step-by-step help for turning on its
	/// remote control so MyTorrents can talk to it, then "Check it now"
	internal sealed class OtherClientForm : Form
	{
		private static readonly string[] Keys = { "biglybt", "utorrent", "deluge", "transmission" };
		private static readonly string[] Names = { "BiglyBT", "uTorrent", "Deluge", "Transmission" };
		private static readonly int[] Ports = { 9091, 8080, 8112, 9091 };
		private static readonly string[] Help =
		{
			"Using Vuze (the old Azureus)? BiglyBT is its free, advert-free replacement from the same people, and reads your Vuze settings and downloads.\r\n\r\n" +
			"Setup installs BiglyBT for you (if it isn't installed) and sets up its Web Remote - nothing to do here.\r\n\r\n" +
			"If you'd rather do it yourself: in BiglyBT, Tools > Plugins > Installation Wizard, install \"Web Remote\", then Tools > Options > Plugins > Web Remote: " +
			"tick 'Enable', port 9091, and 'Only allow connections from this computer'.",

			"Not recommended - newer versions show adverts, offer extra software when installing, and some antivirus programs flag them. " +
			"If you're starting fresh, qBittorrent is free with no adverts (press Cancel and leave qBittorrent ticked).\r\n\r\n" +
			"If you already use uTorrent:\r\n" +
			"1.  Open uTorrent.\r\n" +
			"2.  Options > Preferences > Advanced > Web UI.\r\n" +
			"3.  Tick 'Enable Web UI'. Type a user name and a password (anything you like) - put the same ones below.\r\n" +
			"4.  Tick 'Alternative listening port' and set it to 8080 (or put your own port below).\r\n" +
			"5.  OK, then press 'Check it now'.",

			"1.  Install Deluge and open it.\r\n" +
			"2.  Edit > Preferences > Plugins: tick 'WebUi'.\r\n" +
			"3.  In the 'WebUi' page that appears: tick 'Enable web interface', port 8112.\r\n" +
			"4.  The web password starts as 'deluge' (already filled in below). If you changed it, type yours below.\r\n" +
			"5.  OK, then press 'Check it now'.\r\n\r\n" +
			"MyTorrents only uses the password (no user name) for Deluge.",

			"1.  Install Transmission (Windows version) and open it.\r\n" +
			"2.  Edit > Preferences > Remote.\r\n" +
			"3.  Tick 'Allow remote access', port 9091.\r\n" +
			"4.  Optional: tick 'Use authentication' and type a user name and password - put the same ones below.\r\n" +
			"5.  Tick 'Only allow these IP addresses' and add 127.0.0.1 (this computer only).\r\n" +
			"6.  Close, then press 'Check it now'."
		};

		private readonly RadioButton[] _radios = new RadioButton[Keys.Length];
		private readonly TextBox _help = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, BackColor = Color.White };
		private readonly NumericUpDown _port = new NumericUpDown { Minimum = 1, Maximum = 65535, Width = 80 };
		private readonly TextBox _user = new TextBox { Width = 140 };
		private readonly TextBox _pass = new TextBox { Width = 140, UseSystemPasswordChar = true };
		private readonly Label _userLabel = new Label { Text = "User name", AutoSize = true };
		private readonly Label _passLabel = new Label { Text = "Password", AutoSize = true };
		private readonly Label _check = new Label { AutoSize = true };
		private readonly Button _checkButton = new Button { Text = "Check it now", AutoSize = true };
		private readonly Panel _login = new Panel();
		private int _last = -1;

		public string Key { get; private set; }
		public int Port => (int)_port.Value;
		public string User => _user.Visible ? _user.Text.Trim() : "";
		public string Password => _pass.Text;

		public OtherClientForm(string key, int port, string user, string pass, AppState bigly)
		{
			Text = "A different torrent program";
			Font = new Font("Segoe UI", 9f);
			FormBorderStyle = FormBorderStyle.FixedDialog;
			MaximizeBox = MinimizeBox = false;
			StartPosition = FormStartPosition.CenterParent;
			ClientSize = new Size(620, 470);
			AutoScaleDimensions = new SizeF(96F, 96F);
			AutoScaleMode = AutoScaleMode.Dpi;

			Controls.Add(new Label { Text = "Which one do you use?", Location = new Point(14, 12), AutoSize = true, Font = new Font(Font, FontStyle.Bold) });
			for (int i = 0; i < Keys.Length; i++)
			{
				string extra = Keys[i] == "utorrent" ? "  (you install it - not recommended)" : i == 0 ? (bigly != null && bigly.Installed ? "  (installed - setup sets it up)  - also for Vuze users, it reads your Vuze settings" : "  (setup installs it)  - also for Vuze users, it reads your Vuze settings") : "  (you install it)";
				_radios[i] = new RadioButton { Text = Names[i] + extra, Location = new Point(24, 36 + i * 24), AutoSize = true, Tag = i };
				int n = i;
				_radios[i].CheckedChanged += (s, e) => { if (_radios[n].Checked) ShowHelpFor(n); };
				Controls.Add(_radios[i]);
			}
			Controls.Add(new Label { Text = "How to set it up", Location = new Point(14, 162), AutoSize = true, Font = new Font(Font, FontStyle.Bold) });
			_help.SetBounds(14, 182, 592, 168);
			Controls.Add(_help);

			_login.SetBounds(14, 358, 592, 30);
			_login.Controls.Add(new Label { Text = "Port", Location = new Point(0, 5), AutoSize = true });
			_port.Location = new Point(36, 2);
			_login.Controls.Add(_port);
			_userLabel.Location = new Point(134, 5);
			_user.Location = new Point(206, 2);
			_passLabel.Location = new Point(360, 5);
			_pass.Location = new Point(424, 2);
			_login.Controls.AddRange(new Control[] { _userLabel, _user, _passLabel, _pass });
			Controls.Add(_login);

			_checkButton.Location = new Point(14, 396);
			_check.Location = new Point(124, 402);
			_checkButton.Click += (s, e) =>
			{
				Cursor = Cursors.WaitCursor;
				bool ok = SetupCore.PortAnswers(Port);
				Cursor = Cursors.Default;
				_check.ForeColor = ok ? Color.DarkGreen : Color.Firebrick;
				_check.Text = ok ? "✔ It answers on port " + Port + " - good." : "✖ Nothing answers on port " + Port + " yet - is it open, with its remote control turned on?";
			};
			Controls.Add(_checkButton);
			Controls.Add(_check);

			Button ok2 = new Button { Text = "Use this one", DialogResult = DialogResult.OK, AutoSize = true };
			Button cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
			ok2.Location = new Point(420, 432);
			cancel.Location = new Point(530, 432);
			Controls.Add(ok2);
			Controls.Add(cancel);
			AcceptButton = ok2;
			CancelButton = cancel;
			ok2.Click += (s, e) => Key = _last >= 0 ? Keys[_last] : null;

			int start = Math.Max(0, Array.IndexOf(Keys, key ?? ""));
			_user.Text = user ?? "";
			_pass.Text = pass ?? "";
			_radios[start].Checked = true;
			if (port > 0 && key != null) _port.Value = port;
		}

		private void ShowHelpFor(int i)
		{
			if (_last >= 0 && _port.Value == Ports[_last]) _port.Value = Ports[i];
			else if (_last < 0) _port.Value = Ports[i];
			if (Keys[i] == "deluge" && _pass.Text.Length == 0) _pass.Text = "deluge";
			if (_last >= 0 && Keys[_last] == "deluge" && Keys[i] != "deluge" && _pass.Text == "deluge") _pass.Text = "";
			_last = i;
			_help.Text = Help[i];
			bool bigly = Keys[i] == "biglybt", azureus = false;
			_login.Visible = _checkButton.Visible = _check.Visible = !bigly;
			_user.Visible = _userLabel.Visible = !bigly && !azureus && Keys[i] != "deluge";
			_pass.Visible = _passLabel.Visible = !bigly && !azureus;
			_check.Text = "";
		}
	}
}
