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
using HeapShot.Reader.Graphs;

namespace HeapShot.Reader
{
	public class ReferenceNode: IHeapShotData
	{
		static List<FieldReference> emptyFieldReferences = new List<HeapShot.Reader.FieldReference> ();
		bool inverse;
		HeapSnapshot map;
		
		public string TypeName;
		public long RefCount;
		public int RefsToParent;
		public int RefsToRoot;
		public ulong RootMemory;
		public ulong TotalMemory;
		int type;
		bool globalRefs;
		
		List<ReferenceNode> references;
		List<FieldReference> fieldReferences;
		Dictionary<int,RootRefInfo> refObjects = new Dictionary<int,RootRefInfo> ();
		Dictionary<int,int> parentObjects = new Dictionary<int,int> ();
		PathTree pathTree;
		
		public ReferenceNode (HeapSnapshot map, int type, bool inverse)
		{
			this.map = map;
			this.type = type;
			TypeName = map.GetTypeName (type);
			this.inverse = inverse;
		}
		
		public ReferenceNode (HeapSnapshot map, int type, PathTree pathTree)
		{
			this.map = map;
			this.type = type;
			TypeName = map.GetTypeName (type);
			this.pathTree = pathTree;
			FillRootPaths ();
		}
		
		public uint AverageSize {
			get { return RefCount != 0 ? (uint) (TotalMemory / (ulong)RefCount) : 0; }
		}
		
		public ICollection<FieldReference> FieldReferences {
			get { return fieldReferences != null ? fieldReferences : emptyFieldReferences; }
		}
		
		public void AddGlobalReferences ()
		{
			RefCount = map.GetObjectCountForType (type);
			RefsToParent = 0;
			TotalMemory = map.GetObjectSizeForType (type);
			globalRefs = true;
		}
		
		public RootRefInfo AddReference (int obj, int tnode)
		{
			return AddReference (-1, obj, tnode, 1, map.GetObjectSize (obj), null);
		}
		
		RootRefInfo AddReference (int parentObject, int obj, int tnode, int refsToRoot, ulong rootMem, string fieldName)
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
					f.IsStatic = map.IsStaticObject (obj);
					if (fieldReferences == null)
						fieldReferences = new List<HeapShot.Reader.FieldReference> ();
					fieldReferences.Add (f);
				}
			}
			
			if (refObjects.ContainsKey (obj)) {
				RootRefInfo ri = refObjects [obj];
				ri.References += refsToRoot;
				ri.Memory += rootMem;
				refObjects [obj] = ri;
				return ri;
			}

			RefCount++;
			
			RootRefInfo rr = new RootRefInfo ();
			rr.TreeNode = tnode;
			rr.References = refsToRoot;
			rr.Memory = rootMem;
			refObjects.Add (obj, rr);
			TotalMemory += map.GetObjectSize (obj);
			return rr;
		}
		
		public bool HasReferences {
			get {
				return References.Count > 0 || FieldReferences.Count > 0;
			}
		}
		
		void InitRoots ()
		{
			if (globalRefs) {
				RefsToParent = 0;
				RefCount = 0;
				TotalMemory = 0;
				foreach (int obj in map.GetObjectsByType (type))
					AddReference (obj, 0);
				globalRefs = false;
			}
		}
		
		public List<ReferenceNode> References {
			get {
				if (references != null)
					return references;

				references = new List<ReferenceNode> ();
				InitRoots ();
					
				if (pathTree != null)
					FillTreePathReferences ();
				else if (inverse)
					FillInverseReferences ();
				else
					FillReferences ();
				
				foreach (ReferenceNode r in references)
					r.Flush ();

				refObjects = null;
				return references;
			}
		}
		
		void FillReferences ()
		{
			foreach (KeyValuePair<int,RootRefInfo> entry in refObjects) {
				int obj = entry.Key;
				foreach (int oref in map.GetReferences (obj)) {
					ReferenceNode cnode = GetReferenceNode (oref);
					string fname = map.GetReferencerField (obj, oref);
					cnode.AddReference (obj, oref, 0, 0, 0, fname);
				}
			}
		}
		
		void FillInverseReferences ()
		{
			foreach (KeyValuePair<int,RootRefInfo> entry in refObjects) {
				int obj = entry.Key;
				foreach (int oref in map.GetReferencers (obj)) {
					ReferenceNode cnode = GetReferenceNode (oref);
					string fname = map.GetReferencerField (oref, obj);
					cnode.AddReference (obj, oref, 0, entry.Value.References, entry.Value.Memory, fname);
				}
			}
		}
		
		void FillTreePathReferences ()
		{
			foreach (KeyValuePair<int,RootRefInfo> entry in refObjects) {
				int node = entry.Value.TreeNode;
				int pobj = entry.Key;
				foreach (int cn in pathTree.GetChildNodes (node)) {
					int oref = pathTree.GetNodeObject (cn);
					ReferenceNode cnode = GetReferenceNode (oref);
					string fname = map.GetReferencerField (oref, pobj);
					cnode.AddReference (pobj, oref, cn, entry.Value.References, entry.Value.Memory, fname);
				}
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
			nod.pathTree = pathTree;
			references.Add (nod);
			return nod;
		}
		
		void FillRootPaths ()
		{
			foreach (int node in pathTree.GetRootNodes ()) {
				int pobj = pathTree.GetNodeObject (node);
				AddReference (pobj, node);
			}
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
	
	public class FieldReference: IHeapShotData
	{
		public int RefCount;
		public string FiledName;
		public bool IsStatic;
	}
	
	public struct RootRefInfo
	{
		public int References;
		public ulong Memory;
		public int TreeNode; // Used only on purged trees
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
	
	public interface IHeapShotData
	{
	}
}
