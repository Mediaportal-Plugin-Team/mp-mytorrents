using System.Collections.Generic;

namespace Sohvaperuna.DelugeEngine
{
	public class DelugeContent
	{
		public string type { get; set; }

		public Dictionary<string, DelugeFile> contents { get; set; }
	}
}
