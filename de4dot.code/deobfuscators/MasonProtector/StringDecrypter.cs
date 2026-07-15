using System;
using System.Collections.Generic;
using de4dot.blocks;
using de4dot.blocks.cflow;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace de4dot.code.deobfuscators.MasonProtector;

public class StringDecrypter {
	readonly ModuleDefMD _module;

	public List<StrDecrypterInfo> StringDecrypterInfos { get; } = new();

	public StringDecrypter(ModuleDefMD module) {
		_module = module;
	}

	public void Find() {
		foreach (var type in _module.Types) {
			foreach (var method in type.Methods) {
				if (!method.IsStatic)
					continue;
				if (!DotNetUtils.IsMethod(method, "System.String", "(System.String,System.String,System.Int32)"))
					continue;
				if (!DotNetUtils.CallsMethod(method, "System.Void System.String::.ctor(System.Char[])"))
					continue;

				var instrs = method.Body.Instructions;
				int startIndex = -1, endIndex = -1;
				for (int i = 0; i < instrs.Count - 10; i++) {
					var instr = instrs[i];
					if (instr.OpCode != OpCodes.Callvirt || ((IMethod)instr.Operand).FullName != "System.Char System.String::get_Chars(System.Int32)")
						continue;

					// ld new array (for stelem ~10 insns down)
					// ld index     (for stelem ~10 insns down)
					// ld current char
					// ld num = (int_4 >> 16) & 255
					if (!instrs[i + 2].IsLdloc() || !instrs[i + 3].IsLdloc() || !instrs[i + 4].IsLdloc()
							|| !instrs[i + 5].IsLdloc()) {
						Console.WriteLine("Instruction mismatch in strdec");
						continue;
					}

					startIndex = i + 2;
					for (int j = startIndex; j < instrs.Count; j++) {
						if (instrs[j].OpCode == OpCodes.Stelem_I2) {
							endIndex = j;
							break;
						}
					}

					if (endIndex != -1)
						break;
				}

				if (endIndex != -1) {
					var decryptCharInsns = new List<Instruction>();
					for (int j = startIndex; j < endIndex; j++)
						decryptCharInsns.Add(instrs[j].Clone());

					StringDecrypterInfos.Add(new StrDecrypterInfo(method, decryptCharInsns));
				}
			}
		}
	}

	public string Decrypt(StrDecrypterInfo info, string arg1, string arg2, int arg3) {
		var emu = new InstructionEmulator(info.Method);
		emu.SetLocal(new Local(null, null, 1), new Int32Value((arg3 >> 16) & 255));
		emu.SetLocal(new Local(null, null, 2), new Int32Value((arg3 >> 8) & 255));

		var localI = (Local)info.DecryptChar[1].Operand;
		var localC = (Local)info.DecryptChar[2].Operand;
		string text = arg1 + arg2;
		char[] result = new char[text.Length];
		for (int i = 0; i < text.Length; i++) {
			emu.SetLocal(localI, new Int32Value(i));
			emu.SetLocal(localC, new Int32Value(text[i]));
			foreach (var insn in info.DecryptChar)
				emu.Emulate(insn);
			var newValue = emu.Pop();
			if (newValue is not Int32Value newCharacter || !newCharacter.AllBitsValid())
				throw new Exception();
			result[i] = (char)newCharacter.Value;
		}

		return new string(result);
	}

	public class StrDecrypterInfo {
		public readonly MethodDef Method;
		public readonly List<Instruction> DecryptChar;

		public StrDecrypterInfo(MethodDef method, List<Instruction> decryptChar) {
			Method = method;
			DecryptChar = decryptChar;
		}
	}
}
