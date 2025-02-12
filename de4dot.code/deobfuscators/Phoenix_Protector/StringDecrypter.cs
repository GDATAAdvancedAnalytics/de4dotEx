using System.Collections.Generic;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using de4dot.blocks;

namespace de4dot.code.deobfuscators.Phoenix_Protector {
	class StringDecrypter {
		ModuleDefMD module;

		MethodDefAndDeclaringTypeDict<StringDecrypterInfo> stringDecrypterMethods =
			new MethodDefAndDeclaringTypeDict<StringDecrypterInfo>();

		TypeDef stringDecrypterType;

		// Class of string decrypter function
		public TypeDef Type => stringDecrypterType;

		public class StringDecrypterInfo {
			public MethodDef method;
			public StringDecrypterInfo(MethodDef method) { this.method = method; }
		}

		public bool Detected => stringDecrypterMethods.Count > 0;

		public IEnumerable<MethodDef> StringDecrypters {
			get {
				var list = new List<MethodDef>(stringDecrypterMethods.Count);
				foreach (var info in stringDecrypterMethods.GetValues())
					list.Add(info.method); //adding all calls for string decryptor
				return list;
			}
		}

		public IEnumerable<StringDecrypterInfo> StringDecrypterInfos => stringDecrypterMethods.GetValues();

		public void Find(ISimpleDeobfuscator simpleDeobfuscator) {
			foreach (var type in module.GetTypes()) {
				FindStringDecrypterMethods(type, simpleDeobfuscator);
			}
		}

		void FindStringDecrypterMethods(TypeDef type, ISimpleDeobfuscator simpleDeobfuscator) //Seartching for Decrypt Function
		{
			foreach (var method in DotNetUtils.FindMethods(type.Methods, "System.String",
				         new string[] { "System.String" })) {
				if (method.Body.HasExceptionHandlers)
					continue;
				if (DotNetUtils.GetMethodCalls(method, "System.String System.String::Intern(System.String)") != 1)
					continue;
				simpleDeobfuscator.Deobfuscate(method);
				var instrs = method.Body.Instructions;
				for (int i = 0; i < instrs.Count - 3; i++) //Seartching For String Decrypt Function (that is MsIl code of function (not all))
				{
					if (!instrs[i].IsLdarg() || instrs[i].GetParameterIndex() != 0) continue;
					if (instrs[i + 1].OpCode.Code != Code.Callvirt) continue;
					if (!instrs[i + 2].IsStloc()) continue;
					if (!instrs[i + 3].IsLdloc()) continue;
					if (instrs[i + 4].OpCode.Code != Code.Newarr) continue;
					if (!instrs[i + 5].IsStloc()) continue;
					if (!instrs[i + 6].IsLdcI4()) continue;
					if (!instrs[i + 7].IsStloc()) continue;
					if (instrs[i + 8].OpCode.Code != Code.Br_S) continue;
					if (!instrs[i + 9].IsLdarg()) continue;
					if (!instrs[i + 10].IsLdloc()) continue;
					if (instrs[i + 11].OpCode.Code != Code.Callvirt)
						continue; //if you want you can continue with Il code but i think its enough
					var info = new StringDecrypterInfo(method);
					stringDecrypterMethods.Add(info.method, info);
					stringDecrypterType = method.DeclaringType; // Class Of String Decrypt function
					Logger.v("Found string decrypter method", Utils.RemoveNewlines(info.method));
					break;
				}
			}
		}

		public StringDecrypter(ModuleDefMD module) { this.module = module; }

		public string Decrypt(string str) {
			var chrArr = new char[str.Length];
			var i = 0;
			foreach (char c in str)
				chrArr[i] = char.ConvertFromUtf32((((byte)((c >> 8) ^ i) << 8) | (byte)(c ^ (chrArr.Length - i++))))[0];
			return string.Intern(new string(chrArr));
		}
	}
}
