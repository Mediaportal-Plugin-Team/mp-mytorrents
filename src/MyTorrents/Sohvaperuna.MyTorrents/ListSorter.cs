using System;
using System.Collections;
using System.Collections.Generic;
using System.Windows.Forms;

namespace Sohvaperuna.MyTorrents
{
	/// <summary>
	/// 2026 (1.3.38): click a column heading to sort by it, click again to reverse. A small arrow shows the sorted
	/// column, and the choice is remembered (MyTorrents settings, "Sort.&lt;name&gt;").
	/// Each column can give its own sort key (a date, a number ...); otherwise the text is compared.
	/// </summary>
	public sealed class ListSorter : IComparer
	{
		private readonly ListView _list;
		private readonly string _name;
		private readonly Dictionary<int, Func<ListViewItem, IComparable>> _keys = new Dictionary<int, Func<ListViewItem, IComparable>>();
		private readonly List<string> _titles = new List<string>();
		private int _column = -1;
		private bool _ascending = true;

		public ListSorter(ListView list, string name)
		{
			_list = list;
			_name = name;
			_list.ColumnClick += (s, e) =>
			{
				if (e.Column == _column) _ascending = !_ascending;
				else
				{
					_column = e.Column;
					_ascending = true;
				}
				Save();
				Apply();
			};
			try
			{
				string saved = Configuration.GetExtra("Sort." + _name, "");
				string[] p = saved.Split('|');
				int c;
				if (p.Length == 2 && int.TryParse(p[0], out c))
				{
					_column = c;
					_ascending = p[1] != "desc";
				}
			}
			catch
			{
			}
		}

		/// A sort key for one column (e.g. the real date behind "Thu 21 Jan").
		public ListSorter Key(int column, Func<ListViewItem, IComparable> key)
		{
			_keys[column] = key;
			return this;
		}

		/// Sorts the list as chosen (call after filling it). Without a choice the list keeps its own order.
		public void Apply()
		{
			if (_titles.Count != _list.Columns.Count)
			{
				_titles.Clear();
				foreach (ColumnHeader h in _list.Columns) _titles.Add(h.Text);
			}
			for (int i = 0; i < _list.Columns.Count; i++)
			{
				_list.Columns[i].Text = _titles[i] + (i == _column ? (_ascending ? "  ▲" : "  ▼") : "");
			}
			if (_column < 0 || _column >= _list.Columns.Count) return;
			_list.ListViewItemSorter = this;
			_list.Sort();
			_list.ListViewItemSorter = null; // so adding items later doesn't re-sort them one by one
		}

		private void Save()
		{
			try
			{
				Configuration.SetExtra("Sort." + _name, _column + "|" + (_ascending ? "asc" : "desc"));
			}
			catch
			{
			}
		}

		public int Compare(object x, object y)
		{
			ListViewItem a = x as ListViewItem, b = y as ListViewItem;
			if (a == null || b == null) return 0;
			int r;
			Func<ListViewItem, IComparable> key;
			if (_keys.TryGetValue(_column, out key))
			{
				IComparable ka = key(a), kb = key(b);
				if (ka == null && kb == null) r = 0;
				else if (ka == null) return 1; // nothing to sort by: always at the end
				else if (kb == null) return -1;
				else r = ka.CompareTo(kb);
			}
			else
			{
				string ta = _column < a.SubItems.Count ? a.SubItems[_column].Text : "";
				string tb = _column < b.SubItems.Count ? b.SubItems[_column].Text : "";
				bool ea = ta.Length == 0 || ta == "-", eb = tb.Length == 0 || tb == "-";
				if (ea && eb) r = 0;
				else if (ea) return 1;
				else if (eb) return -1;
				else r = string.Compare(ta, tb, StringComparison.CurrentCultureIgnoreCase);
			}
			if (r == 0) r = string.Compare(a.Text, b.Text, StringComparison.CurrentCultureIgnoreCase);
			return _ascending ? r : -r;
		}
	}
}
