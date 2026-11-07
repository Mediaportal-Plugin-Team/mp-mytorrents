using System.Collections.Generic;
using MediaPortal.GUI.Library;

namespace Sohvaperuna.MyTorrents
{
	internal class LogView
	{
		public static List<string> logList = null;

		public void LoadTorrentLog()
		{
			UpdateLogItems();
		}

		private void UpdateLogItems()
		{
			foreach (string line in Log.Instance().Lines)
			{
				logList.Add(line);
			}
			GUIPropertyManager.SetProperty("#MyTorrents.Count", $"{logList.Count}");
		}
	}
}
