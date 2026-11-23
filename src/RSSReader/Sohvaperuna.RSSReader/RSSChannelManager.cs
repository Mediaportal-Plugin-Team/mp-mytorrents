using System;
using System.Collections.Generic;

namespace Sohvaperuna.RSSReader
{
	public class RSSChannelManager
	{
		private static RSSChannelManager _instance = null;

		private List<IRSSChannel> _cached;

		public List<IRSSChannel> Channels => _cached;

		public event RSSChannelUpdated RSSChannelUpdated;

		public event RSSChannelAdded RSSChannelAdded;

		internal RSSChannelManager()
		{
			_cached = new List<IRSSChannel>();
		}

		public static RSSChannelManager Instance()
		{
			if (_instance == null)
			{
				_instance = new RSSChannelManager();
			}
			return _instance;
		}

		public IRSSChannel Channel(string url)
		{
			foreach (IRSSChannel item in _cached)
			{
				if (item.Url.CompareTo(url) == 0)
				{
					return item;
				}
			}
			return null;
		}

		internal void Add(IRSSChannel channel)
		{
			foreach (IRSSChannel item in _cached)
			{
				if (item.Url.CompareTo(channel.Url) == 0)
				{
					_cached.Remove(item);
				}
			}
			_cached.Add(channel);
			if (this.RSSChannelAdded != null)
			{
				this.RSSChannelAdded(channel);
			}
		}

		internal void ChannelUpdated(IRSSChannel channel)
		{
			if (this.RSSChannelUpdated != null)
			{
				this.RSSChannelUpdated(channel);
			}
		}

		private void ChannelUpdated(IAsyncResult ar)
		{
			if (ar.AsyncState is ChannelUpdate channelUpdate)
			{
				channelUpdate.EndInvoke(ar);
				try
				{
					ChannelUpdated(channelUpdate.Target as IRSSChannel);
				}
				catch (Exception)
				{
				}
			}
		}

		public void UpdateChannels(bool force)
		{
			foreach (IRSSChannel channel in Channels)
			{
				if (force || channel.Expired())
				{
					ChannelUpdate channelUpdate = channel.Update;
					channelUpdate.BeginInvoke(ChannelUpdated, channelUpdate);
				}
			}
		}
	}
}
