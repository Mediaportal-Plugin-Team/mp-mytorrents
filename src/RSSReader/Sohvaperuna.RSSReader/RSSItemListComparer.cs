using System.Collections.Generic;

namespace Sohvaperuna.RSSReader
{
	public class RSSItemListComparer : IComparer<RSSItem>
	{
		public int Compare(RSSItem x, RSSItem y)
		{
			if (x.PublishDate.Ticks < y.PublishDate.Ticks)
			{
				return 1;
			}
			if (x.PublishDate.Ticks > y.PublishDate.Ticks)
			{
				return -1;
			}
			return 0;
		}
	}
}
