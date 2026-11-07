using System;
using System.Collections.Generic;

namespace Sohvaperuna.MyTorrents
{
	internal class ContentCache
	{
		private List<CacheItem> _cache;

		private int _maxItems;

		private TimeSpan _expiration;

		internal List<CacheItem> Cache => _cache;

		public ContentCache(int maxItems, TimeSpan expiration)
		{
			_cache = new List<CacheItem>();
			_maxItems = maxItems;
			_expiration = expiration;
		}

		public void Add(string id, byte[] data)
		{
			Add(new CacheItem(id, data));
		}

		public CacheItem Item(string id)
		{
			foreach (CacheItem item in _cache)
			{
				if (item.Id == id && DateTime.Now - item.Added < _expiration)
				{
					return item;
				}
			}
			return null;
		}

		public void Add(CacheItem item)
		{
			_cache.Add(item);
			if (_cache.Count > _maxItems)
			{
				_cache.RemoveAt(0);
			}
		}
	}
}
