using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Xml;

namespace AzureusEngine.AzCommunication
{
	public class DownloadManager : Singleton
	{
		private Az_Protocol prot;

		private bool _CanPause;

		private bool _CanResume;

		private bool _IsSeedingOnly;

		private List<Download> downloads;

		public ICollection<Download> Downloads => downloads;

		public ICollection<Download> ActiveDownloads
		{
			get
			{
				List<Download> list = new List<Download>();
				foreach (Download download in downloads)
				{
					if (!download.Stopped)
					{
						list.Add(download);
					}
				}
				return list;
			}
		}

		internal DownloadManager(Stream stream, Az_Protocol prot)
			: base(stream)
		{
			this.prot = prot;
		}

		public void Update()
		{
			HttpWebRequest httpWebRequest = prot.CreateRequest();
			Stream requestStream = httpWebRequest.GetRequestStream();
			XmlTextWriter xmlTextWriter = new XmlTextWriter(requestStream, Encoding.GetEncoding("ISO-8859-1"));
			httpWebRequest.KeepAlive = true;
			xmlTextWriter.WriteStartDocument();
			xmlTextWriter.WriteStartElement("REQUEST");
			xmlTextWriter.WriteStartElement("OBJECT");
			xmlTextWriter.WriteElementString("_object_id", base.objID);
			xmlTextWriter.WriteEndElement();
			xmlTextWriter.WriteElementString("METHOD", "getDownloads");
			xmlTextWriter.WriteElementString("CONNECTION_ID", prot.ConnectionID);
			xmlTextWriter.WriteElementString("REQUEST_ID", prot.IDGenerator());
			xmlTextWriter.WriteEndElement();
			xmlTextWriter.WriteEndDocument();
			xmlTextWriter.Close();
			XmlDocument xmlDocument = new XmlDocument();
			xmlDocument.Load(httpWebRequest.GetResponse().GetResponseStream());
			XmlNodeList xmlNodeList = xmlDocument.DocumentElement.SelectNodes("//ENTRY");
			downloads = new List<Download>();
			foreach (XmlNode item2 in xmlNodeList)
			{
				string s = "<root>" + item2.InnerXml + "</root>";
				MemoryStream stream = new MemoryStream(Encoding.ASCII.GetBytes(s));
				Download item = new Download(stream, prot);
				downloads.Add(item);
			}
		}

		public Download GetDownload(string TorrentID)
		{
			HttpWebRequest httpWebRequest = prot.CreateRequest();
			Stream requestStream = httpWebRequest.GetRequestStream();
			XmlTextWriter xmlTextWriter = new XmlTextWriter(requestStream, Encoding.GetEncoding("ISO-8859-1"));
			xmlTextWriter.WriteStartDocument();
			xmlTextWriter.WriteStartElement("REQUEST");
			xmlTextWriter.WriteStartElement("OBJECT");
			xmlTextWriter.WriteElementString("_object_id", base.objID);
			xmlTextWriter.WriteEndElement();
			xmlTextWriter.WriteElementString("METHOD", "getDownload[Torrent]");
			xmlTextWriter.WriteStartElement("PARAMS");
			xmlTextWriter.WriteStartElement("ENTRY");
			xmlTextWriter.WriteStartElement("OBJECT");
			xmlTextWriter.WriteElementString("_object_id", TorrentID);
			xmlTextWriter.WriteEndElement();
			xmlTextWriter.WriteEndElement();
			xmlTextWriter.WriteEndElement();
			xmlTextWriter.WriteElementString("CONNECTION_ID", prot.ConnectionID);
			xmlTextWriter.WriteElementString("REQUEST_ID", prot.IDGenerator());
			xmlTextWriter.WriteEndElement();
			xmlTextWriter.WriteEndDocument();
			xmlTextWriter.Close();
			return new Download(httpWebRequest.GetResponse().GetResponseStream(), prot);
		}

		public Download AddDownload(Torrent Torrent)
		{
			HttpWebRequest httpWebRequest = prot.CreateRequest();
			Stream requestStream = httpWebRequest.GetRequestStream();
			XmlTextWriter xmlTextWriter = new XmlTextWriter(requestStream, Encoding.GetEncoding("ISO-8859-1"));
			xmlTextWriter.WriteStartDocument();
			xmlTextWriter.WriteStartElement("REQUEST");
			xmlTextWriter.WriteStartElement("OBJECT");
			xmlTextWriter.WriteElementString("_object_id", base.objID);
			xmlTextWriter.WriteEndElement();
			xmlTextWriter.WriteElementString("METHOD", "addDownload[Torrent]");
			xmlTextWriter.WriteStartElement("PARAMS");
			xmlTextWriter.WriteStartElement("ENTRY");
			xmlTextWriter.WriteStartElement("OBJECT");
			xmlTextWriter.WriteElementString("_object_id", Torrent.objID);
			xmlTextWriter.WriteEndElement();
			xmlTextWriter.WriteEndElement();
			xmlTextWriter.WriteEndElement();
			xmlTextWriter.WriteElementString("CONNECTION_ID", prot.ConnectionID);
			xmlTextWriter.WriteElementString("REQUEST_ID", prot.IDGenerator());
			xmlTextWriter.WriteEndElement();
			xmlTextWriter.WriteEndDocument();
			xmlTextWriter.Close();
			return new Download(httpWebRequest.GetResponse().GetResponseStream(), prot);
		}
	}
}
