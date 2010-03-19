
using System;
using System.Diagnostics;
using Gtk;

namespace HeapShot.Gui
{
	public partial class SelectProcessDialog : Gtk.Dialog
	{
		ListStore store;
		int pid;

		public SelectProcessDialog()
		{
			Build ();
			store = new ListStore (typeof(string), typeof(string));
			list.Model = store;
			list.AppendColumn ("PID", new Gtk.CellRendererText (), "text", 0);
			list.AppendColumn ("Process", new Gtk.CellRendererText (), "text", 1);

			PerformanceCounterCategory p = new PerformanceCounterCategory (".NET CLR JIT");

			foreach (string proc in p.GetInstanceNames ()) {
				int pos = proc.IndexOf ('/');
				if (pos != -1)
					store.AppendValues (proc.Substring (0, pos), proc);
			}
		}
		
		public int ProcessId {
			get { return pid; }
		}

		protected virtual void OnResponse(object o, Gtk.ResponseArgs args)
		{
			Gtk.TreeModel foo;
			Gtk.TreeIter iter;
			if (!list.Selection.GetSelected (out foo, out iter))
				return;
			string spid = (string) store.GetValue (iter, 0);
			pid = int.Parse (spid);
		}
	}
}
