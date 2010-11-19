// project created on 10/24/2006 at 11:12 AM
using System;
using Gtk;

namespace HeapShot.Gui
{
	class MainClass
	{
		public static void Main (string[] args)
		{
			Application.Init ();
			MainWindow win = new MainWindow (args);
			win.Show ();
			Application.Run ();
		}
	}
}