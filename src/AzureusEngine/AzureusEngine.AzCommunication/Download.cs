using System;
using System.IO;
using System.Net;
using System.Text;
using System.Xml;

namespace AzureusEngine.AzCommunication
{
	public class Download : Singleton
	{
		private Az_Protocol prot;

		private bool _Force_Start;

		private bool _Complete;

		private long _creationTime;

		private double _max_dl;

		private double _max_ul;

		private bool _msgs_enabled;

		private string _name;

		private bool _persistend;

		private int _position;

		private bool _paused;

		private string _category;

		private int _state;

		private int _substate;

		private Stats _stats;

		private Torrent _torrent;

		private Scrape_Result _anresult;

		public long CreationTime => _creationTime;

		public bool Completed => _Complete;

		public double MaxDownload => _max_dl;

		public double MaxUpload => _max_ul;

		public bool Messaging_Enabled => _msgs_enabled;

		public string Name => _name;

		public bool Persistend => _persistend;

		public int Position => _position;

		public bool Paused => _paused;

		public bool ForceStart => _Force_Start;

		public bool Stopped => _stats.Status == "STOPPED";

		public long Remaining => (long)(_torrent.Size - _stats.Downloaded) * 1048576;

		public Stats Stats => _stats;

		public Torrent Torrent => _torrent;

		public Scrape_Result Scrape_Result => _anresult;

		public DownloadState State => (DownloadState)Enum.ToObject(typeof(DownloadState), _state);

		public int Rank
		{
			get
			{
				return getRank();
			}
			set
			{
				setRank(value);
			}
		}

		public string Category
		{
			get
			{
				return _category;
			}
			set
			{
				SetCategory(value);
			}
		}

		internal Download(Stream stream, Az_Protocol prot)
			: base(stream)
		{
			this.prot = prot;
			_Complete = GetBoolValue("//complete");
			_creationTime = Convert.ToInt64(GetStringValue("//creation_time"));
			_max_dl = GetDoubleValue("//max_download_rate");
			_max_ul = GetDoubleValue("//max_upload_rate");
			_msgs_enabled = GetBoolValue("//messaging_enabled");
			_paused = GetBoolValue("//paused");
			_persistend = GetBoolValue("//persistent");
			_position = GetIntValue("//position");
			_Force_Start = GetBoolValue("//force_start");
			_name = GetStringValue("//name");
			_state = GetIntValue("//state");
			MemoryStream stream2 = new MemoryStream(Encoding.ASCII.GetBytes("<root>" + doc.SelectSingleNode("//stats").InnerXml + "</root>"));
			_stats = new Stats(stream2);
			stream2 = new MemoryStream(Encoding.ASCII.GetBytes("<root>" + doc.SelectSingleNode("//torrent").InnerXml + "</root>"));
			_torrent = new Torrent(stream2);
			stream2 = new MemoryStream(Encoding.ASCII.GetBytes("<root>" + doc.SelectSingleNode("//announce_result").InnerXml + "</root>"));
			_anresult = new Scrape_Result(stream2);
			_category = getCategory();
		}

		public bool Stop()
		{
			HttpWebRequest httpWebRequest = prot.CreateRequest();
			Stream requestStream = httpWebRequest.GetRequestStream();
			XmlTextWriter xmlTextWriter = new XmlTextWriter(requestStream, Encoding.GetEncoding("ISO-8859-1"));
			xmlTextWriter.WriteStartDocument();
			xmlTextWriter.WriteStartElement("REQUEST");
			xmlTextWriter.WriteStartElement("OBJECT");
			xmlTextWriter.WriteElementString("_object_id", base.objID);
			xmlTextWriter.WriteEndElement();
			xmlTextWriter.WriteElementString("METHOD", "stop");
			xmlTextWriter.WriteElementString("CONNECTION_ID", prot.ConnectionID);
			xmlTextWriter.WriteElementString("REQUEST_ID", prot.IDGenerator());
			xmlTextWriter.WriteEndElement();
			xmlTextWriter.WriteEndDocument();
			xmlTextWriter.Close();
			StreamReader streamReader = new StreamReader(httpWebRequest.GetResponse().GetResponseStream());
			return streamReader.ReadToEnd().ToLower().Contains("error");
		}

		public bool Start()
		{
			HttpWebRequest httpWebRequest = prot.CreateRequest();
			Stream requestStream = httpWebRequest.GetRequestStream();
			XmlTextWriter xmlTextWriter = new XmlTextWriter(requestStream, Encoding.GetEncoding("ISO-8859-1"));
			xmlTextWriter.WriteStartDocument();
			xmlTextWriter.WriteStartElement("REQUEST");
			xmlTextWriter.WriteStartElement("OBJECT");
			xmlTextWriter.WriteElementString("_object_id", base.objID);
			xmlTextWriter.WriteEndElement();
			xmlTextWriter.WriteElementString("METHOD", "restart");
			xmlTextWriter.WriteElementString("CONNECTION_ID", prot.ConnectionID);
			xmlTextWriter.WriteElementString("REQUEST_ID", prot.IDGenerator());
			xmlTextWriter.WriteEndElement();
			xmlTextWriter.WriteEndDocument();
			xmlTextWriter.Close();
			StreamReader streamReader = new StreamReader(httpWebRequest.GetResponse().GetResponseStream());
			return streamReader.ReadToEnd().ToLower().Contains("error");
		}

		public bool Pause()
		{
			HttpWebRequest httpWebRequest = prot.CreateRequest();
			Stream requestStream = httpWebRequest.GetRequestStream();
			XmlTextWriter xmlTextWriter = new XmlTextWriter(requestStream, Encoding.GetEncoding("ISO-8859-1"));
			xmlTextWriter.WriteStartDocument();
			xmlTextWriter.WriteStartElement("REQUEST");
			xmlTextWriter.WriteStartElement("OBJECT");
			xmlTextWriter.WriteElementString("_object_id", base.objID);
			xmlTextWriter.WriteEndElement();
			xmlTextWriter.WriteElementString("METHOD", "pause");
			xmlTextWriter.WriteElementString("CONNECTION_ID", prot.ConnectionID);
			xmlTextWriter.WriteElementString("REQUEST_ID", prot.IDGenerator());
			xmlTextWriter.WriteEndElement();
			xmlTextWriter.WriteEndDocument();
			xmlTextWriter.Close();
			StreamReader streamReader = new StreamReader(httpWebRequest.GetResponse().GetResponseStream());
			return streamReader.ReadToEnd().ToLower().Contains("error");
		}

		public bool Remove(bool RemoveData)
		{
			HttpWebRequest httpWebRequest = prot.CreateRequest();
			Stream requestStream = httpWebRequest.GetRequestStream();
			XmlTextWriter xmlTextWriter = new XmlTextWriter(requestStream, Encoding.GetEncoding("ISO-8859-1"));
			xmlTextWriter.WriteStartDocument();
			xmlTextWriter.WriteStartElement("REQUEST");
			xmlTextWriter.WriteStartElement("OBJECT");
			xmlTextWriter.WriteElementString("_object_id", base.objID);
			xmlTextWriter.WriteEndElement();
			xmlTextWriter.WriteElementString("METHOD", "remove[boolean,boolean]");
			xmlTextWriter.WriteStartElement("PARAMS");
			xmlTextWriter.WriteStartElement("ENTRY");
			xmlTextWriter.WriteAttributeString("index", "0");
			xmlTextWriter.WriteValue(value: true);
			xmlTextWriter.WriteEndElement();
			xmlTextWriter.WriteStartElement("ENTRY");
			xmlTextWriter.WriteAttributeString("index", "1");
			xmlTextWriter.WriteValue(RemoveData);
			xmlTextWriter.WriteEndElement();
			xmlTextWriter.WriteEndElement();
			xmlTextWriter.WriteElementString("CONNECTION_ID", prot.ConnectionID);
			xmlTextWriter.WriteElementString("REQUEST_ID", prot.IDGenerator());
			xmlTextWriter.WriteEndElement();
			xmlTextWriter.WriteEndDocument();
			xmlTextWriter.Close();
			StreamReader streamReader = new StreamReader(httpWebRequest.GetResponse().GetResponseStream());
			return streamReader.ReadToEnd().ToLower().Contains("error");
		}

		private bool SetCategory(string Category)
		{
			HttpWebRequest httpWebRequest = prot.CreateRequest();
			Stream requestStream = httpWebRequest.GetRequestStream();
			XmlTextWriter xmlTextWriter = new XmlTextWriter(requestStream, Encoding.GetEncoding("ISO-8859-1"));
			xmlTextWriter.WriteStartDocument();
			xmlTextWriter.WriteStartElement("REQUEST");
			xmlTextWriter.WriteStartElement("OBJECT");
			xmlTextWriter.WriteElementString("_object_id", base.objID);
			xmlTextWriter.WriteEndElement();
			xmlTextWriter.WriteElementString("METHOD", "setAttribute[TorrentAttribute,String]");
			xmlTextWriter.WriteStartElement("PARAMS");
			xmlTextWriter.WriteStartElement("ENTRY");
			xmlTextWriter.WriteAttributeString("index", "0");
			xmlTextWriter.WriteStartElement("OBJECT");
			xmlTextWriter.WriteElementString("_object_id", TorrentManager.TA_CATEGORY);
			xmlTextWriter.WriteEndElement();
			xmlTextWriter.WriteEndElement();
			xmlTextWriter.WriteStartElement("ENTRY");
			xmlTextWriter.WriteAttributeString("index", "1");
			xmlTextWriter.WriteValue(Category);
			xmlTextWriter.WriteEndElement();
			xmlTextWriter.WriteEndElement();
			xmlTextWriter.WriteElementString("CONNECTION_ID", prot.ConnectionID);
			xmlTextWriter.WriteElementString("REQUEST_ID", prot.IDGenerator());
			xmlTextWriter.WriteEndElement();
			xmlTextWriter.WriteEndDocument();
			xmlTextWriter.Close();
			StreamReader streamReader = new StreamReader(httpWebRequest.GetResponse().GetResponseStream());
			return streamReader.ReadToEnd().ToLower().Contains("error");
		}

		private string getCategory()
		{
			HttpWebRequest httpWebRequest = prot.CreateRequest();
			Stream requestStream = httpWebRequest.GetRequestStream();
			XmlTextWriter xmlTextWriter = new XmlTextWriter(requestStream, Encoding.GetEncoding("ISO-8859-1"));
			xmlTextWriter.WriteStartDocument();
			xmlTextWriter.WriteStartElement("REQUEST");
			xmlTextWriter.WriteStartElement("OBJECT");
			xmlTextWriter.WriteElementString("_object_id", base.objID);
			xmlTextWriter.WriteEndElement();
			xmlTextWriter.WriteElementString("METHOD", "getAttribute[TorrentAttribute]");
			xmlTextWriter.WriteStartElement("PARAMS");
			xmlTextWriter.WriteStartElement("ENTRY");
			xmlTextWriter.WriteStartElement("OBJECT");
			xmlTextWriter.WriteElementString("_object_id", TorrentManager.TA_CATEGORY);
			xmlTextWriter.WriteEndElement();
			xmlTextWriter.WriteEndElement();
			xmlTextWriter.WriteEndElement();
			xmlTextWriter.WriteElementString("CONNECTION_ID", prot.ConnectionID);
			xmlTextWriter.WriteElementString("REQUEST_ID", prot.IDGenerator());
			xmlTextWriter.WriteEndElement();
			xmlTextWriter.WriteEndDocument();
			xmlTextWriter.Close();
			XmlDocument xmlDocument = new XmlDocument();
			xmlDocument.Load(httpWebRequest.GetResponse().GetResponseStream());
			XmlNode xmlNode = xmlDocument.SelectSingleNode("//RESPONSE");
			if (xmlNode.InnerText == null)
			{
				return "";
			}
			return xmlNode.InnerText;
		}

		private int getRank()
		{
			HttpWebRequest httpWebRequest = prot.CreateRequest();
			Stream requestStream = httpWebRequest.GetRequestStream();
			XmlTextWriter xmlTextWriter = new XmlTextWriter(requestStream, Encoding.GetEncoding("ISO-8859-1"));
			xmlTextWriter.WriteStartDocument();
			xmlTextWriter.WriteStartElement("REQUEST");
			xmlTextWriter.WriteStartElement("OBJECT");
			xmlTextWriter.WriteElementString("_object_id", base.objID);
			xmlTextWriter.WriteEndElement();
			xmlTextWriter.WriteElementString("METHOD", "getPriority");
			xmlTextWriter.WriteElementString("CONNECTION_ID", prot.ConnectionID);
			xmlTextWriter.WriteElementString("REQUEST_ID", prot.IDGenerator());
			xmlTextWriter.WriteEndElement();
			xmlTextWriter.WriteEndDocument();
			xmlTextWriter.Close();
			XmlDocument xmlDocument = new XmlDocument();
			xmlDocument.Load(httpWebRequest.GetResponse().GetResponseStream());
			return Convert.ToInt32(xmlDocument.DocumentElement.Value);
		}

		private void setRank(int val)
		{
			HttpWebRequest httpWebRequest = prot.CreateRequest();
			Stream requestStream = httpWebRequest.GetRequestStream();
			XmlTextWriter xmlTextWriter = new XmlTextWriter(requestStream, Encoding.GetEncoding("ISO-8859-1"));
			xmlTextWriter.WriteStartDocument();
			xmlTextWriter.WriteStartElement("REQUEST");
			xmlTextWriter.WriteStartElement("OBJECT");
			xmlTextWriter.WriteElementString("_object_id", base.objID);
			xmlTextWriter.WriteEndElement();
			xmlTextWriter.WriteElementString("METHOD", "setIndex[int]");
			xmlTextWriter.WriteStartAttribute("PARAMS");
			xmlTextWriter.WriteElementString("CONNECTION_ID", prot.ConnectionID);
			xmlTextWriter.WriteElementString("REQUEST_ID", prot.IDGenerator());
			xmlTextWriter.WriteEndElement();
			xmlTextWriter.WriteEndDocument();
			xmlTextWriter.Close();
		}
	}
}
