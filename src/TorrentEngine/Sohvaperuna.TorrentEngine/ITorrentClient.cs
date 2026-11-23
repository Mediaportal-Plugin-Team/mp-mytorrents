using System;
using System.Collections.Generic;

namespace Sohvaperuna.TorrentEngine
{
	public interface ITorrentClient
	{
		TorrentLabelList Labels { get; }

		TorrentList TorrentsAll { get; }

		event TorrentAdded TorrentAdded;

		event TorrentRemoved TorrentRemoved;

		event TorrentCompleted TorrentCompleted;

		void ChangeLabel(string torrentHash, string labelName);

		bool Connect();

		void Disconnect();

		List<File> GetFiles(string torrentHash);

		string GetResponse(string request);

		string GetResponse(string request, byte[] postData, string boundary, object parameters);

		List<DownloadDir> ListDirs();

		string GetSettings(string element);

		Dictionary<string, int> FillPriorities();

		void Pause(TorrentList torrents);

		void Resume(TorrentList torrents);

		void Start(TorrentList torrents);

		void Stop(TorrentList torrents);

		bool Pause(string Hash);

		bool Resume(string Hash);

		bool Remove(string Hash, bool removeData);

		bool Start(string Hash);

		bool Stop(string Hash);

		bool SetPriority(string hash, int index, int priority);

		bool StartDownloadToFolder(byte[] content, string filename, string label, string hash, string option, string folder);

		bool StartDownloadToFolder(Uri uri, string label, string hash, string option, string folder);

		bool StartDownload(byte[] content, string filename, string label, string hash, string option);

		bool StartDownload(Uri uri, string label, string hash, string option);

		bool UpdateData();

		bool ValidateUTorrentResponse(string response);
	}
}
