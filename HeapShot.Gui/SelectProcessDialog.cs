
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
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
				if (pos != -1) {
					string process_id = proc.Substring (0, pos);
					string [] args = GetArgs (Convert.ToInt32 (process_id));
					if (Array.IndexOf (args, "--profile=heap-shot") == -1)
						continue;
					//store.AppendValues (process_id, proc);
					store.AppendValues (process_id, String.Join (" ", args));
				}
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

		static string [] GetArgs (int pid)
		{
			try {
				string fname = "/proc/" + pid + "/cmdline";
				string content;
				using (StreamReader reader = new StreamReader (fname, Encoding.ASCII)) {
					content = reader.ReadToEnd ();
				}
				return content.Split ('\0');
			} catch {
			}
			return null;
		}
	}
}
