using System.Collections.Generic;
using System.IO;
using MediaPortal.Dialogs;
using MediaPortal.GUI.Library;
using MediaPortal.Player;
using MediaPortal.Util;
using Sohvaperuna.TorrentEngine;

namespace Sohvaperuna.MyTorrents
{
	public class TorrentDetailsView
	{
		public string SelectedHash = "";

		public List<GUIListItem> GetTorrentDetails(Torrent utorrent)
		{
			List<GUIListItem> list = new List<GUIListItem>();
			SelectedHash = utorrent.Hash;
			if (utorrent == null)
			{
			}
			List<Sohvaperuna.TorrentEngine.File> files = TorrentEngine.Instance().TorrentSession.GetFiles(utorrent.Hash);
			foreach (Sohvaperuna.TorrentEngine.File item in files)
			{
				GUIListItem gUIListItem = new GUIListItem();
				gUIListItem.Label = item.Filename;
				gUIListItem.IconImage = Path.Combine(GUIGraphicsContext.Skin, "\\Media\\icon_empty_focus1.png");
				gUIListItem.AlbumInfoTag = item;
				list.Add(gUIListItem);
				gUIListItem.Label2 = $"{UnitConvert.SizeToString(item.Filesize)} ({(double)item.Downloaded / (double)item.Filesize * 100.0:F2}%)";
			}
			GUIPropertyManager.SetProperty("#MyTorrents.Details.Name", PostProcessor.DisplayName(utorrent));
			GUIPropertyManager.SetProperty("#MyTorrents.Details.Progress", $"{utorrent.Progress:F2}");
			GUIPropertyManager.SetProperty("#MyTorrents.Details.Ratio", $"{(double)(float)utorrent.Ratio * 0.001:F2}");
			GUIPropertyManager.SetProperty("#MyTorrents.Details.ETA", UnitConvert.TimeRemainingToString(utorrent.ETA));
			GUIPropertyManager.SetProperty("#MyTorrents.Details.UploadSpeed", UnitConvert.TransferSpeedToString(utorrent.UploadSpeed));
			GUIPropertyManager.SetProperty("#MyTorrents.Details.DownloadSpeed", UnitConvert.TransferSpeedToString(utorrent.DownloadSpeed));
			GUIPropertyManager.SetProperty("#MyTorrents.Details.Peers", $"{utorrent.PeersConnected} ({utorrent.PeersInSwarm})");
			GUIPropertyManager.SetProperty("#MyTorrents.Details.Seeds", $"{utorrent.SeedsConnected} ({utorrent.SeedsInSwarm})");
			GUIPropertyManager.SetProperty("#MyTorrents.Details.Size", UnitConvert.SizeToString(utorrent.Size));
			return list;
		}

		public void TorrentDetailsOnClick(Sohvaperuna.TorrentEngine.File file, int FileIndex)
		{
			Torrent torrent = TorrentEngine.Instance().TorrentByHash(SelectedHash);
			GUIDialogMenu gUIDialogMenu = (GUIDialogMenu)GUIWindowManager.GetWindow(2012);
			if (gUIDialogMenu == null)
			{
				return;
			}
			gUIDialogMenu.Reset();
			gUIDialogMenu.SetHeading("File menu");
			Dictionary<string, int> dictionary = TorrentEngine.Instance().TorrentSession.FillPriorities();
			foreach (KeyValuePair<string, int> item in dictionary)
			{
				if (file.Priority != item.Value)
				{
					if (item.Key != "Do Not Download")
					{
						gUIDialogMenu.Add($"Set {item.Key} Priority");
					}
					else
					{
						gUIDialogMenu.Add("Do Not Download");
					}
				}
			}
			if (torrent.TorrentSavePath != null && file.Downloaded == file.Filesize && ValidateExtension(file.Filename))
			{
				gUIDialogMenu.Add("Play File");
			}
			if (torrent.StreamID != null)
			{
				gUIDialogMenu.Add("Stream File");
			}
			gUIDialogMenu.DoModal(GUIWindowManager.ActiveWindow);
			Menus.Check(gUIDialogMenu);
			if (gUIDialogMenu.SelectedLabelText.IndexOf("Priority") != -1)
			{
				file.SetFilePriority(SelectedHash, FileIndex, dictionary[gUIDialogMenu.SelectedLabelText.Replace("Set", "").Replace(" Priority", "")]);
			}
			switch (gUIDialogMenu.SelectedLabelText)
			{
			case "Do Not Download":
				file.SetFilePriority(SelectedHash, FileIndex, dictionary[gUIDialogMenu.SelectedLabelText]);
				break;
			case "Play File":
				g_Player.PlayBackEnded += OnPlayBackEnded;
				g_Player.PlayBackStopped += OnPlayBackStopped;
				g_Player.Play(Path.Combine(torrent.TorrentSavePath, file.Filename));
				if (Utils.VideoExtensions.Contains(GetExtension(file.Filename)))
				{
					g_Player.ShowFullScreenWindow();
				}
				break;
			}
		}

		public void TorrentDetailsContextMenu(Sohvaperuna.TorrentEngine.File file, int FileIndex)
		{
			TorrentDetailsOnClick(file, FileIndex);
		}

		public bool ValidateExtension(string filename)
		{
			string extension = GetExtension(filename);
			bool flag = Utils.VideoExtensions.Contains(extension);
			bool flag2 = Utils.AudioExtensions.Contains(extension);
			return flag || flag2;
		}

		public string GetExtension(string filename)
		{
			int startIndex = filename.LastIndexOf('.');
			return filename.Substring(startIndex);
		}

		public void OnPlayBackEnded(g_Player.MediaType type, string filename)
		{
			if (GUIGraphicsContext.IsFullScreenVideo)
			{
				GUIGraphicsContext.IsFullScreenVideo = false;
			}
		}

		public void OnPlayBackStopped(g_Player.MediaType type, int timeMovieStopped, string filename)
		{
			if (GUIGraphicsContext.IsFullScreenVideo)
			{
				GUIGraphicsContext.IsFullScreenVideo = false;
			}
		}
	}
}
