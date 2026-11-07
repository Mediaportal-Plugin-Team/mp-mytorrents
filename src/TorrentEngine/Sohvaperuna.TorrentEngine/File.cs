namespace Sohvaperuna.TorrentEngine
{
	public class File
	{
		private ITorrentClient _client;

		public string build { get; set; }

		public object[] files { get; set; }

		public string Filename { get; set; }

		public long Filesize { get; set; }

		public long Downloaded { get; set; }

		public int Priority { get; set; }

		public int FirstPiece { get; set; }

		public int NumPieces { get; set; }

		public bool Streamable { get; set; }

		public int EncodedRate { get; set; }

		public int Duration { get; set; }

		public int Width { get; set; }

		public int Height { get; set; }

		public int StreamETA { get; set; }

		public long Streamability { get; set; }

		public File(ITorrentClient client)
		{
			_client = client;
		}

		public bool SetFilePriority(string hash, int index, int priority)
		{
			return _client.SetPriority(hash, index, 1);
		}
	}
}
