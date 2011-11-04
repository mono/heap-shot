// project created on 10/24/2006 at 11:12 AM
using System;
using System.Collections.Generic;

using Gtk;
using MonoDevelop.MacInterop;
using Mono.Options;

namespace HeapShot.Gui
{
	class MainClass
	{
		public static void Main (string[] args)
		{
			MainWindow win = null;
			List<string> files = new List<string> ();
			bool only_view = false;
			bool continuous_reload = false;
			
			OptionSet options = new OptionSet ()
			{
				{ "viewonly", v => only_view = true },
				{ "continuousreload", v => continuous_reload = true },
			};
			
			foreach (string file in options.Parse (args)) {
				if (file.StartsWith ("-psn"))
					continue;
				files.Add (file);
			}
			
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
			win = new MainWindow (files, only_view, continuous_reload);
			win.Show ();
			Application.Run ();
		}
	}
}