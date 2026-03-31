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

internal record Br : IOpcodePattern {
	public IList<OpCode> Pattern => new List<OpCode>
	{
		OpCodes.Ldarg_0,
		OpCodes.Ldarg_0,
		OpCodes.Ldfld,  // ldfld Operand
		OpCodes.Unbox_Any,
		OpCodes.Ldc_I4_1,
		OpCodes.Sub,
		OpCodes.Stfld,  // stfld InstrPointer
		OpCodes.Ret
	};

	public OpCode Opcode => OpCodes.Br;
}

internal record Brtrue : IOpcodePattern
{
	public IList<OpCode> Pattern => new List<OpCode>
	{
		OpCodes.Ldarg_0,
		OpCodes.Ldfld,
		OpCodes.Callvirt,
		OpCodes.Stloc_S,
		OpCodes.Ldloc_S,
		OpCodes.Brfalse,
		OpCodes.Ldloc_S,
		OpCodes.Callvirt,
		OpCodes.Brfalse,
		OpCodes.Ldarg_0,
		OpCodes.Ldarg_0,
		OpCodes.Ldfld,
		OpCodes.Unbox_Any,
		OpCodes.Ldc_I4_1,
		OpCodes.Sub,
		OpCodes.Stfld,     // stfld InstrPointer
		OpCodes.Ret
	};

	public OpCode Opcode => OpCodes.Brtrue;
}

internal record Brfalse : IOpcodePattern
{
	public IList<OpCode> Pattern => new List<OpCode>
	{
		OpCodes.Ldc_I4_0,
		OpCodes.Stloc_S,
		OpCodes.Ldarg_0,
		OpCodes.Ldfld,     // ldfld Stack
		OpCodes.Callvirt,  // callvirt VMStack::Pop()
		OpCodes.Stloc_S,
		OpCodes.Ldloc_S,
		OpCodes.Brfalse,
		OpCodes.Ldloc_S,
		OpCodes.Callvirt,
		OpCodes.Ldc_I4_0,
		OpCodes.Ceq,
		OpCodes.Stloc_S,
		OpCodes.Ldloc_S,
		OpCodes.Brfalse,
		OpCodes.Ldarg_0,
		OpCodes.Ldarg_0,
		OpCodes.Ldfld,
		OpCodes.Unbox_Any,
		OpCodes.Ldc_I4_1,
		OpCodes.Sub,
		OpCodes.Stfld,
		OpCodes.Ret,
		OpCodes.Ldc_I4_1,
		OpCodes.Stloc_S
	};

	public OpCode Opcode => OpCodes.Brfalse;
}

// Used for all except beq, bne.un, bgt.un, blt.un
record CondBranchPattern : IPattern {
	public IList<OpCode> Pattern => new List<OpCode>
	{
		OpCodes.Ldarg_0,
		OpCodes.Ldfld,     // Stack
		OpCodes.Callvirt,  // Pop()
		OpCodes.Stloc_S,
		OpCodes.Ldarg_0,
		OpCodes.Ldfld,     // Stack
		OpCodes.Callvirt,  // Pop()
		OpCodes.Call,
		OpCodes.Ldloc_S,
		OpCodes.Callvirt,  // [9] Comparison()
		OpCodes.Brfalse,
		OpCodes.Ldarg_0,
		OpCodes.Ldarg_0,
		OpCodes.Ldfld,
		OpCodes.Unbox_Any, // System.Int32
		OpCodes.Ldc_I4_1,
		OpCodes.Sub,
		OpCodes.Stfld,     // InstrPointer
		OpCodes.Ret
	};
	// ReSharper disable once InconsistentNaming
	internal const int CallIndex = 9;
}

internal record Beq : IOpcodePattern {
	public IList<OpCode> Pattern => new List<OpCode>
	{
		OpCodes.Ldarg_0,
		OpCodes.Ldfld,     // Stack
		OpCodes.Callvirt,  // Pop()
		OpCodes.Ldarg_0,
		OpCodes.Ldfld,     // Stack
		OpCodes.Callvirt,  // Pop()
		OpCodes.Stloc_S,
		OpCodes.Ldloc_S,
		OpCodes.Callvirt,  // [8] Equals()
		OpCodes.Brfalse,
		OpCodes.Ldarg_0,
		OpCodes.Ldarg_0,
		OpCodes.Ldfld,
		OpCodes.Unbox_Any, // System.Int32
		OpCodes.Ldc_I4_1,
		OpCodes.Sub,
		OpCodes.Stfld,     // InstrPointer
		OpCodes.Ret
	};

	record Inner : IPattern {
		public IList<OpCode> Pattern => new List<OpCode>
		{
			OpCodes.Ldarg_0,
			OpCodes.Ldflda,
			OpCodes.Ldfld,
			OpCodes.Ldloc_S,
			OpCodes.Castclass,
			OpCodes.Ldflda,
			OpCodes.Ldfld,
			OpCodes.Ceq,
			OpCodes.Ret
		};
		public bool MatchAnywhere => true;
	}

	public OpCode Opcode => OpCodes.Beq;

	public bool Verify(IList<Instruction> instructions) =>
		(instructions[8].Operand as MethodDef).FindPatternInOverrides(new Inner());
}

internal record BneUn : IOpcodePattern {
	public IList<OpCode> Pattern => new List<OpCode>
	{
		OpCodes.Ldarg_0,
		OpCodes.Ldfld,     // Stack
		OpCodes.Callvirt,  // Pop()
		OpCodes.Ldarg_0,
		OpCodes.Ldfld,     // Stack
		OpCodes.Callvirt,  // Pop()
		OpCodes.Callvirt,  // [6] NotEquals()
		OpCodes.Brfalse,
		OpCodes.Ldarg_0,
		OpCodes.Ldarg_0,
		OpCodes.Ldfld,
		OpCodes.Unbox_Any, // System.Int32
		OpCodes.Ldc_I4_1,
		OpCodes.Sub,
		OpCodes.Stfld,     // InstrPointer
		OpCodes.Ret
	};

	record Inner : IPattern {
		public IList<OpCode> Pattern => new List<OpCode>
		{
			OpCodes.Ldloc_S,
			OpCodes.Ceq,
			OpCodes.Ldc_I4_0,
			OpCodes.Ceq,
			OpCodes.Ret
		};
		public bool MatchAnywhere => true;
	}

	public OpCode Opcode => OpCodes.Bne_Un;

	public bool Verify(IList<Instruction> instructions) =>
		(instructions[6].Operand as MethodDef).FindPatternInOverrides(new Inner());
}

internal record Bge : CondBranchPattern, IOpcodePattern {
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
			OpCodes.Clt,
			OpCodes.Ldc_I4_0,
			OpCodes.Ceq,
			OpCodes.Ret
		};
		public bool MatchAnywhere => true;
	}

	public OpCode Opcode => OpCodes.Bge;

	public bool Verify(IList<Instruction> instructions) =>
		(instructions[CallIndex].Operand as MethodDef).FindPatternInOverrides(new Inner());
}

internal record BgeUn : CondBranchPattern, IOpcodePattern {
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
			OpCodes.Clt_Un,
			OpCodes.Ldc_I4_0,
			OpCodes.Ceq,
			OpCodes.Ret
		};
		public bool MatchAnywhere => true;
	}

	public OpCode Opcode => OpCodes.Bge_Un;

	public bool Verify(IList<Instruction> instructions) =>
		(instructions[CallIndex].Operand as MethodDef).FindPatternInOverrides(new Inner());
}

internal record Bgt : CondBranchPattern, IOpcodePattern {
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
			OpCodes.Cgt,
			OpCodes.Ret
		};
		public bool MatchAnywhere => true;
	}

	public OpCode Opcode => OpCodes.Bgt;

	public bool Verify(IList<Instruction> instructions) =>
		(instructions[CallIndex].Operand as MethodDef).FindPatternInOverrides(new Inner());
}

internal record BgtUn : IOpcodePattern {  // UNSURE
	public IList<OpCode> Pattern => new List<OpCode>
	{
		OpCodes.Ldarg_0,
		OpCodes.Ldfld,
		OpCodes.Callvirt,
		OpCodes.Stloc_S,
		OpCodes.Ldloc_S,
		OpCodes.Call,
		OpCodes.Stloc_S,
		OpCodes.Ldarg_0,
		OpCodes.Ldfld,
		OpCodes.Callvirt,
		OpCodes.Stloc_S,
		OpCodes.Ldloc_S,
		OpCodes.Call,
		OpCodes.Stloc_S,
		OpCodes.Ldloc_S,
		OpCodes.Brfalse_S,
		OpCodes.Ldloc_S,
		OpCodes.Brfalse,
		OpCodes.Ldloc_S,
		OpCodes.Ldloc_S,
		OpCodes.Callvirt,   // Greater()
		OpCodes.Brfalse,
		OpCodes.Ldarg_0,
		OpCodes.Ldarg_0,
		OpCodes.Ldfld,
		OpCodes.Unbox_Any,
		OpCodes.Ldc_I4_1,
		OpCodes.Sub,
		OpCodes.Stfld,      // InstrPointer
		OpCodes.Ret,
		OpCodes.Ldloc_S,
		OpCodes.Ldloc_S,
		OpCodes.Callvirt,   // NotEquals()
		OpCodes.Brfalse,
		OpCodes.Ldarg_0,
		OpCodes.Ldarg_0,
		OpCodes.Ldfld,
		OpCodes.Unbox_Any,
		OpCodes.Ldc_I4_1,
		OpCodes.Sub,
		OpCodes.Stfld,      // InstrPointer
		OpCodes.Ret
	};

	public OpCode Opcode => OpCodes.Bgt_Un;
}

internal record Ble : CondBranchPattern, IOpcodePattern {
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
			OpCodes.Cgt,
			OpCodes.Ldc_I4_0,
			OpCodes.Ceq,
			OpCodes.Ret
		};
		public bool MatchAnywhere => true;
	}

	public OpCode Opcode => OpCodes.Ble;

	public bool Verify(IList<Instruction> instructions) =>
		(instructions[CallIndex].Operand as MethodDef).FindPatternInOverrides(new Inner());
}

internal record BleUn : CondBranchPattern, IOpcodePattern {
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
			OpCodes.Cgt_Un,
			OpCodes.Ldc_I4_0,
			OpCodes.Ceq,
			OpCodes.Ret
		};
		public bool MatchAnywhere => true;
	}

	public OpCode Opcode => OpCodes.Ble_Un;

	public bool Verify(IList<Instruction> instructions) =>
		(instructions[CallIndex].Operand as MethodDef).FindPatternInOverrides(new Inner());
}

internal record Blt : CondBranchPattern, IOpcodePattern {
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
			OpCodes.Clt,
			OpCodes.Ret
		};
		public bool MatchAnywhere => true;
	}

	public OpCode Opcode => OpCodes.Blt;

	public bool Verify(IList<Instruction> instructions) =>
		(instructions[CallIndex].Operand as MethodDef).FindPatternInOverrides(new Inner());
}

// blt.un???
