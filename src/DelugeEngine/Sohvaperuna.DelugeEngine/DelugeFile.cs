namespace Sohvaperuna.DelugeEngine
{
	public class DelugeFile
	{
		public string type { get; set; }

		public int priority { get; set; }

		public string path { get; set; }

		public double progress { get; set; }

		public double[] progresses { get; set; }

		public int? index { get; set; }

		public long? offset { get; set; }

		public object contents { get; set; }

		public long size { get; set; }
	}
}
