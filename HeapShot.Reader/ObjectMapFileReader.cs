//
// OutfileReader.cs
//
// Copyright (C) 2005 Novell, Inc.
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
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using MonoDevelop.Profiler;
using HeapShot.Reader.Graphs;

namespace HeapShot.Reader {

	public class ObjectMapReader 
	{
		const string log_file_label = "heap-shot logfile";
		
		string name;
		DateTime timestamp;
		ulong totalMemory;
		
		long currentPtrBase;
		long currentObjBase;
		
		internal const long UnknownTypeId = -1;
		
		internal const long UnknownObjectId = -1;
		internal const long StackObjectId = -2;
		
		long rootId = StackObjectId - 1;
		
		HeapShotData currentData;
		
		List<HeapSnapshot> shots = new List<HeapSnapshot> ();
		
		internal ObjectMapReader ()
		{
		}
		
		public ObjectMapReader (string filename)
		{
			this.name = filename;
			
			Stream stream;
			stream = new FileStream (filename, FileMode.Open, FileAccess.Read);

			BinaryReader reader;
			reader = new BinaryReader (stream);
			
			ReadLogFile (reader);
			
			reader.Close ();
			
			timestamp = File.GetLastWriteTime (filename);
		}
		
		public string Name {
			get { return name; }
		}
		
		public DateTime Timestamp {
			get { return timestamp; }
		}
		
		public IEnumerable<HeapSnapshot> HeapShots {
			get { return shots; }
		}
		
		public static HeapSnapshot CreateProcessSnapshot (int pid)
		{
/*			string dumpFile = "/tmp/heap-shot-dump";
			if (File.Exists (dumpFile))
				File.Delete (dumpFile);
			System.Diagnostics.Process.Start ("kill", "-PROF " + pid);
			
			string fileName = null;
			int tries = 40;
			
			while (fileName == null) {
				if (--tries == 0)
					return null;

				System.Threading.Thread.Sleep (500);
				if (!File.Exists (dumpFile))
					continue;
					
				StreamReader freader = null;
				try {
					freader = new StreamReader (dumpFile);
					fileName = freader.ReadToEnd ();
					freader.Close ();
				} catch {
					if (freader != null)
						freader.Close ();
				}
			}
			return new ObjectMapReader (fileName);*/
			return null;
		}

		//
		// Code to read the log files generated at runtime
		//

		private void ReadLogFile (BinaryReader reader)
		{
			currentData = new HeapShotData ();
			
			// Some stock types
			currentData.TypesList.Add (new TypeInfo () { Code = -1, Name = "<Unknown>" });   // 0
			currentData.TypesList.Add (new TypeInfo () { Code = -2, Name = "<Stack>" });     // 1
			currentData.TypesList.Add (new TypeInfo () { Code = -3, Name = "<Finalizer>" }); // 2
			currentData.TypesList.Add (new TypeInfo () { Code = -4, Name = "<Handle>" });    // 3
			currentData.TypesList.Add (new TypeInfo () { Code = -5, Name = "<Other Root>" });      // 4
			currentData.TypesList.Add (new TypeInfo () { Code = -6, Name = "<Misc Root>" }); // 5
			
			Header h = Header.Read (reader);
			while (reader.BaseStream.Position < reader.BaseStream.Length) {
				BufferHeader bheader = BufferHeader.Read (reader);
				var endPos = reader.BaseStream.Position + bheader.Length;
//				Console.WriteLine ("BUFFER ThreadId: " + bheader.ThreadId);
				currentObjBase = bheader.ObjBase;
				currentPtrBase = bheader.PtrBase;
				while (reader.BaseStream.Position < endPos) {
					Event e = Event.Read (reader);
//					Console.WriteLine ("Event: " + e);
					if (e is MetadataEvent)
						ReadLogFileChunk_Type ((MetadataEvent)e);
					else if (e is HeapEvent)
						ReadLogFileChunk_Object ((HeapEvent)e);
					else if (e is GcEvent)
						ReadGcEvent ((GcEvent)e);
				}
			}
		}

		private void ReadLogFileChunk_Type (MetadataEvent t)
		{
			if (t.MType != MetadataEvent.MetaDataType.Class)
				return;
			
			TypeInfo ti = new TypeInfo ();
			ti.Code = t.Pointer + currentPtrBase;
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
		
		void ReadGcEvent (GcEvent ge)
		{
			if (ge.EventType == GcEvent.GcEventType.Start)
				currentData.ResetHeapData ();
		}
		
		int shotCount;
		
		private void ReadLogFileChunk_Object (HeapEvent he)
		{
			if (he.Type == HeapEvent.EventType.Start) {
				Console.WriteLine ("ppe: START");
				return;
			}
			else if (he.Type == HeapEvent.EventType.End) {
				Console.WriteLine ("ppe: END");
				HeapSnapshot shot = new HeapSnapshot ();
				shotCount++;
				shot.Build (shotCount.ToString (), currentData);
				shots.Add (shot);
			}
			if (he.Type == HeapEvent.EventType.Object) {
				ObjectInfo ob = new ObjectInfo ();
				ob.Code = currentObjBase + he.Object;
				ob.Size = he.Size;
				ob.RefsIndex = currentData.ReferenceCodes.Count;
				ob.RefsCount = he.ObjectRefs != null ? he.ObjectRefs.Length : 0;
				currentData.ObjectTypeCodes.Add (currentPtrBase + he.Class);
				totalMemory += ob.Size;
				if (ob.Size != 0)
					currentData.RealObjectCount++;
				
				// Read referenceCodes
				
				ulong lastOff = 0;
				for (int n=0; n < ob.RefsCount; n++) {
					currentData.ReferenceCodes.Add (he.ObjectRefs [n] + currentObjBase);
					lastOff += he.RelOffset [n];
					currentData.FieldReferenceCodes.Add (lastOff);
				}
				currentData.ObjectsList.Add (ob);
			}
			else if (he.Type == HeapEvent.EventType.Root) {
				for (int n=0; n<he.RootRefs.Length; n++) {
					ObjectInfo ob = new ObjectInfo ();
					ob.Size = 0;
					ob.RefsIndex = currentData.ReferenceCodes.Count;
					ob.RefsCount = 1;
					long type = UnknownTypeId;
					switch (he.RootRefTypes [n] & HeapEvent.RootType.TypeMask) {
					case HeapEvent.RootType.Stack: type = -2; ob.Code = StackObjectId; break;
					case HeapEvent.RootType.Finalizer: type = -3; ob.Code = --rootId; break;
					case HeapEvent.RootType.Handle: type = -4; ob.Code = --rootId; break;
					case HeapEvent.RootType.Other: type = -5; ob.Code = --rootId; break;
					case HeapEvent.RootType.Misc: type = -6; ob.Code = --rootId; break;
					default:
						Console.WriteLine ("pp1:"); break;
					}
					currentData.ObjectTypeCodes.Add (type);
					currentData.ReferenceCodes.Add (he.RootRefs [n] + currentObjBase);
					currentData.FieldReferenceCodes.Add (0);
					currentData.ObjectsList.Add (ob);
					currentData.RealObjectCount++;
				}
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
