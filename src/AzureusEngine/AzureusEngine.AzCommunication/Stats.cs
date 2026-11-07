using System.IO;

namespace AzureusEngine.AzCommunication
{
	public class Stats : Singleton
	{
		private double _availabilty;

		private string _status_localised;

		private double _share_ratio;

		private double _health;

		private double _Perc_comp;

		private double _Downloaded_Size;

		private double _Downloaded_avg;

		private string _ETA;

		private string _Status;

		private double _Upload_avg;

		private double _Uploaded;

		public double Availabilty => _availabilty;

		public string Status_Localised => _status_localised;

		public double Share_Ratio => _share_ratio / 100.0;

		public double Health => _health * 100.0;

		public double Completed => _Perc_comp / 10.0;

		public double Downloaded => _Downloaded_Size / 1048576.0;

		public double DownloadAverage => _Downloaded_avg;

		public string ETA => _ETA;

		public string Status => _Status;

		public double UploadAverage => _Upload_avg;

		public double Uploaded => _Uploaded / 1048576.0;

		internal Stats(Stream stream)
			: base(stream)
		{
			_availabilty = GetDoubleValue("//availability");
			_status_localised = GetStringValue("//status_localised");
			_share_ratio = GetDoubleValue("//share_ratio");
			_health = GetDoubleValue("//health");
			_Perc_comp = GetDoubleValue("//completed");
			_Downloaded_avg = GetDoubleValue("//download_average");
			_Downloaded_Size = GetDoubleValue("//downloaded");
			_ETA = GetStringValue("//eta");
			_Status = GetStringValue("//status");
			_Upload_avg = GetDoubleValue("//upload_average");
			_Uploaded = GetDoubleValue("//uploaded");
		}
	}
}
