using System;
using System.Net;
using System.Text.RegularExpressions;

namespace Sohvaperuna.RSSReader
{
	public class RSSChannel : IRSSChannel, IChannel
	{
		private string _title;

		private string _language;

		private string _description;

		private string _link;

		private RSSItemList _itemList;

		private DateTime _updated;

		private string _url;

		private TimeSpan _expireTime;

		private Regex _transform = null;

		private string _basedllink = null;

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

		public Regex Transform
		{
			get
			{
				return _transform;
			}
			set
			{
				_transform = value;
			}
		}

		public string BaseDownloadLink
		{
			get
			{
				return _basedllink;
			}
			set
			{
				_basedllink = value;
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

		public string Language
		{
			get
			{
				return _language;
			}
			set
			{
				_language = value;
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

		public RSSItemList Items
		{
			get
			{
				return _itemList;
			}
			set
			{
				_itemList = value;
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

		public RSSChannel(string url)
		{
			_itemList = new RSSItemList();
			_url = url;
		}

		public RSSChannel(string url, Regex Transform, string BaseDownload)
		{
			_itemList = new RSSItemList();
			_url = url;
			_transform = Transform;
			_basedllink = BaseDownload;
		}

		public bool Expired()
		{
			TimeSpan timeSpan = DateTime.Now - Updated;
			return timeSpan > ExpireTime;
		}

		public void Reset()
		{
			_title = (_language = (_description = (_link = "")));
			_updated = new DateTime(0L);
			_itemList = new RSSItemList();
		}

		public void Update()
		{
			try
			{
				WebRequest webRequest = WebRequest.Create(Url);
				webRequest.Proxy = new WebProxy("86.105.51.105", 3128);
				using (WebResponse webResponse = webRequest.GetResponse())
				{
					IRSSChannel channel = this;
					if (_transform != null)
					{
						RSSParser.Parse(ref channel, webResponse.GetResponseStream(), Url, _transform, _basedllink);
					}
					else
					{
						if (channel.Type == "rss")
						{
							RSSParser.Parse(ref channel, webResponse.GetResponseStream(), Url);
						}
						if (channel.Type == "atom")
						{
							RSSParser.ParseAtom(ref channel, webResponse.GetResponseStream(), Url);
						}
					}
					_updated = DateTime.Now;
				}
			}
			catch (Exception ex)
			{
				Title = "Connection failed.";
				Description = ex.Message;
			}
		}
	}
}
