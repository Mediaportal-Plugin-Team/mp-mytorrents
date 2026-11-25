namespace Sohvaperuna.RSSReader
{
	public class RSSUTorentFilters
	{
		public int Ident { get; set; }

		public int Flags { get; set; }

		public string Name { get; set; }

		public string Filter { get; set; }

		public string NotFilter { get; set; }

		public string Directory { get; set; }

		public int Feed { get; set; }

		public int Quality { get; set; }

		public string Label { get; set; }

		public int PostponeMode { get; set; }

		public int LastMatch { get; set; }

		public int SmartEpFilter { get; set; }

		public int RepackEpFilter { get; set; }

		public string EpisodeFilterStr { get; set; }

		public bool EpisodeFilter { get; set; }

		public bool ResolvingCandidate { get; set; }
	}
}
