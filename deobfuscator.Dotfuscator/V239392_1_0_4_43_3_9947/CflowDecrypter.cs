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

namespace de4dot.code.deobfuscators.DotfuscatorAll.V239392_1_0_4_43_3_9947 {
	class CflowDecrypter : ICflowDecrypter {
		ModuleDefMD module;

		public CflowDecrypter(ModuleDefMD module) => this.module = module;

		public void CflowClean() {
			foreach (var type in module.GetTypes()) {
				if (!type.HasMethods)
					continue;
				foreach (var method in type.Methods) {
					CleanMethod(method);
				}
			}
		}

		public void CleanMethod(MethodDef method) {
			if (!method.HasBody)
				return;
			if (!method.Body.HasInstructions)
				return;
			if (method.Body.Instructions.Count < 4)
				return;
			if (method.Body.Variables.Count == 0)
				return;
			var instructions = method.Body.Instructions;
			GetFixIndexs(instructions, out var nopIdxs, out var ldlocIdxs);
			if (nopIdxs.Count > 0) {
				foreach (var idx in nopIdxs) {
					method.Body.Instructions[idx].OpCode = OpCodes.Nop;
					method.Body.Instructions[idx].Operand = null;
				}
			}
			if (ldlocIdxs.Count > 0) {
				foreach (var idx in ldlocIdxs) {
					method.Body.Instructions[idx].OpCode = OpCodes.Ldloc;
				}
			}
		}

		static void GetFixIndexs(IList<Instruction> instructions, out List<int> nopIdxs, out List<int> ldlocIdxs) {
			var insNoNops = new List<Instruction>();
			foreach (var ins in instructions) {
				if (ins.OpCode != OpCodes.Nop)
					insNoNops.Add(ins);
			}

			nopIdxs = new List<int>();
			ldlocIdxs = new List<int>();

			/*
			 * num = (short)1689190404;
				num2 = (int)((IntPtr)num);
			 */
			/*
			 * 784	09D6	ldc.i4	0x64AF0004
				785	09DB	stloc	V_14 (14)
				786	09DF	ldloca	V_14 (14)
				787	09E3		nop
				788	09E4		ldind.i2
				789	09E5		conv.i2
				790	09E6		conv.i
				791	09E7		stloc.s	V_13 (13)
			 */
			/*	change to
			 *	784	09D6	ldc.i4	0x64AF0004
				785	09DB	nop
				786	09DF	nop
				787	09E3		nop
				788	09E4		nop
				789	09E5		nop
				790	09E6		conv.i
				791	09E7		stloc.s	V_13 (13)
			 */
			for (int i = 3; i < insNoNops.Count - 1; i++) {
				var ldind = insNoNops[i];
				if (ldind.OpCode != OpCodes.Ldind_I4 && ldind.OpCode != OpCodes.Ldind_I2)
					continue;
				var ldlocX = insNoNops[i - 1];
				if (!ldlocX.IsLdloc() && ldlocX.OpCode.Code != Code.Ldloca && ldlocX.OpCode.Code != Code.Ldloca_S)
					continue;
				var stloc = insNoNops[i - 2];
				if (!stloc.IsStloc())
					continue;
				var ldci4 = insNoNops[i - 3];
				if (!ldci4.IsLdcI4())
					continue;
				//ldlocIdxs.Add(instructions.IndexOf(ldlocX));
				ldci4.Operand = ldci4.GetLdcI4Value() & 0xFFFF;
				nopIdxs.Add(instructions.IndexOf(stloc));
				nopIdxs.Add(instructions.IndexOf(ldlocX));
				nopIdxs.Add(instructions.IndexOf(ldind));
				var convi2 = insNoNops[i + 1];
				if (ldind.OpCode == OpCodes.Ldind_I2 && convi2.OpCode == OpCodes.Conv_I2)
					nopIdxs.Add(instructions.IndexOf(convi2));
			}
		}
	}
}
