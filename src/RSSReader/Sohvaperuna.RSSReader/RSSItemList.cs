using System.Collections.Generic;

namespace Sohvaperuna.RSSReader
{
	public class RSSItemList : List<RSSItem>
	{
		public void SortByDate()
		{
			Sort(new RSSItemListComparer());
		}
	}
}
