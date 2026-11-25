using System.Collections.Generic;

namespace Sohvaperuna.TorrentEngine
{
	public class TorrentList : List<Torrent>
	{
		public TorrentList()
		{
		}

		public TorrentList(IEnumerable<Torrent> list)
			: base(list)
		{
		}

		public Torrent TorrentByHash(string hash)
		{
			using (Enumerator enumerator = GetEnumerator())
			{
				while (enumerator.MoveNext())
				{
					Torrent current = enumerator.Current;
					if (string.Equals(current.Hash, hash, System.StringComparison.OrdinalIgnoreCase))
					{
						return current;
					}
				}
			}
			return null;
		}
	}
}
