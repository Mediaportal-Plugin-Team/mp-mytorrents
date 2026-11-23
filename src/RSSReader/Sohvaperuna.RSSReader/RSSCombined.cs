using System;
using System.Collections.Generic;

namespace Sohvaperuna.RSSReader
{
	public class RSSCombined : IRSSChannel, IChannel
	{
		private string _mytitle;

		private string _mydescription;

		private List<RSSChannel> _channels;

		private RSSItemList _itemList;

		private DateTime _updated;

		private TimeSpan _expireTime;

		public string Type { get; set; }

		public TimeSpan ExpireTime
		{
			get
			{
				return _expireTime;
			}
			set
			{
				_expireTime = value;
			}
		}

		public List<RSSChannel> Channels => _channels;

		public string Description
		{
			get
			{
				return _mydescription;
			}
			set
			{
				_mydescription = value;
			}
		}

		public string Title
		{
			get
			{
				return _mytitle;
			}
			set
			{
				_mytitle = value;
			}
		}

		public DateTime Updated
		{
			get
			{
				return _updated;
			}
			set
			{
				_updated = value;
			}
		}

		public RSSItemList Items
		{
			get
			{
				_itemList.Clear();
				foreach (RSSChannel channel in Channels)
				{
					foreach (RSSItem item in channel.Items)
					{
						_itemList.Add(item);
					}
				}
				_itemList.SortByDate();
				return _itemList;
			}
			set
			{
				_itemList = value;
			}
		}

		public string Language
		{
			get
			{
				return "Combined";
			}
			set
			{
			}
		}

		public string Link
		{
			get
			{
				return "Not applicable";
			}
			set
			{
			}
		}

		public string Url
		{
			get
			{
				return "Not applicable.";
			}
			set
			{
			}
		}

		public RSSCombined(string myTitle, string myDescription)
		{
			_mytitle = myTitle;
			_mydescription = myDescription;
			_channels = new List<RSSChannel>();
		}

		public bool Expired()
		{
			TimeSpan timeSpan = DateTime.Now - Updated;
			return timeSpan > ExpireTime;
		}

		public void Update()
		{
			foreach (RSSChannel channel in Channels)
			{
				channel.Update();
			}
			_updated = DateTime.Now;
		}

		public void Reset()
		{
			foreach (RSSChannel channel in Channels)
			{
				channel.Reset();
			}
		}
	}
}
