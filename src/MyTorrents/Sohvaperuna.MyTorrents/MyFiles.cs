using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MediaPortal.Configuration;

namespace Sohvaperuna.MyTorrents
{
	/// <summary>2026 (1.3.42): MyTorrents' own settings and list files (for "Remove MyTorrents").</summary>
	public static class MyFiles
	{
		/// MyTorrents.* in MediaPortal's settings folder (settings, Auto grabber list, wanted movies, logs of what it got...)
		public static List<string> All()
		{
			try
			{
				string dir = Config.GetFolder(Config.Dir.Config);
				return Directory.Exists(dir) ? Directory.GetFiles(dir, "MyTorrents*.*").ToList() : new List<string>();
			}
			catch
			{
				return new List<string>();
			}
		}

		/// Deletes them; returns how many could not be deleted.
		public static int DeleteAll()
		{
			int failed = 0;
			foreach (string f in All())
			{
				try
				{
					File.Delete(f);
				}
				catch
				{
					failed++;
				}
			}
			return failed;
		}
	}
}
