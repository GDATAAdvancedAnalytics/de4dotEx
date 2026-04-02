using System;
using System.Runtime.InteropServices;

namespace de4dot.Bea
{
	public static class BeaEngine
	{
		[DllImport("BeaEngine")]
		public static extern int Disasm([In, Out, MarshalAs(UnmanagedType.LPStruct)] Disasm disasm);

		[DllImport("BeaEngine")]
		private static extern IntPtr BeaEngineVersion(); // returning string would call free() on a const char*

		[DllImport("BeaEngine")]
		private static extern IntPtr BeaEngineRevision();

		public static string Version => Marshal.PtrToStringAnsi(BeaEngineVersion());

		public static string Revision => Marshal.PtrToStringAnsi(BeaEngineRevision());
	}
}
