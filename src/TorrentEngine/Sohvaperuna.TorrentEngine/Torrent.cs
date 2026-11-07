namespace Sohvaperuna.TorrentEngine
{
	public class Torrent
	{
		public enum UTorrentStatus
		{
			Started = 1,
			Checking = 2,
			StartAfterCheck = 4,
			Checked = 8,
			Error = 0x10,
			Paused = 0x20,
			Queued = 0x40,
			Loaded = 0x80
		}

		private ITorrentClient _client;

		private string _hash;

		private int _status;

		private string _name;

		private long _size;

		private double _progress;

		private long _downloaded;

		private long _uploaded;

		private long _ratio;

		private long _uploadSpeed;

		private long _downloadSpeed;

		private int _ETA;

		private string _label;

		private int _peersConnected;

		private int _peersInSwarm;

		private int _seedsConnected;

		private int _seedsInSwarm;

		private int _availability;

		private int _torrentQueueOrder;

		private long _remaining;

		private string _downloadURL;

		private string _rssfeedURL;

		private string _statusmessage;

		private string _streamID;

		private int _addedOn;

		private int _completedOn;

		private string _appUpdateURL;

		private string _torrentSavePath;

		private int _unknown;

		public string Hash
		{
			get
			{
				return _hash;
			}
			set
			{
				_hash = value;
			}
		}

		public int Status
		{
			get
			{
				return _status;
			}
			set
			{
				_status = value;
			}
		}

		public string Name
		{
			get
			{
				return _name;
			}
			set
			{
				_name = value;
			}
		}

		public long Size
		{
			get
			{
				return _size;
			}
			set
			{
				_size = value;
			}
		}

		public double Progress
		{
			get
			{
				return _progress;
			}
			set
			{
				_progress = value;
			}
		}

		public long Downloaded
		{
			get
			{
				return _downloaded;
			}
			set
			{
				_downloaded = value;
			}
		}

		public long Uploaded
		{
			get
			{
				return _uploaded;
			}
			set
			{
				_uploaded = value;
			}
		}

		public long Ratio
		{
			get
			{
				return _ratio;
			}
			set
			{
				_ratio = value;
			}
		}

		public long UploadSpeed
		{
			get
			{
				return _uploadSpeed;
			}
			set
			{
				_uploadSpeed = value;
			}
		}

		public long DownloadSpeed
		{
			get
			{
				return _downloadSpeed;
			}
			set
			{
				_downloadSpeed = value;
			}
		}

		public int ETA
		{
			get
			{
				return _ETA;
			}
			set
			{
				_ETA = value;
			}
		}

		public string Label
		{
			get
			{
				return _label;
			}
			set
			{
				_label = value;
			}
		}

		public int PeersConnected
		{
			get
			{
				return _peersConnected;
			}
			set
			{
				_peersConnected = value;
			}
		}

		public int PeersInSwarm
		{
			get
			{
				return _peersInSwarm;
			}
			set
			{
				_peersInSwarm = value;
			}
		}

		public int SeedsConnected
		{
			get
			{
				return _seedsConnected;
			}
			set
			{
				_seedsConnected = value;
			}
		}

		public int SeedsInSwarm
		{
			get
			{
				return _seedsInSwarm;
			}
			set
			{
				_seedsInSwarm = value;
			}
		}

		public int Availability
		{
			get
			{
				return _availability;
			}
			set
			{
				_availability = value;
			}
		}

		public int TorrentQueueOrder
		{
			get
			{
				return _torrentQueueOrder;
			}
			set
			{
				_torrentQueueOrder = value;
			}
		}

		public long Remaining
		{
			get
			{
				return _remaining;
			}
			set
			{
				_remaining = value;
			}
		}

		public string DownloadURL
		{
			get
			{
				return _downloadURL;
			}
			set
			{
				_downloadURL = value;
			}
		}

		public string RSSFeedURL
		{
			get
			{
				return _rssfeedURL;
			}
			set
			{
				_rssfeedURL = value;
			}
		}

		public string StatusMessage
		{
			get
			{
				return _statusmessage;
			}
			set
			{
				_statusmessage = value;
			}
		}

		public string StreamID
		{
			get
			{
				return _streamID;
			}
			set
			{
				_streamID = value;
			}
		}

		public int AddedOn
		{
			get
			{
				return _addedOn;
			}
			set
			{
				_addedOn = value;
			}
		}

		public int CompletedOn
		{
			get
			{
				return _completedOn;
			}
			set
			{
				_completedOn = value;
			}
		}

		public string AppUpdateURL
		{
			get
			{
				return _appUpdateURL;
			}
			set
			{
				_appUpdateURL = value;
			}
		}

		public string TorrentSavePath
		{
			get
			{
				return _torrentSavePath;
			}
			set
			{
				_torrentSavePath = value;
			}
		}

		/// 2026: tags from the torrent program (qBittorrent), comma separated
		public string Tags { get; set; } = "";

		public int Unknown
		{
			get
			{
				return _unknown;
			}
			set
			{
				_unknown = value;
			}
		}

		public Torrent(ITorrentClient client)
		{
			_client = client;
		}

		public bool HasStatus(int bits)
		{
			return (Status & bits) == bits;
		}

		public bool Paused()
		{
			return HasStatus(32);
		}

		public bool Started()
		{
			return HasStatus(1);
		}
	}
}
