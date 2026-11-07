using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using MediaPortal.Configuration;
using Sohvaperuna.TorrentSearch;

namespace Sohvaperuna.MyTorrents
{
	/// <summary>2026: the user's own search sites (&lt;sites&gt; in MyTorrents.xml), for the setup windows.</summary>
	public static class OwnSites
	{
		private static string File => Config.GetFile(Config.Dir.Config, "MyTorrents.xml");

		private static XmlDocument Load()
		{
			XmlDocument doc = new XmlDocument();
			try
			{
				if (System.IO.File.Exists(File)) doc.Load(File);
			}
			catch
			{
				doc = new XmlDocument();
			}
			if (doc.SelectSingleNode("config") == null)
			{
				doc.LoadXml("<config><jackett><enabled>true</enabled><url>http://127.0.0.1:9117</url><apikey></apikey></jackett><iconmapping/><feed/><sites/><searchengines/></config>");
			}
			return doc;
		}

		public static List<SimpleSiteSearchProvider> All() => SimpleSiteSearchProvider.LoadAll(Load());

		public static void Add(string name, string searchUrl)
		{
			XmlDocument doc = Load();
			XmlNode root = doc.SelectSingleNode("config");
			XmlNode sites = root.SelectSingleNode("sites");
			if (sites == null)
			{
				sites = doc.CreateElement("sites");
				XmlNode engines = root.SelectSingleNode("searchengines");
				if (engines != null) root.InsertBefore(sites, engines); else root.AppendChild(sites);
			}
			foreach (XmlNode n in sites.SelectNodes("site").Cast<XmlNode>().Where(n => string.Equals(n.SelectSingleNode("url")?.InnerText?.Trim(), searchUrl, StringComparison.OrdinalIgnoreCase)).ToList())
			{
				sites.RemoveChild(n); // no duplicates
			}
			XmlNode site = sites.AppendChild(doc.CreateElement("site"));
			site.AppendChild(doc.CreateElement("name")).InnerText = name;
			site.AppendChild(doc.CreateElement("url")).InnerText = searchUrl;
			site.AppendChild(doc.CreateElement("enabled")).InnerText = "true";
			doc.Save(File);
		}

		public static void Remove(string searchUrl)
		{
			XmlDocument doc = Load();
			XmlNode sites = doc.SelectSingleNode("config/sites");
			if (sites == null) return;
			foreach (XmlNode n in sites.SelectNodes("site").Cast<XmlNode>().Where(n => string.Equals(n.SelectSingleNode("url")?.InnerText?.Trim(), searchUrl, StringComparison.OrdinalIgnoreCase)).ToList())
			{
				sites.RemoveChild(n);
			}
			doc.Save(File);
		}
	}
}
