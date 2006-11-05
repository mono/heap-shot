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

namespace HeapShot.Reader
{
	public class TypeInfo
	{
		uint code;
		string name;
		FieldInfo[] fields;
		
		public ArrayList Objects = new ArrayList ();
		
		internal TypeInfo (uint id, string name, FieldInfo[] fields)
		{
			this.code = id;
			this.name = name;
			this.fields = fields;
		}
		
		public uint Code {
			get { return code; }
		}
		
		public string Name {
			get { return name; }
		}
		
		public FieldInfo[] Fields {
			get { return fields; }
		}
		
		public uint TotalSize {
			get {
				uint s = 0;
				foreach (ObjectInfo oi in Objects)
					s += oi.Size;
				return s;
			}
		}
		
		public string GetFieldName (uint fieldCode)
		{
			foreach (FieldInfo f in Fields)
				if (f.Code == fieldCode)
					return f.Name;
			return null;
		}
	}
}
