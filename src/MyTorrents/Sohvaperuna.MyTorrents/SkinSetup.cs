using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml;
using MediaPortal.Configuration;

namespace Sohvaperuna.MyTorrents
{
	/// <summary>
	/// Makes MyTorrents work in every skin (2026).
	///
	/// The installer puts the "universal" screens in plugins\windows\MyTorrents.Skin\ (1080\, 720\, Media\).
	/// At start-up this copies them into each installed skin that has no MyTorrents screens of its own,
	/// choosing 1920x1080 or 1280x720 from the skin's references.xml. MyTorrents screens that came with a
	/// skin (Titan, TITANIUS, Frames...) were made for older plugin versions and no longer work, so they are
	/// moved to a "MyTorrents-<skin>-original-screens" folder in that skin. Universal files are recognised by
	/// a marker comment and updated when a newer version of the plugin is installed.
	/// </summary>
	public static class SkinSetup
	{
		private const string Marker = "MyTorrents universal screens v";
		private const string MainScreen = "MP-MyTorrents.xml";
		private const string DialogScreen = "MP-MyTorrents.DialogCheckList.xml";

		private static bool _done;
		private static readonly object _lock = new object();

		public static void EnsureAllSkins()
		{
			lock (_lock)
			{
				if (_done)
				{
					return;
				}
				_done = true;
				try
				{
					string templates = Path.Combine(Config.GetFolder(Config.Dir.Plugins), "windows", "MyTorrents.Skin");
					string skins = Config.GetFolder(Config.Dir.Skin);
					if (!Directory.Exists(templates) || !Directory.Exists(skins))
					{
						Log.Instance().Print("SkinSetup: no universal screens found in " + templates);
						return;
					}
					foreach (string skin in Directory.GetDirectories(skins))
					{
						try
						{
							EnsureSkin(templates, skin);
						}
						catch (Exception ex)
						{
							Log.Instance().Print($"SkinSetup: could not set up skin {Path.GetFileName(skin)}: {ex.Message}");
						}
					}
				}
				catch (Exception ex)
				{
					Log.Instance().Print("SkinSetup failed: " + ex.Message);
				}
			}
		}

		private static void EnsureSkin(string templates, string skin)
		{
			string references = Path.Combine(skin, "references.xml");
			if (!File.Exists(references))
			{
				return; // not a real skin (leftover folder)
			}
			string set = SkinWidth(references) >= 1600 ? "1080" : "720";
			string source = Path.Combine(templates, set);
			string main = Path.Combine(skin, MainScreen);
			int copied = 0;
			if (File.Exists(main) && UniversalVersion(main) == null)
			{
				// The MyTorrents screens that came with skins (Titan, TITANIUS, Frames...) were made for
				// older versions of the plugin and no longer work (status light mid-screen, menu out of reach,
				// missing screens). Keep them in a backup folder and use the universal screens instead.
				copied += MoveAsideOldScreens(skin);
			}
			bool ownScreens = File.Exists(main) && UniversalVersion(main) == null;

			foreach (string file in Directory.GetFiles(source, "*.xml"))
			{
				string name = Path.GetFileName(file);
				if (ownScreens && !name.Equals(DialogScreen, StringComparison.OrdinalIgnoreCase))
				{
					continue; // the skin has its own MyTorrents screens
				}
				if (CopyIfNeeded(file, Path.Combine(skin, name)))
				{
					copied++;
				}
			}

			string media = Path.Combine(templates, "Media");
			if (Directory.Exists(media))
			{
				string skinMedia = Path.Combine(skin, "Media");
				Directory.CreateDirectory(skinMedia);
				foreach (string file in Directory.GetFiles(media))
				{
					string dest = Path.Combine(skinMedia, Path.GetFileName(file));
					if (!File.Exists(dest) || (IsOldCopyOfOurs(dest) && Hash(dest) != Hash(file)))
					{
						File.Copy(file, dest, overwrite: true);
						copied++;
					}
				}
			}
			if (copied > 0)
			{
				Log.Instance().Print($"SkinSetup: {Path.GetFileName(skin)} - {copied} file(s) added ({(ownScreens ? "skin has its own screens" : "universal " + set)})");
			}
		}

		/// Files an earlier MyTorrents version put in skins, which may be replaced by newer ones.
		/// (v1.1: hover_mytorrents.png was a small icon; Titan-style skins need a full-screen background.)
		private static readonly string[] OldOwnFiles =
		{
			"c2f8dbc1f137994de56492d9d35bf27b95ae59ee29784ce338e2d6885e28dc80"
		};

		private static bool IsOldCopyOfOurs(string file)
		{
			return Array.IndexOf(OldOwnFiles, Hash(file)) >= 0;
		}

		/// Moves every MyTorrents screen without our marker into a backup folder inside the skin.
		private static int MoveAsideOldScreens(string skin)
		{
			string backup = Path.Combine(skin, "MyTorrents-" + Path.GetFileName(skin) + "-original-screens");
			int moved = 0;
			foreach (string file in Directory.GetFiles(skin, "MP-MyTorrents*.xml"))
			{
				if (UniversalVersion(file) != null)
				{
					continue;
				}
				Directory.CreateDirectory(backup);
				string dest = Path.Combine(backup, Path.GetFileName(file));
				if (File.Exists(dest))
				{
					File.Delete(file); // already backed up earlier
				}
				else
				{
					File.Move(file, dest);
				}
				moved++;
			}
			if (moved > 0)
			{
				Log.Instance().Print($"SkinSetup: {Path.GetFileName(skin)} - moved {moved} old MyTorrents screen(s) to {Path.GetFileName(backup)}; using the universal screens");
			}
			return moved;
		}

		private static string Hash(string file)
		{
			try
			{
				using (System.Security.Cryptography.SHA256 sha = System.Security.Cryptography.SHA256.Create())
				using (FileStream fs = File.OpenRead(file))
				{
					return BitConverter.ToString(sha.ComputeHash(fs)).Replace("-", "").ToLowerInvariant();
				}
			}
			catch
			{
				return "";
			}
		}

		/// Copies a universal screen unless the skin already has the same or a newer one, or its own file.
		private static bool CopyIfNeeded(string template, string dest)
		{
			if (File.Exists(dest))
			{
				Version have = UniversalVersion(dest);
				Version offer = UniversalVersion(template);
				if (have == null || offer == null || have >= offer)
				{
					return false; // skin's own file, or already up to date
				}
			}
			File.Copy(template, dest, overwrite: true);
			return true;
		}

		/// Version from our marker comment, or null when the file is not one of ours.
		private static Version UniversalVersion(string file)
		{
			try
			{
				using (StreamReader r = new StreamReader(file))
				{
					char[] buf = new char[600];
					int n = r.Read(buf, 0, buf.Length);
					Match m = Regex.Match(new string(buf, 0, n), Regex.Escape(Marker) + @"([0-9.]+)");
					Version v;
					return m.Success && Version.TryParse(m.Groups[1].Value.TrimEnd('.'), out v) ? v : null;
				}
			}
			catch
			{
				return null;
			}
		}

		private static int SkinWidth(string references)
		{
			try
			{
				XmlDocument doc = new XmlDocument();
				doc.Load(references);
				XmlNode w = doc.SelectSingleNode("//skin/width") ?? doc.SelectSingleNode("//width");
				int width;
				return w != null && int.TryParse(w.InnerText.Trim(), out width) ? width : 1920;
			}
			catch
			{
				return 1920;
			}
		}
	}
}
