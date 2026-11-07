using System;
using System.Collections.Generic;
using System.IO;
using MediaPortal.Configuration;

namespace Sohvaperuna.UTorrentEngine
{
	public class Log
	{
		private object lockobject = new object();

		private static Log _instance = null;

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

		public void Print(string line)
		{
			lock (lockobject)
			{
				try
				{
					using (TextWriter textWriter = new StreamWriter(_logfile, append: true))
					{
						textWriter.WriteLine($"[{DateTime.Now.ToShortTimeString()} {DateTime.Now.ToShortDateString()}] {line}");
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
