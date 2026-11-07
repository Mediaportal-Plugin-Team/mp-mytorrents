using System;
using System.Linq;
using System.Threading;
using Sohvaperuna.TorrentEngine;
using Sohvaperuna.TorrentSearch;

namespace Sohvaperuna.MyTorrents
{
	public class UrlItem : WatchItem
	{
		private object lockobject;

		public string Name { get; set; }

		public string Url { get; set; }

		public new string SearchTerm { get; set; }

		public string Tracker { get; set; }

		public string Cookies { get; set; }

		public string Id { get; set; }

		public string LastHash { get; set; }

		public string Folder { get; set; }

		public override string GetLabel1()
		{
			return Name;
		}

		public override string GetLabel2()
		{
			return Tracker;
		}

		public override void DoSearch()
		{
			Thread thread = new Thread((ThreadStart)delegate
			{
				try
				{
					lock (lockobject)
					{
						TorrentSearchEngine torrentSearchEngine = new TorrentSearchEngine(Configuration.Instance().Settings["TorrentSearch.Config"] as string);
						UrlItem watch = this;
						string hash = string.Empty;
						ITorrentSearch torrentSearch = torrentSearchEngine.Engines.Where((ITorrentSearch torrentSearch2) => torrentSearch2.Name == watch.Tracker).First();
						TorrentFile torrentFile = TorrentEngine.Instance().GetTorrentFile(watch.Url, watch.Cookies, "");
						if (torrentFile != null && watch.LastHash != torrentFile.Hash)
						{
							Log.Instance().Print($"Watched item {watch.Name} has changed. Downloading new verion of torrent");
							watch.LastCheck = DateTime.Now;
							watch.LastHash = torrentFile.Hash;
							watch.NextCheck = watch.LastCheck.AddHours(1.0);
							bool flag = TorrentEngine.Instance().StartTorrentDownloading(out hash, watch.Url, watch.Label, force: true, "", watch.Name, "", "", watch.Folder);
						}
					}
				}
				catch (Exception e)
				{
					Log.Instance().Print(e);
				}
			});
			thread.IsBackground = true;
			thread.Name = "MyTorrents UrlAutoChecker";
			Thread thread2 = thread;
			thread2.Start();
		}
	}
}
