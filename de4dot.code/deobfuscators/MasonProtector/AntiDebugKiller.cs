using System.Collections.Generic;
using de4dot.blocks;
using de4dot.blocks.cflow;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace de4dot.code.deobfuscators.MasonProtector;

/// <summary>
/// (Applies to older versions of the protector only)<br/>
/// Removes "if (Debugger.IsAttached) Environment.Exit(-1) or Environment.FailFast("")" sequences.
/// This pass is mandatory because the presence of those instructions can otherwise hinder constant inlining when
/// they were placed in the middle of other code.
/// </summary>
class AntiDebugKiller : IBlocksDeobfuscator {
	public bool ExecuteIfNotModified => true;

	public void DeobfuscateBegin(Blocks blocks) {
	}

	public bool Deobfuscate(List<Block> allBlocks) {
		foreach (var block in allBlocks) {
			if (!block.IsConditionalBranch() || block.Instructions.Count < 2)
				continue;
			var instr = block.Instructions[block.Instructions.Count - 2];
			if (instr.OpCode != OpCodes.Call)
				continue;
			if (instr.Operand is not IMethod { FullName: "System.Boolean System.Diagnostics.Debugger::get_IsAttached()" })
				continue;

			foreach (var tBlock in block.GetTargets()) {
				if (tBlock.Instructions.Count < 2)
					continue;
				var exitCall = tBlock.Instructions[1];
				if (exitCall.OpCode != OpCodes.Call)
					continue;
				if (exitCall.Operand is not IMethod method
						|| (method.FullName != "System.Void System.Environment::Exit(System.Int32)"
						    && method.FullName != "System.Void System.Environment::FailFast(System.String)"))
					continue;
				var firstInstr = tBlock.Instructions[0];
				if (firstInstr.OpCode != OpCodes.Ldstr || (string)firstInstr.Operand != "")
					if (!firstInstr.IsLdcI4() || firstInstr.GetLdcI4Value() != -1)
						continue;

				instr.Instruction.OpCode = OpCodes.Nop; // Nop IsAttached()
				block.ReplaceBccWithBranch(tBlock == block.FallThrough);
				return true;
			}
		}

		return false;
	}
}
