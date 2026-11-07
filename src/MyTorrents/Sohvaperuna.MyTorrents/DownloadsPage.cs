using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using Sohvaperuna.TorrentEngine;

namespace Sohvaperuna.MyTorrents
{
	/// <summary>
	/// 2026: "Downloads" tab - the torrent program's list (progress, pause / resume, remove) so downloads can be
	/// looked after from the desktop without starting MediaPortal. Talks to the torrent program directly, using the
	/// connection settings saved on the Configuration tab.
	/// </summary>
	public class DownloadsPage : TabPage
	{
		private sealed class FastListView : ListView
		{
			public FastListView()
			{
				DoubleBuffered = true;
			}
		}

		private ListSorter _sorter; // 2026 (1.3.38)
		private ListSorter _sorter2;
		// 2026 (1.3.42): the first choice splits the page - downloading on top, seeding / finished underneath
		private const string Split = "Downloading + Seeding";
		private static readonly string[] Filters = { Split, "All", "Downloading", "Seeding", "Paused", "Finished", "Problems" };

		private readonly FastListView _list = new FastListView();
		private readonly FastListView _list2 = new FastListView();   // 2026 (1.3.42): the bottom list when split
		private FastListView _active;                                 // the list last clicked
		private readonly Label _topLabel = new Label { AutoSize = true, Font = new Font("Microsoft Sans Serif", 8.25f, FontStyle.Bold) };
		private readonly Label _bottomLabel = new Label { AutoSize = true, Font = new Font("Microsoft Sans Serif", 8.25f, FontStyle.Bold) };
		private readonly ComboBox _filter = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120 };
		private readonly Label _status = new Label { AutoSize = false, ForeColor = Color.DimGray, TextAlign = ContentAlignment.MiddleLeft };
		private readonly Button _pause = new Button { Text = "Pause", Width = 80 };
		private readonly Button _resume = new Button { Text = "Resume", Width = 80 };
		private readonly Button _remove = new Button { Text = "Remove...", Width = 80 };
		private readonly Button _folder = new Button { Text = "Open folder", Width = 90 };
		private readonly System.Windows.Forms.Timer _timer = new System.Windows.Forms.Timer { Interval = 3000 };

		private ITorrentClient _client;
		private string _clientName = "the torrent program";
				private int _busy; // 1 while a refresh is running in the background
		private List<Torrent> _torrents = new List<Torrent>();

		public DownloadsPage()
		{
			Text = "Downloads";
			Padding = new Padding(3);
			UseVisualStyleBackColor = true;
			Font = new Font("Microsoft Sans Serif", 8.25f);

			Label show = new Label { Text = "Show", AutoSize = true, Location = new Point(8, 12) };
			_filter.Location = new Point(50, 8);
			_filter.Items.AddRange(Filters);
			_filter.SelectedIndex = 0;
			_filter.SelectedIndexChanged += (s, e) => { LayoutList(); ShowList(); };
			_pause.Location = new Point(190, 7);
			_resume.Location = new Point(_pause.Right + 4, 7);
			_remove.Location = new Point(_resume.Right + 4, 7);
			_folder.Location = new Point(_remove.Right + 12, 7);
			_pause.Click += (s, e) => Act(t => _client.Pause(t.Hash), "pause");
			_resume.Click += (s, e) => Act(t => _client.Resume(t.Hash), "resume");
			_remove.Click += (s, e) => RemoveSelected();
			_folder.Click += (s, e) => OpenFolder();

			_active = _list;
			_sorter = SetUpList(_list, "Downloads");
			_sorter2 = SetUpList(_list2, "Downloads2");

			_status.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;

			Controls.Add(show);
			Controls.Add(_filter);
			Controls.Add(_pause);
			Controls.Add(_resume);
			Controls.Add(_remove);
			Controls.Add(_folder);
			Controls.Add(_list);
			Controls.Add(_list2);
			Controls.Add(_topLabel);
			Controls.Add(_bottomLabel);
			Controls.Add(_status);

			_timer.Tick += (s, e) => { if (Visible) RefreshNow(); };
			VisibleChanged += (s, e) =>
			{
				if (Visible)
				{
					_timer.Start();
					RefreshNow();
				}
				else
				{
					_timer.Stop();
				}
			};
			Resize += (s, e) => LayoutList();
			UpdateButtons();
		}

		/// 2026 (1.3.42): columns, sorting, keys and the right-click menu - the same for both lists.
		private ListSorter SetUpList(FastListView l, string sortName)
		{
			ListSorter sorter;
			l.View = View.Details;
			l.FullRowSelect = true;
			l.HideSelection = false;
			l.MultiSelect = true;
			l.Location = new Point(8, 38);
			l.Anchor = AnchorStyles.Top | AnchorStyles.Left; // sized by LayoutList
			l.Columns.Add("Name", 250);
			l.Columns.Add("Size", 62, HorizontalAlignment.Right);
			l.Columns.Add("Done", 48, HorizontalAlignment.Right);
			l.Columns.Add("Status", 115);
			l.Columns.Add("Down", 62, HorizontalAlignment.Right);
			l.Columns.Add("Up", 62, HorizontalAlignment.Right);
			l.Columns.Add("Seeds", 48, HorizontalAlignment.Right);
			l.Columns.Add("Ratio", 42, HorizontalAlignment.Right);
			l.Columns.Add("Time left", 62, HorizontalAlignment.Right);
			l.Columns.Add("Library", 58);
			l.Columns.Add("Peers", 52, HorizontalAlignment.Right); // 2026 (1.3.52): connected (in the swarm)
			l.Columns.Add("Seeds:Peers", 70, HorizontalAlignment.Right); // seeds for every downloader in the swarm - higher = faster
			// 2026 (1.3.38): click a heading to sort (sizes, speeds and times sort as numbers)
			sorter = new ListSorter(l, sortName)
				.Key(1, i => (i.Tag as Torrent)?.Size)
				.Key(2, i => (i.Tag as Torrent)?.Progress)
				.Key(4, i => (i.Tag as Torrent)?.DownloadSpeed)
				.Key(5, i => (i.Tag as Torrent)?.UploadSpeed)
				.Key(6, i => (i.Tag as Torrent)?.SeedsConnected)
				.Key(7, i => (i.Tag as Torrent)?.Ratio)
				.Key(8, i => { Torrent x = i.Tag as Torrent; return x == null || x.ETA < 0 ? null : (IComparable)x.ETA; })
				.Key(10, i => (i.Tag as Torrent)?.PeersConnected)
				.Key(11, i => { Torrent x = i.Tag as Torrent; return x == null ? null : (IComparable)SeedPeer(x); });
			// 2026: columns can be dragged into any order; the order is remembered
			l.AllowColumnReorder = true;
			l.ColumnReordered += (s, e) =>
			{
				// fires before the move - save once it has happened
				try
				{
					BeginInvoke((MethodInvoker)(() => SaveColumnOrder(l)));
				}
				catch
				{
				}
			};
			l.SelectedIndexChanged += (s, e) => { if (l.Focused) _active = l; UpdateButtons(); };
			l.Enter += (s, e) =>
			{
				// one list at a time: clicking in one clears the other's selection
				_active = l;
				FastListView other = l == _list ? _list2 : _list;
				other.SelectedItems.Clear();
				UpdateButtons();
			};
			l.DoubleClick += (s, e) => OpenFolder();
			l.KeyDown += (s, e) =>
			{
				if (e.KeyCode == Keys.Delete) RemoveSelected();
				else if (e.KeyCode == Keys.F5) RefreshNow();
				else if (e.KeyCode == Keys.A && e.Control) foreach (ListViewItem i in l.Items) i.Selected = true;
			};
			ContextMenuStrip menu = new ContextMenuStrip();
			menu.Items.Add("Pause", null, (s, e) => Act(t => _client.Pause(t.Hash), "pause"));
			menu.Items.Add("Resume", null, (s, e) => Act(t => _client.Resume(t.Hash), "resume"));
			menu.Items.Add("Remove...", null, (s, e) => RemoveSelected());
			ToolStripItem again = menu.Items.Add("Delete and search again", null, (s, e) => DeleteAndSearchAgain()); // 2026 (1.3.42)
			// 2026 (1.3.53): a download stuck at 99.9% usually plays fine - check it and copy it to the library
			ToolStripItem useNow = menu.Items.Add("Use it now (nearly finished)", null, (s, e) => UseNow());
			menu.Opening += (s, e) =>
			{
				_active = l;
				again.Enabled = Selected().Any(t => Grabbed.FindByHash(t.Hash) != null);
				useNow.Enabled = Selected().Any(t => t.Progress >= 95.0 && t.Progress < 100.0);
			};
			menu.Items.Add(new ToolStripSeparator());
			menu.Items.Add("Open folder", null, (s, e) => OpenFolder());
			// 2026 (1.3.52): look it up
			menu.Items.Add("Look it up on TheTVDB", null, (s, e) => { Torrent t = Selected().FirstOrDefault(); if (t != null) LookUp.ForName(PostProcessor.DisplayName(t), true); });
			menu.Items.Add("Look it up on TMDb", null, (s, e) => { Torrent t = Selected().FirstOrDefault(); if (t != null) LookUp.ForName(PostProcessor.DisplayName(t), false); });
			menu.Items.Add("Refresh now", null, (s, e) => RefreshNow());
			l.ContextMenuStrip = menu;

			l.MouseDown += (s, e) =>
			{
				_active = l;
				if (e.Button != MouseButtons.Right) return;
				ListViewItem hit = l.GetItemAt(e.X, e.Y);
				if (hit != null && !hit.Selected) { l.SelectedItems.Clear(); hit.Selected = true; }
			};
			return sorter;
		}

		private bool IsSplit => (_filter.SelectedItem as string ?? Split) == Split;

		private void LayoutList()
		{
			int w = Math.Max(100, ClientSize.Width - 16);
			int top = 38, bottom = ClientSize.Height - 28;
			if (IsSplit)
			{
				// 2026 (1.3.42): downloading on top, seeding / finished underneath
				int each = Math.Max(60, (bottom - top - 2 * 18 - 6) / 2);
				_topLabel.Location = new Point(8, top);
				_list.Location = new Point(8, top + 16);
				_list.Size = new Size(w, each);
				_bottomLabel.Location = new Point(8, _list.Bottom + 6);
				_list2.Location = new Point(8, _bottomLabel.Top + 16);
				_list2.Size = new Size(w, Math.Max(60, bottom - _list2.Top));
				_topLabel.Visible = _bottomLabel.Visible = _list2.Visible = true;
			}
			else
			{
				_list.Location = new Point(8, top);
				_list.Size = new Size(w, Math.Max(60, bottom - top));
				_topLabel.Visible = _bottomLabel.Visible = _list2.Visible = false;
				_active = _list;
			}
			_status.Location = new Point(8, ClientSize.Height - 24);
			_status.Size = new Size(w, 20);
			// the name gets whatever width the other columns leave
			foreach (FastListView l in new[] { _list, _list2 })
			{
				if (l.Columns.Count <= 1) continue;
				int others = 0;
				for (int i = 1; i < l.Columns.Count; i++) others += l.Columns[i].Width;
				l.Columns[0].Width = Math.Max(200, l.ClientSize.Width - others - 4);
			}
		}

		protected override void OnHandleCreated(EventArgs e)
		{
			base.OnHandleCreated(e);
			ApplyColumnOrder();
			LayoutList();
		}

		/// Column numbers (as added above) in the order they are shown: Name, Size, Done, Library, Status, Down, Up,
		/// Seeds, Ratio, Time left
		private static readonly int[] DefaultOrder = { 0, 1, 2, 9, 3, 4, 5, 6, 10, 11, 7, 8 };

		private static string OrderFile
		{
			get
			{
				try
				{
					return MediaPortal.Configuration.Config.GetFile(MediaPortal.Configuration.Config.Dir.Config, "MyTorrents.downloads-columns.txt");
				}
				catch
				{
					return null;
				}
			}
		}

		/// The saved order, or the default one when there is none (or it does not fit these columns).
		private int[] LoadColumnOrder()
		{
			try
			{
				string file = OrderFile;
				if (file != null && System.IO.File.Exists(file))
				{
					int[] order = System.IO.File.ReadAllText(file).Split(new[] { ',', ' ', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToArray();
					if (order.Length == _list.Columns.Count && order.OrderBy(x => x).SequenceEqual(Enumerable.Range(0, _list.Columns.Count))) return order;
					// 1.3.52: saved before Peers / Seeds:Peers were added - keep it, put the new ones after Seeds
					if (order.Length < _list.Columns.Count && order.OrderBy(x => x).SequenceEqual(Enumerable.Range(0, order.Length)))
					{
						List<int> o = order.ToList();
						int at = o.IndexOf(6) + 1;
						foreach (int extra in Enumerable.Range(order.Length, _list.Columns.Count - order.Length)) o.Insert(Math.Min(at++, o.Count), extra);
						return o.ToArray();
					}
				}
			}
			catch
			{
			}
			return DefaultOrder.Length == _list.Columns.Count ? DefaultOrder : Enumerable.Range(0, _list.Columns.Count).ToArray();
		}

		private void ApplyColumnOrder()
		{
			int[] order = LoadColumnOrder();
			foreach (FastListView l in new[] { _list, _list2 })
			{
				for (int position = 0; position < order.Length; position++)
				{
					l.Columns[order[position]].DisplayIndex = position;
				}
			}
		}

		private void SaveColumnOrder(FastListView from)
		{
			try
			{
				string file = OrderFile;
				if (file == null) return;
				int[] order = from.Columns.Cast<ColumnHeader>().OrderBy(c => c.DisplayIndex).Select(c => c.Index).ToArray();
				System.IO.File.WriteAllText(file, string.Join(",", order));
				FastListView other = from == _list ? _list2 : _list;
				for (int position = 0; position < order.Length; position++) other.Columns[order[position]].DisplayIndex = position;
			}
			catch
			{
			}
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				_timer.Stop();
				_timer.Dispose();
			}
			base.Dispose(disposing);
		}

		/// Connects with the settings saved on the Configuration tab (called on the window's thread; the
		/// connection itself is made in the background by the refresh).
		private ConfigData? NextConfig()
		{
			if (_client != null) return null;
			try
			{
				ConfigData config = Configuration.Instance().LoadConfiguration();
				_clientName = config.Backend == Backend.BiglyBT ? "BiglyBT" : config.Backend.ToString();
				return config;
			}
			catch
			{
				return null;
			}
		}

		private bool EnsureClient(ConfigData? config)
		{
			if (_client != null) return true;
			if (config == null) return false;
			int port;
			if (!int.TryParse(config.Value.Port, out port)) return false;
			ITorrentClient client = TorrentEngine.CreateClient(config.Value.Server, port, config.Value.User, config.Value.Password, config.Value.Backend);
			client.Connect();
			_client = client;
			return true;
		}

		public void RefreshNow()
		{
			if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0) return;
			ConfigData? config = NextConfig();
			ThreadPool.QueueUserWorkItem(_ =>
			{
				List<Torrent> torrents = null;
				string error = null;
				try
				{
					if (!EnsureClient(config))
					{
						error = "Could not read the connection settings - check the address and port on the Configuration tab.";
					}
					else if (!_client.UpdateData())
					{
						error = "Can't reach " + _clientName + " - is it running? (address and port are on the Configuration tab)";
						_client = null;
					}
					else
					{
						torrents = _client.TorrentsAll.Cast<Torrent>().ToList();
					}
				}
				catch (Exception ex)
				{
					error = "Can't reach " + _clientName + ": " + ex.Message;
					_client = null;
				}
				finally
				{
					Interlocked.Exchange(ref _busy, 0);
				}
				try
				{
					if (IsHandleCreated && !IsDisposed)
					{
						BeginInvoke((MethodInvoker)(() =>
						{
							if (torrents != null)
							{
								_torrents = torrents;
								ShowList();
							}
							else
							{
								_torrents = new List<Torrent>();
								_list.Items.Clear();
								_list2.Items.Clear();
								_status.Text = error;
								UpdateButtons();
							}
						}));
					}
				}
				catch
				{
				}
			});
		}

		private static bool Fits(Torrent t, string filter)
		{
			string m = t.StatusMessage ?? "";
			switch (filter)
			{
			case "Downloading": return t.Progress < 100.0 && !t.HasStatus(ClientSupport.Paused) && !t.HasStatus(ClientSupport.Error);
			case "Seeding": return m.StartsWith("Seeding");
			case "Paused": return t.HasStatus(ClientSupport.Paused);
			case "Finished": return t.Progress >= 100.0;
			case "Problems": return t.HasStatus(ClientSupport.Error) || IsStuck(t) || NoSeeds(t);
			default: return true;
			}
		}

		/// No details yet (only the magnet's hash is known) - the Auto grabber gives up on these after 2 hours.
		private static bool IsStuck(Torrent t) => t.Size <= 0 || string.IsNullOrEmpty(t.Name) || t.Name.Equals(t.Hash, StringComparison.OrdinalIgnoreCase);

		/// Fills the list(s), keeping the selection and scroll position.
		private void ShowList()
		{
			string filter = _filter.SelectedItem as string ?? Split;
			int shownCount;
			if (IsSplit)
			{
				List<Torrent> downloading = _torrents.Where(t => t.Progress < 100.0).OrderByDescending(t => t.AddedOn).ToList();
				List<Torrent> seeding = _torrents.Where(t => t.Progress >= 100.0).OrderByDescending(t => t.AddedOn).ToList();
				Fill(_list, _sorter, downloading);
				Fill(_list2, _sorter2, seeding);
				_topLabel.Text = "Downloading (" + downloading.Count + ")";
				_bottomLabel.Text = "Seeding and finished (" + seeding.Count + ")";
				shownCount = _torrents.Count;
			}
			else
			{
				List<Torrent> shown = _torrents.Where(t => Fits(t, filter)).OrderByDescending(t => t.AddedOn).ToList();
				Fill(_list, _sorter, shown);
				shownCount = shown.Count;
			}
			long down = _torrents.Sum(t => t.DownloadSpeed);
			long up = _torrents.Sum(t => t.UploadSpeed);
			_status.Text = $"{_clientName}:  {_torrents.Count} torrent(s), {_torrents.Count(t => t.Progress < 100.0)} still downloading" +
				(filter == "All" || IsSplit ? "" : $", {shownCount} shown") + $"   ·   down {Speed(down)}   up {Speed(up)}";
			UpdateButtons();
		}

		private void Fill(FastListView list, ListSorter sorter, List<Torrent> shown)
		{
			HashSet<string> selected = new HashSet<string>(list.SelectedItems.Cast<ListViewItem>().Select(i => i.Name));
			list.BeginUpdate();
			try
			{
				Dictionary<string, ListViewItem> have = list.Items.Cast<ListViewItem>().ToDictionary(i => i.Name);
				HashSet<string> keep = new HashSet<string>(shown.Select(t => t.Hash));
				foreach (ListViewItem old in have.Values.Where(i => !keep.Contains(i.Name)).ToList()) list.Items.Remove(old);
				for (int n = 0; n < shown.Count; n++)
				{
					Torrent t = shown[n];
					string[] cells = Cells(t);
					ListViewItem item;
					if (have.TryGetValue(t.Hash, out item) && list.Items.Contains(item))
					{
						for (int c = 0; c < cells.Length; c++)
						{
							if (item.SubItems[c].Text != cells[c]) item.SubItems[c].Text = cells[c];
						}
						if (item.Index != n)
						{
							list.Items.Remove(item);
							list.Items.Insert(n, item);
						}
					}
					else
					{
						item = new ListViewItem(cells) { Name = t.Hash };
						list.Items.Insert(n, item);
					}
					item.Tag = t;
					item.ForeColor = t.HasStatus(ClientSupport.Error) ? Color.Firebrick : IsStuck(t) || NoSeeds(t) ? Color.DarkOrange : SystemColors.WindowText;
					item.Selected = selected.Contains(t.Hash);
				}
			}
			finally
			{
				sorter.Apply();
				list.EndUpdate();
			}
		}

		/// 2026 (1.3.42): downloading but nobody has the whole thing - may never finish
		private static bool NoSeeds(Torrent t) => t.Progress < 100.0 && !t.HasStatus(ClientSupport.Paused) && t.Size > 0 && t.SeedsConnected == 0 && t.SeedsInSwarm == 0 && t.DownloadSpeed <= 0;

		internal static string[] Cells(Torrent t)
		{
			bool stuck = IsStuck(t);
			string status = stuck && !t.HasStatus(ClientSupport.Paused) ? "Fetching details" : NoSeeds(t) ? "No seeds - stuck?" : (t.StatusMessage ?? "");
			string left = t.Progress >= 100.0 ? "" : t.ETA > 0 ? Duration(t.ETA) : (t.DownloadSpeed > 0 ? "" : "\u221E");
			return new[]
			{
				t.Name,
				t.Size > 0 ? Bytes(t.Size) : "?",
				t.Progress.ToString("0.#") + "%",
				status,
				t.DownloadSpeed > 0 ? Speed(t.DownloadSpeed) : "",
				t.UploadSpeed > 0 ? Speed(t.UploadSpeed) : "",
				t.SeedsConnected + " (" + t.SeedsInSwarm + ")",
				(t.Ratio / 1000.0).ToString("0.00"),
				left,
				(t.Tags ?? "").IndexOf(PostProcessor.CopiedTag, StringComparison.OrdinalIgnoreCase) >= 0 ? "copied" : "",
				t.PeersConnected + " (" + t.PeersInSwarm + ")",
				SeedPeerText(t)
			};
		}

		/// 2026 (1.3.52): seeds in the swarm for every other downloader (peer) - e.g. 30 seeds, 10 peers = 3.0.
		/// No peers but seeds = plenty (shown as "all seeds"); neither = nothing known.
		internal static double SeedPeer(Torrent t)
		{
			if (t.SeedsInSwarm <= 0) return 0;
			if (t.PeersInSwarm <= 0) return 9999;
			return (double)t.SeedsInSwarm / t.PeersInSwarm;
		}

		internal static string SeedPeerText(Torrent t)
		{
			if (t.SeedsInSwarm <= 0 && t.PeersInSwarm <= 0) return "";
			if (t.SeedsInSwarm <= 0) return "0";
			if (t.PeersInSwarm <= 0) return "all seeds";
			double r = SeedPeer(t);
			return r >= 10 ? r.ToString("0") : r.ToString("0.0");
		}

		private static string Bytes(long b)
		{
			if (b >= 1L << 30) return (b / (double)(1L << 30)).ToString("0.00") + " GB";
			if (b >= 1L << 20) return (b / (double)(1L << 20)).ToString("0") + " MB";
			return (b / 1024.0).ToString("0") + " KB";
		}

		private static string Speed(long bytesPerSecond)
		{
			if (bytesPerSecond >= 1L << 20) return (bytesPerSecond / (double)(1L << 20)).ToString("0.0") + " MB/s";
			return (bytesPerSecond / 1024.0).ToString("0") + " KB/s";
		}

		private static string Duration(int seconds)
		{
			TimeSpan s = TimeSpan.FromSeconds(seconds);
			if (s.TotalDays >= 1) return (int)s.TotalDays + "d " + s.Hours + "h";
			if (s.TotalHours >= 1) return (int)s.TotalHours + "h " + s.Minutes + "m";
			return Math.Max(1, s.Minutes) + "m";
		}

		private List<Torrent> Selected() => (IsSplit && _active != null ? _active : _list).SelectedItems.Cast<ListViewItem>().Select(i => i.Tag as Torrent).Where(t => t != null).ToList();

		private void UpdateButtons()
		{
			List<Torrent> sel = Selected();
			bool any = sel.Count > 0 && _client != null;
			_pause.Enabled = any && sel.Any(t => !t.HasStatus(ClientSupport.Paused) && t.Progress < 100.0);
			_resume.Enabled = any && sel.Any(t => t.HasStatus(ClientSupport.Paused) || (t.StatusMessage ?? "") == "Finished");
			_remove.Enabled = any;
			_folder.Enabled = sel.Count == 1 && !string.IsNullOrEmpty(sel[0].TorrentSavePath);
		}

		/// Runs an action on every selected torrent (in the background), then refreshes.
		private void UseNow()
		{
			List<string> notes = new List<string>();
			foreach (Torrent t in Selected())
			{
				string why = PostProcessor.UseNow(_client, t);
				notes.Add(PostProcessor.DisplayName(t) + ": " + (why ?? "being checked - if the video looks whole it is copied to your library (a message pops up)"));
			}
			if (notes.Count > 0) MessageBox.Show(this, string.Join("\n\n", notes), "Use it now", MessageBoxButtons.OK, MessageBoxIcon.Information);
		}

		private void Act(Func<Torrent, bool> action, string what)
		{
			List<Torrent> sel = Selected();
			if (sel.Count == 0 || _client == null) return;
			ThreadPool.QueueUserWorkItem(_ =>
			{
				int failed = 0;
				foreach (Torrent t in sel)
				{
					try
					{
						if (!action(t)) failed++;
					}
					catch
					{
						failed++;
					}
				}
				try
				{
					BeginInvoke((MethodInvoker)(() =>
					{
						if (failed > 0) MessageBox.Show(this, $"Could not {what} {failed} of {sel.Count} torrent(s).", "MyTorrents", MessageBoxButtons.OK, MessageBoxIcon.Warning);
						RefreshNow();
					}));
				}
				catch
				{
				}
			});
		}

		private void RemoveSelected()
		{
			List<Torrent> sel = Selected();
			if (sel.Count == 0 || _client == null) return;
			string names = string.Join("\r\n", sel.Take(5).Select(t => "  " + t.Name)) + (sel.Count > 5 ? $"\r\n  ...and {sel.Count - 5} more" : "");
			DialogResult files = MessageBox.Show(this,
				"Remove from " + _clientName + ":\r\n\r\n" + names + "\r\n\r\nDelete the downloaded files as well?\r\n\r\n" +
				"Yes = delete the files too\r\nNo = keep the files (copies already in your library are never touched)",
				"Remove torrents", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question, MessageBoxDefaultButton.Button3);
			if (files == DialogResult.Cancel) return;
			bool deleteFiles = files == DialogResult.Yes;

			// Auto grabber downloads that had not finished: offer to look for another copy straight away
			List<Torrent> grabbed = sel.Where(t => t.Progress < 100.0 && Grabbed.FindByHash(t.Hash) != null).ToList();
			bool retry = false;
			if (grabbed.Count > 0)
			{
				retry = MessageBox.Show(this,
					(grabbed.Count == 1 ? "This was started by the Auto grabber" : $"{grabbed.Count} of these were started by the Auto grabber") +
					" and had not finished.\r\n\r\nLook for another copy of " + (grabbed.Count == 1 ? "that episode" : "those episodes") + "?\r\n\r\n" +
					"Yes = try a different copy at the next check (this one is not used again)\r\nNo = leave the episode for now",
					"Auto grabber", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
			}
			ThreadPool.QueueUserWorkItem(_ =>
			{
				int failed = 0;
				foreach (Torrent t in sel)
				{
					bool ok;
					try
					{
						ok = _client.Remove(t.Hash, deleteFiles);
					}
					catch
					{
						ok = false;
					}
					if (!ok)
					{
						failed++;
						continue;
					}
					Log.Instance().Print($"Downloads tab: removed {t.Name}{(deleteFiles ? " and its files" : "")}");
					if (retry && grabbed.Contains(t)) Grabbed.RequestRetry(t.Hash);
				}
				try
				{
					BeginInvoke((MethodInvoker)(() =>
					{
						if (failed > 0) MessageBox.Show(this, $"Could not remove {failed} of {sel.Count} torrent(s).", "MyTorrents", MessageBoxButtons.OK, MessageBoxIcon.Warning);
						RefreshNow();
					}));
				}
				catch
				{
				}
			});
		}

		/// 2026 (1.3.42): right-click "Delete and search again" - removes the copy and its files and the Auto grabber
		/// looks for a different copy (this one is not used again). Only for copies the Auto grabber started.
		private void DeleteAndSearchAgain()
		{
			List<Torrent> sel = Selected().Where(t => Grabbed.FindByHash(t.Hash) != null).ToList();
			if (sel.Count == 0 || _client == null) return;
			if (MessageBox.Show(this, "Delete " + (sel.Count == 1 ? sel[0].Name : sel.Count + " downloads") + " and its files, and look for a different copy?\r\n\r\n" +
				"Copies already in your library are never touched.", "Delete and search again", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return;
			ThreadPool.QueueUserWorkItem(_ =>
			{
				int failed = 0;
				foreach (Torrent t in sel)
				{
					try
					{
						if (_client.Remove(t.Hash, true))
						{
							Grabbed.RequestRetry(t.Hash);
							Log.Instance().Print($"Downloads tab: deleted {t.Name} - the Auto grabber looks for another copy");
						}
						else failed++;
					}
					catch
					{
						failed++;
					}
				}
				try
				{
					BeginInvoke((MethodInvoker)(() =>
					{
						if (failed > 0) MessageBox.Show(this, $"Could not delete {failed} of {sel.Count}.", "MyTorrents", MessageBoxButtons.OK, MessageBoxIcon.Warning);
						RefreshNow();
					}));
				}
				catch
				{
				}
			});
		}

		private void OpenFolder()
		{
			List<Torrent> sel = Selected();
			if (sel.Count != 1 || string.IsNullOrEmpty(sel[0].TorrentSavePath)) return;
			try
			{
				System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", "\"" + sel[0].TorrentSavePath.TrimEnd('\\', '/').Replace('/', '\\') + "\"") { UseShellExecute = true });
			}
			catch (Exception ex)
			{
				MessageBox.Show(this, "Could not open the folder: " + ex.Message, "MyTorrents");
			}
		}
	}
}
