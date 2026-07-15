using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace de4dot.code.deobfuscators.MasonProtector;

/// <summary>
/// Removes calls to idempotent functions that essentially ignore the second argument.
/// </summary>
public static class IntShenanigans {
	// int_0 ^ ~int_1 ^ ~_int_1
	static readonly Code[] NotXor = new Code[] {
		Code.Ldarg_0, Code.Ldarg_1, Code.Not, Code.Xor, Code.Stloc_0, Code.Ldloc_0, Code.Ldarg_1, Code.Not, Code.Xor
	};
	// int_0 ^ (int_1 * 1) ^ int_1
	static readonly Code[] Mul1Xor = new Code[] {
		Code.Ldarg_0, Code.Ldarg_1, Code.Mul, Code.Xor, Code.Stloc_0, Code.Ldloc_0, Code.Ldarg_1, Code.Xor
	};
	// int_0 - int_1 + int_1
	static readonly Code[] SubAdd = new Code[] {
		Code.Ldarg_0, Code.Ldarg_1, Code.Sub, Code.Stloc_0, Code.Ldloc_0, Code.Ldarg_1, Code.Add
	};
	// int_0 + int_1 - int_1
	static readonly Code[] AddSub = new Code[] {
		Code.Ldarg_0, Code.Ldarg_1, Code.Add, Code.Stloc_0, Code.Ldloc_0, Code.Ldarg_1, Code.Sub
	};
	// int_0 + (int_1 * 0)
	static readonly Code[] Mul0Add = new Code[] {
		Code.Ldarg_0, Code.Ldarg_1, Code.Ldc_I4_0, Code.Mul, Code.Add
	};
	// int_0 ^ int_1 ^ int_1
	static readonly Code[] XorXor = new Code[] {
		Code.Ldarg_0, Code.Ldarg_1, Code.Xor, Code.Stloc_0, Code.Ldloc_0, Code.Ldarg_1, Code.Xor
	};

	static readonly Code[][] Patterns = new[] { NotXor, Mul1Xor, SubAdd, AddSub, Mul0Add, XorXor };

	static bool IsMatch(MethodDef method) {
		var instrs = method.Body.Instructions;
		foreach (var pattern in Patterns) {
			if (instrs.Count != pattern.Length + 1) {
				continue;
			}
			bool match = true;
			for (int i = 0; i < pattern.Length; i++) {
				if (instrs[i].OpCode.Code != pattern[i]) {
					match = false;
					break;
				}
			}

			if (match)
				return true;
		}

		return false;
	}

	public static void Deobfuscate(ModuleDefMD module) {
		foreach (var type in module.Types) {
			foreach (var method in type.Methods) {
				if (!method.HasBody) continue;

				var instrs = method.Body.Instructions;
				for (int i = 2; i < instrs.Count; i++) {
					var instr = instrs[i];
					if (instr.OpCode.Code != Code.Call) continue;
					if (instr.Operand is not MethodDef { IsStatic: true, HasBody: true } md || md.Parameters.Count != 2) continue;

					if (IsMatch(md) && instrs[i - 1].IsLdcI4()) {
						instrs[i].OpCode = OpCodes.Nop;
						instrs[i - 1].OpCode = OpCodes.Nop;
					}
				}
			}
		}
	}
}
