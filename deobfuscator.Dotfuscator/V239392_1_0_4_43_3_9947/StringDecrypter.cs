/*
    Copyright (C) 2011-2015 de4dot@gmail.com

    This file is part of de4dot.

    de4dot is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    de4dot is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with de4dot.  If not, see <http://www.gnu.org/licenses/>.
*/

using System.Collections.Generic;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using de4dot.blocks;
using de4dot.code;
using de4dot.code.deobfuscators;

namespace de4dot.plugin.deobfuscators.Dotfuscator.V239392_1_0_4_43_3_9947 {
	class StringDecrypter : IStringDecrypter{
		ModuleDefMD module;
		MethodDefAndDeclaringTypeDict<StringDecrypterInfo> stringDecrypterMethods = new MethodDefAndDeclaringTypeDict<StringDecrypterInfo>();

		public bool Detected => stringDecrypterMethods.Count > 0;

		public IEnumerable<MethodDef> StringDecrypters {
			get {
				var list = new List<MethodDef>(stringDecrypterMethods.Count);
				foreach (var info in stringDecrypterMethods.GetValues())
					list.Add(info.method);
				return list;
			}
		}

		public IEnumerable<StringDecrypterInfo> StringDecrypterInfos => stringDecrypterMethods.GetValues();
		public StringDecrypter(ModuleDefMD module) => this.module = module;

		public void Find(ISimpleDeobfuscator simpleDeobfuscator) {
			foreach (var type in module.GetTypes())
				FindStringDecrypterMethods(type, simpleDeobfuscator);
		}

		void FindStringDecrypterMethods(TypeDef type, ISimpleDeobfuscator simpleDeobfuscator) {
			foreach (var method in DotNetUtils.FindMethods(type.Methods, "System.String", new string[] { "System.String", "System.Int32" })) {
				if (method.Body.HasExceptionHandlers)
					continue;

				if (DotNetUtils.GetMethodCalls(method, "System.Char[] System.String::ToCharArray()") != 1)
					continue;
				if (DotNetUtils.GetMethodCalls(method, "System.String System.String::Intern(System.String)") != 1)
					continue;

				simpleDeobfuscator.Deobfuscate(method);
				var instrs = method.Body.Instructions;
				for (int i = 0; i < instrs.Count - 6; i++) {
					var ldarg = instrs[i];
					if (!ldarg.IsLdarg() || ldarg.GetParameterIndex() != 0)
						continue;
					var callvirt = instrs[i + 1];
					if (callvirt.OpCode.Code != Code.Callvirt)
						continue;
					var calledMethod = callvirt.Operand as MemberRef;
					if (calledMethod == null || calledMethod.FullName != "System.Char[] System.String::ToCharArray()")
						continue;
					var stloc = instrs[i + 2];
					if (!stloc.IsStloc())
						continue;
					var ldci4 = instrs[i + 3];
					if (!ldci4.IsLdcI4())
						continue;
					var ldarg2 = instrs[i + 4];
					if (!ldarg2.IsLdarg() || ldarg2.GetParameterIndex() != 1)
						continue;
					var opAdd1 = instrs[i + 5];
					if (opAdd1.OpCode != OpCodes.Add)
						continue;

					/*
					 * internal static string d(string A_0, int A_1)
						{
							char[] array = A_0.ToCharArray();
							int num = (int)((IntPtr)(1180514709 + A_1) + (IntPtr)78 + (IntPtr)13 + (IntPtr)33 + (IntPtr)18);
							int num3;
							int num2;
							if ((num2 = (num3 = 0)) < 1)
							{
								goto IL_63;
							}
							IL_30:
							int num5;
							int num4 = num5 = num2;
							char[] array2 = array;
							int num6 = num5;
							char c = array[num5];
							byte b = (byte)((int)(c & 'ÿ') ^ num++);
							byte b2 = (byte)((int)(c >> 8) ^ num++);
							byte b3 = b2;
							b2 = b;
							b = b3;
							array2[num6] = (ushort)((int)b2 << 8 | (int)b);
							num3 = num4 + 1;
							IL_63:
							if ((num2 = num3) >= array.Length)
							{
								return string.Intern(new string(array));
							}
							goto IL_30;
						}
					 */
					/*
					 * 0	0000	ldarg.0
						1	0001	callvirt	instance char[] [mscorlib]System.String::ToCharArray()
						2	0006	stloc.0
						3	0007	ldc.i4	0x465D3995
						4	000C	ldarg.1
						5	000D	add
						6	000E	ldc.i4	0x4E
						7	0013	conv.i
						8	0014	add
						9	0015	ldc.i4	13
						10	001A	conv.i
						11	001B	add
						12	001C	ldc.i4	0x21
						13	0021	conv.i
						14	0022	add
						15	0023	ldc.i4	18
						16	0028	conv.i
						17	0029	add
						18	002A	stloc.1
						19	002B	ldc.i4.0

					 */
					int magicAdd = 0;
					int j = i + 6;
					while (j < instrs.Count - 1 && !instrs[j].IsStloc()) {
						var ldcOp = instrs[j];
						var convOp = instrs[j + 1];
						var addOp = instrs[j + 2];
						if (ldcOp.IsLdcI4() && convOp.OpCode == OpCodes.Conv_I && addOp.OpCode == OpCodes.Add) {
							magicAdd = magicAdd + ldcOp.GetLdcI4Value();
							j = j + 3;
						}
						else
							j++;
					}

					var info = new StringDecrypterInfo(method, ldci4.GetLdcI4Value() + magicAdd);
					stringDecrypterMethods.Add(info.method, info);
					Logger.v("Found string decrypter method: {0}, magic: 0x{1:X8}", Utils.RemoveNewlines(info.method), info.magic);
					break;
				}
			}
		}

		public string Decrypt(IMethod method, string encrypted, int value) {
			/*
			 * char c = array[num5];
				byte b = (byte)((int)(c & 'ÿ') ^ num++);
				byte b2 = (byte)((int)(c >> 8) ^ num++);
				byte b3 = b2;
				b2 = b;
				b = b3;
				array2[num6] = (ushort)((int)b2 << 8 | (int)b);
			 */
			var info = stringDecrypterMethods.FindAny(method);
			char[] chars = encrypted.ToCharArray();
			byte key = (byte)(info.magic + value);
			for (int i = 0; i < chars.Length; i++) {
				char c = chars[i];
				byte b1 = (byte)((byte)(c & 'ÿ') ^ key++);
				byte b2 = (byte)((byte)(c >> 8) ^ key++);
				chars[i] = (char)((b1 << 8) | b2);
			}
			return new string(chars);
		}
	}
}
