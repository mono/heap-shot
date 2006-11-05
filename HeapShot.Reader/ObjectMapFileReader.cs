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

namespace HeapShot.Reader {

	public class ObjectMapReader 
	{
		const uint magic_number = 0x4eabbdd1;
		const int expected_log_version = 5;
		const int expected_summary_version = 2;
		const string log_file_label = "heap-shot logfile";
		const string summary_file_label = "heap-shot summary";
		
		bool terminated_normally = true;
		string name;
		DateTime timestamp;
		
		public ObjectMapReader (string filename)
		{
			this.name = filename;
			
			Stream stream;
			stream = new FileStream (filename, FileMode.Open, FileAccess.Read);

			BinaryReader reader;
			reader = new BinaryReader (stream);
			
			ReadPreamble (reader);
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
		
		public static ObjectMapReader CreateProcessSnapshot (int pid)
		{
			string dumpFile = "/tmp/heap-shot-dump";
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
			return new ObjectMapReader (fileName);
		}

		///////////////////////////////////////////////////////////////////

		private void Spew (string format, params object [] args)
		{
			string message;
			message = String.Format (format, args);
			Console.WriteLine (message);
		}

		///////////////////////////////////////////////////////////////////

		// Return true if this is a summary file, false if it is a log file.
		private bool ReadPreamble (BinaryReader reader)
		{
			uint this_magic;
			this_magic = reader.ReadUInt32 ();
			if (this_magic != magic_number) {
				string msg;
				msg = String.Format ("Bad magic number: expected {0}, found {1}",
						     magic_number, this_magic);
				throw new Exception (msg);
			}

			int this_version;
			this_version = reader.ReadInt32 ();

			string this_label;
			bool is_summary;
			int expected_version;

			this_label = reader.ReadString ();
			if (this_label == log_file_label) {
				is_summary = false;
				expected_version = expected_log_version;
			} else if (this_label == summary_file_label) {
				is_summary = true;
				expected_version = expected_summary_version;
			} else
				throw new Exception ("Unknown file label in heap-shot outfile");

			if (this_version != expected_version) {
				string msg;
				msg = String.Format ("Version error in {0}: expected {1}, found {2}",
						     this_label, expected_version, this_version);
				throw new Exception (msg);
			}

			return is_summary;
		}

		//
		// Code to read the log files generated at runtime
		//

		// These need to agree w/ the definitions in outfile-writer.c
		const byte TAG_TYPE      = 0x01;
		const byte TAG_OBJECT    = 0x06;
		const byte TAG_EOS       = 0xff;

		private void ReadLogFile (BinaryReader reader)
		{
			int chunk_count = 0;

			try {
				while (ReadLogFileChunk (reader))
					++chunk_count;

			} catch (System.IO.EndOfStreamException) {
				// This means that the outfile was truncated.
				// In that case, just do nothing --- except if the file
				// claimed that things terminated normally.
				if (terminated_normally)
					throw new Exception ("The heap log did not contain TAG_EOS, "
							     + "but the outfile was marked as having been terminated normally, so "
							     + "something must be terribly wrong.");
			}
			BuildMap ();
			Spew ("Processed {0} chunks", chunk_count);
		}

		private bool ReadLogFileChunk (BinaryReader reader)
		{

			// FIXME: This will fail on truncated outfiles

			byte tag = reader.ReadByte ();

			switch (tag) {
			case TAG_TYPE:
				ReadLogFileChunk_Type (reader);
				break;
					
			case TAG_OBJECT:
				ReadLogFileChunk_Object (reader);
				break;
				
			case TAG_EOS:
				//Spew ("Found EOS");
				return false;

			default:
				throw new Exception ("Unknown tag! " + tag);
			}

			return true;
		}
		
		private void ReadLogFileChunk_Type (BinaryReader reader)
		{
			uint code;
			code = reader.ReadUInt32 ();

			string name;
			name = reader.ReadString ();
			
			ArrayList fields = new ArrayList ();
			uint fcode;
			while ((fcode = reader.ReadUInt32 ()) != 0) {
				FieldInfo f = new FieldInfo ();
				f.Code = fcode;
				f.Name = reader.ReadString ();
				fields.Add (f);
			}

			RegisterType (code, name, (FieldInfo[]) fields.ToArray (typeof(FieldInfo)));
		}
		
		private void ReadLogFileChunk_Object (BinaryReader reader)
		{
			uint code;
			code = reader.ReadUInt32 ();

			uint typeCode;
			typeCode = reader.ReadUInt32 ();

			uint size;
			size = reader.ReadUInt32 ();
			
			// Read references
			
			ArrayList refs = new ArrayList ();
			uint oref;
			while ((oref = reader.ReadUInt32 ()) != 0) {
				ObjectReference o = new ObjectReference ();
				o.ObjectCode = oref;
				o.FieldCode = reader.ReadUInt32 ();
				refs.Add (o);
			}

			ObjectReference[] array = refs.Count > 0 ? (ObjectReference[]) refs.ToArray (typeof(ObjectReference)) : null;
			RegisterObject (code, typeCode, size, array);
		}
		
		Hashtable types = new Hashtable ();
		Dictionary<uint,ObjectInfo> objects = new Dictionary<uint,ObjectInfo> ();
		Hashtable typesByName = new Hashtable ();
		
		void RegisterType (uint id, string name, FieldInfo[] fields)
		{
			TypeInfo t = new TypeInfo (id, name, fields);
			types [id] = t;
			typesByName [name] = t;
		}
		
		void RegisterObject (uint id, uint typeId, uint size, ObjectReference[] refs)
		{
			TypeInfo type = (TypeInfo) types [typeId];
			if (type == null) {
				Spew ("Type not found");
				return;
			}
			ObjectInfo ob = new ObjectInfo (id, type, size, refs);
			type.Objects.Add (ob);
			objects [id] = ob;
		}
		
		void BuildMap ()
		{
			foreach (ObjectInfo o in objects.Values) {
				if (o.References != null) {
					for (int n=0; n<o.References.Length; n++) {
						ObjectInfo refo = GetObject (o.References [n].ObjectCode);
						if (refo != null) {
							o.References [n].Object = refo;
							if (refo.Referencers == null)
								refo.Referencers = new ArrayList (2);
							refo.Referencers.Add (o);
						}
					}
				}
			}
			foreach (TypeInfo t in types.Values) {
				t.Objects.TrimToSize();
			}
		}
		
		public ReferenceNode GetReferenceTree (string typeName, bool inverse)
		{
			ReferenceNode nod = new ReferenceNode (typeName, inverse);
			foreach (ObjectInfo obj in GetObjectsByType (typeName)) {
				nod.AddReference (obj);
			}
			nod.Flush ();
			return nod;
		}
		
		public ObjectInfo GetObject (uint id)
		{
			ObjectInfo val;
			if (!objects.TryGetValue (id, out val))
				return null;
			else
				return val;
		}
		
		public ICollection GetObjectsByType (string typeName)
		{
			TypeInfo t = (TypeInfo) typesByName [typeName];
			if (t == null)
				return new ObjectInfo [0];
			else
				return t.Objects;
		}
		
		public ICollection GetTypes ()
		{
			ArrayList list = new ArrayList ();
			list.AddRange (types.Values);
			list.Sort (new TypeSorter ());
			return list;
		}
		
		public void RemoveData (ObjectMapReader otherReader)
		{
			Hashtable toDelete = new Hashtable ();
			
			foreach (uint code in otherReader.objects.Keys) {
				if (objects.ContainsKey (code)) {
					ObjectInfo oi = objects [code];
					toDelete [oi] = oi;
				}
			}
			
			ArrayList emptyTypes = new ArrayList ();
			foreach (TypeInfo t in types.Values) {
				for (int n = t.Objects.Count - 1; n >= 0; n--) {
					if (toDelete.Contains (t.Objects [n]))
						t.Objects.RemoveAt (n);
				}
				if (t.Objects.Count == 0)
					emptyTypes.Add (t);
			}
			
			foreach (TypeInfo t in emptyTypes) {
				types.Remove (t.Code);
				typesByName.Remove (t.Name);
			}

			// Rebuild referencers list
			foreach (ObjectInfo o in objects.Values)
				o.Referencers = null;
			BuildMap ();
		}
	}
	
	class TypeSorter: IComparer
	{
		public int Compare (object x, object y)
		{
			TypeInfo t1 = (TypeInfo) x;
			TypeInfo t2 = (TypeInfo) y;
			if (t1.Objects.Count == t2.Objects.Count)
				return 0;
			else if (t1.Objects.Count > t2.Objects.Count)
				return -1;
			else
				return 1;
		}
	}
}
