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

internal abstract record Ldelem : IOpcodePattern {
	public override IList<OpCode> Pattern => new List<OpCode>
	{
		OpCodes.Ldarg_0,
		OpCodes.Ldfld,
		OpCodes.Callvirt,
		OpCodes.Call,
		OpCodes.Stloc_S,
		OpCodes.Ldarg_0,
		OpCodes.Ldfld,
		OpCodes.Callvirt,
		OpCodes.Ldnull,
		OpCodes.Callvirt,
		OpCodes.Castclass,
		OpCodes.Ldloc_S,
		OpCodes.Callvirt,
		OpCodes.Ldflda,
		OpCodes.Ldfld,
		OpCodes.Callvirt,
		OpCodes.Stloc_S,
		OpCodes.Ldarg_0,
		OpCodes.Ldfld,
		OpCodes.Ldtoken,   // System.Byte etc.
		OpCodes.Call,
		OpCodes.Ldloc_S,
		OpCodes.Call,
		OpCodes.Callvirt,
		OpCodes.Ret
	};

	protected abstract string TypeName { get; }

	public override bool Verify(IList<Instruction> instructions) => ((TypeRef)instructions[19].Operand).FullName == TypeName;
}

internal record LdelemU1 : Ldelem {
	protected override string TypeName => "System.Byte";
	public override OpCode Opcode => OpCodes.Ldelem_U1;
}

internal record LdelemU2 : Ldelem {
	protected override string TypeName => "System.UInt16";
	public override OpCode Opcode => OpCodes.Ldelem_U2;
}

internal record LdelemU4 : Ldelem {
	protected override string TypeName => "System.UInt32";
	public override OpCode Opcode => OpCodes.Ldelem_U4;
}

internal record LdelemI1 : Ldelem {
	protected override string TypeName => "System.SByte";
	public override OpCode Opcode => OpCodes.Ldelem_I1;
}

internal record LdelemI2 : Ldelem {
	protected override string TypeName => "System.Int16";
	public override OpCode Opcode => OpCodes.Ldelem_I2;
}

internal record LdelemI4 : Ldelem {
	protected override string TypeName => "System.Int32";
	public override OpCode Opcode => OpCodes.Ldelem_I4;
}

internal record LdelemI8 : Ldelem {
	protected override string TypeName => "System.Int64";
	public override OpCode Opcode => OpCodes.Ldelem_I8;
}

internal record LdelemR4 : Ldelem {
	protected override string TypeName => "System.Single";
	public override OpCode Opcode => OpCodes.Ldelem_R4;
}

internal record LdelemR8 : Ldelem {
	protected override string TypeName => "System.Double";
	public override OpCode Opcode => OpCodes.Ldelem_R8;
}

internal record Ldelema : IOpcodePattern {
	public override IList<OpCode> Pattern => new List<OpCode>
	{
		/* these are for 7.0+, older versions are different
		OpCodes.Ldarg_0,
		OpCodes.Ldfld,
		OpCodes.Unbox_Any,
		OpCodes.Call,
		OpCodes.Pop,*/
		OpCodes.Ldarg_0,
		OpCodes.Ldfld,
		OpCodes.Callvirt,
		OpCodes.Call,
		OpCodes.Stloc_S,
		OpCodes.Ldarg_0,
		OpCodes.Ldfld,
		OpCodes.Callvirt,
		OpCodes.Ldnull,
		OpCodes.Callvirt,
		OpCodes.Castclass,
		OpCodes.Stloc_S,
		OpCodes.Ldarg_0,
		OpCodes.Ldfld,
		OpCodes.Ldloc_S,
		OpCodes.Callvirt,
		OpCodes.Ldflda,
		OpCodes.Ldfld,
		OpCodes.Ldloc_S,
		OpCodes.Newobj,
		OpCodes.Callvirt,
		OpCodes.Ret
	};
	public override bool MatchAnywhere => true;
	public override OpCode Opcode => OpCodes.Ldelema;
}
