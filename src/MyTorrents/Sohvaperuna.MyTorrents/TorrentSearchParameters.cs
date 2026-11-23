namespace Sohvaperuna.MyTorrents
{
	internal class TorrentSearchParameters
	{
		private string _searchEngine;

		private string _searchString;

		private string _sortOrder;

		private object _id;

		public string SearchEngine
		{
			get
			{
				return _searchEngine;
			}
			set
			{
				_searchEngine = value;
			}
		}

		public string SearchString
		{
			get
			{
				return _searchString;
			}
			set
			{
				_searchString = value;
			}
		}

		public string SortOrder
		{
			get
			{
				return _sortOrder;
			}
			set
			{
				_sortOrder = value;
			}
		}

		public object Id
		{
			get
			{
				return _id;
			}
			set
			{
				_id = value;
			}
		}

		/// TV / Movie / "" (anything) - see Sohvaperuna.TorrentSearch.SearchContext
		public string Kind { get; set; } = "";

		public TorrentSearchParameters(string searchEngine, string searchString, string sortOrder, object id)
		{
			_searchEngine = searchEngine;
			_searchString = searchString;
			_sortOrder = sortOrder;
			_id = id;
		}
	}
}
