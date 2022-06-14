using System;
using System.Collections.Generic;
using System.Text;
using de4dot.blocks;
using de4dot.blocks.cflow;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
namespace de4dot.code.deobfuscators.DoubleZero {
	public class Unflattener : IBlocksDeobfuscator {
		public bool ExecuteIfNotModified {
			get { return false; }
		}
		public Deobfuscator Deobfuscator;
		public Unflattener(Deobfuscator deobfuscator) {
			Deobfuscator = deobfuscator;
		}
		public void DeobfuscateBegin(Blocks blocks) {
		}
		public bool Deobfuscate(List<Block> methodBlocks) {
			foreach (var block in methodBlocks) {
				if (LooksLikeStartingBlock(block)) {
					// If we detected a creation of an object [] array, we try to perform unflattening
					Flattening flattening = new Flattening(block);
					return flattening.Unflatten();

				}
			}
			return false;
		}
		private bool LooksLikeStartingBlock(Block block) {
			// We try to detect a newarr instruction that creates an object [] array
			foreach (var instr in block.Instructions) {
				if (IsNewArrObject(instr)) {
					return true;
				}
			}
			return false;
		}
		private static bool IsNewArrObject(Instr instr) {
			// Checking that the instruction instr creates an object [] array
			return instr.OpCode == OpCodes.Newarr && instr.Operand.ToString() == "System.Object";
		}
	}
}
