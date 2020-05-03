﻿// 
// Event.cs
//  
// Authors:
//       Mike Krüger <mkrueger@novell.com>
//       Rolf Bjarne Kvinge <rolf@xamarin.com>
//       Łukasz Kucharski <lkucharski@antmicro.com>, <luk32@o2.pl>
// 
// Copyright (c) 2010 Novell, Inc (http://www.novell.com)
// Copyright (C) 2011 Xamarin Inc. (http://www.xamarin.com)
// Copyright (C) 2015 Antmicro Ltd. (http://antmicro.com)
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
using System.IO;
using System.Text;
using System.Collections.Generic;
using HeapShot.Reader;

namespace MonoDevelop.Profiler
{
	public enum EventType
	{
		Alloc      = 0,
		Gc         = 1,
		Metadata   = 2,
		Method     = 3,
		Exception  = 4,
		Monitor    = 5,
		Heap       = 6,
		Sample     = 7,
		Runtime    = 8,
		Coverage   = 9,
        Meta       = 10,
	}

	public class Backtrace
	{
//		public ulong Flags;
		public long[] Frame;
		
		public Backtrace (LogFileReader reader)
		{
//			Flags = reader.ReadULeb128 ();
			ulong num = reader.ReadULeb128 ();
			Frame = new long[num];
			for (ulong i = 0; i < num; i++) {
				Frame [i] = reader.ReadSLeb128 ();
			}
		}
	}
	
	public abstract class Event
	{
		/// <summary>
		/// Gets or sets the nanoseconds since last timing.
		/// </summary>
		/// <value>
		/// Nanoseconds since last timing.
		/// </value>
		public ulong TimeDiff {
			get;
			protected set;
		}
		
		public const byte TYPE_GC_EVENT = 1 << 4;
		public const byte TYPE_GC_RESIZE = 2 << 4;
		public const byte TYPE_GC_MOVE = 3 << 4;
		public const byte TYPE_GC_HANDLE_CREATED = 4 << 4;
		public const byte TYPE_GC_HANDLE_DESTROYED = 5 << 4;
		public const byte TYPE_GC_HANDLE_CREATED_BT   = 6 << 4;
		public const byte TYPE_GC_HANDLE_DESTROYED_BT = 7 << 4;

	    public const byte TYPE_GC_FINALIZE_START = 8 << 4;
	    public const byte TYPE_GC_FINALIZE_END = 9 << 4;
	    public const byte TYPE_GC_FINALIZE_OBJECT_START = 10 << 4;
	    public const byte TYPE_GC_FINALIZE_OBJECT_END = 11 << 4;

	    public const byte TYPE_SYNC_POINT = 0 << 4;
		public static Event CreateEvent (LogFileReader reader, EventType type, byte extendedInfo)
		{
			switch (type) {
			case EventType.Alloc:
				return AllocEvent.Read (reader, extendedInfo); 
			case EventType.Exception:
				return ExceptionEvent.Read (reader, extendedInfo);
			case EventType.Gc:
				switch (extendedInfo) {
				case TYPE_GC_EVENT:
					return GcEvent.Read (reader);
				case TYPE_GC_RESIZE:
					return ResizeGcEvent.Read (reader);
				case TYPE_GC_MOVE:
					return MoveGcEvent.Read (reader);
				case TYPE_GC_HANDLE_CREATED:
				case TYPE_GC_HANDLE_CREATED_BT:
					return HandleCreatedGcEvent.Read (reader, extendedInfo);
				case TYPE_GC_HANDLE_DESTROYED:
				case TYPE_GC_HANDLE_DESTROYED_BT:
					return HandleDestroyedGcEvent.Read (reader, extendedInfo);
                case TYPE_GC_FINALIZE_START:
                case TYPE_GC_FINALIZE_END:
                    return HandleFinalizeEvent.Read(reader, extendedInfo);
                case TYPE_GC_FINALIZE_OBJECT_START:
                case TYPE_GC_FINALIZE_OBJECT_END:
                    return HandleFinalizeObjectEvent.Read(reader, extendedInfo);
                }
                throw new InvalidOperationException ("unknown gc type:" + extendedInfo);
			case EventType.Heap:
				return HeapEvent.Read (reader, extendedInfo); 
			case EventType.Metadata:
				return MetadataEvent.Read (reader, extendedInfo); 
			case EventType.Method:
				return MethodEvent.Read (reader, extendedInfo); 
			case EventType.Monitor:
				return MonitiorEvent.Read (reader, extendedInfo); 
			case EventType.Sample:
				return SampleEvent.Read (reader, extendedInfo);
			case EventType.Runtime:
				return RuntimeEvent.Read (reader, extendedInfo);
			case EventType.Coverage:
				return CoverageEvent.Read (reader, extendedInfo);
            case EventType.Meta:
                return MetaEvent.Read(reader, extendedInfo);
            }
            throw new InvalidOperationException ("invalid event type " + type);	
		}
		
		public static Event Read (LogFileReader reader)
		{
			byte info = reader.ReadByte ();
			EventType type = (EventType)(info & 0xF);
			byte extendedInfo = (byte)(info & 0xF0);
			return CreateEvent (reader, type, extendedInfo);
		}
		
		public abstract object Accept (EventVisitor visitor);
	}
	
	// type == Alloc
	public class AllocEvent : Event
	{
		public const byte TYPE_ALLOC_BT = 1 << 4;
		public readonly long Ptr; // class as a byte difference from ptr_base
		public readonly long Obj; // object address as a byte difference from obj_base
		public readonly ulong Size; // size of the object in the heap
		public readonly Backtrace Backtrace;
		
		AllocEvent (LogFileReader reader, byte extendedInfo)
		{
			TimeDiff = reader.ReadULeb128 ();
			Ptr = reader.ReadSLeb128 ();
			Obj = reader.ReadSLeb128 ();
			Size = reader.ReadULeb128 ();
			if ((extendedInfo & TYPE_ALLOC_BT) != 0)
				Backtrace = new Backtrace (reader);
		}
		
		public static Event Read (LogFileReader reader, byte extendedInfo)
		{
			return new AllocEvent (reader, extendedInfo);
		}

		public override object Accept (EventVisitor visitor)
		{
			return visitor.Visit (this);
		}
	}
	
	// type == Gc
	public class ResizeGcEvent : Event
	{
		public readonly ulong HeapSize; // new heap size
		
		ResizeGcEvent (LogFileReader reader)
		{
			TimeDiff = reader.ReadULeb128 ();
			HeapSize = reader.ReadULeb128 ();
		}
		
		public static new Event Read (LogFileReader reader)
		{
			return new ResizeGcEvent (reader);
		}
		
		public override object Accept (EventVisitor visitor)
		{
			return visitor.Visit (this);
		}
	}
	
	public class GcEvent : Event
	{
        //public readonly GcEventType EventType; //  GC event (MONO_GC_EVENT_* from profiler.h)
        //public readonly ulong Generation;  // GC generation event refers to
        public readonly GcEventType EventType; //  GC event (MONO_GC_EVENT_* from profiler.h)
        public readonly byte Generation;  // GC generation event refers to

        public enum GcEventType {
			Start,
			MarkStart,
			MarkEnd,
			ReclaimStart,
			ReclaimEnd,
			End,
			PreStopWorld,
			PostStopWorld,
			PreStartWorld,
			PostStartWorld
		}
		
		GcEvent (LogFileReader reader)
		{
			TimeDiff = reader.ReadULeb128 ();
            //EventType = (GcEventType) reader.ReadULeb128 ();
            //Generation = reader.ReadULeb128 ();
            EventType = (GcEventType)reader.ReadByte();
            Generation = reader.ReadByte();
        }

        public static new Event Read (LogFileReader reader)
		{
			return new GcEvent (reader);
		}

		public override object Accept (EventVisitor visitor)
		{
			return visitor.Visit (this);
		}
	}
	
	public class MoveGcEvent : Event
	{
		public readonly long[] ObjAddr; //  num_objects object pointer differences from obj_base
		
		MoveGcEvent (LogFileReader reader)
		{
			TimeDiff = reader.ReadULeb128 ();
			ulong num = reader.ReadULeb128 ();
			ObjAddr = new long[num];
			for (ulong i = 0; i < num; i++) {
				ObjAddr [i] = reader.ReadSLeb128 ();
			}
		}
		
		public static new Event Read (LogFileReader reader)
		{
			return new MoveGcEvent (reader);
		}

		public override object Accept (EventVisitor visitor)
		{
			return visitor.Visit (this);
		}
	}
	
	public class HandleCreatedGcEvent : Event
	{
		public readonly ulong HandleType; // GC handle type (System.Runtime.InteropServices.GCHandleType)
		public readonly ulong Handle; // GC handle value
		public readonly long ObjAddr; // object pointer differences from obj_base

		HandleCreatedGcEvent (LogFileReader reader, byte exinfo)
		{
			TimeDiff = reader.ReadULeb128 ();
			HandleType = reader.ReadULeb128 ();
			Handle = reader.ReadULeb128 ();
			ObjAddr = reader.ReadSLeb128 ();
			if (exinfo == TYPE_GC_HANDLE_CREATED_BT)
				new Backtrace (reader);
		}
		
		public static Event Read (LogFileReader reader, byte exinfo)
		{
			return new HandleCreatedGcEvent (reader, exinfo);
		}

		public override object Accept (EventVisitor visitor)
		{
			return visitor.Visit (this);
		}
	}
	
	public class HandleDestroyedGcEvent : Event
	{
		public readonly ulong HandleType; // GC handle type (System.Runtime.InteropServices.GCHandleType)
		public readonly ulong Handle; // GC handle value
		
		HandleDestroyedGcEvent (LogFileReader reader, byte exinfo)
		{
			TimeDiff = reader.ReadULeb128 ();
			HandleType = reader.ReadULeb128 ();
			Handle = reader.ReadULeb128 ();
			if (exinfo == TYPE_GC_HANDLE_DESTROYED_BT)
				new Backtrace (reader);
		}
		
		public static Event Read (LogFileReader reader, byte exinfo)
		{
			return new HandleDestroyedGcEvent (reader, exinfo);
		}

		public override object Accept (EventVisitor visitor)
		{
			return visitor.Visit (this);
		}
	}
    public class HandleFinalizeObjectEvent : Event
    {
        public readonly long Object; // object pointer differences from obj_base

        HandleFinalizeObjectEvent(LogFileReader reader, byte exinfo)
        {
            TimeDiff = reader.ReadULeb128();
            Object = reader.ReadSLeb128();
        }

        public static Event Read(LogFileReader reader, byte exinfo)
        {
            return new HandleFinalizeObjectEvent(reader, exinfo);
        }

        public override object Accept(EventVisitor visitor)
        {
            return visitor.Visit(this);
        }
    }
    public class HandleFinalizeEvent : Event
    {

        HandleFinalizeEvent(LogFileReader reader, byte exinfo)
        {
            TimeDiff = reader.ReadULeb128();
        }

        public static Event Read(LogFileReader reader, byte exinfo)
        {
            return new HandleFinalizeEvent(reader, exinfo);
        }

        public override object Accept(EventVisitor visitor)
        {
            return visitor.Visit(this);
        }
    }

    // type == Methadata
    public class MetadataEvent : Event
	{
		public enum MetaDataType : byte
		{
			Class = 1,
			Image = 2,
			Assembly = 3,
			Domain = 4,
			Thread = 5,
			Context = 6
		}
		
		public readonly MetaDataType MType; //  metadata type, one of: TYPE_CLASS, TYPE_IMAGE, TYPE_ASSEMBLY, TYPE_DOMAINTYPE_THREAD
		public readonly long Pointer; // pointer of the metadata type depending on mtype
		public readonly long Domain; // domain id as a pointer

//		public readonly ulong Flags; // must be 0
		public readonly string Name; // full class/image file or thread name 
		
		public readonly long Image; // MonoImage* as a pointer difference from ptr_base
	
		MetadataEvent (LogFileReader reader, byte extendedInfo)
		{
			TimeDiff = reader.ReadULeb128 ();
			MType = (MetaDataType)reader.ReadByte ();
			Pointer = reader.ReadSLeb128 ();
			switch (MType) {
			case MetaDataType.Class:
				Image = reader.ReadSLeb128 ();
//				Flags = reader.ReadULeb128 ();
				Name = reader.ReadNullTerminatedString ();
				break;
			case MetaDataType.Image:
//				Flags = reader.ReadULeb128 ();
				Name = reader.ReadNullTerminatedString ();
				break;
			case MetaDataType.Assembly:
//				Flags = reader.ReadULeb128 ();
				Name = reader.ReadNullTerminatedString ();
				break;
			case MetaDataType.Thread:
                //				Flags = reader.ReadULeb128 ();
                //if (reader.Header.Format < 11 || (reader.Header.Format > 10 && extendedInfo == 0)) {
                //	Name = reader.ReadNullTerminatedString ();
                //}
                if (extendedInfo == 0)
                    Name = reader.ReadNullTerminatedString();
                break;
			case MetaDataType.Domain:
//				Flags = reader.ReadULeb128 ();
				if (extendedInfo == 0)
					Name = reader.ReadNullTerminatedString ();
				break;
			case MetaDataType.Context:
//				Flags = reader.ReadULeb128 ();
				Domain = reader.ReadSLeb128 ();
				break;
			default:
				throw new ArgumentException ("Unknown metadata type: " + MType);
			}
		}
		
		public static Event Read (LogFileReader reader, byte extendedInfo)
		{
			return new MetadataEvent (reader, extendedInfo);
		}

		public override object Accept (EventVisitor visitor)
		{
			return visitor.Visit (this);
		}
	}
	
	// type == Method
	public class MethodEvent : Event
	{
		public enum MethodType
		{
			Leave = 1 << 4,
			Enter = 2 << 4,
			ExcLeave = 3 << 4,
			Jit = 4 << 4
		};
		
		public readonly long Method; //  MonoMethod* as a pointer difference from the last such pointer or the buffer method_base
		public readonly MethodType Type;
		
		public readonly long CodeAddress; // pointer to the native code as a diff from ptr_base
		public readonly ulong CodeSize; // size of the generated code
		public readonly string Name; // full method name
		
		MethodEvent (LogFileReader reader, byte exinfo)
		{
			TimeDiff = reader.ReadULeb128 ();
			Method = reader.ReadSLeb128 ();
			Type = (MethodType)exinfo;
			if (Type == MethodType.Jit) {
				CodeAddress = reader.ReadSLeb128 ();
				CodeSize = reader.ReadULeb128 ();
				Name = reader.ReadNullTerminatedString ();
			}
		}
		
		public static Event Read (LogFileReader reader, byte exinfo)
		{
			return new MethodEvent (reader, exinfo);
		}

		public override object Accept (EventVisitor visitor)
		{
			return visitor.Visit (this);
		}
	}
	
	// type == Exception
	public class ExceptionEvent : Event
	{
		public const byte TYPE_THROW = 0 << 4;
		public const byte TYPE_CLAUSE = 1 << 4;
		public const byte TYPE_EXCEPTION_BT = 1 << 7;

        // Type clause
        //		public readonly ulong ClauseType; // finally/catch/fault/filter
        public readonly byte ClauseType; // finally/catch/fault/filter
        public readonly ulong ClauseNum; // the clause number in the method header
		public readonly long Method; //  MonoMethod* as a pointer difference from the last such pointer or the buffer method_base
		
		// Type throw
		public readonly long Object; // the object that was thrown as a difference from obj_base If the TYPE_EXCEPTION_BT flag is set, a backtrace follows.
		public readonly Backtrace Backtrace;
		
		ExceptionEvent (LogFileReader reader, byte exinfo)
		{
			TimeDiff = reader.ReadULeb128 ();
			byte subtype = (byte)(exinfo & ~TYPE_EXCEPTION_BT);
			if (subtype == TYPE_CLAUSE) {
				//ClauseType = reader.ReadULeb128 ();
			    ClauseType = reader.ReadByte();
				ClauseNum = reader.ReadULeb128 ();
				Method = reader.ReadSLeb128 ();
			} else if (subtype == TYPE_THROW) {
				Object = reader.ReadSLeb128 ();
				if ((exinfo & TYPE_EXCEPTION_BT) == TYPE_EXCEPTION_BT)
					Backtrace = new Backtrace (reader);
			} else
				throw new InvalidOperationException ("Unknown exception event type:" + (exinfo & ~TYPE_EXCEPTION_BT));
		}
		
		public static Event Read (LogFileReader reader, byte exinfo)
		{
			return new ExceptionEvent (reader, exinfo);
		}

		public override object Accept (EventVisitor visitor)
		{
			return visitor.Visit (this);
		}
	}
	
	// type == Monitor
	public class MonitiorEvent : Event
	{
		public const int MONO_PROFILER_MONITOR_CONTENTION = 1;
		public const int MONO_PROFILER_MONITOR_DONE = 2;
		public const int MONO_PROFILER_MONITOR_FAIL = 3;
		public const byte TYPE_MONITOR_BT = 1 << 7;
		
		public readonly long Object; //  the lock object as a difference from obj_base
		public readonly Backtrace Backtrace;
		
		MonitiorEvent (LogFileReader reader, byte exinfo)
		{
			TimeDiff = reader.ReadULeb128 ();
			Object = reader.ReadSLeb128 ();
			byte ev = (byte)((exinfo >> 4) & 0x3);
			if (ev == MONO_PROFILER_MONITOR_CONTENTION && (exinfo & TYPE_MONITOR_BT) == TYPE_MONITOR_BT) {
				Backtrace = new Backtrace (reader);
			}
		}
		
		public static Event Read (LogFileReader reader, byte exinfo)
		{
			return new MonitiorEvent (reader, exinfo);
		}

		public override object Accept (EventVisitor visitor)
		{
			return visitor.Visit (this);
		}
	}
	
	// type == Heap
	public class HeapEvent : Event
	{
		public const byte TYPE_HEAP_START = 0 << 4;
		public const byte TYPE_HEAP_END = 1 << 4;
		public const byte TYPE_HEAP_OBJECT = 2 << 4;
		public const byte TYPE_HEAP_ROOT = 3 << 4;
		
		public readonly EventType Type;
		public readonly long Object; // the object as a difference from obj_base
		public readonly long Class; // the object MonoClass* as a difference from ptr_base
		public readonly ulong Size; // size of the object on the heap
		public readonly ulong[] RelOffset;
		public readonly long[] ObjectRefs; // object referenced as a difference from obj_base
		public readonly long[] RootRefs; // root references
		public readonly RootType[] RootRefTypes;
		public readonly ulong[] RootRefExtraInfos;
		
		public enum RootType
		{
			Pinning  = 1 << 8,
			WeakRef  = 2 << 8,
			Interior = 4 << 8,
			/* the above are flags, the type is in the low 2 bytes */
			Stack = 0,
			Finalizer = 1,
			Handle = 2,
			Other = 3,
			Misc = 4, /* could be stack, handle, etc. */
			TypeMask = 0xff
		}
		
		public enum EventType
		{
			Start,
			End,
			Root,
			Object
		};
		
		HeapEvent (LogFileReader reader, byte exinfo)
		{
            if (exinfo == TYPE_HEAP_START) {
				Type = EventType.Start;
				TimeDiff = reader.ReadULeb128 ();
			} else if (exinfo == TYPE_HEAP_END) {
				Type = EventType.End;
				TimeDiff = reader.ReadULeb128 ();
			} else if (exinfo == TYPE_HEAP_ROOT) {
                //omanuke
                TimeDiff = reader.ReadULeb128();

                Type = EventType.Root;
				ulong nroots = reader.ReadULeb128 ();
				reader.ReadULeb128 (); // gcs
                RootRefs = new long [nroots];
				RootRefTypes = new RootType [nroots];
				RootRefExtraInfos = new ulong [nroots];
				for (ulong n=0; n<nroots; n++) {
					RootRefs [n] = reader.ReadSLeb128 ();
                    //					RootRefTypes [n] = (RootType) reader.ReadULeb128 ();
                    RootRefTypes[n] = (RootType)reader.ReadByte();
                    RootRefExtraInfos[n] = reader.ReadULeb128 ();
				}
			} else if (exinfo == TYPE_HEAP_OBJECT) {
                
                TimeDiff = reader.ReadULeb128();

                Type = EventType.Object;
				Object = reader.ReadSLeb128 ();
				Class = reader.ReadSLeb128 ();
				Size = reader.ReadULeb128 ();
				ulong num = reader.ReadULeb128 ();
				ObjectRefs = new long[num];
				RelOffset = new ulong[num];
				for (ulong i = 0; i < num; i++) {
					RelOffset [i] = reader.ReadULeb128 ();
					ObjectRefs [i] = reader.ReadSLeb128 ();
				}
			}
		}
		
		public static Event Read (LogFileReader reader, byte exinfo)
		{
			return new HeapEvent (reader, exinfo);
		}

		public override object Accept (EventVisitor visitor)
		{
			return visitor.Visit (this);
		}
	}

	public abstract class SampleEvent: Event
	{
		//from: `mono/profiler/proflog.h`
		public const byte TYPE_SAMPLE_HIT           = 0 << 4;
		public const byte TYPE_SAMPLE_USYM          = 1 << 4;
		public const byte TYPE_SAMPLE_UBIN          = 2 << 4;
		public const byte TYPE_SAMPLE_COUNTERS_DESC = 3 << 4;
		public const byte TYPE_SAMPLE_COUNTERS      = 4 << 4;

		public static Event Read (LogFileReader reader, byte exinfo)
		{
			if (exinfo == TYPE_SAMPLE_HIT)
				return new HitSampleEvent (reader);
			else if (exinfo == TYPE_SAMPLE_USYM)
				return new USymSampleEvent (reader);
			else if (exinfo == TYPE_SAMPLE_UBIN)
				return new UBinSampleEvent (reader);
			else if (exinfo == TYPE_SAMPLE_COUNTERS_DESC)
				return new CountersDescEvent (reader);
			else if (exinfo == TYPE_SAMPLE_COUNTERS)
				return new CountersEvent (reader);
			else
				throw new ArgumentException("Unknown `TYPE_SAMPLE` event: "+exinfo);
		}
	}

	public class HitSampleEvent: SampleEvent
	{
		public readonly SampleType SampleType;

	    public readonly long Thread;

		//public readonly ulong Timestamp;
        public readonly long[] InstructionPointers;
        public readonly long[] Methods;

        public HitSampleEvent (LogFileReader reader)
		{
            TimeDiff = reader.ReadULeb128();
            //			SampleType = (SampleType) reader.ReadULeb128 ();
            SampleType = (SampleType)reader.ReadByte();
            //Timestamp = reader.ReadULeb128 ();
            Thread = reader.ReadSLeb128();
            ulong count = reader.ReadULeb128 ();
			InstructionPointers = new long [count];
			for (uint n=0; n<count; n++)
				InstructionPointers [n] = reader.ReadSLeb128 ();

            //Xamarin.Profiler 0.34 doesn't generate data below?

            //ulong mcount = reader.ReadULeb128();
            //Methods = new long[mcount];
            //for (uint n = 0; n < mcount; n++)
            //    Methods[n] = reader.ReadSLeb128();
        }

        public override object Accept (EventVisitor visitor)
		{
			return visitor.Visit (this);
		}
	}

	public class USymSampleEvent: SampleEvent
	{
		public readonly long Address;
		public readonly ulong Size;
		public readonly string Name;
		
		public USymSampleEvent (LogFileReader reader)
		{
            TimeDiff = reader.ReadULeb128();

            Address = reader.ReadSLeb128 ();
			Size = reader.ReadULeb128 ();
			Name = reader.ReadNullTerminatedString ();
		}
		
		public override object Accept (EventVisitor visitor)
		{
			return visitor.Visit (this);
		}
	}
	
	public class UBinSampleEvent: SampleEvent
	{
		public readonly long Address;
		public readonly ulong Offset;
		public readonly ulong Size;
		public readonly string Name;
		
		public UBinSampleEvent (LogFileReader reader)
		{
			TimeDiff = reader.ReadULeb128 ();
			Address = reader.ReadSLeb128 ();
			Offset = reader.ReadULeb128 ();
			Size = reader.ReadULeb128 ();
			Name = reader.ReadNullTerminatedString ();
		}
		
		public override object Accept (EventVisitor visitor)
		{
			return visitor.Visit (this);
		}
	}

	public class CountersDescEvent: SampleEvent
	{
		public readonly ulong Len; 
		public CounterSection[] Sections {get; private set;}

		public CountersDescEvent (LogFileReader reader)
		{
            //omanuke
            TimeDiff = reader.ReadULeb128();

            Len = reader.ReadULeb128 ();
			Sections = new CounterSection[Len];
			for (ulong i = 0; i < Len; i++) {
				Sections [i] = new CounterSection (reader);
			}
		}

		public override object Accept (EventVisitor visitor)
		{
			return visitor.Visit (this);
		}
	}

	public class CountersEvent: SampleEvent
	{
		public readonly ulong Timestamp;

		public CountersEvent (LogFileReader reader)
		{
            //TimeDiff = reader.ReadULeb128();

            Timestamp = reader.ReadULeb128 ();
			var index = reader.ReadULeb128 ();
			while (index != 0) {
				new CounterValue (reader, index);
				index = reader.ReadULeb128 ();
			}
		}

		public override object Accept (EventVisitor visitor)
		{
			return visitor.Visit (this);
		}
	}

	public class CounterValue
	{
        //		public readonly uint Type;
        public readonly byte Type;
        public readonly ulong Index;

		public CounterValue (LogFileReader reader, ulong index)
		{
			Index = index;
            //			Type = (uint)reader.ReadULeb128 ();
            Type = reader.ReadByte();
            switch ((CounterValueType)Type) {
			case CounterValueType.MONO_COUNTER_STRING:
				if (reader.ReadULeb128 () == 1)
					reader.ReadNullTerminatedString ();
				break;
			case CounterValueType.MONO_COUNTER_WORD:
			case CounterValueType.MONO_COUNTER_INT:
			case CounterValueType.MONO_COUNTER_LONG:
				reader.ReadSLeb128 ();
				break;
			case CounterValueType.MONO_COUNTER_UINT:
			case CounterValueType.MONO_COUNTER_ULONG:
				reader.ReadULeb128 ();
				break;
			case CounterValueType.MONO_COUNTER_DOUBLE:
				reader.ReadUInt64 ();
				break;
			default:
				throw new ArgumentException (String.Format("Unknown Counter Value type {0} [0x{0:x8}], for counter at index {3}, near byte {1} [0x{1:x8}] of {2}.", Type, reader.Position, reader.Length, Index));
			}
		}

		//from `mono/utils/mono-counters.h`
		enum CounterValueType
		{
			/* Counter type, bits 0-7. */
			MONO_COUNTER_INT,    /* 32 bit int */
			MONO_COUNTER_UINT,    /* 32 bit uint */
			MONO_COUNTER_WORD,   /* pointer-sized int */
			MONO_COUNTER_LONG,   /* 64 bit int */
			MONO_COUNTER_ULONG,   /* 64 bit uint */
			MONO_COUNTER_DOUBLE,
			MONO_COUNTER_STRING, /* char* */
			MONO_COUNTER_TIME_INTERVAL, /* 64 bits signed int holding usecs. */
		}
	}

	public class CounterSection
	{
		//from `mono/utils/mono-counters.h`
		public const ulong MONO_COUNTER_PERFCOUNTERS = 1 << 15;

		public readonly ulong Section;
        //public readonly ulong Type;
        //public readonly ulong Unit;
        //public readonly ulong Variance;
        public readonly byte Type;
        public readonly byte Unit;
        public readonly byte Variance;
		public readonly ulong Index;
		public readonly string Name;
		public readonly string SectionName;

		public CounterSection ( LogFileReader reader )
		{
			Section = reader.ReadULeb128 ();
			if (Section == MONO_COUNTER_PERFCOUNTERS)
				SectionName = reader.ReadNullTerminatedString ();

			Name = reader.ReadNullTerminatedString ();
            //Type = reader.ReadULeb128 ();
            //Unit = reader.ReadULeb128 ();
            //Variance = reader.ReadULeb128 ();
            Type = reader.ReadByte();
            Unit = reader.ReadByte();
            Variance = reader.ReadByte();
            Index = reader.ReadULeb128 ();
		}
	}

	//from `mono/profiler/proflog.h`
	public enum SampleType
	{
		SAMPLE_CYCLES = 1,
		SAMPLE_INSTRUCTIONS = 2,
		SAMPLE_CACHE_MISSES = 3,
		SAMPLE_CACHE_REFS = 4,
		SAMPLE_BRANCHES = 5,
		SAMPLE_BRANCH_MISSES = 6,
		SAMPLE_LAST = 7
	};

	public class RuntimeEvent : Event
	{
		//from `mono/profiler/proflog.h`
		public const byte TYPE_JITHELPER = 1 << 4;

		public readonly ulong Time;

		public static Event Read (LogFileReader reader, byte extendedInfo)
		{
			if (extendedInfo == TYPE_JITHELPER)
				return new RuntimeJitHelperEvent (reader);
			throw new ArgumentException ("Unknown `RuntimeEventType`: " + extendedInfo);
		}

		public RuntimeEvent(LogFileReader reader)
		{
			Time = reader.ReadULeb128 ();
		}

		public override object Accept (EventVisitor visitor)
		{
			return visitor.Visit (this);
		}
	}

	public class RuntimeJitHelperEvent : RuntimeEvent
	{
        //		public readonly ulong Type;
        public readonly byte Type;
        public readonly long BufferAddress;
		public readonly ulong BufferSize;
		public readonly string Name;

		public RuntimeJitHelperEvent(LogFileReader reader) : base(reader)
		{
            //			Type = reader.ReadULeb128 ();
            Type = reader.ReadByte();
            BufferAddress = reader.ReadSLeb128 ();
			BufferSize = reader.ReadULeb128 ();
			if (Type == (ulong)MonoProfilerCodeBufferType.MONO_PROFILER_CODE_BUFFER_SPECIFIC_TRAMPOLINE) {
				Name = reader.ReadNullTerminatedString ();
			}
		}

		public override object Accept (EventVisitor visitor)
		{
			return visitor.Visit (this);
		}

		//from `mono/metadata/profiler.h`
		enum MonoProfilerCodeBufferType {
			MONO_PROFILER_CODE_BUFFER_UNKNOWN,
			MONO_PROFILER_CODE_BUFFER_METHOD,
			MONO_PROFILER_CODE_BUFFER_METHOD_TRAMPOLINE,
			MONO_PROFILER_CODE_BUFFER_UNBOX_TRAMPOLINE,
			MONO_PROFILER_CODE_BUFFER_IMT_TRAMPOLINE,
			MONO_PROFILER_CODE_BUFFER_GENERICS_TRAMPOLINE,
			MONO_PROFILER_CODE_BUFFER_SPECIFIC_TRAMPOLINE,
			MONO_PROFILER_CODE_BUFFER_HELPER,
			MONO_PROFILER_CODE_BUFFER_MONITOR,
			MONO_PROFILER_CODE_BUFFER_DELEGATE_INVOKE,
			MONO_PROFILER_CODE_BUFFER_LAST
		}
	}

	public abstract class CoverageEvent : Event
	{
		public const byte TYPE_COVERAGE_ASSEMBLY  = 0 << 4;
		public const byte TYPE_COVERAGE_METHOD    = 1 << 4;
		public const byte TYPE_COVERAGE_STATEMENT = 2 << 4;
		public const byte TYPE_COVERAGE_CLASS     = 3 << 4;

		public static Event Read (LogFileReader reader, byte extendedInfo)
		{
			switch (extendedInfo) {
				case TYPE_COVERAGE_ASSEMBLY: return new CoverageAssemblyEvent (reader);
				case TYPE_COVERAGE_METHOD: return new CoverageMethodEvent (reader);
				case TYPE_COVERAGE_STATEMENT: return new CoverageStatementEvent (reader);
				case TYPE_COVERAGE_CLASS: return new CoverageClassEvent (reader);
			}
			throw new ArgumentException ("Unknown `CoverageEventType`: " + extendedInfo);
		}

		public override object Accept (EventVisitor visitor)
		{
			return visitor.Visit (this);
		}
	}

	class CoverageAssemblyEvent: CoverageEvent 
	{
		public readonly string Name;
		public readonly string Guid;
		public readonly string Filename;
		public readonly ulong NumberOfMethods;
		public readonly ulong FullyCovered;
		public readonly ulong PartiallyCovered;

		public CoverageAssemblyEvent (LogFileReader reader)
		{
			Name = reader.ReadNullTerminatedString ();
			Guid = reader.ReadNullTerminatedString ();
			Filename = reader.ReadNullTerminatedString ();
			NumberOfMethods = reader.ReadULeb128 ();
			FullyCovered = reader.ReadULeb128 ();
			PartiallyCovered = reader.ReadULeb128 ();
		}
	}

	class CoverageMethodEvent: CoverageEvent
	{
		public readonly string Assembly;
		public readonly string Class;
		public readonly string Name;
		public readonly string Signature;
		public readonly string Filename;
		public readonly ulong Token;
		public readonly ulong MethodId;
		public readonly ulong Len;

		public CoverageMethodEvent (LogFileReader reader)
		{
			Assembly = reader.ReadNullTerminatedString ();
			Class = reader.ReadNullTerminatedString ();
			Name = reader.ReadNullTerminatedString ();
			Signature =reader.ReadNullTerminatedString ();
			Filename = reader.ReadNullTerminatedString ();
			Token = reader.ReadULeb128 ();
			MethodId = reader.ReadULeb128 ();
			Len = reader.ReadULeb128 ();
		}
	}

	class CoverageClassEvent: CoverageEvent
	{
		public readonly string Name;
		public readonly string Class;
		public readonly ulong NumberOfMethods;
		public readonly ulong FullyCovered;
		public readonly ulong PartiallyCovered;

		public CoverageClassEvent (LogFileReader reader)
		{
			Name = reader.ReadNullTerminatedString ();
			Class = reader.ReadNullTerminatedString ();
			NumberOfMethods = reader.ReadULeb128 ();
			FullyCovered = reader.ReadULeb128 ();
			PartiallyCovered = reader.ReadULeb128 ();

		}
	}

	public class CoverageStatementEvent : CoverageEvent
	{
		public readonly ulong MethodId;
		public readonly ulong Offset;
		public readonly ulong Counter;
		public readonly ulong Line;
		public readonly ulong Column;

		public CoverageStatementEvent (LogFileReader reader)
		{
			MethodId = reader.ReadULeb128 ();
			Offset = reader.ReadULeb128 ();
			Counter = reader.ReadULeb128 ();
			Line = reader.ReadULeb128 ();
			Column = reader.ReadULeb128 ();
		}
	}

    public class MetaEvent : SampleEvent
    {
        public readonly byte MonoProfilerSyncPointType;

        MetaEvent(LogFileReader reader,byte exinfo)
        {
            TimeDiff = reader.ReadULeb128();
            if (exinfo ==TYPE_SYNC_POINT)
            {
                MonoProfilerSyncPointType = reader.ReadByte();
            }
        }
        public static Event Read(LogFileReader reader, byte extendedInfo)
        {
            return new MetaEvent(reader, extendedInfo);
        }

        public override object Accept(EventVisitor visitor)
        {
            return visitor.Visit(this);
        }
    }
}
