using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace Sohvaperuna.MyTorrents
{
	/// <summary>2026: "When finished" tab - copy finished downloads into the library, renamed and sorted.</summary>
	public class FinishedPage : TabPage
	{
		private readonly ComboBox _nearly = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 170 }; // 2026 (1.3.53)
		private readonly CheckBox _copy = new CheckBox { Text = "Copy finished downloads into my library  (the torrent keeps seeding)", AutoSize = true };
		private readonly ComboBox _tv = new ComboBox { DropDownStyle = ComboBoxStyle.DropDown };
		private readonly ComboBox _movies = new ComboBox { DropDownStyle = ComboBoxStyle.DropDown };
		private readonly CheckBox _sort = new CheckBox { Text = "Rename and sort into folders", AutoSize = true };
		private readonly TextBox _drop = new TextBox(); // 2026 (1.3.50)
		// 2026 (1.3.39): naming choices
		private readonly ComboBox _style = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
		private readonly TextBox _custom = new TextBox();
		private readonly ComboBox _seasonStyle = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
		private readonly ComboBox _showStyle = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList }; // 2026 (1.3.49)
		private readonly Label _example = new Label { AutoSize = false, ForeColor = Color.DarkBlue };
		// 2026 (1.3.49): movie naming ("need movies rename settings like tv series")
		private readonly ComboBox _movieStyle = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
		private readonly TextBox _movieCustom = new TextBox();
		private readonly ComboBox _movieFolderStyle = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
		private readonly Button _tidy = new Button { Text = "Rename my movies to match...", AutoSize = true };
		// 2026 (1.3.38)
		private readonly CheckBox _cleanup = new CheckBox { Text = "Clean up after seeding: remove a copied torrent and its leftover download once it has stopped seeding\r\n(files inside your TV or movie library are never deleted)", AutoSize = false, Size = new Size(548, 32) };

		public FinishedPage()
		{
			Text = "When finished";
			AutoScroll = true; // 1.3.50: room for the drop folder on small windows
			Padding = new Padding(3);
			UseVisualStyleBackColor = true;
			Font = new Font("Microsoft Sans Serif", 8.25f);

			_copy.Location = new Point(10, 12);
			Controls.Add(_copy);

			Controls.Add(new Label { Text = "TV episodes go to", Location = new Point(10, 46), AutoSize = true });
			_tv.SetBounds(120, 43, 340, 21);
			Controls.Add(_tv);
			Controls.Add(Browse(_tv, 466, 42));
			Controls.Add(new Label { Text = "Movies go to", Location = new Point(10, 78), AutoSize = true });
			_movies.SetBounds(120, 75, 340, 21);
			Controls.Add(_movies);
			Controls.Add(Browse(_movies, 466, 74));
			Controls.Add(new Label { Text = "The lists show the folders MP-TVSeries and Moving Pictures already scan.", Location = new Point(120, 100), AutoSize = true, ForeColor = Color.DimGray });
			// 2026 (1.3.50): the drop folder - put a movie or episode here and it's filed like a download
			Controls.Add(new Label { Text = "Drop folder", Location = new Point(10, 461), AutoSize = true });
			_drop.SetBounds(120, 458, 340, 21);
			Controls.Add(_drop);
			Button dropBrowse = new Button { Text = "...", Location = new Point(466, 457), Size = new Size(30, 23) };
			dropBrowse.Click += (s, e) =>
			{
				using (FolderBrowserDialog d = new FolderBrowserDialog { SelectedPath = _drop.Text, ShowNewFolderButton = true })
				{
					if (d.ShowDialog(this) == DialogResult.OK) _drop.Text = d.SelectedPath;
				}
			};
			Controls.Add(dropBrowse);
			Controls.Add(new Label { Text = "Put a movie or episode here (a file, or its folder) and it's named and filed like a download - moved out of here.\r\nEmpty = off. Not inside your TV or movie folder. Anything it can't work out stays, and the Check-up says why.", Location = new Point(120, 483), Size = new Size(470, 30), ForeColor = Color.DimGray });
			// 2026 (1.3.53): stuck at 99.9% with no seeds - it usually plays fine, so check it and use it
			Controls.Add(new Label { Text = "Nearly finished", Location = new Point(10, 523), AutoSize = true });
			_nearly.Location = new Point(120, 520);
			foreach (int h in PostProcessor.NearlyDoneChoices) _nearly.Items.Add(PostProcessor.NearlyDoneText(h));
			_nearly.SelectedIndex = Math.Max(0, Array.IndexOf(PostProcessor.NearlyDoneChoices, PostProcessor.NearlyDoneHours));
			Controls.Add(_nearly);
			Controls.Add(new Label { Text = "-> check the video is whole and copy it anyway.\r\nOr right-click it in Downloads > Use it now (95%+).", Location = new Point(_nearly.Right + 6, 519), Size = new Size(300, 30), ForeColor = Color.DimGray });

			_sort.Location = new Point(10, 126);
			Controls.Add(_sort);
			// 2026 (1.3.39): how episodes and new season folders are named, with a live example
			Controls.Add(new Label { Text = "Episode names", Location = new Point(28, 151), AutoSize = true });
			_style.SetBounds(130, 148, 300, 21);
			foreach (string[] s in Naming.Styles) _style.Items.Add(s[0]);
			_style.Items.Add("My own pattern...");
			Controls.Add(_style);
			_custom.SetBounds(436, 148, 152, 21);
			Controls.Add(_custom);
			Controls.Add(new Label { Text = "Season folders", Location = new Point(28, 178), AutoSize = true });
			_seasonStyle.SetBounds(130, 175, 300, 21);
			foreach (string[] s in Naming.SeasonStyles) _seasonStyle.Items.Add(s[0]);
			Controls.Add(_seasonStyle);
			Controls.Add(new Label { Text = "New show folders", Location = new Point(28, 205), AutoSize = true });
			_showStyle.SetBounds(130, 202, 300, 21);
			foreach (string[] s in Naming.ShowFolderStyles) _showStyle.Items.Add(s[0]);
			Controls.Add(_showStyle);
			Controls.Add(new Label { Text = "(shows you already have keep their folder)", Location = new Point(436, 205), AutoSize = true, ForeColor = Color.DimGray });
			Controls.Add(new Label { Text = "Movie names", Location = new Point(28, 232), AutoSize = true });
			_movieStyle.SetBounds(130, 229, 300, 21);
			foreach (string[] s in Naming.MovieStyles) _movieStyle.Items.Add(s[0]);
			_movieStyle.Items.Add("My own pattern...");
			Controls.Add(_movieStyle);
			_movieCustom.SetBounds(436, 229, 152, 21);
			Controls.Add(_movieCustom);
			Controls.Add(new Label { Text = "Movie folders", Location = new Point(28, 259), AutoSize = true });
			_movieFolderStyle.SetBounds(130, 256, 300, 21);
			foreach (string[] s in Naming.MovieFolderStyles) _movieFolderStyle.Items.Add(s[0]);
			Controls.Add(_movieFolderStyle);
			_tidy.Location = new Point(436, 255);
			_tidy.Click += (s, e) => { Save(); LibraryDialogs.RenameWindow(this); };
			Controls.Add(_tidy);
			_example.SetBounds(28, 283, 560, 30);
			Controls.Add(_example);
			ToolTip tip = new ToolTip { AutoPopDelay = 30000 };
			tip.SetToolTip(_custom, "Your own pattern from these pieces: " + Naming.Pieces + "\r\ne.g. {show} S{season:00}E{episode:00} {title} [{quality}][{airdate}]\r\nPieces with nothing to show are left out.");
			tip.SetToolTip(_style, "How finished episodes are named. A show or season folder you already have is always used.");
			_style.SelectedIndexChanged += (s, e) => { UpdateNaming(); };
			_custom.TextChanged += (s, e) => UpdateNaming();
			_seasonStyle.SelectedIndexChanged += (s, e) => UpdateNaming();
			_showStyle.SelectedIndexChanged += (s, e) => UpdateNaming();
			_movieStyle.SelectedIndexChanged += (s, e) => UpdateNaming();
			_movieCustom.TextChanged += (s, e) => UpdateNaming();
			_movieFolderStyle.SelectedIndexChanged += (s, e) => UpdateNaming();
			tip.SetToolTip(_movieCustom, "Your own pattern from these pieces: " + Naming.MoviePieces + "\r\ne.g. {title} ({year}) [{quality}]\r\nPieces with nothing to show are left out.");
			tip.SetToolTip(_tidy, "Shows the movies in your library whose names don't match these choices, to rename the ones you tick.");

			Controls.Add(new Label
			{
				Text = "Copied torrents are tagged \"" + PostProcessor.CopiedTag + "\" in qBittorrent / BiglyBT, so nothing is copied twice. " +
					"Downloads that had already finished when this was first turned on are left alone.\r\n\r\n" +
					"Moving Pictures is asked to look for the new movie straight away. " + TvScanText(),
				Location = new Point(10, 319), Size = new Size(548, 92)
			});

			_cleanup.Location = new Point(10, 413);
			Controls.Add(_cleanup);

			_copy.CheckedChanged += (s, e) => UpdateEnabled();
			Load();
		}

		private static string TvScanText()
		{
			try
			{
				string t = LibraryFolders.TVSeriesScanning();
				return t.Length > 0 ? t : "MP-TVSeries finds new episodes when it scans its folders.";
			}
			catch
			{
				return "";
			}
		}


		// 2026 (1.3.39)
		private void LoadNaming()
		{
			string p = Naming.Pattern;
			int i = Array.FindIndex(Naming.Styles, s => s[1] == p);
			if (i >= 0) { _style.SelectedIndex = i; _custom.Text = ""; }
			else { _style.SelectedIndex = Naming.Styles.Length; _custom.Text = p; }
			string sp = Naming.SeasonPattern;
			int j = Array.FindIndex(Naming.SeasonStyles, s => s[1] == sp);
			_seasonStyle.SelectedIndex = j >= 0 ? j : 0;
			int sh = Array.FindIndex(Naming.ShowFolderStyles, s => s[1] == Naming.ShowFolderPattern);
			_showStyle.SelectedIndex = sh >= 0 ? sh : 0;
			string mp = Naming.MoviePattern;
			int k = Array.FindIndex(Naming.MovieStyles, s => s[1] == mp);
			if (k >= 0) { _movieStyle.SelectedIndex = k; _movieCustom.Text = ""; }
			else { _movieStyle.SelectedIndex = Naming.MovieStyles.Length; _movieCustom.Text = mp; }
			int m = Array.FindIndex(Naming.MovieFolderStyles, s => s[1] == Naming.MovieFolderPattern);
			_movieFolderStyle.SelectedIndex = m >= 0 ? m : 0;
			UpdateNaming();
		}

		private string CurrentPattern()
		{
			if (_style.SelectedIndex < 0) return "";
			if (_style.SelectedIndex < Naming.Styles.Length) return Naming.Styles[_style.SelectedIndex][1];
			return (_custom.Text ?? "").Trim();
		}

		private string CurrentMoviePattern()
		{
			if (_movieStyle.SelectedIndex < 0) return Naming.MovieStyles[0][1];
			if (_movieStyle.SelectedIndex < Naming.MovieStyles.Length) return Naming.MovieStyles[_movieStyle.SelectedIndex][1];
			string c = (_movieCustom.Text ?? "").Trim();
			return c.Length > 0 ? c : Naming.MovieStyles[0][1];
		}

		private string CurrentMovieFolderPattern() => Naming.MovieFolderStyles[Math.Max(0, _movieFolderStyle.SelectedIndex)][1];

		private void UpdateNaming()
		{
			bool custom = _style.SelectedIndex == Naming.Styles.Length;
			_custom.Enabled = custom && _style.Enabled;
			if (!custom && _style.SelectedIndex >= 0) _custom.Text = Naming.Styles[_style.SelectedIndex][1];
			string season = _seasonStyle.SelectedIndex > 0 ? Naming.SeasonStyles[_seasonStyle.SelectedIndex][1] : "";
			bool movieCustom = _movieStyle.SelectedIndex == Naming.MovieStyles.Length;
			_movieCustom.Enabled = movieCustom && _movieStyle.Enabled;
			if (!movieCustom && _movieStyle.SelectedIndex >= 0) _movieCustom.Text = Naming.MovieStyles[_movieStyle.SelectedIndex][1];
			_example.Text = "Example:  " + Naming.Example(CurrentPattern(), season, Naming.ShowFolderStyles[Math.Max(0, _showStyle.SelectedIndex)][1]) + "\r\nMovies:  " + Naming.MovieExample(CurrentMoviePattern(), CurrentMovieFolderPattern());
		}
		private Button Browse(ComboBox target, int x, int y)
		{
			Button b = new Button { Text = "...", Location = new Point(x, y), Size = new Size(30, 23) };
			b.Click += (s, e) =>
			{
				using (FolderBrowserDialog d = new FolderBrowserDialog { SelectedPath = target.Text, ShowNewFolderButton = true })
				{
					if (d.ShowDialog(this) == DialogResult.OK) target.Text = d.SelectedPath;
				}
			};
			return b;
		}

		private void Load()
		{
			try
			{
				Fill(_tv, LibraryFolders.TV(), PostProcessor.TVFolder);
				Fill(_movies, LibraryFolders.Movies(), PostProcessor.MovieFolder);
				_copy.Checked = PostProcessor.Enabled;
				_sort.Checked = PostProcessor.Sort;
				_cleanup.Checked = PostProcessor.CleanUp;
				_drop.Text = DropFolder.Folder;
				LoadNaming();
			}
			catch (Exception ex)
			{
				Log.Instance().Print("When finished tab: " + ex.Message);
			}
			UpdateEnabled();
		}

		private static void Fill(ComboBox box, List<string> items, string current)
		{
			box.Items.Clear();
			foreach (string i in items) box.Items.Add(i);
			box.Text = current ?? "";
		}

		private void UpdateEnabled()
		{
			foreach (Control c in Controls)
			{
				if (c != _copy) c.Enabled = _copy.Checked;
			}
			UpdateNaming(); // 2026 (1.3.39): the pattern box only for "My own pattern"
		}

		public void Save()
		{
			Configuration.SetExtra("Finished.Copy", _copy.Checked ? "true" : "false");
			Configuration.SetExtra("Finished.TVFolder", _tv.Text.Trim());
			Configuration.SetExtra("Finished.MovieFolder", _movies.Text.Trim());
			Configuration.SetExtra("Finished.Sort", _sort.Checked ? "true" : "false");
			Configuration.SetExtra("Finished.CleanUp", _cleanup.Checked ? "true" : "false");
			Configuration.SetExtra("Finished.DropFolder", (_drop.Text ?? "").Trim());
			Configuration.SetExtra("Finished.NearlyDoneHours", PostProcessor.NearlyDoneChoices[Math.Max(0, _nearly.SelectedIndex)].ToString());
			Configuration.SetExtra("Finished.NamePattern", CurrentPattern());
			Configuration.SetExtra("Finished.ShowFolderPattern", Naming.ShowFolderStyles[Math.Max(0, _showStyle.SelectedIndex)][1]);
			Configuration.SetExtra("Films.NamePattern", CurrentMoviePattern());
			Configuration.SetExtra("Films.FolderPattern", CurrentMovieFolderPattern());
			Configuration.SetExtra("Finished.SeasonPattern", _seasonStyle.SelectedIndex > 0 ? Naming.SeasonStyles[_seasonStyle.SelectedIndex][1] : "");
		}
	}
}
