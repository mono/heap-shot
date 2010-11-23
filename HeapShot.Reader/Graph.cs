// /home/lluis/work/heap-shot/HeapShot.Reader/Graph.cs created with MonoDevelop
// User: lluis at 00:57 08/06/2007
//

using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;

namespace HeapShot.Reader.Graphs
{
	public class Graph
	{
		Dictionary<string, Node> nodes = new Dictionary<string,Node> ();
		Dictionary<int,int> added = new Dictionary<int,int> ();
		Hashtable visitedItems = new Hashtable ();
		
		HeapSnapshot map;
		
		public Graph (HeapSnapshot map)
		{
			this.map = map;
		}
		
		public void ResetRootReferenceTracking ()
		{
			visitedItems.Clear ();
		}
		
		internal bool RootRefItemVisited (object item)
		{
			if (visitedItems.ContainsKey (item))
				return true;
			visitedItems [item] = item;
			return false;
		}
		
		internal void Flush ()
		{
			foreach (Node nod in nodes.Values)
				nod.UpdateInfo ();
			foreach (Node nod in nodes.Values)
				nod.Flush ();
			added = null;
		}
		
		public void AddObject (int obj)
		{
			if (added.ContainsKey (obj))
				return;

			added [obj] = obj;
			
			string name = map.GetObjectTypeName (obj);
			Node nod;
			if (!nodes.TryGetValue (name, out nod)) {
				nod = new Node (this);
				nod.Name = name;
				nodes [name] = nod;
			}
			nod.Count++;
		}
		
		public void AddReference (int sobj, int tobj, int rref)
		{
			string name = map.GetObjectTypeName (sobj);
			Node snod = nodes [name];
			name = map.GetObjectTypeName (tobj);
			Node tnod = nodes [name];
			
			Edge ed = null;
			
			foreach (Edge e in snod.OutEdges) {
				if (e.Target == tnod) {
					ed = e;
					break;
				}
			}
			if (ed == null) {
				ed = new Edge (this);
				ed.Source = snod;
				ed.Target = tnod;
				snod.OutEdges.Add (ed);
				tnod.InEdges.Add (ed);
			}
			ed.AddReference (sobj, tobj, rref);
		}
		
		public void WriteDot (string fileName)
		{
			using (StreamWriter sw = new StreamWriter (fileName)) {
				WriteDot (sw);
			}
		}
		
		public void WriteDot (TextWriter tw)
		{
			tw.WriteLine ("digraph ClassGraph {");
			tw.WriteLine ("\tnode [shape=box];");
			foreach (Node nod in nodes.Values) {
				nod.WriteDot (tw);
			}
			tw.WriteLine ("}");
		}
	}
	
	public class Node
	{
		List<Edge> inEdges = new List<Edge> ();
		List<Edge> outEdges = new List<Edge> ();
		int count;
		string name;
		string label;
		internal int rootRefCount;
		
		internal Node (Graph graph)
		{
		}
		
		internal void UpdateInfo ()
		{
			label = name + "\\n(c:" + count + " rr:" + rootRefCount + ")";
		}
		
		internal void Flush ()
		{
			foreach (Edge e in outEdges) {
				e.Flush ();
			}
		}
		
		public List<Edge> InEdges {
			get {
				return inEdges;
			}
		}

		public List<Edge> OutEdges {
			get {
				return outEdges;
			}
		}

		public int Count {
			get {
				return count;
			}
			set {
				count = value;
			}
		}

		public string Name {
			get {
				return name;
			}
			set {
				name = value;
			}
		}
		
		public string Label {
			get { return label; }
		}
		
		public void WriteDot (TextWriter tw)
		{
			bool found = false;
			string style = "";
			
			foreach (Edge e in InEdges) {
				if (e.Source != this) {
					found = true;
					break;
				}
			}
			
			if (found) {
				found = false;
				foreach (Edge e in OutEdges) {
					if (e.Target != this) {
						found = true;
						break;
					}
				}
				if (!found)
					style = ",style=\"filled\",color=\"lightgrey\"";
			}
			else
				style = ",style=\"filled\",color=\"lightblue\"";
			
			tw.WriteLine ("\t\"{0}\" [label=\"{1}\"{2}]", name, Label, style);
			foreach (Edge e in OutEdges) {
				e.WriteDot (tw);
			}
		}
	}
	
	public class Edge
	{
		Node source;
		Node target;
		int sourceCount;
		int targetCount;
		Dictionary<int,int> sourceNodes = new Dictionary<int,int> ();
		Dictionary<int,int> targetNodes = new Dictionary<int,int> ();
		Graph graph;
		int rootRefCount;
		
		internal Edge (Graph graph)
		{
			this.graph = graph;
		}
		
		internal void Flush ()
		{
			sourceNodes = null;
			targetNodes = null;
		}
		
		public void WriteDot (TextWriter tw)
		{
			tw.Write ("\t\"{0}\" -> \"{1}\"", Target.Name, Source.Name);
			tw.WriteLine (" [headlabel=\"{0}\",taillabel=\"{1}\",label=\"rr:{2}\"]", SourceCount, TargetCount, rootRefCount);
		}
		
		public Node Source {
			get { return source; }
			set { source = value; }
		}
		
		public Node Target {
			get { return target; }
			set { target = value; }
		}

		public void AddReference (int sobj, int tobj, int rref)
		{
			int rcount;
			if (!sourceNodes.TryGetValue (sobj, out rcount)) {
				sourceNodes [sobj] = rref;
				sourceCount++;
			}
			if (!targetNodes.TryGetValue (tobj, out rcount)) {
				targetNodes [tobj] = 0;
				targetCount++;
			}
			if (!graph.RootRefItemVisited (this))
				rootRefCount++;
			if (!graph.RootRefItemVisited (target))
				target.rootRefCount++;
		}
		
		public int GetTargetRefCount (int obj)
		{
			int rref;
			if (targetNodes.TryGetValue (obj, out rref))
				return rref;
			else
				return 0;
		}
		
		public int GetTotalTargetRefCount ()
		{
			int res = 0;
			foreach (int r in targetNodes.Values)
				res += r;
			return res;
		}

		public int SourceCount {
			get {
				return sourceCount;
			}
		}

		public int TargetCount {
			get {
				return targetCount;
			}
		}
	}
}
