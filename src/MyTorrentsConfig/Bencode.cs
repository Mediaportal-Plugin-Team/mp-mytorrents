using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Sohvaperuna.MyTorrentsConfig
{
	/// <summary>
	/// Reads and writes BiglyBT's settings file (biglybt.config), which is a bencoded dictionary.
	/// Strings are kept as raw bytes so nothing BiglyBT stored is changed by accident.
	/// </summary>
	internal static class Bencode
	{
		public static object Decode(byte[] data)
		{
			int i = 0;
			object result = Read(data, ref i);
			return result;
		}

		private static object Read(byte[] d, ref int i)
		{
			byte c = d[i];
			if (c == (byte)'d')
			{
				i++;
				var dict = new SortedDictionary<byte[], object>(ByteComparer.Instance);
				while (d[i] != (byte)'e')
				{
					byte[] key = (byte[])Read(d, ref i);
					dict[key] = Read(d, ref i);
				}
				i++;
				return dict;
			}
			if (c == (byte)'l')
			{
				i++;
				var list = new List<object>();
				while (d[i] != (byte)'e')
				{
					list.Add(Read(d, ref i));
				}
				i++;
				return list;
			}
			if (c == (byte)'i')
			{
				int end = Array.IndexOf(d, (byte)'e', i);
				long v = long.Parse(Encoding.ASCII.GetString(d, i + 1, end - i - 1));
				i = end + 1;
				return v;
			}
			int colon = Array.IndexOf(d, (byte)':', i);
			int len = int.Parse(Encoding.ASCII.GetString(d, i, colon - i));
			byte[] s = new byte[len];
			Buffer.BlockCopy(d, colon + 1, s, 0, len);
			i = colon + 1 + len;
			return s;
		}

		public static byte[] Encode(object value)
		{
			using (MemoryStream ms = new MemoryStream())
			{
				Write(ms, value);
				return ms.ToArray();
			}
		}

		private static void Write(Stream s, object v)
		{
			if (v is byte[] b)
			{
				WriteAscii(s, b.Length + ":");
				s.Write(b, 0, b.Length);
			}
			else if (v is string str)
			{
				Write(s, Encoding.UTF8.GetBytes(str));
			}
			else if (v is long || v is int)
			{
				WriteAscii(s, "i" + Convert.ToInt64(v) + "e");
			}
			else if (v is SortedDictionary<byte[], object> dict)
			{
				s.WriteByte((byte)'d');
				foreach (var kv in dict) // already sorted by raw bytes, as bencoding requires
				{
					Write(s, kv.Key);
					Write(s, kv.Value);
				}
				s.WriteByte((byte)'e');
			}
			else if (v is List<object> list)
			{
				s.WriteByte((byte)'l');
				foreach (object o in list)
				{
					Write(s, o);
				}
				s.WriteByte((byte)'e');
			}
			else
			{
				throw new InvalidDataException("Unsupported value in BiglyBT settings: " + v?.GetType().Name);
			}
		}

		private static void WriteAscii(Stream s, string text)
		{
			byte[] a = Encoding.ASCII.GetBytes(text);
			s.Write(a, 0, a.Length);
		}

		public static byte[] Key(string key) => Encoding.UTF8.GetBytes(key);

		internal sealed class ByteComparer : IComparer<byte[]>
		{
			public static readonly ByteComparer Instance = new ByteComparer();

			public int Compare(byte[] x, byte[] y)
			{
				int n = Math.Min(x.Length, y.Length);
				for (int k = 0; k < n; k++)
				{
					int c = x[k].CompareTo(y[k]);
					if (c != 0) return c;
				}
				return x.Length.CompareTo(y.Length);
			}
		}
	}
}
