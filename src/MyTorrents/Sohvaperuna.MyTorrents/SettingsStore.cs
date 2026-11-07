using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml;
using MediaPortal.Configuration;

namespace Sohvaperuna.MyTorrents
{
	/// <summary>
	/// 2026: MyTorrents' own settings file, MyTorrents.settings.xml next to MediaPortal.xml.
	/// The settings used to be in MediaPortal.xml, which MediaPortal keeps a copy of while it runs and writes back when it
	/// closes - so a change made in MyTorrents Configuration while MediaPortal was running could be lost. MediaPortal never
	/// touches this file. Both programs read it again when it changes, so a change in one is used by the other at once.
	/// The first time, the MyTorrents settings are copied across from MediaPortal.xml (nothing to set again).
	/// </summary>
	public static class SettingsStore
	{
		private static readonly object Lock = new object();
		private static Dictionary<string, string> _values;
		private static DateTime _stamp = DateTime.MinValue;
		private static long _length = -1;
		private static DateTime _lastLook = DateTime.MinValue;

		/// set in tests (no MediaPortal folders)
		public static string FileOverride;
		public static string MediaPortalXmlOverride;

		public static string File => FileOverride ?? Config.GetFile(Config.Dir.Config, "MyTorrents.settings.xml");

		private static string MediaPortalXml => MediaPortalXmlOverride ?? Config.GetFile(Config.Dir.Config, "MediaPortal.xml");

		/// raised (in whichever program notices) when the other program changed the file
		public static event Action Changed;

		/// one program writes at a time (MediaPortal and MyTorrents Configuration)
		private static T Exclusive<T>(Func<T> work)
		{
			Mutex m = null;
			bool have = false;
			try
			{
				m = new Mutex(false, "MyTorrentsSettingsFile");
				try
				{
					have = m.WaitOne(3000);
				}
				catch (AbandonedMutexException)
				{
					have = true;
				}
			}
			catch
			{
			}
			try
			{
				return work();
			}
			finally
			{
				if (have)
				{
					try
					{
						m.ReleaseMutex();
					}
					catch
					{
					}
				}
				m?.Dispose();
			}
		}

		/// Reads the file again when it changed (checked at most every 2 seconds).
		private static void Fresh(bool force = false)
		{
			if (!force && _values != null && DateTime.Now - _lastLook < TimeSpan.FromSeconds(2)) return;
			_lastLook = DateTime.Now;
			string file = File;
			if (!System.IO.File.Exists(file))
			{
				if (_values == null)
				{
					_values = Exclusive(() => System.IO.File.Exists(file) ? Read(file) : Migrate(file));
					Stamp(file);
				}
				return;
			}
			FileInfo fi = new FileInfo(file);
			if (_values != null && fi.LastWriteTimeUtc == _stamp && fi.Length == _length) return;
			bool first = _values == null;
			Dictionary<string, string> read = Exclusive(() => Read(file));
			if (read == null)
			{
				if (_values == null) _values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
				return; // half-written - next look
			}
			_values = read;
			Stamp(file);
			if (!first)
			{
				try
				{
					Changed?.Invoke();
				}
				catch
				{
				}
			}
		}

		private static void Stamp(string file)
		{
			try
			{
				FileInfo fi = new FileInfo(file);
				_stamp = fi.Exists ? fi.LastWriteTimeUtc : DateTime.MinValue;
				_length = fi.Exists ? fi.Length : -1;
			}
			catch
			{
			}
		}

		private static Dictionary<string, string> Read(string file)
		{
			Dictionary<string, string> d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			try
			{
				XmlDocument x = LoadXml(file);
				foreach (XmlElement e in x.SelectNodes("/MyTorrents/entry"))
				{
					string name = e.GetAttribute("name");
					if (name.Length > 0) d[name] = e.InnerText;
				}
				return d;
			}
			catch
			{
				return null;
			}
		}

		/// The first time: the MyTorrents part of MediaPortal.xml becomes the settings file.
		private static Dictionary<string, string> Migrate(string file)
		{
			Dictionary<string, string> d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			try
			{
				if (System.IO.File.Exists(MediaPortalXml))
				{
					XmlDocument x = LoadXml(MediaPortalXml);
					foreach (XmlElement e in x.SelectNodes("/profile/section[@name='MyTorrents']/entry"))
					{
						string name = e.GetAttribute("name");
						if (name.Length > 0) d[name] = e.InnerText;
					}
				}
			}
			catch (Exception ex)
			{
				try
				{
					Log.Instance().Print("Settings: could not read MediaPortal.xml - " + ex.Message);
				}
				catch
				{
				}
			}
			Write(file, d);
			try
			{
				Log.Instance().Print($"Settings: {d.Count} MyTorrents setting(s) copied from MediaPortal.xml to MyTorrents.settings.xml - kept there from now on");
			}
			catch
			{
			}
			return d;
		}

		/// opened as a file (shared - MediaPortal may have MediaPortal.xml open), not as a web address
		private static XmlDocument LoadXml(string file)
		{
			XmlDocument x = new XmlDocument();
			using (FileStream fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
			{
				x.Load(fs);
			}
			return x;
		}

		private static void Write(string file, Dictionary<string, string> d)
		{
			XmlDocument x = new XmlDocument();
			x.AppendChild(x.CreateXmlDeclaration("1.0", "utf-8", null));
			XmlElement root = x.CreateElement("MyTorrents");
			x.AppendChild(root);
			root.AppendChild(x.CreateComment(" MyTorrents settings - change them in MyTorrents Configuration or in MediaPortal "));
			foreach (KeyValuePair<string, string> kv in d.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
			{
				XmlElement e = x.CreateElement("entry");
				e.SetAttribute("name", kv.Key);
				e.InnerText = kv.Value ?? "";
				root.AppendChild(e);
			}
			string dir = Path.GetDirectoryName(file);
			if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
			string temp = file + ".tmp";
			using (FileStream fs = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
			{
				x.Save(fs);
			}
			if (System.IO.File.Exists(file))
			{
				System.IO.File.Copy(temp, file, true);
				System.IO.File.Delete(temp);
			}
			else
			{
				System.IO.File.Move(temp, file);
			}
		}

		public static string Get(string key, string defaultValue)
		{
			lock (Lock)
			{
				Fresh();
				string v;
				return _values.TryGetValue(key, out v) && v != null ? v : defaultValue;
			}
		}

		public static void Set(string key, string value)
		{
			SetMany(new Dictionary<string, string> { { key, value } });
		}

		/// Writes the changes on top of what is in the file now (so a change the other program just made is kept).
		public static void SetMany(IDictionary<string, string> changes)
		{
			lock (Lock)
			{
				Fresh(true);
				string file = File;
				Exclusive(() =>
				{
					Dictionary<string, string> now = (System.IO.File.Exists(file) ? Read(file) : null) ?? new Dictionary<string, string>(_values, StringComparer.OrdinalIgnoreCase);
					bool changed = false;
					foreach (KeyValuePair<string, string> kv in changes)
					{
						string old;
						string v = kv.Value ?? "";
						if (!now.TryGetValue(kv.Key, out old) || old != v)
						{
							now[kv.Key] = v;
							changed = true;
						}
					}
					if (changed || !System.IO.File.Exists(file)) Write(file, now);
					_values = now;
					return true;
				});
				Stamp(file);
			}
		}

		/// Forget what was read (tests).
		public static void Reset()
		{
			lock (Lock)
			{
				_values = null;
				_stamp = DateTime.MinValue;
				_length = -1;
				_lastLook = DateTime.MinValue;
			}
		}
	}
}
