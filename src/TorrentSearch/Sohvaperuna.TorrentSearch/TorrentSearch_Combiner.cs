using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

namespace Sohvaperuna.TorrentSearch
{
	public class TorrentSearch_Combiner : ITorrentSearch
	{
		private List<ITorrentSearch> _engines;

		private List<TorrentMatchListSort> _sortMethods;

		private string _name;

		public List<ITorrentSearch> Engines => _engines;

		public string Name
		{
			get
			{
				return _name;
			}
			set
			{
				_name = value;
			}
		}

		public string Type => "XML";

		public string Enabled { get; set; }

		public bool IsTVDBIDSearchSupported => false;

		public object Info { get; set; }

		public Hashtable Parameters => new Hashtable();

		public bool IsIMDBIDSearchSupported
		{
			get
			{
				throw new NotImplementedException();
			}
		}

		public HttpWebResponse Login()
		{
			return null;
		}

		public TorrentMatchList SearchByTVDBID(int tvdb, string order)
		{
			return null;
		}

		public string GetContent(string url, Encoding correctencoding, string PostData)
		{
			return "";
		}

		public TorrentMatchList Search(string searchString, string order, object id)
		{
			TorrentMatchListSort torrentMatchListSort = null;
			foreach (TorrentMatchListSort sortMethod in _sortMethods)
			{
				if (sortMethod.Name == order)
				{
					torrentMatchListSort = sortMethod;
					break;
				}
			}
			if (torrentMatchListSort == null)
			{
				if (_sortMethods.Count == 0)
				{
					return new TorrentMatchList(null);
				}
				torrentMatchListSort = _sortMethods[0];
			}
			// 2026: all engines are searched at the same time; a site that fails or times out is skipped
			TorrentMatchList torrentMatchList = new TorrentMatchList(null);
			object sync = new object();
			string iname = torrentMatchListSort.IName;
			System.Threading.Tasks.Task[] tasks = _engines.Select(engine => System.Threading.Tasks.Task.Run(() =>
			{
				try
				{
					TorrentMatchList found = engine.Search(searchString, iname, null);
					if (found != null)
					{
						lock (sync)
						{
							torrentMatchList.AddRange(found);
						}
					}
				}
				catch (Exception ex)
				{
					Log.Instance().Print($"Search on {engine.Name} failed: {ex.Message}");
				}
			})).ToArray();
			System.Threading.Tasks.Task.WaitAll(tasks, TimeSpan.FromSeconds(120));
			lock (sync)
			{
				TorrentMatchList result = new TorrentMatchList(null);
				result.AddRange(torrentMatchList);
				result.Sort(torrentMatchListSort);
				return result;
			}
		}

		public string RawSearch(string searchString, string order, object id)
		{
			return "";
		}

		public List<string> SortMethods()
		{
			List<string> list = new List<string>();
			foreach (TorrentMatchListSort sortMethod in _sortMethods)
			{
				list.Add(sortMethod.Name);
			}
			return list;
		}

		public bool HasOption(string option)
		{
			return false;
		}

		public TorrentSearch_Combiner(string name)
		{
			_name = name;
			_engines = new List<ITorrentSearch>();
			_sortMethods = new List<TorrentMatchListSort>();
		}

		internal void AddSort(string name, string itype, string iname)
		{
			_sortMethods.Add(new TorrentMatchListSort(name, itype, iname));
		}
	}
}
