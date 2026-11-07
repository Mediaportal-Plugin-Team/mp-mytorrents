using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Xml;
using MediaPortal.Configuration;
using Sohvaperuna.TorrentSearch;

namespace Sohvaperuna.MyTorrents
{
	/// <summary>
	/// "Search sites" tab of the MyTorrents settings window (2026):
	/// - Jackett address / API key
	/// - your own sites: a name and a search address containing %search%
	/// Everything is saved into MyTorrents.xml.
	/// </summary>
	public class SearchSitesPage : TabPage
	{
		private readonly string _configFile = Config.GetFile(Config.Dir.Config, "MyTorrents.xml");

		private readonly CheckBox _jackettEnabled = new CheckBox { Text = "Use Jackett", AutoSize = true };
		private readonly TextBox _jackettUrl = new TextBox();
		private readonly TextBox _jackettKey = new TextBox();
		private readonly Button _jackettTest = new Button { Text = "Test" };
		private readonly DataGridView _grid = new DataGridView();
		private readonly TextBox _testWords = new TextBox { Text = "ubuntu" };
		private readonly Button _siteTest = new Button { Text = "Test site" };
		private readonly Button _siteRemove = new Button { Text = "Remove" };

		/// Official Jackett download page (Windows installer: Jackett.Installer.Windows.exe)
		public const string JackettDownloadUrl = "https://github.com/Jackett/Jackett/releases/latest";

		private const string JackettHelp =
			"Jackett is a free program that searches torrent sites for MyTorrents. Each site it searches is called an \"indexer\".\r\n\r\n" +
			"1.  Installing: the setup assistant does it for you (Configuration tab > Setup assistant...). Jackett then runs quietly in the background as a Windows service - you never need to open it.\r\n\r\n" +
			"2.  Adding sites: click \"Add sites...\" and tick the sites you want. Public sites need no account. A handful of well-known ones is plenty - each one adds a little time to every search.\r\n\r\n" +
			"3.  Sites that need an account (private trackers): click \"Open Jackett\", then \"+ Add indexer\", and enter your login there.\r\n\r\n" +
			"4.  Address and API key are filled in by the setup assistant. Click Test to check them.";

		private static void OpenLink(string url)
		{
			try
			{
				if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase)) url = "http://" + url;
				System.Diagnostics.Process.Start(url);
			}
			catch (Exception ex)
			{
				MessageBox.Show("Could not open " + url + "\r\n" + ex.Message, "MyTorrents");
			}
		}

		public SearchSitesPage()
		{
			Text = "Search sites";
			Padding = new Padding(6);
			UseVisualStyleBackColor = true;

			GroupBox jackett = new GroupBox { Text = "Jackett (500+ ready-made sites - add them in Jackett's web page)", Location = new Point(6, 6), Size = new Size(548, 82) };
			jackett.Controls.Add(_jackettEnabled);
			_jackettEnabled.Location = new Point(10, 20);
			jackett.Controls.Add(new Label { Text = "Address", Location = new Point(110, 22), AutoSize = true });
			_jackettUrl.Location = new Point(170, 19);
			_jackettUrl.Width = 130;
			jackett.Controls.Add(_jackettUrl);
			jackett.Controls.Add(new Label { Text = "API key", Location = new Point(110, 52), AutoSize = true });
			_jackettKey.Location = new Point(170, 49);
			_jackettKey.Width = 280;
			jackett.Controls.Add(_jackettKey);
			LinkLabel addSites = new LinkLabel { Text = "Add sites...", Location = new Point(308, 22), AutoSize = true, Font = new Font(Font, FontStyle.Bold) };
			addSites.LinkClicked += (s, e) => AddSites();
			jackett.Controls.Add(addSites);
			LinkLabel open = new LinkLabel { Text = "Open Jackett", Location = new Point(408, 22), AutoSize = true };
			open.LinkClicked += (s, e) => OpenLink(string.IsNullOrWhiteSpace(_jackettUrl.Text) ? "http://127.0.0.1:9117" : _jackettUrl.Text.Trim());
			jackett.Controls.Add(open);
			LinkLabel help = new LinkLabel { Text = "Setup help", Location = new Point(482, 22), AutoSize = true };
			help.LinkClicked += (s, e) => MessageBox.Show(JackettHelp, "Setting up Jackett", MessageBoxButtons.OK, MessageBoxIcon.Information);
			jackett.Controls.Add(help);
			_jackettTest.Location = new Point(460, 47);
			_jackettTest.Width = 75;
			_jackettTest.Click += JackettTest_Click;
			jackett.Controls.Add(_jackettTest);
			Controls.Add(jackett);

			GroupBox sites = new GroupBox { Text = "My sites", Location = new Point(6, 94), Size = new Size(548, 196) };
			sites.Controls.Add(new Label
			{
				Text = "Easiest: click \"Add by address...\" and type just the site, e.g.  ext.to\r\nOr type a name and the search address yourself, with %search% where the words go.",
				Location = new Point(8, 16),
				Size = new Size(535, 30)
			});
			_grid.Location = new Point(8, 48);
			_grid.Size = new Size(532, 112);
			_grid.AllowUserToAddRows = true;
			_grid.AllowUserToResizeRows = false;
			_grid.RowHeadersVisible = false;
			_grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
			_grid.BackgroundColor = SystemColors.Window;
			_grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "On", HeaderText = "On", Width = 32 });
			_grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "SiteName", HeaderText = "Name", Width = 110 });
			_grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Url", HeaderText = "Search address", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
			_grid.DefaultValuesNeeded += (s, e) => e.Row.Cells["On"].Value = true;
			sites.Controls.Add(_grid);
			sites.Controls.Add(new Label { Text = "Test words", Location = new Point(8, 169), AutoSize = true });
			_testWords.Location = new Point(75, 166);
			_testWords.Width = 150;
			sites.Controls.Add(_testWords);
			_siteTest.Location = new Point(232, 164);
			_siteTest.Width = 80;
			_siteTest.Click += SiteTest_Click;
			sites.Controls.Add(_siteTest);
			_siteRemove.Location = new Point(460, 164);
			_siteRemove.Width = 80;
			_siteRemove.Click += (s, e) =>
			{
				foreach (DataGridViewRow r in _grid.SelectedRows.Cast<DataGridViewRow>().Where(r => !r.IsNewRow).ToList())
				{
					_grid.Rows.Remove(r);
				}
			};
			sites.Controls.Add(_siteRemove);
			Button byAddress = new Button { Text = "Add by address...", Location = new Point(330, 164), Width = 120 };
			byAddress.Click += (s, e) => AddByAddress();
			sites.Controls.Add(byAddress);
			Controls.Add(sites);

			LoadFromFile();
		}

		private XmlDocument LoadDoc()
		{
			XmlDocument doc = new XmlDocument();
			try
			{
				if (File.Exists(_configFile))
				{
					doc.Load(_configFile);
					if (doc.SelectSingleNode("config") != null) return doc;
				}
			}
			catch (Exception ex)
			{
				Log.Instance().Print("SearchSitesPage: could not read MyTorrents.xml: " + ex.Message);
			}
			doc.LoadXml("<config><jackett><enabled>true</enabled><url>http://127.0.0.1:9117</url><apikey></apikey></jackett><iconmapping/><feed/><sites/><searchengines/></config>");
			return doc;
		}

		private void LoadFromFile()
		{
			XmlDocument doc = LoadDoc();
			XmlNode j = doc.SelectSingleNode("config/jackett");
			_jackettEnabled.Checked = !string.Equals(j?.SelectSingleNode("enabled")?.InnerText?.Trim(), "false", StringComparison.OrdinalIgnoreCase);
			_jackettUrl.Text = j?.SelectSingleNode("url")?.InnerText?.Trim() ?? "http://127.0.0.1:9117";
			_jackettKey.Text = j?.SelectSingleNode("apikey")?.InnerText?.Trim() ?? "";
			foreach (SimpleSiteSearchProvider site in SimpleSiteSearchProvider.LoadAll(doc))
			{
				_grid.Rows.Add(!string.Equals(site.Enabled, "false", StringComparison.OrdinalIgnoreCase), site.Name, site.SearchUrl);
			}
		}

		/// Writes the Jackett settings and the site list into MyTorrents.xml (other sections are kept).
		public void Save()
		{
			try
			{
				_grid.EndEdit();
				XmlDocument doc = LoadDoc();
				XmlNode root = doc.SelectSingleNode("config");
				XmlNode j = root.SelectSingleNode("jackett") ?? root.PrependChild(doc.CreateElement("jackett"));
				j.RemoveAll();
				Add(doc, j, "enabled", _jackettEnabled.Checked ? "true" : "false");
				Add(doc, j, "url", _jackettUrl.Text.Trim());
				Add(doc, j, "apikey", _jackettKey.Text.Trim());

				XmlNode sites = root.SelectSingleNode("sites");
				if (sites == null)
				{
					sites = doc.CreateElement("sites");
					XmlNode engines = root.SelectSingleNode("searchengines");
					if (engines != null) root.InsertBefore(sites, engines); else root.AppendChild(sites);
				}
				sites.RemoveAll();
				foreach (DataGridViewRow r in _grid.Rows)
				{
					if (r.IsNewRow) continue;
					string name = (r.Cells["SiteName"].Value as string ?? "").Trim();
					string url = (r.Cells["Url"].Value as string ?? "").Trim();
					if (name.Length == 0 || url.Length == 0) continue;
					XmlNode site = sites.AppendChild(doc.CreateElement("site"));
					Add(doc, site, "name", name);
					Add(doc, site, "url", url);
					Add(doc, site, "enabled", (r.Cells["On"].Value as bool? ?? true) ? "true" : "false");
				}
				doc.Save(_configFile);
			}
			catch (Exception ex)
			{
				MessageBox.Show("Could not save the search sites to MyTorrents.xml:\r\n" + ex.Message, "MyTorrents", MessageBoxButtons.OK, MessageBoxIcon.Warning);
			}
		}

		private static void Add(XmlDocument doc, XmlNode parent, string name, string value)
		{
			parent.AppendChild(doc.CreateElement(name)).InnerText = value;
		}

		private void SiteTest_Click(object sender, EventArgs e)
		{
			DataGridViewRow row = _grid.CurrentRow;
			_grid.EndEdit();
			string url = (row?.Cells["Url"].Value as string ?? "").Trim();
			string name = (row?.Cells["SiteName"].Value as string ?? "").Trim();
			if (row == null || url.Length == 0)
			{
				MessageBox.Show("Click on a site in the list first.", "Test site");
				return;
			}
			if (!url.Contains("%search"))
			{
				MessageBox.Show("The search address needs %search% where the search words go.\r\n\r\nTip: search for something on the site in your browser, copy the address, and replace the words you searched for with %search%", "Test site");
				return;
			}
			Cursor = Cursors.WaitCursor;
			try
			{
				SimpleSiteSearchProvider site = new SimpleSiteSearchProvider { Name = name, SearchUrl = url, Enabled = "true" };
				TorrentMatchList results = site.Search(_testWords.Text, "Default", null);
				if (results.Count == 0)
				{
					MessageBox.Show($"{name}: nothing found.\r\n\r\n{site.LastError}", "Test site", MessageBoxButtons.OK, MessageBoxIcon.Warning);
				}
				else
				{
					string sample = string.Join("\r\n", results.Take(5).Select(m => "  • " + m.Title + (m.Seed > 0 ? $"  ({m.Seed} seeds)" : "")));
					MessageBox.Show($"{name} works - {results.Count} results, e.g.:\r\n\r\n{sample}", "Test site", MessageBoxButtons.OK, MessageBoxIcon.Information);
				}
			}
			finally
			{
				Cursor = Cursors.Default;
			}
		}

		private void ReloadMySites()
		{
			try
			{
				_grid.Rows.Clear();
				foreach (SimpleSiteSearchProvider site in SimpleSiteSearchProvider.LoadAll(LoadDoc()))
				{
					_grid.Rows.Add(!string.Equals(site.Enabled, "false", StringComparison.OrdinalIgnoreCase), site.Name, site.SearchUrl);
				}
			}
			catch (Exception ex)
			{
				Log.Instance().Print("Search sites tab: could not reload My sites: " + ex.Message);
			}
		}

		/// Works out a site's search address from just its name (Sohvaperuna.TorrentSearch.SiteFinder).
		private void AddByAddress()
		{
			string address = Prompt("Add a site by its address", "Type the site's address, for example  ext.to");
			if (string.IsNullOrWhiteSpace(address)) return;
			Cursor = Cursors.WaitCursor;
			SiteFinder.Result r;
			try
			{
				r = SiteFinder.Find(address, null);
			}
			finally
			{
				Cursor = Cursors.Default;
			}
			if (!r.Found)
			{
				MessageBox.Show(this, r.Message, "Add by address", MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}
			_grid.Rows.Add(true, r.Name, r.SearchUrl);
			MessageBox.Show(this, r.Message + "\r\n\r\nIt is in My sites now - click Ok to keep it.", "Add by address", MessageBoxButtons.OK, MessageBoxIcon.Information);
		}

		private string Prompt(string title, string text)
		{
			using (Form f = new Form { Text = title, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false, StartPosition = FormStartPosition.CenterParent, ClientSize = new Size(380, 110) })
			{
				f.Controls.Add(new Label { Text = text, Location = new Point(10, 10), AutoSize = true });
				TextBox box = new TextBox { Location = new Point(10, 34), Width = 360 };
				Button ok = new Button { Text = "Find it", DialogResult = DialogResult.OK, Location = new Point(214, 72), Width = 75 };
				Button cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(295, 72), Width = 75 };
				f.Controls.AddRange(new Control[] { box, ok, cancel });
				f.AcceptButton = ok;
				f.CancelButton = cancel;
				return f.ShowDialog(this) == DialogResult.OK ? box.Text.Trim() : null;
			}
		}

		/// Opens the simple site picker ("MyTorrents Configuration.exe /sites").
		private void AddSites()
		{
			string exe = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MyTorrents Configuration.exe");
			if (!System.IO.File.Exists(exe))
			{
				MessageBox.Show(this, "Open Jackett and click \"+ Add indexer\" to add sites.", "Add sites");
				OpenLink(string.IsNullOrWhiteSpace(_jackettUrl.Text) ? "http://127.0.0.1:9117" : _jackettUrl.Text.Trim());
				return;
			}
			try
			{
				System.Diagnostics.Process p = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exe, "/sites") { UseShellExecute = true });
				if (p != null)
				{
					// the picker may add or remove your own sites - show them here too, so Ok does not undo that
					p.EnableRaisingEvents = true;
					p.Exited += (s, e) =>
					{
						if (!IsDisposed) BeginInvoke((Action)ReloadMySites);
					};
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show(this, "Could not open the site picker: " + ex.Message, "Add sites");
			}
		}

		private void JackettTest_Click(object sender, EventArgs e)
		{
			Cursor = Cursors.WaitCursor;
			try
			{
				XmlDocument doc = new XmlDocument();
				doc.LoadXml("<config><jackett><enabled>true</enabled><url/><apikey/></jackett></config>");
				doc.SelectSingleNode("config/jackett/url").InnerText = _jackettUrl.Text.Trim();
				doc.SelectSingleNode("config/jackett/apikey").InnerText = _jackettKey.Text.Trim();
				JackettAggregateEngine.Configure(doc);
				JackettAggregateEngine engine = new JackettAggregateEngine();
				int count = engine.Engines.Count(x => x.Name != JackettAggregateEngine.AllIndexersName);
				if (count > 0)
				{
					MessageBox.Show($"Jackett works - {count} sites:\r\n\r\n" + string.Join(", ", engine.Engines.Where(x => x.Name != JackettAggregateEngine.AllIndexersName).Select(x => x.Name)), "Jackett", MessageBoxButtons.OK, MessageBoxIcon.Information);
				}
				else
				{
					string why;
					switch (JackettAggregateEngine.LastStatus)
					{
					case "ok":
						why = "Jackett is working, but no sites have been added to it yet.\r\n\r\nClick \"Open Jackett\", then \"+ Add indexer\" on its page and add the sites you want. Then click Test again.";
						break;
					case "badkey":
						why = "Jackett did not accept the API key (" + JackettAggregateEngine.LastError + ").\r\n\r\nClick \"Open Jackett\" and copy the API Key from the top right of its page again.";
						break;
					case "nokey":
						why = "Enter the API key first. Click \"Open Jackett\" and copy the API Key from the top right of its page.";
						break;
					default:
						why = "Could not reach Jackett at " + _jackettUrl.Text + " (" + JackettAggregateEngine.LastError + ").\r\n\r\nNot installed yet? Click \"Download Jackett\". Otherwise check Jackett is running: click \"Open Jackett\" - its page should open.";
						break;
					}
					MessageBox.Show(why, "Jackett", MessageBoxButtons.OK, JackettAggregateEngine.LastStatus == "ok" ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show("Jackett test failed: " + ex.Message, "Jackett");
			}
			finally
			{
				Cursor = Cursors.Default;
			}
		}
	}
}
