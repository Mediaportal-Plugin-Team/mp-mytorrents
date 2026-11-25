namespace Sohvaperuna.TorrentEngine
{
	public class TorrentLabel
	{
		private string _name;

		private int _count;

		public string Name
		{
			get
			{
				return _name;
			}
			set
			{
				_name = value;
			}
		}

		public int Count
		{
			get
			{
				return _count;
			}
			set
			{
				_count = value;
			}
		}
	}
}
