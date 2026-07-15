using System;
using System.Collections.Generic;
using System.Linq;
using de4dot.blocks;
using de4dot.blocks.cflow;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace de4dot.code.deobfuscators.MasonProtector;

class ConstantsInliner : IBlocksDeobfuscator {
	readonly ModuleDefMD _module;
	readonly ISimpleDeobfuscator _simpleDeobfuscator;
	readonly Dictionary<FieldDef, int> _constFields = new();
	readonly Dictionary<FieldDef, int[]> _constArrays = new();

	readonly Dictionary<MethodDef, bool> _inlinedMethods = new();
	public IEnumerable<MethodDef> Methods => _inlinedMethods.Keys;

	public ConstantsInliner(ModuleDefMD module, ISimpleDeobfuscator simpleDeobfuscator) {
		_module = module;
		_simpleDeobfuscator = simpleDeobfuscator;
		Find();
	}

	void Find() {
		var moduleCctor = DotNetUtils.GetModuleTypeCctor(_module);
		if (moduleCctor == null)
			return;
		WalkMethod(moduleCctor, true);

		foreach (var type in _module.Types) {
			var cctor = type.FindStaticConstructor();
			if (cctor is not { HasBody: true } || cctor.Body.Instructions.Count < 20) continue;
			if (cctor.Body.Instructions[1].OpCode != OpCodes.Newarr) continue;
			var stsfld = cctor.Body.Instructions[cctor.Body.Instructions.Count - 2];
			if (stsfld.OpCode != OpCodes.Stsfld || stsfld.Operand is not FieldDef { FieldType: SZArraySig } field) continue;

			WalkClassCctor(cctor, field);
		}
	}

	void WalkMethod(MethodDef method, bool allowRecursion = false) {
		_simpleDeobfuscator.Deobfuscate(method);
		var instrs = method.Body.Instructions;
		for (var i = 0; i < instrs.Count; i++) {
			var instr = instrs[i];
			if (allowRecursion && instr.OpCode.Code == Code.Call && instr.Operand is MethodDef { IsStatic: true } methodDef) {
				WalkMethod(methodDef);
				continue;
			}

			if (instr.OpCode == OpCodes.Newarr && instrs[i - 1].IsLdcI4()) {
				int len = instrs[i - 1].GetLdcI4Value();
				int index = i;
				int[] theirInts = ArrayFinder.GetInitializedInt32Array(len, method, ref index);
				if (theirInts == null) {
					continue;
				}

				FieldDef arrayField = null;
				if (instrs[i + 1].OpCode == OpCodes.Stsfld) {
					arrayField = (FieldDef)instrs[i + 1].Operand;
				}
				else if (instrs[index + 1].OpCode == OpCodes.Stsfld) {
					index += 1;
					arrayField = (FieldDef)instrs[index].Operand;
				}

				if (arrayField != null) {
					Console.WriteLine($"Found array of len {len}, [0]={theirInts[0]}, field={arrayField}");
					_constArrays[arrayField] = theirInts;
				}
				i = index;
				continue;
			}

			if (!instr.IsLdcI4())
				continue;
			if (i + 1 >= instrs.Count)
				continue;
			var store = instrs[i + 1];
			if (store.OpCode.Code != Code.Stsfld)
				continue;
			if (store.Operand is not FieldDef key)
				continue;

			_constFields[key] = instr.GetLdcI4Value();
		}
		_simpleDeobfuscator.MethodModified(method); // flush from cache so it can be properly handled once all consts are collected
	}

	/* This is a support method for string decryption. It causes parameters to the decryption calls to become foldable. */
	void WalkClassCctor(MethodDef method, FieldDef field) {
		var emu = new ArrayExtractor(method);
		if (!emu.Run(field)) {
			return;
		}

		if (emu.GetArrayValues<int>() is { } array)
			_constArrays[field] = array;
	}

	public bool ExecuteIfNotModified => true;

	public void DeobfuscateBegin(Blocks blocks) {
	}

	public bool Deobfuscate(List<Block> allBlocks) {
		if (_constFields.Count == 0)
			return false;

		bool result = false;
		foreach (var block in allBlocks) {
			var instrs = block.Instructions;
			for (var i = 0; i < instrs.Count; i++) {
				var load = instrs[i];
				if (load.OpCode.Code == Code.Call && GetMethod((IMethod)load.Operand) is { IsStatic: true } called) {
					if (!IsFunnyMethod(called))
						continue;

					int paramCount = called.GetParamCount();
					if (i >= paramCount && Enumerable.Range(1, paramCount).All(offset => instrs[i - offset].IsLdcI4())) {
						var args = Enumerable.Range(1, paramCount)
							.Reverse()
							.Select(offset => instrs[i - offset].GetLdcI4Value())
							.ToArray();

						var callResult = new Evaluator(called, _constArrays).Run(0, args);
						if (callResult is Int32Value i32 && i32.AllBitsValid()) {
							for (int offset = 1; offset <= paramCount; offset++)
								instrs[i - offset].Instruction.OpCode = OpCodes.Nop;

							instrs[i].Instruction.OpCode = OpCodes.Ldc_I4;
							instrs[i].Instruction.Operand = i32.Value;
							_inlinedMethods[called] = true;
							result = true;
						}
						else {
							//Console.WriteLine("No value from " + called);
						}
					}
				}

				if (load.OpCode.Code != Code.Ldsfld)
					continue;
				if (load.Operand is not FieldDef loadField)
					continue;
				// Inline an int load
				if (_constFields.TryGetValue(loadField, out var value)) {
					instrs[i].Instruction.OpCode = OpCodes.Ldc_I4;
					instrs[i].Instruction.Operand = value;
					result = true;
					continue;
				}
				// Inline an array int load
				if (_constArrays.TryGetValue(loadField, out var values) && instrs[i + 1].IsLdcI4()
				                                                       && instrs[i + 2].OpCode == OpCodes.Ldelem_I4) {
					instrs[i].Instruction.OpCode = OpCodes.Ldc_I4;
					instrs[i].Instruction.Operand = values[instrs[i + 1].GetLdcI4Value()];
					instrs[i + 1].Instruction.OpCode = OpCodes.Nop;
					instrs[i + 2].Instruction.OpCode = OpCodes.Nop;
					result = true;
				}
			}
		}

		return result;
	}

	private static MethodDef GetMethod(IMethod method) {
		if (method is MethodDef md)
			return md;
		if (method is MemberRef mr)
			return mr.ResolveMethod();
		return null;
	}

	private static bool IsFunnyMethod(MethodDef method) =>
		IsTrivialMethod(method)
		|| IsTrivialIntLogic(method)
		|| IsSingleTableMethod(method)
		|| IsManyTablesMethod(method);

	/*
		return A_0;
	*/
	private static bool IsTrivialMethod(MethodDef method) =>
		method.HasBody && method.GetParamCount() == 1 &&
		method.Parameters[0].Type.ElementType == ElementType.I4 &&
		method.HasReturnType && method.Body.Instructions.Count == 2;

	/*
	 	int num = int_0 ^ ~int_1;
	    return num ^ ~int_1;
	*/
	private static bool IsTrivialIntLogic(MethodDef method) =>
		method.HasBody && method.GetParamCount() > 0 &&
		method.Parameters.All(p => p.Type.ElementType == ElementType.I4) &&
		method.HasReturnType && method.Body.Instructions.All(
			ins => ins.OpCode.FlowControl is FlowControl.Next or FlowControl.Return
							&& ins.OpCode.OpCodeType is OpCodeType.Primitive or OpCodeType.Macro);

	/*
		int num = Class32.int_0[int_6];
		int num2 = Class32.int_0[int_7];
		return ~num + num2;
	*/
	private static bool IsSingleTableMethod(MethodDef method) {
		if (!method.HasBody || method.GetParamCount() != 2) return false;

		var instrs = method.Body.Instructions;
		if (instrs.Count < 10) return false;
		for (int i = 0; i < 8; i += 4) {
			if (instrs[i].OpCode != OpCodes.Ldsfld
			    || !instrs[i + 1].IsLdarg()
			    || instrs[i + 2].OpCode != OpCodes.Ldelem_I4
			    || (!instrs[i + 3].IsStloc() && (i != 4 || !instrs[i + 3].IsLdloc())))
				return false;
		}
		return true;
	}

	/*
		int num = int_21 % 12;
		if (num == 0)
		{
			int num2 = Class31.int_4[int_22];
			return Class31.int_4[int_23] ^ ~num2;
		}
		...
	*/
	private static bool IsManyTablesMethod(MethodDef method) {
		if (!method.HasBody || method.GetParamCount() != 3) return false;

		var instrs = method.Body.Instructions;
		if (instrs.Count < 10) return false;
		return instrs[0].IsLdarg()
		       && instrs[1].IsLdcI4()
		       && instrs[2].OpCode == OpCodes.Rem
		       && instrs[3].IsStloc()
		       && instrs[4].IsLdloc()
		       && instrs[5].IsLdcI4();
	}

	private class Evaluator : InstructionAndBranchEmulator {
		readonly Dictionary<FieldDef, int[]> _constArrays;

		public Evaluator(MethodDef method, Dictionary<FieldDef, int[]> constArrays) : base(method)
			=> _constArrays = constArrays;

		protected override bool OnInstruction(Instruction instr, out bool shouldSkip) {
			shouldSkip = false;
			if (instr.OpCode == OpCodes.Ldsfld) {
				if (_constArrays.TryGetValue((FieldDef)instr.Operand, out var values)) {
					var valsList = values.Select(v => new Int32Value(v)).ToList<Value>();
					Emulator.Push(new ObjectValue(valsList));
					shouldSkip = true;
				}
			}

			return true;
		}
	}
}
