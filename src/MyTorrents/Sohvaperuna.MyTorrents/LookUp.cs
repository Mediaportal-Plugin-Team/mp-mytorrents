using System;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace Sohvaperuna.MyTorrents
{
	/// <summary>2026 (1.3.52): right-click "Look it up on TheTVDB / TMDb" - opens the page in your web browser.</summary>
	public static class LookUp
	{
		public static void Tvdb(string name, string id = null)
		{
			long n;
			Open(!string.IsNullOrEmpty(id) && long.TryParse(id, out n) && n > 0
				? "https://thetvdb.com/dereferrer/series/" + n
				: "https://thetvdb.com/search?query=" + Uri.EscapeDataString(Clean(name)));
		}

		public static void TmdbTv(string name) => Open("https://www.themoviedb.org/search/tv?query=" + Uri.EscapeDataString(Clean(name)));

		public static void TmdbMovie(string title, int year = 0) =>
			Open("https://www.themoviedb.org/search/movie?query=" + Uri.EscapeDataString(Clean(title)) + (year > 0 ? "%20y:" + year : ""));

		/// A download's name: an episode -> TheTVDB / TMDb TV, a movie -> TMDb.
		public static void ForName(string name, bool tvdb)
		{
			string show, code, title;
			int season, year;
			if (MediaNames.TryEpisode(name ?? "", out show, out season, out code))
			{
				if (tvdb) Tvdb(show);
				else TmdbTv(show);
			}
			else if (MediaNames.TryMovie(name ?? "", out title, out year)) TmdbMovie(title, year);
			else if (tvdb) Tvdb(name);
			else Open("https://www.themoviedb.org/search?query=" + Uri.EscapeDataString(Clean(name)));
		}

		public static void TmdbCollection(string name) => Open("https://www.themoviedb.org/search/collection?query=" + Uri.EscapeDataString(Clean(name)));

		/// What a row is: a show (name, TheTVDB id), a movie (title, year), a collection, or a file / download name.
		public sealed class What
		{
			public string Kind = "", Name = "", Id = "";
			public int Year;
			public static What Show(string name, string id = "") => new What { Kind = "show", Name = name, Id = id };
			public static What Movie(string title, int year) => new What { Kind = "movie", Name = title, Year = year };
			public static What Collection(string name) => new What { Kind = "collection", Name = name };
			public static What FromName(string name) => new What { Kind = "name", Name = name };
		}

		/// Adds "Look it up on TheTVDB / TMDb" to a list's right-click menu (making one if it has none).
		public static void AddTo(ListView lv, Func<ListViewItem, What> whatOf)
		{
			ContextMenuStrip menu = lv.ContextMenuStrip;
			if (menu == null)
			{
				menu = new ContextMenuStrip();
				lv.ContextMenuStrip = menu;
				lv.MouseDown += (s, e) =>
				{
					if (e.Button != MouseButtons.Right) return;
					ListViewItem hit = lv.GetItemAt(e.X, e.Y);
					if (hit != null && !hit.Selected) { lv.SelectedItems.Clear(); hit.Selected = true; }
				};
			}
			else menu.Items.Add(new ToolStripSeparator());
			ToolStripMenuItem tvdb = new ToolStripMenuItem("Look it up on TheTVDB");
			ToolStripMenuItem tmdb = new ToolStripMenuItem("Look it up on TMDb");
			Func<What> current = () => lv.SelectedItems.Count > 0 ? whatOf(lv.SelectedItems[0]) : null;
			tvdb.Click += (s, e) =>
			{
				What w = current();
				if (w == null) return;
				if (w.Kind == "show") Tvdb(w.Name, w.Id);
				else ForName(w.Name, true);
			};
			tmdb.Click += (s, e) =>
			{
				What w = current();
				if (w == null) return;
				if (w.Kind == "show") TmdbTv(w.Name);
				else if (w.Kind == "movie") TmdbMovie(w.Name, w.Year);
				else if (w.Kind == "collection") TmdbCollection(w.Name);
				else ForName(w.Name, false);
			};
			menu.Items.Add(tvdb);
			menu.Items.Add(tmdb);
			menu.Opening += (s, e) =>
			{
				What w = current();
				tvdb.Visible = w != null && w.Kind != "movie" && w.Kind != "collection";
				tmdb.Visible = w != null;
			};
		}

		/// "Highlander: The Series (1992)" -> "Highlander: The Series"
		private static string Clean(string s) => Regex.Replace((s ?? "").Trim(), @"\s*\((19|20)\d{2}\)\s*$", "");

		private static void Open(string url)
		{
			try
			{
				Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
			}
			catch (Exception ex)
			{
				MessageBox.Show("Could not open the web browser: " + ex.Message + "\r\n\r\n" + url, "MyTorrents");
			}
		}
	}
}
