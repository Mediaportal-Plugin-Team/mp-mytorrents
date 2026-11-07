using System;
using System.Linq;
using System.Reflection;
using MediaPortal.Dialogs;
using MediaPortal.GUI.Library;
using Sohvaperuna.TorrentEngine;

namespace Sohvaperuna.MyTorrents
{
	internal class Notifier
	{
		private static Notifier _instance = null;

		public int DownNotifyID = 0;

		public int UpNotifyID = 0;

		public int SeedNotifyID = 0;

		public int ActiveNotifyID = 0;

		public bool IsNBInitialized = false;

		public static bool Initialize()
		{
			_instance = new Notifier();
			return true;
		}

		public static Notifier Instance()
		{
			return _instance;
		}

		protected Notifier()
		{
			TorrentEngine torrentEngine = TorrentEngine.Instance();
			torrentEngine.TorrentAdded += _torrentSession_TorrentAdded;
			torrentEngine.TorrentRemoved += _torrentSession_TorrentRemoved;
			torrentEngine.TorrentCompleted += _torrentSession_TorrentCompleted;
		}

		private void _torrentSession_TorrentRemoved(Torrent torrent)
		{
			Log.Instance().Print($"Torrent [{torrent.Name}] removed.");
		}

		private void _torrentSession_TorrentCompleted(Torrent torrent)
		{
			Log.Instance().Print($"Torrent [{torrent.Name}] Completed.");
			if (!PostProcessor.Enabled)
			{
				ShowPopup("Download finished", torrent.Name); // otherwise "Copied to your library" says it
			}
		}

		private void _torrentSession_TorrentAdded(Torrent torrent)
		{
			Log.Instance().Print($"Torrent [{torrent.Name}] added.");
		}

		private void ShowPopup(string type, string name)
		{
			if (!GUIGraphicsContext.IsFullScreenVideo)
			{
				GUIDialogNotify gUIDialogNotify = (GUIDialogNotify)GUIWindowManager.GetWindow(2016);
				gUIDialogNotify.Reset();
				gUIDialogNotify.SetHeading(type);
				gUIDialogNotify.SetText(name);
				gUIDialogNotify.DoModal(GUIWindowManager.ActiveWindow);
				Menus.Check(gUIDialogNotify);
			}
		}

		// MPNotificationBar is not available for 64-bit MP 1.3x; popups use MP's built-in notify dialog instead.
		public bool IsNotificationBarAvailable()
		{
			return false;
		}

		public void ChangeText()
		{
		}
	}
}
