using System;
using System.Collections.Generic;

namespace Sohvaperuna.TorrentSearch
{
	public class JackettResult
	{
		public string Title { get; set; }

		public string Tracker { get; set; }

		public Uri Guid { get; set; }

		public Uri Link { get; set; }

		public Uri Comments { get; set; }

		public DateTime PublishDate { get; set; }

		public ICollection<int> Category { get; set; }

		public long? Size { get; set; }

		public long? Files { get; set; }

		public long? Grabs { get; set; }

		public string Description { get; set; }

		public long? RageID { get; set; }

		public long? TVDBId { get; set; }

		public long? Imdb { get; set; }

		public long? TMDb { get; set; }

		public int? Seeders { get; set; }

		public int? Peers { get; set; }

		public Uri BannerUrl { get; set; }

		public string InfoHash { get; set; }

		public Uri MagnetUri { get; set; }

		public double? MinimumRatio { get; set; }

		public long? MinimumSeedTime { get; set; }

		public double? DownloadVolumeFactor { get; set; }

		public double? UploadVolumeFactor { get; set; }
	}
}
