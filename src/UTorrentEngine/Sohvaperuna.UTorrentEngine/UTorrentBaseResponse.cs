using System.Collections.Generic;

namespace Sohvaperuna.UTorrentEngine
{
	public class UTorrentBaseResponse
	{
		public int build { get; set; }

		public IList<object[]> label { get; set; }

		public IList<object> torrents { get; set; }

		public string torrentc { get; set; }

		public IList<object> rssfeeds { get; set; }

		public IList<object> rssfilters { get; set; }
	}
}
