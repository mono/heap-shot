using System;
using Gtk;
using HeapShot.Gui;
using HeapShot.Reader;

public partial class MainWindow: Gtk.Window
{
	public int processId = -1;
	string lastFolder;
	
	public MainWindow (): base ("")
	{
		Build ();
		viewer.Sensitive = false;
	}
	
	protected void OnDeleteEvent (object sender, DeleteEventArgs a)
	{
		Application.Quit ();
		a.RetVal = true;
	}

	protected virtual void OnOpenActivated(object sender, System.EventArgs e)
	{
		FileChooserDialog dialog =
			new FileChooserDialog ("Open Object Map File", null, FileChooserAction.Open,
					       Gtk.Stock.Cancel, Gtk.ResponseType.Cancel,
					       Gtk.Stock.Open, Gtk.ResponseType.Ok);
					       
		if (lastFolder != null)
			dialog.SetCurrentFolder (lastFolder);
			
		int response = dialog.Run ();
		try {
			if (response == (int)Gtk.ResponseType.Ok) {
				lastFolder = dialog.CurrentFolder;
				viewer.AddFile (dialog.Filename);
				viewer.Sensitive = true;
			}
		} finally {
			dialog.Destroy ();
		}
	}

	protected virtual void OnQuitActivated(object sender, System.EventArgs e)
	{
		Application.Quit ();
	}

	protected virtual void OnMemorySnapshotActivated(object sender, System.EventArgs e)
	{
		if (processId == -1) {
			SelectProcessDialog dlg = new SelectProcessDialog ();
			try {
				if (dlg.Run () == (int) Gtk.ResponseType.Ok) {
					processId = dlg.ProcessId;
				} else
					return;
			} finally {
				dlg.Destroy ();
			}
		}
		ObjectMapReader map = ObjectMapReader.CreateProcessSnapshot (processId);
		viewer.AddSnapshot (map);
		viewer.Sensitive = true;
	}

	protected virtual void OnNewActivated(object sender, System.EventArgs e)
	{
		viewer.Clear ();
		viewer.Sensitive = false;
		processId = -1;
	}
}

