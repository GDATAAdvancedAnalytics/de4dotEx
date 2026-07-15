using System;
using System.Collections.Generic;
using System.Linq;
using de4dot.blocks;
using de4dot.blocks.cflow;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace de4dot.code.deobfuscators.MasonProtector;

public class StringDecrypter2 {
	readonly ModuleDefMD _module;
	readonly Dictionary<FieldDef, Value> _fieldData = new();

	public List<StrDecrypterInfo> StringDecrypterInfos { get; } = new();

	public StringDecrypter2(ModuleDefMD module) => _module = module;

	public void Find() {
		foreach (var type in _module.Types) {
			foreach (var method in type.Methods) {
				if (!method.IsStatic)
					continue;
				if (!DotNetUtils.IsMethod(method, "System.String", "(System.Int32,System.Int32,System.Int32,System.Int32)"))
					continue;
				if (!DotNetUtils.CallsMethod(method, "System.String System.String::Intern(System.String)"))
					continue;

				var blocks = new Blocks(method);
				var loopBody = blocks.MethodBlocks.GetAllBlocks().FirstOrDefault(
					b => b.Instructions.Count > 10 && b.Instructions.FindIndex(ins => ins.OpCode == OpCodes.Ldelem_U2) is >= 0 and < 10);
				if (loopBody?.FallThrough == null) {
					Logger.w("Proper loop body not found in decryptor method");
					continue;
				}
				if (loopBody.FallThrough.FirstInstr.OpCode != OpCodes.Ldloc) {
					Logger.w("Block following loop body does not load index Local");
					continue;
				}
				var indexLocal = (Local)loopBody.FallThrough.FirstInstr.Operand;

				var fields = loopBody.Instructions
					.Where(ins => ins.OpCode == OpCodes.Ldsfld)
					.Select(ins => (FieldDef)ins.Operand)
					.Distinct()
					.ToArray();
				if (fields.Any(f => f.FieldType.ElementType != ElementType.SZArray))
					continue;

				if (!fields.All(EnsureFieldData)) {
					continue;
				}

				var stelemIndex = loopBody.Instructions.FindIndex(ins => ins.OpCode == OpCodes.Stelem_I2);
				if (stelemIndex == -1)
					continue;

				var decryptCharInsns = loopBody.Instructions.Take(stelemIndex).Select(ins => ins.Instruction.Clone()).ToArray();
				StringDecrypterInfos.Add(new StrDecrypterInfo(method, indexLocal, decryptCharInsns));
			}
		}
	}

	/// Extracts array field data that is initialized in the declaring type's cctor.
	bool EnsureFieldData(FieldDef field) {
		if (_fieldData.ContainsKey(field))
			return true;

		var cctor = field.DeclaringType.FindStaticConstructor();
		if (cctor == null)
			return false;

		var emu = new ArrayExtractor(cctor);
		if (!emu.Run(field)) {
			Logger.w("[StrDec2] Stsfld for {0} not found in cctor", field);
			return false;
		}

		var value = emu.Emulator.Pop();
		if (value is not ObjectValue { obj: List<Value> valList }
				|| !valList.All(v => v is Int32Value i32 && i32.AllBitsValid())) {
			return false;
		}

		_fieldData[field] = value;
		return true;
	}

	public string Decrypt(StrDecrypterInfo info, int arg0, int arg1, int arg2, int arg3) {
		var emu = new InstructionEmulator(info.Method);
		int num = arg2 ^ (arg3 & 0xFFFFFF);
		emu.SetArg(new Parameter(0), new Int32Value(arg0));
		emu.SetArg(new Parameter(3), new Int32Value(arg3));
		emu.SetLocal(new Local(null, null, 0), new Int32Value((num >> 16) & 255));
		emu.SetLocal(new Local(null, null, 1), new Int32Value((num >> 8) & 255));

		char[] result = new char[arg1];
		for (int i = 0; i < result.Length; i++) {
			emu.SetLocal(info.IndexLocal, new Int32Value(i));
			foreach (var insn in info.DecryptChar) {
				if (insn.OpCode == OpCodes.Ldsfld && _fieldData.TryGetValue((FieldDef)insn.Operand, out var value))
					emu.Push(value);
				else
					emu.Emulate(insn);
			}

			var newValue = emu.Pop();
			if (newValue is not Int32Value newCharacter || !newCharacter.AllBitsValid())
				throw new Exception();
			result[i] = (char)newCharacter.Value;
		}

		//Console.WriteLine("[dec] " + new string(result));
		return new string(result);
	}

	public class StrDecrypterInfo {
		public readonly MethodDef Method;
		/// A Local that is expected to be set to the current array loop index.
		public readonly Local IndexLocal;
		/// The instructions that need to be executed in order to decrypt a single character.
		public readonly IList<Instruction> DecryptChar;

		public StrDecrypterInfo(MethodDef method, Local indexLocal, IList<Instruction> decryptChar) {
			Method = method;
			IndexLocal = indexLocal;
			DecryptChar = decryptChar;
		}
	}
}
