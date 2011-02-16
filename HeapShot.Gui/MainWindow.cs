using System;
using Gtk;
using HeapShot.Gui;
using HeapShot.Reader;
using System.Diagnostics;

public partial class MainWindow: Gtk.Window
{
	string lastFolder;
	string outfile;
	Process profProcess;
	ObjectMapReader mapReader;
	
	public MainWindow (string[] args): base ("")
	{
		Build ();
		viewer.Sensitive = false;
		if (args.Length > 0)
			OpenFile (args [0]);
		
		stopAction.Sensitive = false;
		executeAction.Sensitive = true;
		ForceHeapSnapshotAction.Sensitive = false;
	}
	
	protected override void OnDestroyed ()
	{
		ResetFile ();
		if (profProcess != null) {
			try {
				profProcess.Kill ();
			} catch {}
		}
		base.OnDestroyed ();
	}
	
	protected void OnDeleteEvent (object sender, DeleteEventArgs a)
	{
		Application.Quit ();
		a.RetVal = true;
	}
	
	void ResetFile ()
	{
		if (mapReader != null) {
			mapReader.Dispose ();
			mapReader = null;
		}
		if (outfile != null) {
			try {
				System.IO.File.Delete (outfile);
			} catch {}
			outfile = null;
		}
		viewer.Clear ();
		viewer.Sensitive = false;
	}
	
	void OpenFile (string file)
	{
		mapReader = new ObjectMapReader (file);
		mapReader.HeapSnapshotAdded += delegate (object o, HeapShotEventArgs args) {
			Application.Invoke (delegate {
				viewer.AddSnapshot (args.HeapSnapshot);
			});
		};
		viewer.Sensitive = true;
		mapReader.Read ();
	}
	
	void ProfileApplication (string file)
	{
		string mono = typeof(int).Assembly.Location;
		for (int n=0; n<4; n++) mono = System.IO.Path.GetDirectoryName (mono);
		mono = System.IO.Path.Combine (mono, "bin","mono");
		ResetFile ();
		outfile = System.IO.Path.GetTempFileName ();
		profProcess = new Process ();
		profProcess.StartInfo.FileName = mono;
		profProcess.StartInfo.Arguments = "--gc=sgen --profile=log:heapshot=ondemand,nocalls,output=-" + outfile + " \"" + file + "\"";
		profProcess.StartInfo.UseShellExecute = false;
		profProcess.EnableRaisingEvents = true;
		profProcess.Exited += delegate {
			Application.Invoke (ProcessExited);
		};
		try {
			profProcess.Start ();
			stopAction.Sensitive = true;
			executeAction.Sensitive = false;
			ForceHeapSnapshotAction.Sensitive = true;
		} catch (Exception ex) {
			Console.WriteLine (ex);
			profProcess = null;
		}
		OpenFile (outfile);
	}
	
	void ProcessExited (object o, EventArgs a)
	{
		profProcess = null;
		stopAction.Sensitive = false;
		executeAction.Sensitive = true;
		ForceHeapSnapshotAction.Sensitive = false;
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
				ResetFile ();
				OpenFile (dialog.Filename);
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
		mapReader.ForceSnapshot ();
		mapReader.WaitForHeapShot (4000);
	}

	protected virtual void OnExecuteActionActivated (object sender, System.EventArgs e)
	{
		FileChooserDialog dialog =
			new FileChooserDialog ("Profile Application", null, FileChooserAction.Open,
					       Gtk.Stock.Cancel, Gtk.ResponseType.Cancel,
					       Gtk.Stock.Open, Gtk.ResponseType.Ok);
					       
		if (lastFolder != null)
			dialog.SetCurrentFolder (lastFolder);
			
		int response = dialog.Run ();
		try {
			if (response == (int)Gtk.ResponseType.Ok) {
				lastFolder = dialog.CurrentFolder;
				ProfileApplication (dialog.Filename);
			}
		} finally {
			dialog.Destroy ();
		}
	}
	
	protected virtual void OnStopActionActivated (object sender, System.EventArgs e)
	{
		if (profProcess != null) {
			try {
				profProcess.Kill ();
			} catch {
			}
		}
	}
}

