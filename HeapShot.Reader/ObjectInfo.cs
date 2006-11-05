//
// OutfileReader.cs
//
// Copyright (C) 2005 Novell, Inc.
//

//
// This program is free software; you can redistribute it and/or
// modify it under the terms of version 2 of the GNU General Public
// License as published by the Free Software Foundation.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with this program; if not, write to the Free Software
// Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA 02111-1307
// USA.
//

using System;
using System.Collections;
using System.IO;
using System.Text.RegularExpressions;

namespace HeapShot.Reader {
	
	public class ObjectInfo
	{
		TypeInfo type;
		uint size;
		ObjectReference[] references;
		uint code;
		
		public ArrayList Referencers;
		
		public ObjectInfo (uint code, TypeInfo type, uint size, ObjectReference[] references)
		{
			this.code = code;
			this.type = type;
			this.size = size + (uint)IntPtr.Size + (uint)IntPtr.Size;	// Add MonoObject overhead
			this.references = references;
		}
		
		public uint Code {
			get { return code; }
		}
		
		public TypeInfo Type {
			get { return type; }
		}
		
		public uint Size {
			get { return size; }
		}
		
		public ObjectReference[] References {
			get { return references; }
		}
		
		public string GetReferencerField (ObjectInfo referenced)
		{
			foreach (ObjectReference oref in References) {
				if (oref.Object == referenced)
					return this.Type.GetFieldName (oref.FieldCode);
			}
			return null;
		}
	}
}
