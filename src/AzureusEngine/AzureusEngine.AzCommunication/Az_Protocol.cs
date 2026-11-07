using System;
using System.IO;
using System.Net;
using System.Text;
using System.Xml;

namespace AzureusEngine.AzCommunication
{
	public class Az_Protocol
	{
		private Credentials _cred;

		private string _conID;

		private string _intID;

		private DownloadManager dl_mgr;

		private TorrentManager t_mgr;

		public DownloadManager DownloadManager
		{
			get
			{
				if (dl_mgr == null)
				{
					dl_mgr = GetDownloadManager();
				}
				return dl_mgr;
			}
		}

		public TorrentManager TorrentManager
		{
			get
			{
				if (t_mgr == null)
				{
					t_mgr = GetTorrentManager();
				}
				return t_mgr;
			}
		}

		internal string ConnectionID => _conID;

		public Az_Protocol(Credentials cred)
		{
			_cred = cred;
		}

		public Az_Protocol(string host, string port)
		{
			_cred = new Credentials(host, port);
		}

		internal HttpWebRequest CreateRequest()
		{
			HttpWebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create(_cred.GetAdress());
			httpWebRequest.Method = "POST";
			return httpWebRequest;
		}

		internal string IDGenerator()
		{
			return Guid.NewGuid().GetHashCode().ToString();
		}

		public bool InitConnection()
		{
			HttpWebRequest httpWebRequest = CreateRequest();
			Stream requestStream = httpWebRequest.GetRequestStream();
			XmlTextWriter xmlTextWriter = new XmlTextWriter(requestStream, Encoding.GetEncoding("ISO-8859-1"));
			xmlTextWriter.WriteStartDocument();
			xmlTextWriter.WriteStartElement("REQUEST");
			xmlTextWriter.WriteElementString("METHOD", "getSingleton");
			xmlTextWriter.WriteElementString("REQUEST_ID", DateTime.Now.Ticks.ToString());
			xmlTextWriter.WriteEndElement();
			xmlTextWriter.WriteEndDocument();
			xmlTextWriter.Close();
			Singleton singleton = new Singleton(httpWebRequest.GetResponse().GetResponseStream());
			_conID = singleton.ConID;
			_intID = singleton.objID;
			if (_conID != null && _intID != null)
			{
				t_mgr = GetTorrentManager();
				dl_mgr = GetDownloadManager();
				return true;
			}
			return false;
		}

		public TorrentManager GetTorrentManager()
		{
			HttpWebRequest httpWebRequest = CreateRequest();
			Stream requestStream = httpWebRequest.GetRequestStream();
			XmlTextWriter xmlTextWriter = new XmlTextWriter(requestStream, Encoding.GetEncoding("ISO-8859-1"));
			xmlTextWriter.WriteStartDocument();
			xmlTextWriter.WriteStartElement("REQUEST");
			xmlTextWriter.WriteStartElement("OBJECT");
			xmlTextWriter.WriteElementString("_object_id", _intID);
			xmlTextWriter.WriteEndElement();
			xmlTextWriter.WriteElementString("METHOD", "getTorrentManager");
			xmlTextWriter.WriteElementString("CONNECTION_ID", _conID);
			xmlTextWriter.WriteElementString("REQUEST_ID", IDGenerator());
			xmlTextWriter.WriteEndElement();
			xmlTextWriter.WriteEndDocument();
			xmlTextWriter.Close();
			return new TorrentManager(httpWebRequest.GetResponse().GetResponseStream(), this);
		}

		public DownloadManager GetDownloadManager()
		{
			HttpWebRequest httpWebRequest = CreateRequest();
			Stream requestStream = httpWebRequest.GetRequestStream();
			XmlTextWriter xmlTextWriter = new XmlTextWriter(requestStream, Encoding.GetEncoding("ISO-8859-1"));
			xmlTextWriter.WriteStartDocument();
			xmlTextWriter.WriteStartElement("REQUEST");
			xmlTextWriter.WriteStartElement("OBJECT");
			xmlTextWriter.WriteElementString("_object_id", _intID);
			xmlTextWriter.WriteEndElement();
			xmlTextWriter.WriteElementString("METHOD", "getDownloadManager");
			xmlTextWriter.WriteElementString("CONNECTION_ID", _conID);
			xmlTextWriter.WriteElementString("REQUEST_ID", IDGenerator());
			xmlTextWriter.WriteEndElement();
			xmlTextWriter.WriteEndDocument();
			xmlTextWriter.Close();
			return new DownloadManager(httpWebRequest.GetResponse().GetResponseStream(), this);
		}
	}
}
