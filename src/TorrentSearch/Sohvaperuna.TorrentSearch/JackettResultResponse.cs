using System.Collections.Generic;

namespace Sohvaperuna.TorrentSearch
{
	public class JackettResultResponse
	{
		public List<JackettIndexer> Indexers { get; set; }

		public List<JackettResult> Results { get; set; }
	}
}
