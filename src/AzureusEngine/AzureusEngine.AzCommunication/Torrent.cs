using System.IO;

namespace AzureusEngine.AzCommunication
{
	public class Torrent : Singleton
	{
		private string _hash;

		private double _size;

		private string _name;

		public string Hash => _hash;

		public double Size => _size / 1048576.0;

		public string Name => _name;

		internal Torrent(Stream stream)
			: base(stream)
		{
			_name = GetStringValue("//name");
			_hash = GetStringValue("//hash");
			_size = GetDoubleValue("//size");
		}
	}
}
