// /home/lluis/work/heap-shot/HeapShot.Gui.Widgets/ProgressDialog.cs created with MonoDevelop
// User: lluis at 12:35 14/06/2007
//

using System;
using HeapShot.Reader;

namespace HeapShot.Gui.Widgets
{
	
	
	public partial class ProgressDialog : Gtk.Dialog, IProgressListener
	{
		bool cancelled;
		int lastp = -1;
		
		public ProgressDialog (Gtk.Window parent)
		{
			this.Build();
			this.TransientFor = parent;
		}
	
		public void ReportProgress (string message, double progress)
		{
			int newp = (int) (progress * 1000);
			if (lastp == newp)
				return;
			lastp = newp;
			
			label.Text = message;
			this.progress.Fraction = progress;
			while (Gtk.Application.EventsPending ())
				Gtk.Application.RunIteration ();
		}
		
		public bool Cancelled {
			get { return cancelled; } 
		}

		protected virtual void OnButtonCancelClicked (object sender, System.EventArgs e)
		{
			cancelled = true;
			buttonCancel.Sensitive = false;
		}
	}
}
