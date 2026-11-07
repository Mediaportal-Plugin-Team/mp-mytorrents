using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Security.Principal;
using System.ServiceProcess;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;
using System.Xml;
using Microsoft.Win32;

namespace Sohvaperuna.MyTorrentsConfig
{
	/// <summary>What is installed, and where.</summary>
	internal sealed class AppState
	{
		public bool Installed;
		public string Version = "";
		public string Path = "";
		public override string ToString() => Installed ? (Version.Length > 0 ? "installed (" + Version + ")" : "installed") : "not installed";
	}

	/// <summary>Latest release on GitHub.</summary>
	internal sealed class Release
	{
		public string Version = "";
		public string Url = "";
		public long Size;
	}

	/// <summary>
	/// Everything the setup assistant does: finding the apps, downloading, installing and configuring them.
	/// UI-free so it can also run in the elevated helper ("/admin").
	/// </summary>
	internal static class SetupCore
	{
		public const string FlareSolverrUrl = "http://127.0.0.1:8191";
		public const string RunKeyName = "FlareSolverr (MyTorrents)";
		private const string UserAgent = "MyTorrents-Setup";

		public static readonly string ProgramData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
		public static readonly string AppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
		public static readonly string LocalAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
		public static readonly string JackettDir = System.IO.Path.Combine(ProgramData, "Jackett");
		public static readonly string FlareSolverrDir = System.IO.Path.Combine(LocalAppData, "MyTorrents", "FlareSolverr");
		/// 2026 (1.3.42): the programs this setup installed (one name per line) - only these are offered for removal
		public static readonly string InstalledFile = System.IO.Path.Combine(LocalAppData, "MyTorrents", "installed-by-setup.txt");

		public static void NoteInstalled(string name)
		{
			Directory.CreateDirectory(System.IO.Path.GetDirectoryName(InstalledFile));
			List<string> have = File.Exists(InstalledFile) ? File.ReadAllLines(InstalledFile).Select(l => l.Trim()).Where(l => l.Length > 0).ToList() : new List<string>();
			if (have.Contains(name)) return;
			have.Add(name);
			File.WriteAllLines(InstalledFile, have);
		}

		public static List<string> InstalledBySetup()
		{
			try
			{
				return File.Exists(InstalledFile) ? File.ReadAllLines(InstalledFile).Select(l => l.Trim()).Where(l => l.Length > 0).Distinct().ToList() : new List<string>();
			}
			catch
			{
				return new List<string>();
			}
		}

		public static readonly string WorkDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "MyTorrentsSetup");
		public static string MediaPortalDir => AppDomain.CurrentDomain.BaseDirectory;
		public static string ThisExe => System.Reflection.Assembly.GetExecutingAssembly().Location;

		static SetupCore()
		{
			try
			{
				ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12 | (SecurityProtocolType)12288; // TLS 1.2 + 1.3
			}
			catch
			{
				ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
			}
		}

		// ------------------------------------------------------------------ detection

		public static bool IsElevated
		{
			get
			{
				try
				{
					return new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
				}
				catch
				{
					return false;
				}
			}
		}

		/// Looks in Windows' list of installed programs (both 64- and 32-bit views, all users and this user).
		private static AppState FromUninstallList(string displayNameStart, string exeName)
		{
			foreach (RegistryHive hive in new[] { RegistryHive.LocalMachine, RegistryHive.CurrentUser })
			{
				foreach (RegistryView view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
				{
					try
					{
						using (RegistryKey root = RegistryKey.OpenBaseKey(hive, view))
						using (RegistryKey un = root.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"))
						{
							if (un == null) continue;
							foreach (string sub in un.GetSubKeyNames())
							{
								using (RegistryKey k = un.OpenSubKey(sub))
								{
									string name = k?.GetValue("DisplayName") as string;
									if (name == null || !name.StartsWith(displayNameStart, StringComparison.OrdinalIgnoreCase)) continue;
									AppState s = new AppState { Installed = true, Version = (k.GetValue("DisplayVersion") as string ?? "").Trim() };
									string loc = (k.GetValue("InstallLocation") as string ?? "").Trim().Trim('"');
									string icon = (k.GetValue("DisplayIcon") as string ?? "").Split(',')[0].Trim().Trim('"');
									if (loc.Length > 0 && File.Exists(System.IO.Path.Combine(loc, exeName))) s.Path = System.IO.Path.Combine(loc, exeName);
									else if (icon.EndsWith(exeName, StringComparison.OrdinalIgnoreCase) && File.Exists(icon)) s.Path = icon;
									return s;
								}
							}
						}
					}
					catch
					{
					}
				}
			}
			return new AppState();
		}

		private static AppState FindProgram(string displayName, string folder, string exeName)
		{
			AppState s = FromUninstallList(displayName, exeName);
			if (s.Path.Length == 0)
			{
				foreach (string pf in new[] { Environment.GetEnvironmentVariable("ProgramW6432"), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) })
				{
					if (string.IsNullOrEmpty(pf)) continue;
					string p = System.IO.Path.Combine(pf, folder, exeName);
					if (File.Exists(p))
					{
						s.Installed = true;
						s.Path = p;
						break;
					}
				}
			}
			if (s.Installed && s.Version.Length == 0 && s.Path.Length > 0)
			{
				s.Version = FileVersion(s.Path);
			}
			return s;
		}

		public static AppState QBittorrent() => FindProgram("qBittorrent", "qBittorrent", "qbittorrent.exe");

		public static AppState BiglyBT() => FindProgram("BiglyBT", "BiglyBT", "BiglyBT.exe");

		public static AppState Jackett()
		{
			foreach (string exe in new[] { "JackettConsole.exe", "JackettTray.exe", "JackettService.exe" })
			{
				string p = System.IO.Path.Combine(JackettDir, exe);
				if (File.Exists(p))
				{
					return new AppState { Installed = true, Path = p, Version = FileVersion(p) };
				}
			}
			return new AppState();
		}

		public static AppState FlareSolverr()
		{
			string exe = FindFlareSolverrExe();
			if (exe == null) return new AppState();
			string v = "";
			try
			{
				v = File.ReadAllText(System.IO.Path.Combine(FlareSolverrDir, "mytorrents-version.txt")).Trim();
			}
			catch
			{
			}
			return new AppState { Installed = true, Path = exe, Version = v };
		}

		internal static string FindFlareSolverrExe()
		{
			try
			{
				return Directory.Exists(FlareSolverrDir) ? Directory.GetFiles(FlareSolverrDir, "flaresolverr.exe", SearchOption.AllDirectories).FirstOrDefault() : null;
			}
			catch
			{
				return null;
			}
		}

		private static string FileVersion(string path)
		{
			try
			{
				FileVersionInfo fv = FileVersionInfo.GetVersionInfo(path);
				string v = (fv.ProductVersion ?? fv.FileVersion ?? "").Trim();
				int plus = v.IndexOfAny(new[] { '+', ' ' });
				return plus > 0 ? v.Substring(0, plus) : v;
			}
			catch
			{
				return "";
			}
		}

		/// True when "latest" is newer than "installed" (an unknown installed version counts as older).
		public static bool IsNewer(string latest, string installed)
		{
			Version a = ParseVersion(latest), b = ParseVersion(installed);
			if (a == null) return false;
			if (b == null) return true;
			return a > b;
		}

		private static Version ParseVersion(string v)
		{
			Match m = Regex.Match(v ?? "", @"\d+(\.\d+){1,3}");
			Version r;
			return m.Success && System.Version.TryParse(m.Value, out r) ? Normalise(r) : null;
		}

		private static Version Normalise(Version v) => new Version(v.Major, v.Minor, Math.Max(0, v.Build), Math.Max(0, v.Revision));

		// ------------------------------------------------------------------ web

		public static Release LatestGitHub(string repo, Func<string, bool> assetMatch)
		{
			string json = HttpGet("https://api.github.com/repos/" + repo + "/releases/latest", 30000, null);
			var d = new JavaScriptSerializer().DeserializeObject(json) as Dictionary<string, object>;
			Release r = new Release { Version = (d?["tag_name"] as string ?? "").TrimStart('v', 'V') };
			if (d != null && d.ContainsKey("assets") && d["assets"] is object[] assets)
			{
				foreach (object o in assets)
				{
					var a = o as Dictionary<string, object>;
					string name = a?["name"] as string ?? "";
					if (assetMatch(name))
					{
						r.Url = a["browser_download_url"] as string ?? "";
						r.Size = Convert.ToInt64(a["size"]);
						break;
					}
				}
			}
			if (r.Url.Length == 0) throw new Exception("no Windows download found in the latest " + repo + " release");
			return r;
		}

		public static string HttpGet(string url, int timeout, CookieContainer cookies)
		{
			HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
			req.UserAgent = UserAgent;
			req.Timeout = timeout;
			req.ReadWriteTimeout = timeout;
			req.CookieContainer = cookies;
			using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
			using (StreamReader sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
			{
				return sr.ReadToEnd();
			}
		}

		public static string HttpPost(string url, string body, string contentType, int timeout, CookieContainer cookies)
		{
			HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
			req.Method = "POST";
			req.UserAgent = UserAgent;
			req.ContentType = contentType;
			req.Timeout = timeout;
			req.ReadWriteTimeout = timeout;
			req.CookieContainer = cookies;
			byte[] data = Encoding.UTF8.GetBytes(body ?? "");
			using (Stream s = req.GetRequestStream())
			{
				s.Write(data, 0, data.Length);
			}
			using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
			using (StreamReader sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
			{
				return sr.ReadToEnd();
			}
		}

		/// Downloads to a file, reporting 0-100.
		public static void Download(string url, string dest, Action<int> progress)
		{
			Directory.CreateDirectory(System.IO.Path.GetDirectoryName(dest));
			string part = dest + ".part";
			HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
			req.UserAgent = UserAgent;
			req.Timeout = 60000;
			req.ReadWriteTimeout = 120000;
			using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
			using (Stream src = resp.GetResponseStream())
			using (FileStream fs = File.Create(part))
			{
				long total = resp.ContentLength, done = 0;
				byte[] buf = new byte[81920];
				int n, last = -1;
				while ((n = src.Read(buf, 0, buf.Length)) > 0)
				{
					fs.Write(buf, 0, n);
					done += n;
					int pct = total > 0 ? (int)(done * 100 / total) : 0;
					if (pct != last)
					{
						last = pct;
						progress?.Invoke(pct);
					}
				}
			}
			if (File.Exists(dest)) File.Delete(dest);
			File.Move(part, dest);
		}

		/// Waits until a URL answers (any HTTP reply counts, unless "accept" says otherwise).
		public static bool WaitForHttp(string url, int seconds, Func<HttpStatusCode, string, bool> accept)
		{
			DateTime end = DateTime.Now.AddSeconds(seconds);
			while (true)
			{
				try
				{
					string body = HttpGet(url, 5000, null);
					if (accept == null || accept(HttpStatusCode.OK, body)) return true;
				}
				catch (WebException ex) when (ex.Response is HttpWebResponse r)
				{
					if (accept == null || accept(r.StatusCode, "")) return true;
				}
				catch
				{
				}
				if (DateTime.Now > end) return false;
				Thread.Sleep(1500);
			}
		}

		// ------------------------------------------------------------------ elevated helper

		/// Runs admin steps. One Windows permission prompt for all of them (none when already elevated,
		/// e.g. when started from the MediaPortal installer). Returns "OK|step|message" / "FAIL|step|message" lines.
		public static List<string> RunAdmin(List<string> tasks)
		{
			if (tasks.Count == 0) return new List<string>();
			if (IsElevated)
			{
				return AdminTasks.Run(tasks);
			}
			Directory.CreateDirectory(WorkDir);
			string file = System.IO.Path.Combine(WorkDir, "admin-tasks.txt");
			string result = file + ".result";
			File.WriteAllLines(file, tasks);
			if (File.Exists(result)) File.Delete(result);
			try
			{
				ProcessStartInfo psi = new ProcessStartInfo(ThisExe, "/admin \"" + file + "\"") { UseShellExecute = true, Verb = "runas" };
				using (Process p = Process.Start(psi))
				{
					p.WaitForExit();
				}
			}
			catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
			{
				return tasks.Select(t => "FAIL|" + t.Split('|')[0] + "|you answered No to the Windows permission question").ToList();
			}
			return File.Exists(result) ? File.ReadAllLines(result).ToList() : tasks.Select(t => "FAIL|" + t.Split('|')[0] + "|the admin step did not finish").ToList();
		}

		// ------------------------------------------------------------------ Jackett

		public static string JackettConfigFile()
		{
			foreach (string dir in new[] { JackettDir, System.IO.Path.Combine(LocalAppData, "Jackett"), System.IO.Path.Combine(AppData, "Jackett") })
			{
				string f = System.IO.Path.Combine(dir, "ServerConfig.json");
				if (File.Exists(f)) return f;
			}
			return null;
		}

		/// Reads APIKey and Port from Jackett's ServerConfig.json.
		public static bool ReadJackettConfig(out string apiKey, out int port)
		{
			apiKey = "";
			port = 9117;
			string f = JackettConfigFile();
			if (f == null) return false;
			try
			{
				var d = new JavaScriptSerializer().DeserializeObject(File.ReadAllText(f)) as Dictionary<string, object>;
				if (d == null) return false;
				foreach (var kv in d)
				{
					if (kv.Key.Equals("APIKey", StringComparison.OrdinalIgnoreCase)) apiKey = Convert.ToString(kv.Value) ?? "";
					if (kv.Key.Equals("Port", StringComparison.OrdinalIgnoreCase) && kv.Value != null) port = Convert.ToInt32(kv.Value);
				}
				return apiKey.Length > 0;
			}
			catch
			{
				return false;
			}
		}

		/// Sets FlareSolverrUrl in ServerConfig.json text. Returns null when nothing needed changing.
		public static string WithFlareSolverr(string json, string url)
		{
			Regex re = new Regex("\"FlareSolverrUrl\"\\s*:\\s*(null|\"[^\"]*\")", RegexOptions.IgnoreCase);
			Match m = re.Match(json);
			string value = "\"" + url + "\"";
			if (m.Success)
			{
				return m.Groups[1].Value == value ? null : json.Substring(0, m.Groups[1].Index) + value + json.Substring(m.Groups[1].Index + m.Groups[1].Length);
			}
			int brace = json.IndexOf('{');
			if (brace < 0) throw new InvalidDataException("ServerConfig.json is not valid");
			return json.Substring(0, brace + 1) + "\r\n  \"FlareSolverrUrl\": " + value + "," + json.Substring(brace + 1);
		}

		public static string JackettBase(int port) => "http://127.0.0.1:" + port;

		/// Number of configured Jackett sites, or -1 when Jackett does not answer / the key is wrong.
		public static int JackettIndexerCount(int port, string apiKey)
		{
			try
			{
				string xml = HttpGet(JackettBase(port) + "/api/v2.0/indexers/all/results/torznab/api?apikey=" + Uri.EscapeDataString(apiKey) + "&t=indexers&configured=true", 30000, null);
				XmlDocument doc = new XmlDocument();
				doc.LoadXml(xml);
				if (doc.SelectSingleNode("//error") != null) return -1;
				return doc.SelectNodes("//indexer").Count;
			}
			catch
			{
				return -1;
			}
		}

		// ------------------------------------------------------------------ FlareSolverr

		public static void InstallFlareSolverr(string zip, string version, Action<string> status)
		{
			StopFlareSolverr();
			string tmp = FlareSolverrDir + ".new";
			if (Directory.Exists(tmp)) Directory.Delete(tmp, true);
			status("unpacking - Windows checks each file, this can take a few minutes...");
			ExtractZip(zip, tmp);
			if (Directory.Exists(FlareSolverrDir))
			{
				for (int i = 0; ; i++)
				{
					try
					{
						Directory.Delete(FlareSolverrDir, true);
						break;
					}
					catch when (i < 5)
					{
						Thread.Sleep(2000); // its browser may take a moment to close
					}
				}
			}
			Directory.Move(tmp, FlareSolverrDir);
			File.WriteAllText(System.IO.Path.Combine(FlareSolverrDir, "mytorrents-version.txt"), version);
			EnableFlareSolverrAutostart();
		}

		public static void EnableFlareSolverrAutostart()
		{
			using (RegistryKey run = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"))
			{
				run.SetValue(RunKeyName, "\"" + ThisExe + "\" /start-flaresolverr");
			}
		}

		/// The programs inside FlareSolverr that Windows Firewall might ask about (where they will be, even before installing).
		public static List<string> FlareSolverrPrograms()
		{
			string exe = FindFlareSolverrExe() ?? System.IO.Path.Combine(FlareSolverrDir, "flaresolverr", "flaresolverr.exe");
			string dir = System.IO.Path.GetDirectoryName(exe);
			string chrome = null;
			try
			{
				chrome = Directory.Exists(dir) ? Directory.GetFiles(dir, "chrome.exe", SearchOption.AllDirectories).FirstOrDefault() : null;
			}
			catch
			{
			}
			return new List<string> { exe, chrome ?? System.IO.Path.Combine(dir, "_internal", "chrome", "chrome.exe") };
		}

		public static List<string> FirewallTasks() => FlareSolverrPrograms().Select(p => "firewall-quiet|" + p).ToList();

		public static bool FlareSolverrRunning() => Process.GetProcessesByName("flaresolverr").Length > 0;

		/// Starts FlareSolverr with no window (used at Windows sign-in through the Run key, and by the assistant).
		public static bool StartFlareSolverr()
		{
			if (FlareSolverrRunning()) return true;
			string exe = FindFlareSolverrExe();
			if (exe == null) return false;
			string log = System.IO.Path.Combine(FlareSolverrDir, "flaresolverr.log");
			try
			{
				if (File.Exists(log) && new FileInfo(log).Length > 2000000) File.Delete(log);
			}
			catch
			{
			}
			// cmd keeps FlareSolverr's messages in flaresolverr.log, so a failed start can be seen
			ProcessStartInfo psi = new ProcessStartInfo(Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe",
				"/d /c \"\"" + exe + "\" >> \"" + log + "\" 2>&1\"")
			{
				UseShellExecute = false,
				CreateNoWindow = true,
				WindowStyle = ProcessWindowStyle.Hidden,
				WorkingDirectory = System.IO.Path.GetDirectoryName(exe)
			};
			// listen on this PC only: only Jackett (on this PC) uses it, and Windows Firewall then has nothing to ask about
			psi.EnvironmentVariables["HOST"] = "127.0.0.1";
			Process.Start(psi);
			Log("FlareSolverr started (hidden, this PC only), log: " + log);
			return true;
		}

		public static void StopFlareSolverr()
		{
			if (!FlareSolverrRunning()) return;
			try
			{
				using (Process p = Process.Start(new ProcessStartInfo("taskkill", "/F /T /IM flaresolverr.exe") { UseShellExecute = false, CreateNoWindow = true }))
				{
					p.WaitForExit(20000);
				}
			}
			catch
			{
			}
		}

		public static bool FlareSolverrAnswers(int seconds) =>
			WaitForHttp(FlareSolverrUrl + "/", seconds, (code, body) => code == HttpStatusCode.OK && body.IndexOf("FlareSolverr", StringComparison.OrdinalIgnoreCase) >= 0);

		public static void ExtractZip(string zip, string dest)
		{
			Directory.CreateDirectory(dest);
			string root = System.IO.Path.GetFullPath(dest) + System.IO.Path.DirectorySeparatorChar;
			using (ZipArchive z = ZipFile.OpenRead(zip))
			{
				foreach (ZipArchiveEntry e in z.Entries)
				{
					string target = System.IO.Path.GetFullPath(System.IO.Path.Combine(dest, e.FullName));
					if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase)) continue; // ignore unsafe paths
					if (e.FullName.EndsWith("/") || e.FullName.EndsWith("\\"))
					{
						Directory.CreateDirectory(target);
						continue;
					}
					Directory.CreateDirectory(System.IO.Path.GetDirectoryName(target));
					e.ExtractToFile(target, true);
				}
			}
		}

		// ------------------------------------------------------------------ qBittorrent

		public static string QBittorrentIni => System.IO.Path.Combine(AppData, "qBittorrent", "qBittorrent.ini");

		public static bool QBittorrentRunning() => Process.GetProcessesByName("qbittorrent").Length > 0;

		/// qBittorrent's stored form of a Web UI password: PBKDF2-HMAC-SHA512, 100000 rounds, 16-byte salt, 64-byte key.
		public static string QBittorrentPasswordHash(string password)
		{
			byte[] salt = new byte[16];
			using (System.Security.Cryptography.RandomNumberGenerator rng = System.Security.Cryptography.RandomNumberGenerator.Create())
			{
				rng.GetBytes(salt);
			}
			byte[] key;
			using (System.Security.Cryptography.Rfc2898DeriveBytes kdf = new System.Security.Cryptography.Rfc2898DeriveBytes(password, salt, 100000, System.Security.Cryptography.HashAlgorithmName.SHA512))
			{
				key = kdf.GetBytes(64);
			}
			return "\"@ByteArray(" + Convert.ToBase64String(salt) + ":" + Convert.ToBase64String(key) + ")\"";
		}

		public static string RandomPassword()
		{
			const string chars = "abcdefghijkmnpqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ23456789";
			byte[] b = new byte[16];
			using (System.Security.Cryptography.RandomNumberGenerator rng = System.Security.Cryptography.RandomNumberGenerator.Create())
			{
				rng.GetBytes(b);
			}
			return new string(b.Select(x => chars[x % chars.Length]).ToArray());
		}

		/// Web UI login MyTorrents will use as well as the "no login from this PC" bypass.
		public sealed class QbLogin
		{
			public string User = "admin";
			public string Password = "";   // filled when setup made the password
			public bool HadPassword;        // the user already set one - ask for it
		}

		/// Turns on the Web UI with no login for this PC, and makes sure it has a password
		/// (qBittorrent 4.6+ will not save its options without one). Returns the Web UI port.
		public static int ConfigureQBittorrent()
		{
			return ConfigureQBittorrent(new QbLogin());
		}

		public static int ConfigureQBittorrent(QbLogin login)
		{
			string ini = QBittorrentIni;
			List<string> lines = File.Exists(ini) ? File.ReadAllLines(ini).ToList() : new List<string> { "[LegalNotice]", "Accepted=true", "" };
			if (File.Exists(ini) && !File.Exists(ini + ".before-mytorrents"))
			{
				File.Copy(ini, ini + ".before-mytorrents");
			}
			string portText = QbGet(lines, "Port");
			int port;
			if (!int.TryParse(portText, out port) || port <= 0) port = 8080;
			QbSet(lines, "Enabled", "true");
			QbSet(lines, "Port", port.ToString());
			QbSet(lines, "LocalHostAuth", "false");
			string user = QbGet(lines, "Username");
			if (string.IsNullOrEmpty(user))
			{
				user = "admin";
			}
			QbSet(lines, "Username", user);
			login.User = user;
			string hash = QbGet(lines, "Password_PBKDF2");
			if (string.IsNullOrEmpty(hash) || hash.Trim('"').Length == 0)
			{
				// qBittorrent 5 does not start its Web UI at all without a login ("WebUI: Credentials are not set")
				login.Password = RandomPassword();
				QbSet(lines, "Password_PBKDF2", QBittorrentPasswordHash(login.Password));
			}
			else
			{
				QbSet(lines, "Password_PBKDF2", hash); // the same in both places
				login.HadPassword = true;
			}
			// 2026 (1.3.53): the first time only - qBittorrent starts with Windows, tucked away in the tray.
			// Later runs of Setup leave whatever you have changed since alone.
			bool firstTime = !StartupDone;
			if (firstTime)
			{
				IniSet(lines, "Preferences", @"General\StartMinimized", "true");
				IniSet(lines, "Preferences", @"General\SystemTrayEnabled", "true");
				IniSet(lines, "Preferences", @"General\MinimizeToTray", "true");
				IniSet(lines, "Preferences", @"General\CloseToTray", "true");
			}
			Directory.CreateDirectory(System.IO.Path.GetDirectoryName(ini));
			File.WriteAllLines(ini, lines, new UTF8Encoding(false));
			if (firstTime) QBittorrentStartWithWindows();
			return port;
		}

		private const string MyKey = @"Software\MyTorrents";

		/// 2026 (1.3.53): true once Setup has set qBittorrent to start with Windows (so it is only done once)
		private static bool StartupDone
		{
			get
			{
				try
				{
					using (RegistryKey k = Registry.CurrentUser.OpenSubKey(MyKey))
						return k != null && (k.GetValue("QbStartupSet") as string) == "yes";
				}
				catch
				{
					return false;
				}
			}
		}

		/// 2026 (1.3.53): the same as qBittorrent's own "Start qBittorrent on Windows start up" tick
		public static void QBittorrentStartWithWindows()
		{
			try
			{
				AppState app = QBittorrent();
				string exe = app.Installed ? app.Path : null;
				if (!string.IsNullOrEmpty(exe) && Directory.Exists(exe)) exe = System.IO.Path.Combine(exe, "qbittorrent.exe");
				if (string.IsNullOrEmpty(exe) || !File.Exists(exe)) return;
				using (RegistryKey run = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"))
				{
					if (run.GetValue("qBittorrent") == null) run.SetValue("qBittorrent", "\"" + exe + "\"");
				}
				using (RegistryKey k = Registry.CurrentUser.CreateSubKey(MyKey))
					k.SetValue("QbStartupSet", "yes");
			}
			catch
			{
			}
		}

		/// qBittorrent 5 keeps Web UI settings in [WebUI] (Port=...); qBittorrent 4 in [Preferences] (WebUI\\Port=...).
		/// Read the new place first, write both, so either version uses them.
		private static string QbGet(List<string> lines, string key)
		{
			string v = IniGet(lines, "WebUI", key);
			return string.IsNullOrEmpty(v) ? IniGet(lines, "Preferences", @"WebUI\" + key) : v;
		}

		private static void QbSet(List<string> lines, string key, string value)
		{
			IniSet(lines, "WebUI", key, value);
			IniSet(lines, "Preferences", @"WebUI\" + key, value);
		}

		public static string IniGet(List<string> lines, string section, string key)
		{
			bool inSection = false;
			foreach (string raw in lines)
			{
				string l = raw.Trim();
				if (l.StartsWith("[")) inSection = l.Equals("[" + section + "]", StringComparison.OrdinalIgnoreCase);
				else if (inSection && l.StartsWith(key + "=", StringComparison.OrdinalIgnoreCase)) return l.Substring(key.Length + 1);
			}
			return null;
		}

		public static void IniSet(List<string> lines, string section, string key, string value)
		{
			int start = lines.FindIndex(l => l.Trim().Equals("[" + section + "]", StringComparison.OrdinalIgnoreCase));
			if (start < 0)
			{
				if (lines.Count > 0 && lines[lines.Count - 1].Trim().Length > 0) lines.Add("");
				lines.Add("[" + section + "]");
				lines.Add(key + "=" + value);
				return;
			}
			int end = lines.FindIndex(start + 1, l => l.Trim().StartsWith("["));
			if (end < 0) end = lines.Count;
			for (int i = start + 1; i < end; i++)
			{
				if (lines[i].Trim().StartsWith(key + "=", StringComparison.OrdinalIgnoreCase))
				{
					lines[i] = key + "=" + value;
					return;
				}
			}
			int insert = end;
			while (insert > start + 1 && lines[insert - 1].Trim().Length == 0) insert--;
			lines.Insert(insert, key + "=" + value);
		}

		/// True when qBittorrent accepts this Web UI login. qBittorrent 4 answers "Ok."; newer versions may answer
		/// with no text at all - only "Fails." (or a 401/403) means a wrong login.
		public static bool QBittorrentLoginOk(int port, string user, string password)
		{
			try
			{
				HttpWebRequest req = (HttpWebRequest)WebRequest.Create("http://127.0.0.1:" + port + "/api/v2/auth/login");
				req.Method = "POST";
				req.ContentType = "application/x-www-form-urlencoded";
				req.UserAgent = UserAgent;
				req.Timeout = 15000;
				req.CookieContainer = new CookieContainer();
				byte[] data = Encoding.UTF8.GetBytes("username=" + Uri.EscapeDataString(user ?? "") + "&password=" + Uri.EscapeDataString(password ?? ""));
				using (Stream st = req.GetRequestStream())
				{
					st.Write(data, 0, data.Length);
				}
				using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
				using (StreamReader sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
				{
					string body = sr.ReadToEnd().Trim();
					int code = (int)resp.StatusCode;
					Log("qBittorrent login check: HTTP " + code + " '" + (body.Length > 20 ? body.Substring(0, 20) : body) + "'");
					return code >= 200 && code < 300 && !body.StartsWith("Fails", StringComparison.OrdinalIgnoreCase);
				}
			}
			catch (WebException ex) when (ex.Response is HttpWebResponse r)
			{
				Log("qBittorrent login check: HTTP " + (int)r.StatusCode);
				return false;
			}
			catch (Exception ex)
			{
				Log("qBittorrent login check failed: " + ex.Message);
				return false;
			}
		}

		/// "MyTorrents Configuration.exe /check-updates": no windows - writes what can be updated to
		/// MyTorrents.updates.txt (next to MediaPortal.xml), which MediaPortal shows once as a reminder.
		public static int CheckUpdatesQuietly()
		{
			List<string> found = new List<string>();
			string backend = "";
			try
			{
				backend = (Sohvaperuna.MyTorrents.Configuration.Instance().Settings["uTorrent.Backend"] as string ?? "").ToLowerInvariant();
			}
			catch
			{
			}
			try
			{
				AppState c = backend == "qbittorrent" ? QBittorrent() : backend == "biglybt" ? BiglyBT() : null;
				if (c != null && c.Installed)
				{
					string latest = WingetLatest(backend == "qbittorrent" ? QbWingetId : BiglyWingetId);
					if (IsNewer(latest, c.Version)) found.Add((backend == "qbittorrent" ? "qBittorrent " : "BiglyBT ") + latest);
				}
				AppState j = Jackett();
				if (j.Installed)
				{
					Release r = LatestGitHub("Jackett/Jackett", n => n.Equals("Jackett.Installer.Windows.exe", StringComparison.OrdinalIgnoreCase));
					if (IsNewer(r.Version, j.Version)) found.Add("Jackett " + r.Version);
				}
				AppState f = FlareSolverr();
				if (f.Installed)
				{
					Release r = LatestGitHub("FlareSolverr/FlareSolverr", n => n.IndexOf("windows", StringComparison.OrdinalIgnoreCase) >= 0 && n.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));
					if (IsNewer(r.Version, f.Version)) found.Add("FlareSolverr " + r.Version);
				}
			}
			catch (Exception ex)
			{
				Log("Update check: " + ex.Message);
			}
			try
			{
				string file = MediaPortal.Configuration.Config.GetFile(MediaPortal.Configuration.Config.Dir.Config, "MyTorrents.updates.txt");
				File.WriteAllLines(file, new[] { "checked=" + DateTime.Now.ToString("yyyy-MM-dd") }.Concat(found.Select(x => "update=" + x)));
				Log("Update check: " + (found.Count == 0 ? "everything up to date" : string.Join(", ", found)));
			}
			catch (Exception ex)
			{
				Log("Update check: could not save the result: " + ex.Message);
			}
			return 0;
		}

		public const string QbWingetId = "qBittorrent.qBittorrent";
		public const string BiglyWingetId = "BiglySoftware.BiglyBT";

		/// The newest version Windows' package manager (winget) offers, or "" when it cannot tell.
		public static string WingetLatest(string id)
		{
			try
			{
				ProcessStartInfo psi = new ProcessStartInfo("winget", "show -e --id " + id + " --accept-source-agreements")
				{
					UseShellExecute = false,
					CreateNoWindow = true,
					RedirectStandardOutput = true,
					RedirectStandardError = true,
					StandardOutputEncoding = Encoding.UTF8
				};
				using (Process p = Process.Start(psi))
				{
					string output = p.StandardOutput.ReadToEnd();
					p.WaitForExit(120000);
					Match m = Regex.Match(output, @"^\s*Version:\s*(\S+)", RegexOptions.Multiline | RegexOptions.IgnoreCase);
					Log("winget show " + id + ": " + (m.Success ? m.Groups[1].Value : "no version found"));
					return m.Success ? m.Groups[1].Value : "";
				}
			}
			catch (Exception ex)
			{
				Log("winget show " + id + " failed: " + ex.Message);
				return "";
			}
		}

		/// Asks qBittorrent to close itself cleanly (so it can be updated). True when it has closed.
		public static bool QBittorrentShutdown(int port, string user, string password)
		{
			try
			{
				CookieContainer jar = new CookieContainer();
				if (!string.IsNullOrEmpty(password))
				{
					try
					{
						HttpPost("http://127.0.0.1:" + port + "/api/v2/auth/login", "username=" + Uri.EscapeDataString(user ?? "") + "&password=" + Uri.EscapeDataString(password), "application/x-www-form-urlencoded", 15000, jar);
					}
					catch
					{
					}
				}
				HttpPost("http://127.0.0.1:" + port + "/api/v2/app/shutdown", "", "application/x-www-form-urlencoded", 15000, jar);
			}
			catch (Exception ex)
			{
				Log("qBittorrent shutdown request: " + ex.Message);
			}
			for (int i = 0; i < 60 && QBittorrentRunning(); i++)
			{
				Thread.Sleep(1000);
			}
			return !QBittorrentRunning();
		}

		/// qBittorrent's default download folder ("" when it cannot be read).
		public static string QBittorrentSaveFolder(int port, string user, string password)
		{
			try
			{
				CookieContainer jar = new CookieContainer();
				if (!string.IsNullOrEmpty(password))
				{
					HttpPost("http://127.0.0.1:" + port + "/api/v2/auth/login", "username=" + Uri.EscapeDataString(user ?? "") + "&password=" + Uri.EscapeDataString(password),
						"application/x-www-form-urlencoded", 15000, jar);
				}
				return HttpGet("http://127.0.0.1:" + port + "/api/v2/app/defaultSavePath", 15000, jar).Trim();
			}
			catch
			{
				return "";
			}
		}

		/// 2026: stop seeding at a share ratio of 1.1 (each torrent gives back a little more than it took, then stops) -
		/// only when no limit has been set yet, so a choice someone made themselves is kept. Returns what it did.
		public static string QBittorrentShareLimit(int port, string user, string password, double ratio)
		{
			try
			{
				CookieContainer jar = new CookieContainer();
				if (!string.IsNullOrEmpty(password))
				{
					HttpPost("http://127.0.0.1:" + port + "/api/v2/auth/login", "username=" + Uri.EscapeDataString(user ?? "") + "&password=" + Uri.EscapeDataString(password),
						"application/x-www-form-urlencoded", 15000, jar);
				}
				string prefs = HttpGet("http://127.0.0.1:" + port + "/api/v2/app/preferences", 15000, jar);
				var p = new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<Dictionary<string, object>>(prefs);
				object enabled;
				if (p != null && p.TryGetValue("max_ratio_enabled", out enabled) && enabled is bool && (bool)enabled)
				{
					return "already set to " + (p.ContainsKey("max_ratio") ? Convert.ToString(p["max_ratio"], System.Globalization.CultureInfo.InvariantCulture) : "?") + " - left as it is";
				}
				// max_ratio_act 0 = stop (pause) the torrent; the files stay where they are
				string json = "{\"max_ratio_enabled\":true,\"max_ratio\":" + ratio.ToString(System.Globalization.CultureInfo.InvariantCulture) + ",\"max_ratio_act\":0}";
				HttpPost("http://127.0.0.1:" + port + "/api/v2/app/setPreferences", "json=" + Uri.EscapeDataString(json), "application/x-www-form-urlencoded", 15000, jar);
				return "set to " + ratio.ToString(System.Globalization.CultureInfo.InvariantCulture);
			}
			catch (Exception ex)
			{
				return "could not be set: " + ex.Message;
			}
		}

		/// qBittorrent Web UI reply: 200 = works without a login, 403 = it wants a login, null = no answer.
		public static HttpStatusCode? QBittorrentStatus(int port, int seconds)
		{
			HttpStatusCode? got = null;
			WaitForHttp("http://127.0.0.1:" + port + "/api/v2/app/version", seconds, (code, body) =>
			{
				got = code;
				return true;
			});
			return got;
		}

		public static string QBittorrentBypassSetting()
		{
			try
			{
				return QbGet(File.ReadAllLines(QBittorrentIni).ToList(), "LocalHostAuth") ?? "(not set)";
			}
			catch (Exception ex)
			{
				return "(" + ex.Message + ")";
			}
		}

		/// Setup log: %TEMP%\MyTorrentsSetup\setup.log
		public static void Log(string text)
		{
			try
			{
				Directory.CreateDirectory(WorkDir);
				File.AppendAllText(System.IO.Path.Combine(WorkDir, "setup.log"), DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + text + Environment.NewLine);
			}
			catch
			{
			}
		}

		// ------------------------------------------------------------------ BiglyBT

		public static string BiglyConfigFile => System.IO.Path.Combine(AppData, "BiglyBT", "biglybt.config");

		public static bool BiglyRunning() => Process.GetProcessesByName("BiglyBT").Length > 0;

		/// The Web Remote plugin is there (per-user or with the program).
		public static bool WebRemoteInstalled(AppState bigly)
		{
			List<string> dirs = new List<string> { System.IO.Path.Combine(AppData, "BiglyBT", "plugins", "xmwebui") };
			if (bigly.Path.Length > 0) dirs.Add(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(bigly.Path), "plugins", "xmwebui"));
			return dirs.Any(d => Directory.Exists(d) && Directory.GetFiles(d, "xmwebui_*.jar").Length > 0);
		}

		/// Puts the Web Remote plugin (shipped with MyTorrents) where BiglyBT finds it.
		public static void InstallWebRemote()
		{
			string src = System.IO.Path.Combine(MediaPortalDir, "plugins", "windows", "MyTorrents.Setup");
			string zip = Directory.Exists(src) ? Directory.GetFiles(src, "xmwebui_*.zip").OrderBy(f => f).LastOrDefault() : null;
			if (zip == null) throw new FileNotFoundException("the Web Remote plugin is missing from MyTorrents (plugins\\windows\\MyTorrents.Setup)");
			string dest = System.IO.Path.Combine(AppData, "BiglyBT", "plugins", "xmwebui");
			ExtractZip(zip, dest);
			File.Copy(zip, System.IO.Path.Combine(dest, System.IO.Path.GetFileName(zip)), true);
		}

		/// Web Remote: local access, no password, any host name. Returns the Web Remote port.
		public static int ConfigureBiglyBT()
		{
			string file = BiglyConfigFile;
			SortedDictionary<byte[], object> cfg;
			if (File.Exists(file))
			{
				cfg = Bencode.Decode(File.ReadAllBytes(file)) as SortedDictionary<byte[], object>;
				if (cfg == null) throw new InvalidDataException("biglybt.config could not be read");
				if (!File.Exists(file + ".before-mytorrents")) File.Copy(file, file + ".before-mytorrents");
			}
			else
			{
				cfg = new SortedDictionary<byte[], object>(Bencode.ByteComparer.Instance);
				Directory.CreateDirectory(System.IO.Path.GetDirectoryName(file));
			}
			int port = 9091;
			object p;
			if (cfg.TryGetValue(Bencode.Key("Plugin.xmwebui.Port"), out p) && p is long lp && lp > 0) port = (int)lp;
			cfg[Bencode.Key("Plugin.xmwebui.Access")] = Bencode.Key("local");
			cfg[Bencode.Key("Plugin.xmwebui.Password Enable")] = 0L;
			cfg[Bencode.Key("Plugin.xmwebui.Password Disabled Whitelist")] = Bencode.Key("*");
			if (!cfg.ContainsKey(Bencode.Key("Plugin.xmwebui.Config Migrated"))) cfg[Bencode.Key("Plugin.xmwebui.Config Migrated")] = 1L;
			if (!cfg.ContainsKey(Bencode.Key("Plugin.xmwebui.Pairing Migrated"))) cfg[Bencode.Key("Plugin.xmwebui.Pairing Migrated")] = 1L;
			File.WriteAllBytes(file, Bencode.Encode(cfg));
			return port;
		}

		/// BiglyBT Web Remote answers (409 = ready, it wants a session id; 401 would mean a password is still on).
		public static HttpStatusCode? BiglyAnswers(int port, int seconds)
		{
			HttpStatusCode? got = null;
			WaitForHttp("http://127.0.0.1:" + port + "/transmission/rpc", seconds, (code, body) =>
			{
				got = code;
				return true;
			});
			return got;
		}

		// ------------------------------------------------------------------ starting programs

		/// Starts a program as the normal user even when the assistant runs as administrator.
		public static void StartAsUser(string exe)
		{
			if (string.IsNullOrEmpty(exe) || !File.Exists(exe)) return;
			if (IsElevated)
			{
				Process.Start("explorer.exe", "\"" + exe + "\"");
			}
			else
			{
				Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true, WorkingDirectory = System.IO.Path.GetDirectoryName(exe) });
			}
		}

		// ------------------------------------------------------------------ MyTorrents settings

		/// 2026 (1.3.57): true when something on this PC answers on that port (a torrent program's web remote)
		public static bool PortAnswers(int port)
		{
			try
			{
				using (System.Net.Sockets.TcpClient c = new System.Net.Sockets.TcpClient())
				{
					IAsyncResult r = c.BeginConnect("127.0.0.1", port, null, null);
					bool ok = r.AsyncWaitHandle.WaitOne(2000) && c.Connected;
					return ok;
				}
			}
			catch
			{
				return false;
			}
		}

		public static void SaveMyTorrents(string backend, int port, string jackettUrl, string jackettKey)
		{
			SaveMyTorrents(backend, port, jackettUrl, jackettKey, "", "");
		}

		public static void SaveMyTorrents(string backend, int port, string jackettUrl, string jackettKey, string user, string password)
		{
			if (backend != null)
			{
				Sohvaperuna.MyTorrents.Configuration c = Sohvaperuna.MyTorrents.Configuration.Instance();
				c.Settings["uTorrent.Backend"] = backend;
				c.Settings["uTorrent.Server.IP"] = "127.0.0.1";
				c.Settings["uTorrent.Server.Port"] = port.ToString();
				c.Settings["uTorrent.Username"] = user ?? "";
				c.Settings["uTorrent.Password"] = password ?? "";
				c.Save();
				Sohvaperuna.MyTorrents.Configuration.SetExtra("Setup.Client", backend);
				Sohvaperuna.MyTorrents.Configuration.SetExtra("Setup.Done", "true");
			}
			if (jackettUrl != null)
			{
				string file = MediaPortal.Configuration.Config.GetFile(MediaPortal.Configuration.Config.Dir.Config, "MyTorrents.xml");
				XmlDocument doc = new XmlDocument();
				try
				{
					if (File.Exists(file)) doc.Load(file);
				}
				catch
				{
					doc = new XmlDocument();
				}
				if (doc.SelectSingleNode("config") == null)
				{
					doc.LoadXml("<config><jackett/><iconmapping/><feed/><sites/><searchengines/></config>");
				}
				XmlNode root = doc.SelectSingleNode("config");
				XmlNode j = root.SelectSingleNode("jackett") ?? root.PrependChild(doc.CreateElement("jackett"));
				j.RemoveAll();
				j.AppendChild(doc.CreateElement("enabled")).InnerText = "true";
				j.AppendChild(doc.CreateElement("url")).InnerText = jackettUrl;
				j.AppendChild(doc.CreateElement("apikey")).InnerText = jackettKey ?? "";
				doc.Save(file);
			}
		}
	}

	/// <summary>Talks to Jackett's own web page API (the same calls its "+ Add indexer" page makes).</summary>
	internal sealed class JackettApi
	{
		private readonly CookieContainer _cookies = new CookieContainer();
		private readonly string _base;
		private readonly JavaScriptSerializer _js = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };

		public JackettApi(int port)
		{
			_base = SetupCore.JackettBase(port);
		}

		/// With no admin password set, opening the dashboard gives the login cookie.
		public void Login()
		{
			try
			{
				SetupCore.HttpGet(_base + "/UI/Dashboard", 30000, _cookies);
			}
			catch (WebException)
			{
			}
		}

		public sealed class Site
		{
			public string Id, Name, Language, Description, Link;
			public bool Adult, TvOrMovies;
			public bool Configured;      // Jackett already searches it
			public bool Public = true;   // false = needs an account
			public string LastError = ""; // Jackett's last problem with this site, "" = fine
			public List<string> Links = new List<string>(); // its web addresses
			public bool IsOwn;           // one of MyTorrents' own sites (not Jackett); Id = "own:" + search address
			public string OwnUrl = "";
		}

		private static readonly System.Text.RegularExpressions.Regex AdultWords =
			new System.Text.RegularExpressions.Regex(@"\b(xxx|porn|adult|hentai|jav|doujin\w*|erotic\w*)\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

		/// Jackett lists each site's categories: 2000 = Movies, 5000 = TV, 6000 = XXX.
		private static void Classify(Site site, Dictionary<string, object> d)
		{
			List<int> cats = new List<int>();
			if (d.ContainsKey("caps") && d["caps"] is object[] caps)
			{
				foreach (object c in caps)
				{
					int id;
					if (c is Dictionary<string, object> cd && cd.ContainsKey("ID") && int.TryParse(Convert.ToString(cd["ID"]), out id) && id < 100000) cats.Add(id);
				}
			}
			site.TvOrMovies = cats.Any(c => (c >= 2000 && c < 3000) || (c >= 5000 && c < 6000));
			site.Adult = (cats.Count > 0 && cats.All(c => c >= 6000 && c < 7000)) || AdultWords.IsMatch(site.Name + " " + site.Description);
		}

		/// Public sites (no account needed) that are not added yet.
		public List<Site> PublicSites() => AllSites().Where(x => x.Public && !x.Configured).ToList();

		/// The sites Jackett already searches (any kind) plus the public ones it could add.
		public List<Site> AllSites()
		{
			string json = SetupCore.HttpGet(_base + "/api/v2.0/indexers?configured=false", 60000, _cookies);
			List<Site> list = new List<Site>();
			if (!(_js.DeserializeObject(json) is object[] arr)) throw new Exception("Jackett did not return its site list");
			foreach (object o in arr)
			{
				var d = o as Dictionary<string, object>;
				if (d == null) continue;
				string type = Get(d, "type");
				bool configured = d.ContainsKey("configured") && d["configured"] is bool b && b;
				bool isPublic = type.Equals("public", StringComparison.OrdinalIgnoreCase);
				List<string> links = new List<string> { Get(d, "site_link") };
				if (d.ContainsKey("alternativesitelinks") && d["alternativesitelinks"] is object[] alt) links.AddRange(alt.Select(x => Convert.ToString(x)));
				links = links.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
				// sites only reachable over I2P or Tor need extra software - not offered
				if (!configured && links.Any(x => x.IndexOf(".i2p", StringComparison.OrdinalIgnoreCase) >= 0 || x.IndexOf(".onion", StringComparison.OrdinalIgnoreCase) >= 0)) continue;
				Site site = new Site
				{
					Id = Get(d, "id"), Name = Get(d, "name"), Language = Get(d, "language"), Description = Get(d, "description"), Link = Get(d, "site_link"),
					Configured = configured, Public = isPublic, LastError = configured ? Get(d, "last_error").Trim() : "", Links = links
				};
				Classify(site, d);
				list.Add(site);
			}
			return list.OrderBy(s => s.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
		}

		/// The settings Jackett asks for when adding a site (login fields, help text, options).
		public object[] GetConfig(string id)
		{
			string json = SetupCore.HttpGet(_base + "/api/v2.0/indexers/" + Uri.EscapeDataString(id) + "/config", 60000, _cookies);
			return _js.DeserializeObject(json) as object[] ?? new object[0];
		}

		/// Adds the site with these settings. Jackett logs in to test them; its error is thrown back.
		public void SaveConfig(string id, object[] config)
		{
			string url = _base + "/api/v2.0/indexers/" + Uri.EscapeDataString(id) + "/config";
			try
			{
				SetupCore.HttpPost(url, _js.Serialize(config), "application/json", 120000, _cookies);
			}
			catch (WebException ex) when (ex.Response is HttpWebResponse r)
			{
				string msg = "";
				try
				{
					using (StreamReader sr = new StreamReader(r.GetResponseStream()))
					{
						msg = sr.ReadToEnd();
					}
					var d = _js.DeserializeObject(msg) as Dictionary<string, object>;
					if (d != null && d.ContainsKey("error")) msg = Convert.ToString(d["error"]);
				}
				catch
				{
				}
				throw new Exception(msg.Length > 0 ? FirstLine(msg) : ex.Message);
			}
		}

		/// Stops Jackett searching a site (its settings are deleted from Jackett).
		public void Remove(string id)
		{
			HttpWebRequest req = (HttpWebRequest)WebRequest.Create(_base + "/api/v2.0/indexers/" + Uri.EscapeDataString(id));
			req.Method = "DELETE";
			req.CookieContainer = _cookies;
			req.Timeout = 30000;
			using (req.GetResponse())
			{
			}
		}

		/// Adds one site with its default settings. Jackett tests it while adding; its error is thrown back.
		public void Add(string id)
		{
			string url = _base + "/api/v2.0/indexers/" + Uri.EscapeDataString(id) + "/config";
			string config = SetupCore.HttpGet(url, 60000, _cookies);
			try
			{
				SetupCore.HttpPost(url, config, "application/json", 120000, _cookies);
			}
			catch (WebException ex) when (ex.Response is HttpWebResponse r)
			{
				string msg = "";
				try
				{
					using (StreamReader sr = new StreamReader(r.GetResponseStream()))
					{
						msg = sr.ReadToEnd();
					}
					var d = _js.DeserializeObject(msg) as Dictionary<string, object>;
					if (d != null && d.ContainsKey("error")) msg = Convert.ToString(d["error"]);
				}
				catch
				{
				}
				throw new Exception(msg.Length > 0 ? FirstLine(msg) : ex.Message);
			}
		}

		private static string FirstLine(string s)
		{
			s = s.Trim();
			int nl = s.IndexOfAny(new[] { '\r', '\n' });
			s = nl > 0 ? s.Substring(0, nl) : s;
			return s.Length > 160 ? s.Substring(0, 160) + "..." : s;
		}

		private static string Get(Dictionary<string, object> d, string key) => d.ContainsKey(key) ? Convert.ToString(d[key]) ?? "" : "";
	}

	/// <summary>The steps that need administrator rights. Runs in "MyTorrents Configuration.exe /admin tasks.txt".</summary>
	internal static class AdminTasks
	{
		public static int RunFile(string taskFile)
		{
			List<string> results = Run(File.ReadAllLines(taskFile).Where(l => l.Trim().Length > 0).ToList());
			File.WriteAllLines(taskFile + ".result", results);
			return 0;
		}

		public static List<string> Run(List<string> tasks)
		{
			List<string> results = new List<string>();
			foreach (string task in tasks)
			{
				string[] p = task.Split(new[] { '|' }, 2);
				string name = p[0], arg = p.Length > 1 ? p[1] : "";
				try
				{
					string msg;
					switch (name)
					{
					case "winget": msg = Winget(arg); break;
					case "winget-upgrade": msg = WingetUpgrade(arg); break;
					case "jackett-install": msg = JackettInstall(arg); break;
					case "jackett-service": msg = EnsureJackettService(); break;
					case "jackett-flaresolverr": msg = JackettFlareSolverr(arg); break;
					case "firewall-quiet": msg = FirewallQuiet(arg); break;
					case "winget-uninstall": msg = WingetUninstall(arg); break;   // 2026 (1.3.42)
					case "firewall-web": msg = FirewallWeb(arg); break;            // 2026 (1.3.45)
					case "jackett-uninstall": msg = JackettUninstall(); break;
					default: throw new Exception("unknown step");
					}
					results.Add("OK|" + name + "|" + msg);
				}
				catch (Exception ex)
				{
					results.Add("FAIL|" + name + "|" + ex.Message.Replace("\r", " ").Replace("\n", " "));
				}
			}
			return results;
		}

		/// Installs a program with Windows' own package manager (winget).
		private static string Winget(string id)
		{
			ProcessStartInfo psi = new ProcessStartInfo("winget", "install -e --id " + id + " --silent --accept-package-agreements --accept-source-agreements")
			{
				UseShellExecute = false,
				CreateNoWindow = true,
				RedirectStandardOutput = true,
				RedirectStandardError = true
			};
			Process proc;
			try
			{
				proc = Process.Start(psi);
			}
			catch (Win32Exception)
			{
				throw new Exception("winget (Windows App Installer) is not on this PC");
			}
			using (proc)
			{
				string output = proc.StandardOutput.ReadToEnd();
				proc.WaitForExit(15 * 60 * 1000);
				// 0 = installed; 0x8A15002B = already installed / nothing newer
				if (proc.ExitCode == 0 || proc.ExitCode == unchecked((int)0x8A15002B)) return "installed";
				string last = output.Split('\n').Select(l => l.Trim()).LastOrDefault(l => l.Length > 0 && !l.All(c => "-\\|/ ".IndexOf(c) >= 0)) ?? "";
				throw new Exception("winget could not install it (" + proc.ExitCode + ") " + last);
			}
		}

		/// Updates a program with winget (0x8A15002B = nothing newer, also fine).
		private static string WingetUpgrade(string id)
		{
			ProcessStartInfo psi = new ProcessStartInfo("winget", "upgrade -e --id " + id + " --silent --accept-package-agreements --accept-source-agreements")
			{
				UseShellExecute = false,
				CreateNoWindow = true,
				RedirectStandardOutput = true,
				RedirectStandardError = true
			};
			Process proc;
			try
			{
				proc = Process.Start(psi);
			}
			catch (Win32Exception)
			{
				throw new Exception("winget (Windows App Installer) is not on this PC");
			}
			using (proc)
			{
				string output = proc.StandardOutput.ReadToEnd();
				proc.WaitForExit(15 * 60 * 1000);
				if (proc.ExitCode == 0 || proc.ExitCode == unchecked((int)0x8A15002B)) return "updated";
				string last = output.Split('\n').Select(l => l.Trim()).LastOrDefault(l => l.Length > 0 && !l.All(c => "-\\|/ ".IndexOf(c) >= 0)) ?? "";
				throw new Exception("winget could not update it (" + proc.ExitCode + ") " + last);
			}
		}

		/// 2026 (1.3.45): lets computers on the home network (Private network) reach the MyTorrents web page.
		private static string FirewallWeb(string port)
		{
			int p;
			if (!int.TryParse(port, out p)) p = 9118;
			const string name = "MyTorrents web page (home network)";
			Netsh("advfirewall firewall delete rule name=\"" + name + "\"");
			int code = Netsh("advfirewall firewall add rule name=\"" + name + "\" dir=in action=allow protocol=TCP localport=" + p + " profile=private");
			if (code != 0) throw new Exception("netsh answered " + code);
			return "rule added";
		}

		/// 2026 (1.3.42): "Remove MyTorrents" - uninstalls a program with winget.
		private static string WingetUninstall(string id)
		{
			ProcessStartInfo psi = new ProcessStartInfo("winget", "uninstall -e --id " + id + " --silent --accept-source-agreements")
			{
				UseShellExecute = false,
				CreateNoWindow = true,
				RedirectStandardOutput = true,
				RedirectStandardError = true
			};
			Process proc;
			try
			{
				proc = Process.Start(psi);
			}
			catch (Win32Exception)
			{
				throw new Exception("winget (Windows App Installer) is not on this PC - uninstall it in Windows Settings > Apps");
			}
			using (proc)
			{
				string output = proc.StandardOutput.ReadToEnd();
				proc.WaitForExit(10 * 60 * 1000);
				if (proc.ExitCode == 0) return "uninstalled";
				string last = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? "";
				throw new Exception("winget could not uninstall it (" + proc.ExitCode + ") " + last);
			}
		}

		/// 2026 (1.3.42): stops and removes the Jackett service, then runs Jackett's own uninstaller.
		private static string JackettUninstall()
		{
			ServiceController sc = JackettService();
			if (sc != null)
			{
				try
				{
					if (sc.Status == ServiceControllerStatus.Running)
					{
						sc.Stop();
						sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(60));
					}
				}
				catch
				{
				}
				string console = Path.Combine(SetupCore.JackettDir, "JackettConsole.exe");
				if (File.Exists(console))
				{
					using (Process p = Process.Start(new ProcessStartInfo(console, "--Uninstall") { UseShellExecute = false, CreateNoWindow = true }))
					{
						p.WaitForExit(120000);
					}
				}
			}
			foreach (string name in new[] { "JackettTray", "JackettConsole" })
			{
				foreach (Process x in Process.GetProcessesByName(name))
				{
					try { x.Kill(); } catch { }
				}
			}
			string uninstaller = Path.Combine(SetupCore.JackettDir, "unins000.exe");
			if (!File.Exists(uninstaller)) throw new Exception("Jackett's uninstaller was not found - uninstall it in Windows Settings > Apps");
			using (Process p = Process.Start(new ProcessStartInfo(uninstaller, "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART") { UseShellExecute = false }))
			{
				p.WaitForExit(10 * 60 * 1000);
				if (p.ExitCode != 0) throw new Exception("Jackett's uninstaller stopped with code " + p.ExitCode);
			}
			return "uninstalled";
		}

		private static ServiceController JackettService() =>
			ServiceController.GetServices().FirstOrDefault(s => s.ServiceName.Equals("Jackett", StringComparison.OrdinalIgnoreCase));

		private static string JackettInstall(string installer)
		{
			ServiceController sc = JackettService();
			if (sc != null && sc.Status == ServiceControllerStatus.Running)
			{
				sc.Stop();
				sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(60));
			}
			using (Process p = Process.Start(new ProcessStartInfo(installer, "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP-") { UseShellExecute = false }))
			{
				p.WaitForExit(10 * 60 * 1000);
				if (p.ExitCode != 0) throw new Exception("the Jackett installer stopped with code " + p.ExitCode);
			}
			foreach (Process tray in Process.GetProcessesByName("JackettTray"))
			{
				try { tray.Kill(); } catch { } // the service does the work; the tray app would start a second copy
			}
			return "installed";
		}

		/// Jackett as a Windows service, set to start with Windows, running now.
		private static string EnsureJackettService()
		{
			ServiceController sc = JackettService();
			if (sc == null)
			{
				string console = Path.Combine(SetupCore.JackettDir, "JackettConsole.exe");
				if (!File.Exists(console)) throw new Exception("Jackett is not installed");
				using (Process p = Process.Start(new ProcessStartInfo(console, "--Install") { UseShellExecute = false, CreateNoWindow = true }))
				{
					p.WaitForExit(120000);
				}
				sc = JackettService();
				if (sc == null) throw new Exception("could not create the Jackett Windows service");
			}
			sc.Refresh();
			if (sc.Status != ServiceControllerStatus.Running)
			{
				sc.Start();
				sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(60));
			}
			return "running";
		}

		/// A Windows Firewall rule that stops other computers connecting to this program. With a rule in place Windows
		/// does not pop up "allow access?" for it (FlareSolverr's browser listens for network devices, which is not needed).
		private static string FirewallQuiet(string program)
		{
			string name = "MyTorrents - " + Path.GetFileNameWithoutExtension(program) + " (FlareSolverr, this PC only)";
			Netsh("advfirewall firewall delete rule name=\"" + name + "\"");
			int code = Netsh("advfirewall firewall add rule name=\"" + name + "\" dir=in action=block profile=any enable=yes program=\"" + program + "\"");
			SetupCore.Log("Firewall rule for " + program + ": " + (code == 0 ? "added" : "netsh answered " + code));
			if (code != 0) throw new Exception("netsh answered " + code);
			return "rule added";
		}

		private static int Netsh(string args)
		{
			using (Process p = Process.Start(new ProcessStartInfo("netsh", args) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true }))
			{
				p.StandardOutput.ReadToEnd();
				p.WaitForExit(30000);
				return p.ExitCode;
			}
		}

		/// Tells Jackett where FlareSolverr is (needs the service stopped while ServerConfig.json changes).
		private static string JackettFlareSolverr(string url)
		{
			string file = null;
			for (int i = 0; i < 40 && (file = SetupCore.JackettConfigFile()) == null; i++)
			{
				Thread.Sleep(1500); // a new Jackett writes its settings on first start
			}
			if (file == null) throw new Exception("Jackett's settings file was not found");
			string updated = SetupCore.WithFlareSolverr(File.ReadAllText(file), url);
			if (updated == null) return "already set";
			ServiceController sc = JackettService();
			bool wasRunning = sc != null && sc.Status == ServiceControllerStatus.Running;
			if (wasRunning)
			{
				sc.Stop();
				sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(60));
			}
			File.Copy(file, file + ".before-mytorrents", true);
			File.WriteAllText(file, updated, new UTF8Encoding(false));
			if (sc != null)
			{
				sc.Start();
				sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(60));
			}
			return "set";
		}
	}
}
