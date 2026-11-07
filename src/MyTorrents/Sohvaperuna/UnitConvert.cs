namespace Sohvaperuna
{
	public class UnitConvert
	{
		public static string SizeToString(long p)
		{
			float num = p;
			if (num < 1024f)
			{
				return $"{num:F2} B";
			}
			num /= 1024f;
			if (num < 1024f)
			{
				return $"{num:F2} KB";
			}
			num /= 1024f;
			if (num < 1024f)
			{
				return $"{num:F2} MB";
			}
			num /= 1024f;
			if (num < 1024f)
			{
				return $"{num:F2} GB";
			}
			num /= 1024f;
			if (num < 1024f)
			{
				return $"{num:F2} TB";
			}
			return "";
		}

		public static string TimeRemainingToString(long seconds)
		{
			if (seconds < 60)
			{
				return $"{seconds}s";
			}
			long num = seconds / 60;
			seconds -= num * 60;
			if (num < 60)
			{
				return $"{num}min {seconds}s";
			}
			long num2 = num / 60;
			num -= num2 * 60;
			if (num2 < 24)
			{
				return $"{num2}h {num}min {seconds}s";
			}
			long num3 = num2 / 24;
			num2 -= num3 * 24;
			if (num3 < 7)
			{
				return $"{num3}d {num2}h {num}min";
			}
			long num4 = num3 / 7;
			num3 -= num4 * 7;
			return $"{num4}w {num3}d {num2}h";
		}

		public static string TransferSpeedToString(long bytes)
		{
			return SizeToString(bytes) + "/s";
		}
	}
}
