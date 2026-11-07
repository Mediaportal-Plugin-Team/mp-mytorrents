using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Sohvaperuna.TorrentSearch;

namespace Sohvaperuna.MyTorrents
{
	/// <summary>
	/// 2026 (1.3.45): sound preference - read from the copy's name. Separate settings for movies ("Films.Sound") and
	/// TV ("AutoGrabber.Sound"): any / prefer-surround / prefer-stereo / only-surround / only-stereo.
	/// A copy whose name doesn't say counts as "unknown": kept with "prefer", left out with "only".
	/// </summary>
	public static class Sound
	{
		public static readonly string[] Choices = { "any", "prefer-surround", "prefer-stereo", "only-surround", "only-stereo" };

		public static string Text(string c)
		{
			switch (c)
			{
			case "prefer-surround": return "Prefer surround (5.1 / 7.1 / Atmos)";
			case "prefer-stereo": return "Prefer stereo (2.0)";
			case "only-surround": return "Only surround";
			case "only-stereo": return "Only stereo";
			default: return "Any";
			}
		}

		public static string Movies => Configuration.GetExtra("Films.Sound", "any");

		public static string Tv => Configuration.GetExtra("AutoGrabber.Sound", "any");

		private static readonly Regex Surround = new Regex(@"(?<![a-z0-9])(5[ ._]?1|7[ ._]?1|6ch|8ch|ddp?5|dd\+5|eac3|e-ac-3|ac3|dts(-?hd)?|truehd|atmos)(?![a-z0-9])", RegexOptions.IgnoreCase);
		private static readonly Regex Stereo = new Regex(@"(?<![a-z0-9])(2[ ._]0|aac2|ddp?2|dd\+2|stereo|2ch)(?![a-z0-9])", RegexOptions.IgnoreCase);

		/// "surround", "stereo" or "" (the name doesn't say). "DDP5.1" = surround even though "DDP2.0" exists.
		public static string Of(string title)
		{
			string t = title ?? "";
			bool s = Surround.IsMatch(t), st = Stereo.IsMatch(t);
			if (s && !st) return "surround";
			if (st && !s) return "stereo";
			if (s && st) return Regex.IsMatch(t, @"(5[ ._]?1|7[ ._]?1|atmos)", RegexOptions.IgnoreCase) ? "surround" : "stereo";
			return "";
		}

		/// Leaves out copies that don't match an "only" choice.
		public static bool Allowed(TorrentMatch m, string choice, bool tv = false)
		{
			if (OtherLanguage(m?.Title, tv)) return false; // 2026 (1.3.56)
			if (choice == "only-surround") return Of(m.Title) == "surround";
			if (choice == "only-stereo") return Of(m.Title) == "stereo";
			return true;
		}

		// 2026 (1.3.56): copies dubbed into another language are left out - the Auto grabber got a whole season of
		// "Love Death Robots S01 PLDUB" (Polish) on 5 Nov. Copies that say they also have English (ENG, MULTi, DUAL) are fine.
		private static readonly string[] DubTags = { "DUB", "DUBBED", "PLDUB", "DUBPL", "LEKTOR", "PL", "ITA", "LATINO", "CASTELLANO", "RUS", "UKR", "HINDI", "TAMIL", "TELUGU", "TRUEFRENCH", "VFF", "VFQ", "VF2", "VOSTFR" };
		// for TV only - a foreign film's own language is often written like this, so films keep them
		private static readonly string[] TvOnlyTags = { "FRENCH", "GERMAN", "SPANISH", "ITALIAN", "POLISH", "DUTCH", "NL", "SWEDISH", "CZECH", "HUN", "TURKISH", "ARABIC" };
		private static readonly string[] EnglishTags = { "ENG", "ENGLISH", "MULTI", "MULTi", "DUAL", "DUAL-AUDIO", "EN" };

		public static bool OtherLanguage(string title, bool tv)
		{
			if (string.IsNullOrEmpty(title)) return false;
			string[] words = System.Text.RegularExpressions.Regex.Split(title.ToUpperInvariant(), @"[^A-Z0-9]+");
			if (words.Any(w => EnglishTags.Contains(w, StringComparer.OrdinalIgnoreCase))) return false;
			return words.Any(w => DubTags.Contains(w) || (tv && TvOnlyTags.Contains(w)));
		}

		/// 1 for the kind you prefer, 0 otherwise - used to put them first.
		public static int Rank(TorrentMatch m, string choice)
		{
			string want = choice.EndsWith("surround") ? "surround" : choice.EndsWith("stereo") ? "stereo" : "";
			return want.Length > 0 && Of(m?.Title) == want ? 1 : 0;
		}
	}
}
