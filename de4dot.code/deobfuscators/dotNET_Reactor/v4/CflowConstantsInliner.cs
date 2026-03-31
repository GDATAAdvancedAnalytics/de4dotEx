using System.Collections.Generic;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace de4dot.code.deobfuscators.dotNET_Reactor.v4 {
	class CflowConstantsInliner {
		public TypeDef Type;

		ModuleDefMD module;
		ISimpleDeobfuscator simpleDeobfuscator;
		Dictionary<FieldDef, int> dictionary = new Dictionary<FieldDef, int>();

		public CflowConstantsInliner(ModuleDefMD module, ISimpleDeobfuscator simpleDeobfuscator) {
			this.module = module;
			this.simpleDeobfuscator = simpleDeobfuscator;
			Find();
		}

		void Find() {
			foreach (var type in module.GetTypes()) {
				if (type.IsSealed && type.HasFields) {
					if (type.Fields.Count < 100)
						continue;
					foreach (var method in type.Methods) {
						if (!method.IsStatic)
							continue;
						if (!method.IsAssembly)
							continue;
						if (!method.HasBody)
							continue;

						simpleDeobfuscator.Deobfuscate(method);

						var instrs = method.Body.Instructions;
						for (var i = 0; i < instrs.Count; i++) {
							var ldcI4 = instrs[i];
							if (!ldcI4.IsLdcI4())
								continue;
							if (i + 1 >= instrs.Count)
								continue;
							var store = instrs[i + 1];
							if (store.OpCode.Code is not (Code.Stsfld or Code.Stfld))
								continue;
							if (store.Operand is not FieldDef key)
								continue;

							dictionary[key] = ldcI4.GetLdcI4Value();
						}

						if (dictionary.Count < 100) {
							dictionary.Clear();
							continue;
						}

						Type = type;
						return;
					}
				}
			}
		}

		public void InlineAllConstants() {
			if (dictionary.Count == 0)
				return;

			foreach (var type in module.GetTypes()) {
				foreach (var method in type.Methods) {
					if (!method.HasBody)
						continue;

					var instrs = method.Body.Instructions;

					for (var i = 0; i < instrs.Count; i++) {
						bool nopNext = false;
						var load = instrs[i];
						if (load.OpCode.Code != Code.Ldsfld)
							continue;
						if (i < instrs.Count - 1 && instrs[i + 1].OpCode.Code == Code.Ldfld) {
							load = instrs[i + 1];
							nopNext = true;
						}
						if (load.Operand is not FieldDef loadField)
							continue;
						if (dictionary.TryGetValue(loadField, out var value)) {
							instrs[i] = Instruction.CreateLdcI4(value);
							if (nopNext)
								instrs[i + 1] = Instruction.Create(OpCodes.Nop);
						}
					}
				}
			}
		}
	}
}
