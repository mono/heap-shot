
using System;
using Gtk;
using HeapShot.Reader;

namespace HeapShot.Gui.Widgets
{
	public delegate void ProgressEventHandler (int current, int max, string message);
	
	public class ReferenceTreeViewer : Gtk.Bin
	{
		protected Gtk.TreeView treeview;
		protected Gtk.CheckButton checkInverse;
		protected Gtk.Entry entryFilter;
		
		Gtk.TreeStore store;
		const int ReferenceCol = 0;
		const int ImageCol = 1;
		const int TypeCol = 2;
		const int FilledCol = 3;
		const int SizeCol = 4;
		const int AvgSizeCol = 5;
		const int InstancesCol = 6;
		const int RefsCol = 7;
		int TreeColRefs;
		bool reloadRequested;
		bool loading;
		
		ObjectMapReader file;
		string typeName;
		protected Gtk.HBox boxFilter;
		
		public event ProgressEventHandler ProgressEvent;

		public ReferenceTreeViewer()
		{
			Stetic.Gui.Build(this, typeof(HeapShot.Gui.Widgets.ReferenceTreeViewer));
			store = new Gtk.TreeStore (typeof(object), typeof(string), typeof(string), typeof(bool), typeof(string), typeof(string), typeof(string), typeof(string));
			treeview.Model = store;
			treeview.HeadersClickable = true;
			
			Gtk.TreeViewColumn complete_column = new Gtk.TreeViewColumn ();
			complete_column.Title = "Type";

			Gtk.CellRendererPixbuf pix_render = new Gtk.CellRendererPixbuf ();
			complete_column.PackStart (pix_render, false);
			complete_column.AddAttribute (pix_render, "stock-id", ImageCol);

			Gtk.CellRendererText text_render = new Gtk.CellRendererText ();
			complete_column.PackStart (text_render, true);
			
			complete_column.AddAttribute (text_render, "text", TypeCol);
			complete_column.Clickable = true;
	
			treeview.AppendColumn (complete_column);
			CellRendererText crt = new CellRendererText ();
			crt.Xalign = 1;
			treeview.AppendColumn ("Instances", crt, "text", InstancesCol).Clickable = true;
			crt = new CellRendererText ();
			crt.Xalign = 1;
			TreeColRefs = treeview.Columns.Length;
			treeview.AppendColumn ("References", crt, "text", RefsCol).Clickable = true;
			crt = new CellRendererText ();
			crt.Xalign = 1;
			treeview.AppendColumn ("Memory Size", crt, "text", SizeCol).Clickable = true;
			crt = new CellRendererText ();
			crt.Xalign = 1;
			treeview.AppendColumn ("Avg. Size", crt, "text", AvgSizeCol).Clickable = true;
			
			treeview.TestExpandRow += new Gtk.TestExpandRowHandler (OnTestExpandRow);
			treeview.RowActivated += new Gtk.RowActivatedHandler (OnNodeActivated);
			treeview.AppendColumn (new Gtk.TreeViewColumn());
			
			int nc = 0;
			foreach (TreeViewColumn c in treeview.Columns) {
				store.SetSortFunc (nc, CompareNodes);
				c.SortColumnId = nc++;
			}
			store.SetSortColumnId (1, Gtk.SortType.Descending);
		}
		
		public void Clear ()
		{
			entryFilter.Text = "";
			store.Clear ();
		}
		
		public bool InverseReferences {
			get { return checkInverse.Active; }
			set { checkInverse.Active = value; }
		}
		
		public string RootTypeName {
			get { return typeName; }
		}
		
		public string SelectedType {
			get {
				Gtk.TreeModel foo;
				Gtk.TreeIter iter;
				if (!treeview.Selection.GetSelected (out foo, out iter))
					return null;
				ReferenceNode nod = store.GetValue (iter, 0) as ReferenceNode;
				if (nod != null)
					return nod.TypeName;
				else
					return null;
			}
		}
		
		public void FillAllTypes (ObjectMapReader file)
		{
			this.file = file;
			this.typeName = null;
			boxFilter.Visible = true;
			treeview.Columns [TreeColRefs].Visible = InverseReferences;
			
			if (loading) {
				// If the tree is already being loaded, notify that loading
				// has to start again, since the file has changed.
				reloadRequested = true;
				return;
			}

			loading = true;
			store.Clear ();
			int n=0;
			foreach (int t in file.GetTypes ()) {
				if (++n == 20) {
					if (ProgressEvent != null) {
						ProgressEvent (n, file.GetTypeCount (), null);
					}
					while (Gtk.Application.EventsPending ())
						Gtk.Application.RunIteration ();
					if (reloadRequested) {
						loading = false;
						reloadRequested = false;
						FillAllTypes (this.file);
						return;
					}
					n = 0;
				}
				if (file.GetObjectCountForType (t) > 0)
					InternalFillType (file, t);
			}
			loading = false;
		}
		
		public void FillType (ObjectMapReader file, string typeName)
		{
			this.typeName = typeName;
			this.file = file;
			store.Clear ();
			boxFilter.Visible = false;
			treeview.Columns [TreeColRefs].Visible = InverseReferences;
			TreeIter iter = InternalFillType (file, file.GetTypeFromName (typeName));
			treeview.ExpandRow (store.GetPath (iter), false);
		}
		
		TreeIter InternalFillType (ObjectMapReader file, int type)
		{
			ReferenceNode node = file.GetReferenceTree (type, checkInverse.Active);
			return AddNode (TreeIter.Zero, node);
		}
		
		void Refill ()
		{
			if (typeName != null)
				FillType (file, typeName);
			else
				FillAllTypes (file);
		}
		
		TreeIter AddNode (TreeIter parent, ReferenceNode node)
		{
			if (entryFilter.Text.Length > 0 && node.TypeName.IndexOf (entryFilter.Text) == -1)
				return TreeIter.Zero;
			
			TreeIter iter;
			if (parent.Equals (TreeIter.Zero)) {
				iter = store.AppendValues (node, "class", node.TypeName, !node.HasReferences, node.TotalMemory.ToString("n0"), node.AverageSize.ToString("n0"), node.RefCount.ToString ("n0"), "");
			} else {
				string refs = (InverseReferences ? node.RefsToParent.ToString ("n0") : "");
				iter = store.AppendValues (parent, node, "class", node.TypeName, !node.HasReferences, node.TotalMemory.ToString("n0"), node.AverageSize.ToString("n0"), node.RefCount.ToString ("n0"), refs);
			}

			if (node.HasReferences) {
				// Add a dummy element to make the expansion icon visible
				store.AppendValues (iter, null, "", "", true, "", "", "", "");
			}
			return iter;
		}

		TreeIter AddNode (TreeIter parent, FieldReference node)
		{
			if (parent.Equals (TreeIter.Zero))
				return store.AppendValues (node, "field", node.FiledName, true, "", "", node.RefCount.ToString ("n0"), "");
			else
				return store.AppendValues (parent, node, "field", node.FiledName, true, "", "", node.RefCount.ToString ("n0"), "");
		}

		private void OnTestExpandRow (object sender, Gtk.TestExpandRowArgs args)
		{
			bool filled = (bool) store.GetValue (args.Iter, FilledCol);
			ReferenceNode parent = (ReferenceNode) store.GetValue (args.Iter, ReferenceCol);
			if (!filled) {
				store.SetValue (args.Iter, FilledCol, true);
				TreeIter iter;
				store.IterChildren (out iter, args.Iter);
				store.Remove (ref iter);
				if (parent.References.Count > 0 || parent.FieldReferences.Count > 0) {
					int nr = 0;
					foreach (ReferenceNode nod in parent.References)
						if (!AddNode (args.Iter, nod).Equals (TreeIter.Zero))
							nr++;
					foreach (FieldReference fref in parent.FieldReferences)
						if (!AddNode (args.Iter, fref).Equals (TreeIter.Zero))
							nr++;
					if (nr == 0)
						args.RetVal = true;
				} else
					args.RetVal = true;
			}
		}

		protected virtual void OnNodeActivated (object sender, Gtk.RowActivatedArgs args)
		{
			if (TypeActivated != null && SelectedType != null)
				TypeActivated (this, EventArgs.Empty);
		}
		
		protected virtual void OnCheckInverseClicked(object sender, System.EventArgs e)
		{
			Refill ();
		}

		protected virtual void OnButtonFilterClicked(object sender, System.EventArgs e)
		{
			Refill ();
		}

		protected virtual void OnEntryFilterActivated(object sender, System.EventArgs e)
		{
			Refill ();
		}
		
		int CompareNodes (Gtk.TreeModel model, Gtk.TreeIter a, Gtk.TreeIter b)
		{
			int col;
			SortType type;
			store.GetSortColumnId (out col, out type);
			
			object o1 = model.GetValue (a, ReferenceCol);
			object o2 = model.GetValue (b, ReferenceCol);
			
			if (o1 is ReferenceNode && o2 is ReferenceNode) {
				ReferenceNode nod1 = (ReferenceNode) o1;
				ReferenceNode nod2 = (ReferenceNode) o2;
				switch (col) {
					case 0:
						return string.Compare (nod1.TypeName, nod2.TypeName);
					case 1:
						return nod1.RefCount.CompareTo (nod2.RefCount);
					case 2:
						return nod1.RefsToParent.CompareTo (nod2.RefsToParent);
					case 3:
						return nod1.TotalMemory.CompareTo (nod2.TotalMemory);
					case 4:
						return nod1.AverageSize.CompareTo (nod2.AverageSize);
					default:
						Console.WriteLine ("PPP: " + col);
						return 1;
	//					throw new InvalidOperationException ();
				}
			} else if (o1 is FieldReference && o2 is FieldReference) {
				return ((FieldReference)o1).FiledName.CompareTo (((FieldReference)o2).FiledName);
			} else if (o1 is FieldReference) {
				return -1;
			} else {
				return 1;
			}
		}
		
		public event EventHandler TypeActivated;
	}
}
