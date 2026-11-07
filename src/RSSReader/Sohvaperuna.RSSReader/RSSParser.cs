using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml;

namespace Sohvaperuna.RSSReader
{
	public class RSSParser
	{
		public static string GetLinkType(string url)
		{
			if (url.StartsWith("magnet"))
			{
				return "magnet";
			}
			return "torrent";
		}

		internal static void Parse(ref IRSSChannel channel, Stream stream, string url, Regex ToTransform, string baseDownload)
		{
			RSSItemList rSSItemList = new RSSItemList();
			XmlDocument xmlDocument = new XmlDocument();
			xmlDocument.Load(stream);
			XmlNode xmlNode = xmlDocument.SelectSingleNode("/rss/channel/title");
			channel.Title = ((xmlNode != null) ? xmlNode.InnerText : "");
			xmlNode = xmlDocument.SelectSingleNode("/rss/channel/link");
			channel.Link = ((xmlNode != null) ? xmlNode.InnerText : "");
			xmlNode = xmlDocument.SelectSingleNode("/rss/channel/description");
			channel.Description = ((xmlNode != null) ? xmlNode.InnerText : "");
			xmlNode = xmlDocument.SelectSingleNode("/rss/channel/language");
			channel.Language = ((xmlNode != null) ? xmlNode.InnerText : "");
			XmlNodeList xmlNodeList = xmlDocument.SelectNodes("/rss/channel/item");
			foreach (XmlNode item in xmlNodeList)
			{
				RSSItem rSSItem = new RSSItem();
				xmlNode = item.SelectSingleNode("title");
				rSSItem.Title = ((xmlNode != null) ? xmlNode.InnerText : "");
				xmlNode = item.SelectSingleNode("link");
				if (xmlNode != null)
				{
					string innerText = xmlNode.InnerText;
					Match match = ToTransform.Match(innerText);
					string value = match.Groups["TID"].Value;
					if (baseDownload.EndsWith("/"))
					{
						rSSItem.Link = baseDownload + value;
					}
					else
					{
						rSSItem.Link = baseDownload + "/" + value;
					}
				}
				xmlNode = item.SelectSingleNode("description");
				rSSItem.Description = ((xmlNode != null) ? xmlNode.InnerText : "");
				xmlNode = item.SelectSingleNode("author");
				rSSItem.Author = ((xmlNode != null) ? xmlNode.InnerText : "");
				xmlNode = item.SelectSingleNode("category");
				rSSItem.Category = ((xmlNode != null) ? xmlNode.InnerText : "");
				xmlNode = item.SelectSingleNode("comments");
				rSSItem.Commments = ((xmlNode != null) ? xmlNode.InnerText : "");
				xmlNode = item.SelectSingleNode("enclosure");
				rSSItem.Enclosure = ((xmlNode != null) ? xmlNode.InnerText : "");
				xmlNode = item.SelectSingleNode("guid");
				rSSItem.Guid = ((xmlNode != null) ? xmlNode.InnerText : "");
				xmlNode = item.SelectSingleNode("pubDate");
				if (xmlNode == null)
				{
					rSSItem.PublishDate = new DateTime(0L);
				}
				else
				{
					rSSItem.PublishDate = Convert.ToDateTime(xmlNode.InnerText).ToUniversalTime() + DateTimeOffset.Now.Offset;
				}
				xmlNode = item.SelectSingleNode("source");
				rSSItem.Source = ((xmlNode != null) ? xmlNode.InnerText : "");
				rSSItemList.Add(rSSItem);
			}
			channel.Items = rSSItemList;
		}

		internal static void ParseAtom(ref IRSSChannel channel, Stream stream, string url)
		{
			RSSItemList rSSItemList = new RSSItemList();
			XmlDocument xmlDocument = new XmlDocument();
			XmlNamespaceManager xmlNamespaceManager = new XmlNamespaceManager(xmlDocument.NameTable);
			xmlNamespaceManager.AddNamespace("atom", "http://www.w3.org/2005/Atom");
			xmlDocument.Load(stream);
			XmlNode xmlNode = xmlDocument.SelectSingleNode("/atom:feed/atom:title", xmlNamespaceManager);
			channel.Title = ((xmlNode != null) ? xmlNode.InnerText : "");
			xmlNode = xmlDocument.SelectSingleNode("/atom:feed/atom:link", xmlNamespaceManager);
			channel.Link = ((xmlNode != null) ? xmlNode.InnerText : "");
			xmlNode = xmlDocument.SelectSingleNode("/atom:feed/atom:subtitle", xmlNamespaceManager);
			channel.Description = ((xmlNode != null) ? xmlNode.InnerText : "");
			channel.Language = null;
			XmlNodeList xmlNodeList = xmlDocument.SelectNodes("/atom:feed/atom:entry", xmlNamespaceManager);
			foreach (XmlNode item in xmlNodeList)
			{
				RSSItem rSSItem = new RSSItem();
				xmlNode = item.SelectSingleNode("atom:title", xmlNamespaceManager);
				rSSItem.Title = ((xmlNode != null) ? xmlNode.InnerText : "");
				xmlNode = item.SelectSingleNode("atom:link", xmlNamespaceManager);
				rSSItem.Link = ((xmlNode != null) ? xmlNode.InnerText : "");
				xmlNode = item.SelectSingleNode("atom:summary", xmlNamespaceManager);
				rSSItem.Description = ((xmlNode != null) ? xmlNode.InnerText : "");
				xmlNode = item.SelectSingleNode("atom:author", xmlNamespaceManager);
				rSSItem.Author = ((xmlNode != null) ? xmlNode.InnerText : "");
				xmlNode = item.SelectSingleNode("atom:category", xmlNamespaceManager);
				rSSItem.Category = ((xmlNode != null) ? xmlNode.InnerText : "");
				rSSItem.Commments = null;
				rSSItem.Enclosure = null;
				xmlNode = item.SelectSingleNode("atom:id", xmlNamespaceManager);
				rSSItem.Guid = ((xmlNode != null) ? xmlNode.InnerText : "");
				xmlNode = item.SelectSingleNode("atom:updated", xmlNamespaceManager);
				if (xmlNode == null)
				{
					rSSItem.PublishDate = new DateTime(0L);
				}
				else
				{
					rSSItem.PublishDate = XmlConvert.ToDateTime(xmlNode.InnerText).ToUniversalTime() + DateTimeOffset.Now.Offset;
				}
				xmlNode = item.SelectSingleNode("atom:source", xmlNamespaceManager);
				rSSItem.Source = ((xmlNode != null) ? xmlNode.InnerText : "");
				rSSItemList.Add(rSSItem);
			}
			channel.Items = rSSItemList;
		}

		internal static void Parse(ref IRSSChannel channel, Stream stream, string url)
		{
			RSSItemList rSSItemList = new RSSItemList();
			XmlDocument xmlDocument = new XmlDocument();
			xmlDocument.Load(stream);
			XmlNode xmlNode = xmlDocument.SelectSingleNode("/rss/channel/title");
			channel.Title = ((xmlNode != null) ? xmlNode.InnerText : "");
			xmlNode = xmlDocument.SelectSingleNode("/rss/channel/link");
			channel.Link = ((xmlNode != null) ? xmlNode.InnerText : "");
			xmlNode = xmlDocument.SelectSingleNode("/rss/channel/description");
			channel.Description = ((xmlNode != null) ? xmlNode.InnerText : "");
			xmlNode = xmlDocument.SelectSingleNode("/rss/channel/language");
			channel.Language = ((xmlNode != null) ? xmlNode.InnerText : "");
			XmlNodeList xmlNodeList = xmlDocument.SelectNodes("/rss/channel/item");
			foreach (XmlNode item in xmlNodeList)
			{
				RSSItem rSSItem = new RSSItem();
				xmlNode = item.SelectSingleNode("title");
				rSSItem.Title = ((xmlNode != null) ? xmlNode.InnerText : "");
				xmlNode = item.SelectSingleNode("link");
				rSSItem.Link = ((xmlNode != null) ? xmlNode.InnerText : "");
				xmlNode = item.SelectSingleNode("description");
				rSSItem.Description = ((xmlNode != null) ? xmlNode.InnerText : "");
				xmlNode = item.SelectSingleNode("author");
				rSSItem.Author = ((xmlNode != null) ? xmlNode.InnerText : "");
				xmlNode = item.SelectSingleNode("category");
				rSSItem.Category = ((xmlNode != null) ? xmlNode.InnerText : "");
				xmlNode = item.SelectSingleNode("comments");
				rSSItem.Commments = ((xmlNode != null) ? xmlNode.InnerText : "");
				xmlNode = item.SelectSingleNode("enclosure");
				if (xmlNode != null)
				{
					rSSItem.Enclosure = ((xmlNode != null) ? xmlNode.Attributes["url"].InnerText : "");
				}
				xmlNode = item.SelectSingleNode("guid");
				rSSItem.Guid = ((xmlNode != null) ? xmlNode.InnerText : "");
				xmlNode = item.SelectSingleNode("pubDate");
				if (xmlNode == null)
				{
					rSSItem.PublishDate = new DateTime(0L);
				}
				else
				{
					rSSItem.PublishDate = Convert.ToDateTime(xmlNode.InnerText).ToUniversalTime() + DateTimeOffset.Now.Offset;
				}
				xmlNode = item.SelectSingleNode("source");
				rSSItem.Source = ((xmlNode != null) ? xmlNode.InnerText : "");
				rSSItemList.Add(rSSItem);
			}
			channel.Items = rSSItemList;
		}
	}
}
