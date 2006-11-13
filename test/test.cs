
using System;

namespace Application
{
	public class Test
	{
		public static void Main ()
		{
			staticStruct.endType = new EndType ();
			RefType rt = new RefType ();
			rt.valueType.endType = new EndType ();
			rt.endType = new EndType ();
			rt.structArray = new StructType [2];
			rt.structArray [0].endType = new EndType ();
			rt.structArray [1].endType = new EndType ();
			rt.endTypes = new EndType [2];
			rt.endTypes [0] = new EndType ();
			rt.endTypes [1] = new EndType ();
			refType = rt;
			Aux ();
			Console.WriteLine ("Ready");
			Console.ReadLine ();
			EndType rt2 = new EndType ();
			Console.WriteLine ("Ready 2");
			Console.ReadLine ();
		}
		
		static void Aux ()
		{
			EndType tt = new EndType ();
			Console.WriteLine (tt);
		}
		
		public static RefType refType;
		public static EndType staticReference = new EndType ();
		public static StructType staticStruct = new StructType ();
	}
	
	public class RefType
	{
		public StructType valueType;
		public EndType endType;
		public EndType[] endTypes;
		public StructType[] structArray;
		public string str = "hello ref";
	}
	
	public struct StructType
	{
		public EndType endType;
	}
	
	public class EndType
	{
		public string str = "hello";
	}
}
