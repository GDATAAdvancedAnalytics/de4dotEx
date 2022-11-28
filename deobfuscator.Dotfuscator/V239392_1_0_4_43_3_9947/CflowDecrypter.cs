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

namespace de4dot.plugin.deobfuscators.Dotfuscator.V239392_1_0_4_43_3_9947 {
	class CflowDecrypter {
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

			var nopIdxs = new List<int>();
			var ldlocIdxs = new List<int>();
			var instructions = method.Body.Instructions;
			GetBaseBlockFixIndexs(instructions, ref nopIdxs, ref ldlocIdxs);
			
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

			nopIdxs.Clear();
			ldlocIdxs.Clear();
			GetSwitchFixIndexs(instructions, ref nopIdxs, ref ldlocIdxs);
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

		static void GetBaseBlockFixIndexs(IList<Instruction> instructions, ref List<int> nopIdxs, ref List<int> ldlocIdxs) {
			/* obfuscated il code like this:
			 * 2	0009		ldc.i4	0x1B5E0000
				3	000E		stloc	V_14 (14)
				4	0012		ldloca	V_14 (14)
				5	0016		nop
				6	0017		ldind.i2
				7	0018		conv.i2
				8	0019		stloc.s	V_13 (13)
				9	001B	ldloc.s	V_13 (13)
				10	 001D	switch	[11 (0026)]

				or
				11	 0026		ldc.i4.0
				12	 0027		switch	[15 (006F)]

				or
				13 0030		ldloc.s	V_13 (13)
				14	 0032		switch	[817 (0A2E), 778 (09C3), 96 (014D), 752 (0986), 793 (09EE), 54 (00E4), 764 (09A5), 842 (0A72), 34 (00A5), 803 (0A08), 832 (0A55), 737 (0954), 69 (0103), 80 (0121)]
				
				or
				25	 0090		ldc.i4	0x42290008
				26	 0095		stloc	V_14 (14)
				27	 0099		ldloca	V_14 (14)
				28	 009D		nop
				29	 009E		ldind.i2
				30	 009F		conv.i2
				31 00A0		conv.i
				32 00A1		stloc.s	V_13 (13)
				33	 00A3		br.s	13 (0030) ldloc.s V_13 (13)

				or
				794	09F0		ldc.i4	0x78FA0009
				795	09F5		nop
				796	09F6		stloc	V_14 (14)
				797	09FA	ldloca	V_14 (14)
				798	09FE		ldind.i2
				799	09FF		conv.i2
				800	0A00	conv.i
				801	0A01	stloc.s	V_13 (13)
				802	0A03	br	13 (0030) ldloc.s V_13 (13)

				or
				877	0AC6	ldc.i4	0x534D0000
				878	0ACB	stloc	V_14 (14)
				879	0ACF	nop
				880	0AD0	ldloca	V_14 (14)
				881	0AD4	ldind.i2
				882	0AD5	conv.i2
				883	0AD6	br.s	875 (0AC3) pop 

				or
				460	0645		ldc.i4	0x78C40000
				461	064A	nop
				462	064B	stloc	V_14 (14)
				463	064F		nop
				464	0650		ldloca	V_14 (14)
				465	0654		ldind.i2
				466	0655		conv.i2
				467	0656		brfalse	468 (065B) call valuetype [mscorlib]System.DateTime [mscorlib]System.DateTime::get_Now()

				or
				451	0621		ldc.i4	0x6999A680
				452	0626		stloc	V_14 (14)
				453	062A	nop
				454	062B	ldloca	V_14 (14)
				455	062F		ldind.i2
				456	0630		conv.i2
				457	0631		ceq
				458	0633		switch	[397 (0581), 460 (0645), 397 (0581)]

				so i think this is the based block:
				2	0009		ldc.i4	0x1B5E0000
				3	000E		stloc	V_14 (14)
				4	0012		ldloca	V_14 (14)
				5	0016		nop
				6	0017		ldind.i2
				7	0018		conv.i2
			*/

			//remove nop
			var insNoNops = new List<Instruction>();
			foreach (var ins in instructions) {
				if (ins.OpCode != OpCodes.Nop)
					insNoNops.Add(ins);
			}

			/*find base block:
			   2	0009		ldc.i4	0x1B5E0000
			   3	000E		stloc	V_14 (14)
			   4	0012		ldloca	V_14 (14)
			   5	0016		nop
			   6	0017		ldind.i2
			   7	0018		conv.i2
			*/
			for (int i = 3; i < insNoNops.Count - 4; i++) {
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

				/*change base block:
				   2	0009		ldc.i4	0x00000000
				   3	000E		nop
				   4	0012		nop
				   5	0016		nop
				   6	0017		nop
				   7	0018		nop
				*/
				var value = ldci4.GetLdcI4Value() & 0xFFFF;
				ldci4.Operand = value;
				nopIdxs.Add(instructions.IndexOf(stloc));
				nopIdxs.Add(instructions.IndexOf(ldlocX));
				nopIdxs.Add(instructions.IndexOf(ldind));
				var convi2 = insNoNops[i + 1];
				if (ldind.OpCode == OpCodes.Ldind_I2 && convi2.OpCode == OpCodes.Conv_I2)
					nopIdxs.Add(instructions.IndexOf(convi2));
			}
		}

		static void GetSwitchFixIndexs(IList<Instruction> instructions, ref List<int> nopIdxs, ref List<int> ldlocIdxs) {
			/* obfuscated il code like this:
			 * 2	0009		ldc.i4	0x1B5E0000
				3	000E		stloc	V_14 (14)
				4	0012		ldloca	V_14 (14)
				5	0016		nop
				6	0017		ldind.i2
				7	0018		conv.i2
				8	0019		stloc.s	V_13 (13)
				9	001B	ldloc.s	V_13 (13)
				10	 001D	switch	[11 (0026)]

				or
				11	 0026		ldc.i4.0
				12	 0027		switch	[15 (006F)]

				or
				13 0030		ldloc.s	V_13 (13)
				14	 0032		switch	[817 (0A2E), 778 (09C3), 96 (014D), 752 (0986), 793 (09EE), 54 (00E4), 764 (09A5), 842 (0A72), 34 (00A5), 803 (0A08), 832 (0A55), 737 (0954), 69 (0103), 80 (0121)]

				or
				451	0621		ldc.i4	0x6999A680
				452	0626		stloc	V_14 (14)
				453	062A	nop
				454	062B	ldloca	V_14 (14)
				455	062F		ldind.i2
				456	0630		conv.i2
				457	0631		ceq
				458	0633		switch	[397 (0581), 460 (0645), 397 (0581)]
			*/

			//remove nop
			var insNoNops = new List<Instruction>();
			foreach (var ins in instructions) {
				if (ins.OpCode != OpCodes.Nop)
					insNoNops.Add(ins);
			}

			//find switch
			var insSwitch = new Dictionary<Instruction, Instruction>();
			for (int i = 0; i < insNoNops.Count - 1; i++) {
				var first = insNoNops[i];
				var sw = insNoNops[i + 1];
				if (sw.OpCode == OpCodes.Switch && (first.IsLdloc() || first.IsLdcI4() || first.OpCode==OpCodes.Ceq) ){
					if (!insSwitch.TryGetValue(first, out var target))
						insSwitch[first] = sw;
				}
			}

			/* find cases
				25   0090     ldc.i4  0x42290008
				26   0095     stloc V_14(14)
				27   0099     ldloca V_14(14)
				28   009D     nop
				29   009E      ldind.i2
				30   009F      conv.i2
				31	   00A0		 conv.i
				32   00A1		 stloc.s V_13(13)
				33   00A3		 br.s    13(0030) ldloc.s V_13(13)

				or
				794 09F0     ldc.i4  0x78FA0009
				795 09F5     nop
				796 09F6     stloc V_14(14)
				797 09FA		ldloca  V_14(14)
				798 09FE		ldind.i2
				799 09FF		conv.i2
				800 0A00		conv.i
				801 0A01		stloc.s V_13(13)
				802 0A03		br  13(0030) ldloc.s V_13(13)
				*/
			for (int i = 0; i < insNoNops.Count - 3; i++) {
				var ldc4 = insNoNops[i];
				var convi = insNoNops[i + 1];
				var stloc = insNoNops[i + 2];
				var br = insNoNops[i + 3];
				if (ldc4.IsLdcI4() && convi.OpCode == OpCodes.Conv_I && stloc.IsStloc() && br.IsBr()) {
					var value = ldc4.GetLdcI4Value();
					var target = (Instruction)br.Operand;
					if (insSwitch.TryGetValue(target, out var sw)) {
						var switchTargets = (Instruction[])sw.Operand;
						br.Operand = switchTargets[value];
						nopIdxs.Add(instructions.IndexOf(ldc4));
						nopIdxs.Add(instructions.IndexOf(stloc));
					}
					nopIdxs.Add(instructions.IndexOf(convi));
				}
			}
		}
	}
}
