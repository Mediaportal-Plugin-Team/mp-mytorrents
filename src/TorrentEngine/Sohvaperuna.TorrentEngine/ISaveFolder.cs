namespace Sohvaperuna.TorrentEngine
{
	/// <summary>2026: clients whose default download folder can be changed from MyTorrents.</summary>
	public interface ISaveFolder
	{
		bool SetSaveFolder(string folder);
	}
}
