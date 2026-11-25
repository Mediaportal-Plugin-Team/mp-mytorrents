using System.Collections.Generic;

namespace Sohvaperuna.TorrentSearch
{
	public class Result
	{
		public IDictionary<string, BTNTorrent> torrents { get; set; }

		public int results { get; set; }
	}
}
