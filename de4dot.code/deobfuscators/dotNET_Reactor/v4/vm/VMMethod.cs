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
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace de4dot.code.deobfuscators.dotNET_Reactor.v4.vm;

internal class VMMethod {
	public int Token { get; set; }
	public List<VMInstruction> Instructions { get; } = new();
	public List<VMElemType> Locals { get; } = new();
	public List<VMExceptionHandler> ExceptionHandlers { get; } = new();

	/**
	 * Resolves numeric token operands into references for instructions like call.
	 */
	public void ResolveTokens(ModuleDefMD module) {
		foreach (var ins in Instructions) {
			switch (ins.Opcode!.Code) {
				case Code.Call:
				case Code.Callvirt:
				case Code.Ldelema:
				case Code.Ldfld:
				case Code.Stfld:
				case Code.Ldsfld:
				case Code.Stsfld:
				case Code.Ldtoken:
				case Code.Newobj:
				case Code.Newarr:
					if (ins.Operand is not int token)
						throw new Exception($"Operation mapped to {ins.Opcode} has unexpected operand {ins.Operand}");
					ins.Operand = module.ResolveToken(token);
					break;
			}
		}
	}

	public void ResolveStrings(string[] strings, ModuleDefMD module) {
		foreach (var ins in Instructions.Where(ins => ins.Opcode == OpCodes.Ldstr))
		{
			if (strings.Length != 0) {
				ins.Operand = strings[(int)ins.Operand!];
			}
			else {
				ins.Operand = module.ReadUserString((uint)(int)ins.Operand! | 0x70000000);
			}
		}
	}

	/**
	 * The VM maps all stelem variants to the same handler. This method tries to turn them
	 * back into their correct concrete opcodes.
	 */
	public void ConcretizeStelem() {
		Dictionary<int, ElementType> locTypes = new();
		// Find local variables initialized as arrays.
		for (int i = 0; i < Instructions.Count - 1; i++) {
			if (Instructions[i].Opcode == OpCodes.Newarr && Instructions[i + 1].Opcode == OpCodes.Stloc) {
				locTypes[(int)Instructions[i + 1].Operand!] = ((TypeRef)Instructions[i].Operand).ToTypeSig().ElementType;
			} else if (Instructions[i].Opcode!.Code is Code.Call or Code.Callvirt &&
			           Instructions[i + 1].Opcode == OpCodes.Stloc &&
			           Instructions[i].Operand is IMethodDefOrRef method &&
			           method.MethodSig.RetType is { IsSZArray: true } retType) {
				locTypes[(int)Instructions[i + 1].Operand!] = retType.Next.ElementType;
			}
		}
		// Find aliased assignments.
		for (int i = 1; i < Instructions.Count; i++) {
			if (Instructions[i].Opcode == OpCodes.Stloc && Instructions[i - 1].Opcode == OpCodes.Ldloc
			                                            && locTypes.TryGetValue((int)Instructions[i - 1].Operand!, out var alias)) {
				locTypes[(int)Instructions[i].Operand!] = alias;
			}
		}
		// foreach (var loc in locTypes)
		// 	Console.WriteLine($"Local {loc.Key} -> {loc.Value}");

		for (int i = 0; i < Instructions.Count; i++) {
			if (Instructions[i].Opcode == OpCodes.Stelem) {
				int arrayLoc = FindArrayLoad(i);
				if (locTypes.TryGetValue(arrayLoc, out var storeType)) {
					Instructions[i].Opcode = storeType switch {
						ElementType.I1 or ElementType.U1 => OpCodes.Stelem_I1,
						ElementType.I2 or ElementType.U2 => OpCodes.Stelem_I2,
						ElementType.I4 or ElementType.U4 => OpCodes.Stelem_I4,
						ElementType.I8 or ElementType.U8 => OpCodes.Stelem_I8,
						ElementType.I or ElementType.U => OpCodes.Stelem_I,
						ElementType.R4 => OpCodes.Stelem_R4,
						ElementType.R8 => OpCodes.Stelem_R8,
						_ => OpCodes.Stelem_Ref  // then hopefully it's some reference type array
					};
				}
				else
					throw new Exception($"Unable to find type of array (local {arrayLoc}) used for stelem");
			}
		}
	}

	private int FindArrayLoad(int iFrom) {
		int stackDebt = -3; // stelem going upwards: value, index, array
		for (int i = iFrom - 1; i >= 0; i--) {
			var ins = Instructions[i];
			switch (ins.Opcode!.StackBehaviourPop) {
				case StackBehaviour.Pop1:
					stackDebt -= 1;
					break;
				case StackBehaviour.Pop1_pop1:
				case StackBehaviour.Popref_pop1:
				case StackBehaviour.Popref_popi:
					stackDebt -= 2;
					break;
				case StackBehaviour.Pop0:
					break;
				default:
					throw new Exception($"Unhandled StackBehaviourPop of {ins.Opcode}: {ins.Opcode.StackBehaviourPop}");
			}
			switch (ins.Opcode.StackBehaviourPush) {
				case StackBehaviour.Push1:
				case StackBehaviour.Pushi:
				case StackBehaviour.Pushi8:
				case StackBehaviour.Pushr4:
				case StackBehaviour.Pushr8:
				case StackBehaviour.Pushref:
					stackDebt += 1;
					break;
				case StackBehaviour.Push1_push1:
					stackDebt += 2;
					break;
				case StackBehaviour.Push0:
					break;
				default:
					throw new Exception($"Unhandled StackBehaviourPush of {ins.Opcode}: {ins.Opcode.StackBehaviourPush}");
			}

			if (stackDebt == 0) {
				if (Instructions[i].Opcode != OpCodes.Ldloc)
					throw new Exception($"Expected ldloc for stelem array, got {Instructions[i].Opcode}");
				return (int)Instructions[i].Operand!;
			}
		}

		throw new Exception("Array load not found");
	}
}
