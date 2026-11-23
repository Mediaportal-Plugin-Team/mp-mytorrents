using System;

namespace Sohvaperuna.MyTorrents
{
	public class WatchItem
	{
		private string _searchTerm;

		private string _label;

		private DateTime _added;

		private DateTime _lastCheck;

		private DateTime _nextCheck;

		public DateTime Added
		{
			get
			{
				return _added;
			}
			set
			{
				_added = value;
			}
		}

		public DateTime LastCheck
		{
			get
			{
				return _lastCheck;
			}
			set
			{
				_lastCheck = value;
			}
		}

		public DateTime NextCheck
		{
			get
			{
				return _nextCheck;
			}
			set
			{
				_nextCheck = value;
			}
		}

		public string SearchTerm
		{
			get
			{
				return _searchTerm;
			}
			set
			{
				_searchTerm = value;
			}
		}

		public string Label
		{
			get
			{
				return _label;
			}
			set
			{
				_label = value;
			}
		}

		public virtual string GetLabel1()
		{
			return SearchTerm;
		}

		public virtual string GetLabel2()
		{
			return Added.ToShortDateString();
		}

		public virtual void DoSearch()
		{
		}
	}
}
