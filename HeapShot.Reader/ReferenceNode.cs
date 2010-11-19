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

namespace HeapShot.Reader
{
	public class ReferenceNode
	{
		bool inverse;
		ObjectMapReader map;
		
		public string TypeName;
		public long RefCount;
		public int RefsToParent;
		public int RefsToRoot;
		public ulong RootMemory;
		public ulong TotalMemory;
		int type;
		bool globalRefs;
		
		public ArrayList references;
		public ArrayList fieldReferences;
		public Dictionary<int,RootRefInfo> refObjects = new Dictionary<int,RootRefInfo> ();
		public Dictionary<int,int> parentObjects = new Dictionary<int,int> ();
		
		public ReferenceNode (ObjectMapReader map, int type, bool inverse)
		{
			this.map = map;
			this.type = type;
			TypeName = map.GetTypeName (type);
			this.inverse = inverse;
		}
		
		public uint AverageSize {
			get { return RefCount != 0 ? (uint) (TotalMemory / (ulong)RefCount) : 0; }
		}
		
		public ICollection FieldReferences {
			get { return fieldReferences != null ? fieldReferences : (ICollection) Type.EmptyTypes; }
		}
		
		public void AddGlobalReferences ()
		{
			RefCount = map.GetObjectCountForType (type);
			RefsToParent = 0;
			TotalMemory = map.GetObjectSizeForType (type);
			globalRefs = true;
		}
		
		public void AddReference (int obj)
		{
			AddReference (-1, obj, 1, map.GetObjectSize (obj), null);
		}
		
		void AddReference (int parentObject, int obj, int refsToRoot, ulong rootMem, string fieldName)
		{
			if (parentObject != -1 && !parentObjects.ContainsKey (parentObject)) {
				parentObjects [parentObject] = parentObject;
				RefsToParent++;
				RefsToRoot += refsToRoot;
				RootMemory += rootMem;
			}

			if (fieldName != null) {
				// Update field reference count
				bool ffound = false;
				if (fieldReferences != null) {
					foreach (FieldReference f in fieldReferences) {
						if (f.FiledName == fieldName) {
							f.RefCount++;
							ffound = true;
							break;
						}
					}
				}
				if (!ffound) {
					FieldReference f = new FieldReference ();
					f.FiledName = fieldName;
					f.RefCount = 1;
					if (fieldReferences == null)
						fieldReferences = new ArrayList ();
					fieldReferences.Add (f);
				}
			}
			
			if (refObjects.ContainsKey (obj)) {
				RootRefInfo ri = refObjects [obj];
				ri.References += refsToRoot;
				ri.Memory += rootMem;
				refObjects [obj] = ri;
				return;
			}

			RefCount++;
			
			RootRefInfo rr = new RootRefInfo ();
			rr.References = refsToRoot;
			rr.Memory = rootMem;
			refObjects.Add (obj, rr);
			TotalMemory += map.GetObjectSize (obj);
		}
		
		public bool HasReferences {
			get {
				return true;
			}
		}
		
		public ArrayList References {
			get {
				if (references != null)
					return references;

				if (globalRefs) {
					RefsToParent = 0;
					RefCount = 0;
					TotalMemory = 0;
					foreach (int obj in map.GetObjectsByType (type))
						AddReference (obj);
					globalRefs = false;
				}
				
				references = new ArrayList ();
				foreach (KeyValuePair<int,RootRefInfo> entry in refObjects) {
					int obj = entry.Key;
					if (inverse) {
						foreach (int oref in map.GetReferencers (obj)) {
							ReferenceNode cnode = GetReferenceNode (oref);
							string fname = map.GetReferencerField (oref, obj);
							cnode.AddReference (obj, oref, entry.Value.References, entry.Value.Memory, fname);
						}
					} else {
						foreach (int oref in map.GetReferences (obj)) {
							ReferenceNode cnode = GetReferenceNode (oref);
							string fname = map.GetReferencerField (obj, oref);
							cnode.AddReference (obj, oref, 0, 0, fname);
						}
					}
				}
				foreach (ReferenceNode r in references)
					r.Flush ();

				refObjects = null;
				return references;
			}
		}
		
		public void Flush ()
		{
			parentObjects = null;
		}
		
		public ReferenceNode GetReferenceNode (int obj)
		{
			string name = map.GetObjectTypeName (obj);
			foreach (ReferenceNode cnode in references) {
				if (cnode.TypeName == name)
					return cnode;
			}
			ReferenceNode nod = new ReferenceNode (map, map.GetObjectType (obj), inverse);
			references.Add (nod);
			return nod;
		}
		
		public void Print (int maxLevels)
		{
			Print (0, maxLevels);
		}
		
		void Print (int level, int maxLevels)
		{
			Console.Write (new string (' ', level*3));
			Console.WriteLine (RefCount + " " + TypeName);
			if (fieldReferences != null && fieldReferences.Count != 0) {
				Console.Write (new string (' ', level*3) + new string (' ', RefCount.ToString().Length) + " ");
				Console.Write ("(");
				for (int n=0; n<fieldReferences.Count; n++) {
					if (n > 0) Console.Write (", ");
					FieldReference f = (FieldReference) fieldReferences [n];
					Console.Write (f.FiledName + ":" + f.RefCount);
				}
				Console.WriteLine (")");
			}
			if (level < maxLevels) {
				foreach (ReferenceNode cnode in References)
					cnode.Print (level + 1, maxLevels);
			}
		}
	}
	
	public class FieldReference
	{
		public int RefCount;
		public string FiledName;
	}
	
	public struct RootRefInfo
	{
		public int References;
		public ulong Memory;
	}
	
	class ReferenceSorter: IComparer
	{
		public int Compare (object x, object y)
		{
			ReferenceNode t1 = (ReferenceNode) x;
			ReferenceNode t2 = (ReferenceNode) y;
			if (t1.RefCount == t2.RefCount)
				return 0;
			else if (t1.RefCount > t2.RefCount)
				return -1;
			else
				return 1;
		}
	}
}
