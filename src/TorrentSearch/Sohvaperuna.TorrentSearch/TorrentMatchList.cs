using System.Collections.Generic;

namespace Sohvaperuna.TorrentSearch
{
	public class TorrentMatchList : List<TorrentMatch>
	{
		private object _id;

		public object Id => _id;

		public TorrentMatchList(object id)
		{
			_id = id;
		}
	}
}
