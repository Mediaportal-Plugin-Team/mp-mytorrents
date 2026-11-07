using System;

namespace Sohvaperuna.RSSReader
{
	public class RSSItem
	{
		private string _source;

		private DateTime _publishDate;

		private string _guid;

		private string _enclosure;

		private string _commments;

		private string _category;

		private string _author;

		private string _description;

		private string _link;

		private string _title;

		public string Source
		{
			get
			{
				return _source;
			}
			set
			{
				_source = value;
			}
		}

		public DateTime PublishDate
		{
			get
			{
				return _publishDate;
			}
			set
			{
				_publishDate = value;
			}
		}

		public string Guid
		{
			get
			{
				return _guid;
			}
			set
			{
				_guid = value;
			}
		}

		public string Enclosure
		{
			get
			{
				return _enclosure;
			}
			set
			{
				_enclosure = value;
			}
		}

		public string Commments
		{
			get
			{
				return _commments;
			}
			set
			{
				_commments = value;
			}
		}

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

		public string Author
		{
			get
			{
				return _author;
			}
			set
			{
				_author = value;
			}
		}

		public string Description
		{
			get
			{
				return _description;
			}
			set
			{
				_description = value;
			}
		}

		public string Link
		{
			get
			{
				return _link;
			}
			set
			{
				_link = value;
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

		public string Magnet { get; set; }
	}
}
