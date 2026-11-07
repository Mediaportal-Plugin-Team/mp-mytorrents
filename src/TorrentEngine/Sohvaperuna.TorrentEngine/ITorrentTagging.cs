namespace Sohvaperuna.TorrentEngine
{
	/// <summary>
	/// 2026: clients that can put a tag on a torrent (qBittorrent tags, BiglyBT tags).
	/// MyTorrents tags torrents "MyTorrents-copied" once their files are copied to the library.
	/// </summary>
	public interface ITorrentTagging
	{
		bool AddTag(string hash, string tag);
	}
}
