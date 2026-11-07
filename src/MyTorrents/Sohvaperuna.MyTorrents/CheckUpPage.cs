using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace Sohvaperuna.MyTorrents
{
	/// <summary>2026 (1.3.49): "Check-up" tab - what the daily check-up found, with a button to put each thing right.</summary>
	public class CheckUpPage : TabPage
	{
		private readonly Label _summary = new Label { AutoSize = false, Font = new Font("Microsoft Sans Serif", 9f, FontStyle.Bold) };
		private readonly Button _now = new Button { Text = "Check now", AutoSize = true };
		private readonly ListView _list = new ListView { View = View.Details, FullRowSelect = true, MultiSelect = false };
		private readonly Button _fix = new Button { Text = "Fix it", AutoSize = true, Enabled = false };
		private readonly Button _open = new Button { Text = "Open folder", AutoSize = true, Enabled = false };
		private readonly Button _ignore = new Button { Text = "Ignore", AutoSize = true, Enabled = false };
		private readonly Button _all = new Button { Text = "Delete all the samples / adverts", AutoSize = true, Visible = false }; // 1.3.52
		private readonly Label _note = new Label { AutoSize = true, ForeColor = Color.DimGray };
		private readonly Label _help = new Label
		{
			AutoSize = false, ForeColor = Color.DimGray,
			Text = "Once a day MyTorrents checks what it did in the last " + CheckUp.Days + " days: every episode and movie is where it put it, in MP-TVSeries / Moving Pictures, " +
				"not there twice, not a poor copy - and that the NAS isn't filling up and the search sites answer. \"Fix it\" puts a thing right; \"Ignore\" stops it being mentioned."
		};

		public CheckUpPage()
		{
			Text = "Check-up";
			Padding = new Padding(3);
			UseVisualStyleBackColor = true;
			Font = new Font("Microsoft Sans Serif", 8.25f);
			_list.Columns.Add("", 60);
			_list.Columns.Add("What", 90);
			_list.Columns.Add("", 600);
			_list.ShowItemToolTips = true;
			Controls.AddRange(new Control[] { _summary, _now, _help, _list, _fix, _open, _ignore, _all, _note });
			_all.Click += (s, e) =>
			{
				List<CheckUp.Finding> left = _list.Items.Cast<ListViewItem>().Select(i => i.Tag as CheckUp.Finding).Where(x => x != null && x.Action == "leftover").ToList();
				if (left.Count == 0 || MessageBox.Show(this, "Delete these " + left.Count + " sample / advert videos?\r\n\r\nOnly small files with names like sample, RARBG, YTS... - never a film or an episode.", "Check-up", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
				int bad = left.Count(x => CheckUp.DeleteLeftover(x).Length > 0);
				_note.Text = (left.Count - bad) + " deleted" + (bad > 0 ? ", " + bad + " not" : "") + ".";
				Fill();
			};
			_now.Click += (s, e) => RunNow();
			_list.SelectedIndexChanged += (s, e) => UpdateButtons();
			LibraryDialogs.AddOpen(_list, i => (i.Tag as CheckUp.Finding)?.File); // 1.3.50: right-click / double-click opens the folder
			LookUp.AddTo(_list, i => { CheckUp.Finding f = i.Tag as CheckUp.Finding; return f == null || f.File.Length == 0 ? null : f.Title.Length > 0 ? LookUp.What.Movie(f.Title, f.Year) : LookUp.What.FromName(System.IO.Path.GetFileName(f.File)); }); // 1.3.52
			_fix.Click += (s, e) => Fix();
			_open.Click += (s, e) => OpenFolder();
			_ignore.Click += (s, e) =>
			{
				CheckUp.Finding f = Selected();
				if (f == null) return;
				CheckUp.Ignore(f);
				Fill();
			};
			Resize += (s, e) => LayOut();
			VisibleChanged += (s, e) => { if (Visible) Fill(); };
			LayOut();
		}

		private void LayOut()
		{
			int w = Math.Max(400, ClientSize.Width - 20);
			_summary.SetBounds(10, 12, w - 110, 20);
			_now.Location = new Point(10 + w - _now.PreferredSize.Width, 8);
			_help.SetBounds(10, 36, w, 32);
			int bottom = ClientSize.Height - 36;
			_list.SetBounds(10, 72, w, Math.Max(100, bottom - 80));
			_list.Columns[2].Width = Math.Max(300, w - 160);
			_fix.Location = new Point(10, bottom);
			_open.Location = new Point(_fix.Right + 6, bottom);
			_ignore.Location = new Point(_open.Right + 6, bottom);
			_all.Location = new Point(_ignore.Right + 12, bottom);
			_note.Location = new Point((_all.Visible ? _all.Right : _ignore.Right) + 12, bottom + 5);
		}

		private void Fill()
		{
			_list.Items.Clear();
			List<CheckUp.Finding> found = CheckUp.Read();
			_summary.Text = CheckUp.Summary() + (CheckUp.LastRun > DateTime.MinValue ? "   -   checked " + CheckUp.LastRun.ToString("ddd d MMM h:mm tt") : "");
			foreach (CheckUp.Finding f in found.OrderBy(x => x.Level == "fix" ? 0 : x.Level == "note" ? 1 : 2))
			{
				string mark = f.Level == "fix" ? "Needs you" : f.Level == "note" ? "Note" : "OK";
				ListViewItem i = new ListViewItem(new[] { mark, f.Area, f.Text }) { Tag = f, ToolTipText = f.File };
				i.ForeColor = f.Level == "fix" ? Color.Firebrick : f.Level == "note" ? Color.DarkGoldenrod : Color.DarkGreen;
				_list.Items.Add(i);
			}
			UpdateButtons();
		}

		private CheckUp.Finding Selected() => _list.SelectedItems.Count > 0 ? _list.SelectedItems[0].Tag as CheckUp.Finding : null;

		private void UpdateButtons()
		{
			CheckUp.Finding f = Selected();
			_fix.Enabled = f != null && (f.Action == "import" || f.Action == "dupe" || f.Action == "better" || f.Action == "rename" || f.Action == "leftover");
			_fix.Text = f == null ? "Fix it" : f.Action == "import" ? "Add to MP-TVSeries" : f.Action == "dupe" ? "Delete this copy" : f.Action == "better" ? "Look for a better copy" : f.Action == "rename" ? "Rename..." : f.Action == "leftover" ? "Delete it" : "Fix it";
			_all.Visible = _list.Items.Cast<ListViewItem>().Count(i => (i.Tag as CheckUp.Finding)?.Action == "leftover") > 1;
			_open.Enabled = f != null && f.File.Length > 0;
			_ignore.Enabled = f != null && f.Key.Length > 0 && f.Level != "ok";
			LayOut();
		}

		private void RunNow()
		{
			_now.Enabled = false;
			_note.Text = "Checking - on a NAS this takes a minute...";
			BackgroundWorker w = new BackgroundWorker();
			w.DoWork += (s, e) => CheckUp.Run();
			w.RunWorkerCompleted += (s, e) =>
			{
				_now.Enabled = true;
				_note.Text = e.Error != null ? "Could not check: " + e.Error.Message : "";
				Fill();
			};
			w.RunWorkerAsync();
		}

		private void Fix()
		{
			CheckUp.Finding f = Selected();
			if (f == null) return;
			switch (f.Action)
			{
			case "import":
				CheckUp.ImportAgain(f);
				_note.Text = "MP-TVSeries adds it " + (MyTorrents.InMediaPortal ? "now." : "when MediaPortal next runs.");
				break;
			case "dupe":
				if (MessageBox.Show(this, "Delete this copy?\r\n\r\n" + f.File + "\r\n\r\nThe other copy stays.", "Check-up", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
				string r = CheckUp.DeleteExtra(f);
				_note.Text = r.Length == 0 ? "Deleted." : "Could not delete it: " + r;
				break;
			case "better":
				PoorCopies.Look(f.Title, f.Year, f.File);
				CheckUp.Done(f);
				_note.Text = f.Title + " is on Wanted movies to replace.";
				break;
			case "rename":
				LibraryDialogs.RenameWindow(this);
				break;
			case "leftover":
				string r2 = CheckUp.DeleteLeftover(f);
				_note.Text = r2.Length == 0 ? "Deleted." : r2;
				break;
			}
			Fill();
		}

		private void OpenFolder()
		{
			CheckUp.Finding f = Selected();
			if (f == null || f.File.Length == 0) return;
			try
			{
				if (File.Exists(f.File)) Process.Start("explorer.exe", "/select,\"" + f.File + "\"");
				else
				{
					string dir = Directory.Exists(f.File) ? f.File : Path.GetDirectoryName(f.File);
					if (Directory.Exists(dir)) Process.Start("explorer.exe", "\"" + dir + "\"");
					else _note.Text = "That folder isn't there any more.";
				}
			}
			catch (Exception ex)
			{
				_note.Text = ex.Message;
			}
		}
	}
}
