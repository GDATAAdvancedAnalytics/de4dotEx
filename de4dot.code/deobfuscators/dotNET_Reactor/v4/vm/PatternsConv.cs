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

using System.Collections.Generic;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace de4dot.code.deobfuscators.dotNET_Reactor.v4.vm;

abstract record ConvPattern : IOpcodePattern {
	public override IList<OpCode> Pattern => new List<OpCode>
	{
		OpCodes.Ldarg_0,
		OpCodes.Ldfld,     // Stack
		OpCodes.Callvirt,  // Pop()
		OpCodes.Call,
		OpCodes.Stloc_S,
		OpCodes.Ldloc_S,
		OpCodes.Brfalse,
		OpCodes.Ldarg_0,
		OpCodes.Ldfld,     // Stack
		OpCodes.Ldloc_S,
		OpCodes.Callvirt,  // [10] Convert()
		OpCodes.Callvirt,  // Push()
		OpCodes.Ret,
		OpCodes.Newobj,    // VMException ctor
		OpCodes.Throw
	};
	// ReSharper disable once InconsistentNaming
	internal const int CallIndex = 10;
}

internal record ConvI1 : ConvPattern {
	record Inner : IPattern {
		public override IList<OpCode> Pattern => new List<OpCode>
		{
			OpCodes.Ldarg_0,
			OpCodes.Ldfld,
			OpCodes.Conv_I1,
			OpCodes.Ldc_I4_1,
			OpCodes.Newobj,
			OpCodes.Ret
		};
		public override bool MatchAnywhere => true;
	}

	public override OpCode Opcode => OpCodes.Conv_I1;

	public override bool Verify(IList<Instruction> instructions)
		=> (instructions[CallIndex].Operand as MethodDef).FindPatternInOverridesCall(new Inner());
}

internal record ConvU1 : ConvPattern {
	record Inner : IPattern {
		public override IList<OpCode> Pattern => new List<OpCode>
		{
			OpCodes.Ldarg_0,
			OpCodes.Ldfld,
			OpCodes.Conv_U1,
			OpCodes.Ldc_I4_2,
			OpCodes.Newobj,
			OpCodes.Ret
		};
		public override bool MatchAnywhere => true;
	}

	public override OpCode Opcode => OpCodes.Conv_U1;

	public override bool Verify(IList<Instruction> instructions)
		=> (instructions[CallIndex].Operand as MethodDef).FindPatternInOverridesCall(new Inner());
}

internal record ConvI2 : ConvPattern {
	record Inner : IPattern {
		public override IList<OpCode> Pattern => new List<OpCode>
		{
			OpCodes.Ldarg_0,
			OpCodes.Ldfld,
			OpCodes.Conv_I2,
			OpCodes.Ldc_I4_3,
			OpCodes.Newobj,
			OpCodes.Ret
		};
		public override bool MatchAnywhere => true;
	}

	public override OpCode Opcode => OpCodes.Conv_I2;

	public override bool Verify(IList<Instruction> instructions)
		=> (instructions[CallIndex].Operand as MethodDef).FindPatternInOverridesCall(new Inner());
}

internal record ConvU2 : ConvPattern {
	record Inner : IPattern {
		public override IList<OpCode> Pattern => new List<OpCode>
		{
			OpCodes.Ldarg_0,
			OpCodes.Ldfld,
			OpCodes.Conv_U2,
			OpCodes.Ldc_I4_4,
			OpCodes.Newobj,
			OpCodes.Ret
		};
		public override bool MatchAnywhere => true;
	}

	public override OpCode Opcode => OpCodes.Conv_U2;

	public override bool Verify(IList<Instruction> instructions)
		=> (instructions[CallIndex].Operand as MethodDef).FindPatternInOverridesCall(new Inner());
}

internal record ConvI4 : ConvPattern {
	record Inner : IPattern {
		public override IList<OpCode> Pattern => new List<OpCode>
		{
			OpCodes.Ldarg_0,
			OpCodes.Ldfld,
			OpCodes.Conv_I4,
			OpCodes.Ldc_I4_5,
			OpCodes.Newobj,
			OpCodes.Ret
		};
		public override bool MatchAnywhere => true;
	}

	public override OpCode Opcode => OpCodes.Conv_I4;

	public override bool Verify(IList<Instruction> instructions)
		=> (instructions[CallIndex].Operand as MethodDef).FindPatternInOverridesCall(new Inner());
}

internal record ConvU4 : ConvPattern {
	record Inner : IPattern {
		public override IList<OpCode> Pattern => new List<OpCode>
		{
			OpCodes.Ldarg_0,
			OpCodes.Ldfld,
			OpCodes.Conv_U4,
			OpCodes.Ldc_I4_6,
			OpCodes.Newobj,
			OpCodes.Ret
		};
		public override bool MatchAnywhere => true;
	}

	public override OpCode Opcode => OpCodes.Conv_U4;

	public override bool Verify(IList<Instruction> instructions)
		=> (instructions[CallIndex].Operand as MethodDef).FindPatternInOverridesCall(new Inner());
}

internal record ConvI8 : ConvPattern {
	record Inner : IPattern {
		public override IList<OpCode> Pattern => new List<OpCode>
		{
			OpCodes.Ldarg_0,
			OpCodes.Ldfld,
			OpCodes.Conv_I8,
			OpCodes.Ldc_I4_7,
			OpCodes.Newobj,
			OpCodes.Ret
		};
		public override bool MatchAnywhere => true;
	}

	public override OpCode Opcode => OpCodes.Conv_I8;

	public override bool Verify(IList<Instruction> instructions)
		=> (instructions[CallIndex].Operand as MethodDef).FindPatternInOverridesCall(new Inner());
}

internal record ConvU8 : ConvPattern {
	record Inner : IPattern {
		public override IList<OpCode> Pattern => new List<OpCode>
		{
			OpCodes.Ldarg_0,
			OpCodes.Ldfld,
			OpCodes.Conv_U8,
			OpCodes.Ldc_I4_8,
			OpCodes.Newobj,
			OpCodes.Ret
		};
		public override bool MatchAnywhere => true;
	}

	public override OpCode Opcode => OpCodes.Conv_U8;

	public override bool Verify(IList<Instruction> instructions)
		=> (instructions[CallIndex].Operand as MethodDef).FindPatternInOverridesCall(new Inner());
}

internal record ConvR4 : ConvPattern {
	record Inner : IPattern {
		public override IList<OpCode> Pattern => new List<OpCode>
		{
			OpCodes.Ldarg_0,
			OpCodes.Ldflda,
			OpCodes.Ldfld,
			OpCodes.Conv_R4,
			OpCodes.Newobj,
			OpCodes.Ret
		};
		public override bool MatchAnywhere => true;
	}

	public override OpCode Opcode => OpCodes.Conv_R4;

	public override bool Verify(IList<Instruction> instructions)
		=> (instructions[CallIndex].Operand as MethodDef).FindPatternInOverridesCall(new Inner());
}

internal record ConvR8 : ConvPattern {
	record Inner : IPattern {
		public override IList<OpCode> Pattern => new List<OpCode>
		{
			OpCodes.Ldarg_0,
			OpCodes.Ldflda,
			OpCodes.Ldfld,
			OpCodes.Conv_R8,
			OpCodes.Newobj,
			OpCodes.Ret
		};
		public override bool MatchAnywhere => true;
	}

	public override OpCode Opcode => OpCodes.Conv_R8;

	public override bool Verify(IList<Instruction> instructions) {

		return (instructions[CallIndex].Operand as MethodDef).FindPatternInOverridesCall(new Inner());
	}
}

internal record ConvRUn : ConvPattern {
	record Inner : IPattern {
		public override IList<OpCode> Pattern => new List<OpCode>
		{
			OpCodes.Ldarg_0,
			OpCodes.Ldflda,
			OpCodes.Ldfld,
			OpCodes.Conv_R_Un,
			OpCodes.Conv_R8,
			OpCodes.Newobj,
			OpCodes.Ret
		};
		public override bool MatchAnywhere => true;
	}

	public override OpCode Opcode => OpCodes.Conv_R_Un;

	public override bool Verify(IList<Instruction> instructions)
		=> (instructions[CallIndex].Operand as MethodDef).FindPatternInOverridesCall(new Inner());
}

internal record ConvOvfI1 : ConvPattern {
	record Inner : IPattern {
		public override IList<OpCode> Pattern => new List<OpCode>
		{
			OpCodes.Ldarg_0,
			OpCodes.Ldflda,
			OpCodes.Ldfld,
			OpCodes.Conv_Ovf_I1,
			OpCodes.Ldc_I4_1,
			OpCodes.Newobj,
			OpCodes.Ret
		};
		public override bool MatchAnywhere => true;
	}

	public override OpCode Opcode => OpCodes.Conv_Ovf_I1;

	public override bool Verify(IList<Instruction> instructions)
		=> (instructions[CallIndex].Operand as MethodDef).FindPatternInOverridesCall(new Inner());
}

internal record ConvOvfI1Un : ConvPattern {
	record Inner : IPattern {
		public override IList<OpCode> Pattern => new List<OpCode>
		{
			OpCodes.Ldarg_0,
			OpCodes.Ldflda,
			OpCodes.Ldfld,
			OpCodes.Conv_Ovf_I1_Un,
			OpCodes.Ldc_I4_1,
			OpCodes.Newobj,
			OpCodes.Ret
		};
		public override bool MatchAnywhere => true;
	}

	public override OpCode Opcode => OpCodes.Conv_Ovf_I1_Un;

	public override bool Verify(IList<Instruction> instructions)
		=> (instructions[CallIndex].Operand as MethodDef).FindPatternInOverridesCall(new Inner());
}

internal record ConvOvfU1 : ConvPattern {
	record Inner : IPattern {
		public override IList<OpCode> Pattern => new List<OpCode>
		{
			OpCodes.Ldarg_0,
			OpCodes.Ldflda,
			OpCodes.Ldfld,
			OpCodes.Conv_Ovf_U1,
			OpCodes.Ldc_I4_2,
			OpCodes.Newobj,
			OpCodes.Ret
		};
		public override bool MatchAnywhere => true;
	}

	public override OpCode Opcode => OpCodes.Conv_Ovf_U1;

	public override bool Verify(IList<Instruction> instructions)
		=> (instructions[CallIndex].Operand as MethodDef).FindPatternInOverridesCall(new Inner());
}

internal record ConvOvfU1Un : ConvPattern {
	record Inner : IPattern {
		public override IList<OpCode> Pattern => new List<OpCode>
		{
			OpCodes.Ldarg_0,
			OpCodes.Ldflda,
			OpCodes.Ldfld,
			OpCodes.Conv_Ovf_U1_Un,
			OpCodes.Ldc_I4_2,
			OpCodes.Newobj,
			OpCodes.Ret
		};
		public override bool MatchAnywhere => true;
	}

	public override OpCode Opcode => OpCodes.Conv_Ovf_U1_Un;

	public override bool Verify(IList<Instruction> instructions)
		=> (instructions[CallIndex].Operand as MethodDef).FindPatternInOverridesCall(new Inner());
}

internal record ConvOvfI2 : ConvPattern {
	record Inner : IPattern {
		public override IList<OpCode> Pattern => new List<OpCode>
		{
			OpCodes.Ldarg_0,
			OpCodes.Ldflda,
			OpCodes.Ldfld,
			OpCodes.Conv_Ovf_I2,
			OpCodes.Ldc_I4_3,
			OpCodes.Newobj,
			OpCodes.Ret
		};
		public override bool MatchAnywhere => true;
	}

	public override OpCode Opcode => OpCodes.Conv_Ovf_I2;

	public override bool Verify(IList<Instruction> instructions)
		=> (instructions[CallIndex].Operand as MethodDef).FindPatternInOverridesCall(new Inner());
}

internal record ConvOvfI2Un : ConvPattern {
	record Inner : IPattern {
		public override IList<OpCode> Pattern => new List<OpCode>
		{
			OpCodes.Ldarg_0,
			OpCodes.Ldflda,
			OpCodes.Ldfld,
			OpCodes.Conv_Ovf_I2_Un,
			OpCodes.Ldc_I4_3,
			OpCodes.Newobj,
			OpCodes.Ret
		};
		public override bool MatchAnywhere => true;
	}

	public override OpCode Opcode => OpCodes.Conv_Ovf_I2_Un;

	public override bool Verify(IList<Instruction> instructions)
		=> (instructions[CallIndex].Operand as MethodDef).FindPatternInOverridesCall(new Inner());
}

internal record ConvOvfU2 : ConvPattern {
	record Inner : IPattern {
		public override IList<OpCode> Pattern => new List<OpCode>
		{
			OpCodes.Ldarg_0,
			OpCodes.Ldflda,
			OpCodes.Ldfld,
			OpCodes.Conv_Ovf_U2,
			OpCodes.Ldc_I4_4,
			OpCodes.Newobj,
			OpCodes.Ret
		};
		public override bool MatchAnywhere => true;
	}

	public override OpCode Opcode => OpCodes.Conv_Ovf_U2;

	public override bool Verify(IList<Instruction> instructions)
		=> (instructions[CallIndex].Operand as MethodDef).FindPatternInOverridesCall(new Inner());
}

internal record ConvOvfU2Un : ConvPattern {
	record Inner : IPattern {
		public override IList<OpCode> Pattern => new List<OpCode>
		{
			OpCodes.Ldarg_0,
			OpCodes.Ldflda,
			OpCodes.Ldfld,
			OpCodes.Conv_Ovf_U2_Un,
			OpCodes.Ldc_I4_4,
			OpCodes.Newobj,
			OpCodes.Ret
		};
		public override bool MatchAnywhere => true;
	}

	public override OpCode Opcode => OpCodes.Conv_Ovf_U2_Un;

	public override bool Verify(IList<Instruction> instructions)
		=> (instructions[CallIndex].Operand as MethodDef).FindPatternInOverridesCall(new Inner());
}

internal record ConvOvfI4 : ConvPattern {
	record Inner : IPattern {
		public override IList<OpCode> Pattern => new List<OpCode>
		{
			OpCodes.Ldarg_0,
			OpCodes.Ldflda,
			OpCodes.Ldfld,
			OpCodes.Conv_Ovf_I4,
			OpCodes.Ldc_I4_5,
			OpCodes.Newobj,
			OpCodes.Ret
		};
		public override bool MatchAnywhere => true;
	}

	public override OpCode Opcode => OpCodes.Conv_Ovf_I4;

	public override bool Verify(IList<Instruction> instructions)
		=> (instructions[CallIndex].Operand as MethodDef).FindPatternInOverridesCall(new Inner());
}

internal record ConvOvfI4Un : ConvPattern {
	record Inner : IPattern {
		public override IList<OpCode> Pattern => new List<OpCode>
		{
			OpCodes.Ldarg_0,
			OpCodes.Ldflda,
			OpCodes.Ldfld,
			OpCodes.Conv_Ovf_I4_Un,
			OpCodes.Ldc_I4_5,
			OpCodes.Newobj,
			OpCodes.Ret
		};
		public override bool MatchAnywhere => true;
	}

	public override OpCode Opcode => OpCodes.Conv_Ovf_I4_Un;

	public override bool Verify(IList<Instruction> instructions)
		=> (instructions[CallIndex].Operand as MethodDef).FindPatternInOverridesCall(new Inner());
}

internal record ConvOvfU4 : ConvPattern {
	record Inner : IPattern {
		public override IList<OpCode> Pattern => new List<OpCode>
		{
			OpCodes.Ldarg_0,
			OpCodes.Ldflda,
			OpCodes.Ldfld,
			OpCodes.Conv_Ovf_U4,
			OpCodes.Ldc_I4_6,
			OpCodes.Newobj,
			OpCodes.Ret
		};
		public override bool MatchAnywhere => true;
	}

	public override OpCode Opcode => OpCodes.Conv_Ovf_U4;

	public override bool Verify(IList<Instruction> instructions)
		=> (instructions[CallIndex].Operand as MethodDef).FindPatternInOverridesCall(new Inner());
}

internal record ConvOvfU4Un : ConvPattern {
	record Inner : IPattern {
		public override IList<OpCode> Pattern => new List<OpCode>
		{
			OpCodes.Ldarg_0,
			OpCodes.Ldflda,
			OpCodes.Ldfld,
			OpCodes.Conv_Ovf_U4_Un,
			OpCodes.Ldc_I4_6,
			OpCodes.Newobj,
			OpCodes.Ret
		};
		public override bool MatchAnywhere => true;
	}

	public override OpCode Opcode => OpCodes.Conv_Ovf_U4_Un;

	public override bool Verify(IList<Instruction> instructions)
		=> (instructions[CallIndex].Operand as MethodDef).FindPatternInOverridesCall(new Inner());
}

internal record ConvOvfU8 : ConvPattern {
	record Inner : IPattern {
		public override IList<OpCode> Pattern => new List<OpCode>
		{
			OpCodes.Ldarg_0,
			OpCodes.Ldflda,
			OpCodes.Ldfld,
			OpCodes.Conv_Ovf_U8,
			OpCodes.Ldc_I4_8,
			OpCodes.Newobj,
			OpCodes.Ret
		};
		public override bool MatchAnywhere => true;
	}

	public override OpCode Opcode => OpCodes.Conv_Ovf_U8;

	public override bool Verify(IList<Instruction> instructions)
		=> (instructions[CallIndex].Operand as MethodDef).FindPatternInOverridesCall(new Inner());
}

// conv.ovf.i8, conv.ovf.i8.un, conv.ovf.u8.un are tricky
