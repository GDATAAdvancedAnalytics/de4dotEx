using System;
using System.Collections.Generic;
using System.Text;
using de4dot.blocks;
using de4dot.blocks.cflow;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
namespace de4dot.code.deobfuscators.DoubleZero {
	public class Flattening {
		private Block StartBlock, // The block with an assignment of the execution order array
					  EndBlock, // The block that is executed after all iterations of the flattening loop
					  IncrementBlock, // The block that increments the variable used for indexing the execution order array
					  IndexComparisonBlock; // The block that contains a while loop with a comparison of the execution order array index variable
		private List<int> FlatteningOrder = new List<int>(); // Extracted execution order of flattened blocks
		private Dictionary<Block, int> AllFlattenedBlocks = new Dictionary<Block, int>(); // Maps each flattened block to its execution order number
		private Dictionary<int, Block> IfConditionTargets = new Dictionary<int, Block>(); // Maps an execution order number to the
																						  // flattened block that branches from the "if (num == XX)" block
		public Flattening(Block startBlock) {
			StartBlock = startBlock;
			ExtractFlattenedBlockOrder();
			// After executing the ExtractFlattenedBlockOrder() function, the FlatteningOrder list should be filled with the execution order sequence
			if (FlatteningOrder.Count == 0) {
				// If we failed to extract the execution order, then the newarr instruction is not used for creating the execution order array
				return;
			}
			// Collect important blocks that we will use while traversing the control flow graph and reordering blocks
			IndexComparisonBlock = GetIndexComparisonBlock();
			EndBlock = GetEndBlock();
			IncrementBlock = GetIncrementBlock();
			// Recursively traverse the control flow graph to fill the AllFlattenedBlocks and IfConditionTargets dictionary
			GetFlattenedBlocksInfo();
			// Remove bogus numbers from the execution order array
			FlatteningOrder.RemoveAll(x => !IfConditionTargets.ContainsKey(x));
		}

		public bool Unflatten() {
			if (FlatteningOrder.Count == 0) {
				// We make a return here if we previously failed to extract the execution order array
				return false;
			}
			List<Block> blocksToReconnect = new List<Block>(IncrementBlock.Sources);
			Block firstBlockInOrder = IfConditionTargets[FlatteningOrder[0]];
			RemoveExecutionOrderArrayAssignment(firstBlockInOrder);
			// Connect the block with the removed assignment of the execution order variable to the first block in the execution order
			StartBlock.SetNewFallThrough(firstBlockInOrder);
			for (int i = 0; i < blocksToReconnect.Count; i++) {
				// We iterate over all sources of the block that increments the execution order index variable
				var blockToReconnect = blocksToReconnect[i];
				if (AllFlattenedBlocks.ContainsKey(blockToReconnect)) {
					var blockNumber = AllFlattenedBlocks[blockToReconnect];
					// We find the block that follows the current block in the execution order
					int blockIndexInOrder = FlatteningOrder.FindIndex(x => x == blockNumber);
					if (blockIndexInOrder != -1) {
						Block newDestinationBlock = null;
						if (blockIndexInOrder != FlatteningOrder.Count - 1) {
							int nextBlockNumberInOrder = FlatteningOrder[blockIndexInOrder + 1];
							newDestinationBlock = IfConditionTargets[nextBlockNumberInOrder];
						}
						else {
							newDestinationBlock = EndBlock;
						}
						// We connect the current block with the next block in the execution order
						ConnectFlattenedBlocks(blockToReconnect, newDestinationBlock);
					}
				}
			}
			return true;
		}
		private void RemoveExecutionOrderArrayAssignment(Block destination) {
			// Sometimes de4dot does not optimize the assignment to the execution order variable
			// Here we remove it manually by deleting the newarr instruction and the instructions after it
			var startBlocksInstrs = StartBlock.Instructions;
			var newArrIndex = startBlocksInstrs.FindIndex(instr => IsNewArrObject(instr));
			StartBlock.Instructions.RemoveRange(newArrIndex - 1, StartBlock.Instructions.Count - newArrIndex + 1);
		}
		private Block GetIndexComparisonBlock() {
			return StartBlock.FallThrough;
		}
		private Block GetEndBlock() {
			return IndexComparisonBlock.FallThrough;
		}
		private Block GetIncrementBlock() {
			foreach (var block in IndexComparisonBlock.Sources) {
				if (block != StartBlock) {
					return block;
				}
			}
			return null;
		}
		private void ExtractFlattenedBlockOrder() {
			var startBlocksInstrs = StartBlock.Instructions;
			var newArrIndex = startBlocksInstrs.FindIndex(instr => IsNewArrObject(instr));
			for (int i = newArrIndex + 1; i < startBlocksInstrs.Count; i++) {
				// Iterating over all instructions after the newarr instruction
				if (startBlocksInstrs[i].OpCode == OpCodes.Ldtoken) {
					// If the instruction is ldtoken, it most likely contains an encrypted execution order number
					var tokenOperand = (FieldDef)startBlocksInstrs[i].Operand;
					// tokenOperand contains a byte [] array with the encrypted execution order number
					FlatteningOrder.Add(ConstantsDecrypter.DecryptIntegerFromBytearray(tokenOperand.InitialValue));
				}
			}
		}
		private static bool IsNewArrObject(Instr instr) {
			return instr.OpCode == OpCodes.Newarr && instr.Operand.ToString() == "System.Object";
		}
		private void GetFlattenedBlocksInfo() {
			var BranchVarFetchBlock = IndexComparisonBlock.Targets[0];
			TraverseControlFlowGraph(BranchVarFetchBlock);
		}
		private void TraverseControlFlowGraph(Block currentBlock) {
			Block flatteningTargetBlock = null;
			int flatteningTargetBlockNumber = -1;
			if (currentBlock == null || currentBlock == IncrementBlock) {
				// If we reach the block that increments the index variable of the execution order array, we stop the recursion
				return;
			}
			switch (currentBlock.LastInstr.OpCode.Code) {
			case Code.Beq:
			case Code.Beq_S:
				// Pattern 1 from the paper
				flatteningTargetBlockNumber = currentBlock.Instructions[currentBlock.Instructions.Count - 2].GetLdcI4Value();
				flatteningTargetBlock = currentBlock.Targets[0];
				break;
			case Code.Bne_Un:
			case Code.Bne_Un_S:
				// Pattern 2 from the paper
				flatteningTargetBlockNumber = currentBlock.Instructions[currentBlock.Instructions.Count - 2].GetLdcI4Value();
				flatteningTargetBlock = currentBlock.FallThrough;
				break;
			case Code.Brfalse:
			case Code.Brfalse_S:
				// Pattern 3 from the paper
				flatteningTargetBlockNumber = 0;
				flatteningTargetBlock = currentBlock.Targets[0];
				break;
			}
			if (flatteningTargetBlockNumber != -1) {
				// Add a newly reached block to the dictionary of if condition targets and do another recursive traversal to fill in the AllFlattenedBlocks dictionary
				IfConditionTargets.Add(flatteningTargetBlockNumber, flatteningTargetBlock);
				MarkTargetBlocks(flatteningTargetBlock, flatteningTargetBlockNumber);
			}
			if (currentBlock.FallThrough != flatteningTargetBlock) {
				// Recurse into the fallthrough block of the current block
				TraverseControlFlowGraph(currentBlock.FallThrough);
			}
			if (currentBlock.Targets != null) {
				// Recurse into target blocks of the current block
				foreach (var successor in currentBlock.Targets) {
					if (successor != flatteningTargetBlock) {
						TraverseControlFlowGraph(successor);
					}
				}
			}
		}
		private void MarkTargetBlocks(Block blockToMark, int blockNumber) {
			if (blockToMark == null || blockToMark == IncrementBlock || blockToMark == StartBlock) {
				// If we reach the block that increments the index variable of the execution order array, we stop the recursion
				return;
			}
			if (AllFlattenedBlocks.ContainsKey(blockToMark)) {
				// If we already visited this block, we stop the recursion
				return;
			}
			AllFlattenedBlocks.Add(blockToMark, blockNumber);
			// Recurse into the fallthrough block of the current block
			MarkTargetBlocks(blockToMark.FallThrough, blockNumber);
			if (blockToMark.Targets != null) {
				// Recurse into targets of the current block
				foreach (var successor in blockToMark.Targets) {
					MarkTargetBlocks(successor, blockNumber);
				}
			}
			if (blockToMark.Parent is TryBlock) {
				// Recurse into the exception handler of the current block
				TryBlock parent = (TryBlock)blockToMark.Parent;
				var exceptionHandlers = parent.TryHandlerBlocks;
				foreach (var handler in exceptionHandlers) {
					MarkTargetBlocks((Block)handler.HandlerBlock.BaseBlocks[0], blockNumber);
				}
			}
		}

		private void ConnectFlattenedBlocks(Block source, Block destination) {
			if (source.FallThrough == IncrementBlock) {
				// If the current block is connected with the block that increments the execution order variable index
				// through an unconditional jump, we replace the fallthrough block
				source.SetNewFallThrough(destination);
				return;
			}
			// Otherwise, we replace one of the target blocks
			for (int i = 0; i < source.Targets.Count; i++) {
				if (source.Targets[i] == IncrementBlock) {
					source.SetNewTarget(i, destination);
					return;
				}
			}
		}

	}
}
