
using System;
using System.Linq;
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
			store = new ListStore (typeof(string), typeof(string), typeof(int));
			list.Model = store;
			list.AppendColumn ("PID", new Gtk.CellRendererText (), "text", 0);
			list.AppendColumn ("Process", new Gtk.CellRendererText (), "text", 1);

			PerformanceCounterCategory p = new PerformanceCounterCategory (".NET CLR JIT");

			foreach (string proc in p.GetInstanceNames ()) {
				int pos = proc.IndexOf ('/');
				if (pos != -1) {
					string process_id = proc.Substring (0, pos);
					string [] args = GetArgs (Convert.ToInt32 (process_id));
					string prof = args.FirstOrDefault (a => a.StartsWith ("--profile=log"));
					if (prof == null || prof.IndexOf ("heapshot=ondemand") == -1 || prof.IndexOf ("port=") == -1)
						continue;
					int i = prof.IndexOf ("port=") + 5;
					int j = i;
					while (j < prof.Length && char.IsDigit(prof[j]))
						j++;
					int port;
					if (!int.TryParse (prof.Substring (i, j - i), out port))
						continue;
					store.AppendValues (process_id, String.Join (" ", args), port);
				}
			}
		}
		
		public int Port {
			get { return pid; }
		}

		protected virtual void OnResponse(object o, Gtk.ResponseArgs args)
		{
			Gtk.TreeModel foo;
			Gtk.TreeIter iter;
			if (!list.Selection.GetSelected (out foo, out iter))
				return;
			pid = (int) store.GetValue (iter, 2);
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
