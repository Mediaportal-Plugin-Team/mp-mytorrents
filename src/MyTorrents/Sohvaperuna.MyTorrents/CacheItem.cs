using System;

namespace Sohvaperuna.MyTorrents
{
	internal class CacheItem
	{
		private string _id;

		private byte[] _data;

		private DateTime _added;

		public DateTime Added => _added;

		public string Id => _id;

		public byte[] Data => _data;

		public CacheItem(string id, byte[] data)
		{
			_id = id;
			_data = data;
			_added = DateTime.Now;
		}
	}
}
