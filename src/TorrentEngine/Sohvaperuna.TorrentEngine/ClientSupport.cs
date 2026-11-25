using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Text;
using MediaPortal.Configuration;

namespace Sohvaperuna.TorrentEngine
{
	/// <summary>
	/// Shared helpers for the newer client engines (qBittorrent, BiglyBT/Transmission).
	/// Added 2026 - not part of the original MyTorrents code.
	/// </summary>
	public static class ClientSupport
	{
		private static readonly object _logLock = new object();

		/// uTorrent-style status bits the UI understands (see Torrent.UTorrentStatus)
		public const int Started = 1, Checking = 2, Checked = 8, Error = 16, Paused = 32, Queued = 64, Loaded = 128;

		static ClientSupport()
		{
			EnableModernTls();
		}

		/// .NET 4.7.2 on older Windows can default to TLS 1.0 - make sure 1.2 (and 1.3 where available) are on.
		public static void EnableModernTls()
		{
			try
			{
				ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
				ServicePointManager.SecurityProtocol |= (SecurityProtocolType)12288; // Tls13
			}
			catch
			{
			}
		}

		public static void Log(string line)
		{
			try
			{
				lock (_logLock)
				{
					using (StreamWriter w = new StreamWriter(Config.GetFile(Config.Dir.Log, "MyTorrents.log"), append: true))
					{
						w.WriteLine($"[{DateTime.Now.ToShortTimeString()} {DateTime.Now.ToShortDateString()}] {line}");
					}
				}
			}
			catch
			{
			}
		}

		/// "localhost" can resolve to IPv6 (::1) on Windows 10/11. BiglyBT's "local only" access and
		/// password-off host whitelist expect 127.0.0.1, so localhost is always sent as 127.0.0.1.
		public static string LocalHostToIPv4(string host)
		{
			if (string.IsNullOrWhiteSpace(host))
			{
				return "127.0.0.1";
			}
			return Regex.Replace(host.Trim(), @"(^|//)localhost(?=$|[:/])", "${1}127.0.0.1", RegexOptions.IgnoreCase);
		}

		public static int UnixNow()
		{
			return (int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
		}

		/// Compare the previous and current torrent lists and raise added/completed/removed events.
		public static void RaiseChanges(ITorrentClient client, TorrentList previous, TorrentList current, int timerSeconds,
			TorrentAdded added, TorrentCompleted completed, TorrentRemoved removed)
		{
			if (previous == null)
			{
				return; // first poll - nothing to compare with
			}
			Dictionary<string, Torrent> old = new Dictionary<string, Torrent>(StringComparer.OrdinalIgnoreCase);
			foreach (Torrent t in previous)
			{
				old[t.Hash] = t;
			}
			foreach (Torrent t in current)
			{
				Torrent before;
				if (!old.TryGetValue(t.Hash, out before))
				{
					if (added != null && UnixNow() - t.AddedOn < Math.Max(60, timerSeconds * 2))
					{
						added(t);
					}
					continue;
				}
				if (completed != null && t.Progress >= 100.0 && before.Progress < 100.0)
				{
					completed(t);
				}
				old.Remove(t.Hash);
			}
			if (removed != null)
			{
				foreach (Torrent t in old.Values)
				{
					removed(t);
				}
			}
		}

		public static int ToInt(object o)
		{
			if (o == null) return 0;
			try { return Convert.ToInt32(o, CultureInfo.InvariantCulture); } catch { return 0; }
		}

		public static long ToLong(object o)
		{
			if (o == null) return 0;
			try { return Convert.ToInt64(o, CultureInfo.InvariantCulture); } catch { return 0; }
		}

		public static double ToDouble(object o)
		{
			if (o == null) return 0;
			try { return Convert.ToDouble(o, CultureInfo.InvariantCulture); } catch { return 0; }
		}

		public static string ToStr(object o)
		{
			return o == null ? "" : Convert.ToString(o, CultureInfo.InvariantCulture);
		}

		public static object Get(IDictionary d, string key)
		{
			return d != null && d.Contains(key) ? d[key] : null;
		}

		/// Simple multipart/form-data body builder.
		public sealed class Multipart
		{
			public readonly string Boundary = "----MyTorrents" + Guid.NewGuid().ToString("N");
			private readonly MemoryStream _ms = new MemoryStream();

			public string ContentType => "multipart/form-data; boundary=" + Boundary;

			public void Field(string name, string value)
			{
				if (value == null) return;
				Write($"--{Boundary}\r\nContent-Disposition: form-data; name=\"{name}\"\r\n\r\n{value}\r\n");
			}

			public void File(string name, string filename, byte[] data)
			{
				Write($"--{Boundary}\r\nContent-Disposition: form-data; name=\"{name}\"; filename=\"{filename}\"\r\nContent-Type: application/x-bittorrent\r\n\r\n");
				_ms.Write(data, 0, data.Length);
				Write("\r\n");
			}

			public byte[] ToArray()
			{
				Write($"--{Boundary}--\r\n");
				return _ms.ToArray();
			}

			private void Write(string s)
			{
				byte[] b = Encoding.UTF8.GetBytes(s);
				_ms.Write(b, 0, b.Length);
			}
		}
	}
}
