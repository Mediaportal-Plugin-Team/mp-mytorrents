using System.IO;

namespace AzureusEngine.AzCommunication
{
	public class Scrape_Result : Singleton
	{
		private int _non_seed_count;

		private int _seed_count;

		public int Seeders => _seed_count;

		public int Leechers => _non_seed_count;

		internal Scrape_Result(Stream stream)
			: base(stream)
		{
			_non_seed_count = GetIntValue("//non_seed_count");
			_seed_count = GetIntValue("//seed_count");
		}
	}
}
