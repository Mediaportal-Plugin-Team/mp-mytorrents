using System.Collections.Generic;

namespace Sohvaperuna.MyTorrents
{
	public class Folder
	{
		public string FolderName { get; set; }

		public string Path { get; set; }

		public List<string> SubFolders { get; set; }

		public string ParentFolder { get; set; }
	}
}
