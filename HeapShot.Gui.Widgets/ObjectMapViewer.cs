
using System;
using System.IO;
using System.Collections;

using Gtk;
using HeapShot.Reader;

namespace HeapShot.Gui.Widgets
{
	public partial class ObjectMapViewer : Gtk.Bin
	{
		ListStore fileStore;
		ObjectMapReader baseMap;
		ArrayList difs = new ArrayList ();
		ObjectMapReader lastMap;
		
		public ObjectMapViewer()
		{
			Build ();
			fileStore = new Gtk.ListStore (typeof(object), typeof(string), typeof(bool));
			fileList.Model = fileStore;
			Gtk.CellRendererToggle ctog = new Gtk.CellRendererToggle ();
			ctog.Toggled += OnToggled;
			fileList.AppendColumn ("Base", ctog, "active", 2);
			fileList.AppendColumn ("File", new Gtk.CellRendererText (), "text", 1);
			fileList.CursorChanged += new EventHandler (OnSelectionChanged);
			allObjectsTree.TypeActivated += OnAllObjectsTreeTypeActivated;
			notebook.Page = 0;
		}
		
		public void Clear ()
		{
			while (notebook.NPages > 2)
				notebook.Remove (notebook.Children [2]);
			
			baseMap = null;
			fileStore.Clear ();
			allObjectsTree.Clear ();
			notebook.Page = 0;
			labelCount.Text = "";
			labelMemory.Text = "";
			labelName.Text = "";
		}
		
		public event ProgressEventHandler ProgressEvent {
			add { allObjectsTree.ProgressEvent += value; }
			remove { allObjectsTree.ProgressEvent -= value; }
		}
		
		public void AddFile (string fileName)
		{
			ObjectMapReader map = new ObjectMapReader (fileName);
			AddSnapshot (map);
		}
		
		public void AddSnapshot (ObjectMapReader map)
		{
			fileStore.AppendValues (map, System.IO.Path.GetFileName (map.Name), false);
		}
		
		protected virtual void OnSelectionChanged (object sender, EventArgs args)
		{
			ObjectMapReader map = GetCurrentObjectMap ();
			if (map == lastMap)
				return;
				
			lastMap = map;
			
			if (map != null) {
				allObjectsTree.FillAllTypes (map);
				
				if (baseMap != null && map != baseMap) {
					labelName.Text = System.IO.Path.GetFileName (map.Name) + " - " + System.IO.Path.GetFileName (baseMap.Name);
				}
				else
					labelName.Text = System.IO.Path.GetFileName (map.Name);
					
				labelCount.Text = map.NumObjects.ToString ("n0");
				labelMemory.Text = map.TotalMemory.ToString ("n0") + " bytes";
			}
		}
		
		void OnToggled (object s, ToggledArgs args)
		{
			Gtk.TreeIter iter;
			if (!fileStore.GetIterFromString (out iter, args.Path))
				return;

			if (!(bool) fileStore.GetValue (iter, 2))
				baseMap = GetCurrentObjectMap ();
			else
				baseMap = null;
			UpdateBase ();
		}
		
		void UpdateBase ()
		{
			TreeIter iter;
			if (fileStore.GetIterFirst (out iter)) {
				do {
					ObjectMapReader map = (ObjectMapReader) fileStore.GetValue (iter, 0);
					if (map == baseMap)
						fileStore.SetValue (iter, 2, true);
					else
						fileStore.SetValue (iter, 2, false);
				}
				while (fileStore.IterNext (ref iter));
			}
		}
		
		ObjectMapReader GetCurrentObjectMap ()
		{
			Gtk.TreeModel foo;
			Gtk.TreeIter iter;
			if (!fileList.Selection.GetSelected (out foo, out iter))
				return null;
			
			ObjectMapReader map = (ObjectMapReader) fileStore.GetValue (iter, 0);
			if (baseMap != null && baseMap != map)
				return GetCombinedMap (map, baseMap);
			else
				return map;
		}

		protected virtual void OnAllObjectsTreeTypeActivated(object sender, System.EventArgs e)
		{
			ShowTypeTree (allObjectsTree.SelectedType, allObjectsTree.InverseReferences);
		}
		
		void ShowTypeTree (string typeName, bool inverse)
		{
			foreach (object child in notebook.Children) {
				ReferenceTreeViewer tree = child as ReferenceTreeViewer;
				if (tree != null && tree.RootTypeName == typeName) {
					tree.InverseReferences = inverse;
					notebook.Page = notebook.PageNum (tree);
					return;
				}
			}
			
			ReferenceTreeViewer viewer = new ReferenceTreeViewer ();
			viewer.FillType (GetCurrentObjectMap (), typeName);
			viewer.Show ();
			viewer.TypeActivated += delegate {
				ShowTypeTree (viewer.SelectedType, viewer.InverseReferences);
			};
			HBox label = new HBox ();
			label.Spacing = 3;
			label.PackStart (new Gtk.Image ("class", Gtk.IconSize.Menu), false, false, 0);
			label.PackStart (new Gtk.Label (typeName), true, true, 0);
			Button but = new Button (new Gtk.Image (Gtk.Stock.Close, Gtk.IconSize.Menu));
			but.Relief = ReliefStyle.None;
			but.SetSizeRequest (18, 18);
			label.PackStart (but, false, false, 0);
			but.Clicked += delegate {
				notebook.Remove (viewer);
				viewer.Destroy ();
			};
			label.ShowAll ();
			int i = notebook.AppendPage (viewer, label);
			notebook.Page = i;
		}
		
		ObjectMapReader GetCombinedMap (ObjectMapReader m1, ObjectMapReader m2)
		{
			if (m2.Timestamp < m1.Timestamp) {
				ObjectMapReader tmp = m1;
				m1 = m2;
				m2 = tmp;
			}
			
			foreach (ObjectMapReader[] dif in difs) {
				if (dif[0] == m1 && dif[1] == m2)
					return dif[2];
			}
			
			ObjectMapReader res = ObjectMapReader.GetDiff (m1, m2);
			difs.Add (new ObjectMapReader[] { m1, m2, res });
			return res;
		}
	}
}
