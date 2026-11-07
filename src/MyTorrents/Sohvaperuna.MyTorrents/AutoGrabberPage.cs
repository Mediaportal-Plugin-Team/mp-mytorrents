using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Sohvaperuna.MyTorrents
{
	/// <summary>2026: "Auto grabber" tab - one set of defaults used for every show.</summary>
	public class AutoGrabberPage : TabPage
	{
		private readonly ComboBox _quality = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 230 };
		private readonly NumericUpDown _size = new NumericUpDown { Minimum = 0, Maximum = 100000, Increment = 10, Width = 70 };
		private readonly ComboBox _codec = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 230 };
		private readonly ComboBox _type = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 230 };
		private readonly ComboBox _ask = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 230 };
		private readonly NumericUpDown _seeds = new NumericUpDown { Minimum = 0, Maximum = 1000, Width = 70 };
		private readonly CheckBox _skipPlaceholders = new CheckBox { AutoSize = true };
		private readonly NumericUpDown _minSize = new NumericUpDown { Minimum = 0, Maximum = 5000, Increment = 10, Width = 60 };
		private readonly TextBox _favourites = new TextBox { Width = 380 };
		private readonly CheckBox _packs = new CheckBox { AutoSize = true };                                   // 2026 (1.3.41)
		private readonly ComboBox _packPercent = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 230 };
		private readonly NumericUpDown _packGB = new NumericUpDown { Minimum = 0, Maximum = 500, Increment = 1, Width = 60 };
		private readonly TextBox _packFavourites = new TextBox { Width = 380 };
		private readonly ComboBox _tvSound = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 230 }; // 2026 (1.3.45)
		private readonly ComboBox _atOnce = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 45 };
		private readonly ComboBox _cantFind = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 230 };
		private readonly ComboBox _stuck = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 230 };
		private readonly ComboBox _fallback = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 230 };  // 2026 (1.3.42)
		private readonly CheckBox _relax = new CheckBox { AutoSize = true };
		private readonly ComboBox _relaxAfter = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 70 };
		private readonly ComboBox _relaxStep = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 70 };
		private readonly ComboBox _relaxLowest = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 45 };
		private readonly ComboBox _favMode = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 230 };
		private readonly ComboBox _nightScan = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 230 }; // 2026 (1.3.47)
		private readonly ComboBox _maxDl = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120 }; // 2026 (1.3.50)

		public static readonly string[] TypeChoices = { "New episodes", "Missing episodes" };

		public AutoGrabberPage()
		{
			Text = "TV shows"; // 2026 (1.3.45): was "Auto grabber"
			AutoScroll = true;
			Padding = new Padding(3);
			UseVisualStyleBackColor = true;
			Font = new Font("Microsoft Sans Serif", 8.25f);

			// 2026 (1.3.41): Automatic or Manual - at the top, one under the other
			// 2026 (1.3.45): Automatic / Manual moved to the top of the My shows tab (one line)

			// 2026 (1.3.46): the tab keeps the everyday settings; the rest are behind "More settings..."
			GroupBox every = new GroupBox { Text = "Used for every show", Location = new Point(8, 8), Size = new Size(548, 216) };
			Controls.Add(every);
			Row(every, "Quality", 24, _quality);
			foreach (string q in SeriesItem.QualityChoices) _quality.Items.Add(q);
			_quality.SelectedItem = SeriesItem.QualityChoices.Contains(SeriesItem.Quality) ? SeriesItem.Quality : "720p";

			Row(every, "Biggest episode", 56, _size);
			every.Controls.Add(new Label { Text = "MB", Location = new Point(_size.Right + 6, 59), AutoSize = true });
			_size.Value = Math.Min(_size.Maximum, SeriesItem.MaxSizeMB);
			Hint(every, "0 = no limit.  720p x265: 300-500 MB,  x264: 700-1500 MB", 80);

			Row(every, "Video type", 106, _codec);
			foreach (string c in SeriesItem.CodecChoices) _codec.Items.Add(SeriesItem.CodecText(c));
			_codec.SelectedIndex = Math.Max(0, Array.IndexOf(SeriesItem.CodecChoices, SeriesItem.Codec));
			Hint(every, "Prefer = that type when there is one, else the best other copy", 130);

			Row(every, "Sound", 156, _tvSound);
			foreach (string c in Sound.Choices) _tvSound.Items.Add(Sound.Text(c));
			_tvSound.SelectedIndex = Math.Max(0, Array.IndexOf(Sound.Choices, Sound.Tv));

			Row(every, "Minimum seeders", 186, _seeds);
			_seeds.Value = Math.Min(_seeds.Maximum, SeriesItem.MinSeeders);
			every.Controls.Add(new Label { Text = "fewer are skipped", Location = new Point(_seeds.Right + 8, 189), AutoSize = true, ForeColor = Color.DimGray });

			// 2026: favourite release groups / sites
			GroupBox fav = new GroupBox { Text = "Favourite release groups and sites", Location = new Point(8, 230), Size = new Size(548, 90) };
			Controls.Add(fav);
			Row(fav, "Favourites", 22, _favourites);
			_favourites.Text = string.Join(", ", SeriesItem.Favourites);
			Hint(fav, "Part of the copy's or site's name, with commas - e.g. MeGusta, PSA", 45);
			Row(fav, "Use them", 62, _favMode);
			foreach (string m in SeriesItem.FavouritesModeChoices) _favMode.Items.Add(SeriesItem.FavouritesModeText(m));
			_favMode.SelectedIndex = Math.Max(0, Array.IndexOf(SeriesItem.FavouritesModeChoices, SeriesItem.FavouritesMode));

			Button more = new Button { Text = "More settings...", AutoSize = true, Location = new Point(8, 330) };
			more.Click += (s, e) => _more.ShowDialog(FindForm());
			Controls.Add(more);
			Controls.Add(new Label
			{
				Text = "When nothing good turns up, whole seasons, smallest episode, shows searching at once, and the rest.",
				Location = new Point(more.Left + more.PreferredSize.Width + 10, 335), AutoSize = true, ForeColor = Color.DimGray
			});
			Controls.Add(new Label
			{
				Text = "Each show searches all your sites and downloads the one best copy that fits these settings.",
				Location = new Point(10, 368), Size = new Size(548, 18), ForeColor = Color.DimGray
			});

			// ---------------------------------------------------------------- the "More settings" window
			_more.Text = "TV shows - more settings";
			_more.StartPosition = FormStartPosition.CenterParent;
			_more.FormBorderStyle = FormBorderStyle.FixedDialog;
			_more.MinimizeBox = _more.MaximizeBox = false;
			_more.ShowInTaskbar = false;
			_more.Font = Font;
			_more.ClientSize = new Size(566, 620);

			// 2026 (1.3.42): when nothing good turns up for a long time
			GroupBox late = new GroupBox { Text = "When nothing good turns up", Location = new Point(8, 8), Size = new Size(550, 130) };
			_more.Controls.Add(late);
			Row(late, "1080p if no good 720p", 20, _fallback);
			foreach (int p in Relax.FallbackChoices) _fallback.Items.Add(Relax.FallbackText(p));
			_fallback.SelectedIndex = Math.Max(0, Array.IndexOf(Relax.FallbackChoices, Relax.FallbackPercent));
			late.Controls.Add(new Label { Text = "(only with 720p chosen)", Location = new Point(_fallback.Right + 6, 23), AutoSize = true, ForeColor = Color.DimGray });
			_relax.Text = "Fewer seeders after";
			_relax.Location = new Point(12, 48);
			_relax.Checked = Relax.SeedsOn;
			late.Controls.Add(_relax);
			foreach (int h in Relax.AfterChoices) _relaxAfter.Items.Add(h + " hrs");
			_relaxAfter.SelectedIndex = Math.Max(0, Array.IndexOf(Relax.AfterChoices, Relax.AfterHours) < 0 ? 2 : Array.IndexOf(Relax.AfterChoices, Relax.AfterHours));
			_relaxAfter.Location = new Point(150, 46);
			late.Controls.Add(_relaxAfter);
			late.Controls.Add(new Label { Text = "then 1 less every", Location = new Point(226, 49), AutoSize = true });
			foreach (int h in Relax.StepChoices) _relaxStep.Items.Add(h + " hrs");
			_relaxStep.SelectedIndex = Array.IndexOf(Relax.StepChoices, Relax.StepHours) < 0 ? 1 : Array.IndexOf(Relax.StepChoices, Relax.StepHours);
			_relaxStep.Location = new Point(320, 46);
			late.Controls.Add(_relaxStep);
			late.Controls.Add(new Label { Text = "never under", Location = new Point(396, 49), AutoSize = true });
			foreach (int n in Relax.LowestChoices) _relaxLowest.Items.Add(n.ToString());
			_relaxLowest.SelectedIndex = Array.IndexOf(Relax.LowestChoices, Relax.LowestSeeders) < 0 ? 0 : Array.IndexOf(Relax.LowestChoices, Relax.LowestSeeders);
			_relaxLowest.Location = new Point(462, 46);
			late.Controls.Add(_relaxLowest);
			_relax.CheckedChanged += (s, e) => _relaxAfter.Enabled = _relaxStep.Enabled = _relaxLowest.Enabled = _relax.Checked;
			_relaxAfter.Enabled = _relaxStep.Enabled = _relaxLowest.Enabled = _relax.Checked;
			Row(late, "Stuck downloads", 74, _stuck);
			foreach (int h in Grabbed.StuckChoices) _stuck.Items.Add(Grabbed.StuckText(h));
			_stuck.SelectedIndex = Math.Max(0, Array.IndexOf(Grabbed.StuckChoices, Grabbed.StuckHours));
			late.Controls.Add(new Label { Text = "-> delete it and try another copy", Location = new Point(_stuck.Right + 6, 77), AutoSize = true, ForeColor = Color.DimGray });
			Row(late, "Can't find list after", 100, _cantFind);
			foreach (int d in CantFind.DayChoices) _cantFind.Items.Add(d + " days of looking");
			_cantFind.SelectedIndex = Math.Max(0, Array.IndexOf(CantFind.DayChoices, CantFind.Days));
			late.Controls.Add(new Label { Text = "(then looked for once a day)", Location = new Point(_cantFind.Right + 6, 103), AutoSize = true, ForeColor = Color.DimGray });

			GroupBox add = new GroupBox { Text = "Shows and episodes", Location = new Point(8, 144), Size = new Size(550, 254) };
			_more.Controls.Add(add);
			// 2026: smallest episode - anything smaller is a fake or a sample
			Row(add, "Smallest episode", 22, _minSize);
			_minSize.Value = Math.Min(_minSize.Maximum, SeriesItem.MinSizeMB);
			add.Controls.Add(new Label { Text = "MB  (smaller = a fake or a sample;  0 = no minimum)", Location = new Point(_minSize.Right + 6, 25), AutoSize = true, ForeColor = Color.DimGray });
			// 2026 (1.3.42): how many shows search at the same time (shows you ask for now don't wait)
			Row(add, "Shows searching at once", 48, _atOnce);
			foreach (int n in SeriesItem.SearchesAtOnceChoices) _atOnce.Items.Add(n.ToString());
			_atOnce.SelectedIndex = Math.Max(0, Array.IndexOf(SeriesItem.SearchesAtOnceChoices, SeriesItem.SearchesAtOnce));
			add.Controls.Add(new Label { Text = "(fewer = lighter on the PC; ones you ask for now never wait)", Location = new Point(_atOnce.Right + 6, 51), AutoSize = true, ForeColor = Color.DimGray });
			Row(add, "No copy fits?", 74, _ask);
			foreach (int h in TooBig.HourChoices) _ask.Items.Add(TooBig.HoursText(h));
			_ask.SelectedIndex = Math.Max(0, Array.IndexOf(TooBig.HourChoices, TooBig.AskAfterHours));
			Row(add, "New shows look for", 100, _type);
			_type.Items.AddRange(TypeChoices);
			_type.SelectedItem = TypeChoices.Contains(SeriesItem.NewShowType) ? SeriesItem.NewShowType : TypeChoices[0];
			_skipPlaceholders.Text = "Skip placeholder episodes (numbered 99, or no air date yet)";
			_skipPlaceholders.Location = new Point(12, 128);
			_skipPlaceholders.Checked = SeriesItem.SkipPlaceholders;
			add.Controls.Add(_skipPlaceholders);
			// 2026 (1.3.47): one quiet MP-TVSeries scan at night instead of at every start
			Row(add, "MP-TVSeries scan", 152, _nightScan);
			foreach (string m in NightScan.Choices) _nightScan.Items.Add(NightScan.Text(m));
			_nightScan.SelectedIndex = Math.Max(0, Array.IndexOf(NightScan.Choices, NightScan.Mode));
			Hint(add, "Between 2 and 5 am, never while something plays. Catches files you add, rename or delete by hand.", 177);
			// 2026 (1.3.50): the most downloads started by themselves at the same time (TV and movies)
			Row(add, "Downloads at once", 202, _maxDl);
			foreach (int n in Busy.Choices) _maxDl.Items.Add(Busy.Text(n));
			_maxDl.SelectedIndex = Math.Max(0, Array.IndexOf(Busy.Choices, Busy.Max) < 0 ? 1 : Array.IndexOf(Busy.Choices, Busy.Max));
			Hint(add, "TV and movies. More than this still downloading = the Auto grabber and Wanted movies wait. Ones you ask for never wait.", 227);

			// 2026 (1.3.41): whole seasons first, then single episodes
			GroupBox packs = new GroupBox { Text = "Whole seasons", Location = new Point(8, 404), Size = new Size(550, 166) };
			_more.Controls.Add(packs);
			_packs.Text = "Look for the whole season first when a lot of it is missing";
			_packs.Location = new Point(12, 20);
			_packs.Checked = SeriesItem.SeasonPacks;
			packs.Controls.Add(_packs);
			Row(packs, "Whole season when", 44, _packPercent);
			foreach (int p in SeriesItem.SeasonPackPercentChoices) _packPercent.Items.Add(SeriesItem.SeasonPackPercentText(p));
			_packPercent.SelectedIndex = Math.Max(0, Array.IndexOf(SeriesItem.SeasonPackPercentChoices, SeriesItem.SeasonPackPercent));
			Hint(packs, "e.g. 50%: 5 of a 10-episode season, 3 of 6, 11 of 22 (always at least 2)", 68);
			Hint(packs, "Finished seasons only. None good? Single episodes straight away", 88);
			Row(packs, "Biggest season", 114, _packGB);
			_packGB.Value = (decimal)Math.Min((double)_packGB.Maximum, SeriesItem.MaxSeasonGB);
			packs.Controls.Add(new Label { Text = "GB  (0 = biggest episode x the number of episodes)", Location = new Point(_packGB.Right + 6, 117), AutoSize = true, ForeColor = Color.DimGray });
			Row(packs, "Favourite season groups", 138, _packFavourites);
			_packFavourites.Text = string.Join(", ", SeriesItem.SeasonFavourites);

			Label keep = new Label { Text = "These are kept when you press Ok on the main window.", Location = new Point(10, 586), AutoSize = true, ForeColor = Color.DimGray };
			Button done = new Button { Text = "Done", DialogResult = DialogResult.OK, Location = new Point(478, 580), Width = 80 };
			_more.Controls.Add(keep);
			_more.Controls.Add(done);
			_more.AcceptButton = done;
			_more.CancelButton = done;
		}

		private readonly Form _more = new Form();

		protected override void Dispose(bool disposing)
		{
			if (disposing) _more.Dispose();
			base.Dispose(disposing);
		}

		private static void Row(Control parent, string text, int y, Control box)
		{
			parent.Controls.Add(new Label { Text = text, Location = new Point(12, y + 3), AutoSize = true });
			box.Location = new Point(150, y);
			parent.Controls.Add(box);
		}

		private static void Hint(Control parent, string text, int y)
		{
			parent.Controls.Add(new Label { Text = text, Location = new Point(150, y), AutoSize = true, ForeColor = Color.DimGray });
		}

		public void Save()
		{
			Configuration.SetExtra("AutoGrabber.Quality", _quality.SelectedItem as string ?? "720p");
			Configuration.SetExtra("AutoGrabber.MaxSizeMB", ((long)_size.Value).ToString());
			Configuration.SetExtra("AutoGrabber.MinSeeders", ((int)_seeds.Value).ToString());
			Configuration.SetExtra("AutoGrabber.Codec", SeriesItem.CodecChoices[Math.Max(0, _codec.SelectedIndex)]);
			Configuration.SetExtra("AutoGrabber.Type", _type.SelectedItem as string ?? TypeChoices[0]);
			Configuration.SetExtra("AutoGrabber.SkipPlaceholders", _skipPlaceholders.Checked ? "true" : "false");
			Configuration.SetExtra("AutoGrabber.MinSizeMB", ((long)_minSize.Value).ToString());
			Configuration.SetExtra("AutoGrabber.Favourites", string.Join(", ", (_favourites.Text ?? "").Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries).Select(f => f.Trim()).Where(f => f.Length > 0)));
			Configuration.SetExtra("AutoGrabber.FavouritesMode", SeriesItem.FavouritesModeChoices[Math.Max(0, _favMode.SelectedIndex)]);
			Configuration.SetExtra("AutoGrabber.SeasonPacks", _packs.Checked ? "true" : "false");
			Configuration.SetExtra("AutoGrabber.SeasonPackPercent", SeriesItem.SeasonPackPercentChoices[Math.Max(0, _packPercent.SelectedIndex)].ToString());
			Configuration.SetExtra("AutoGrabber.MaxSeasonGB", ((int)_packGB.Value).ToString(System.Globalization.CultureInfo.InvariantCulture));
			Configuration.SetExtra("AutoGrabber.SeasonFavourites", string.Join(", ", (_packFavourites.Text ?? "").Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries).Select(f => f.Trim()).Where(f => f.Length > 0)));
			Configuration.SetExtra("AutoGrabber.Sound", Sound.Choices[Math.Max(0, _tvSound.SelectedIndex)]);
			Configuration.SetExtra("AutoGrabber.SearchesAtOnce", SeriesItem.SearchesAtOnceChoices[Math.Max(0, _atOnce.SelectedIndex)].ToString());
			Configuration.SetExtra("AutoGrabber.CantFindDays", CantFind.DayChoices[Math.Max(0, _cantFind.SelectedIndex)].ToString());
			Configuration.SetExtra("AutoGrabber.StuckHours", Grabbed.StuckChoices[Math.Max(0, _stuck.SelectedIndex)].ToString());
			Configuration.SetExtra("AutoGrabber.Fallback1080Percent", Relax.FallbackChoices[Math.Max(0, _fallback.SelectedIndex)].ToString());
			Configuration.SetExtra("AutoGrabber.RelaxSeeds", _relax.Checked ? "true" : "false");
			Configuration.SetExtra("AutoGrabber.RelaxAfterHours", Relax.AfterChoices[Math.Max(0, _relaxAfter.SelectedIndex)].ToString());
			Configuration.SetExtra("AutoGrabber.RelaxStepHours", Relax.StepChoices[Math.Max(0, _relaxStep.SelectedIndex)].ToString());
			Configuration.SetExtra("AutoGrabber.LowestSeeders", Relax.LowestChoices[Math.Max(0, _relaxLowest.SelectedIndex)].ToString());
			Configuration.SetExtra("AutoGrabber.MaxDownloading", Busy.Choices[Math.Max(0, _maxDl.SelectedIndex)].ToString());
			Configuration.SetExtra("TV.NightScan", NightScan.Choices[Math.Max(0, _nightScan.SelectedIndex)]);
			Configuration.SetExtra("AutoGrabber.AskAfterHours", TooBig.HourChoices[Math.Max(0, _ask.SelectedIndex)].ToString());
		}
	}
}
