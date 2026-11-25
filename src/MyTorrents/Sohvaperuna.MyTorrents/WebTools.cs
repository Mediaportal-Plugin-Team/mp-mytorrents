using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

namespace Sohvaperuna.MyTorrents
{
	/// <summary>
	/// 2026 (1.3.69): the tools that were only in MyTorrents Configuration - duplicates check, renaming movies to match
	/// your naming, and the Background tab's state - on the web page too.
	/// </summary>
	public static partial class WebServer
	{
		// ------------------------------------------------------------------ duplicates (Check-up tab)

		private static List<Duplicates.Group> _dupes;
		private static DateTime _dupesAt = DateTime.MinValue;

		/// The duplicates search reads both libraries, so it is kept for 10 minutes (or until something is done).
		private static List<Duplicates.Group> DupeList(bool fresh)
		{
			if (fresh || _dupes == null || (DateTime.Now - _dupesAt).TotalMinutes > 10)
			{
				_dupes = Duplicates.Find();
				_dupesAt = DateTime.Now;
			}
			return _dupes;
		}

		private static void DupesPage(StringBuilder b, Dictionary<string, string> f)
		{
			b.Append("<h2>Duplicates check</h2>");
			if (Get(f, "go") != "1" && _dupes == null)
			{
				b.Append("<p class=dim>Looks through MP-TVSeries and Moving Pictures for the same episode or movie more than once. Takes a minute on a big library.</p>" +
					"<p><a class=btn href=\"/dupes?go=1\">Look for duplicates</a></p>");
				DupesHidden(b);
				return;
			}
			List<Duplicates.Group> all = DupeList(Get(f, "go") == "1" && Get(f, "i") == "");
			if (all.Count == 0)
			{
				b.Append("<p>No duplicates - every episode and movie is there once.</p>");
				DupesHidden(b);
				return;
			}
			int i;
			int.TryParse(Get(f, "i"), out i);
			if (i < 0) i = 0;
			if (i >= all.Count) i = all.Count - 1;
			Duplicates.Group g = all[i];
			bool different = g.OtherEpisode || g.OtherShow;
			b.Append("<p class=dim>" + (i + 1) + " of " + all.Count + " &middot; " +
				(i > 0 ? "<a href=\"/dupes?i=" + (i - 1) + "\">&lt; Back</a> &middot; " : "") +
				(i + 1 < all.Count ? "<a href=\"/dupes?i=" + (i + 1) + "\">Skip for now &gt;</a> &middot; " : "") +
				"<a href=\"/dupes?go=1\">Look again</a></p>");
			b.Append("<h3>" + H(g.Title) + "</h3><p>" + H(Duplicates.Why(g)).Replace("\n", "<br>") + "</p><table><tr><th>Copy</th><th>Picture</th><th>Sound</th><th>Size</th><th></th></tr>");
			for (int c = 0; c < g.Copies.Count; c++)
			{
				Duplicates.Copy x = g.Copies[c];
				string note = Duplicates.Note(g, x);
				b.Append("<tr><td>" + (x == g.Keep && !different ? "<b>" + H(Path.GetFileName(x.File)) + "</b> (the best one)" : H(Path.GetFileName(x.File))) +
					"<div class=\"dim small\">" + H(Path.GetDirectoryName(x.File)) + (note.Length > 0 ? " &middot; " + H(note) : "") + "</div></td><td>" + H(x.Picture) + "</td><td>" + H(x.Sound) +
					"</td><td data-v=\"" + x.Size + "\">" + (x.Size > 0 ? Films.SizeText(x.Size / 1024 / 1024) : "") + "</td><td>" +
					(different ? "" : Button("dupkeep", "Keep this one", "key", g.Key, "file", x.File, "i", i.ToString())) + "</td></tr>");
			}
			b.Append("</table><p>" + Button("dupall", "Keep them all - they're different", "key", g.Key, "i", i.ToString()) +
				"<span class=\"dim small\"> &nbsp;Keep this one hides the others (renamed .duplicate) - nothing is deleted. You can put them back below.</span></p>");
			DupesHidden(b);
		}

		private static void DupesHidden(StringBuilder b)
		{
			List<string> aside = Duplicates.SetAside();
			b.Append("<h3>Hidden copies (" + aside.Count + ")</h3>");
			if (aside.Count == 0)
			{
				b.Append("<p class=dim>None.</p>");
				return;
			}
			b.Append("<p class=dim>Hidden by the duplicates check. Put back = it's played again; delete for good can't be undone.</p><table><tr><th>File</th><th></th></tr>");
			foreach (string a in aside)
				b.Append("<tr><td>" + H(Path.GetFileName(a)) + "<div class=\"dim small\">" + H(Path.GetDirectoryName(a)) + "</div></td><td>" + Button("dupback", "Put back", "file", a) +
					"<form method=post class=inline onsubmit=\"return confirm('Delete this copy for good? It can\\'t be undone.')\"><input type=hidden name=a value=dupdelete><input type=hidden name=file value=\"" + H(a) + "\"><button>Delete for good</button></form></td></tr>");
			b.Append("</table>");
		}

		private static string DupesAct(string a, Dictionary<string, string> f)
		{
			string i = Get(f, "i");
			switch (a)
			{
			case "dupkeep":
			case "dupall":
			{
				List<Duplicates.Group> all = DupeList(false);
				Duplicates.Group g = all.FirstOrDefault(x => x.Key == Get(f, "key"));
				if (g == null) return "/dupes?go=1&msg=" + Uri.EscapeDataString("That one has changed - looked again");
				string msg;
				if (a == "dupall")
				{
					Duplicates.Ignore(g);
					msg = g.Title + ": all kept - won't be shown again";
				}
				else
				{
					if (g.OtherEpisode || g.OtherShow) return "/dupes?i=" + i + "&msg=" + Uri.EscapeDataString("These are different episodes - keep them all");
					Duplicates.Copy keep = g.Copies.FirstOrDefault(c => c.File == Get(f, "file"));
					if (keep == null) return "/dupes?i=" + i;
					List<string> bad = new List<string>();
					int ok = 0;
					foreach (Duplicates.Copy c in g.Copies.Where(c => c != keep))
					{
						string err = Duplicates.PutAside(c.File);
						if (err.Length == 0) ok++; else bad.Add(Path.GetFileName(c.File) + ": " + err);
					}
					msg = g.Title + ": kept 1, hid " + ok + " (nothing deleted)" + (bad.Count > 0 ? ". Couldn't hide " + string.Join("; ", bad) : "");
				}
				all.Remove(g);
				return "/dupes?i=" + i + "&msg=" + Uri.EscapeDataString(msg);
			}
			case "dupback":
			{
				string err = Duplicates.PutBack(Get(f, "file"));
				return "/dupes?msg=" + Uri.EscapeDataString(err.Length == 0 ? "Put back: " + Path.GetFileName(Get(f, "file")) : "Couldn't put it back: " + err);
			}
			case "dupdelete":
			{
				string err = Duplicates.DeleteForGood(Get(f, "file"));
				return "/dupes?msg=" + Uri.EscapeDataString(err.Length == 0 ? "Deleted: " + Path.GetFileName(Get(f, "file")) : "Couldn't delete it: " + err);
			}
			}
			return null;
		}

		// ------------------------------------------------------------------ rename movies to match my naming (Movies tab)

		private static void RenamePage(StringBuilder b)
		{
			List<MovieRenamer.Item> list = MovieRenamer.Proposals();
			b.Append("<h2>Rename to match my naming (" + list.Count + ")</h2><p class=dim>Movies in your library whose names don't match your naming choices (File management tab). " +
				"Renamed movies are read again by Moving Pictures.</p>");
			if (list.Count == 0)
			{
				b.Append("<p>Every movie already matches.</p>");
				return;
			}
			b.Append("<p>" + Button("renameall", "Rename them all (" + list.Count + ")") + "</p><table><tr><th>Movie</th><th>Now</th><th>Will be</th><th></th></tr>");
			foreach (MovieRenamer.Item x in list)
				b.Append("<tr><td>" + H(x.Display) + "</td><td class=small>" + H(Path.GetFileName(x.OldFile)) + "</td><td class=small>" + H(Path.GetFileName(x.NewFile)) + (x.FolderToo ? "<div class=dim>and its folder</div>" : "") +
					"</td><td>" + Button("renameone", "Rename", "file", x.OldFile) + "</td></tr>");
			b.Append("</table>");
		}

		private static string RenameAct(string a, Dictionary<string, string> f)
		{
			List<MovieRenamer.Item> list = MovieRenamer.Proposals();
			if (a == "renameone") list = list.Where(x => x.OldFile == Get(f, "file")).ToList();
			int ok = 0;
			List<string> bad = new List<string>();
			foreach (MovieRenamer.Item x in list)
			{
				string r = MovieRenamer.Rename(x);
				if (string.IsNullOrEmpty(r)) ok++; else bad.Add(x.Display + ": " + r);
			}
			if (ok > 0) PostProcessor.RescanMovingPictures();
			return "/rename?msg=" + Uri.EscapeDataString("Renamed " + ok + (bad.Count > 0 ? ". Not renamed: " + string.Join("; ", bad.Take(5)) : ""));
		}

		// ------------------------------------------------------------------ Background tab

		private static void BackgroundPage(StringBuilder b)
		{
			bool mp = System.Diagnostics.Process.GetProcessesByName("MediaPortal").Length > 0;
			b.Append("<h2>Background</h2><table class=form>" +
				"<tr><td>Doing the work now</td><td>" + (mp ? "MediaPortal" : "the background helper (MediaPortal is closed)") + "</td></tr>" +
				"<tr><td>This web page</td><td>http://" + H(Dns.GetHostName().ToLowerInvariant()) + ":" + Port + (Password.Length > 0 ? " &middot; password on (user name mytorrents)" : " &middot; no password") + "</td></tr>" +
				"</table><p class=dim>Turning the background helper or this web page off, or changing its port or password, stays in MyTorrents Configuration on the HTPC " +
				"(Background tab) - doing it from here would cut this page off.</p>");
		}
	}
}
