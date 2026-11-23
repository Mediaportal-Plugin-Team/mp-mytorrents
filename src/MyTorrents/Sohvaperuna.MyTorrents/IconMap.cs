namespace Sohvaperuna.MyTorrents
{
	internal class IconMap
	{
		private string _key;

		private string _icon;

		public string Key => _key;

		public string Icon => _icon;

		public IconMap(string key, string icon)
		{
			_key = key;
			_icon = icon;
		}
	}
}
