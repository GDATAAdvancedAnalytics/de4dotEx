using System;
using System.Collections.Generic;
using System.Linq;
using de4dot.blocks.cflow;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace de4dot.code.deobfuscators.MasonProtector;

public class ArrayExtractor : InstructionAndBranchEmulator {
	FieldDef _targetField;
	bool _reached;
	readonly byte[] _guidValue;

	public int InstructionIndex => InsnIndex;

	public ArrayExtractor(MethodDef method, byte[] guidValue = null) : base(method) => _guidValue = guidValue;

	/// If this method returns true, the array belonging to the specified field can be popped or obtained via GetArrayValues().
	public bool Run(FieldDef targetField, int startIndex = 0) {
		_targetField = targetField;

		Run(startIndex);

		return _reached;
	}

	protected override bool OnInstruction(Instruction instr, out bool shouldSkip) {
		shouldSkip = false;

		if (instr.OpCode == OpCodes.Stsfld && instr.Operand == _targetField) {
			_reached = true;
			return false;
		}
		if (instr.OpCode == OpCodes.Call && instr.Operand is IMethod {
			    FullName:
			    "System.Void System.Runtime.CompilerServices.RuntimeHelpers::InitializeArray(System.Array,System.RuntimeFieldHandle)"
		    }) {
			shouldSkip = true;
			var token = ((ObjectValue)Emulator.Pop()).obj as FieldDef;
			var array = ((ObjectValue)Emulator.Pop()).obj as List<Value>;
			if (token == null || array == null || !token.HasFieldRVA)
				return true;

			var elemType = ((SZArraySig)_targetField.FieldType).Next.ElementType;
			int elementSize = elemType switch {
				ElementType.I1 or ElementType.U1 => 1,
				ElementType.I2 or ElementType.Char => 2,
				ElementType.I4 => 4,
				_ => 0
			};

			if (elementSize == 0 || token.InitialValue.Length != array.Count * elementSize)
				return true;

			for (int i = 0, offset = 0; i < array.Count; i++, offset += elementSize) {
				int value = 0;
				for (int j = 0; j < elementSize; j++)
					value |= token.InitialValue[offset + j] << (j * 8);

				array[i] = new Int32Value(value);
			}
			return true;
		}
		if (_guidValue != null && instr.OpCode == OpCodes.Call
		                       && instr.Operand is IMethod { FullName: "System.Byte[] System.Guid::ToByteArray()"}) {
			shouldSkip = true;
			Emulator.Pop();
			Emulator.Push(new ObjectValue(new List<Value>(_guidValue.Select(b => new Int32Value(b)))));
		}

		return true;
	}

	public ObjectValue GetListObjectValue() {
		var ourArrayVal = Emulator.Pop();
		if (ourArrayVal is not ObjectValue { obj: List<Value> valList } objVal
				|| !valList.All(v => v is Int32Value i32 && i32.AllBitsValid())) {
			return null;
		}
		return objVal;
	}

	/// Returns the array after Run(), or null if the top stack value is not an array or does not consist of concrete values.
	public T[] GetArrayValues<T>() {
		var ourArrayVal = Emulator.Pop();
		if (ourArrayVal is not ObjectValue objVal || objVal.obj is not List<Value> valList
		                                          || !valList.All(v => v is Int32Value i32 && i32.AllBitsValid())) {
			return null;
		}
		return valList.Select(v => (T)Convert.ChangeType(((Int32Value)v).Value, typeof(T))).ToArray();
	}
}
