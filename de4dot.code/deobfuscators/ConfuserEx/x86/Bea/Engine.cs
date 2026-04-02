using System;
using System.IO;
using System.Runtime.InteropServices;

namespace de4dot.Bea
{
	public static class BeaEngine
	{
		// 'de4dot\bin\de4dot.blocks.dll' -> 'de4dot\bin\'
		private static readonly string ExecutingPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);

		static BeaEngine()
		{
			//TODO: Better handle native DLL discovery
			if (IsWindows())
				SetDllDirectory(ExecutingPath);
		}

		private static bool IsWindows()
		{
#if NET5_0_OR_GREATER
			return OperatingSystem.IsWindows();
#else
			return Environment.OSVersion.Platform == PlatformID.Win32NT;
#endif
		}

		[DllImport("kernel32", CharSet = CharSet.Auto, SetLastError = true)]
		private static extern bool SetDllDirectory(string lpPathName);

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
