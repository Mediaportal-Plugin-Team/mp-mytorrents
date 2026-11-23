using System.Collections.Generic;

namespace Sohvaperuna.DelugeEngine
{
	public class UpdateResponse
	{
		public DelugeStat stats { get; set; }

		public bool connected { get; set; }

		public Dictionary<string, DelugeTorrent> torrents { get; set; }

		public DelugeFilter filters { get; set; }
	}
}
