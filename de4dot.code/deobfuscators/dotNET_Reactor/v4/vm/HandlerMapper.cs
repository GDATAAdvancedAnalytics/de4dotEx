/*
    Copyright (C) 2011-2020 de4dot@gmail.com
                  2025-2026 G DATA Advanced Analytics GmbH

    This file is part of de4dotEx.

    de4dotEx is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    de4dotEx is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with de4dotEx.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using de4dot.blocks;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace de4dot.code.deobfuscators.dotNET_Reactor.v4.vm;

#nullable enable

/// <summary>
/// This class analyzes the VM logic and finds all opcode handlers.
/// In other words: It collects the code of each switch case in the opcode dispatcher.
/// </summary>
internal class HandlerMapper {
	public Dictionary<int, VMHandler> Handlers { get; } = new();

	public HandlerMapper(TypeDef vmType, ISimpleDeobfuscator deobfuscator) {
		var interpreterMethod = FindInterpreterMethod(vmType, deobfuscator);
		if (interpreterMethod == null) {
			throw new Exception("Interpreter method not found in " + vmType);
		}

		FindHandlers(interpreterMethod);
	}

	/**
	 * Finds and deobfuscates the method containing the huge opcode switch.
	 */
	private static MethodDef? FindInterpreterMethod(TypeDef vmType, ISimpleDeobfuscator deobfuscator) {
		var interpreterMethod = vmType.NestedTypes
			.Where(type => type.Methods.Count > 15 && type.Fields.Count > 10)
			.SelectMany(type => type.Methods)
			.FirstOrDefault(method =>
				!method.IsStatic
				&& method.GetParamCount() == 1
				&& method.Parameters[1].Type.DefinitionAssembly == vmType.DefinitionAssembly
				&& method.HasBody
				&& method.Body.Instructions.Count > 1500);

		if (interpreterMethod == null) {
			return null;
		}

		deobfuscator.Deobfuscate(interpreterMethod);

		return interpreterMethod;
	}

	/**
	 * Finds the opcode switch in <c>interpreterMethod</c> and collects the handling instructions for each opcode.
	 */
	private void FindHandlers(MethodDef interpreterMethod) {
		interpreterMethod = DotNetUtils.Clone(interpreterMethod);
		var insns = interpreterMethod.Body.Instructions;
		Instruction? switchIns = null;
		List<Local> trashLocals = new();
		for (int i = 0; i < insns.Count; i++) {
			if (insns[i].OpCode == OpCodes.Switch) {
				var ldloc = insns[i - 1];
				if (!ldloc.IsLdloc())
					continue;

				var switchedLocal = ldloc.GetLocal(interpreterMethod.Body.Variables);
				if (switchedLocal.Type.DefinitionAssembly == interpreterMethod.DeclaringType.DefinitionAssembly)
					switchIns = insns[i];
				else if (switchedLocal.Type.FullName == "System.Int32") {
					trashLocals.Add(switchedLocal);
				}
			}
		}
		if (switchIns == null) {
			throw new Exception("Switch instruction not found in " + interpreterMethod);
		}

		var blocks = new Blocks(interpreterMethod);
		var allBlocks = blocks.MethodBlocks.GetAllBlocks();

		// Do some cleaning and normalization.
		foreach (var target in (Instruction[])switchIns.Operand) {
			var targetBlock = allBlocks.First(b => b.Instructions.Count > 0 && b.FirstInstr.Instruction == target);
			foreach (var block in Reachable(targetBlock)) {
				CleanSpuriousCffAssignments(block, trashLocals, interpreterMethod.Body.Variables);
				if (block.IsConditionalBranch() && ShouldNormalizeBranch(block.LastInstr.OpCode)) {
					block.FlipConditionalBranch(); // NOTE: This doesn't update operands, so instruction printings can be confsuing.
				}
			}
		}
		blocks.RepartitionBlocks(); // Removes empty blocks.

		int c = 0;
		foreach (var target in (Instruction[])switchIns.Operand) {
			var targetBlock = allBlocks.First(b => b.Instructions.Count > 0 && b.FirstInstr.Instruction == target);
			//Console.WriteLine("--- case " + (c) + " ---");
			var list = new List<Instruction>();
			foreach (var block in Reachable(targetBlock)) {
				/*Console.WriteLine("{");
				foreach (var ins in block.Instructions) Console.WriteLine(ins);
				Console.WriteLine("}");*/
				list.AddRange(block.Instructions.Select(i => i.Instruction));
			}

			Handlers.Add(c++, new VMHandler(list));
			//Console.WriteLine(Handlers[c - 1]);
		}
	}

	/**
	 * Removes any control flow key assignments that are left after unflattening.
	 */
	private static void CleanSpuriousCffAssignments(Block block, List<Local> cffVars, LocalList allLocals) {
		// IL_16DE: ldc.i4 330
		// IL_16E3: stloc V_2
		for (int i = 1; i < block.Instructions.Count; i++) {
			if (block.Instructions[i].IsStloc()
				    && cffVars.Contains(block.Instructions[i].Instruction.GetLocal(allLocals))
				    && block.Instructions[i - 1].IsLdcI4()) {
				block.Remove(i - 1, 2);
				return;
			}
		}
	}

	private static List<Block> Reachable(Block block) {
		List<Block> result = new();
		TraverseBlocks(block, result);
		return result;
	}

	private static void TraverseBlocks(Block block, List<Block> found) {
		found.Add(block);
		if (block.Targets is { Count: > 1 }) {
			return; // Skip switches (remnants of flattening)
		}
		foreach (var target in block.GetTargets()) {
			if (!found.Contains(target))
				TraverseBlocks(target, found);
		}
	}

	private static bool ShouldNormalizeBranch(OpCode opcode) {
		switch (opcode.Code) {
			case Code.Bge:
			case Code.Bge_S:
			case Code.Bge_Un:
			case Code.Bge_Un_S:
			case Code.Bgt:
			case Code.Bgt_S:
			case Code.Bgt_Un:
			case Code.Bgt_Un_S:
			case Code.Brtrue:
			case Code.Brtrue_S:
				return true;
		}
		return false;
	}
}
