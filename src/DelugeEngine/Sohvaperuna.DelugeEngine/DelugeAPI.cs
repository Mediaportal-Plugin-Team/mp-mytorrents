using System.Collections.Generic;
using System.Web.Script.Serialization;

namespace Sohvaperuna.DelugeEngine
{
	internal class DelugeAPI
	{
		private static JavaScriptSerializer js = new JavaScriptSerializer();

		public static string[] AuthParams = new string[1] { "deluge" };

		public static string[] TorrentParams = new string[24]
		{
			"queue", "name", "total_size", "state", "progress", "num_seeds", "total_seeds", "num_peers", "total_peers", "download_payload_rate",
			"upload_payload_rate", "eta", "ratio", "distributed_copies", "is_auto_managed", "time_added", "tracker_host", "save_path", "total_done", "total_uploaded",
			"max_download_speed", "max_upload_speed", "seeds_peers_ratio", "label"
		};

		public static Dictionary<string, object> FilterDict = new Dictionary<string, object>();

		public static object[] UpdateParams = new object[2] { TorrentParams, FilterDict };

		private static void Main(string[] args)
		{
		}
	}
}
