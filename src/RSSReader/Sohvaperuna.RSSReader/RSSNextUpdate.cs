namespace Sohvaperuna.RSSReader
{
	public class RSSNextUpdate
	{
		public string Name { get; set; }

		public string NameFull { get; set; }

		public string Url { get; set; }

		public int Quality { get; set; }

		public int Codec { get; set; }

		public int Timestamp { get; set; }

		public int Season { get; set; }

		public int Episode { get; set; }

		public int EpisodeTo { get; set; }

		public int FeedId { get; set; }

		public bool Repack { get; set; }

		public bool InHistory { get; set; }
	}
}
