using System;
using System.Collections.Generic;
using System.IO;
using MediaPortal.Configuration;

namespace Sohvaperuna.MyTorrents
{
	public class Log
	{
		private object lockobject = new object();

		private static Log _instance = null;

		/// 2026 (1.3.45): "[helper] " in the background helper, so its lines can be told apart.
		public static string Prefix = "";

		private List<string> _lines;

		private string _logfile;

		public List<string> Lines => _lines;

		private Log()
		{
			_logfile = Config.GetFile(Config.Dir.Log, "MyTorrents.log");
			_lines = new List<string>();
		}

		public static Log Instance()
		{
			if (_instance == null)
			{
				_instance = new Log();
			}
			return _instance;
		}

		private static readonly System.Text.RegularExpressions.Regex Secret = new System.Text.RegularExpressions.Regex(@"((?:jackett_)?api_?key|passkey|password|token)=[^&\s\]]+", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

		public void Print(string line)
		{
			// 2026: never write API keys / passwords into the log (people share their logs)
			line = Secret.Replace(line ?? "", "$1=<hidden>");
			lock (lockobject)
			{
				NewDayFile();
				try
				{
					using (TextWriter textWriter = new StreamWriter(_logfile, append: true))
					{
						textWriter.WriteLine($"[{DateTime.Now.ToShortTimeString()} {DateTime.Now.ToShortDateString()}] {Prefix}{line}");
					}
				}
				catch
				{
					// never let logging break the plugin (file locked, folder missing...)
				}
				_lines.Insert(0, line);
				while (_lines.Count > 100)
				{
					_lines.RemoveAt(_lines.Count - 1);
				}
			}
		}

		/// 2026 (1.3.55): one log file per day - MyTorrents.log is today, MyTorrents.1.log yesterday ... up to
		/// MyTorrents.7.log, older ones are deleted (it used to grow for ever - 12 MB in 9 days)
		public const int KeepDays = 7;

		private DateTime _checkedDay = DateTime.MinValue, _lastTry = DateTime.MinValue;

		// 2026 (1.3.57): which day MyTorrents.log holds is kept in MyTorrents.logday.txt - the file's own "last
		// changed" time couldn't be used, as the search and torrent-program parts write to the same log and had
		// always touched it after midnight first, so it never moved on (7 Nov)
		private static string DayFile => Config.GetFile(Config.Dir.Log, "MyTorrents.logday.txt");

		private void NewDayFile()
		{
			if (_checkedDay == DateTime.Today || DateTime.Now - _lastTry < TimeSpan.FromMinutes(1)) return;
			_lastTry = DateTime.Now;
			try
			{
				// MediaPortal and the background helper both write this log - only one of them moves the files
				using (System.Threading.Mutex m = new System.Threading.Mutex(false, @"Local\MyTorrentsLogRotate"))
				{
					bool got = false;
					try
					{
						try { got = m.WaitOne(3000); } catch (System.Threading.AbandonedMutexException) { got = true; }
						if (!got) return;
						FileInfo fi = new FileInfo(_logfile);
						DateTime day = DateTime.MinValue;
						try { if (File.Exists(DayFile)) DateTime.TryParse(File.ReadAllText(DayFile).Trim(), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out day); } catch { }
						if (day == DateTime.MinValue && fi.Exists) day = fi.CreationTime.Date; // first time: the day the log was started
						if (fi.Exists && fi.Length > 0 && day.Date < DateTime.Today)
						{
							string oldest = OldFile(KeepDays);
							if (File.Exists(oldest)) File.Delete(oldest);
							for (int n = KeepDays - 1; n >= 1; n--)
							{
								string from = OldFile(n);
								if (File.Exists(from)) File.Move(from, OldFile(n + 1));
							}
							File.Move(_logfile, OldFile(1));
						}
						if (day.Date != DateTime.Today) File.WriteAllText(DayFile, DateTime.Today.ToString("yyyy-MM-dd"));
						_checkedDay = DateTime.Today;
					}
					finally
					{
						if (got) m.ReleaseMutex();
					}
				}
			}
			catch
			{
				// in use by the other part right now - tried again in a minute
			}
		}

		private static string OldFile(int n) => Config.GetFile(Config.Dir.Log, "MyTorrents." + n + ".log");

		/// 2026 (1.3.55): the log files that exist, oldest first, today's last
		public static List<string> Files()
		{
			List<string> list = new List<string>();
			for (int n = KeepDays; n >= 1; n--) if (File.Exists(OldFile(n))) list.Add(OldFile(n));
			string today = Config.GetFile(Config.Dir.Log, "MyTorrents.log");
			if (File.Exists(today)) list.Add(today);
			return list;
		}

		internal void Clear()
		{
			_lines.Clear();
		}

		internal void Print(Exception e)
		{
			Print($"\nMessage ---\n{e.Message}");
			Print($"\nHelpLink ---\n{e.HelpLink}");
			Print($"\nSource ---\n{e.Source}");
			Print($"\nStackTrace ---\n{e.StackTrace}");
			Print($"\nTargetSite ---\n{e.TargetSite}");
		}
	}
}
