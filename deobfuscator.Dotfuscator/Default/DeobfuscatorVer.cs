using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using dnlib.DotNet;


namespace de4dot.plugin.deobfuscators.Dotfuscator.Default {
	internal class DeobfuscatorVer : IDeobfuscatorVer {
		private StringDecrypter stringDecrypter;
		private Deobfuscator parent;
		private ModuleDefMD module;


		public DeobfuscatorVer(Deobfuscator parent) {
			this.parent = parent;
			this.module = parent.GetModule();
		}

		public void DeobfuscateBegin() {
			DoCflowClean();
			DoStringBuilderClean();
			foreach (var info in stringDecrypter.StringDecrypterInfos)
				parent.GetStaticStringInliner().Add(info.method, (method, gim, args) => stringDecrypter.Decrypt(method, (string)args[0], (int)args[1]));
		}

		public void DeobfuscateEnd() {
		}

		public int DetectInternal() {
			if (stringDecrypter.Detected)
				return 100;
			else
				return 0;
		}

		public IEnumerable<int> GetStringDecrypterMethods() {
			var list = new List<int>();
			foreach (var method in stringDecrypter.StringDecrypters)
				list.Add(method.MDToken.ToInt32());
			return list;
		}

		public IEnumerable<MethodDef> GetStringDecrypters() {
			return stringDecrypter.StringDecrypters;
		}

		public void ScanForObfuscator() {
			stringDecrypter = new StringDecrypter(module);
			stringDecrypter.Find(parent.GetDeobfuscatedFile());
		}

		public void DoCflowClean() {
			var cflowDescrypter = new CflowDecrypter(module);
			cflowDescrypter.CflowClean();
		}

		public void DoStringBuilderClean() {
			var decrypter = new StringBuilderDecrypter(module);
			decrypter.StringBuilderClean();
		}
	}
}
