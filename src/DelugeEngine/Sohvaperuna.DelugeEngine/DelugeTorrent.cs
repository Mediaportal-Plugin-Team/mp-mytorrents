namespace Sohvaperuna.DelugeEngine
{
	public class DelugeTorrent
	{
		public string hash { get; set; }

		public double max_download_speed { get; set; }

		public double upload_payload_rate { get; set; }

		public double download_payload_rate { get; set; }

		public int num_peers { get; set; }

		public double ratio { get; set; }

		public int total_peers { get; set; }

		public long total_size { get; set; }

		public double max_upload_speed { get; set; }

		public string label { get; set; }

		public string state { get; set; }

		public double distributed_copies { get; set; }

		public string save_path { get; set; }

		public double progress { get; set; }

		public double time_added { get; set; }

		public string tracker_host { get; set; }

		public long total_uploaded { get; set; }

		public long total_done { get; set; }

		public int total_seeds { get; set; }

		public double seeds_peers_ratio { get; set; }

		public int num_seeds { get; set; }

		public string name { get; set; }

		public bool is_auto_managed { get; set; }

		public double queue { get; set; }

		public double eta { get; set; }
	}
}
