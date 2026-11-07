using System;

namespace Sohvaperuna.TorrentSearch
{
	public class TorrentMatch
	{
		private string _category;

		private string _subCategory;

		private string _id;

		private string _url;

		private string _title;

		private long _size;

		private int _seed;

		private int _leech;

		private DateTime _date;

		private string _cookie;

		private string _tracker;

		public string Category
		{
			get
			{
				return _category;
			}
			set
			{
				_category = value;
			}
		}

		public string SubCategory
		{
			get
			{
				return _subCategory;
			}
			set
			{
				_subCategory = value;
			}
		}

		public string Id
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

		public string Url
		{
			get
			{
				return _url;
			}
			set
			{
				_url = value;
			}
		}

		public string Title
		{
			get
			{
				return _title;
			}
			set
			{
				_title = value;
			}
		}

		public long Size
		{
			get
			{
				return _size;
			}
			set
			{
				_size = value;
			}
		}

		public int Seed
		{
			get
			{
				return _seed;
			}
			set
			{
				_seed = value;
			}
		}

		public int Leech
		{
			get
			{
				return _leech;
			}
			set
			{
				_leech = value;
			}
		}

		public DateTime Date
		{
			get
			{
				return _date;
			}
			set
			{
				_date = value;
			}
		}

		public string cookie
		{
			get
			{
				return _cookie;
			}
			set
			{
				_cookie = value;
			}
		}

		public string tracker
		{
			get
			{
				return _tracker;
			}
			set
			{
				_tracker = value;
			}
		}

		public string Icon { get; set; }

		public string imdb { get; set; }

		public string tvdb { get; set; }

		public string magnet { get; set; }
	}
}
