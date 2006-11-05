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
using System.IO;
using System.Text.RegularExpressions;

namespace HeapShot.Reader
{
	public class ReferenceNode
	{
		bool inverse;
		public string TypeName;
		public int RefCount;
		public int RefsToParent;
		public uint TotalMemory;
		
		public ArrayList references;
		public ArrayList fieldReferences;
		public Hashtable refObjects = new Hashtable ();
		public Hashtable parentObjects = new Hashtable ();
		
		public ReferenceNode (string typeName, bool inverse)
		{
			TypeName = typeName;
			this.inverse = inverse;
		}
		
		public uint AverageSize {
			get { return RefCount != 0 ? (uint) (TotalMemory / RefCount) : 0; }
		}
		
		public ICollection FieldReferences {
			get { return fieldReferences != null ? fieldReferences : (ICollection) Type.EmptyTypes; }
		}
		
		public void AddReference (ObjectInfo obj)
		{
			AddReference (null, obj, null);
		}
		
		public void AddReference (ObjectInfo parentObject, ObjectInfo obj)
		{
			AddReference (parentObject, obj, (string) null);
		}
		
		void AddReference (ObjectInfo parentObject, ObjectInfo obj, string fieldName)
		{
			if (parentObject != null && !parentObjects.ContainsKey (parentObject)) {
				parentObjects [parentObject] = parentObject;
				RefsToParent++;
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
			
			if (refObjects.ContainsKey (obj))
				return;

			RefCount++;
			
			refObjects.Add (obj, obj);
			TotalMemory += obj.Size;
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

				references = new ArrayList ();
				foreach (ObjectInfo obj in refObjects.Keys) {
					if (inverse) {
						if (obj.Referencers != null) {
							for (int n=0; n<obj.Referencers.Count; n++) {
								ObjectInfo oref = (ObjectInfo) obj.Referencers [n];
								ReferenceNode cnode = GetReferenceNode (oref.Type.Name);
								string fname = oref.GetReferencerField (obj);
								cnode.AddReference (obj, oref, fname);
							}
						}
					} else {
						if (obj.References != null) {
							foreach (ObjectReference oref in obj.References) {
								if (oref.Object == null) continue;
								ReferenceNode cnode = GetReferenceNode (oref.Object.Type.Name);
								string fname = obj.GetReferencerField (oref.Object);
								cnode.AddReference (obj, oref.Object, fname);
							}
						}
					}
				}
				foreach (ReferenceNode r in references)
					r.Flush ();

				refObjects = null;
				references.Sort (new ReferenceSorter ());
				return references;
			}
		}
		
		public void Flush ()
		{
			parentObjects = null;
		}
		
		public ReferenceNode GetReferenceNode (string name)
		{
			foreach (ReferenceNode cnode in references) {
				if (cnode.TypeName == name)
					return cnode;
			}
			ReferenceNode nod = new ReferenceNode (name, inverse);
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
