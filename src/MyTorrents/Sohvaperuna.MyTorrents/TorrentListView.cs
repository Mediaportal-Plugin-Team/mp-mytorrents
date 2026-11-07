using System.Collections.Generic;
using MediaPortal.Dialogs;
using MediaPortal.GUI.Library;
using Sohvaperuna.MediaPortalExt;
using Sohvaperuna.TorrentEngine;

namespace Sohvaperuna.MyTorrents
{
	internal class TorrentListView
	{
		[SkinControl(208)]
		protected GUIListControl torrentList = null;

		[SkinControl(4)]
		protected GUIButtonControl buttonBack = null;

		private TorrentLabel _selectedLabel = null;

		private void Window_ActiveTorrentList_TorrentsUpdated()
		{
			UpdateTorrentList(timed: true);
		}

		public GUIListItem FindItemByHash(GUIListControl list, Torrent torrent)
		{
			foreach (GUIListItem listItem in list.ListItems)
			{
				if ((listItem.AlbumInfoTag as Torrent).Hash == torrent.Hash)
				{
					return listItem;
				}
			}
			return null;
		}

		public string Ellipsis(string str, int len)
		{
			if (str.Length > len)
			{
				return str.Substring(0, len) + "...";
			}
			return str;
		}

		private void UpdateTorrentList(bool timed)
		{
			if (torrentList == null)
			{
				return;
			}
			List<GUIListItem> list = new List<GUIListItem>(torrentList.ListItems);
			foreach (Torrent item in TorrentEngine.Instance().TorrentSession.TorrentsAll)
			{
				if (item.Progress < 100.0)
				{
					GUIListItem gUIListItem = FindItemByHash(torrentList, item);
					if (gUIListItem == null)
					{
						gUIListItem = new GUIListItem($"{Ellipsis(PostProcessor.DisplayName(item), 75)}");
						gUIListItem.AlbumInfoTag = item;
						torrentList.Add(gUIListItem);
					}
					else
					{
						list.Remove(gUIListItem);
						gUIListItem.Label = Ellipsis(PostProcessor.DisplayName(item), 75); // 2026: the real name once it is known
					}
					gUIListItem.Label2 = $"{UnitConvert.SizeToString(item.Size)} ({item.Progress:F2}%)";
				}
			}
			foreach (GUIListItem item2 in list)
			{
				torrentList.ListItems.Remove(item2);
			}
			if (torrentList.ListItems.Count == 0)
			{
				GUIPropertyManager.SetProperty("#MyTorrents.Count", "0");
				GUIListItem gUIListItem = new GUIListItem();
				gUIListItem.Label = "No torrents.";
				torrentList.Add(gUIListItem);
			}
			else
			{
				GUIPropertyManager.SetProperty("#MyTorrents.Count", $"{torrentList.ListItems.Count}");
			}
		}

		public void TorrentsContextMenu(Torrent uTorrent)
		{
			GUIDialogMenu gUIDialogMenu = (GUIDialogMenu)GUIWindowManager.GetWindow(2012);
			if (gUIDialogMenu == null)
			{
				return;
			}
			gUIDialogMenu.Reset();
			gUIDialogMenu.SetHeading("Torrent details menu");
			if (uTorrent.Started())
			{
				gUIDialogMenu.Add("Stop");
			}
			else
			{
				gUIDialogMenu.Add("Start");
			}
			if (uTorrent.Paused())
			{
				gUIDialogMenu.Add("Resume");
			}
			else
			{
				gUIDialogMenu.Add("Pause");
			}
			gUIDialogMenu.Add("Remove");
			gUIDialogMenu.Add("Start All");
			gUIDialogMenu.Add("Stop All");
			gUIDialogMenu.Add("Pause All");
			gUIDialogMenu.Add("Resume All");
			gUIDialogMenu.DoModal(GUIWindowManager.ActiveWindow);
			Menus.Check(gUIDialogMenu);
			Menu menu = new Menu();
			switch (gUIDialogMenu.SelectedLabelText)
			{
			case "Start":
				TorrentEngine.Instance().TorrentSession.Start(uTorrent.Hash);
				break;
			case "Stop":
				TorrentEngine.Instance().TorrentSession.Stop(uTorrent.Hash);
				break;
			case "Resume":
				TorrentEngine.Instance().TorrentSession.Resume(uTorrent.Hash);
				break;
			case "Pause":
				TorrentEngine.Instance().TorrentSession.Pause(uTorrent.Hash);
				break;
			case "Remove":
			{
				GUIDialogYesNo gUIDialogYesNo = (GUIDialogYesNo)GUIWindowManager.GetWindow(100);
				gUIDialogYesNo.Reset();
				gUIDialogYesNo.SetHeading("Remove also files?");
				gUIDialogYesNo.SetLine(1, "Files are removed permanently.");
				gUIDialogYesNo.SetLine(2, "Cannot be undone.");
				gUIDialogYesNo.SetDefaultToYes(bYesNo: false);
				gUIDialogYesNo.DoModal(GUIWindowManager.ActiveWindow);
				Menus.Check(gUIDialogYesNo);
				if (gUIDialogYesNo.IsConfirmed)
				{
					TorrentEngine.Instance().TorrentSession.Remove(uTorrent.Hash, removeData: true);
				}
				else
				{
					TorrentEngine.Instance().TorrentSession.Remove(uTorrent.Hash, removeData: false);
				}
				// 2026: an Auto grabber download that had not finished - look for another copy? (same as the
				// Downloads tab in MyTorrents Configuration; the copy removed is never used again)
				if (uTorrent.Progress < 100.0 && Grabbed.FindByHash(uTorrent.Hash) != null)
				{
					GUIDialogYesNo again = (GUIDialogYesNo)GUIWindowManager.GetWindow(100);
					again.Reset();
					again.SetHeading("Look for another copy?");
					again.SetLine(1, "The Auto grabber started this one and it had not finished.");
					again.SetLine(2, "Yes = try a different copy of that episode soon.");
					again.SetLine(3, "No = leave the episode for now.");
					again.SetDefaultToYes(bYesNo: true);
					again.DoModal(GUIWindowManager.ActiveWindow);
					Menus.Check(again);
					if (again.IsConfirmed)
					{
						Grabbed.RequestRetry(uTorrent.Hash);
						Log.Instance().Print("Removed in MediaPortal: " + uTorrent.Name + " - another copy will be looked for");
					}
				}
				break;
			}
			case "Start All":
				TorrentEngine.Instance().TorrentSession.Start(TorrentEngine.Instance().TorrentSession.TorrentsAll);
				break;
			case "Stop All":
				TorrentEngine.Instance().TorrentSession.Stop(TorrentEngine.Instance().TorrentSession.TorrentsAll);
				break;
			case "Pause All":
				TorrentEngine.Instance().TorrentSession.Pause(TorrentEngine.Instance().TorrentSession.TorrentsAll);
				break;
			case "Resume All":
				TorrentEngine.Instance().TorrentSession.Resume(TorrentEngine.Instance().TorrentSession.TorrentsAll);
				break;
			}
		}

		public void ViewButtonHandler()
		{
			GUIDialogMenu gUIDialogMenu = (GUIDialogMenu)GUIWindowManager.GetWindow(2012);
			if (gUIDialogMenu != null)
			{
				MyTorrents.ListType = "Torrents";
				gUIDialogMenu.Reset();
				gUIDialogMenu.SetHeading("Switch Torrent View");
				gUIDialogMenu.Add("All");
				gUIDialogMenu.Add("Active");
				gUIDialogMenu.Add("Downloading");
				gUIDialogMenu.Add("Seeding");
				gUIDialogMenu.Add("Finished");
				gUIDialogMenu.Add("Paused");
				gUIDialogMenu.Add("With Errors");
				gUIDialogMenu.Add("Stopped");
				gUIDialogMenu.DoModal(GUIWindowManager.ActiveWindow);
				Menus.Check(gUIDialogMenu);
				switch (gUIDialogMenu.SelectedLabelText)
				{
				case "All":
				case "Active":
				case "Downloading":
				case "Seeding":
				case "Finished":
				case "Paused":
				case "Stopped":
					MyTorrents.TorrentView = gUIDialogMenu.SelectedLabelText;
					break;
				case "With Errors":
					MyTorrents.TorrentView = "Errors";
					break;
				}
			}
		}

		public void LabelButtonHandler()
		{
			GUIDialogMenu gUIDialogMenu = (GUIDialogMenu)GUIWindowManager.GetWindow(2012);
			if (gUIDialogMenu == null)
			{
				return;
			}
			gUIDialogMenu.Reset();
			gUIDialogMenu.SetHeading("Choose label");
			gUIDialogMenu.Add("None");
			gUIDialogMenu.Add("No Label");
			foreach (TorrentLabel label in TorrentEngine.Instance().TorrentSession.Labels)
			{
				if (!(label.Name == ""))
				{
					GUIListItem gUIListItem = new GUIListItem();
					gUIListItem.Label = label.Name;
					gUIListItem.AlbumInfoTag = label.Name;
					gUIDialogMenu.Add(gUIListItem);
				}
			}
			gUIDialogMenu.DoModal(GUIWindowManager.ActiveWindow);
			Menus.Check(gUIDialogMenu);
			switch (gUIDialogMenu.SelectedId)
			{
			case -1:
				break;
			case 1:
				MyTorrents.LabelFilter = null;
				break;
			case 2:
				MyTorrents.LabelFilter = "";
				break;
			default:
				MyTorrents.LabelFilter = gUIDialogMenu.SelectedLabelText;
				break;
			}
		}

		public void SortButtonHandler()
		{
			GUIDialogMenu gUIDialogMenu = (GUIDialogMenu)GUIWindowManager.GetWindow(2012);
			if (gUIDialogMenu != null)
			{
				gUIDialogMenu.Reset();
				gUIDialogMenu.SetHeading("Choose sort order");
				gUIDialogMenu.Add("Default");
				gUIDialogMenu.Add("By Name ASC");
				gUIDialogMenu.Add("By Name DESC");
				gUIDialogMenu.Add("By Size ASC");
				gUIDialogMenu.Add("By Size DESC");
				if (TorrentEngine.Instance().TorrentsAll[0].AddedOn != 0)
				{
					gUIDialogMenu.Add("By Date Added ASC");
					gUIDialogMenu.Add("By Date Added DESC");
				}
				gUIDialogMenu.DoModal(GUIWindowManager.ActiveWindow);
				Menus.Check(gUIDialogMenu);
				switch (gUIDialogMenu.SelectedLabelText)
				{
				case "Default":
					MyTorrents.SortOrder = "Default";
					break;
				case "By Name ASC":
					MyTorrents.SortOrder = "Name ASC";
					break;
				case "By Name DESC":
					MyTorrents.SortOrder = "Name DESC";
					break;
				case "By Size ASC":
					MyTorrents.SortOrder = "Size ASC";
					break;
				case "By Size DESC":
					MyTorrents.SortOrder = "Size DESC";
					break;
				case "By Date Added ASC":
					MyTorrents.SortOrder = "Date Added ASC";
					break;
				case "By Date Added DESC":
					MyTorrents.SortOrder = "Date Added DESC";
					break;
				}
			}
		}

		protected void OnClicked(int controlId, GUIControl control, Action.ActionType actionType)
		{
			if (control.GetID == 4)
			{
			}
			if (control.GetID == 208)
			{
				if (actionType == Action.ActionType.ACTION_SELECT_ITEM)
				{
					Torrent torrent = torrentList.SelectedListItem.AlbumInfoTag as Torrent;
					if (torrent == null)
					{
					}
				}
			}
			else if (control.GetID == 0)
			{
			}
		}
	}
}
