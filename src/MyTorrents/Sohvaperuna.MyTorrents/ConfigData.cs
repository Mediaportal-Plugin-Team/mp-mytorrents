using System.Xml;

namespace Sohvaperuna.MyTorrents
{
	public struct ConfigData
	{
		private XmlDocument _xmldoc;

		private string _serverIP;

		private string _serverPort;

		private Backend _backend;

		private string _username;

		private string _password;

		private bool _notify;

		public XmlDocument MyTorrentsConfiguration
		{
			get
			{
				return _xmldoc;
			}
			set
			{
				_xmldoc = value;
			}
		}

		public string Server
		{
			get
			{
				return _serverIP;
			}
			set
			{
				_serverIP = value;
			}
		}

		public string Port
		{
			get
			{
				return _serverPort;
			}
			set
			{
				_serverPort = value;
			}
		}

		public Backend Backend
		{
			get
			{
				return _backend;
			}
			set
			{
				_backend = value;
			}
		}

		public string User
		{
			get
			{
				return _username;
			}
			set
			{
				_username = value;
			}
		}

		public string Password
		{
			get
			{
				return _password;
			}
			set
			{
				_password = value;
			}
		}

		public bool Notify
		{
			get
			{
				return _notify;
			}
			set
			{
				_notify = value;
			}
		}
	}
}
