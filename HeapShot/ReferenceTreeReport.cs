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
using System.Collections.Generic;
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
				Console.Error.WriteLine ("Usage is: heap-shot MAP_FILE [-s map-file-to-compare] -i -r [Type [MaxLevels]].");
				Console.Error.WriteLine ("    -s MAP_FILE    The source map file to compare against");
				Console.Error.WriteLine ("    -i             Invert references");
				Console.Error.WriteLine ("    -r             Print roots");
				
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
				int ntype = omap.GetTypeFromName (type);
				if (ntype == -1) {
					Console.WriteLine ("Type not found: " + type);
					return;
				}
				if (roots) {
					PrintRoots (omap, ntype, maxlevels);
				} else {
					// Show the tree for a type
					ReferenceNode nod = new ReferenceNode (omap, ntype, inverse);
					foreach (int obj in omap.GetObjectsByType (ntype)) {
						nod.AddReference (obj, 0);
					}
					nod.Print (maxlevels);
				}
			} else {
				// Show a summary
				long tot = 0;
				foreach (int t in omap.GetTypes ()) {
					long no = omap.GetObjectCountForType (t);
					Console.WriteLine ("{0} {1} {2}", no, omap.GetObjectSizeForType (t), omap.GetTypeName (t));
					tot += no;
				}
				Console.WriteLine ();
				Console.WriteLine ("Total: " + tot);
			}
		}
		
		void PrintRoots (ObjectMapReader omap, int type, int maxlevels)
		{
			List<int> path = new List<int> ();
			Dictionary<int,List<int>> roots = new Dictionary<int,List<int>> ();
			Dictionary<int,int> visited = new Dictionary<int,int> ();
			
			foreach (int obj in omap.GetObjectsByType (type)) {
				FindRoot (omap, visited, path, roots, obj);
				visited.Clear ();
			}
			
			foreach (List<int> ep in roots.Values) {
				for (int n=0; n < ep.Count && n < maxlevels; n++) {
					int ob  = ep [n];
					Console.WriteLine (n + ". " + omap.GetObjectTypeName (ob));
				}
				if (maxlevels < ep.Count)
					Console.WriteLine ("...");
				Console.WriteLine ();
				Console.WriteLine ("-");
				Console.WriteLine ();
			}
		}
		
		void FindRoot (ObjectMapReader omap, Dictionary<int,int> visited, List<int> path, Dictionary<int,List<int>> roots, int obj)
		{
			if (visited.ContainsKey (obj))
				return;
			visited [obj] = obj;
			path.Add (obj);
			
			bool hasrefs = false;
			foreach (int oref in omap.GetReferencers (obj)) {
				hasrefs = true;
				FindRoot (omap, visited, path, roots, oref);
			}
			
			if (!hasrefs) {
				// A root
				List<int> ep = roots [obj];
				if (ep == null) {
					roots [obj] = new List<int> (path);
				} else {
					if (ep.Count > path.Count)
						roots [obj] = new List<int> (path);
				}
				Console.WriteLine ("found root" + roots.Count + " " + path.Count + " " + omap.GetObjectTypeName (obj));
				foreach (int o in path) {
					Console.Write (omap.GetObjectTypeName (o) + " / ");
				}
				Console.WriteLine ();
			}
			path.RemoveAt (path.Count - 1);
		}
	}
	
}
