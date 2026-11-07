using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Sohvaperuna.TorrentEngine
{
	public class TorrentFile
	{
		private Hashtable _content;

		private Stream _stream;

		private byte[] _sharedBuffer;

		private long _infoStarts;

		private long _infoEnds;

		private string _hash;

		public Hashtable Content => _content;

		public string Hash => _hash;

		public string Name
		{
			get
			{
				if (_content == null || !_content.ContainsKey("info") || !(_content["info"] as Hashtable).ContainsKey("name"))
				{
					return "";
				}
				return (_content["info"] as Hashtable)["name"] as string;
			}
		}

		public TorrentFile(Stream stream)
		{
			_stream = stream;
			_infoStarts = 0L;
			_infoEnds = 0L;
			_sharedBuffer = new byte[_stream.Length];
			_content = ReadBencoded() as Hashtable;
			_hash = CalculateHash();
			_stream = null;
			_sharedBuffer = null;
		}

		private int ReadUntil(byte[] array, int offset, int count, string until)
		{
			int num = offset;
			do
			{
				bool flag = true;
				byte b = (byte)_stream.ReadByte();
				if (until.IndexOf((char)b) != -1)
				{
					_stream.Seek(_stream.Position - 1, SeekOrigin.Begin);
					break;
				}
				array[num] = b;
				num++;
			}
			while (count <= 0 || num - offset <= count);
			return num - offset;
		}

		private char PeekByte()
		{
			char result = (char)_stream.ReadByte();
			_stream.Seek(_stream.Position - 1, SeekOrigin.Begin);
			return result;
		}

		private object ReadBencoded()
		{
			switch (PeekByte())
			{
			case 'd':
			{
				_stream.ReadByte();
				Hashtable hashtable = new Hashtable();
				char c;
				do
				{
					string text = ReadString();
					if (text == "info")
					{
						_infoStarts = _stream.Position;
					}
					object item = ReadBencoded();
					if (text == "info")
					{
						_infoEnds = _stream.Position;
					}
					hashtable.Add(text, item);
					c = PeekByte();
				}
				while (c != 'e');
				_stream.ReadByte();
				return hashtable;
			}
			case 'i':
				_stream.ReadByte();
				return ReadInt();
			case 'l':
			{
				_stream.ReadByte();
				List<object> list = new List<object>();
				char c;
				do
				{
					object item = ReadBencoded();
					list.Add(item);
					c = PeekByte();
				}
				while (c != 'e');
				_stream.ReadByte();
				return list;
			}
			case '0':
			case '1':
			case '2':
			case '3':
			case '4':
			case '5':
			case '6':
			case '7':
			case '8':
			case '9':
				return ReadString();
			default:
				return null;
			}
		}

		private string ReadString()
		{
			int count = ReadUntil(_sharedBuffer, 0, 0, ":");
			int count2 = Convert.ToInt32(Encoding.ASCII.GetString(_sharedBuffer, 0, count));
			_stream.ReadByte();
			count = _stream.Read(_sharedBuffer, 0, count2);
			return Encoding.ASCII.GetString(_sharedBuffer, 0, count);
		}

		private long ReadInt()
		{
			int count = ReadUntil(_sharedBuffer, 0, 0, "e");
			long result = Convert.ToInt64(Encoding.ASCII.GetString(_sharedBuffer, 0, count));
			_stream.ReadByte();
			return result;
		}

		private string CalculateHash()
		{
			SHA1Managed sHA1Managed = new SHA1Managed();
			_stream.Position = _infoStarts;
			_stream.Seek(_infoStarts, SeekOrigin.Begin);
			int num = Convert.ToInt32(_infoEnds - _infoStarts);
			int num2 = _stream.Read(_sharedBuffer, 0, num);
			if (num2 != num)
			{
				return "";
			}
			return BitConverter.ToString(sHA1Managed.ComputeHash(_sharedBuffer, 0, num)).Replace("-", "");
		}

		public void Close()
		{
		}
	}
}
