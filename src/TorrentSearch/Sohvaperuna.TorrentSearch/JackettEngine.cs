using System.Collections.Generic;

namespace Sohvaperuna.TorrentSearch
{
	public class JackettEngine
	{
		public string id { get; set; }

		public string name { get; set; }

		public string description { get; set; }

		public string type { get; set; }

		public bool configured { get; set; }

		public string site_link { get; set; }

		public IEnumerable<string> alternativesitelinks { get; set; }

		public string language { get; set; }

		public string last_error { get; set; }

		public bool potatoenabled { get; set; }

		public List<Category> caps { get; set; }
	}
}
