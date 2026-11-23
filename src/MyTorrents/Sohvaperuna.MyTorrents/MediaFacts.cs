using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace Sohvaperuna.MyTorrents
{
	/// <summary>
	/// 1.3.79: quality read from the video file itself, never trusted from its name (the rule HTPC Library Manager
	/// uses, 19 Nov 2026) - through MediaInfo.dll, which comes with MediaPortal.
	///   resolution: 2160p / 1080p / 720p / 576p / 480p only - width 1700+ or height 950+ is 1080p (no "972p")
	///   codec:      x264 or HEVC (never H264)
	///   audio:      AAC, AC3, EAC3, DTS, TrueHD...
	/// Anything it can't read stays "" and the name is used as before.
	/// </summary>
	public static class MediaFacts
	{
		public sealed class Facts
		{
			public string Quality = "", Codec = "", Audio = "";
		}

		[DllImport("MediaInfo.dll")] private static extern IntPtr MediaInfo_New();
		[DllImport("MediaInfo.dll", CharSet = CharSet.Unicode)] private static extern IntPtr MediaInfo_Open(IntPtr handle, string fileName);
		[DllImport("MediaInfo.dll", CharSet = CharSet.Unicode)] private static extern IntPtr MediaInfo_Get(IntPtr handle, IntPtr streamKind, IntPtr streamNumber, string parameter, IntPtr infoKind, IntPtr searchKind);
		[DllImport("MediaInfo.dll")] private static extern void MediaInfo_Close(IntPtr handle);
		[DllImport("MediaInfo.dll")] private static extern void MediaInfo_Delete(IntPtr handle);

		private static readonly Dictionary<string, Facts> Cache = new Dictionary<string, Facts>(StringComparer.OrdinalIgnoreCase);
		private static bool _broken;

		public static Facts Read(string path)
		{
			Facts f = new Facts();
			if (_broken || string.IsNullOrEmpty(path)) return f;
			try
			{
				if (!System.IO.File.Exists(path)) return f;
				string key = path + "|" + new FileInfo(path).Length;
				lock (Cache)
				{
					Facts known;
					if (Cache.TryGetValue(key, out known)) return known;
				}
				IntPtr h = MediaInfo_New();
				try
				{
					if (MediaInfo_Open(h, path) == IntPtr.Zero) return f;
					Func<int, string, string> get = (kind, p) => Marshal.PtrToStringUni(MediaInfo_Get(h, (IntPtr)kind, IntPtr.Zero, p, (IntPtr)1, IntPtr.Zero)) ?? "";
					int w, ht;
					int.TryParse(get(1, "Width"), out w);
					int.TryParse(get(1, "Height"), out ht);
					f.Quality = Resolution(w, ht);
					f.Codec = Codec(get(1, "Format"));
					f.Audio = Audio(get(2, "Format"), get(2, "Format_Commercial_IfAny") + " " + get(2, "Format_AdditionalFeatures"));
					MediaInfo_Close(h);
				}
				finally
				{
					MediaInfo_Delete(h);
				}
				lock (Cache) Cache[key] = f;
			}
			catch (Exception ex) when (ex is DllNotFoundException || ex is EntryPointNotFoundException || ex is BadImageFormatException)
			{
				_broken = true;
				Log.Instance().Print("Naming: MediaInfo.dll can't be used here (" + ex.GetType().Name + ") - quality is taken from the names");
			}
			catch
			{
			}
			return f;
		}

		/// width 1700+ or height 950+ = 1080p, and so on - only the five usual sizes
		public static string Resolution(int width, int height)
		{
			if (width <= 0 && height <= 0) return "";
			if (width >= 3400 || height >= 2000) return "2160p";
			if (width >= 1700 || height >= 950) return "1080p";
			if (width >= 1200 || height >= 700) return "720p";
			if (height >= 540 || width >= 1000) return "576p";
			return "480p";
		}

		/// "1080", "1080p", "972p", "1920x800"... (Moving Pictures' reading) to one of the five sizes
		public static string Resolution(string text)
		{
			System.Text.RegularExpressions.Match m = System.Text.RegularExpressions.Regex.Match(text ?? "", @"(\d{3,4})\s*[x×]\s*(\d{3,4})");
			if (m.Success) return Resolution(int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value));
			m = System.Text.RegularExpressions.Regex.Match(text ?? "", @"(\d{3,4})");
			if (!m.Success) return System.Text.RegularExpressions.Regex.IsMatch(text ?? "", @"4k|uhd", System.Text.RegularExpressions.RegexOptions.IgnoreCase) ? "2160p" : "";
			return Resolution(0, int.Parse(m.Groups[1].Value));
		}

		public static string Codec(string format)
		{
			string f = (format ?? "").ToUpperInvariant();
			if (f.Contains("HEVC") || f.Contains("H265") || f.Contains("H.265") || f.Contains("X265")) return "HEVC";
			if (f.Contains("AVC") || f.Contains("H264") || f.Contains("H.264") || f.Contains("X264")) return "x264";
			if (f.Contains("AV1")) return "AV1";
			if (f.Contains("MPEG-4") || f.Contains("XVID") || f.Contains("DIVX") || f == "DX50") return "XviD";
			if (f.Contains("VC-1") || f == "VC1") return "VC1";
			if (f.Contains("MPEG2") || f.Contains("MPEG-2") || f == "MPEG VIDEO") return "MPEG2";
			return "";
		}

		public static string Audio(string format, string more = "")
		{
			string f = (format ?? "").ToUpperInvariant().Replace(" ", ""), x = (more ?? "").ToUpperInvariant();
			if (f.Contains("MLP") || f.Contains("TRUEHD") || x.Contains("TRUEHD")) return "TrueHD";
			if (f.StartsWith("DTS")) return "DTS";
			if (f.StartsWith("EAC3") || f.Contains("E-AC-3")) return "EAC3";
			if (f == "AC-3" || f == "AC3") return "AC3";
			if (f.StartsWith("AAC")) return "AAC";
			if (f.Contains("FLAC")) return "FLAC";
			if (f.Contains("OPUS")) return "Opus";
			if (f.Contains("MP3") || f.Contains("LAYER3")) return "MP3";
			if (f == "MP2" || f.Contains("LAYER2")) return "MP2";
			if (f.Contains("MPEGAUDIO")) return "MP3";
			if (f.StartsWith("WMA")) return "WMA";
			if (f.Contains("PCM")) return "PCM";
			return "";
		}
	}
}
