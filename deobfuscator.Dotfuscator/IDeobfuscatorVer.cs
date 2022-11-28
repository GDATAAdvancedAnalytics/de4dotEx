using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using dnlib.DotNet;

namespace de4dot.plugin.deobfuscators.Dotfuscator {
	public interface IDeobfuscatorVer {
		int DetectInternal();
		void ScanForObfuscator();
		void DeobfuscateBegin();
		void DeobfuscateEnd();
		IEnumerable<int> GetStringDecrypterMethods();
		IEnumerable<MethodDef> GetStringDecrypters();
	}
}
