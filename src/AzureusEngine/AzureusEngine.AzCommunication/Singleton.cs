using System;
using System.IO;
using System.Xml;

namespace AzureusEngine.AzCommunication
{
	public class Singleton
	{
		private string _conID;

		private string _objID;

		protected XmlDocument doc;

		public string ConID
		{
			get
			{
				return _conID;
			}
			set
			{
				_conID = value;
			}
		}

		public string objID
		{
			get
			{
				return _objID;
			}
			set
			{
				_objID = value;
			}
		}

		protected string GetStringValue(string Key)
		{
			XmlNode xmlNode;
			try
			{
				xmlNode = doc.DocumentElement.SelectSingleNode(Key);
			}
			catch
			{
				return "";
			}
			return xmlNode.InnerText;
		}

		protected int GetIntValue(string Key)
		{
			XmlNode xmlNode;
			try
			{
				xmlNode = doc.DocumentElement.SelectSingleNode(Key);
			}
			catch
			{
				return 0;
			}
			return Convert.ToInt32(xmlNode.InnerText);
		}

		protected double GetDoubleValue(string Key)
		{
			XmlNode xmlNode = null;
			try
			{
				xmlNode = doc.DocumentElement.SelectSingleNode(Key);
				if (xmlNode == null)
				{
					return 0.0;
				}
			}
			catch
			{
				return 0.0;
			}
			return Convert.ToDouble(xmlNode.InnerText);
		}

		protected bool GetBoolValue(string Key)
		{
			XmlNode xmlNode;
			try
			{
				xmlNode = doc.DocumentElement.SelectSingleNode(Key);
			}
			catch
			{
				return false;
			}
			return Convert.ToBoolean(xmlNode.InnerText);
		}

		public Singleton()
		{
		}

		internal Singleton(Stream stream)
		{
			doc = new XmlDocument();
			doc.Load(stream);
			try
			{
				_conID = doc.DocumentElement.SelectSingleNode("//_connection_id").InnerText;
			}
			catch (Exception)
			{
				_conID = "";
			}
			_objID = doc.DocumentElement.SelectSingleNode("//_object_id").InnerText;
		}

		internal Singleton(XmlReader rdr)
		{
			XmlDocument xmlDocument = new XmlDocument();
			xmlDocument.Load(rdr);
			_conID = xmlDocument.DocumentElement.SelectSingleNode("//_connection_id").InnerText;
			_objID = xmlDocument.DocumentElement.SelectSingleNode("//_object_id").InnerText;
		}
	}
}
