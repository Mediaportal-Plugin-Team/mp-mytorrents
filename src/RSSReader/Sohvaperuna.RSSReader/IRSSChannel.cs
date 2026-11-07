using System;

namespace Sohvaperuna.RSSReader
{
	public interface IRSSChannel : IChannel
	{
		string Description { get; set; }

		TimeSpan ExpireTime { get; set; }

		RSSItemList Items { get; set; }

		string Language { get; set; }

		string Link { get; set; }

		string Title { get; set; }

		DateTime Updated { get; set; }

		new string Url { get; set; }

		new string Type { get; set; }

		bool Expired();

		void Update();

		void Reset();
	}
}
