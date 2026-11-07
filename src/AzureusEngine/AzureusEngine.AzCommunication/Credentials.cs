namespace AzureusEngine.AzCommunication
{
	public class Credentials
	{
		private string _host;

		private string _port;

		public Credentials(string host, string port)
		{
			_host = host;
			_port = port;
		}

		public string GetAdress()
		{
			return "http://" + _host + ":" + _port + "/process.cgi";
		}
	}
}
