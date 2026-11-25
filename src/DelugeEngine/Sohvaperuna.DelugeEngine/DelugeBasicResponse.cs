namespace Sohvaperuna.DelugeEngine
{
	public class DelugeBasicResponse
	{
		public int id { get; set; }

		public object result { get; set; }

		public DelugeError error { get; set; }
	}
}
