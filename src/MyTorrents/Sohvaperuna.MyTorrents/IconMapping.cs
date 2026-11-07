using System;
using System.Collections.Generic;
using System.Xml;

namespace Sohvaperuna.MyTorrents
{
	public class IconMapping
	{
		private List<IconMap> _iconMap;

		public IconMapping()
		{
			_iconMap = new List<IconMap>();
		}

		public void Add(string name, string icon)
		{
			_iconMap.Add(new IconMap(name, icon));
		}

		public bool LoadIconMappings(ConfigData _config)
		{
			XmlDocument myTorrentsConfiguration = _config.MyTorrentsConfiguration;
			IconMapping iconMapping = new IconMapping();
			foreach (XmlNode item in myTorrentsConfiguration.SelectNodes("config/iconmapping/map"))
			{
				XmlNode xmlNode2 = item.SelectSingleNode("key");
				XmlNode xmlNode3 = item.SelectSingleNode("icon");
				if (xmlNode2 != null && xmlNode3 != null)
				{
					iconMapping.Add(xmlNode2.InnerText, xmlNode3.InnerText);
				}
			}
			Configuration.Instance().Settings["MyTorrents.GlobalIconMapping"] = iconMapping;
			Log.Instance().Print("Loaded Icon Mappings");
			return true;
		}

		public List<string> Keys()
		{
			List<string> list = new List<string>();
			foreach (IconMap item in _iconMap)
			{
				list.Add(item.Key);
			}
			return list;
		}

		public string IconByName(string name)
		{
			foreach (IconMap item in _iconMap)
			{
				if (item.Key == name)
				{
					return item.Icon;
				}
			}
			return "";
		}

		public string GetIcon(string p)
		{
			foreach (IconMap item in _iconMap)
			{
				int num = p.IndexOf(item.Key, StringComparison.CurrentCultureIgnoreCase);
				if (num != -1)
				{
					return item.Icon;
				}
			}
			return "";
		}
	}
}
