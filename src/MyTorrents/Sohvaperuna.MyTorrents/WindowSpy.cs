using System.Linq;
using MediaPortal.GUI.Library;

namespace Sohvaperuna.MyTorrents
{
	public class WindowSpy
	{
		public int BackId;

		public void Start()
		{
			GUIWindowManager.OnActivateWindow += GUIWindowManager_OnActivateWindow;
		}

		private void GUIWindowManager_OnActivateWindow(int windowId)
		{
			int[] source = new int[9] { 5680, 5679, 5678, 5681, 5682, 5683, 5684, 5685, 7658 };
			if (!source.Contains(windowId))
			{
				BackId = windowId;
				if (TorrentEngine.Instance() != null)
				{
					TorrentEngine.Instance().UpdateTimer = 1000.0 * double.Parse(Configuration.Instance().Settings["Torrent.Timer.Outside"].ToString());
				}
			}
			else
			{
				TorrentEngine.Instance().UpdateTimer = 1000.0 * double.Parse(Configuration.Instance().Settings["Torrent.Timer.Inside"].ToString());
			}
		}
	}
}
