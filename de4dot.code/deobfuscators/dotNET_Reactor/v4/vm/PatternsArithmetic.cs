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

record BaseArithPattern : IPattern {
	public IList<OpCode> Pattern => new List<OpCode>
	{
		OpCodes.Ldarg_0,
		OpCodes.Ldfld,     // Stack
		OpCodes.Callvirt,  // Pop()
		OpCodes.Call,
		OpCodes.Stloc_S,
		OpCodes.Ldarg_0,
		OpCodes.Ldfld,     // Stack
		OpCodes.Callvirt,  // Pop()
		OpCodes.Call,
		OpCodes.Stloc_S,
		OpCodes.Ldloc_S,
		OpCodes.Brfalse,
		OpCodes.Ldloc_S,
		OpCodes.Brfalse,
		OpCodes.Ldarg_0,
		OpCodes.Ldfld,     // Stack
		OpCodes.Ldloc_S,
		OpCodes.Ldloc_S,
		OpCodes.Callvirt,  // [18] Add(), Sub(), etc.
		OpCodes.Callvirt,  // Push()
		OpCodes.Ret,
		OpCodes.Newobj,    // VMException ctor
		OpCodes.Throw
	};
	// ReSharper disable once InconsistentNaming
	internal const int CallIndex = 18;
}

internal record Add : BaseArithPattern, IOpcodePattern {
	record Inner : IPattern {
		public IList<OpCode> Pattern => new List<OpCode>
		{
			OpCodes.Ldarg_0,
			OpCodes.Ldflda,
			OpCodes.Ldfld,
			OpCodes.Ldarg_1,
			OpCodes.Castclass,
			OpCodes.Ldflda,
			OpCodes.Ldfld,
			OpCodes.Add,
			OpCodes.Newobj,
			OpCodes.Ret
		};
		public bool MatchAnywhere => true;
	}

	public OpCode Opcode => OpCodes.Add;

	public bool Verify(IList<Instruction> instructions)
		=> (instructions[CallIndex].Operand as MethodDef).FindPatternInOverrides(new Inner());
}

internal record AddOvf : BaseArithPattern, IOpcodePattern {
	record Inner : IPattern {
		public IList<OpCode> Pattern => new List<OpCode>
		{
			OpCodes.Ldarg_0,
			OpCodes.Ldflda,
			OpCodes.Ldfld,
			OpCodes.Ldarg_1,
			OpCodes.Castclass,
			OpCodes.Ldflda,
			OpCodes.Ldfld,
			OpCodes.Add_Ovf,
			OpCodes.Newobj,
			OpCodes.Ret
		};
		public bool MatchAnywhere => true;
	}

	public OpCode Opcode => OpCodes.Add_Ovf;

	public bool Verify(IList<Instruction> instructions)
		=> (instructions[CallIndex].Operand as MethodDef).FindPatternInOverrides(new Inner());
}

internal record AddOvfUn : BaseArithPattern, IOpcodePattern {
	record Inner : IPattern {
		public IList<OpCode> Pattern => new List<OpCode>
		{
			OpCodes.Ldarg_0,
			OpCodes.Ldflda,
			OpCodes.Ldfld,
			OpCodes.Ldarg_1,
			OpCodes.Castclass,
			OpCodes.Ldflda,
			OpCodes.Ldfld,
			OpCodes.Add_Ovf_Un,
			OpCodes.Newobj,
			OpCodes.Ret
		};
		public bool MatchAnywhere => true;
	}

	public OpCode Opcode => OpCodes.Add_Ovf_Un;

	public bool Verify(IList<Instruction> instructions)
		=> (instructions[CallIndex].Operand as MethodDef).FindPatternInOverrides(new Inner());
}

internal record Sub : BaseArithPattern, IOpcodePattern {
	record Inner : IPattern {
		public IList<OpCode> Pattern => new List<OpCode>
		{
			OpCodes.Ldarg_0,
			OpCodes.Ldflda,
			OpCodes.Ldfld,
			OpCodes.Ldarg_1,
			OpCodes.Castclass,
			OpCodes.Ldflda,
			OpCodes.Ldfld,
			OpCodes.Sub,
			OpCodes.Newobj,
			OpCodes.Ret
		};
		public bool MatchAnywhere => true;
	}

	public OpCode Opcode => OpCodes.Sub;

	public bool Verify(IList<Instruction> instructions)
		=> (instructions[CallIndex].Operand as MethodDef).FindPatternInOverrides(new Inner());
}

internal record SubOvf : IOpcodePattern {
	record Inner : IPattern {
		public IList<OpCode> Pattern => new List<OpCode>
		{
			OpCodes.Ldarg_0,
			OpCodes.Ldflda,
			OpCodes.Ldfld,
			OpCodes.Ldarg_1,
			OpCodes.Castclass,
			OpCodes.Ldflda,
			OpCodes.Ldfld,
			OpCodes.Sub_Ovf,
			OpCodes.Newobj,
			OpCodes.Ret
		};
		public bool MatchAnywhere => true;
	}

	public IList<OpCode> Pattern => new List<OpCode>
	{
		OpCodes.Ldarg_0,
		OpCodes.Ldfld,     // Stack
		OpCodes.Callvirt,  // Pop()
		OpCodes.Call,
		OpCodes.Stloc_S,
		OpCodes.Ldarg_0,
		OpCodes.Ldfld,     // Stack
		OpCodes.Callvirt,  // Pop()
		OpCodes.Castclass, // not call unlike the others
		OpCodes.Stloc_S,
		OpCodes.Ldloc_S,
		OpCodes.Brfalse,
		OpCodes.Ldloc_S,
		OpCodes.Brfalse,
		OpCodes.Ldarg_0,
		OpCodes.Ldfld,     // Stack
		OpCodes.Ldloc_S,
		OpCodes.Ldloc_S,
		OpCodes.Callvirt,  // [18] SubOvf()
		OpCodes.Callvirt,  // Push()
		OpCodes.Ret,
		OpCodes.Newobj,    // VMException ctor
		OpCodes.Throw
	};

	public OpCode Opcode => OpCodes.Sub_Ovf;

	public bool Verify(IList<Instruction> instructions)
		=> (instructions[18].Operand as MethodDef).FindPatternInOverrides(new Inner());
}

internal record SubOvfUn : BaseArithPattern, IOpcodePattern {
	record Inner : IPattern {
		public IList<OpCode> Pattern => new List<OpCode>
		{
			OpCodes.Ldarg_0,
			OpCodes.Ldflda,
			OpCodes.Ldfld,
			OpCodes.Ldarg_1,
			OpCodes.Castclass,
			OpCodes.Ldflda,
			OpCodes.Ldfld,
			OpCodes.Sub_Ovf_Un,
			OpCodes.Newobj,
			OpCodes.Ret
		};
		public bool MatchAnywhere => true;
	}

	public OpCode Opcode => OpCodes.Sub_Ovf_Un;

	public bool Verify(IList<Instruction> instructions)
		=> (instructions[CallIndex].Operand as MethodDef).FindPatternInOverrides(new Inner());
}

internal record Mul : BaseArithPattern, IOpcodePattern {
	record Inner : IPattern {
		public IList<OpCode> Pattern => new List<OpCode>
		{
			OpCodes.Ldarg_0,
			OpCodes.Ldflda,
			OpCodes.Ldfld,
			OpCodes.Ldarg_1,
			OpCodes.Castclass,
			OpCodes.Ldflda,
			OpCodes.Ldfld,
			OpCodes.Mul,
			OpCodes.Newobj,
			OpCodes.Ret
		};
		public bool MatchAnywhere => true;
	}

	public OpCode Opcode => OpCodes.Mul;

	public bool Verify(IList<Instruction> instructions)
		=> (instructions[CallIndex].Operand as MethodDef).FindPatternInOverrides(new Inner());
}

internal record MulOvf : BaseArithPattern, IOpcodePattern {
	record Inner : IPattern {
		public IList<OpCode> Pattern => new List<OpCode>
		{
			OpCodes.Ldarg_0,
			OpCodes.Ldflda,
			OpCodes.Ldfld,
			OpCodes.Ldarg_1,
			OpCodes.Castclass,
			OpCodes.Ldflda,
			OpCodes.Ldfld,
			OpCodes.Mul_Ovf,
			OpCodes.Newobj,
			OpCodes.Ret
		};
		public bool MatchAnywhere => true;
	}

	public OpCode Opcode => OpCodes.Mul_Ovf;

	public bool Verify(IList<Instruction> instructions)
		=> (instructions[CallIndex].Operand as MethodDef).FindPatternInOverrides(new Inner());
}

internal record MulOvfUn : BaseArithPattern, IOpcodePattern {
	record Inner : IPattern {
		public IList<OpCode> Pattern => new List<OpCode>
		{
			OpCodes.Ldarg_0,
			OpCodes.Ldflda,
			OpCodes.Ldfld,
			OpCodes.Ldarg_1,
			OpCodes.Castclass,
			OpCodes.Ldflda,
			OpCodes.Ldfld,
			OpCodes.Mul_Ovf_Un,
			OpCodes.Newobj,
			OpCodes.Ret
		};
		public bool MatchAnywhere => true;
	}

	public OpCode Opcode => OpCodes.Mul_Ovf_Un;

	public bool Verify(IList<Instruction> instructions)
		=> (instructions[CallIndex].Operand as MethodDef).FindPatternInOverrides(new Inner());
}

internal record Div : BaseArithPattern, IOpcodePattern {
	record Inner : IPattern {
		public IList<OpCode> Pattern => new List<OpCode>
		{
			OpCodes.Ldarg_0,
			OpCodes.Ldflda,
			OpCodes.Ldfld,
			OpCodes.Ldarg_1,
			OpCodes.Castclass,
			OpCodes.Ldflda,
			OpCodes.Ldfld,
			OpCodes.Div,
			OpCodes.Newobj,
			OpCodes.Ret
		};
		public bool MatchAnywhere => true;
	}

	public OpCode Opcode => OpCodes.Div;

	public bool Verify(IList<Instruction> instructions)
		=> (instructions[CallIndex].Operand as MethodDef).FindPatternInOverrides(new Inner());
}

internal record DivUn : BaseArithPattern, IOpcodePattern {
	record Inner : IPattern {
		public IList<OpCode> Pattern => new List<OpCode>
		{
			OpCodes.Ldarg_0,
			OpCodes.Ldflda,
			OpCodes.Ldfld,
			OpCodes.Ldarg_1,
			OpCodes.Castclass,
			OpCodes.Ldflda,
			OpCodes.Ldfld,
			OpCodes.Div_Un,
			OpCodes.Newobj,
			OpCodes.Ret
		};
		public bool MatchAnywhere => true;
	}

	public OpCode Opcode => OpCodes.Div_Un;

	public bool Verify(IList<Instruction> instructions)
		=> (instructions[CallIndex].Operand as MethodDef).FindPatternInOverrides(new Inner());
}

internal record Rem : BaseArithPattern, IOpcodePattern {
	record Inner : IPattern {
		public IList<OpCode> Pattern => new List<OpCode>
		{
			OpCodes.Ldarg_0,
			OpCodes.Ldflda,
			OpCodes.Ldfld,
			OpCodes.Ldarg_1,
			OpCodes.Castclass,
			OpCodes.Ldflda,
			OpCodes.Ldfld,
			OpCodes.Rem,
			OpCodes.Newobj,
			OpCodes.Ret
		};
		public bool MatchAnywhere => true;
	}

	public OpCode Opcode => OpCodes.Rem;

	public bool Verify(IList<Instruction> instructions)
		=> (instructions[CallIndex].Operand as MethodDef).FindPatternInOverrides(new Inner());
}

internal record RemUn : BaseArithPattern, IOpcodePattern {
	record Inner : IPattern {
		public IList<OpCode> Pattern => new List<OpCode>
		{
			OpCodes.Ldarg_0,
			OpCodes.Ldflda,
			OpCodes.Ldfld,
			OpCodes.Ldarg_1,
			OpCodes.Castclass,
			OpCodes.Ldflda,
			OpCodes.Ldfld,
			OpCodes.Rem_Un,
			OpCodes.Newobj,
			OpCodes.Ret
		};
		public bool MatchAnywhere => true;
	}

	public OpCode Opcode => OpCodes.Rem_Un;

	public bool Verify(IList<Instruction> instructions)
		=> (instructions[CallIndex].Operand as MethodDef).FindPatternInOverrides(new Inner());
}

internal record And : BaseArithPattern, IOpcodePattern {
	record Inner : IPattern {
		public IList<OpCode> Pattern => new List<OpCode>
		{
			OpCodes.Ldarg_0,
			OpCodes.Ldflda,
			OpCodes.Ldfld,
			OpCodes.Ldarg_1,
			OpCodes.Castclass,
			OpCodes.Ldflda,
			OpCodes.Ldfld,
			OpCodes.And,
			OpCodes.Newobj,
			OpCodes.Ret
		};
		public bool MatchAnywhere => true;
	}

	public OpCode Opcode => OpCodes.And;

	public bool Verify(IList<Instruction> instructions)
		=> (instructions[CallIndex].Operand as MethodDef).FindPatternInOverrides(new Inner());
}

internal record Or : BaseArithPattern, IOpcodePattern {
	record Inner : IPattern {
		public IList<OpCode> Pattern => new List<OpCode>
		{
			OpCodes.Ldarg_0,
			OpCodes.Ldflda,
			OpCodes.Ldfld,
			OpCodes.Ldarg_1,
			OpCodes.Castclass,
			OpCodes.Ldflda,
			OpCodes.Ldfld,
			OpCodes.Or,
			OpCodes.Newobj,
			OpCodes.Ret
		};
		public bool MatchAnywhere => true;
	}

	public OpCode Opcode => OpCodes.Or;

	public bool Verify(IList<Instruction> instructions)
		=> (instructions[CallIndex].Operand as MethodDef).FindPatternInOverrides(new Inner());
}

internal record Xor : BaseArithPattern, IOpcodePattern {
	record Inner : IPattern {
		public IList<OpCode> Pattern => new List<OpCode>
		{
			OpCodes.Ldarg_0,
			OpCodes.Ldflda,
			OpCodes.Ldfld,
			OpCodes.Ldarg_1,
			OpCodes.Castclass,
			OpCodes.Ldflda,
			OpCodes.Ldfld,
			OpCodes.Xor,
			OpCodes.Newobj,
			OpCodes.Ret
		};
		public bool MatchAnywhere => true;
	}

	public OpCode Opcode => OpCodes.Xor;

	public bool Verify(IList<Instruction> instructions)
		=> (instructions[CallIndex].Operand as MethodDef).FindPatternInOverrides(new Inner());
}

internal record Shl : BaseArithPattern, IOpcodePattern {
	record Inner : IPattern {
		public IList<OpCode> Pattern => new List<OpCode>
		{
			OpCodes.Ldarg_0,
			OpCodes.Ldflda,
			OpCodes.Ldfld,
			OpCodes.Ldarg_1,
			OpCodes.Castclass,
			OpCodes.Ldflda,
			OpCodes.Ldfld,
			OpCodes.Ldc_I4_S,   // ldc.i4.s 0x1F
			OpCodes.And,
			OpCodes.Shl,
			OpCodes.Newobj,
			OpCodes.Ret
		};
		public bool MatchAnywhere => true;
	}

	public OpCode Opcode => OpCodes.Shl;

	public bool Verify(IList<Instruction> instructions)
		=> (instructions[CallIndex].Operand as MethodDef).FindPatternInOverrides(new Inner());
}

internal record Shr : BaseArithPattern, IOpcodePattern {
	record Inner : IPattern {
		public IList<OpCode> Pattern => new List<OpCode>
		{
			OpCodes.Ldarg_0,
			OpCodes.Ldflda,
			OpCodes.Ldfld,
			OpCodes.Ldarg_1,
			OpCodes.Castclass,
			OpCodes.Ldflda,
			OpCodes.Ldfld,
			OpCodes.Ldc_I4_S,   // ldc.i4.s 0x1F
			OpCodes.And,
			OpCodes.Shr,
			OpCodes.Newobj,
			OpCodes.Ret
		};
		public bool MatchAnywhere => true;
	}

	public OpCode Opcode => OpCodes.Shr;

	public bool Verify(IList<Instruction> instructions)
		=> (instructions[CallIndex].Operand as MethodDef).FindPatternInOverrides(new Inner());
}

internal record ShrUn : BaseArithPattern, IOpcodePattern {
	record Inner : IPattern {
		public IList<OpCode> Pattern => new List<OpCode>
		{
			OpCodes.Ldarg_0,
			OpCodes.Ldflda,
			OpCodes.Ldfld,
			OpCodes.Ldarg_1,
			OpCodes.Castclass,
			OpCodes.Ldflda,
			OpCodes.Ldfld,
			OpCodes.Ldc_I4_S,   // ldc.i4.s 0x1F
			OpCodes.And,
			OpCodes.Shr_Un,
			OpCodes.Newobj,
			OpCodes.Ret
		};
		public bool MatchAnywhere => true;
	}

	public OpCode Opcode => OpCodes.Shr_Un;

	public bool Verify(IList<Instruction> instructions)
		=> (instructions[CallIndex].Operand as MethodDef).FindPatternInOverrides(new Inner());
}

record BaseUnaryPattern : IPattern {
	public IList<OpCode> Pattern => new List<OpCode>
	{
		OpCodes.Ldarg_0,
		OpCodes.Ldfld,
		OpCodes.Callvirt,  // Pop()
		OpCodes.Call,
		OpCodes.Stloc_S,
		OpCodes.Ldloc_S,
		OpCodes.Brfalse,
		OpCodes.Ldarg_0,
		OpCodes.Ldfld,
		OpCodes.Ldloc_S,
		OpCodes.Callvirt,  // [10] Operator()
		OpCodes.Callvirt,  // Push()
		OpCodes.Ret,
		OpCodes.Newobj,
		OpCodes.Throw
	};
	// ReSharper disable once InconsistentNaming
	internal const int CallIndex = 10;
}

internal record Neg : IOpcodePattern {
	public IList<OpCode> Pattern => new List<OpCode>
	{
		OpCodes.Ldarg_0,
		OpCodes.Ldfld,
		OpCodes.Ldarg_0,
		OpCodes.Ldfld,
		OpCodes.Callvirt,   // Pop()
		OpCodes.Castclass,
		OpCodes.Callvirt,   // Neg()
		OpCodes.Callvirt,   // Push()
		OpCodes.Ret
	};

	record Inner : IPattern {
		public IList<OpCode> Pattern => new List<OpCode>
		{
			OpCodes.Ldarg_0,
			OpCodes.Ldflda,
			OpCodes.Ldfld,
			OpCodes.Neg,
			OpCodes.Newobj,
			OpCodes.Ret
		};
		public bool MatchAnywhere => true;
	}

	public OpCode Opcode => OpCodes.Neg;

	public bool Verify(IList<Instruction> instructions)
		=> (instructions[6].Operand as MethodDef).FindPatternInOverrides(new Inner());
}

internal record Not : BaseUnaryPattern, IOpcodePattern {
	record Inner : IPattern {
		public IList<OpCode> Pattern => new List<OpCode>
		{
			OpCodes.Ldarg_0,
			OpCodes.Ldflda,
			OpCodes.Ldfld,
			OpCodes.Not,
			OpCodes.Newobj,
			OpCodes.Ret
		};
		public bool MatchAnywhere => true;
	}

	public OpCode Opcode => OpCodes.Not;

	public bool Verify(IList<Instruction> instructions)
		=> (instructions[CallIndex].Operand as MethodDef).FindPatternInOverrides(new Inner());
}
