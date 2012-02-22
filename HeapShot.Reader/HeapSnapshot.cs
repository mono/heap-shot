//
// OutfileReader.cs
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
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using MonoDevelop.Profiler;
using HeapShot.Reader.Graphs;

namespace HeapShot.Reader {

	public class HeapSnapshot 
	{
		string name;
		DateTime timestamp;
		ulong totalMemory;
		
		ObjectInfo[] objects;
		TypeInfo[] types;
		string[] fieldNames;
		int[] typeIndices;
		int[] references;
		int[] inverseRefs;
		int[] fieldReferences;
		bool[] filteredObjects;
		int filteredCount;
		long[] objectCodes;
		
/*
		 * Here is a visual example of how tables are filled:
		 * 
		 * objects: sorted array of ObjectInfo objects (rXXX means reference to ObjectInfo with code XXX)
		 *                     0    1    2    3    4    5
		 *                    ---  ---  ---  ---  ---  ---
		 * Code:              100  101  102  103  104  105
		 * Type:               0    0    0    1    1    1
		 * RefsIndex:          0    2    3    -    -    -
		 * RefsCount:          2    1    1    0    0    0
		 * InverseRefsIndex:   -    -    2    3    1    0
		 * InverseRefsCount:   0    0    1    1    1    1
		 *
		 * objectCodes: sorted array of object codes, used for binary search. The found index is used to query 'objects'
		 *   0    1    2    3    4    5
		 * [100][101][102][103][104][105]
		 *
		 * types: array of TypeInfo objects (rXXX means reference to TypeInfo with code XXX)
		 *    0     1
		 * [r201][r200]
		 * 
		 * typeCodes: sorted array of type codes, used for binary search. The found index is used to query 'typeIndices'
		 *   0    1
		 * [200][201]
		 *
		 * typeIndices: from an index found in 'typeCodes', returns an index for 'types'
		 *  0  1
		 * [1][0]
		 * 
		 * referenceCodes: object references. ObjectInfo.RefsIndex is the position
		 * in this array where references for an object start. ObjectInfo.RefsCount is
		 * the number of references of the object:
		 *   0    1    2    3
		 * [105][104][102][103]
		 * 
		 * references: same as 'referenceCodes', but using object indexes instead of codes
		 *  0  1  2  3
		 * [5][4][2][3]
		 * 
		 * inverseRefs: inverse reference indexes
		 *  0  1  2  3
		 * [0][0][1][2]

 
 
*/
		
		public HeapSnapshot ()
		{
		}
		
		public string Name {
			get { return name; }
		}
		
		public DateTime Timestamp {
			get { return timestamp; }
		}
		
		public ulong TotalMemory {
			get { return totalMemory; }
		}
		
		public uint NumObjects {
			get { return (uint) (objects.Length - filteredCount); }
		}
		
		//
		// Code to read the log files generated at runtime
		//

		HashSet<long> types_not_found = new HashSet<long> ();
		internal void Build (string name, HeapShotData data)
		{
			this.name = name;
			
			// Build an array of type indices and sort it
			
			types = data.TypesList.ToArray ();
			TypeComparer typeComparer = new TypeComparer ();
			typeComparer.types = types;
			
			typeIndices = new int [types.Length];
			for (int n=0; n < types.Length; n++)
				typeIndices [n] = n;
			Array.Sort<int> (typeIndices, typeComparer);
			// Sorted array of codes needed for the binary search
			long[] typeCodes = new long [types.Length];	
			for (int n=0; n < types.Length; n++) {
				typeCodes [n] = types [typeIndices[n]].Code;
			}
			
			// Build an array of object indices and sort it
			
			RefComparer objectComparer = new RefComparer ();
			objectComparer.objects = data.ObjectsList;
			
			int[] objectIndices = new int [data.ObjectsList.Count];
			for (int n=0; n < data.ObjectsList.Count; n++)
				objectIndices [n] = n;
			Array.Sort<int> (objectIndices, objectComparer);
			// Sorted array of codes needed for the reordering and binary search
			objectCodes = new long [data.ObjectsList.Count];	
			for (int n=0; n < data.ObjectsList.Count; n++)
				objectCodes [n] = data.ObjectsList [objectIndices[n]].Code;
			
			// Merge duplicates
			
			long[] mergedObjectCodes = new long [data.RealObjectCount];
			ObjectInfo[] mergedObjects = new ObjectInfo [data.RealObjectCount];
			long[] mergedReferenceCodes = new long [data.ReferenceCodes.Count];
			ulong[] mergedFieldReferenceCodes = new ulong [data.FieldReferenceCodes.Count];
			long last = long.MinValue;
			int mergedObjectPos = -1;
			int mergedRefPos = 0;
			
			for (int n=0; n<objectCodes.Length; n++) {
				ObjectInfo ob = data.ObjectsList [objectIndices[n]];
				if (n == 0 || objectCodes [n] != last) {
					last = objectCodes [n];
					mergedObjectPos++;
					mergedObjects [mergedObjectPos] = ob;
					mergedObjects [mergedObjectPos].RefsIndex = mergedRefPos;
					mergedObjects [mergedObjectPos].RefsCount = 0; // Refs are being added below
					mergedObjectCodes [mergedObjectPos] = mergedObjects [mergedObjectPos].Code;

					// Find the type index
					int i = Array.BinarySearch<long> (typeCodes, data.ObjectTypeCodes [objectIndices[n]]);
					if (i >= 0) {
						i = typeIndices [i];
						mergedObjects [mergedObjectPos].Type = i;
						types [i].ObjectCount++;
						types [i].TotalSize += mergedObjects [mergedObjectPos].Size;
					} else {
						mergedObjects [mergedObjectPos].Type = 0;
						long type_not_found = data.ObjectTypeCodes [objectIndices [n]];
						if (!types_not_found.Contains (type_not_found)) {
							types_not_found.Add (type_not_found);
							Console.WriteLine ("Type not found: " + type_not_found);
						}
					}
				}
				int baseRefIndex = ob.RefsIndex;
				int refsCount = ob.RefsCount;
				for (int r = baseRefIndex; r < baseRefIndex + refsCount; r++, mergedRefPos++) {
					mergedReferenceCodes [mergedRefPos] = data.ReferenceCodes [r];
					mergedFieldReferenceCodes [mergedRefPos] = data.FieldReferenceCodes [r];
				}
				mergedObjects [mergedObjectPos].RefsCount += refsCount;
				totalMemory += mergedObjects [mergedObjectPos].Size;
			}
			
			objects = mergedObjects;
			objectCodes = mergedObjectCodes;
			int missingRefs = 0;
			
			// Build the array of referenceCodes, but using indexes
			// Also calculate the number of inverse references for each object
			references = new int [mergedReferenceCodes.Length];
			
			for (int n=0; n<mergedReferenceCodes.Length; n++) {
				int i = Array.BinarySearch (objectCodes, mergedReferenceCodes[n]);
				if (i >= 0) {
					references[n] = i;
					objects [i].InverseRefsCount++;
				} else {
					//Console.WriteLine ("Referenced object not found: " + mergedReferenceCodes[n]);
					references[n] = 0;
					missingRefs++;
				}
			}
			
			Console.WriteLine ("pp Missing references: " + missingRefs + " of " + mergedReferenceCodes.Length);
			
			// Calculate the array index of inverse referenceCodes for each object
			
			int[] invPositions = new int [objects.Length];	// Temporary array to hold reference positions
			int rp = 0;
			for (int n=0; n<objects.Length; n++) {
				objects [n].InverseRefsIndex = rp;
				invPositions [n] = rp;
				rp += objects [n].InverseRefsCount;
			}
			
			// Build the array of inverse referenceCodes
			// Also calculate the index of each field name
			
			inverseRefs = new int [mergedReferenceCodes.Length];
			fieldReferences = new int [mergedReferenceCodes.Length];
			
			for (int ob=0; ob < objects.Length; ob++) {
				long t = objects [ob].Type;
				int fi = types [t].FieldsIndex;
				int nf = fi + types [t].FieldsCount;
				int sr = objects [ob].RefsIndex;
				int er = sr + objects [ob].RefsCount;
				for (; sr<er; sr++) {
					int i = references [sr];
					if (i != -1) {
						inverseRefs [invPositions [i]] = ob;
						invPositions [i]++;
					}
					// If the reference is bound to a field, locate the field
					ulong fr = mergedFieldReferenceCodes [sr];
					if (fr != 0) {
						for (int k=fi; k<nf; k++) {
							if (data.FieldCodes [k] == fr) {
								fieldReferences [sr] = k;
								break;
							}
						}
					}
				}
			}
		}
		
		class RefComparer: IComparer <int> {
			public List<ObjectInfo> objects;
			
			public int Compare (int x, int y) {
				return objects [x].Code.CompareTo (objects [y].Code);
			}
		}
		
		class TypeComparer: IComparer <int> {
			public TypeInfo[] types;
			
			public int Compare (int x, int y) {
				return types [x].Code.CompareTo (types [y].Code);
			}
		}
		
		public ReferenceNode GetReferenceTree (string typeName, bool inverse)
		{
			int type = GetTypeFromName (typeName);
			if (type != -1)
				return GetReferenceTree (type, inverse);
			else
				return new ReferenceNode (this, type, inverse);
		}
		
		public ReferenceNode GetReferenceTree (int type, bool inverse)
		{
			ReferenceNode nod = new ReferenceNode (this, type, inverse);
			nod.AddGlobalReferences ();
			nod.Flush ();
			return nod;
		}
		
		public ReferenceNode GetRootReferenceTree (IProgressListener listener, int type)
		{
			PathTree ptree = GetRoots (listener, type);
			if (ptree == null)
				return null;
			ReferenceNode nod = new ReferenceNode (this, type, ptree);
			nod.Flush ();
			return nod;
		}
		
		public Graph CreateGraph (int minInstances)
		{
			Graph gr = new Graph (this);
			for (int n=0; n<objects.Length; n++) {
				gr.AddObject (n);
			}
			for (int n=0; n<objects.Length; n++) {
				foreach (int ob in GetReferences (n))
					gr.AddReference (n, ob, 0);
			}
			
			gr.Flush ();
			return gr;
		}
		
		class RootInfo
		{
			public List<int> Path = new List<int> ();
			public Dictionary<int,int[]> Roots = new Dictionary<int,int[]> ();
			public Dictionary<int,int> Visited = new Dictionary<int,int> ();
			public Dictionary<int,int> BaseObjects = new Dictionary<int,int> ();
			public Dictionary<int,int> DeadEnds = new Dictionary<int,int> ();
			public Dictionary<int,int> Allobs = new Dictionary<int,int> ();
			public int nc;
		}

		// Returns a list of paths. Each path is a sequence of objects, starting
		// on an object of type 'type' and ending on a root.
		public PathTree GetRoots (IProgressListener listener, int type)
		{
			RootInfo rootInfo = new RootInfo ();
			PathTree pathTree = new PathTree (this);

			foreach (int obj in GetObjectsByType (type))
				rootInfo.BaseObjects [obj] = obj;

			int nc = 0;
			foreach (int obj in GetObjectsByType (type)) {
				
				if (listener.Cancelled)
					return null;
				
				rootInfo.nc = 0;
				
				FindRoot (rootInfo, pathTree, obj);
				
				// Register partial paths to the root, to avoid having to
				// recalculate them again
				
//				if (nc % 100 == 0)
//					Console.WriteLine ("NC: " + nc + " " + rootInfo.Roots.Count);
				
				pathTree.AddBaseObject (obj);
				foreach (KeyValuePair<int, int[]> e in rootInfo.Roots) {
					pathTree.AddPath (e.Value);
				}
				rootInfo.Visited.Clear ();
				rootInfo.Roots.Clear ();
				nc++;

				double newp = (double)nc / (double)rootInfo.BaseObjects.Count;
				listener.ReportProgress ("Looking for roots", newp);
			}
			
			pathTree.Flush ();
			return pathTree;
		}
		
		// It returns -2 of obj is a dead end
		// Returns n >= 0, if all paths starting at 'obj' end in objects already
		// visited. 'n' is the index of a node in rootInfo.Path, which is the closest
		// visited node found
		// Returns -1 otherwise.
		// This return value is used to detect dead ends.
		
		int FindRoot (RootInfo rootInfo, PathTree pathTree, int obj)
		{
			if (rootInfo.DeadEnds.ContainsKey (obj))
				return -2;
			
			int curval;
			if (rootInfo.Visited.TryGetValue (obj, out curval)) {
				// The object has already been visited
				if (rootInfo.Path.Count >= curval) {
					return rootInfo.Path.IndexOf (obj);
				}
			}
			rootInfo.Visited [obj] = rootInfo.Path.Count;
			
			int treePos = pathTree.GetObjectNode (obj);
			if (treePos != -1) {
				// If this object already has partial paths to roots,
				// reuse them.
				FindTreeRoot (rootInfo.Path, rootInfo.Roots, pathTree, treePos);
				return -1;
			}
			
			rootInfo.Path.Add (obj);
			
			bool hasrefs = false;
			int findresult = int.MaxValue;
			foreach (int oref in GetReferencers (obj)) {
				hasrefs = true;
				if (!rootInfo.BaseObjects.ContainsKey (oref)) {
					int fr = FindRoot (rootInfo, pathTree, oref);
					if (fr != -2 && fr < findresult)
						findresult = fr;
				}
			}
			
			if (!hasrefs) {
				// A root
				rootInfo.Visited.Remove (obj);
				RegisterPath (rootInfo.Roots, rootInfo.Path, obj);
				findresult = -1;
			}
			
			rootInfo.Path.RemoveAt (rootInfo.Path.Count - 1);
			
			// If all children paths end in nodes already visited, it means that it is a dead end.
			if (findresult >= rootInfo.Path.Count) {
				rootInfo.DeadEnds [obj] = obj;
//				Console.WriteLine ("de: " + findresult);
			}
			
			return findresult;
		}
		
		void FindTreeRoot (List<int> path, Dictionary<int,int[]> roots, PathTree pathTree, int node)
		{
			int obj = pathTree.GetNodeObject (node);
			path.Add (obj);
			
			bool hasRef = false;
			foreach (int cnode in pathTree.GetChildNodes (node)) {
				FindTreeRoot (path, roots, pathTree, cnode);
				hasRef = true;
			}
			
			if (!hasRef) {
				// A root
				RegisterPath (roots, path, obj);
			}
			
			path.RemoveAt (path.Count - 1);
		}
		
		void RegisterPath (Dictionary<int,int[]> roots, List<int> path, int obj)
		{
			if (!roots.ContainsKey (obj)) {
				roots [obj] = path.ToArray ();
			} else {
				// Keep the shortest path to the root
				int[] ep = roots [obj];
				if (ep.Length > path.Count)
					roots [obj] = path.ToArray ();
			}
		}
		
		public int GetTypeCount ()
		{
			return types.Length;
		}
		
		public int GetTypeFromName (string name)
		{
			for (int n=0; n<types.Length; n++) {
				if (name == types [n].Name)
					return n;
			}
			return -1;
		}
		
		public IEnumerable<int> GetObjectsByType (int type)
		{
			for (int n=0; n < objects.Length; n++) {
				if (objects [n].Type == type && (filteredObjects == null || !filteredObjects[n])) {
					yield return n;
				}
			}
		}
		
		public static HeapSnapshot GetDiff (HeapSnapshot oldMap, HeapSnapshot newMap)
		{
			HeapSnapshot dif = new HeapSnapshot ();
			dif.fieldNames = newMap.fieldNames;
			dif.fieldReferences = newMap.fieldReferences;
			dif.inverseRefs = newMap.inverseRefs;
			dif.objects = newMap.objects;
			dif.objectCodes = newMap.objectCodes;
			dif.references = newMap.references;
			dif.totalMemory = newMap.totalMemory;
			dif.typeIndices = newMap.typeIndices;
			dif.types = newMap.types;
			dif.RemoveData (oldMap);
			dif.name = string.Format ("Diff from {0} to {1}", oldMap.Name, newMap.Name);
			return dif;
		}
		
		public void RemoveData (HeapSnapshot otherShot)
		{
			types = (TypeInfo[]) types.Clone ();
			filteredObjects = new bool [objects.Length];
			for (int n=0; n<otherShot.objects.Length; n++) {
				int i = Array.BinarySearch (objectCodes, otherShot.objects[n].Code);
				// FIXME: we don't keep track of objects that have moved,
				// so those are treated as different objects right now.
				if (i >= 0) {
					filteredObjects [i] = true;
					long t = objects[i].Type;
					types [t].ObjectCount--;
					//types [t].TotalSize -= objects[i].Size;
					filteredCount++;
					//this.totalMemory -= objects[i].Size;
				}
			}
		}
		
		public IEnumerable<int> GetReferencers (int obj)
		{
			int n = objects [obj].InverseRefsIndex;
			int end = n + objects [obj].InverseRefsCount;
			for (; n<end; n++) {
				int ro = inverseRefs [n];
				if (filteredObjects == null || !filteredObjects [ro])
					yield return ro;
			}
		}
		
		public IEnumerable<int> GetReferences (int obj)
		{
			int n = objects [obj].RefsIndex;
			int end = n + objects [obj].RefsCount;
			for (; n<end; n++) {
				int ro = references [n];
				if (filteredObjects == null || !filteredObjects [ro])
					yield return ro;
			}
		}
		
		public string GetReferencerField (int obj, int refObj)
		{
			// The log profiler doesn't support referencer fields, so
			// this just produces useless (even senseless) data in
			// the output.
			return null;
/*
			int n = objects [obj].RefsIndex;
			int end = n + objects [obj].RefsCount;
			for (; n<end; n++) {
				if (references [n] == refObj) {
					if (fieldReferences [n] != 0)
						return fieldNames [fieldReferences [n]];
					else
						return "<Unknown>";
				}
			}
			return "<Unknown>";
*/
		}
		
		public string GetObjectTypeName (int obj)
		{
			return types [objects [obj].Type].Name;
		}
		
		public int GetObjectType (int obj)
		{
			return objects [obj].Type;
		}
		
		public ulong GetObjectSize (int obj)
		{
			return objects [obj].Size;
		}
		
		public IEnumerable<int> GetTypes ()
		{
			for (int n=0; n<types.Length; n++)
				yield return n;
		}
		
		public string GetTypeName (int type)
		{
			return types [type].Name;
		}
		
		public long GetObjectCountForType (int type)
		{
			return types [type].ObjectCount;
		}
		
		public ulong GetObjectSizeForType (int type)
		{
			return types [type].TotalSize;
		}
		
		public bool IsStaticObject (int obj)
		{
			return objects [obj].Code == types [objects [obj].Type].Code;
		}
	}
	
	public class PathTree
	{
		List<int> pathTree = new List<int> ();
		List<int> roots = new List<int> ();
		Dictionary<int,int> pathIndex = new Dictionary<int,int> ();
		HeapSnapshot map;
		
		internal PathTree (HeapSnapshot map)
		{
			this.map = map;
		}

		public IEnumerable<int> GetRootNodes ()
		{
			return roots;
		}
		
		public IEnumerable<int> GetChildNodes (int node)
		{
			int cpos = pathTree [node + 1];
			while (cpos != -1) {
				yield return pathTree [cpos];
				cpos = pathTree [cpos + 1];
			}
		}
		
		public int GetNodeObject (int node)
		{
			return pathTree [node];
		}
		
		internal int GetObjectNode (int obj)
		{
			int res;
			if (pathIndex.TryGetValue (obj, out res))
				return res;
			else
				return -1;
		}
		
		internal void AddBaseObject (int obj)
		{
			int pos = AddObject (obj);
			roots.Add (pos);
			pathIndex [obj] = pos;
		}
		
		internal int AddObject (int obj)
		{
			int pos = pathTree.Count;
			pathTree.Add (obj);
			pathTree.Add (-1);
			return pos;
		}
		
		internal void Flush ()
		{
			pathIndex = null;
		}
		
		internal void AddPath (int[] cpath)
		{
			int tpos = pathIndex [cpath [0]];
			
			for (int n=1; n<cpath.Length; n++) {
				// Fill gaps in the tree
				int cobj = cpath[n];
				int lastcpos = tpos;
				int cpos = pathTree [tpos + 1];
				while (cpos != -1) {
					if (pathTree [pathTree [cpos]] == cobj)
						break;
					lastcpos = cpos;
					cpos = pathTree [cpos + 1];
				}
				if (cpos != -1) {
					// Child already exist
					tpos = pathTree [cpos];
				} else {
					// New child
					int newObjPos;
					if (pathIndex.TryGetValue (cobj, out newObjPos)) {
						// The object is already in the tree.
						// We only need to register the child node.
						pathTree.Add (newObjPos);
						pathTree.Add (-1);
						tpos = newObjPos;
					} else {
						// The object is new in the tree. Register the object.
						tpos = pathTree.Count;
						pathIndex.Add (cobj, tpos);
						pathTree.Add (cobj);
						pathTree.Add (-1);
						// Now register the child node
						pathTree.Add (tpos);
						pathTree.Add (-1);
					}
					// Link the new child node
					pathTree [lastcpos + 1] = pathTree.Count - 2;
				}
			}
		}
		
		public Graph CreateGraph ()
		{
			Graph gr = new Graph (map);
			Dictionary<int,int> visited = new Dictionary<int,int> ();
			foreach (int node in roots) {
				gr.ResetRootReferenceTracking ();
				FillGraph (gr, visited, node);
			}
			gr.Flush ();
			return gr;
		}
		
		void FillGraph (Graph gr, Dictionary<int,int> visited, int node)
		{
			if (visited.ContainsKey (node))
				return;
			visited [node] = node;
			int obj = GetNodeObject (node);
			gr.AddObject (obj);
			foreach (int cn in GetChildNodes (node)) {
				int tobj = GetNodeObject (cn);
				gr.AddObject (tobj);
				gr.AddReference (obj, tobj, 1);
				FillGraph (gr, visited, cn);
			}
			visited.Remove (node);
		}
		
		internal void Dump ()
		{
			Dictionary<int,int> dict = new Dictionary<int,int> ();
			foreach (int n in roots) {
				Dump (0, n, dict);
			}
		}
		
		internal void Dump (int ind, int n, Dictionary<int,int> dict)
		{
			Console.WriteLine (new string (' ', ind*2) + pathTree [n]);
			foreach (int cn in GetChildNodes (n)) {
				Dump (ind+1, cn, dict);
			}
		}
	}
}
