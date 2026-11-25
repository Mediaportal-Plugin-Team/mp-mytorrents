using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using MediaPortal.Dialogs;
using MediaPortal.GUI.Library;
using Sohvaperuna.TorrentEngine;

namespace Sohvaperuna.MyTorrents
{
	/// <summary>
	/// 2026: where the torrent program saves downloads - shown as #MyTorrents.SaveFolder ("Saving to: ...")
	/// and changeable from the MyTorrents menu with the remote ("Download folder").
	/// </summary>
	public static class SaveFolder
	{
		public static string Current(ITorrentClient client)
		{
			try
			{
				List<DownloadDir> dirs = client?.ListDirs();
				return dirs != null && dirs.Count > 0 ? (dirs[0].path ?? "").Trim() : "";
			}
			catch
			{
				return "";
			}
		}

		/// Reads the folder in the background and puts it in #MyTorrents.SaveFolder.
		public static void Refresh()
		{
			ThreadPool.QueueUserWorkItem(_ =>
			{
				string f = Current(TorrentEngine.Instance().TorrentSession);
				GUIPropertyManager.SetProperty("#MyTorrents.SaveFolder", f.Length > 0 ? f : "(not known)");
			});
		}

		/// "Download folder" menu button: pick a folder with the remote, then tell the torrent program.
		public static void ChangeFromMenu()
		{
			ITorrentClient client = TorrentEngine.Instance().TorrentSession;
			ISaveFolder setter = client as ISaveFolder;
			string current = Current(client);
			if (setter == null)
			{
				MyTorrents.Error("Download folder", "This torrent program's folder can only be changed in the program itself." + (current.Length > 0 ? "\nNow: " + current : ""));
				return;
			}
			string chosen = Pick(current);
			if (chosen == null || chosen.Equals(current, StringComparison.OrdinalIgnoreCase)) return;
			if (setter.SetSaveFolder(chosen))
			{
				Log.Instance().Print("Download folder changed to " + chosen);
				GUIPropertyManager.SetProperty("#MyTorrents.SaveFolder", chosen);
				MyTorrents.Error("Download folder", "New downloads are saved to:\n" + chosen);
			}
			else
			{
				MyTorrents.Error("Download folder", "The torrent program did not accept the new folder.");
			}
		}

		private const string UseThis = "[ Use this folder ]";
		private const string Up = "[ .. up one folder ]";
		private const string Drives = "[ Another drive ]";
		private const string NewFolder = "[ New folder here... ]";

		/// "D:\   Media - 1.2 TB free"
		private static string DriveText(DriveInfo d)
		{
			string label = "";
			try
			{
				label = d.VolumeLabel;
			}
			catch
			{
			}
			string free = "";
			try
			{
				double gb = d.AvailableFreeSpace / 1024.0 / 1024.0 / 1024.0;
				free = gb >= 1024 ? (gb / 1024).ToString("0.0") + " TB free" : gb.ToString("0") + " GB free";
			}
			catch
			{
			}
			return d.RootDirectory.FullName + "   " + (label.Length > 0 ? label + " - " : "") + free;
		}

		private static bool Hidden(string path)
		{
			try
			{
				return (new DirectoryInfo(path).Attributes & (FileAttributes.Hidden | FileAttributes.System)) != 0;
			}
			catch
			{
				return true; // cannot even look at it - leave it out
			}
		}

		/// 2026: a folder picker that works with a remote. Every list has "Another drive" so a second drive is one
		/// step away, and "New folder here..." to make e.g. E:\Torrents.
		private static string Pick(string start)
		{
			string dir = Directory.Exists(start) ? start : null;
			while (true)
			{
				GUIDialogMenu menu = (GUIDialogMenu)GUIWindowManager.GetWindow(2012);
				if (menu == null) return null;
				menu.Reset();
				List<string> items = new List<string>();
				List<string> labels = new List<string>();
				if (dir == null)
				{
					menu.SetHeading("Download folder - choose a drive");
					foreach (DriveInfo d in DriveInfo.GetDrives().Where(d => d.IsReady && (d.DriveType == DriveType.Fixed || d.DriveType == DriveType.Network || d.DriveType == DriveType.Removable)))
					{
						items.Add(d.RootDirectory.FullName);
						labels.Add(DriveText(d));
					}
				}
				else
				{
					menu.SetHeading(dir);
					items.Add(UseThis);
					labels.Add(UseThis);
					items.Add(Drives);
					labels.Add(Drives);
					if (Directory.GetParent(dir) != null)
					{
						items.Add(Up);
						labels.Add(Up);
					}
					items.Add(NewFolder);
					labels.Add(NewFolder);
					try
					{
						foreach (string p in Directory.GetDirectories(dir).Where(p => !Hidden(p)).OrderBy(p => p, StringComparer.CurrentCultureIgnoreCase))
						{
							items.Add(p);
							labels.Add(Path.GetFileName(p));
						}
					}
					catch (Exception ex)
					{
						Log.Instance().Print("Folder picker: " + ex.Message);
					}
				}
				foreach (string l in labels)
				{
					menu.Add(l);
				}
				menu.DoModal(GUIWindowManager.ActiveWindow);
				Menus.Check(menu);
				int sel = menu.SelectedLabel;
				if (sel < 0 || sel >= items.Count) return null; // Back
				string pick = items[sel];
				if (pick == UseThis) return dir;
				if (pick == Drives) dir = null;
				else if (pick == Up) dir = Directory.GetParent(dir)?.FullName;
				else if (pick == NewFolder)
				{
					string made = MakeFolder(dir);
					if (made != null) dir = made;
				}
				else dir = pick;
			}
		}

		/// Asks for a name with MediaPortal's keyboard and makes the folder ("Torrents" by default).
		private static string MakeFolder(string parent)
		{
			try
			{
				VirtualKeyboard keyboard = (VirtualKeyboard)GUIWindowManager.GetWindow(1002);
				if (keyboard == null) return null;
				keyboard.Reset();
				keyboard.Text = "Torrents";
				keyboard.DoModal(GUIWindowManager.ActiveWindow);
				Menus.Check(keyboard);
				if (!keyboard.IsConfirmed) return null;
				string name = MediaNames.SafeName(keyboard.Text);
				if (name.Length == 0) return null;
				string path = Path.Combine(parent, name);
				Directory.CreateDirectory(path);
				Log.Instance().Print("Folder picker: made " + path);
				return path;
			}
			catch (Exception ex)
			{
				MyTorrents.Error("Download folder", "Could not make that folder:\n" + ex.Message);
				return null;
			}
		}
	}
}
