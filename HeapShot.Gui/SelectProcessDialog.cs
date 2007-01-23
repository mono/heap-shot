
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
			
			foreach (Process proc in Process.GetProcesses ()) {
				store.AppendValues (proc.Id.ToString(), proc.ProcessName);
			}
		}
		
		public int ProcessId {
			get { return pid; }
		}

		public override void Dispose ()
		{
			base.Dispose ();
			Destroy ();
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
