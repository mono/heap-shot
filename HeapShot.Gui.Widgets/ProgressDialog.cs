// 
// ProgressDialog.cs
// 
// Copyright (C) 2010-2011 Novell, Inc. (http://www.novell.com)
// Copyright (C) 2011 Xamarin Inc. (http://www.xamarin.com) 
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
using System;
using Gtk;
using HeapShot.Reader;

namespace HeapShot.Gui.Widgets
{
	
	
	public partial class ProgressDialog : Gtk.Dialog, IProgressListener
	{
		bool cancelled;
		int lastp = -1;
		bool threaded;
		
		public ProgressDialog (Gtk.Window parent, bool threaded)
		{
			this.Build();
			this.TransientFor = parent;
			this.threaded = threaded;
		}
	
		public void ReportProgress (string message, double progress)
		{
			if (threaded) {
				Application.Invoke ((sender, args) => ShowProgress (message, progress));
			} else {
				ShowProgress (message, progress);
			}
		}
		
		private void ShowProgress (string message, double progress)
		{
			try {
				int newp = (int) (progress * 1000);
				if (lastp == newp)
					return;
				lastp = newp;
				
				label.Text = message;
				this.progress.Fraction = progress;
				while (Gtk.Application.EventsPending ())
					Gtk.Application.RunIteration ();
			} catch (Exception ex) {
				Console.WriteLine ("Exception while showing progress: {0}", ex.Message);
			}
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
