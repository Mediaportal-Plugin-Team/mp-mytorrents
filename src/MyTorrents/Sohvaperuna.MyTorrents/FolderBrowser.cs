using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MediaPortal.Dialogs;
using MediaPortal.GUI.Library;
using Sohvaperuna.TorrentEngine;
using Sohvaperuna.TorrentSearch;

namespace Sohvaperuna.MyTorrents
{
	internal class FolderBrowser
	{
		public List<GUIListItem> GUIFolders = new List<GUIListItem>();

		public List<string> Folders = new List<string>();

		private string backfolder = "...";

		private TorrentLabel _label = null;

		private string _downloadoption = "";

		public string folder = string.Empty;

		public List<string> GetFolders(string path, string folder)
		{
			if (folder == "")
			{
				return ListDisks();
			}
			if (folder == backfolder)
			{
				return ParentFolder(path);
			}
			if (path == "")
			{
				return SubFolder(folder);
			}
			return SubFolder(path + "\\" + folder);
		}

		public void FolderClickMenu(GUIListItem fldr, GUIListItem _selectedtorrent, TorrentLabel label)
		{
			GUIDialogMenu gUIDialogMenu = (GUIDialogMenu)GUIWindowManager.GetWindow(2012);
			if (gUIDialogMenu == null)
			{
				return;
			}
			gUIDialogMenu.Reset();
			gUIDialogMenu.SetHeading("Folder Options");
			gUIDialogMenu.Add("Set as download location");
			gUIDialogMenu.DoModal(GUIWindowManager.ActiveWindow);
			Menus.Check(gUIDialogMenu);
			string selectedLabelText = gUIDialogMenu.SelectedLabelText;
			if (selectedLabelText == null || !(selectedLabelText == "Set as download location"))
			{
				return;
			}
			MyTorrents.Instance().HideFolderBrowser();
			if (MyTorrents.ListType == "SeriesInfo")
			{
				SeriesItem item = _selectedtorrent.AlbumInfoTag as SeriesItem;
				(from e in MyTorrents.WL.GetSeriesList()
					where e.show.ID == item.show.ID
					select e).First().folder = fldr.Path + "\\" + fldr.Label;
				GUIPropertyManager.SetProperty("#MyTorrents.SeriesInfo.Folder", fldr.Path + "\\" + fldr.Label);
				return;
			}
			TorrentMatch torrentMatch = _selectedtorrent.AlbumInfoTag as TorrentMatch;
			bool flag = false;
			string hash = string.Empty;
			flag = ((!string.IsNullOrEmpty(torrentMatch.magnet)) ? TorrentEngine.Instance().StartMagnetDownloading(out hash, torrentMatch.magnet, label.Name, force: true, torrentMatch.cookie, torrentMatch.Id, _downloadoption, "", fldr.Path + "\\" + fldr.Label) : TorrentEngine.Instance().StartTorrentDownloading(out hash, torrentMatch.Url, label.Name, force: true, torrentMatch.cookie, torrentMatch.Id, _downloadoption, "", fldr.Path + "\\" + fldr.Label));
			folder = fldr.Path + "\\" + fldr.Label;
			if (flag) PostProcessor.Remember(hash, MyTorrents.Instance().ViewSearchKind, MyTorrents.Instance().ViewSearchText, torrentMatch.Title);
			MyTorrents.Instance().AfterDownloadStarted(flag, torrentMatch.Title);
		}

		private List<string> SubFolder(string CurDir)
		{
			List<string> list = new List<string>();
			string[] directories = Directory.GetDirectories(CurDir);
			list.Add(backfolder);
			list.AddRange(directories);
			return list;
		}

		private List<string> ParentFolder(string CurDir)
		{
			List<string> list = new List<string>();
			if (CurDir.IndexOf("\\") == CurDir.Length - 1)
			{
				list = ListDisks();
			}
			else
			{
				DirectoryInfo parent = Directory.GetParent(CurDir);
				string fullName = parent.FullName;
				string[] directories = Directory.GetDirectories(fullName);
				list.Add(backfolder);
				list.AddRange(directories);
			}
			return list;
		}

		public List<string> ListDisks()
		{
			return new List<string>(Environment.GetLogicalDrives());
		}

		public List<GUIListItem> GetGUIFolders(GUIListItem selected)
		{
			GUIFolders.Clear();
			List<string> folders = GetFolders(selected.Path, selected.Label);
			string text = selected.Path + "\\" + selected.Label;
			if (selected.Path == "")
			{
				text = selected.Label;
			}
			else if (selected.Label == "...")
			{
				text = selected.Path.Substring(0, selected.Path.LastIndexOf("\\"));
				if (text.IndexOf("\\") == -1)
				{
					text = "";
				}
			}
			else
			{
				text = selected.Path + "\\" + selected.Label;
			}
			foreach (string item in folders)
			{
				GUIListItem gUIListItem = new GUIListItem();
				string text2 = item.Substring(item.LastIndexOf("\\") + 1);
				gUIListItem.Label = ((item.Length > 3) ? item.Substring(item.LastIndexOf("\\") + 1) : item);
				gUIListItem.Path = text;
				GUIFolders.Add(gUIListItem);
			}
			return GUIFolders;
		}

		public List<GUIListItem> ShowFolderBrowser(ref string folder)
		{
			GUIFolders.Clear();
			List<string> folders = GetFolders("", "");
			foreach (string item in folders)
			{
				int num = item.LastIndexOf("\\");
				string text = item.Substring(item.LastIndexOf("\\") + 1);
				GUIListItem gUIListItem = new GUIListItem();
				gUIListItem.Label = item;
				gUIListItem.Path = "";
				GUIFolders.Add(gUIListItem);
			}
			return GUIFolders;
		}
	}
}
