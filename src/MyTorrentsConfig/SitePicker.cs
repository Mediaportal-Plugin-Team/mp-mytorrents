using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace Sohvaperuna.MyTorrentsConfig
{
	/// <summary>
	/// 2026: adding Jackett sites without Jackett's own web page. Used by the setup assistant and by
	/// "MyTorrents Configuration.exe /sites" (the "Add sites..." button on the Search sites tab).
	/// </summary>
	internal static class SitePicker
	{
		/// Fewer sites than this and the setup assistant offers to add some.
		public const int EnoughSites = 5;

		public static List<string> AddAll(JackettApi api, List<JackettApi.Site> chosen, Action<int, int, string> progress)
		{
			List<string> failed = new List<string>();
			for (int i = 0; i < chosen.Count; i++)
			{
				progress?.Invoke(i + 1, chosen.Count, chosen[i].Name);
				try
				{
					api.Add(chosen[i].Id);
					SetupCore.Log("Jackett: added " + chosen[i].Name);
				}
				catch (Exception ex)
				{
					failed.Add(chosen[i].Name + " (" + ex.Message + ")");
					SetupCore.Log("Jackett: could not add " + chosen[i].Name + ": " + ex.Message);
				}
			}
			return failed;
		}

		/// Removes sites from Jackett. Returns the ones that could not be removed.
		public static List<string> RemoveAll(JackettApi api, List<JackettApi.Site> sites)
		{
			List<string> failed = new List<string>();
			foreach (JackettApi.Site s in sites)
			{
				try
				{
					if (s.IsOwn)
					{
						Sohvaperuna.MyTorrents.OwnSites.Remove(s.OwnUrl);
						SetupCore.Log("My sites: removed " + s.Name);
						continue;
					}
					api.Remove(s.Id);
					SetupCore.Log("Jackett: removed " + s.Name);
				}
				catch (Exception ex)
				{
					failed.Add(s.Name + " (" + ex.Message + ")");
				}
			}
			return failed;
		}

		/// Asks before removing sites. Returns the sites to remove (empty when the answer is No).
		public static List<JackettApi.Site> ConfirmRemove(IWin32Window owner, List<JackettApi.Site> remove)
		{
			if (remove.Count == 0) return remove;
			DialogResult r = MessageBox.Show(owner, "Stop using these sites? Jackett will no longer search them:\r\n\r\n" + string.Join("\r\n", remove.Select(x => x.Name)) +
				(remove.Any(x => !x.Public) ? "\r\n\r\nSites with your own account would need the login entering again in Jackett to add them back." : ""),
				"Your search sites", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
			return r == DialogResult.Yes ? remove : new List<JackettApi.Site>();
		}

		public static string FailedText(List<string> failed) =>
			"Jackett could not add these sites - they may be down or blocking Jackett right now. You can try them again later:\r\n\r\n" + string.Join("\r\n", failed);

		/// "MyTorrents Configuration.exe /sites"
		public static int RunStandalone()
		{
			const string title = "Add search sites";
			string key;
			int port;
			if (!SetupCore.ReadJackettConfig(out key, out port))
			{
				MessageBox.Show("Jackett is not installed yet.\r\n\r\nRun the setup assistant first (MyTorrents Configuration > Setup assistant...). It installs Jackett for you.", title, MessageBoxButtons.OK, MessageBoxIcon.Information);
				return 1;
			}
			List<JackettApi.Site> sites = null;
			JackettApi api = new JackettApi(port);
			string error = null;
			Busy(title, "Asking Jackett for its sites...", () =>
			{
				if (SetupCore.JackettIndexerCount(port, key) < 0)
				{
					error = "Jackett is not answering. It normally runs by itself in the background - restart the PC, or run the setup assistant again.";
					return;
				}
				try
				{
					api.Login();
					sites = api.AllSites();
				}
				catch (Exception ex)
				{
					error = "Could not read Jackett's site list: " + ex.Message;
				}
			});
			if (error != null)
			{
				MessageBox.Show(error, title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
				return 1;
			}
			List<JackettApi.Site> add, remove;
			using (PickSitesForm pick = new PickSitesForm(sites) { JackettUrl = SetupCore.JackettBase(port), Api = api })
			{
				pick.StartPosition = FormStartPosition.CenterScreen;
				pick.ShowInTaskbar = true;
				if (pick.ShowDialog() != DialogResult.OK) return 0;
				add = pick.ToAdd;
				remove = ConfirmRemove(null, pick.ToRemove);
			}
			if (add.Count == 0 && remove.Count == 0) return 0;
			List<string> failed = new List<string>();
			Busy(title, "Updating Jackett's sites...", () =>
			{
				failed.AddRange(RemoveAll(api, remove));
				failed.AddRange(AddAll(api, add, null));
			});
			int inUse = sites.Count(s => s.Configured) - remove.Count + add.Count - failed.Count;
			string msg = (add.Count > 0 ? (add.Count) + " added. " : "") + (remove.Count > 0 ? remove.Count + " removed. " : "") + "Jackett now searches about " + Math.Max(0, inUse) + " site(s).";
			if (failed.Count > 0) msg += "\r\n\r\n" + FailedText(failed);
			MessageBox.Show(msg, title, MessageBoxButtons.OK, failed.Count > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
			return 0;
		}

		/// A small "please wait" window while work runs in the background.
		private static void Busy(string title, string text, Action work)
		{
			using (Form f = new Form { Text = title, FormBorderStyle = FormBorderStyle.FixedDialog, ControlBox = false, StartPosition = FormStartPosition.CenterScreen, ClientSize = new Size(380, 80), Font = new Font("Segoe UI", 9f), ShowInTaskbar = true })
			{
				f.Controls.Add(new Label { Text = text, Location = new Point(16, 16), AutoSize = true });
				f.Controls.Add(new ProgressBar { Style = ProgressBarStyle.Marquee, Location = new Point(16, 44), Size = new Size(348, 16) });
				f.Shown += (s, e) => ThreadPool.QueueUserWorkItem(_ =>
				{
					try
					{
						work();
					}
					finally
					{
						f.BeginInvoke((Action)f.Close);
					}
				});
				f.ShowDialog();
			}
		}
	}
}

namespace Sohvaperuna.MyTorrentsConfig
{
	/// <summary>
	/// "Your search sites": every site Jackett already searches is ticked and listed first with its status
	/// (working / problem), and public sites can be ticked to add. Unticking a site removes it from Jackett.
	/// </summary>
	internal sealed class PickSitesForm : Form
	{
		private readonly List<JackettApi.Site> _all;
		private readonly HashSet<string> _ticked;
		private readonly CheckedListBox _list = new CheckedListBox { CheckOnClick = true, IntegralHeight = false };
		private readonly CheckBox _english = new CheckBox { Text = "English sites only", Checked = true, AutoSize = true };
		private readonly CheckBox _noAdult = new CheckBox { Text = "Hide adult sites", Checked = true, AutoSize = true };
		private readonly CheckBox _tvMovies = new CheckBox { Text = "Only sites with TV or movies", Checked = true, AutoSize = true };
		private readonly TextBox _find = new TextBox();
		private readonly Label _count = new Label { AutoSize = true };
		private readonly Label _detail = new Label { AutoEllipsis = true, ForeColor = Color.DimGray };
		private readonly CheckBox _accounts = new CheckBox { Text = "Show sites that need an account", Checked = false, AutoSize = true };
		private readonly TextBox _ownAddress = new TextBox();
		private readonly Button _ownAdd = new Button { Text = "Add" };
		private List<JackettApi.Site> _shown = new List<JackettApi.Site>();
		private bool _filling;

		public List<JackettApi.Site> ToAdd => _all.Where(s => !s.Configured && _ticked.Contains(s.Id)).ToList();

		public List<JackettApi.Site> ToRemove => _all.Where(s => s.Configured && !_ticked.Contains(s.Id)).ToList();

		/// Jackett, for sites that need a login (set by whoever opens this window).
		public JackettApi Api;

		/// Jackett's own web page (opened by the link under the list).
		public string JackettUrl = "http://127.0.0.1:9117";

		/// Kept for the old call sites: the sites to add.
		public List<JackettApi.Site> Chosen => ToAdd;

		/// 2026 (1.3.53): public sites that gave good results in testing (4 Nov 2026) - ticked on a first install
		internal static readonly HashSet<string> Recommended = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
			{ "torrentgalaxyclone", "torrentdownload", "torrentproject2" };

		public PickSitesForm(List<JackettApi.Site> sites) : this(sites, -1)
		{
		}

		public PickSitesForm(List<JackettApi.Site> sites, int unused)
		{
			_all = sites.Where(s => !s.IsOwn).ToList();
			try
			{
				foreach (var o in Sohvaperuna.MyTorrents.OwnSites.All())
				{
					_all.Add(OwnSite(o.Name, o.SearchUrl));
				}
			}
			catch
			{
			}
			sites = _all;
			_ticked = new HashSet<string>(sites.Where(s => s.Configured).Select(s => s.Id), StringComparer.OrdinalIgnoreCase);
			int inUse = _ticked.Count, problems = sites.Count(s => s.Configured && s.LastError.Length > 0);
			// 2026 (1.3.53): nothing set up yet (a first install) - the public sites that tested best are ticked
			// already, so OK on its own gives working searches. Untick or add any
			if (inUse == 0)
				foreach (JackettApi.Site r in sites.Where(x => !x.Configured && x.Public && Recommended.Contains(x.Id)))
					_ticked.Add(r.Id);
			Text = "Your search sites";
			Font = new Font("Segoe UI", 9f);
			// 2026: bigger, and it can be stretched (things were cut off on screens set to 125% or more)
			FormBorderStyle = FormBorderStyle.Sizable;
			MaximizeBox = true;
			MinimizeBox = false;
			StartPosition = FormStartPosition.CenterParent;
			ClientSize = new Size(820, 680);
			MinimumSize = new Size(700, 560);
			string intro = inUse == 0
				? "Jackett searches torrent sites for MyTorrents. The ticked ones are good public sites (no account needed) -\r\njust press OK to use them, or tick more. A handful is plenty; each one adds a little time to every search."
				: "Jackett searches the " + inUse + " ticked site" + (inUse == 1 ? "" : "s") + " for MyTorrents" + (problems > 0 ? " (" + problems + " with a problem - shown with ⚠)" : " - all working") + ".\r\n" +
				  (inUse < SitePicker.EnoughSites ? "More sites means more results: tick any you would like to add." : "Tick a site to add it, untick one to stop using it.") + " Public sites need no account.";
			Controls.Add(new Label { Text = intro + "\r\nYou can come back any time: MyTorrents Configuration > Search sites > Add sites...", Location = new Point(14, 12), Size = new Size(792, 60), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right });
			Controls.Add(new Label { Text = "Find", Location = new Point(14, 85), AutoSize = true });
			_find.SetBounds(60, 82, 220, 23);
			_english.Location = new Point(310, 84);
			_noAdult.Location = new Point(540, 84);
			_tvMovies.Location = new Point(310, 110);
			_accounts.Location = new Point(540, 110);
			Controls.Add(new Label { Text = "Add your own site", Location = new Point(14, 149), AutoSize = true });
			_ownAddress.SetBounds(140, 146, 420, 23);
			_ownAdd.SetBounds(570, 144, 80, 27);
			Controls.Add(new Label { Text = "e.g.  ext.to", Location = new Point(660, 149), AutoSize = true, ForeColor = Color.DimGray });
			_ownAdd.Click += (s, e) => AddOwnSite();
			_ownAddress.KeyDown += (s, e) =>
			{
				if (e.KeyCode == Keys.Enter)
				{
					e.SuppressKeyPress = true;
					AddOwnSite();
				}
			};
			_list.SetBounds(14, 182, 792, 370);
			_list.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
			_detail.SetBounds(14, 558, 792, 20);
			_detail.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
			_count.Location = new Point(14, 586);
			_count.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
			LinkLabel open = new LinkLabel { Text = "Open Jackett's own page - for sites that need an account, or to change a site's settings", AutoSize = true, Location = new Point(14, 610), Anchor = AnchorStyles.Bottom | AnchorStyles.Left };
			open.LinkClicked += (s, e) => SetupForm.OpenUrl(JackettUrl);
			Controls.Add(open);
			Button ok = new Button { Text = "Save", DialogResult = DialogResult.OK };
			Button cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel };
			ok.SetBounds(610, 638, 95, 32);
			cancel.SetBounds(711, 638, 95, 32);
			ok.Anchor = cancel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
			Controls.AddRange(new Control[] { _find, _english, _noAdult, _tvMovies, _accounts, _ownAddress, _ownAdd, _list, _detail, _count, ok, cancel });
			AcceptButton = ok;
			CancelButton = cancel;
			// 2026: grow with Windows' text size setting (125%, 150%) so nothing is cut off or overlaps
			AutoScaleDimensions = new SizeF(96F, 96F);
			AutoScaleMode = AutoScaleMode.Dpi;
			_find.TextChanged += (s, e) => Fill();
			_english.CheckedChanged += (s, e) => Fill();
			_noAdult.CheckedChanged += (s, e) => Fill();
			_tvMovies.CheckedChanged += (s, e) => Fill();
			_accounts.CheckedChanged += (s, e) => Fill();
			_list.ItemCheck += (s, e) =>
			{
				if (_filling) return;
				JackettApi.Site site = _shown[e.Index];
				string id = site.Id;
				if (e.NewValue == CheckState.Checked && !site.Configured && !site.Public)
				{
					e.NewValue = CheckState.Unchecked; // added only once the login works
					BeginInvoke((Action)(() => LogInAndAdd(site)));
					return;
				}
				if (e.NewValue == CheckState.Checked) _ticked.Add(id); else _ticked.Remove(id);
				BeginInvoke((Action)UpdateCount);
			};
			_list.SelectedIndexChanged += (s, e) => ShowDetail();
			Fill();
		}

		private void Fill()
		{
			string q = _find.Text.Trim();
			Func<JackettApi.Site, bool> matchesFind = s => q.Length == 0 || s.Name.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 || s.Description.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0;
			// sites in use or ticked are always listed first, whatever the filters say (2026 1.3.57: ticked ones too,
			// so the ones setup ticks for you are at the top, not somewhere in a list of 500 - laptop test, 7 Nov)
			List<JackettApi.Site> inUse = _all.Where(s => (s.Configured || _ticked.Contains(s.Id)) && matchesFind(s)).ToList();
			List<JackettApi.Site> others = _all.Where(s => !s.Configured && !_ticked.Contains(s.Id) && (s.Public || _accounts.Checked)
				&& (!_english.Checked || s.Language.StartsWith("en", StringComparison.OrdinalIgnoreCase))
				&& (!_noAdult.Checked || !s.Adult) && (!_tvMovies.Checked || s.TvOrMovies) && matchesFind(s)).ToList();
			_shown = inUse.Concat(others).ToList();
			_filling = true;
			_list.BeginUpdate();
			_list.Items.Clear();
			foreach (JackettApi.Site s in _shown)
			{
				_list.Items.Add(ItemText(s), _ticked.Contains(s.Id));
			}
			_list.EndUpdate();
			_filling = false;
			UpdateCount();
		}

		private static string ItemText(JackettApi.Site s)
		{
			if (s.IsOwn)
			{
				return s.Name + "   \u2714 your own site   " + Short(s.OwnUrl, 60);
			}
			if (s.Configured && !s.IsOwn)
			{
				string state = s.LastError.Length > 0 ? "⚠ problem: " + Short(s.LastError, 60) : "✔ in use, working";
				// 2026: how it has done in MyTorrents' own searches
				string stats = Sohvaperuna.TorrentSearch.SiteStats.Summary(s.Name);
				string why;
				if (Sohvaperuna.TorrentSearch.SiteStats.NotWorthIt(s.Name, out why)) state = "⚠ " + why + " - worth unticking";
				return s.Name + "   " + state + (stats.Length > 0 ? "   (" + stats + ")" : "") + (s.Public ? "" : "   (your account)");
			}
			string desc = Short(s.Description, 70);
			return s.Name + (s.Public ? "" : "   [needs an account]") + (desc.Length > 0 ? "  -  " + desc : "");
		}

		private void ShowDetail()
		{
			int i = _list.SelectedIndex;
			if (i < 0 || i >= _shown.Count)
			{
				_detail.Text = "";
				return;
			}
			JackettApi.Site s = _shown[i];
			if (s.IsOwn)
			{
				_detail.Text = s.Name + " - searched by MyTorrents itself: " + s.OwnUrl;
				return;
			}
			_detail.Text = s.Configured && s.LastError.Length > 0
				? s.Name + ": " + s.LastError
				: s.Name + (string.IsNullOrEmpty(s.Link) ? "" : "  -  " + s.Link);
		}

		/// A site that needs an account: ask for the login Jackett wants for it, then add it.
		private void LogInAndAdd(JackettApi.Site site)
		{
			if (Api == null)
			{
				SetupForm.OpenUrl(JackettUrl);
				return;
			}
			object[] config;
			Cursor = Cursors.WaitCursor;
			try
			{
				config = Api.GetConfig(site.Id);
			}
			catch (Exception ex)
			{
				Cursor = Cursors.Default;
				MessageBox.Show(this, "Could not ask Jackett about " + site.Name + ": " + ex.Message, "Log in", MessageBoxButtons.OK, MessageBoxIcon.Warning);
				return;
			}
			Cursor = Cursors.Default;
			string cannot = AccountForm.CannotHandle(config);
			if (cannot != null)
			{
				if (MessageBox.Show(this, site.Name + " has to be added on Jackett's own page, because " + cannot + ".\r\n\r\nOpen Jackett's page now? There, click \"+ Add indexer\" and search for " + site.Name + ".",
					"Log in", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
				{
					SetupForm.OpenUrl(JackettUrl);
				}
				return;
			}
			using (AccountForm f = new AccountForm(Api, site, config) { JackettUrl = JackettUrl })
			{
				if (f.ShowDialog(this) != DialogResult.OK) return;
			}
			site.Configured = true;
			site.LastError = "";
			_ticked.Add(site.Id);
			Fill();
			int idx = _shown.IndexOf(site);
			if (idx >= 0) _list.SelectedIndex = idx;
			_detail.Text = site.Name + " added - Jackett logged in to it.";
		}

		private static JackettApi.Site OwnSite(string name, string url) =>
			new JackettApi.Site { Id = "own:" + url, Name = name, Description = "", Language = "en", Link = url, Configured = true, IsOwn = true, OwnUrl = url, TvOrMovies = true };

		/// "Add your own site": a site Jackett knows is just ticked; any other is worked out and added to My sites.
		private void AddOwnSite()
		{
			string text = _ownAddress.Text.Trim();
			string host = Sohvaperuna.TorrentSearch.SiteFinder.HostName(text);
			if (host.Length == 0)
			{
				MessageBox.Show(this, "Type the site's address, for example  ext.to", "Add your own site");
				return;
			}
			// 1. does Jackett know it? (better: Jackett and FlareSolverr handle tricky sites)
			JackettApi.Site known = _all.FirstOrDefault(s => !s.IsOwn && s.Links.Concat(new[] { s.Link }).Any(l => Sohvaperuna.TorrentSearch.SiteFinder.HostName(l).Equals(host, StringComparison.OrdinalIgnoreCase)));
			if (known != null)
			{
				_ticked.Add(known.Id);
				_find.Text = "";
				_english.Checked = _noAdult.Checked = _tvMovies.Checked = false;
				Fill();
				int idx = _shown.IndexOf(known);
				if (idx >= 0) _list.SelectedIndex = idx;
				_detail.Text = known.Configured ? "Jackett already searches " + known.Name + " (" + host + ")." : "Jackett knows " + host + " as \"" + known.Name + "\" - ticked. Click Save to add it.";
				_ownAddress.Text = "";
				return;
			}
			// 2. work out its search address
			_ownAdd.Enabled = false;
			_ownAddress.Enabled = false;
			Cursor = Cursors.WaitCursor;
			ThreadPool.QueueUserWorkItem(_ =>
			{
				Sohvaperuna.TorrentSearch.SiteFinder.Result r;
				try
				{
					r = Sohvaperuna.TorrentSearch.SiteFinder.Find(text, msg => BeginInvoke((Action)(() => _detail.Text = msg)));
				}
				catch (Exception ex)
				{
					r = new Sohvaperuna.TorrentSearch.SiteFinder.Result { Message = ex.Message };
				}
				if (r.Found)
				{
					try
					{
						Sohvaperuna.MyTorrents.OwnSites.Add(r.Name, r.SearchUrl);
						SetupCore.Log("My sites: added " + r.Name + " " + r.SearchUrl);
					}
					catch (Exception ex)
					{
						r.Found = false;
						r.Message = "Could not save it: " + ex.Message;
					}
				}
				if (IsDisposed) return;
				BeginInvoke((Action)(() =>
				{
					_ownAdd.Enabled = true;
					_ownAddress.Enabled = true;
					Cursor = Cursors.Default;
					if (r.Found)
					{
						JackettApi.Site own = OwnSite(r.Name, r.SearchUrl);
						_all.RemoveAll(s => s.IsOwn && s.OwnUrl.Equals(r.SearchUrl, StringComparison.OrdinalIgnoreCase));
						_all.Add(own);
						_ticked.Add(own.Id);
						_find.Text = "";
						Fill();
						int idx = _shown.IndexOf(own);
						if (idx >= 0) _list.SelectedIndex = idx;
						_detail.Text = r.Name + " added to your own sites (" + r.Results + " test results).";
						_ownAddress.Text = "";
					}
					else
					{
						_detail.Text = "";
						MessageBox.Show(this, r.Message, "Add your own site", MessageBoxButtons.OK, MessageBoxIcon.Information);
					}
				}));
			});
		}

		private static string Short(string s, int n)
		{
			s = (s ?? "").Replace("\r", " ").Replace("\n", " ").Trim();
			return s.Length > n ? s.Substring(0, n) + "..." : s;
		}

		private void UpdateCount()
		{
			int add = ToAdd.Count, remove = ToRemove.Count, inUse = _all.Count(s => s.Configured);
			string changes = add == 0 && remove == 0 ? "no changes" : (add > 0 ? "+" + add + " to add" : "") + (add > 0 && remove > 0 ? ", " : "") + (remove > 0 ? remove + " to remove" : "");
			int ticked = _ticked.Count;
			_count.Text = ticked + " site" + (ticked == 1 ? "" : "s") + " selected (at the top of the list)   -   " + inUse + " in use now   -   " + changes;
		}
	}
}
