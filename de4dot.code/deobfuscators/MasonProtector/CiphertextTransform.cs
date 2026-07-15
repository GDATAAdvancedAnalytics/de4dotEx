using System;
using System.Collections.Generic;
using System.Linq;
using de4dot.blocks.cflow;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace de4dot.code.deobfuscators.MasonProtector;

public class CiphertextTransform {
	readonly MethodDef _method;
	readonly int _startIndex;
	readonly Dictionary<FieldDef, ObjectValue> _fields;

	public static CiphertextTransform Analyze(MethodDef method) {
		int startIndex = -1, endIndex = -1;
		Dictionary<FieldDef, ObjectValue> fields = new();

		var instrs = method.Body.Instructions;
		for (int i = 2; i < instrs.Count; i++) {
			var insn = instrs[i];
			if (insn.OpCode == OpCodes.Ldelem_U1 && instrs[i - 1].IsLdloc() && instrs[i - 2].OpCode == OpCodes.Ldarg_0) {
				startIndex = i - 2;
			}
			else if (insn.OpCode == OpCodes.Ldsfld) {
				var field = (FieldDef)insn.Operand;
				if (!field.FieldType.IsSZArray || field.FieldType.Next.ElementType != ElementType.U1)
					throw new Exception("Unexpected array field type for extra ciphertext transform");

				var ex = new ArrayExtractor(field.DeclaringType.FindStaticConstructor());
				if (!ex.Run(field))
					throw new Exception("Failed to get required field data for extra ciphertext transform");

				fields[field] = ex.GetListObjectValue();
			}
			else if (startIndex != -1 && insn.OpCode == OpCodes.Stelem_I1) {
				endIndex = i;
				break;
			}
		}

		if (endIndex == -1)
			throw new Exception("Analyzing extra ciphertext transform method failed");

		return new CiphertextTransform(method, startIndex, fields);
	}

	public CiphertextTransform(MethodDef method, int startIndex, Dictionary<FieldDef, ObjectValue> fields) {
		_method = method;
		_startIndex = startIndex;
		_fields = fields;
	}

	public void TransformArray(byte[] array) {
		var emu = new InstructionEmulator(_method);
		emu.SetArg(new Parameter(0), ByteArrayToValue(array));

		var instrs = _method.Body.Instructions;
		var local = (Local)instrs[_startIndex + 1].Operand;

		for (int i = 0; i < array.Length; i++) {
			emu.SetLocal(local, new Int32Value(i));
			for (int j = _startIndex; j < instrs.Count; j++) {
				if (instrs[j].OpCode == OpCodes.Ldsfld) {
					if (_fields.TryGetValue((FieldDef)instrs[j].Operand, out var value))
						emu.Push(value);
					else
						emu.Push(new UnknownValue());
				}
				else if (instrs[j].OpCode == OpCodes.Stelem_I1) {
					var val = emu.Pop();
					if (val is not Int32Value i32 || !i32.AllBitsValid())
						throw new Exception("Emulation didn't yield value");

					array[i] = (byte)i32.Value;
					break;
				}
				else {
					emu.Emulate(instrs[j]);
				}
			}
		}
	}

	static ObjectValue ByteArrayToValue(byte[] array)
		=> new ObjectValue(new List<Value>(array.Select(b => new Int32Value(b))));
}
