using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using dnlib.DotNet;
using de4dot.code.deobfuscators;

namespace de4dot.code.deobfuscators.DotfuscatorAll {
	public interface IStringDecrypter  {
		bool Detected { get; }
		void Find(ISimpleDeobfuscator simpleDeobfuscator);
		IEnumerable<MethodDef> StringDecrypters { get; }
		 IEnumerable<StringDecrypterInfo> StringDecrypterInfos { get; }
		string Decrypt(IMethod method, string encrypted, int value);
		}
}
