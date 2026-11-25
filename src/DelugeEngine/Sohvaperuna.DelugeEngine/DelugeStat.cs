namespace Sohvaperuna.DelugeEngine
{
	public class DelugeStat
	{
		public long upload_protocol_rate { get; set; }

		public double max_upload { get; set; }

		public long download_protocol_rate { get; set; }

		public int download_rate { get; set; }

		public bool has_incoming_connections { get; set; }

		public int num_connections { get; set; }

		public int upload_rate { get; set; }

		public int dht_nodes { get; set; }

		public long free_space { get; set; }

		public int max_num_connections { get; set; }

		public double max_download { get; set; }
	}
}
