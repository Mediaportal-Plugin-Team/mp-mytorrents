using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Xml;
using MediaPortal.Dialogs;
using MediaPortal.GUI.Library;
using Sohvaperuna.RSSReader;
using Sohvaperuna.TorrentEngine;

namespace Sohvaperuna.MyTorrents
{
	internal class RSSView
	{
		public List<GUIListItem> rssList = new List<GUIListItem>();

		private IChannel _selectedChannel = null;

		private IRSSChannel _selectedRSSChannel = null;

		public bool LoadRssChannels(ConfigData _config)
		{
			XmlDocument myTorrentsConfiguration = _config.MyTorrentsConfiguration;
			RSSChannelManager rSSChannelManager = RSSChannelManager.Instance();
			XmlNode feedNode = myTorrentsConfiguration?.SelectSingleNode("config/feed");
			if (feedNode == null)
			{
				return true;
			}
			XmlNodeList childNodes = feedNode.ChildNodes;
			foreach (XmlNode item2 in childNodes)
			{
				if (item2.NodeType != XmlNodeType.Element || item2.SelectSingleNode("url") == null)
				{
					continue; // comments / incomplete entries
				}
				IRSSChannel iRSSChannel = null;
				string type = "";
				if (item2.Name == "rss")
				{
					type = "rss";
				}
				if (item2.Name == "atom")
				{
					type = "atom";
				}
				XmlNodeList xmlNodeList = item2.SelectNodes("url");
				if (xmlNodeList.Count >= 2)
				{
					XmlNode xmlNode2 = item2.SelectSingleNode("title");
					XmlNode xmlNode3 = item2.SelectSingleNode("description");
					if (xmlNode2 == null || xmlNode3 == null)
					{
						continue;
					}
					RSSCombined rSSCombined = new RSSCombined(xmlNode2.InnerText, xmlNode3.InnerText);
					foreach (XmlNode item3 in item2.SelectNodes("url"))
					{
						RSSChannel item = new RSSChannel(item3.InnerText);
						rSSCombined.Channels.Add(item);
					}
					iRSSChannel = rSSCombined;
				}
				else if (item2.SelectSingleNode("transform") != null)
				{
					XmlNode xmlNode5 = item2.SelectSingleNode("url");
					XmlNode xmlNode6 = item2.SelectSingleNode("transform/origin");
					XmlNode xmlNode7 = item2.SelectSingleNode("transform/basedownload");
					iRSSChannel = new RSSChannel(xmlNode5.InnerText, new Regex(xmlNode6.InnerText, RegexOptions.IgnoreCase), xmlNode7.InnerText);
				}
				else
				{
					XmlNode xmlNode4 = item2.SelectSingleNode("url");
					iRSSChannel = new RSSChannel(xmlNode4.InnerText);
				}
				XmlNode xmlNode8 = item2.SelectSingleNode("expires");
				if (xmlNode8 == null)
				{
					iRSSChannel.ExpireTime = new TimeSpan(0, 5, 0);
				}
				else
				{
					iRSSChannel.ExpireTime = new TimeSpan(0, 0, Convert.ToInt32(xmlNode8.InnerText));
				}
				iRSSChannel.Type = type;
				rSSChannelManager.Channels.Add(iRSSChannel);
			}
			Log.Instance().Print("Started RssChanel");
			return true;
		}

		protected void OnPageLoad()
		{
		}

		private void View_TorrentRSS_RSSChannelUpdated(IRSSChannel channel)
		{
		}

		public List<GUIListItem> UpdateSelectedRSS()
		{
			return ShowSelectedRSS(_selectedRSSChannel);
		}

		public List<GUIListItem> ShowSelectedRSS(IRSSChannel channel)
		{
			_selectedRSSChannel = channel;
			List<GUIListItem> list = new List<GUIListItem>();
			list.Clear();
			IconMapping iconMapping = Configuration.Instance().Settings["MyTorrents.GlobalIconMapping"] as IconMapping;
			foreach (RSSItem item in channel.Items)
			{
				GUIListItem gUIListItem = new GUIListItem();
				gUIListItem.PinImage = iconMapping.GetIcon(item.Title);
				gUIListItem.Label = ((item.Title.Length > 130) ? item.Title.Substring(0, 130) : item.Title);
				gUIListItem.Label2 = item.PublishDate.ToShortDateString() + " " + item.PublishDate.ToShortTimeString();
				gUIListItem.AlbumInfoTag = item;
				list.Add(gUIListItem);
			}
			return list;
		}

		public List<GUIListItem> ShowRSS()
		{
			List<GUIListItem> list = new List<GUIListItem>();
			list.Clear();
			RSSChannelManager.Instance().UpdateChannels(force: true);
			foreach (IRSSChannel channel in RSSChannelManager.Instance().Channels)
			{
				GUIListItem gUIListItem = new GUIListItem();
				gUIListItem.Label = channel.Title + " " + channel.Description;
				gUIListItem.Label2 = channel.Updated.ToShortDateString() + " " + channel.Updated.ToShortTimeString();
				gUIListItem.AlbumInfoTag = channel;
				list.Add(gUIListItem);
			}
			return list;
		}

		public IRSSChannel RSSContextMenu(IRSSChannel channel)
		{
			GUIDialogMenu gUIDialogMenu = (GUIDialogMenu)GUIWindowManager.GetWindow(2012);
			if (gUIDialogMenu == null)
			{
				return null;
			}
			gUIDialogMenu.Reset();
			gUIDialogMenu.SetHeading("RSS Feeds");
			gUIDialogMenu.Add("Search");
			gUIDialogMenu.Add("Update selected");
			gUIDialogMenu.Add("Update all");
			gUIDialogMenu.DoModal(GUIWindowManager.ActiveWindow);
			Menus.Check(gUIDialogMenu);
			switch (gUIDialogMenu.SelectedLabelText)
			{
			case "Update selected":
				if (channel == null)
				{
					return null;
				}
				channel?.Update();
				return null;
			case "Update all":
				RSSChannelManager.Instance().UpdateChannels(force: true);
				return null;
			case "Search":
			{
				VirtualKeyboard virtualKeyboard = (VirtualKeyboard)GUIWindowManager.GetWindow(1002);
				virtualKeyboard.Reset();
				virtualKeyboard.Text = "";
				virtualKeyboard.DoModal(GUIWindowManager.ActiveWindow);
				Menus.Check(virtualKeyboard);
				if (!virtualKeyboard.IsConfirmed)
				{
					return null;
				}
				Regex regex = new Regex(virtualKeyboard.Text, RegexOptions.IgnoreCase);
				RSSChannel rSSChannel = new RSSChannel("MediaPortal");
				if (channel != null && channel.Url != "MediaPortal")
				{
					foreach (RSSItem item in channel.Items)
					{
						if (regex.IsMatch(item.Title))
						{
							rSSChannel.Items.Add(item);
						}
					}
				}
				return rSSChannel;
			}
			default:
				return null;
			}
		}

		public void RSSDownload(RSSItem item)
		{
			GUIDialogYesNo gUIDialogYesNo = (GUIDialogYesNo)GUIWindowManager.GetWindow(100);
			gUIDialogYesNo.SetHeading("Download this torrent?");
			gUIDialogYesNo.SetLine(1, item.Title);
			gUIDialogYesNo.DoModal(GUIWindowManager.ActiveWindow);
			Menus.Check(gUIDialogYesNo);
			if (!gUIDialogYesNo.IsConfirmed)
			{
				return;
			}
			TorrentLabel torrentLabel = DialogAskLabel.Ask();
			if (torrentLabel != null)
			{
				string hash = string.Empty;
				if (!TorrentEngine.Instance().StartDownloadingToFolder(out hash, item.Link, torrentLabel.Name, force: true, "", "0", "", "", "", RSSParser.GetLinkType(item.Link)))
				{
					GUIDialogNotify gUIDialogNotify = (GUIDialogNotify)GUIWindowManager.GetWindow(2016);
					gUIDialogNotify.Reset();
					gUIDialogNotify.SetHeading("Failed!");
					gUIDialogNotify.SetText("Unable to start download.");
					gUIDialogNotify.DoModal(GUIWindowManager.ActiveWindow);
					Menus.Check(gUIDialogNotify);
				}
			}
		}

		public void OnRSSAction(MediaPortal.GUI.Library.Action action)
		{
			if (action.wID == MediaPortal.GUI.Library.Action.ActionType.ACTION_PREVIOUS_MENU)
			{
				if (_selectedRSSChannel != null)
				{
					_selectedRSSChannel = null;
				}
			}
			else if (action.wID != MediaPortal.GUI.Library.Action.ActionType.ACTION_MOVE_DOWN && action.wID != MediaPortal.GUI.Library.Action.ActionType.ACTION_MOVE_UP)
			{
			}
		}

		private void OnButtonBack()
		{
			if (_selectedChannel != null)
			{
				_selectedChannel = null;
			}
			else
			{
				MediaPortal.GUI.Library.Action action = new MediaPortal.GUI.Library.Action(MediaPortal.GUI.Library.Action.ActionType.ACTION_PREVIOUS_MENU, 0f, 0f);
			}
		}
	}
}
