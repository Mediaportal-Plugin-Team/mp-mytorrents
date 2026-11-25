using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MediaPortal.GUI.Library;

namespace Sohvaperuna.MyTorrents
{
	/// <summary>
	/// 2026 (1.3.44): what Media Center Master used to do for new downloads, so MCM isn't needed any more.
	///  - Movies: look the film up on TMDb (through MP-TVSeries, which has the TMDb key - none in MyTorrents) or Trakt,
	///    write a small .nfo with its IMDb / TMDb id next to it (Moving Pictures then matches the exact film), and -
	///    if you choose - file it straight into your Movies folder, or Collections\[collection name]\ when it belongs
	///    to one (e.g. Collections\Alien Collection\Alien (1979)\).
	///  - TV: straight after an episode is copied in, MP-TVSeries is told to import it (no waiting for its scan).
	/// </summary>
	public static class MovieFiling
	{
		public sealed class Info
		{
			public string Title = "", ImdbId = "", Collection = "";
			public int Year, TmdbId;
			public double Rating;                      // 2026 (1.3.49): for movie names like "[7.5]"
			public List<string> Genres = new List<string>();
		}

		/// "new" = into the movies folder (e.g. Movies New) as before, you file them; "library" = file them for me.
		public static string Mode => Configuration.GetExtra("Films.FileInto", "new");

		public static string MoviesFolder => (Configuration.GetExtra("Films.MoviesFolder", "") ?? "").Trim();

		public static string CollectionsFolder => (Configuration.GetExtra("Films.CollectionsFolder", "") ?? "").Trim();

		public static bool WriteNfo => Configuration.GetExtra("Films.WriteNfo", "true") != "false";

		public static bool FileForMe => Mode == "library" && MoviesFolder.Length > 0;

		/// 2026 (1.3.45): "file them for me, but show me first" - they wait in the new-movies folder for your OK.
		public static bool AskFirst => Mode == "ask" && MoviesFolder.Length > 0;

		// ------------------------------------------------------------------ waiting for your OK (1.3.45)

		public sealed class Pending
		{
			public string From = "", To = "", Title = "";
			public string Display => Title + "  ->  " + To;
		}

		private static readonly object PendingLock = new object();

		private static string PendingFile => MediaPortal.Configuration.Config.GetFile(MediaPortal.Configuration.Config.Dir.Config, "MyTorrents.movies-to-file.txt");

		/// Movies waiting for your OK: (the folder in the new-movies folder, where it would go).
		public static List<Pending> Waiting()
		{
			lock (PendingLock)
			{
				try
				{
					if (!File.Exists(PendingFile)) return new List<Pending>();
					return File.ReadAllLines(PendingFile).Select(l => l.Split('\t')).Where(p => p.Length >= 3)
						.Select(p => new Pending { From = p[0], To = p[1], Title = p[2] }).Where(p => Directory.Exists(p.From) || File.Exists(p.From)).ToList();
				}
				catch
				{
					return new List<Pending>();
				}
			}
		}

		private static void SaveWaiting(List<Pending> list)
		{
			try
			{
				File.WriteAllLines(PendingFile, list.Select(p => string.Join("\t", p.From, p.To, p.Title)));
			}
			catch
			{
			}
		}

		/// After copying a movie into the new-movies folder: note where "file them for me" would put it.
		public static void AddWaiting(string copiedFile, Info info)
		{
			try
			{
				string folder = Path.GetDirectoryName(copiedFile);
				string target = Place(copiedFile, info, true);
				string targetFolder = Path.GetDirectoryName(target);
				if (string.Equals(folder, targetFolder, StringComparison.OrdinalIgnoreCase)) return;
				lock (PendingLock)
				{
					List<Pending> list = Waiting();
					if (list.Any(p => string.Equals(p.From, folder, StringComparison.OrdinalIgnoreCase))) return;
					list.Add(new Pending { From = folder, To = targetFolder, Title = Path.GetFileName(folder) });
					SaveWaiting(list);
				}
				Log.Instance().Print("Movies: " + Path.GetFileName(folder) + " is waiting for your OK to go to " + targetFolder);
			}
			catch (Exception ex)
			{
				Log.Instance().Print("Movies: " + ex.Message);
			}
		}

		/// OK - move it into place (the whole folder: movie, .nfo, subtitles). Returns "" or what went wrong.
		public static string Approve(Pending p)
		{
			try
			{
				if (Directory.Exists(p.To)) return p.Title + ": " + p.To + " is already there - left where it is";
				Directory.CreateDirectory(Path.GetDirectoryName(p.To));
				try
				{
					Directory.Move(p.From, p.To);
				}
				catch (IOException)
				{
					// another drive or share: copy, check, then remove the old one
					CopyFolder(p.From, p.To);
					Directory.Delete(p.From, true);
				}
				Forget(p);
				foreach (string f in Directory.GetFiles(p.To)) LibraryFolders.NoteMovieFile(f);
				Log.Instance().Print("Movies: filed " + p.Title + " into " + p.To);
				return "";
			}
			catch (Exception ex)
			{
				return p.Title + ": " + ex.Message;
			}
		}

		private static void CopyFolder(string from, string to)
		{
			Directory.CreateDirectory(to);
			foreach (string f in Directory.GetFiles(from))
			{
				string d = Path.Combine(to, Path.GetFileName(f));
				File.Copy(f, d, true);
				if (new FileInfo(d).Length != new FileInfo(f).Length) throw new IOException("copy of " + Path.GetFileName(f) + " is incomplete");
			}
			foreach (string sub in Directory.GetDirectories(from)) CopyFolder(sub, Path.Combine(to, Path.GetFileName(sub)));
		}

		/// Leave it in the new-movies folder (off the list).
		public static void Forget(Pending p)
		{
			lock (PendingLock)
			{
				List<Pending> list = Waiting();
				list.RemoveAll(x => string.Equals(x.From, p.From, StringComparison.OrdinalIgnoreCase));
				SaveWaiting(list);
			}
		}

		// ------------------------------------------------------------------ looking a film up

		private static MethodInfo _tmdbGet;
		private static bool _tmdbLooked;

		/// MP-TVSeries' TMDb client (it has the key); null when MP-TVSeries isn't loaded.
		private static MethodInfo TmdbGet
		{
			get
			{
				if (_tmdbLooked) return _tmdbGet;
				_tmdbLooked = true;
				try
				{
					Type t = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("WindowPlugins.GUITVSeries.Online_Parsing_Classes.Tmdb.TmdbClient", false)).FirstOrDefault(x => x != null);
					if (t == null)
					{
						// MyTorrents Configuration / the background helper: load MP-TVSeries ourselves (1.3.46)
						try
						{
							t = Assembly.Load("MP-TVSeries").GetType("WindowPlugins.GUITVSeries.Online_Parsing_Classes.Tmdb.TmdbClient", false);
						}
						catch
						{
						}
					}
					_tmdbGet = t?.GetMethod("Get", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, new[] { typeof(string) }, null);
				}
				catch
				{
					_tmdbGet = null;
				}
				return _tmdbGet;
			}
		}

		private static object Tmdb(string pathAndQuery)
		{
			MethodInfo m = TmdbGet;
			if (m == null) return null;
			try
			{
				return m.Invoke(null, new object[] { pathAndQuery });
			}
			catch (Exception ex)
			{
				Log.Instance().Print("Movies: TMDb lookup failed: " + (ex.InnerException ?? ex).Message);
				return null;
			}
		}

		/// A TMDb request through MP-TVSeries (null when it can't be done).
		public static object TmdbCall(string pathAndQuery) => Tmdb(pathAndQuery);

		private static object Field(object o, string name) => (o as IDictionary)?[name];

		private static string Str(object o, string name) => Convert.ToString(Field(o, name) ?? "");

		private static int Int(object o, string name)
		{
			int n;
			return int.TryParse(Str(o, name), out n) ? n : 0;
		}

		/// The film's TMDb / IMDb ids and collection - null when it can't be found.
		public static Info Lookup(string title, int year)
		{
			try
			{
				object search = Tmdb("/search/movie?query=" + Uri.EscapeDataString(title ?? "") + (year > 0 ? "&year=" + year : "") + "&language=en-US");
				IList results = Field(search, "results") as IList;
				object pick = null;
				if (results != null)
				{
					string key = MediaNames.Key(title);
					pick = results.Cast<object>().FirstOrDefault(r => MediaNames.Key(Str(r, "title")) == key || MediaNames.Key(Str(r, "original_title")) == key) ?? results.Cast<object>().FirstOrDefault();
				}
				if (pick != null)
				{
					int id = Int(pick, "id");
					object movie = Tmdb("/movie/" + id + "?language=en-US");
					if (movie != null)
					{
						Info i = new Info { TmdbId = id, Title = Str(movie, "title"), ImdbId = Str(movie, "imdb_id"), Collection = Str(Field(movie, "belongs_to_collection"), "name") };
						string rd = Str(movie, "release_date");
						int y;
						if (rd.Length >= 4 && int.TryParse(rd.Substring(0, 4), out y)) i.Year = y;
						if (i.Title.Length == 0) i.Title = title;
						double r;
						if (double.TryParse(Str(movie, "vote_average"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out r)) i.Rating = r;
						if (Field(movie, "genres") is IList gl) i.Genres = gl.Cast<object>().Select(g => Str(g, "name")).Where(g => g.Length > 0).ToList();
						return i;
					}
				}
			}
			catch (Exception ex)
			{
				Log.Instance().Print("Movies: TMDb lookup failed: " + ex.Message);
			}
			// no MP-TVSeries / TMDb: Trakt has the ids (but not collections)
			try
			{
				TraktAPI.DataModels.TraktMovie m = (global::TraktAPI.TraktAPI.SearchMovies(title) ?? Enumerable.Empty<TraktAPI.DataModels.TraktSearchResult>())
					.Where(r => r.Movie != null).Select(r => (TraktAPI.DataModels.TraktMovie)r.Movie)
					.OrderByDescending(x => year > 0 && x.Year == year).FirstOrDefault();
				if (m != null) return new Info { Title = m.Title, Year = m.Year ?? year, ImdbId = m.Ids?.Imdb ?? "", TmdbId = m.Ids?.Tmdb ?? 0 };
			}
			catch
			{
			}
			return null;
		}

		// ------------------------------------------------------------------ where a new movie goes

		/// The new place for a movie file worked out by the usual naming ("...\Title (Year)\Title (Year).mkv"):
		/// unchanged unless "file them for me" is on.
		public static string Place(string dest, Info info) => Place(dest, info, false);

		public static string Place(string dest, Info info, bool evenIfAsking)
		{
			if (dest == null || !(FileForMe || (evenIfAsking && MoviesFolder.Length > 0))) return dest;
			string folderName = Path.GetFileName(Path.GetDirectoryName(dest));
			string fileName = Path.GetFileName(dest);
			string root = MoviesFolder;
			if (info != null && info.Collection.Length > 0 && CollectionsFolder.Length > 0)
			{
				string coll = MediaNames.SafeName(info.Collection);
				// a collection folder you already have with nearly the same name ("Alien Collection" / "Alien Anthology")
				try
				{
					if (Directory.Exists(CollectionsFolder))
					{
						string key = MediaNames.Key(info.Collection.Replace(" Collection", ""));
						string have = Directory.GetDirectories(CollectionsFolder).FirstOrDefault(d => MediaNames.Key(Path.GetFileName(d)).StartsWith(key));
						if (have != null) coll = Path.GetFileName(have);
					}
				}
				catch
				{
				}
				root = Path.Combine(CollectionsFolder, coll);
			}
			return Path.Combine(root, folderName, fileName);
		}

		/// A small .nfo next to the movie with its IMDb / TMDb id - Moving Pictures reads the IMDb id from it.
		public static void Nfo(string movieFile, Info info)
		{
			if (!WriteNfo || info == null || (info.ImdbId.Length == 0 && info.TmdbId == 0)) return;
			try
			{
				string nfo = Path.ChangeExtension(movieFile, ".nfo");
				if (File.Exists(nfo)) return;
				string x(string s) => System.Security.SecurityElement.Escape(s ?? "");
				File.WriteAllText(nfo, "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n<movie>\r\n" +
					"  <title>" + x(info.Title) + "</title>\r\n" +
					(info.Year > 0 ? "  <year>" + info.Year + "</year>\r\n" : "") +
					(info.ImdbId.Length > 0 ? "  <imdbid>" + x(info.ImdbId) + "</imdbid>\r\n  <id>" + x(info.ImdbId) + "</id>\r\n" : "") +
					(info.TmdbId > 0 ? "  <tmdbid>" + info.TmdbId + "</tmdbid>\r\n" : "") +
					(info.Collection.Length > 0 ? "  <set>" + x(info.Collection) + "</set>\r\n" : "") +
					"</movie>\r\n" +
					(info.ImdbId.Length > 0 ? "https://www.imdb.com/title/" + info.ImdbId + "/\r\n" : ""));
				Log.Instance().Print("Movies: wrote " + Path.GetFileName(nfo) + (info.ImdbId.Length > 0 ? " (" + info.ImdbId + ")" : ""));
			}
			catch (Exception ex)
			{
				Log.Instance().Print("Movies: could not write the .nfo: " + ex.Message);
			}
		}

		// ------------------------------------------------------------------ TV: tell MP-TVSeries to import now

		/// Hands new episode files to MP-TVSeries' importer straight away (as its folder watcher would), so they show
		/// up in TV Series within a minute. Does nothing when MP-TVSeries isn't running.
		public static void ImportTv(List<string> files)
		{
			try
			{
				if (files == null || files.Count == 0) return;
				Assembly asm = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "MP-TVSeries");
				if (asm == null) return;
				Type pairType = asm.GetType("WindowPlugins.GUITVSeries.PathPair");
				Type paramsType = asm.GetType("WindowPlugins.GUITVSeries.CParsingParameters");
				Type actionType = asm.GetType("WindowPlugins.GUITVSeries.ParsingAction");
				object plugin = GUIWindowManager.GetWindow(9811);
				FieldInfo queueField = plugin?.GetType().GetField("m_parserUpdaterQueue", BindingFlags.NonPublic | BindingFlags.Instance);
				if (pairType == null || paramsType == null || actionType == null || queueField == null) return;
				List<string> roots = LibraryFolders.TV().Select(r => r.TrimEnd('\\') + "\\").ToList();
				IList pairs = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(pairType));
				foreach (string f in files.Distinct(StringComparer.OrdinalIgnoreCase))
				{
					string root = roots.Where(r => f.StartsWith(r, StringComparison.OrdinalIgnoreCase)).OrderByDescending(r => r.Length).FirstOrDefault();
					if (root == null) continue; // not in one of MP-TVSeries' folders - its own scan won't see it either
					pairs.Add(Activator.CreateInstance(pairType, f.Substring(root.Length), f));
				}
				if (pairs.Count == 0) return;
				object add = Enum.Parse(actionType, "List_Add");
				object p = Activator.CreateInstance(paramsType, add, pairs, false, false);
				object queue = queueField.GetValue(plugin);
				lock (queue)
				{
					((IList)queue).Add(p);
				}
				Log.Instance().Print("Finished downloads: asked MP-TVSeries to import " + pairs.Count + " new episode(s) now");
			}
			catch (Exception ex)
			{
				Log.Instance().Print("Finished downloads: could not ask MP-TVSeries to import: " + ex.Message);
			}
		}
	}
}
