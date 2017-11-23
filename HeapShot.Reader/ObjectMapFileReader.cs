//
// ObjectMapFileReader.cs
//
// Copyright (C) 2005 Novell, Inc.
// Copyright (C) 2011 Xamarin Inc. (http://www.xamarin.com)
//
//
// This program is free software; you can redistribute it and/or
// modify it under the terms of version 2 of the GNU General Public
// License as published by the Free Software Foundation.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with this program; if not, write to the Free Software
// Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA 02111-1307
// USA.
//

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using Mono.Profiler.Log;
using System.Threading;

namespace HeapShot.Reader
{

	public class ObjectMapReader: IDisposable
	{
		const string log_file_label = "heap-shot logfile";
		
		string name;
        LogStream reader;
		DateTime timestamp;
		ulong totalMemory;
		
		LogStreamHeader header;
		
		internal const long UnknownTypeId = -1;
		
		internal const long UnknownObjectId = -1;
		internal const long StackObjectId = -2;
		internal const long FinalizerObjectId = -3;
		internal const long HandleObjectId = -4;
		internal const long OtherRootObjectId = -5;
		internal const long MiscRootObjectId = -6;

		long rootId = StackObjectId - 1;
		
		HeapShotData currentData;
		List<HeapSnapshot> shots = new List<HeapSnapshot> ();
        IProgressListener progress;
		
		public event EventHandler<HeapShotEventArgs> HeapSnapshotAdded;
		
		internal ObjectMapReader ()
		{
		}
		
		public ObjectMapReader (string filename)
		{
			this.name = filename;
			
			currentData = new HeapShotData ();
			
			// Some stock types
			currentData.TypesList.Add (new TypeInfo () { Code = UnknownObjectId, Name = "<Unknown>" });   // 0
			currentData.TypesList.Add (new TypeInfo () { Code = StackObjectId, Name = "<Stack>" });     // 1
			currentData.TypesList.Add (new TypeInfo () { Code = FinalizerObjectId, Name = "<Finalizer>" }); // 2
			currentData.TypesList.Add (new TypeInfo () { Code = HandleObjectId, Name = "<Handle>" });    // 3
			currentData.TypesList.Add (new TypeInfo () { Code = OtherRootObjectId, Name = "<Other Root>" });      // 4
			currentData.TypesList.Add (new TypeInfo () { Code = MiscRootObjectId, Name = "<Misc Root>" }); // 5
		}
		
		public void Dispose ()
		{
			reader.Dispose ();
		}

		static public LogStreamHeader TryReadHeader (string filename)
		{
			try {
				var s = File.OpenRead (filename);
				var reader = new LogStream (s);

				var visitor = new HeadReaderLogEventVisitor ();
				var processor = new LogProcessor (reader, visitor, new NullLogEventVisitor ());

				try {
					processor.Process (visitor.TokenSource.Token);
				} catch {
				}
				return processor.StreamHeader;
			} catch {
				return null;
			}
		}
		
		
		public void Read ()
		{
			Read (null);
		}
		
		public void Read (IProgressListener progress)
		{
			Stopwatch watch = new Stopwatch ();
			watch.Start ();
			
			try {
				if (!File.Exists (name))
					return;
				
				DateTime tim = File.GetLastWriteTime (name);
				if (tim == timestamp)
					return;
				timestamp = tim;

                if (reader == null) {
                    var s = File.OpenRead(name);
                    reader = new LogStream(s);
                }

                this.progress = progress;
				ReadLogFile ();
			} catch (Exception ex) {
				Console.WriteLine (ex);
			} finally {
				watch.Stop ();
				Console.WriteLine ("ObjectMapFileReader.Read (): Completed in {0} s", watch.ElapsedMilliseconds / (double) 1000);
			}
		}
		
		public bool WaitForHeapShot (int timeout)
		{
			int ns = shots.Count;
			DateTime tlimit = DateTime.Now + TimeSpan.FromMilliseconds (timeout);
			while (DateTime.Now < tlimit) {
				Read ();
				if (shots.Count > ns)
					return true;
				System.Threading.Thread.Sleep (500);
			}
			return false;
		}
		
		public string Name {
			get { return name; }
		}
		
		public DateTime Timestamp {
			get { return timestamp; }
		}
		
		public IList<HeapSnapshot> HeapShots {
			get { return shots; }
		}
		
		public int Port {
			get { return header == null ? 0 : header.Port; }
		}
		
		public static void ForceSnapshot (int port)
		{
			using (TcpClient client = new TcpClient ()) {
				client.Connect ("127.0.0.1", port);
				using (StreamWriter sw = new StreamWriter (client.GetStream ())) {
					sw.WriteLine ("heapshot");
					sw.Flush ();
				}
			}
		}

		//
		// Code to read the log files generated at runtime
		//
		void ReadLogFile ()
		{
			last_pct = -1;

			var processor = new LogProcessor (reader, new HeapShotLogEventVisitor { Parent = this }, new NullLogEventVisitor ());
			processor.Process (progress.CancellationToken);

			header = processor.StreamHeader;
		}

        int last_pct;

        void UpdatePosition ()
        {
			long pct = (reader.BaseStream.Position * 100) / reader.BaseStream.Length;
			if (pct != last_pct) {
				last_pct = (int) pct;
				progress.ReportProgress ("Loading profiler log", pct / 100.0f);
			}
        }

        void ReadClassEvent (ClassLoadEvent t)
		{
			TypeInfo ti = new TypeInfo ();
			ti.Code = t.ClassPointer;
			ti.Name = t.Name;
			ti.FieldsIndex = currentData.FieldCodes.Count;
			
			int nf = 0;
/*			uint fcode;
			while ((fcode = reader.ReadUInt32 ()) != 0) {
				fieldCodes.Add (fcode);
				fieldNamesList.Add (reader.ReadString ());
				nf++;
			}*/
			ti.FieldsCount = nf;
			currentData.TypesList.Add (ti);
		}
		
        void ReadGcEvent (GCEvent ge)
		{
			if (ge.Type == LogGCEvent.Begin)
				currentData.ResetHeapData ();
		}
		
		int shotCount;

        void ReadHeapStartEvent ()
        {
            //Console.WriteLine ("ppe: START");
        }
		
        void ReadHeapEndEvent ()
        {
            HeapSnapshot shot = new HeapSnapshot();
            shotCount++;
            shot.Build(shotCount.ToString(), currentData);
            AddShot(shot);
        }

        void ReadHeapObjectEvent(HeapObjectEvent he)
        {
            ObjectInfo ob = new ObjectInfo();
            ob.Code = he.ObjectPointer;
            ob.Size = (ulong)he.ObjectSize;
            ob.RefsIndex = currentData.ReferenceCodes.Count;
            ob.RefsCount = he.References != null ? he.References.Count: 0;
            currentData.ObjectTypeCodes.Add(he.ClassPointer);
            totalMemory += (ulong)he.ObjectSize;
            if (ob.Size != 0)
                currentData.RealObjectCount++;

            // Read referenceCodes

            long lastOff = 0;
            for (int n = 0; n < ob.RefsCount; n++) {
                var reference = he.References[n];
                currentData.ReferenceCodes.Add(reference.ObjectPointer);
                lastOff += reference.Offset;
                currentData.FieldReferenceCodes.Add((ulong)lastOff);
            }
            currentData.ObjectsList.Add(ob);
			UpdatePosition ();
        }

        void ReadHeapRootEvent (HeapRootsEvent he)
		{
            for (int n=0; n<he.Roots.Count; n++) {
                var root = he.Roots[n];
				ObjectInfo ob = new ObjectInfo ();
				ob.Size = 0;
				ob.RefsIndex = currentData.ReferenceCodes.Count;
				ob.RefsCount = 1;
				long type = UnknownTypeId;
                switch (root.Attributes & LogHeapRootAttributes.TypeMask) {
                    case LogHeapRootAttributes.Stack: type = StackObjectId; ob.Code = StackObjectId; break;
                    case LogHeapRootAttributes.Finalizer: type = FinalizerObjectId; ob.Code = --rootId; break;
                    case LogHeapRootAttributes.Handle: type = HandleObjectId; ob.Code = --rootId; break;
                    case LogHeapRootAttributes.Other: type = OtherRootObjectId; ob.Code = --rootId; break;
                    case LogHeapRootAttributes.Miscellaneous: type = MiscRootObjectId; ob.Code = --rootId; break;
                }
				currentData.ObjectTypeCodes.Add (type);
				currentData.ReferenceCodes.Add (root.ObjectPointer);
				currentData.FieldReferenceCodes.Add (0);
				currentData.ObjectsList.Add (ob);
				currentData.RealObjectCount++;
			}
		}
		
		void AddShot (HeapSnapshot shot)
		{
			shots.Add (shot);
			if (HeapSnapshotAdded != null)
				HeapSnapshotAdded (this, new HeapShotEventArgs (shot));
		}

        class HeapShotLogEventVisitor: LogEventVisitor
        {
            public ObjectMapReader Parent { get; set; }

            public override void Visit(ClassLoadEvent ev)
            {
                Parent.ReadClassEvent(ev);
            }

            public override void Visit(HeapEndEvent ev)
            {
                Parent.ReadHeapEndEvent();
            }

            public override void Visit(HeapBeginEvent ev)
            {
                Parent.ReadHeapStartEvent();
            }

            public override void Visit(HeapRootsEvent ev)
            {
                Parent.ReadHeapRootEvent(ev);
            }

            public override void Visit(HeapObjectEvent ev)
            {
                Parent.ReadHeapObjectEvent(ev);
            }

            public override void Visit(GCEvent ev)
            {
                Parent.ReadGcEvent(ev);
            }
        }

		class NullLogEventVisitor: LogEventVisitor
		{
			
		}

		class HeadReaderLogEventVisitor : LogEventVisitor
		{
			public CancellationTokenSource TokenSource = new CancellationTokenSource ();

            public override void VisitBefore(LogEvent ev)
            {
				TokenSource.Cancel ();
            }
        }
	}
	
	internal class HeapShotData
	{
		public HeapShotData ()
		{
			ObjectsList = new List<ObjectInfo> ();
			TypesList = new List<TypeInfo> ();
			ObjectTypeCodes = new List<long> ();
			ReferenceCodes = new List<long> ();
			FieldReferenceCodes = new List<ulong> ();
			FieldCodes = new List<uint> ();
			FieldNamesList = new List<string> ();
		}
		
		public void ResetHeapData ()
		{
			ObjectsList.Clear ();
			ObjectTypeCodes.Clear ();
			ReferenceCodes.Clear ();
			FieldReferenceCodes.Clear ();
			RealObjectCount = 1;
			
			// The 'unknown' object
			ObjectInfo ob = new ObjectInfo ();
			ob.Code = ObjectMapReader.UnknownObjectId;
			ob.Size = 0;
			ob.RefsIndex = 0;
			ob.RefsCount = 0;
			ObjectTypeCodes.Add (ObjectMapReader.UnknownTypeId);
			ObjectsList.Add (ob);
		}
		
		public int RealObjectCount;
		public List<ObjectInfo> ObjectsList;
		public List<TypeInfo> TypesList;
		public List<string> FieldNamesList;
		public List<long> ReferenceCodes;
		public List<long> ObjectTypeCodes;
		public List<uint> FieldCodes;
		public List<ulong> FieldReferenceCodes;
	}
}
