using System;
using System.IO;
using System.Net;
using System.Text;
using System.Xml;

namespace AzureusEngine.AzCommunication
{
	public class TorrentManager : Singleton
	{
		internal static string TA_CATEGORY = "";

		private Az_Protocol prot;

		internal TorrentManager(Stream stream, Az_Protocol prot)
			: base(stream)
		{
			this.prot = prot;
			CategoryAttributeID();
		}

		internal void CategoryAttributeID()
		{
			HttpWebRequest httpWebRequest = prot.CreateRequest();
			Stream requestStream = httpWebRequest.GetRequestStream();
			XmlTextWriter xmlTextWriter = new XmlTextWriter(requestStream, Encoding.GetEncoding("ISO-8859-1"));
			xmlTextWriter.WriteStartDocument();
			xmlTextWriter.WriteStartElement("REQUEST");
			xmlTextWriter.WriteStartElement("OBJECT");
			xmlTextWriter.WriteElementString("_object_id", base.objID);
			xmlTextWriter.WriteEndElement();
			xmlTextWriter.WriteElementString("METHOD", "getAttribute[String]");
			xmlTextWriter.WriteStartElement("PARAMS");
			xmlTextWriter.WriteElementString("ENTRY", "Category");
			xmlTextWriter.WriteEndElement();
			xmlTextWriter.WriteElementString("CONNECTION_ID", prot.ConnectionID);
			xmlTextWriter.WriteElementString("REQUEST_ID", prot.IDGenerator());
			xmlTextWriter.WriteEndElement();
			xmlTextWriter.WriteEndDocument();
			xmlTextWriter.Close();
			Singleton singleton = new Singleton(httpWebRequest.GetResponse().GetResponseStream());
			TA_CATEGORY = singleton.objID;
		}

		public TorrentDownloader GetURLDownloader(string URL)
		{
			HttpWebRequest httpWebRequest = prot.CreateRequest();
			Stream requestStream = httpWebRequest.GetRequestStream();
			XmlTextWriter xmlTextWriter = new XmlTextWriter(requestStream, Encoding.GetEncoding("ISO-8859-1"));
			xmlTextWriter.WriteStartDocument();
			xmlTextWriter.WriteStartElement("REQUEST");
			xmlTextWriter.WriteStartElement("OBJECT");
			xmlTextWriter.WriteElementString("_object_id", base.objID);
			xmlTextWriter.WriteEndElement();
			xmlTextWriter.WriteElementString("METHOD", "getURLDownloader[URL]");
			xmlTextWriter.WriteStartElement("PARAMS");
			xmlTextWriter.WriteElementString("ENTRY", URL);
			xmlTextWriter.WriteEndElement();
			xmlTextWriter.WriteElementString("CONNECTION_ID", prot.ConnectionID);
			xmlTextWriter.WriteElementString("REQUEST_ID", prot.IDGenerator());
			xmlTextWriter.WriteEndElement();
			xmlTextWriter.WriteEndDocument();
			xmlTextWriter.Close();
			return new TorrentDownloader(httpWebRequest.GetResponse().GetResponseStream(), prot);
		}

		public Torrent createFromBEncodedData(byte[] arr)
		{
			string text = BitConverter.ToString(arr);
			text = text.Replace("-", "");
			HttpWebRequest httpWebRequest = prot.CreateRequest();
			Stream requestStream = httpWebRequest.GetRequestStream();
			XmlTextWriter xmlTextWriter = new XmlTextWriter(requestStream, Encoding.GetEncoding("ISO-8859-1"));
			xmlTextWriter.WriteStartDocument();
			xmlTextWriter.WriteStartElement("REQUEST");
			xmlTextWriter.WriteStartElement("OBJECT");
			xmlTextWriter.WriteElementString("_object_id", base.objID);
			xmlTextWriter.WriteEndElement();
			xmlTextWriter.WriteElementString("METHOD", "createFromBEncodedData[byte[]]");
			xmlTextWriter.WriteStartElement("PARAMS");
			xmlTextWriter.WriteElementString("ENTRY", text);
			xmlTextWriter.WriteEndElement();
			xmlTextWriter.WriteElementString("CONNECTION_ID", prot.ConnectionID);
			xmlTextWriter.WriteElementString("REQUEST_ID", prot.IDGenerator());
			xmlTextWriter.WriteEndElement();
			xmlTextWriter.WriteEndDocument();
			xmlTextWriter.Close();
			return new Torrent(httpWebRequest.GetResponse().GetResponseStream());
		}
	}
}
