namespace Sohvaperuna.TorrentSearch
{
	public class HDBTorrent
	{
		public string id { get; set; }

		public string leechers { get; set; }

		public string seeders { get; set; }

		public string name { get; set; }

		public string times_completed { get; set; }

		public string size { get; set; }

		public string utadded { get; set; }

		public string comments { get; set; }

		public string numfiles { get; set; }

		public string filename { get; set; }

		public string owner { get; set; }

		public string freelech { get; set; }

		public IMDBObject imdb { get; set; }

		public TVDBObject tvdb { get; set; }

		public string type_category { get; set; }

		public string type_codec { get; set; }

		public string type_medium { get; set; }

		public string username { get; set; }
	}
}
