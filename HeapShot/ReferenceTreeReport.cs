//
// TypesReport.cs
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
using HeapShot.Reader;

namespace HeapShot {

	public class ReferenceTreeReport {

		public void Run (string [] args)
		{
			// Parameters are: [-s map-file-to-compare] [-i] [-r] object map file, type to check, tree deepness
			
			int maxlevels = 5;
			string type = null;
			bool inverse = false;
			bool roots = false;
			
			if (args.Length == 0) {
				Console.WriteLine ("Map file name missing.");
				return;
			}
			
			ObjectMapReader omap = new ObjectMapReader (args [0]);
			
			int p = 1;
			
			while (p < args.Length) {
				if (args[p].Length > 0 && args[p][0] == '-') {
					switch (args [p]) {
						case "-s":
							p++;
							if (p >= args.Length) {
								Console.WriteLine ("Map file name missing.");
								return;
							}
							ObjectMapReader oldmap = new ObjectMapReader (args[p]);
							omap.RemoveData (oldmap);
							break;
							
						case "-i":
							inverse = true;
							break;
							
						case "-r":
							roots = true;
							break;
					}
					p++;
				} else {
					break;
				}
			}
			
			if (p < args.Length) {
				type = args [p];
			}
			
			p++;
			if (p < args.Length) {
				maxlevels = int.Parse (args [p]);
			}
			
			if (type != null) {
				if (roots) {
					PrintRoots (omap, type, maxlevels);
				} else {
					// Show the tree for a type
					ReferenceNode nod = new ReferenceNode (type, inverse);
					foreach (ObjectInfo obj in omap.GetObjectsByType (type)) {
						nod.AddReference (obj);
					}
					nod.Print (maxlevels);
				}
			} else {
				// Show a summary
				int tot = 0;
				foreach (TypeInfo t in omap.GetTypes ()) {
					Console.WriteLine ("{0} {1} {2}", t.Objects.Count, t.TotalSize, t.Name);
					tot += t.Objects.Count;
				}
				Console.WriteLine ();
				Console.WriteLine ("Total: " + tot);
			}
		}
		
		void PrintRoots (ObjectMapReader omap, string typeName, int maxlevels)
		{
			ArrayList path = new ArrayList ();
			Hashtable roots = new Hashtable ();
			Hashtable visited = new Hashtable ();
			
			foreach (ObjectInfo obj in omap.GetObjectsByType (typeName)) {
				FindRoot (visited, path, roots, obj);
				visited.Clear ();
			}
			
			foreach (ArrayList ep in roots.Values) {
				for (int n=0; n < ep.Count && n < maxlevels; n++) {
					ObjectInfo ob  = (ObjectInfo) ep [n];
					Console.WriteLine (n + ". " + ob.Type.Name);
				}
				if (maxlevels < ep.Count)
					Console.WriteLine ("...");
				Console.WriteLine ();
				Console.WriteLine ("-");
				Console.WriteLine ();
			}
		}
		
		void FindRoot (Hashtable visited, ArrayList path, Hashtable roots, ObjectInfo obj)
		{
			if (visited.Contains (obj))
				return;
			visited [obj] = obj;
			path.Add (obj);
			if (obj.Referencers != null && obj.Referencers.Count > 0) {
				foreach (ObjectInfo oref in obj.Referencers) {
					FindRoot (visited, path, roots, oref);
				}
			} else {
				// A root
				ArrayList ep = (ArrayList) roots [obj];
				if (ep == null) {
					roots [obj] = path.Clone ();
				} else {
					if (ep.Count > path.Count)
						roots [obj] = path.Clone ();
				}
				Console.WriteLine ("found root" + roots.Count + " " + path.Count + " " + obj.Type.Name);
				foreach (ObjectInfo o in path) {
					Console.Write (o.Type.Name + " / ");
				}
				Console.WriteLine ();
			}
			path.RemoveAt (path.Count - 1);
		}
	}
	
}
