using System.Collections.Generic;

namespace Sohvaperuna.RSSReader
{
	public class RSSUTorrentFeeds : IChannel
	{
		private string type;

		public int Ident { get; set; }

		public bool Enabled { get; set; }

		public bool UseFeedTitle { get; set; }

		public bool UserSelected { get; set; }

		public bool Programmed { get; set; }

		public int DownloadState { get; set; }

		public string Url { get; set; }

		public int Timestamp { get; set; }

		public List<RSSNextUpdate> nextUpd { get; set; }

		public string Type
		{
			get
			{
				return type;
			}
			set
			{
				type = "UTorrentFeed";
			}
		}
	}
}
