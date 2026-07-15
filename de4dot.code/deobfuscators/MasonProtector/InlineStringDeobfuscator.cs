using System;
using System.Collections.Generic;
using System.Linq;
using de4dot.blocks;
using de4dot.blocks.cflow;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace de4dot.code.deobfuscators.MasonProtector;

public class InlineStringDeobfuscator : IBlocksDeobfuscator {
	public bool ExecuteIfNotModified => false;

	MethodDef _curMethod;

	public void DeobfuscateBegin(Blocks blocks) => _curMethod = blocks.Method;

	public bool Deobfuscate(List<Block> allBlocks) {
		bool modified = false;

		foreach (var block in allBlocks) {
			var instrs = block.Instructions;
			for (int i = 5; i < instrs.Count - 1; i++) {
				var instr = instrs[i];
				if (instr.OpCode == OpCodes.Newobj
					    && instr.Operand is IMethod { FullName: "System.Void System.String::.ctor(System.Char[])" }
					    && instrs[i + 1].OpCode == OpCodes.Call
					    && instrs[i + 1].Operand is IMethod { FullName: "System.String System.String::Intern(System.String)" })
					modified |= CheckAndProcessBlock(block, i + 1);
			}
		}

		return modified;
	}

	bool CheckAndProcessBlock(Block block, int internIndex) {
		var instrs = block.Instructions;
		int storeCount = 0, startIndex = -1;
		for (int i = internIndex - 1; i >= 0; i--) {
			var instr = instrs[i];
			if (instr.OpCode == OpCodes.Stelem_I2) {
				storeCount++;
				continue;
			}

			if (instr.OpCode != OpCodes.Newarr)
				continue;
			if (!instrs[i - 1].IsLdcI4()) {
				return false;
			}

			if (instrs[i - 1].GetLdcI4Value() != storeCount) {
				return false;
			}

			startIndex = i - 1;
			break;
		}

		if (startIndex == -1)
			return false;

		return ProcessBlock(block, startIndex, internIndex);
	}

	bool ProcessBlock(Block block, int startIndex, int internIndex) {
		var instrs = block.Instructions;
		var instrRange = instrs.GetRange(startIndex, internIndex - startIndex);

		var fields = new Dictionary<FieldDef, Value>();

		var arrayFields = instrRange
								.Where(ins => ins.OpCode == OpCodes.Ldsfld)
								.Select(ins => ins.Operand as FieldDef)
								.Where(fld => fld != null && fld.FieldType.IsSZArray)
								.Distinct()
								.ToArray();
		if (arrayFields.Length > 1) {
			Logger.w("Encountered more than one field in {0} ({1:X8})", _curMethod, _curMethod.MDToken.ToInt32());
			return false;
		}

		if (arrayFields.Length == 1) {
			char[] fieldData = GetCharArray(arrayFields[0]);
			if (fieldData == null) {
				Logger.w("Unable to obtain field data for {0} in {1} ({2:X8})", arrayFields[0], _curMethod, _curMethod.MDToken.ToInt32());
				return false;
			}
			fields[arrayFields[0]] = new ObjectValue(fieldData.Select(Value (c) => new Int32Value(c)).ToList());
		}

		var intFields = instrRange
			.Where(ins => ins.OpCode == OpCodes.Ldsfld)
			.Select(ins => ins.Operand as FieldDef)
			.Where(fld => fld != null && fld.FieldType.ElementType == ElementType.I4)
			.ToArray();
		if (intFields.Length > 0) {
			if (intFields.Any(f => f.DeclaringType != intFields[0].DeclaringType)) {
				Logger.w("Differing field declaring types");
				return false;
			}
			GrabInts(intFields[0].DeclaringType, fields);
		}

		var emu = new InstructionEmulator(_curMethod);
		for (int i = startIndex; i < internIndex; i++) {
			if (instrs[i].OpCode == OpCodes.Ldsfld) {
				if (fields.TryGetValue((FieldDef)instrs[i].Operand, out var fVal)) {
					emu.Push(fVal);
					continue;
				}
			}
			else if (instrs[i].OpCode == OpCodes.Newobj) {
				break;
			}
			emu.Emulate(instrs[i].Instruction);
		}

		var charVals = emu.Pop();
		if (charVals is not ObjectValue { obj: List<Value> values }
		    || !values.All(v => v is Int32Value i32 && i32.AllBitsValid())) {
			return false;
		}

		var str = new string(values.Select(v => (char)((Int32Value)v).Value).ToArray());
		//Console.WriteLine("[IDEC] " + str);
		block.Replace(internIndex - 1, 1, OpCodes.Pop.ToInstruction()); // newobj -> pop
		block.Replace(internIndex, 1, OpCodes.Ldstr.ToInstruction(str)); // call -> ldstr
		return true;
	}

	static char[] GetCharArray(FieldDef field) {
		var cctor = field.DeclaringType.FindStaticConstructor();
		if (cctor == null)
			return null;

		var emu = new ArrayExtractor(cctor);
		if (!emu.Run(field)) {
			return null;
		}

		return emu.GetArrayValues<char>();
	}

	static void GrabInts(TypeDef declType, Dictionary<FieldDef, Value> fieldValues) {
		var cctor = declType.FindStaticConstructor();
		if (cctor == null)
			return;

		var emu = new InstructionEmulator(cctor);
		foreach (var ins in cctor.Body.Instructions) {
			if (ins.OpCode == OpCodes.Stsfld && ins.Operand is FieldDef field
			                                 && field.FieldType.ElementType == ElementType.I4) {
				fieldValues[field] = emu.Pop();
			}
			else {
				emu.Emulate(ins);
			}
		}
	}
}
