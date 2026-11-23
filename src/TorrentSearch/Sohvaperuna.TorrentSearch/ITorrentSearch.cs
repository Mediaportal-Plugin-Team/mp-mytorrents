using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace Sohvaperuna.TorrentSearch
{
	public interface ITorrentSearch
	{
		Hashtable Parameters { get; }

		string Name { get; set; }

		string Type { get; }

		string Enabled { get; set; }

		bool IsTVDBIDSearchSupported { get; }

		bool IsIMDBIDSearchSupported { get; }

		object Info { get; set; }

		TorrentMatchList Search(string searchString, string order, object id);

		TorrentMatchList SearchByTVDBID(int tvdbID, string order);

		string RawSearch(string searchString, string order, object id);

		List<string> SortMethods();

		bool HasOption(string option);

		HttpWebResponse Login();

		string GetContent(string url, Encoding correctencoding, string PostData);
	}
}
