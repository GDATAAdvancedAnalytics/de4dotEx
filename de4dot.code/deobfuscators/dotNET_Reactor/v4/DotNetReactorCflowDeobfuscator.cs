using System.Collections.Generic;
using System.Linq;
using de4dot.blocks;
using de4dot.blocks.cflow;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace de4dot.code.deobfuscators.dotNET_Reactor.v4 {
	class DotNetReactorCflowDeobfuscator : IBlocksDeobfuscator {
		bool _hasSwitch;

		public bool ExecuteIfNotModified { get; }

		public void DeobfuscateBegin(Blocks blocks) {
			_hasSwitch = blocks.Method.Body.Instructions.Any(instr => instr.OpCode == OpCodes.Switch);
		}

		public bool Deobfuscate(List<Block> allBlocks) {
			if (!_hasSwitch)
				return false;

			var modified = false;
			foreach (var block in allBlocks) {
				var instrs = block.Instructions;
				if (instrs.Count < 2)
					continue;
				var lastInstr = block.LastInstr;
				if (!lastInstr.IsBrtrue() && !lastInstr.IsBrfalse())
					continue;
				var callIndex = instrs.IndexOf(block.LastInstr) - 1;
				var call = instrs[callIndex];
				if (call.OpCode.Code != Code.Call)
					continue;
				if (block.FallThrough == null)
					continue;
				var pop = block.FallThrough.FirstInstr;
				if (pop.OpCode.Code != Code.Pop)
					continue;
				var method = call.Operand as MethodDef;
				if (method == null)
					continue;
				var methodInstrs = method.Body.Instructions;
				if (methodInstrs.Count < 2)
					continue;

				var flag = method.ReturnType.FullName == typeof(bool).FullName && methodInstrs[methodInstrs.Count - 2].OpCode.Code != Code.Ldc_I4_0;
				block.Replace(callIndex, 1, flag ? OpCodes.Ldc_I4_1.ToInstruction() : OpCodes.Ldc_I4_0.ToInstruction());

				modified = true;
			}

			return modified;
		}
	}
}
