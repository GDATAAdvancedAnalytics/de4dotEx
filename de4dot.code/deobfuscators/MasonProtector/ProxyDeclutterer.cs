using System.Collections.Generic;
using System.Linq;
using de4dot.blocks;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace de4dot.code.deobfuscators.MasonProtector;

/// <summary>
/// Removes junk from proxy methods in order to make them classic inline candidates (ldarg..., call, ret).
/// </summary>
static class ProxyDeclutterer {
	public static List<MethodDef> Run(ModuleDefMD module) {
		var result = new List<MethodDef>();
		foreach (var type in module.Types) {
			foreach (var method in type.Methods) {
				if (!method.IsStatic || !method.HasBody || method.Body.Instructions.Count is < 4 or > 30)
					continue;

				if (!method.Body.Instructions[0].IsLdcI4() || method.Body.Instructions[1].OpCode != OpCodes.Call)
					continue;
				if (method.Body.Instructions[1].Operand is not IMethod {
					    FullName: "System.Void System.Diagnostics.Debug::Assert(System.Boolean)"
				    })
					continue;

				var blocks = new Blocks(method);
				var allBlocks = blocks.MethodBlocks.GetAllBlocks();

				var realBlock = allBlocks.FirstOrDefault(b =>
					b.LastInstr.OpCode == OpCodes.Ret && b.FirstInstr.OpCode.Code is Code.Ldarg_0 or Code.Call or Code.Newobj);
				if (realBlock == null)
					continue;

				if (realBlock != allBlocks[0]) {
					allBlocks[0].Remove(0, allBlocks[0].Instructions.Count);
					allBlocks[0].SetNewFallThrough(realBlock);
				}
				else {  // older protector versions only have the Assert call and no other blocks
					allBlocks[0].Remove(0, 2);
				}

				blocks.RemoveDeadBlocks();
				blocks.GetCode(out var code, out var eh);
				DotNetUtils.RestoreBody(method, code, eh);

				result.Add(method);
			}
		}

		return result;
	}
}
