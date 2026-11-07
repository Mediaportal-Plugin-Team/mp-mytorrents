namespace Sohvaperuna.TorrentSearch
{
	public class PostData
	{
		public string username { get; set; }

		public string passkey { get; set; }

		public string search { get; set; }

		public int[] category { get; set; }

		public int[] codec { get; set; }

		public int[] medium { get; set; }

		public IMDBObject imdb { get; set; }

		public TVDBObject tvdb { get; set; }
	}
}
