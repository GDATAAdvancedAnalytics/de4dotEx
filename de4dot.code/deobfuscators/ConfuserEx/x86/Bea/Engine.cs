using System.IO;
using System.Runtime.InteropServices;

namespace de4dot.Bea
{
    public static class BeaEngine
    {
        // 'de4dot\bin\de4dot.blocks.dll' -> 'de4dot\bin\'
        private static string _executingPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);

        static BeaEngine()
        {
            //TODO: Better handle native DLL discovery
            SetDllDirectory(_executingPath);
        }

        [DllImport("kernel32", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool SetDllDirectory(string lpPathName);

        [DllImport("BeaEngine")]
        public static extern int Disasm([In, Out, MarshalAs(UnmanagedType.LPStruct)] Disasm disasm);

        [DllImport("BeaEngine")]
        private static extern string BeaEngineVersion();

        [DllImport("BeaEngine")]
        private static extern string BeaEngineRevision();

        public static string Version
        {
            get
            {
                return BeaEngineVersion();
            }
        }

        public static string Revision
        {
            get
            {
                return BeaEngineRevision();
            }
        }
    }
}
