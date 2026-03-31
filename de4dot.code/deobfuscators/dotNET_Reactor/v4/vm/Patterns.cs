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

// Note when reading these patterns: They are often not 1:1 what you find in the assembly.
// HandlerMapper applies normalization to guarantee matches even if blocks are laid out differently.

internal record Callvirt : IOpcodePattern {
	public IList<OpCode> Pattern => new List<OpCode>
	{
		OpCodes.Ldarg_0,
		OpCodes.Ldc_I4_0,
		OpCodes.Call,  // call System.Void VM/VMExecution::CallOperandMethod(System.Boolean)
		OpCodes.Ret
	};

	public OpCode Opcode => OpCodes.Callvirt;
}

internal record Call : IOpcodePattern {
	public IList<OpCode> Pattern => new List<OpCode>
	{
		OpCodes.Ldarg_0,
		OpCodes.Ldc_I4_1,
		OpCodes.Call,  // call System.Void VM/VMExecution::CallOperandMethod(System.Boolean)
		OpCodes.Ret
	};

	public OpCode Opcode => OpCodes.Call;
}

internal record Ceq : IOpcodePattern {
	public IList<OpCode> Pattern => new List<OpCode>
	{
		OpCodes.Ldarg_0,
		OpCodes.Ldfld,
		OpCodes.Callvirt,
		OpCodes.Call,
		OpCodes.Ldarg_0,
		OpCodes.Ldfld,
		OpCodes.Callvirt,
		OpCodes.Call,
		OpCodes.Stloc_S,
		OpCodes.Ldloc_S,
		OpCodes.Callvirt,
		OpCodes.Brfalse,
		OpCodes.Ldarg_0,
		OpCodes.Ldfld,
		OpCodes.Ldc_I4_1,
		OpCodes.Newobj,
		OpCodes.Callvirt,
		OpCodes.Ret,
		OpCodes.Ldarg_0,
		OpCodes.Ldfld,
		OpCodes.Ldc_I4_0,
		OpCodes.Newobj,
		OpCodes.Callvirt,
		OpCodes.Ret
	};
	public OpCode Opcode => OpCodes.Ceq;
}

internal record Dup : IOpcodePattern {
	public IList<OpCode> Pattern => new List<OpCode>
	{
		OpCodes.Ldarg_0,
		OpCodes.Ldfld,
		OpCodes.Ldarg_0,
		OpCodes.Ldfld,
		OpCodes.Callvirt,
		OpCodes.Callvirt,
		OpCodes.Ret
	};

	public OpCode Opcode => OpCodes.Dup;
}

internal record Ldarg : IOpcodePattern {
	public IList<OpCode> Pattern => new List<OpCode>
	{
		OpCodes.Ldarg_0,
		OpCodes.Ldfld,
		OpCodes.Unbox_Any,
		OpCodes.Stloc_S,
		OpCodes.Ldarg_0,
		OpCodes.Ldfld,
		OpCodes.Ldarg_0,
		OpCodes.Ldfld,
		OpCodes.Ldloc_S,
		OpCodes.Ldelem_Ref,
		OpCodes.Callvirt,
		OpCodes.Ret
	};

	public OpCode Opcode => OpCodes.Ldarg;
}

// Before 7.0?
internal record LdargOld : IOpcodePattern {
	public IList<OpCode> Pattern => new List<OpCode>
	{
		OpCodes.Ldarg_0,
		OpCodes.Ldfld,
		OpCodes.Ldarg_0,
		OpCodes.Ldfld,
		OpCodes.Ldarg_0,
		OpCodes.Ldfld,
		OpCodes.Unbox_Any,
		OpCodes.Ldelem_Ref,
		OpCodes.Callvirt,
		OpCodes.Ret
	};

	public OpCode Opcode => OpCodes.Ldarg;
}

internal abstract record Ldc {
	public IList<OpCode> Pattern => new List<OpCode>
	{
		OpCodes.Ldarg_0,
		OpCodes.Ldfld,
		OpCodes.Ldarg_0,
		OpCodes.Ldfld,
		OpCodes.Unbox_Any,  // [4]
		OpCodes.Newobj,
		OpCodes.Callvirt,
		OpCodes.Ret
	};

	protected abstract string TypeName { get; }

	public bool Verify(IList<Instruction> instructions) => ((TypeRef)instructions[4].Operand).FullName == TypeName;
}

internal record LdcI4 : Ldc, IOpcodePattern {
	protected override string TypeName => "System.Int32";
	public OpCode Opcode => OpCodes.Ldc_I4;
}

internal record LdcI8 : Ldc, IOpcodePattern {
	protected override string TypeName => "System.Int64";
	public OpCode Opcode => OpCodes.Ldc_I8;
}

internal record LdcR4 : Ldc, IOpcodePattern {
	protected override string TypeName => "System.Single";
	public OpCode Opcode => OpCodes.Ldc_R4;
}

internal record LdcR8 : Ldc, IOpcodePattern {
	protected override string TypeName => "System.Double";
	public OpCode Opcode => OpCodes.Ldc_R8;
}

internal record Ldlen : IOpcodePattern {
	public IList<OpCode> Pattern => new List<OpCode>
	{
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
		OpCodes.Ldc_I4_5,
		OpCodes.Newobj,
		OpCodes.Callvirt,
		OpCodes.Ret
	};

	public OpCode Opcode => OpCodes.Ldlen;
}

internal record Ldloc : IOpcodePattern {
	public IList<OpCode> Pattern => new List<OpCode>
	{
		OpCodes.Ldarg_0,
		OpCodes.Ldfld,
		OpCodes.Unbox_Any,
		OpCodes.Stloc_S,
		OpCodes.Ldarg_0,
		OpCodes.Ldfld,
		OpCodes.Ldloc_S,
		OpCodes.Ldelem_Ref,
		OpCodes.Stloc_S,
		OpCodes.Ldarg_0,
		OpCodes.Ldfld,
		OpCodes.Ldloc_S,
		OpCodes.Callvirt,
		OpCodes.Ret
	};

	public OpCode Opcode => OpCodes.Ldloc;
}

internal record LdlocOld : IOpcodePattern {
	public IList<OpCode> Pattern => new List<OpCode>
	{
		OpCodes.Ldarg_0,
		OpCodes.Ldfld,
		OpCodes.Ldarg_0,
		OpCodes.Ldfld,
		OpCodes.Unbox_Any,
		OpCodes.Ldelem_Ref,
		OpCodes.Stloc_S,
		OpCodes.Ldarg_0,
		OpCodes.Ldfld,
		OpCodes.Ldloc_S,
		OpCodes.Callvirt,
		OpCodes.Ret
	};

	public OpCode Opcode => OpCodes.Ldloc;
}

internal record Ldnull : IOpcodePattern {
	public IList<OpCode> Pattern => new List<OpCode>
	{
		OpCodes.Ldarg_0,
		OpCodes.Ldfld,
		OpCodes.Ldnull,
		OpCodes.Newobj,
		OpCodes.Callvirt,
		OpCodes.Ret
	};

	public OpCode Opcode => OpCodes.Ldnull;
}

internal record Ldsfld : IOpcodePattern {
	public IList<OpCode> Pattern => new List<OpCode>
	{
		OpCodes.Ldarg_0,
		OpCodes.Ldfld,
		OpCodes.Unbox_Any,
		OpCodes.Call,
		OpCodes.Stloc_S,
		OpCodes.Ldarg_0,
		OpCodes.Ldfld,
		OpCodes.Ldloc_S,
		OpCodes.Callvirt,  // callvirt System.Type System.Reflection.FieldInfo::get_FieldType()
		OpCodes.Ldloc_S,
		OpCodes.Ldnull,
		OpCodes.Callvirt,  // callvirt System.Object System.Reflection.FieldInfo::GetValue(System.Object)
		OpCodes.Call,
		OpCodes.Callvirt,
		OpCodes.Ret
	};

	public OpCode Opcode => OpCodes.Ldsfld;
}

internal record LdsfldOld : IOpcodePattern {
	public IList<OpCode> Pattern => new List<OpCode>
	{
		OpCodes.Ldarg_0,
		OpCodes.Ldfld,
		OpCodes.Unbox_Any,
		OpCodes.Stloc_S,
		OpCodes.Ldtoken,
		OpCodes.Call,      // call System.Type System.Type::GetTypeFromHandle(System.RuntimeTypeHandle)
		OpCodes.Callvirt,  // callvirt System.Reflection.Module System.Type::get_Module()
		OpCodes.Ldloc_S,
		OpCodes.Callvirt,  // callvirt System.Reflection.FieldInfo System.Reflection.Module::ResolveField(System.Int32)
		OpCodes.Stloc_S,
		OpCodes.Ldarg_0,
		OpCodes.Ldfld,
		OpCodes.Ldloc_S,
		OpCodes.Callvirt,  // callvirt System.Type System.Reflection.FieldInfo::get_FieldType()
		OpCodes.Ldloc_S,
		OpCodes.Ldnull,
		OpCodes.Callvirt,  // callvirt System.Object System.Reflection.FieldInfo::GetValue(System.Object)
		OpCodes.Call,
		OpCodes.Callvirt,
		OpCodes.Ret
	};

	public OpCode Opcode => OpCodes.Ldsfld;
}

internal record Ldstr : IOpcodePattern {
	public IList<OpCode> Pattern => new List<OpCode>
	{
		OpCodes.Ldsfld,
		OpCodes.Callvirt,   // callvirt System.Int32 System.Collections.Generic.List`1<System.String>::get_Count()
		OpCodes.Brfalse,
		OpCodes.Ldarg_0,
		OpCodes.Ldfld,
		OpCodes.Ldsfld,
		OpCodes.Ldarg_0,
		OpCodes.Ldfld,
		OpCodes.Unbox_Any,  // System.Int32
		OpCodes.Callvirt,   // System.String System.Collections.Generic.List`1<System.String>::get_Item(System.Int32)
		OpCodes.Newobj,
		OpCodes.Callvirt,
		OpCodes.Ret
	};
	public bool MatchAnywhere => true; // lower half of pattern differs between versions
	public OpCode Opcode => OpCodes.Ldstr;
}

internal record Ldtoken : IOpcodePattern {
	public IList<OpCode> Pattern => new List<OpCode>
	{
		OpCodes.Ldloc_S,
		OpCodes.Nop,       // callvirt ResolveType() - new builds call an internal helper method
		OpCodes.Stloc_S,
		OpCodes.Leave_S,
		// (catch handler not captured by HandlerMapper)
		OpCodes.Ldarg_0,
		OpCodes.Ldfld,
		OpCodes.Ldloc_S,
		OpCodes.Newobj,
		OpCodes.Callvirt,  // Push()
		OpCodes.Ret
	};
	public bool MatchAnywhere => true;
	public OpCode Opcode => OpCodes.Ldtoken;
}

internal record Leave : IOpcodePattern {
	public IList<OpCode> Pattern => new List<OpCode>
	{
		OpCodes.Ldarg_0,
		OpCodes.Ldarg_0,
		OpCodes.Ldfld,
		OpCodes.Unbox_Any,
		OpCodes.Ldc_I4_1,
		OpCodes.Sub,
		OpCodes.Stfld,
		OpCodes.Ldarg_0,
		OpCodes.Ldc_I4_1,
		OpCodes.Stfld,
		OpCodes.Ret
	};
	public OpCode Opcode => OpCodes.Leave;
}

internal record Newarr : IOpcodePattern {
	public IList<OpCode> Pattern => new List<OpCode>
	{
		OpCodes.Ldarg_0,
		OpCodes.Ldfld,
		OpCodes.Unbox_Any, // unbox.any System.Int32
		OpCodes.Call,
		OpCodes.Ldarg_0,
		OpCodes.Ldfld,
		OpCodes.Callvirt,
		OpCodes.Call,
		OpCodes.Stloc_S,
		OpCodes.Ldloc_S,
		OpCodes.Callvirt,
		OpCodes.Ldflda,
		OpCodes.Ldfld,
		OpCodes.Call,      // call System.Array System.Array::CreateInstance(System.Type,System.Int32)
		OpCodes.Stloc_S,
		OpCodes.Ldarg_0,
		OpCodes.Ldfld,
		OpCodes.Ldloc_S,
		OpCodes.Newobj,
		OpCodes.Callvirt,
		OpCodes.Ret
	};

	public OpCode Opcode => OpCodes.Newarr;
}

internal record NewarrOld : IOpcodePattern {
	public IList<OpCode> Pattern => new List<OpCode>
	{
		OpCodes.Ldarg_0,
		OpCodes.Ldfld,
		OpCodes.Unbox_Any,
		OpCodes.Stloc_S,
		OpCodes.Ldtoken,
		OpCodes.Call,
		OpCodes.Callvirt,
		OpCodes.Ldloc_S,
		OpCodes.Callvirt,
		OpCodes.Ldarg_0,
		OpCodes.Ldfld,
		OpCodes.Callvirt,
		OpCodes.Call,
		OpCodes.Stloc_S,
		OpCodes.Ldloc_S,
		OpCodes.Callvirt,
		OpCodes.Ldflda,
		OpCodes.Ldfld,
		OpCodes.Call,      // call System.Array System.Array::CreateInstance(System.Type,System.Int32)
		OpCodes.Stloc_S,
		OpCodes.Ldarg_0,
		OpCodes.Ldfld,
		OpCodes.Ldloc_S,
		OpCodes.Newobj,
		OpCodes.Callvirt,
		OpCodes.Ret
	};

	public OpCode Opcode => OpCodes.Newarr;
}

internal record Newobj : IOpcodePattern {
	// Partial pattern
	public IList<OpCode> Pattern => new List<OpCode>
	{
		OpCodes.Ldarg_0,
		OpCodes.Ldfld,
		OpCodes.Unbox_Any,
		OpCodes.Call,
		OpCodes.Castclass,  // castclass System.Reflection.ConstructorInfo
		OpCodes.Stloc_S,
		OpCodes.Ldloc_S,
		OpCodes.Callvirt,   // callvirt System.Reflection.ParameterInfo[] System.Reflection.MethodBase::GetParameters()
		OpCodes.Stloc_S,
		OpCodes.Ldloc_S,
		OpCodes.Ldlen,
		OpCodes.Conv_I4,
		OpCodes.Newarr
	};

	public OpCode Opcode => OpCodes.Newobj;
}

internal record NewobjOld : IOpcodePattern {
	// Partial pattern
	public IList<OpCode> Pattern => new List<OpCode>
	{
		OpCodes.Ldarg_0,
		OpCodes.Ldfld,
		OpCodes.Unbox_Any,
		OpCodes.Stloc_S,
		OpCodes.Ldtoken,
		OpCodes.Call,       // call System.Type System.Type::GetTypeFromHandle(System.RuntimeTypeHandle)
		OpCodes.Callvirt,   // callvirt System.Reflection.Module System.Type::get_Module()
		OpCodes.Ldloc_S,
		OpCodes.Callvirt,   // callvirt System.Reflection.MethodBase System.Reflection.Module::ResolveMethod(System.Int32)
		OpCodes.Castclass,  // castclass System.Reflection.ConstructorInfo
		OpCodes.Stloc_S,
		OpCodes.Ldloc_S,
		OpCodes.Callvirt,   // callvirt System.Reflection.ParameterInfo[] System.Reflection.MethodBase::GetParameters()
		OpCodes.Stloc_S,
		OpCodes.Ldloc_S,
		OpCodes.Ldlen,
		OpCodes.Conv_I4,
		OpCodes.Newarr
	};

	public OpCode Opcode => OpCodes.Newobj;
}

internal record Nop : IOpcodePattern {
	public IList<OpCode> Pattern => new List<OpCode>
	{
		OpCodes.Ret
	};
	public OpCode Opcode => OpCodes.Nop;
}

internal record Pop : IOpcodePattern {
	public IList<OpCode> Pattern => new List<OpCode>
	{
		OpCodes.Ldarg_0,
		OpCodes.Ldfld,     // ldfld    System.Object VM/VMExecution::Stack
		OpCodes.Callvirt,  // callvirt VM/VMObject VM/VMStack::Pop()
		OpCodes.Pop,
		OpCodes.Ret
	};

	public OpCode Opcode => OpCodes.Pop;
}

internal record Ret : IOpcodePattern
{
	public IList<OpCode> Pattern => new List<OpCode>
	{
		OpCodes.Ldarg_0,
		OpCodes.Ldc_I4_S, // ldc.i4.s -3
		OpCodes.Stfld,    // stfld InstrPointer
		OpCodes.Ldarg_0,
		OpCodes.Ldfld,    // ldfld Stack
		OpCodes.Callvirt, // callvirt VMStack::Count()
		OpCodes.Ldc_I4_0,
		OpCodes.Ble,      // ble -> ret
		OpCodes.Ldarg_0,
		OpCodes.Ldarg_0,
		OpCodes.Ldfld,    // ldfld Stack
		OpCodes.Callvirt, // callvirt VMStack::Pop()
		OpCodes.Stfld,    // stfld ReturnValue
		OpCodes.Ret
	};

	public OpCode Opcode => OpCodes.Ret;
}

internal record Stelem : IOpcodePattern {
	public IList<OpCode> Pattern => new List<OpCode>
	{
		OpCodes.Ldarg_0,
		OpCodes.Ldfld,
		OpCodes.Callvirt,
		OpCodes.Stloc_S,
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
		OpCodes.Castclass,  // System.Array
		OpCodes.Dup,
		OpCodes.Callvirt,   // System.Type System.Object::GetType()
		OpCodes.Callvirt,   // System.Type System.Type::GetElementType()
		OpCodes.Stloc_S,
		OpCodes.Ldloc_S,
		OpCodes.Ldloc_S,
		OpCodes.Callvirt,
		OpCodes.Ldloc_S,
		OpCodes.Callvirt,
		OpCodes.Ldflda,
		OpCodes.Ldfld,
		OpCodes.Callvirt,   // System.Void System.Array::SetValue(System.Object,System.Int32)
		OpCodes.Ret
	};

	public OpCode Opcode => OpCodes.Stelem;
}

internal record Stfld : IOpcodePattern {
	public IList<OpCode> Pattern => new List<OpCode> {
		OpCodes.Callvirt,  // callvirt System.Void System.Reflection.FieldInfo::SetValue(System.Object,System.Object)
		OpCodes.Ret,
		OpCodes.Ldloc_S,
		OpCodes.Callvirt,  // callvirt System.Type System.Reflection.MemberInfo::get_DeclaringType()
		OpCodes.Stloc_S,
		OpCodes.Ldloc_S,
		OpCodes.Callvirt,  // callvirt System.Boolean System.Type::get_IsByRef()
		OpCodes.Brfalse,
		OpCodes.Ldloc_S,
		OpCodes.Callvirt,  // callvirt System.Type System.Type::GetElementType()
		OpCodes.Stloc_S,
		OpCodes.Ldloc_S,
		OpCodes.Callvirt,  // callvirt System.Boolean System.Type::get_IsValueType()
		OpCodes.Brfalse_S,
		OpCodes.Ldloc_S,
		OpCodes.Call,      // call System.Object System.Activator::CreateInstance(System.Type)
		OpCodes.Stloc_S,
		OpCodes.Ldloc_S,
		OpCodes.Isinst,
		OpCodes.Brfalse_S,
		OpCodes.Ldloc_S,
		OpCodes.Castclass,
		OpCodes.Ldloc_S,
		OpCodes.Ldloc_S,
		OpCodes.Call,
		OpCodes.Callvirt,
		OpCodes.Newobj,
		OpCodes.Throw
	};
	public bool MatchAnywhere => true;
	public OpCode Opcode => OpCodes.Stfld;
}

internal record Stloc : IOpcodePattern {
	public IList<OpCode> Pattern => new List<OpCode>
	{
		OpCodes.Ldarg_0,
		OpCodes.Ldfld,
		OpCodes.Unbox_Any,
		OpCodes.Stloc_S,
		OpCodes.Ldarg_0,
		OpCodes.Ldfld,
		OpCodes.Ldloc_S,
		OpCodes.Ldarg_0,
		OpCodes.Ldarg_0,
		OpCodes.Ldfld,
		OpCodes.Callvirt,
		OpCodes.Ldarg_0,
		OpCodes.Ldfld,
		OpCodes.Ldfld,
		OpCodes.Ldloc_S,
		OpCodes.Callvirt,
		OpCodes.Ldfld,
		OpCodes.Ldarg_0,
		OpCodes.Ldfld,
		OpCodes.Ldfld,
		OpCodes.Ldloc_S,
		OpCodes.Callvirt,
		OpCodes.Ldfld,
		OpCodes.Call,
		OpCodes.Stelem_Ref,
		OpCodes.Ret
	};

	public OpCode Opcode => OpCodes.Stloc;
}

internal record Stsfld : IOpcodePattern {
	public IList<OpCode> Pattern => new List<OpCode>
	{
		OpCodes.Ldarg_0,
		OpCodes.Ldfld,
		OpCodes.Unbox_Any,  // unbox.any System.Int32
		OpCodes.Call,
		OpCodes.Stloc_S,
		OpCodes.Ldarg_0,
		OpCodes.Ldfld,
		OpCodes.Callvirt,
		OpCodes.Ldloc_S,
		OpCodes.Callvirt,   // callvirt System.Type System.Reflection.FieldInfo::get_FieldType()
		OpCodes.Callvirt,
		OpCodes.Stloc_S,
		OpCodes.Ldloc_S,
		OpCodes.Ldnull,
		OpCodes.Ldloc_S,
		OpCodes.Callvirt,   // callvirt System.Void System.Reflection.FieldInfo::SetValue(System.Object,System.Object)
		OpCodes.Ret
	};

	public OpCode Opcode => OpCodes.Stsfld;
}

internal record StsfldOld : IOpcodePattern {
	public IList<OpCode> Pattern => new List<OpCode>
	{
		OpCodes.Ldarg_0,
		OpCodes.Ldfld,
		OpCodes.Unbox_Any,  // unbox.any System.Int32
		OpCodes.Stloc_S,
		OpCodes.Ldtoken,
		OpCodes.Call,       // call System.Type System.Type::GetTypeFromHandle(System.RuntimeTypeHandle)
		OpCodes.Callvirt,   // callvirt System.Reflection.Module System.Type::get_Module()
		OpCodes.Ldloc_S,
		OpCodes.Callvirt,   // callvirt System.Reflection.FieldInfo System.Reflection.Module::ResolveField(System.Int32)
		OpCodes.Stloc_S,
		OpCodes.Ldarg_0,
		OpCodes.Ldfld,
		OpCodes.Callvirt,
		OpCodes.Ldloc_S,
		OpCodes.Callvirt,   // callvirt System.Type System.Reflection.FieldInfo::get_FieldType()
		OpCodes.Callvirt,
		OpCodes.Stloc_S,
		OpCodes.Ldloc_S,
		OpCodes.Ldnull,
		OpCodes.Ldloc_S,
		OpCodes.Callvirt,   // callvirt System.Void System.Reflection.FieldInfo::SetValue(System.Object,System.Object)
		OpCodes.Ret
	};

	public OpCode Opcode => OpCodes.Stsfld;
}

internal record Switch : IOpcodePattern {
	// Partial pattern
	public IList<OpCode> Pattern => new List<OpCode>
	{
		OpCodes.Ldarg_0,
		OpCodes.Ldfld,
		OpCodes.Castclass,  // System.Int32[]
		OpCodes.Stloc_S,
		OpCodes.Ldarg_0,
		OpCodes.Ldfld,
		OpCodes.Callvirt,
		OpCodes.Call,
		OpCodes.Stloc_S,
		OpCodes.Ldloc_S,
		OpCodes.Callvirt,
		OpCodes.Ldflda,
		OpCodes.Ldfld,
		OpCodes.Stloc_S,
		OpCodes.Ldloc_S,
	};

	public OpCode Opcode => OpCodes.Switch;
}

internal record Throw : IOpcodePattern {
	public IList<OpCode> Pattern => new List<OpCode>
	{
		OpCodes.Ldarg_0,
		OpCodes.Ldfld,     // ldfld    System.Object VM/VMExecution::Stack
		OpCodes.Callvirt,  // callvirt VM/VMObject VM/VMStack::Pop()
		OpCodes.Ldnull,
		OpCodes.Callvirt,
		OpCodes.Castclass,
		OpCodes.Throw
	};

	public OpCode Opcode => OpCodes.Throw;
}
