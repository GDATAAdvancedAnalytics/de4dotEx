using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using dnlib.DotNet;
using de4dot.blocks;

namespace de4dot.plugin.deobfuscators.SmartAssembly {
	public interface IDeobfuscatorVer {
		int DetectInternal();
		void ScanForObfuscator();
		void DeobfuscateBegin();
		void DeobfuscateEnd();
		void DeobfuscateMethodEnd(Blocks blocks);
		bool IsProxyCallFixerDetected();
	}
}
