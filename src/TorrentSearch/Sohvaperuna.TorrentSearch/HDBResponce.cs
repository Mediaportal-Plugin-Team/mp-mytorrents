using System.Collections.Generic;

namespace Sohvaperuna.TorrentSearch
{
	public class HDBResponce
	{
		public string status { get; set; }

		public List<HDBTorrent> data { get; set; }
	}
}
