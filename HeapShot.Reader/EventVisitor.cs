﻿// 
// EventVisitor.cs
//  
// Author:
//       Mike Krüger <mkrueger@novell.com>
// 
// Copyright (c) 2010 Novell, Inc (http://www.novell.com)
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

namespace MonoDevelop.Profiler
{
	public abstract class EventVisitor
	{
		public virtual object Visit (AllocEvent allocEvent)
		{
			return null;
		}
		
		public virtual object Visit (ResizeGcEvent resizeGcEvent)
		{
			return null;
		}
		
		public virtual object Visit (GcEvent gcEvent)
		{
			return null;
		}
		
		public virtual object Visit (MoveGcEvent moveGcEvent)
		{
			return null;
		}
		
		public virtual object Visit (HandleCreatedGcEvent handleCreatedGcEvent)
		{
			return null;
		}

        public virtual object Visit(HandleDestroyedGcEvent handleDestroyedGcEvent)
        {
            return null;
        }
        public virtual object Visit(HandleFinalizeObjectEvent handleDestroyedGcEvent)
        {
            return null;
        }
        public virtual object Visit(HandleFinalizeEvent handleDestroyedGcEvent)
        {
            return null;
        }

        public virtual object Visit (MetadataEvent metadataEvent)
		{
			return null;
		}
		
		public virtual object Visit (MethodEvent methodEvent)
		{
			return null;
		}
		
		public virtual object Visit (ExceptionEvent exceptionEvent)
		{
			return null;
		}

		public virtual object Visit (MonitiorEvent monitiorEvent)
		{
			return null;
		}

		public virtual object Visit (HeapEvent heapEvent)
		{
			return null;
		}

		public virtual object Visit (HitSampleEvent heapEvent)
		{
			return null;
		}

		public virtual object Visit (USymSampleEvent heapEvent)
		{
			return null;
		}

		public virtual object Visit (UBinSampleEvent heapEvent)
		{
			return null;
		}

		public virtual object Visit (CountersEvent heapEvent)
		{
			return null;
		}

		public virtual object Visit (CountersDescEvent heapEvent)
		{
			return null;
		}

		public virtual object Visit (RuntimeEvent heapEvent)
		{
			return null;
		}

		public virtual object Visit (RuntimeJitHelperEvent heapEvent)
		{
			return null;
		}

		public virtual object Visit (CoverageEvent heapEvent)
		{
			return null;
		}
        public virtual object Visit(MetaEvent heapEvent)
        {
            return null;
        }
    }
}

