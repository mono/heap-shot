// 
// LogFileReader.cs
//  
// Authors:
//       Rolf Bjarne Kvinge <rolf@xamarin.com>
// 
// Copyright (C) 2011 Xamarin Inc. (http://www.xamarin.com) 
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.IO;

namespace HeapShot.Reader
{
	public class LogFileReader : IDisposable
	{
		FileStream stream;
		byte [] buffer = new byte [ushort.MaxValue];
		int buffered_size;
		int position;
		
		public LogFileReader (string filename)
		{
			stream = new FileStream (filename, FileMode.Open, FileAccess.Read);
		}
		
		public bool LoadData (int size)
		{
			long str_pos = stream.Position;
			
			if (str_pos + size > stream.Length)
				return false;
			
			if (buffer.Length < size)
				buffer = new byte [size];
			
			if (stream.Read (buffer, 0, size) != size) {
				stream.Position = str_pos;
				return false;
			}
			
			position = 0;
			buffered_size = size;
			
			return true;
		}
		
		public bool IsBufferEmpty {
			get {
				return position >= buffered_size;
			}
		}
		
		public bool IsEof {
			get { return stream.Position == stream.Length; }
		}
		
		public long Position {
			get { return stream.Position; }
			set { stream.Position = value; }
		}
		
		public long Length {
			get { return stream.Length; }
		}
		
		public byte ReadByte ()
		{
			return buffer [position++];
		}
		
		public ushort ReadUInt16 ()
		{
			ushort res;
			res = (ushort) (buffer [position] | (buffer [position + 1] << 8));
			position += 2;
			return res;
		}
		
		public int ReadInt32 ()
		{
			int res;
			res = (buffer [position] | (buffer [position + 1] << 8) | (buffer [position + 2] << 16) | (buffer [position + 3] << 24));
			position += 4;
			return res;
		}
		
		public long ReadInt64 ()
		{
			uint ret_low  = (uint) (((uint)buffer [position + 0])        |
			                       (((uint)buffer [position + 1]) << 8)  |
			                       (((uint)buffer [position + 2]) << 16) |
			                       (((uint)buffer [position + 3]) << 24)
			                       );
			uint ret_high = (uint) (((uint)buffer [position + 4])        |
			                       (((uint)buffer [position + 5]) << 8)  |
			                       (((uint)buffer [position + 6]) << 16) |
			                       (((uint)buffer [position + 7]) << 24)
			                       );
			position += 8;
			return (long) ((((ulong) ret_high) << 32) | ret_low);
		}
		
		public ulong ReadUInt64 ()
		{
			uint ret_low  = (uint) (((uint)buffer [position + 0])        |
			                       (((uint)buffer [position + 1]) << 8)  |
			                       (((uint)buffer [position + 2]) << 16) |
			                       (((uint)buffer [position + 3]) << 24)
			                       );
			uint ret_high = (uint) (((uint)buffer [position + 4])        |
			                       (((uint)buffer [position + 5]) << 8)  |
			                       (((uint)buffer [position + 6]) << 16) |
			                       (((uint)buffer [position + 7]) << 24)
			                       );
			position += 8;
			return (((ulong) ret_high) << 32) | ret_low;
		}
			
		public ulong ReadULeb128 ()
		{
			ulong result = 0;
			int shift = 0;
			while (true) {
				byte b = buffer [position++];
				result |= ((ulong)(b & 0x7f)) << shift;
				if ((b & 0x80) != 0x80)
					break;
				shift += 7;
			}
			return result;
		}
		
		public long ReadSLeb128 ()
		{
			long result = 0;
			int shift = 0;
			while (true) {
				byte b = buffer [position++];
				result |= ((long)(b & 0x7f)) << shift;
				shift += 7;
				if ((b & 0x80) != 0x80) {
					if (shift < sizeof(long) * 8 && (b & 0x40) == 0x40)
						result |= -(1L << shift);
					break;
				}
			}
			return result;
		}
		
		public string ReadNullTerminatedString ()
		{
			int start = position;
			
			if (buffer [position] == 0) {
				position++;
				return string.Empty;
			}
			
			while (buffer [position++] != 0) {
				// nothing to do
			}
			
			return System.Text.Encoding.UTF8.GetString (buffer, start, position - start - 1);
		}
		
		public void Close ()
		{
			Dispose ();
		}
		
		public void Dispose ()
		{
			stream.Dispose ();
		}
	}
}

