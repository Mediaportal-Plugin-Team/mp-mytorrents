using System.IO;
using System.Net;
using System.Text;
using System.Xml;

namespace AzureusEngine.AzCommunication
{
	public class TorrentDownloader : Singleton
	{
		private Az_Protocol prot;

		internal TorrentDownloader(Stream stream, Az_Protocol prot)
			: base(stream)
		{
			this.prot = prot;
		}

		public Torrent Download()
		{
			HttpWebRequest httpWebRequest = prot.CreateRequest();
			Stream requestStream = httpWebRequest.GetRequestStream();
			XmlTextWriter xmlTextWriter = new XmlTextWriter(requestStream, Encoding.GetEncoding("ISO-8859-1"));
			xmlTextWriter.WriteStartDocument();
			xmlTextWriter.WriteStartElement("REQUEST");
			xmlTextWriter.WriteStartElement("OBJECT");
			xmlTextWriter.WriteElementString("_object_id", base.objID);
			xmlTextWriter.WriteEndElement();
			xmlTextWriter.WriteElementString("METHOD", "download");
			xmlTextWriter.WriteElementString("CONNECTION_ID", prot.ConnectionID);
			xmlTextWriter.WriteElementString("REQUEST_ID", prot.IDGenerator());
			xmlTextWriter.WriteEndElement();
			xmlTextWriter.WriteEndDocument();
			xmlTextWriter.Close();
			return new Torrent(httpWebRequest.GetResponse().GetResponseStream());
		}
	}
}
