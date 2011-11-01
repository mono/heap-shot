// project created on 10/24/2006 at 11:12 AM
using System;
using Gtk;
using MonoDevelop.MacInterop;

namespace HeapShot.Gui
{
	class MainClass
	{
		public static void Main (string[] args)
		{
			MainWindow win = null;
			
			if (PlatformDetection.IsMac) {
				ApplicationEvents.Quit += delegate (object sender, ApplicationQuitEventArgs e) {
					Application.Quit ();
					e.Handled = true;
				};
			 
				ApplicationEvents.Reopen += delegate (object sender, ApplicationEventArgs e) {
					win.Deiconify ();
					win.Visible = true;
					e.Handled = true;
				};
			 
				ApplicationEvents.OpenDocuments += delegate (object sender, ApplicationDocumentEventArgs e) {
					if (e.Documents != null || e.Documents.Count > 0) {
						win.OpenFiles (e.Documents);
					}
					e.Handled = true;
				};
			}
			
			Application.Init ();
			win = new MainWindow (args);
			win.Show ();
			Application.Run ();
		}
	}
}