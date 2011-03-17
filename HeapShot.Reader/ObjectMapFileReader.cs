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
using System.Net.Sockets;

namespace HeapShot.Reader {

	public class ObjectMapReader: IDisposable
	{
		const string log_file_label = "heap-shot logfile";
		
		int port = -1;
		long lastReadPosition;
		bool insideBuffer;
		long bufferEndPos;
		
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
		
		public event EventHandler<HeapShotEventArgs> HeapSnapshotAdded;
		
		internal ObjectMapReader ()
		{
		}
		
		public ObjectMapReader (string filename)
		{
			this.name = filename;
			
			currentData = new HeapShotData ();
			
			// Some stock types
			currentData.TypesList.Add (new TypeInfo () { Code = -1, Name = "<Unknown>" });   // 0
			currentData.TypesList.Add (new TypeInfo () { Code = -2, Name = "<Stack>" });     // 1
			currentData.TypesList.Add (new TypeInfo () { Code = -3, Name = "<Finalizer>" }); // 2
			currentData.TypesList.Add (new TypeInfo () { Code = -4, Name = "<Handle>" });    // 3
			currentData.TypesList.Add (new TypeInfo () { Code = -5, Name = "<Other Root>" });      // 4
			currentData.TypesList.Add (new TypeInfo () { Code = -6, Name = "<Misc Root>" }); // 5
		}
		
		public void Dispose ()
		{
		}
		
		public bool Read ()
		{
			try {
				if (!File.Exists (name))
					return false;
				DateTime tim = File.GetLastWriteTime (name);
				if (tim == timestamp)
					return true;
				
				Stream stream = new FileStream (name, FileMode.Open, FileAccess.Read);
	
				BinaryReader reader;
				reader = new BinaryReader (stream);
				ReadLogFile (reader);
				reader.Close ();
				timestamp = File.GetLastWriteTime (name);
				
				return true;
			} catch (Exception ex) {
				Console.WriteLine (ex);
				return false;
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
		
		public IEnumerable<HeapSnapshot> HeapShots {
			get { return shots; }
		}
		
		public void ForceSnapshot ()
		{
			if (port == -1) {
				Read ();
				if (port == -1)
					throw new Exception ("Log file could not be opened");
			}
			using (TcpClient client = new TcpClient ()) {
				client.Connect ("127.0.0.1", port);
				using (StreamWriter sw = new StreamWriter (client.GetStream ())) {
					sw.WriteLine ("heapshot");
					sw.Flush ();
				}
			}
			System.Threading.Thread.Sleep (3000);
		}

		//
		// Code to read the log files generated at runtime
		//

		private void ReadLogFile (BinaryReader reader)
		{
			if (lastReadPosition == 0) {
				Header h = Header.Read (reader);
				port = h.Port;
				lastReadPosition = reader.BaseStream.Position;
			}
			else {
				reader.BaseStream.Position = lastReadPosition;
			}
			
			while (reader.BaseStream.Position < reader.BaseStream.Length) {
				if (!insideBuffer) {
					BufferHeader bheader = BufferHeader.Read (reader);
					bufferEndPos = reader.BaseStream.Position + bheader.Length;
//					Console.WriteLine ("BUFFER ThreadId: " + bheader.ThreadId + " End:" + bufferEndPos + " Len:" + reader.BaseStream.Length);
					currentObjBase = bheader.ObjBase;
					currentPtrBase = bheader.PtrBase;
					insideBuffer = true;
					lastReadPosition = reader.BaseStream.Position;
				}
				while (reader.BaseStream.Position < bufferEndPos) {
					Event e = Event.Read (reader);
//					Console.WriteLine ("Event: " + e + " " + reader.BaseStream.Position);
					if (e is MetadataEvent)
						ReadLogFileChunk_Type ((MetadataEvent)e);
					else if (e is HeapEvent)
						ReadLogFileChunk_Object ((HeapEvent)e);
					else if (e is GcEvent)
						ReadGcEvent ((GcEvent)e);
					lastReadPosition = reader.BaseStream.Position;
				}
				insideBuffer = false;
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
				AddShot (shot);
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
		
		void AddShot (HeapSnapshot shot)
		{
			shots.Add (shot);
			if (HeapSnapshotAdded != null)
				HeapSnapshotAdded (this, new HeapShotEventArgs (shot));
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
