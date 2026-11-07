using System;
using System.Collections;
using System.Collections.Generic;
using System.Xml;
using MediaPortal.Configuration;
using MediaPortal.Database;
using SQLite.NET;

namespace Sohvaperuna.MyTorrents
{
	public class Configuration
	{
		private static Configuration _instance;

		private Hashtable _settings;

		private Dictionary<string, string> SQLSettings = new Dictionary<string, string>();

		public Hashtable Settings => _settings;

		private Configuration()
		{
			_settings = new Hashtable();
		}

		public static Configuration Instance()
		{
			if (_instance == null)
			{
				_instance = new Configuration();
				_instance.Load();
				// 2026: changed in the other program (MyTorrents Configuration / MediaPortal) - use the new values
				SettingsStore.Changed += () =>
				{
					try
					{
						_instance?.Load();
					}
					catch
					{
					}
				};
			}
			return _instance;
		}

		public void ClearSettings()
		{
			_instance = null;
		}

		/// The settings kept in the Hashtable, with their name in the settings file and their default.
		private static readonly string[,] Stored =
		{
			{ "uTorrent.Server.IP", "uTorrent.Server.IP", "127.0.0.1" },
			{ "uTorrent.Server.Port", "uTorrent.Server.Port", "9091" },
			{ "uTorrent.Username", "uTorrent.User", "" },
			{ "uTorrent.Password", "uTorrent.Password", "" },
			{ "Torrent.Notifications", "Torrent.Notifications", "True" },
			{ "Torrent.PluginName", "Torrent.PluginName", "My Torrents" },
			{ "uTorrent.Backend", "uTorrent.Backend", "biglybt" },
			{ "Torrent.Timer.Inside", "Torrent.Timer.Inside", "5" },
			{ "Torrent.Timer.Outside", "Torrent.Timer.Outside", "240" },
			{ "TorrentView", "TorrentView", "All" },
			{ "SortOrder", "SortOrder", "Default" },
		};

		/// 2026: from MyTorrents.settings.xml (see SettingsStore) instead of MediaPortal.xml
		private bool Load()
		{
			for (int i = 0; i < Stored.GetLength(0); i++)
			{
				_settings[Stored[i, 0]] = SettingsStore.Get(Stored[i, 1], Stored[i, 2]);
			}
			return true;
		}

		/// Extra values in MyTorrents' settings file (Auto grabber, folders, setup assistant ...).
		public static string GetExtra(string key, string defaultValue)
		{
			return (SettingsStore.Get(key, defaultValue) ?? defaultValue).Trim();
		}

		public static void SetExtra(string key, string value)
		{
			SettingsStore.Set(key, value ?? "");
		}

		public bool Save()
		{
			Dictionary<string, string> all = new Dictionary<string, string>();
			for (int i = 0; i < Stored.GetLength(0); i++)
			{
				all[Stored[i, 1]] = Convert.ToString(_settings[Stored[i, 0]]) ?? "";
			}
			SettingsStore.SetMany(all);
			return true;
		}

		public ConfigData LoadConfiguration()
		{
			ConfigData result = default(ConfigData);
			Configuration configuration = Instance();
			configuration.Settings["TorrentSearch.Config"] = Config.GetFile(Config.Dir.Config, "MyTorrents.xml");
			result.Server = (configuration.Settings["uTorrent.Server.IP"] as string ?? "").Trim();
			result.Port = (configuration.Settings["uTorrent.Server.Port"] as string ?? "").Trim();
			result.User = (configuration.Settings["uTorrent.Username"] as string ?? "").Trim();
			result.Password = (configuration.Settings["uTorrent.Password"] as string ?? "").Trim();
			string text = configuration.Settings["uTorrent.Backend"] as string;
			switch (text.ToLower())
			{
			case "azureus":
				result.Backend = Backend.Azureus;
				break;
			case "utorrent":
				result.Backend = Backend.uTorrent;
				break;
			case "deluge":
				result.Backend = Backend.Deluge;
				break;
			case "qbittorrent":
				result.Backend = Backend.qBittorrent;
				break;
			case "biglybt":
			case "transmission":
				result.Backend = Backend.BiglyBT;
				break;
			}
			result.Notify = Convert.ToBoolean(configuration.Settings["Torrent.Notifications"]);
			XmlDocument xmlDocument = new XmlDocument();
			try
			{
				xmlDocument.Load(Instance().Settings["TorrentSearch.Config"] as string);
			}
			catch
			{
				Log.Instance().Print("MyTorrents.xml config file loading failed!");
			}
			result.MyTorrentsConfiguration = xmlDocument;
			Log.Instance().Print("Configuration loaded");
			return result;
		}

		public void GetSQLSettings()
		{
			SQLSettings.Clear();
			SQLiteClient sQLiteClient = new SQLiteClient(Config.GetFile(Config.Dir.Database, "MyTorrents.db3"));
			if (!DatabaseUtility.TableExists(sQLiteClient, "Settings"))
			{
				DatabaseUtility.AddTable(sQLiteClient, "Settings", "CREATE TABLE Settings (ParameterName CHAR(256), ParameterValue CHAR(256));");
				sQLiteClient.Execute($"INSERT INTO Settings (ParameterName, ParameterValue) VALUES (\"TorrentView\" , \"All\");");
				sQLiteClient.Execute($"INSERT INTO Settings (ParameterName, ParameterValue) VALUES (\"SortOrder\" , \"Default\");");
				sQLiteClient.Execute($"INSERT INTO Settings (ParameterName, ParameterValue) VALUES (\"LabelFilter\" , \"None\");");
			}
			SQLiteResultSet sQLiteResultSet = sQLiteClient.Execute("SELECT * FROM Settings;");
			foreach (SQLiteResultSet.Row row in sQLiteResultSet.Rows)
			{
				SQLSettings.Add(row.fields[(int)sQLiteResultSet.ColumnIndices["ParameterName"]], row.fields[(int)sQLiteResultSet.ColumnIndices["ParameterValue"]]);
			}
			if (SQLSettings.Count != 0)
			{
			}
		}

		public void SaveSQLSettings()
		{
		}
	}
}
