using System.Collections.Generic;

namespace Sohvaperuna.TorrentSearch
{
	internal class TorrentMatchListSort : IComparer<TorrentMatch>
	{
		private string _name;

		private string _iname;

		private string _itype;

		public string Name => _name;

		public string IName => _iname;

		public string IType => _itype;

		public TorrentMatchListSort(string name, string iname, string itype)
		{
			_name = name;
			_iname = iname;
			_itype = itype;
		}

		public int Compare(TorrentMatch x, TorrentMatch y)
		{
			if (x == null || y == null)
			{
				return 0;
			}
			switch (IType)
			{
			case "Seeds":
				if (x.Seed == y.Seed)
				{
					return 0;
				}
				return (x.Seed <= y.Seed) ? 1 : (-1);
			case "Leech":
				if (x.Leech == y.Leech)
				{
					return 0;
				}
				return (x.Leech <= y.Leech) ? 1 : (-1);
			case "Size":
				if (x.Size == y.Size)
				{
					return 0;
				}
				return (x.Size <= y.Size) ? 1 : (-1);
			case "Date":
				if (x.Date.Ticks == y.Date.Ticks)
				{
					return 0;
				}
				return (x.Date.Ticks <= y.Date.Ticks) ? 1 : (-1);
			default:
				return 0;
			}
		}
	}
}
