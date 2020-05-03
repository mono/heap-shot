// 
// Header.cs
//  
// Authors:
//       Mike Krüger <mkrueger@novell.com>
//       Rolf Bjarne Kvinge <rolf@xamarin.com>
// 
// Copyright (c) 2010 Novell, Inc (http://www.novell.com)
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
using HeapShot.Reader;

namespace MonoDevelop.Profiler
{
	public class Header
	{
		const int LogHeaderId = 0x4D505A01;

		public readonly int Id; // constant value: LOG_HEADER_ID
		public readonly byte Major, Minor; // major and minor version of the log profiler
		public readonly byte Format; // version of the data format for the rest of the file
		public readonly byte PtrSize; // size in bytes of a pointer in the profiled program
		public readonly long StartupTime; // time in milliseconds since the unix epoch when the program started
		public readonly int TimerOverhead; // approximate overhead in nanoseconds of the timer
		public readonly int Flags; // file format flags, should be 0 for now
		public readonly int Pid; // pid of the profiled process
		public readonly int Port; // tcp port for server if != 0
//		public readonly int SysId; //  operating system and architecture identifier
	    public readonly string Args;
	    public readonly string Arch;
	    public readonly string OS;

        Header(LogFileReader reader)
        {
            Console.WriteLine("hoge");
			Id = reader.ReadInt32 ();
			if (Id != LogHeaderId)
				throw new InvalidOperationException ("Id doesn't match.");
			Major = reader.ReadByte ();
			Minor = reader.ReadByte ();
			Format = reader.ReadByte ();
			PtrSize = reader.ReadByte ();
			StartupTime = reader.ReadInt64 ();
			TimerOverhead = reader.ReadInt32 ();
			Flags = reader.ReadInt32 ();
			Pid = reader.ReadInt32 ();
			Port = reader.ReadUInt16 ();
            //		SysId = reader.ReadUInt16 ();
            Args = reader.ReadVarString();
            Arch = reader.ReadVarString();
            OS = reader.ReadVarString();
        }

        public static Header Read(LogFileReader reader)
        {
            if (!reader.LoadData(30))
                return null;

            return new Header(reader);
        }
    }
}
