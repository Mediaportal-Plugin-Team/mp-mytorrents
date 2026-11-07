using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Windows.Forms;

namespace Sohvaperuna.MyTorrentsConfig
{
	/// <summary>
	/// MyTorrents setup assistant. First time: asks which torrent program you use, installs what is missing
	/// (torrent program, Jackett, FlareSolverr), sets them up and connects them to MyTorrents.
	/// Later installs: only checks Jackett and FlareSolverr for updates.
	/// </summary>
	internal sealed class SetupForm : Form
	{
		private enum Row { Client, ClientWeb, Jackett, FlareSolverr, Sites, MyTorrents }

		private enum State { Plan, Working, Done, Failed, Info }

		private static readonly Color Accent = Color.FromArgb(0, 120, 212);
		private static readonly Color Good = Color.FromArgb(16, 124, 16);
		private static readonly Color Bad = Color.FromArgb(196, 43, 28);
		private static readonly Color Muted = Color.FromArgb(96, 96, 96);

		private readonly bool _updateOnly;
		private readonly RadioButton _qb = new RadioButton { Text = "qBittorrent  (recommended)", AutoSize = true, Checked = true };
		private readonly RadioButton _bigly = new RadioButton { Text = "BiglyBT", AutoSize = true };
		// 2026 (1.3.57): the other programs MyTorrents works with (as on the Torrent client tab) - setup can't install
		// these, so you install them yourself and setup connects MyTorrents to them (laptop test, 7 Nov)
		// 2026 (1.3.58): qBittorrent is ticked and recommended (the one that is fully tested - setup installs and sets it
		// up); the others are behind "I already use a different torrent program...", with help for each
		private readonly RadioButton _other = new RadioButton { Text = "I already use a different torrent program:", AutoSize = true };
		private readonly LinkLabel _otherChoose = new LinkLabel { Text = "choose it...", AutoSize = true };
		private readonly Label _otherState = new Label { AutoSize = false, ForeColor = Muted, Size = new Size(560, 18) };
		private string _otherKey; // biglybt, utorrent, deluge, transmission, azureus
		private int _otherPort;
		private string _otherUser = "", _otherPass = "";
		private bool IsBigly => _other.Checked && _otherKey == "biglybt";
		private bool IsOther => _other.Checked && _otherKey != null && _otherKey != "biglybt";
		private int OtherIdx => Math.Max(0, Array.IndexOf(OtherKeys, _otherKey));
		private static readonly string[] OtherNames = { "uTorrent", "Deluge", "Transmission", "Vuze / Azureus" };
		private static readonly string[] OtherKeys = { "utorrent", "deluge", "transmission", "azureus" };
		private static readonly int[] OtherPorts = { 8080, 8112, 9091, 6884 };
		private static readonly string[] OtherHints =
		{
			"install it first, then turn on Options > Preferences > Advanced > Web UI",
			"install it first, then turn on the Web UI plugin",
			"install it first, then turn on Edit > Preferences > Remote",
			"install it first, with the 'XML over HTTP' plugin"
		};
		private readonly Label _qbState = new Label { AutoSize = true, ForeColor = Muted };
		private readonly Label _biglyState = new Label { AutoSize = true, ForeColor = Muted };
		private readonly Dictionary<Row, Label> _names = new Dictionary<Row, Label>();
		private readonly Dictionary<Row, Label> _states = new Dictionary<Row, Label>();
		private readonly ProgressBar _progress = new ProgressBar { Style = ProgressBarStyle.Continuous };
		private readonly Label _status = new Label { AutoEllipsis = true };
		private readonly Button _go = new Button { Text = "Set up", Enabled = false };
		private readonly Button _close = new Button { Text = "Close" };
		private readonly LinkLabel _openJackett = new LinkLabel { Text = "Open Jackett", AutoSize = true, Visible = false };
		private readonly LinkLabel _fullSetup = new LinkLabel { Text = "Run the full setup again", AutoSize = true, Visible = false };
		private readonly GroupBox _clientBox;
		private readonly GroupBox _stepsBox;

		private AppState _qbApp = new AppState(), _biglyApp = new AppState(), _jackett = new AppState(), _flare = new AppState();
		private Release _jackettLatest, _flareLatest;
		private string _checkError = "";
		private bool _running;
		private int _jackettPort = 9117;

		public SetupForm(bool updateOnly)
		{
			_updateOnly = updateOnly;
			Text = "MyTorrents setup";
			Font = new Font("Segoe UI", 9f);
			FormBorderStyle = FormBorderStyle.FixedDialog;
			MaximizeBox = false;
			StartPosition = FormStartPosition.CenterScreen;
			ClientSize = new Size(640, updateOnly ? 456 : 652);
			BackColor = Color.White;

			Panel header = new Panel { Dock = DockStyle.Top, Height = 72, BackColor = Color.FromArgb(31, 42, 54) };
			header.Controls.Add(new Label { Text = updateOnly ? "MyTorrents - checking for updates" : "MyTorrents setup", Font = new Font("Segoe UI", 15f, FontStyle.Bold), ForeColor = Color.White, Location = new Point(18, 10), AutoSize = true });
			header.Controls.Add(new Label
			{
				Text = updateOnly ? "Checks the torrent program, Jackett and FlareSolverr, and updates them if a newer version is out." : "Installs and connects everything MyTorrents needs. You can run this again any time.",
				ForeColor = Color.FromArgb(200, 210, 220), Location = new Point(20, 44), AutoSize = true
			});
			Controls.Add(header);

			int y = 86;
			_clientBox = new GroupBox { Text = "1.  Which torrent program will you use?", Location = new Point(16, y), Size = new Size(608, 130), Visible = !updateOnly };
			_qb.Location = new Point(18, 26);
			_qbState.Location = new Point(250, 28);
			_clientBox.Controls.Add(new Label { Text = "Free, no adverts. Setup installs it, sets it up and starts it with Windows - just press Set up.", Location = new Point(36, 50), AutoSize = true, ForeColor = Muted });
			_other.Location = new Point(18, 78);
			_otherChoose.Location = new Point(278, 80);
			_otherState.Location = new Point(36, 102);
			_clientBox.Controls.AddRange(new Control[] { _qb, _qbState, _other, _otherChoose, _otherState });
			_qb.CheckedChanged += (s, e) => UpdatePlan();
			_other.CheckedChanged += (s, e) =>
			{
				if (_other.Checked && _otherKey == null && !_choosing) BeginInvoke((Action)ChooseOther);
				UpdatePlan();
			};
			_otherChoose.LinkClicked += (s, e) => ChooseOther();
			Controls.Add(_clientBox);
			if (!updateOnly) y += 142;

			Row[] rows = updateOnly ? new[] { Row.ClientWeb, Row.Jackett, Row.FlareSolverr } : (Row[])Enum.GetValues(typeof(Row));
			_stepsBox = new GroupBox { Text = updateOnly ? "Updates" : "2.  What setup will do", Location = new Point(16, y), Size = new Size(608, 30 + rows.Length * 38 + (rows.Contains(Row.FlareSolverr) ? 18 : 0)) };
			int ry = 26;
			foreach (Row r in rows)
			{
				Label name = new Label { Location = new Point(14, ry), Size = new Size(250, 34), Text = RowTitle(r) };
				Label state = new Label { Location = new Point(270, ry), Size = new Size(326, 34), AutoEllipsis = true, ForeColor = Muted, Text = "checking..." };
				_names[r] = name;
				_states[r] = state;
				_stepsBox.Controls.Add(name);
				_stepsBox.Controls.Add(state);
				if (r == Row.FlareSolverr)
				{
					_stepsBox.Controls.Add(new Label
					{
						Text = "Free and safe - it only talks to this PC. If Windows asks about chrome.exe, click Cancel.",
						Location = new Point(14, ry + 34), Size = new Size(580, 18),
						Font = new Font(Font.FontFamily, 8.25f, FontStyle.Italic), ForeColor = Color.FromArgb(150, 100, 0)
					});
					ry += 18;
				}
				ry += 38;
			}
			Controls.Add(_stepsBox);
			y += _stepsBox.Height + 12;

			_progress.SetBounds(16, y, 608, 16);
			_status.SetBounds(16, y + 22, 608, 36);
			Controls.Add(_progress);
			Controls.Add(_status);

			int by = ClientSize.Height - 46;
			_go.SetBounds(436, by, 92, 30);
			_close.SetBounds(534, by, 90, 30);
			_openJackett.Location = new Point(16, by + 7);
			_fullSetup.Location = new Point(16, by + 7);
			_go.Click += Go_Click;
			_close.Click += (s, e) => Close();
			_openJackett.LinkClicked += (s, e) => OpenUrl(SetupCore.JackettBase(_jackettPort));
			_fullSetup.LinkClicked += (s, e) =>
			{
				Hide();
				using (SetupForm full = new SetupForm(false))
				{
					full.ShowDialog();
				}
				Close();
			};
			Controls.AddRange(new Control[] { _go, _close, _openJackett, _fullSetup });
			AcceptButton = _go;
			try
			{
				Icon = Icon.ExtractAssociatedIcon(SetupCore.ThisExe);
			}
			catch
			{
			}
		}

		private static string RowTitle(Row r)
		{
			switch (r)
			{
			case Row.Client: return "Torrent program";
			case Row.ClientWeb: return "Remote control for MyTorrents";
			case Row.Jackett: return "Jackett\r\nsearches the torrent sites";
			case Row.FlareSolverr: return "FlareSolverr\r\ngets past \"checking your browser\" pages";
			case Row.Sites: return "Search sites in Jackett";
			default: return "MyTorrents settings";
			}
		}

		protected override void OnShown(EventArgs e)
		{
			base.OnShown(e);
			Status("Checking what is installed...", 0);
			ThreadPool.QueueUserWorkItem(_ => Detect());
		}

		protected override void OnFormClosing(FormClosingEventArgs e)
		{
			if (_running && e.CloseReason == CloseReason.UserClosing)
			{
				MessageBox.Show(this, "Setup is still working - please wait until it finishes.", Text);
				e.Cancel = true;
			}
			base.OnFormClosing(e);
		}

		// ---------------------------------------------------------------- checking

		private void Detect()
		{
			_qbApp = SetupCore.QBittorrent();
			_biglyApp = SetupCore.BiglyBT();
			_jackett = SetupCore.Jackett();
			_flare = SetupCore.FlareSolverr();
			try
			{
				_jackettLatest = SetupCore.LatestGitHub("Jackett/Jackett", n => n.Equals("Jackett.Installer.Windows.exe", StringComparison.OrdinalIgnoreCase));
				_flareLatest = SetupCore.LatestGitHub("FlareSolverr/FlareSolverr", n => n.IndexOf("windows", StringComparison.OrdinalIgnoreCase) >= 0 && n.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));
			}
			catch (Exception ex)
			{
				_checkError = ex.Message;
			}
			if (_updateOnly)
			{
				_clientKey = SavedBackend();
				if (_clientKey == "qbittorrent" && _qbApp.Installed) _clientLatest = SetupCore.WingetLatest(SetupCore.QbWingetId);
				else if (_clientKey == "biglybt" && _biglyApp.Installed) _clientLatest = SetupCore.WingetLatest(SetupCore.BiglyWingetId);
			}
			string saved = "";
			try
			{
				saved = Sohvaperuna.MyTorrents.Configuration.GetExtra("Setup.Client", "");
			}
			catch
			{
			}
			Ui(() =>
			{
				_qbState.Text = _qbApp.ToString();
				_biglyState.Text = _biglyApp.ToString();
				if (saved == "qbittorrent") _qb.Checked = true;
				else if (saved == "biglybt" || Array.IndexOf(OtherKeys, saved) >= 0)
				{
					_otherKey = saved;
					_otherPort = saved == "biglybt" ? 9091 : OtherPorts[OtherIdx];
					_choosing = true;
					_other.Checked = true;
					_choosing = false;
				}
				UpdatePlan();
				if (_updateOnly)
				{
					_fullSetup.Visible = true;
					bool any = NeedsJackett() || NeedsFlare() || NeedsClient();
					if (_checkError.Length > 0) Status("Could not check for updates: " + _checkError, 0);
					else if (!any)
					{
						Status("Everything is up to date.", 100);
						ShowSitesAfterCheck();
					}
					else
					{
						Status("Click Update to get the newer version.", 0);
						Set(Row.ClientWeb, "checked when you click Update", State.Plan);
					}
					_go.Text = "Update";
					_go.Visible = any && _checkError.Length == 0;
					_go.Enabled = _go.Visible;
				}
				else
				{
					Status(_checkError.Length > 0 ? "No internet? Could not check Jackett / FlareSolverr releases: " + _checkError : "Choose your torrent program, then click Set up.", 0);
				}
			});
		}

		/// Update check with nothing to update: still show "Your search sites" so you can see they are working.
		private void ShowSitesAfterCheck()
		{
			string key;
			int port;
			if (!SetupCore.ReadJackettConfig(out key, out port)) return;
			_running = true;
			_close.Enabled = false;
			ThreadPool.QueueUserWorkItem(_ =>
			{
				try
				{
					CheckClient();
					if (SetupCore.IsElevated) AdminTasks.Run(SetupCore.FirewallTasks()); // started by the installer: no permission question needed
					_sitesSummary = "";
					SetUpSites(port, key);
				}
				catch (Exception ex)
				{
					SetupCore.Log("Sites check failed: " + ex.Message);
				}
				Ui(() =>
				{
					_running = false;
					_close.Enabled = true;
					Status("Everything is up to date. " + _sitesSummary, 100);
				});
			});
		}

		/// Update check: can MyTorrents still reach the torrent program? (An update or reinstall of it can switch its
		/// remote control off.) Starts the program if it is not running.
		private bool CheckClient()
		{
			string backend = "", user = "", password = "";
			int port = 0;
			try
			{
				var c = Sohvaperuna.MyTorrents.Configuration.Instance();
				backend = (c.Settings["uTorrent.Backend"] as string ?? "").ToLowerInvariant();
				int.TryParse(c.Settings["uTorrent.Server.Port"] as string, out port);
				user = (c.Settings["uTorrent.Username"] as string ?? "").Trim();
				password = (c.Settings["uTorrent.Password"] as string ?? "").Trim();
			}
			catch
			{
			}
			bool qb = backend == "qbittorrent";
			if (!qb && backend != "biglybt")
			{
				Set(Row.ClientWeb, "not checked (" + (backend.Length > 0 ? backend : "no torrent program chosen") + ")", State.Info);
				return true;
			}
			string name = qb ? "qBittorrent" : "BiglyBT";
			Ui(() => _names[Row.ClientWeb].Text = name + "\r\nconnection for MyTorrents");
			AppState app = qb ? SetupCore.QBittorrent() : SetupCore.BiglyBT();
			if (!(qb ? SetupCore.QBittorrentRunning() : SetupCore.BiglyRunning()) && app.Installed)
			{
				Set(Row.ClientWeb, "starting " + name + " to check...", State.Working);
				SetupCore.StartAsUser(app.Path);
			}
			else
			{
				Set(Row.ClientWeb, "checking...", State.Working);
			}
			bool ok;
			if (qb)
			{
				HttpStatusCode? code = SetupCore.QBittorrentStatus(port > 0 ? port : 8080, 60);
				ok = code == HttpStatusCode.OK || (code != null && password.Length > 0 && SetupCore.QBittorrentLoginOk(port > 0 ? port : 8080, user.Length > 0 ? user : "admin", password));
			}
			else
			{
				HttpStatusCode? code = SetupCore.BiglyAnswers(port > 0 ? port : 9091, 120);
				ok = code == HttpStatusCode.Conflict || code == HttpStatusCode.OK;
			}
			if (ok)
			{
				if (qb)
				{
					SetupCore.Log("qBittorrent share ratio limit " + SetupCore.QBittorrentShareLimit(port > 0 ? port : 8080, user.Length > 0 ? user : "admin", password, 1.1));
				}
				Set(Row.ClientWeb, name + (app.Version.Length > 0 ? " " + app.Version : "") + " answers - all good", State.Done);
				return true;
			}
			Set(Row.ClientWeb, name + " does not answer MyTorrents", State.Failed);
			DialogResult fix = DialogResult.No;
			Ui(() => fix = MessageBox.Show(this, "MyTorrents cannot reach " + name + ".\r\n\r\nThis usually happens after " + name + " has been updated or reinstalled - its remote control setting gets switched off.\r\n\r\nPut it right now?",
				"MyTorrents setup", MessageBoxButtons.YesNo, MessageBoxIcon.Warning), true);
			if (fix != DialogResult.Yes)
			{
				Set(Row.ClientWeb, name + " does not answer - click \"Run the full setup again\" below to fix it", State.Failed);
				return false;
			}
			return RepairClient(qb, app);
		}

		/// Puts the torrent program's remote control back (the same steps as the full setup) and saves it for MyTorrents.
		private bool RepairClient(bool qb, AppState app)
		{
			int port;
			bool ok = qb ? SetUpQBittorrent(app, out port) : SetUpBiglyBT(app, out port);
			if (!ok) return false;
			try
			{
				SetupCore.SaveMyTorrents(qb ? "qbittorrent" : "biglybt", port, null, null, qb ? _clientUser : "", qb ? _clientPassword : "");
			}
			catch (Exception ex)
			{
				return Fail(Row.ClientWeb, "fixed, but MyTorrents settings could not be saved: " + ex.Message);
			}
			return true;
		}

		// ---------------------------------------------------------------- torrent program updates

		private string _clientKey = "", _clientLatest = "";

		private static string SavedBackend()
		{
			try
			{
				return (Sohvaperuna.MyTorrents.Configuration.Instance().Settings["uTorrent.Backend"] as string ?? "").ToLowerInvariant();
			}
			catch
			{
				return "";
			}
		}

		private AppState ClientState => _clientKey == "qbittorrent" ? _qbApp : _biglyApp;

		private string ClientDisplayName => _clientKey == "qbittorrent" ? "qBittorrent" : "BiglyBT";

		private bool NeedsClient() => _updateOnly && (_clientKey == "qbittorrent" || _clientKey == "biglybt") && ClientState.Installed
			&& _clientLatest.Length > 0 && SetupCore.IsNewer(_clientLatest, ClientState.Version);

		/// The program must be closed while it is updated. qBittorrent can be asked to close; BiglyBT is closed by you.
		private bool CloseClient()
		{
			if (_clientKey == "qbittorrent")
			{
				if (!SetupCore.QBittorrentRunning()) return true;
				int port = 8080;
				string user = "", pw = "";
				try
				{
					var c = Sohvaperuna.MyTorrents.Configuration.Instance();
					int.TryParse(c.Settings["uTorrent.Server.Port"] as string, out port);
					user = (c.Settings["uTorrent.Username"] as string ?? "").Trim();
					pw = (c.Settings["uTorrent.Password"] as string ?? "").Trim();
				}
				catch
				{
				}
				if (SetupCore.QBittorrentShutdown(port > 0 ? port : 8080, user, pw)) return true;
				return WaitClosed("qBittorrent", SetupCore.QBittorrentRunning);
			}
			return WaitClosed("BiglyBT", SetupCore.BiglyRunning);
		}

		private bool NeedsJackett() => _jackettLatest != null && (!_jackett.Installed || SetupCore.IsNewer(_jackettLatest.Version, _jackett.Version));

		private bool NeedsFlare() => _flareLatest != null && (!_flare.Installed || SetupCore.IsNewer(_flareLatest.Version, _flare.Version));

		private string ClientName => _qb.Checked ? "qBittorrent" : IsOther ? OtherNames[OtherIdx] : "BiglyBT";

		private AppState ClientApp => _qb.Checked ? _qbApp : _biglyApp;

		private bool _choosing;

		/// the window for the other torrent programs, with help for setting each one up
		private void ChooseOther()
		{
			if (_choosing) return;
			_choosing = true;
			try
			{
				using (OtherClientForm f = new OtherClientForm(_otherKey, _otherPort, _otherUser, _otherPass, _biglyApp))
				{
					if (f.ShowDialog(this) == DialogResult.OK)
					{
						_otherKey = f.Key;
						_otherPort = f.Port;
						_otherUser = f.User;
						_otherPass = f.Password;
						_other.Checked = true;
					}
					else if (_otherKey == null)
					{
						_qb.Checked = true;
					}
				}
			}
			finally
			{
				_choosing = false;
			}
			UpdatePlan();
		}

		private void UpdatePlan()
		{
			if (_running) return;
			bool chosen = _qb.Checked || IsBigly || IsOther;
			_otherState.Text = IsBigly ? "BiglyBT - setup installs it and sets up its Web Remote" : IsOther ? OtherNames[OtherIdx] + " on port " + _otherPort + " - " + OtherHints[OtherIdx] : "";
			_otherChoose.Text = _otherKey == null ? "choose it..." : "change...";
			if (!_updateOnly)
			{
				_go.Enabled = chosen;
				if (!chosen)
				{
					Set(Row.Client, "choose your torrent program above", State.Info);
					Set(Row.ClientWeb, "", State.Plan);
				}
				else
				{
					_names[Row.Client].Text = ClientName;
					_names[Row.ClientWeb].Text = ClientName + " remote control\r\nso MyTorrents can talk to it";
					if (IsOther)
					{
						int i = OtherIdx;
						Set(Row.Client, "you install it yourself - setup can't install " + OtherNames[i], State.Info);
						Set(Row.ClientWeb, "MyTorrents will connect on port " + _otherPort + " (" + OtherHints[i] + ")", State.Plan);
					}
					else
					{
						Set(Row.Client, ClientApp.Installed ? "already installed " + Ver(ClientApp.Version) : "will be downloaded and installed", ClientApp.Installed ? State.Done : State.Plan);
						Set(Row.ClientWeb, _qb.Checked ? "will be turned on - this PC only, no password" : "Web Remote will be set up - this PC only, no password", State.Plan);
					}
				}
				Set(Row.Sites, "you pick them once Jackett is running (skipped if Jackett already has sites)", State.Plan);
				Set(Row.MyTorrents, "will be connected to the torrent program and Jackett", State.Plan);
			}
			if (_updateOnly && (_clientKey == "qbittorrent" || _clientKey == "biglybt"))
			{
				AppState c = ClientState;
				_names[Row.ClientWeb].Text = ClientDisplayName + "\r\nversion and connection";
				Set(Row.ClientWeb, NeedsClient() ? "will be updated " + c.Version + " -> " + _clientLatest
					: c.Installed ? "up to date (" + c.Version + ") - connection checked next" : "not installed", NeedsClient() ? State.Plan : State.Info);
			}
			Set(Row.Jackett, Plan(_jackett, _jackettLatest, "runs quietly as a Windows service"), NeedsJackett() ? State.Plan : State.Done);
			Set(Row.FlareSolverr, Plan(_flare, _flareLatest, "starts hidden with Windows"), NeedsFlare() ? State.Plan : State.Done);
		}

		private string Plan(AppState app, Release latest, string how)
		{
			if (latest == null) return app.Installed ? "installed " + Ver(app.Version) + " (could not check for updates)" : "cannot download - no internet?";
			if (!app.Installed) return "will be installed (" + latest.Version + ") - " + how;
			if (SetupCore.IsNewer(latest.Version, app.Version)) return "will be updated " + app.Version + " -> " + latest.Version;
			return "up to date " + Ver(app.Version);
		}

		private static string Ver(string v) => string.IsNullOrEmpty(v) ? "" : "(" + v + ")";

		// ---------------------------------------------------------------- running

		private void Go_Click(object sender, EventArgs e)
		{
			if (Process.GetProcessesByName("MediaPortal").Length > 0)
			{
				MessageBox.Show(this, "Please close MediaPortal first, then click " + _go.Text + " again.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}
			_running = true;
			_go.Enabled = false;
			_close.Enabled = false;
			_clientBox.Enabled = false;
			_fullSetup.Visible = false;
			bool qb = _qb.Checked;
			int other = IsOther ? OtherIdx : -1;
			ThreadPool.QueueUserWorkItem(_ =>
			{
				bool ok = false;
				try
				{
					ok = _updateOnly ? RunUpdate() : RunFull(qb, other);
				}
				catch (Exception ex)
				{
					Status("Setup stopped: " + ex.Message, 0);
				}
				Ui(() =>
				{
					_running = false;
					_close.Enabled = true;
					_close.Text = "Finish";
					_go.Visible = !ok;
					_go.Enabled = !ok;
					_go.Text = "Try again";
					_clientBox.Enabled = !ok;
					AcceptButton = ok ? _close : _go;
				});
			});
		}

		private bool RunUpdate()
		{
			bool ok = true;
			List<string> admin = new List<string>();
			bool updateClient = NeedsClient();
			if (updateClient)
			{
				Set(Row.ClientWeb, "closing " + ClientDisplayName + " for the update...", State.Working);
				if (!CloseClient())
				{
					updateClient = false;
					ok = Fail(Row.ClientWeb, ClientDisplayName + " was still open - not updated");
				}
				else
				{
					admin.Add("winget-upgrade|" + (_clientKey == "qbittorrent" ? SetupCore.QbWingetId : SetupCore.BiglyWingetId));
				}
			}
			if (NeedsJackett())
			{
				string installer = DownloadJackett();
				admin.Add("jackett-install|" + installer);
				admin.Add("jackett-service");
			}
			if (NeedsFlare())
			{
				ok &= InstallFlare();
			}
			if (admin.Count > 0)
			{
				admin.AddRange(SetupCore.FirewallTasks()); // same permission question
				Set(Row.Jackett, "installing - answer Yes if Windows asks for permission...", State.Working);
				if (updateClient) Set(Row.ClientWeb, "updating " + ClientDisplayName + "...", State.Working);
				var res = Results(SetupCore.RunAdmin(admin));
				if (admin.Any(a => a.StartsWith("jackett"))) ok &= ReportJackett(res);
				else UpdatePlan();
				KeyValuePair<bool, string> up;
				if (updateClient && res.TryGetValue("winget-upgrade", out up) && !up.Key)
				{
					ok = Fail(Row.ClientWeb, "could not update " + ClientDisplayName + ": " + up.Value);
				}
			}
			else if (SetupCore.IsElevated)
			{
				AdminTasks.Run(SetupCore.FirewallTasks());
			}
			ok &= CheckClient(); // after an update the program's remote control may need putting back
			string key;
			int port;
			if (SetupCore.ReadJackettConfig(out key, out port))
			{
				_sitesSummary = "";
				SetUpSites(port, key);
			}
			Status((ok ? "Updates done. " : "Some updates did not work - see the red lines above. ") + _sitesSummary, 100);
			return ok;
		}

		private bool RunFull(bool qb, int other = -1)
		{
			string client = other >= 0 ? OtherNames[other] : qb ? "qBittorrent" : "BiglyBT";
			bool ok = true;
			SetupCore.Log("---- full setup, " + client + ", user " + Environment.UserName + (SetupCore.IsElevated ? " (administrator)" : "") + ", settings folder " + SetupCore.AppData);

			// 1. downloads (no admin needed)
			List<string> admin = new List<string>();
			AppState app = other >= 0 ? new AppState { Installed = true } : qb ? _qbApp : _biglyApp; // another program: you installed it
			bool hadClient = app.Installed, hadJackett = _jackett.Installed; // 2026 (1.3.42): for "Remove MyTorrents"
			if (!app.Installed)
			{
				admin.Add("winget|" + (qb ? "qBittorrent.qBittorrent" : "BiglySoftware.BiglyBT"));
				Set(Row.Client, "will be installed in a moment", State.Working);
			}
			if (NeedsJackett())
			{
				admin.Add("jackett-install|" + DownloadJackett());
			}
			if (_jackett.Installed || NeedsJackett())
			{
				admin.Add("jackett-service");
				admin.Add("jackett-flaresolverr|" + SetupCore.FlareSolverrUrl);
			}
			admin.AddRange(SetupCore.FirewallTasks()); // no "allow access?" pop-ups for FlareSolverr's browser
			// FlareSolverr is the big download: do it in the background while everything else is set up
			bool flareOk = false;
			bool needFlare = NeedsFlare();
			Thread flare = new Thread(() =>
			{
				try
				{
					flareOk = needFlare ? InstallFlare() : StartFlare();
				}
				catch (Exception ex)
				{
					flareOk = Fail(Row.FlareSolverr, "could not install: " + ex.Message);
				}
			}) { IsBackground = true, Name = "FlareSolverr setup" };
			flare.Start();

			// 2. everything that needs administrator rights, with one Windows permission question
			Dictionary<string, KeyValuePair<bool, string>> res = new Dictionary<string, KeyValuePair<bool, string>>();
			if (admin.Count > 0)
			{
				Status("Installing - answer Yes if Windows asks for permission. Installing a program can take a few minutes.", 0);
				if (!app.Installed) Set(Row.Client, "installing " + client + "...", State.Working);
				if (admin.Any(a => a.StartsWith("jackett"))) Set(Row.Jackett, "installing and starting...", State.Working);
				res = Results(SetupCore.RunAdmin(admin));
			}

			// 3. torrent program
			if (!app.Installed)
			{
				app = qb ? SetupCore.QBittorrent() : SetupCore.BiglyBT();
				KeyValuePair<bool, string> w;
				if (!app.Installed)
				{
					string why = res.TryGetValue("winget", out w) && !w.Key ? w.Value : "it did not install";
					Set(Row.Client, "could not install: " + why, State.Failed);
					Set(Row.ClientWeb, "skipped - install " + client + " first", State.Failed);
					Ui(() =>
					{
						if (MessageBox.Show(this, client + " could not be installed automatically:\r\n" + why + "\r\n\r\nOpen the " + client + " download page? Install it, then run this setup again.", Text, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
						{
							OpenUrl(qb ? "https://www.qbittorrent.org/download" : "https://www.biglybt.com/download/");
						}
					});
					ok = false;
				}
				else
				{
					Set(Row.Client, "installed " + Ver(app.Version), State.Done);
				}
			}
			else if (other < 0)
			{
				Set(Row.Client, "already installed " + Ver(app.Version), State.Done);
			}
			int clientPort = other >= 0 ? (_otherPort > 0 ? _otherPort : OtherPorts[other]) : qb ? 8080 : 9091;
			if (other >= 0)
			{
				// 2026 (1.3.57): can't install or set it up from here - just check something answers on its usual port
				bool answers = SetupCore.PortAnswers(clientPort);
				Set(Row.Client, answers ? "found it running" : "not running - " + OtherHints[other], answers ? State.Done : State.Failed);
				Set(Row.ClientWeb, answers ? "MyTorrents will connect on port " + clientPort + " (change it on the Torrent client tab if yours is different)"
					: "port " + clientPort + " doesn't answer yet - start " + client + " and turn on its web remote, then Try again", answers ? State.Done : State.Failed);
				ok &= answers;
			}
			else if (app.Installed)
			{
				bool webOk = qb ? SetUpQBittorrent(app, out clientPort) : SetUpBiglyBT(app, out clientPort);
				ok &= webOk;
			}

			// 4. Jackett
			bool jackettOk = admin.Any(a => a.StartsWith("jackett")) ? ReportJackett(res) : Fail(Row.Jackett, "could not download Jackett: " + _checkError);
			ok &= jackettOk;
			if (flare.IsAlive)
			{
				Status("Nearly done - waiting for FlareSolverr to finish installing...", 90);
				flare.Join();
			}
			if (!flareOk) ok = false;

			// 5. Jackett API key + sites, MyTorrents settings
			string key;
			int port;
			bool haveKey = false;
			for (int i = 0; i < 20 && !(haveKey = SetupCore.ReadJackettConfig(out key, out port)); i++)
			{
				Thread.Sleep(1500);
			}
			SetupCore.ReadJackettConfig(out key, out port);
			_jackettPort = port;
			if (jackettOk && haveKey)
			{
				ok &= SetUpSites(port, key);
			}
			else
			{
				Set(Row.Sites, "skipped - Jackett is not running", State.Failed);
			}
			try
			{
				SetupCore.SaveMyTorrents(other >= 0 ? (OtherKeys[other] == "transmission" ? "biglybt" : OtherKeys[other]) : app.Installed ? (qb ? "qbittorrent" : "biglybt") : null, clientPort, haveKey ? SetupCore.JackettBase(port) : null, key,
					qb ? _clientUser : other >= 0 ? _otherUser : "", qb ? _clientPassword : other >= 0 ? _otherPass : "");
				Set(Row.MyTorrents, "connected to " + client + " (port " + clientPort + ")" + (haveKey ? " and Jackett (API key filled in)" : ""), app.Installed && haveKey ? State.Done : State.Failed);
			}
			catch (Exception ex)
			{
				ok = Fail(Row.MyTorrents, "could not save: " + ex.Message);
			}
			// 2026 (1.3.42): remember what this setup installed (only those are offered in "Remove MyTorrents")
			try
			{
				if (!hadClient && app.Installed && other < 0) SetupCore.NoteInstalled(qb ? "qbittorrent" : "biglybt");
				if (!hadJackett && SetupCore.Jackett().Installed) SetupCore.NoteInstalled("jackett");
				if (SetupCore.FindFlareSolverrExe() != null) SetupCore.NoteInstalled("flaresolverr");
			}
			catch
			{
			}
			Ui(() => _openJackett.Visible = haveKey);
			Status(ok ? "All done. Start MediaPortal and open MyTorrents." : "Setup finished, but some steps did not work - see the red lines. Fix them and click Try again.", 100);
			return ok;
		}

		private string DownloadJackett()
		{
			string dest = Path.Combine(SetupCore.WorkDir, "Jackett.Installer.Windows.exe");
			Set(Row.Jackett, "downloading " + _jackettLatest.Version + "...", State.Working);
			SetupCore.Download(_jackettLatest.Url, dest, p =>
			{
				Set(Row.Jackett, "downloading " + _jackettLatest.Version + "... " + p + "%", State.Working);
				Progress(p);
			});
			Set(Row.Jackett, "downloaded, waiting to install...", State.Working);
			return dest;
		}

		private bool InstallFlare()
		{
			try
			{
				string zip = Path.Combine(SetupCore.WorkDir, "flaresolverr_windows_x64.zip");
				SetupCore.Download(_flareLatest.Url, zip, p =>
				{
					Set(Row.FlareSolverr, "downloading " + _flareLatest.Version + " (" + (_flareLatest.Size / 1048576) + " MB)... " + p + "%", State.Working);
				});
				SetupCore.InstallFlareSolverr(zip, _flareLatest.Version, s => Set(Row.FlareSolverr, s, State.Working));
				try
				{
					File.Delete(zip);
				}
				catch
				{
				}
				return StartFlare();
			}
			catch (Exception ex)
			{
				return Fail(Row.FlareSolverr, "could not install: " + ex.Message);
			}
		}

		private bool StartFlare()
		{
			if (!SetupCore.FlareSolverr().Installed)
			{
				return Fail(Row.FlareSolverr, "not installed: " + (_checkError.Length > 0 ? _checkError : "download failed"));
			}
			SetupCore.EnableFlareSolverrAutostart();
			Set(Row.FlareSolverr, "starting...", State.Working);
			SetupCore.StartFlareSolverr();
			if (SetupCore.FlareSolverrAnswers(180))
			{
				Set(Row.FlareSolverr, "running " + Ver(SetupCore.FlareSolverr().Version) + " - starts hidden with Windows", State.Done);
				return true;
			}
			return Fail(Row.FlareSolverr, "installed, but did not start - see flaresolverr.log in " + SetupCore.FlareSolverrDir);
		}

		private bool ReportJackett(Dictionary<string, KeyValuePair<bool, string>> res)
		{
			foreach (string step in new[] { "jackett-install", "jackett-service", "jackett-flaresolverr" })
			{
				KeyValuePair<bool, string> r;
				if (res.TryGetValue(step, out r) && !r.Key)
				{
					string what = step == "jackett-install" ? "install" : step == "jackett-service" ? "start the Jackett service" : "connect Jackett to FlareSolverr";
					return Fail(Row.Jackett, "could not " + what + ": " + r.Value);
				}
			}
			AppState j = SetupCore.Jackett();
			Set(Row.Jackett, "running " + Ver(j.Version) + " as a Windows service" + (res.ContainsKey("jackett-flaresolverr") ? ", using FlareSolverr" : ""), State.Done);
			return true;
		}

		private string _clientUser = "", _clientPassword = "";

		private bool SetUpQBittorrent(AppState app, out int port)
		{
			port = 8080;
			Set(Row.ClientWeb, "setting up...", State.Working);
			if (!WaitClosed("qBittorrent", SetupCore.QBittorrentRunning))
			{
				return Fail(Row.ClientWeb, "skipped - qBittorrent was still open");
			}
			SetupCore.QbLogin login = new SetupCore.QbLogin();
			try
			{
				port = SetupCore.ConfigureQBittorrent(login);
			}
			catch (Exception ex)
			{
				return Fail(Row.ClientWeb, "could not change qBittorrent's settings: " + ex.Message);
			}
			Set(Row.ClientWeb, "starting qBittorrent to check...", State.Working);
			SetupCore.StartAsUser(app.Path);
			HttpStatusCode? code = SetupCore.QBittorrentStatus(port, 90);
			if (code == null)
			{
				return Fail(Row.ClientWeb, "set, but qBittorrent did not answer on port " + port + " - is the port used by another program?");
			}
			// MyTorrents also keeps the Web UI login, so it still works if the "no login from this PC" bypass is off
			_clientUser = login.User;
			if (!login.HadPassword)
			{
				_clientPassword = login.Password; // made by setup - nobody needs to type it
			}
			else
			{
				// a password MyTorrents already has, and that works, means no question
				string savedUser = "", savedPassword = "";
				try
				{
					savedUser = (Sohvaperuna.MyTorrents.Configuration.Instance().Settings["uTorrent.Username"] as string ?? "").Trim();
					savedPassword = (Sohvaperuna.MyTorrents.Configuration.Instance().Settings["uTorrent.Password"] as string ?? "").Trim();
				}
				catch
				{
				}
				bool skipped = Sohvaperuna.MyTorrents.Configuration.GetExtra("Setup.QbPasswordSkipped", "") == "true";
				if (savedPassword.Length > 0 && SetupCore.QBittorrentLoginOk(port, savedUser.Length > 0 ? savedUser : login.User, savedPassword))
				{
					_clientPassword = savedPassword;
				}
				else if (skipped && code == HttpStatusCode.OK)
				{
					_clientPassword = ""; // you said not to ask, and the "no login from this PC" setting works
				}
				else
				{
					_clientPassword = AskQbPassword(port, login.User, code == HttpStatusCode.OK) ?? "";
				}
			}
			bool loginOk = _clientPassword.Length > 0 && SetupCore.QBittorrentLoginOk(port, _clientUser, _clientPassword);
			SetupCore.Log("qBittorrent: reply " + code + ", login saved: " + loginOk + ", bypass setting now: " + SetupCore.QBittorrentBypassSetting());
			if (code == HttpStatusCode.OK || loginOk)
			{
				string saveTo = SetupCore.QBittorrentSaveFolder(port, _clientUser, _clientPassword);
				SetupCore.Log("qBittorrent saves downloads to " + saveTo);
				SetupCore.Log("qBittorrent share ratio limit " + SetupCore.QBittorrentShareLimit(port, _clientUser, _clientPassword, 1.1));
				Set(Row.ClientWeb, "on - port " + port + (code == HttpStatusCode.OK ? ", no password from this PC" : ", logs in as " + _clientUser) +
					(saveTo.Length > 0 ? "\r\nDownloads go to " + saveTo : ""), State.Done);
				return true;
			}
			return Fail(Row.ClientWeb, "qBittorrent asks for a login and the password did not work - Try again, or tick Tools > Options > Web UI > \"Bypass authentication for clients on localhost\"");
		}

		/// The user set a Web UI password themselves - ask for it once (checked against qBittorrent).
		private string AskQbPassword(int port, string user, bool optional)
		{
			for (int attempt = 0; attempt < 3; attempt++)
			{
				string pw = null;
				Ui(() =>
				{
					using (Form f = new Form { Text = "qBittorrent Web UI password", FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false, StartPosition = FormStartPosition.CenterParent, ClientSize = new Size(420, 150), Font = Font })
					{
						f.ClientSize = new Size(420, optional ? 190 : 150);
						string intro = (attempt == 0 ? "" : "That password did not work. ") + "qBittorrent has a Web UI password (user: " + user + ").\r\n" +
							(optional ? "It is not needed right now - \"no login from this PC\" is on. Type it as a back-up so MyTorrents still connects if that is ever turned off, or click Skip and you will not be asked again."
									  : "Type it here so MyTorrents can log in.");
						f.Controls.Add(new Label { Text = intro, Location = new Point(12, 12), Size = new Size(396, optional ? 84 : 40) });
						int y = optional ? 100 : 60;
						TextBox box = new TextBox { UseSystemPasswordChar = true, Location = new Point(12, y), Width = 396 };
						Button ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(236, y + 48), Size = new Size(84, 28) };
						Button skip = new Button { Text = "Skip", DialogResult = DialogResult.Cancel, Location = new Point(324, y + 48), Size = new Size(84, 28) };
						f.Controls.AddRange(new Control[] { box, ok, skip });
						f.AcceptButton = ok;
						f.CancelButton = skip;
						if (f.ShowDialog(this) == DialogResult.OK) pw = box.Text;
					}
				}, true);
				if (pw == null)
				{
					if (optional) Sohvaperuna.MyTorrents.Configuration.SetExtra("Setup.QbPasswordSkipped", "true"); // do not ask again
					return null;
				}
				if (SetupCore.QBittorrentLoginOk(port, user, pw)) return pw;
			}
			return null;
		}

		private bool SetUpBiglyBT(AppState app, out int port)
		{
			port = 9091;
			Set(Row.ClientWeb, "setting up...", State.Working);
			if (!WaitClosed("BiglyBT", SetupCore.BiglyRunning))
			{
				return Fail(Row.ClientWeb, "skipped - BiglyBT was still open");
			}
			try
			{
				if (!SetupCore.WebRemoteInstalled(app))
				{
					Set(Row.ClientWeb, "adding the Web Remote plugin...", State.Working);
					SetupCore.InstallWebRemote();
				}
				port = SetupCore.ConfigureBiglyBT();
			}
			catch (Exception ex)
			{
				return Fail(Row.ClientWeb, "could not set up Web Remote: " + ex.Message);
			}
			Set(Row.ClientWeb, "starting BiglyBT to check (it can take a minute)...", State.Working);
			SetupCore.StartAsUser(app.Path);
			HttpStatusCode? code = SetupCore.BiglyAnswers(port, 120);
			if (code == HttpStatusCode.Conflict || code == HttpStatusCode.OK)
			{
				Set(Row.ClientWeb, "Web Remote on - port " + port + ", no password from this PC", State.Done);
				return true;
			}
			if (code == HttpStatusCode.Unauthorized)
			{
				return Fail(Row.ClientWeb, "BiglyBT still asks for a password - check Options > Plugins > Web Remote");
			}
			return Fail(Row.ClientWeb, "set, but BiglyBT did not answer on port " + port + " yet - if it was still starting, click Try again");
		}

		/// Settings can only be changed while the program is closed (it saves its own settings when it exits).
		private bool WaitClosed(string name, Func<bool> running)
		{
			while (running())
			{
				DialogResult r = DialogResult.Cancel;
				Ui(() => r = MessageBox.Show(this, name + " is open.\r\n\r\nPlease close it completely - right-click its icon by the clock and choose Exit (or File > Exit) - then click Retry.\r\n\r\nSetup will start it again afterwards.", Text, MessageBoxButtons.RetryCancel, MessageBoxIcon.Information), true);
				if (r != DialogResult.Retry) return false;
				Thread.Sleep(2000);
			}
			return true;
		}

		private bool SetUpSites(int port, string key)
		{
			Set(Row.Sites, "checking Jackett...", State.Working);
			int count = SetupCore.JackettIndexerCount(port, key);
			for (int i = 0; i < 10 && count < 0; i++)
			{
				Thread.Sleep(3000); // Jackett can take a moment after starting
				count = SetupCore.JackettIndexerCount(port, key);
			}
			if (count < 0) return Fail(Row.Sites, "Jackett did not answer on port " + port);
			JackettApi api = new JackettApi(port);
			List<JackettApi.Site> sites;
			try
			{
				api.Login();
				sites = api.AllSites();
			}
			catch (Exception ex)
			{
				return count > 0 ? Ok(Row.Sites, "Jackett has " + count + " site(s)") : Fail(Row.Sites, "could not read Jackett's site list (" + ex.Message + ") - add sites with Open Jackett");
			}
			// "Your search sites" every time: what is in use (ticked, with its status) and what could be added
			List<JackettApi.Site> add = new List<JackettApi.Site>(), remove = new List<JackettApi.Site>();
			bool saved = false;
			Ui(() =>
			{
				using (PickSitesForm pick = new PickSitesForm(sites) { JackettUrl = SetupCore.JackettBase(port), Api = api })
				{
					if (pick.ShowDialog(this) == DialogResult.OK)
					{
						saved = true;
						add = pick.ToAdd;
						remove = SitePicker.ConfirmRemove(this, pick.ToRemove);
					}
				}
			}, true);
			int problems = sites.Count(x => x.Configured && x.LastError.Length > 0);
			string problemText = problems > 0 ? " (" + problems + " with a problem - see Add sites...)" : "";
			if (add.Count == 0 && remove.Count == 0)
			{
				_sitesSummary = count > 0 ? "Jackett searches " + count + " site(s)" + (problems > 0 ? ", " + problems + " with a problem." : ", all working.") : "";
				if (count > 0) return Ok(Row.Sites, "Jackett searches " + count + " site(s)" + problemText);
				Set(Row.Sites, "no sites yet - add some with MyTorrents Configuration > Search sites > Add sites...", State.Info);
				return true;
			}
			List<string> failed = SitePicker.RemoveAll(api, remove);
			failed.AddRange(SitePicker.AddAll(api, add, (i, n, name) =>
			{
				Set(Row.Sites, "adding " + i + " of " + n + ": " + name + "...", State.Working);
				Progress((i - 1) * 100 / n);
			}));
			if (failed.Count > 0)
			{
				Ui(() => MessageBox.Show(this, SitePicker.FailedText(failed), Text, MessageBoxButtons.OK, MessageBoxIcon.Information));
			}
			int now = SetupCore.JackettIndexerCount(port, key);
			string changes = (add.Count > 0 ? add.Count - failed.Count(f => add.Any(a => f.StartsWith(a.Name + " ("))) + " added, " : "") + (remove.Count > 0 ? remove.Count + " removed, " : "");
			Set(Row.Sites, changes + "Jackett now searches " + (now >= 0 ? now.ToString() : "?") + " site(s)" + (failed.Count > 0 ? " (" + failed.Count + " could not be changed)" : ""), now > 0 ? State.Done : State.Failed);
			_sitesSummary = changes + "Jackett searches " + now + " site(s).";
			return now > 0;
		}

		private string _sitesSummary = "";

		private bool Ok(Row r, string text)
		{
			Set(r, text, State.Done);
			return true;
		}

		private static Dictionary<string, KeyValuePair<bool, string>> Results(List<string> lines)
		{
			var d = new Dictionary<string, KeyValuePair<bool, string>>();
			foreach (string l in lines)
			{
				string[] p = l.Split(new[] { '|' }, 3);
				if (p.Length == 3) d[p[1]] = new KeyValuePair<bool, string>(p[0] == "OK", p[2]);
			}
			return d;
		}

		// ---------------------------------------------------------------- UI helpers

		private bool Fail(Row r, string text)
		{
			Set(r, text, State.Failed);
			return false;
		}

		private void Set(Row r, string text, State st)
		{
			if (st == State.Done || st == State.Failed) SetupCore.Log(r + ": " + (st == State.Failed ? "FAILED " : "ok ") + text);
			Ui(() =>
			{
				Label l;
				if (!_states.TryGetValue(r, out l)) return;
				string mark = st == State.Done ? "✔  " : st == State.Failed ? "✖  " : st == State.Working ? "•  " : "";
				l.Text = mark + text;
				l.ForeColor = st == State.Done ? Good : st == State.Failed ? Bad : st == State.Working ? Accent : Muted;
			});
		}

		private void Status(string text, int pct)
		{
			Ui(() =>
			{
				_status.Text = text;
				_progress.Value = Math.Max(0, Math.Min(100, pct));
			});
		}

		private void Progress(int pct) => Ui(() => _progress.Value = Math.Max(0, Math.Min(100, pct)));

		private void Ui(Action a, bool wait = false)
		{
			if (IsDisposed) return;
			if (!InvokeRequired)
			{
				a();
			}
			else if (wait)
			{
				Invoke(a);
			}
			else
			{
				BeginInvoke(a);
			}
		}

		internal static void OpenUrl(string url)
		{
			try
			{
				Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
			}
			catch
			{
			}
		}
	}
}
