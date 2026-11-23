namespace Sohvaperuna.TorrentEngine
{
	public class Action_ChangeLabel : IAction
	{
		private string _hash;

		private string _label;

		private ITorrentClient _client;

		public Action_ChangeLabel(ITorrentClient client, string hash, string label)
		{
			_client = client;
			_hash = hash;
			_label = label;
		}

		public bool DoIt()
		{
			_client.GetResponse($"?action=setprops&hash={_hash}&s=label&v={_label}");
			return true;
		}
	}
}
