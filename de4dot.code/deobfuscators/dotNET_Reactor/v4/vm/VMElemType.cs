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

using System;
using dnlib.DotNet;

namespace de4dot.code.deobfuscators.dotNET_Reactor.v4.vm;

internal enum VMElemType : byte
{
	Object = 0,
	SByte = 1,
	Byte = 2,
	Int16 = 3,
	UInt16 = 4,
	Int32 = 5,
	UInt32 = 6,
	Int64 = 7,
	UInt64 = 8,
	Single = 9,
	Double = 10,
	Boolean = 11,
	IntPtr = 12,
	UIntPtr = 13,
	String = 14,
	Char = 15,
	Enum = 16,
	Unknown = 17,
	Void = 18
}

internal static class VMElemTypeExtensions {
	internal static TypeSig ToTypeSig(this VMElemType elemType, ICorLibTypes types) =>
		elemType switch {
			VMElemType.Object => types.Object,
			VMElemType.SByte => types.SByte,
			VMElemType.Byte => types.Byte,
			VMElemType.Int16 => types.Int16,
			VMElemType.UInt16 => types.UInt16,
			VMElemType.Int32 => types.Int32,
			VMElemType.UInt32 => types.UInt32,
			VMElemType.Int64 => types.Int64,
			VMElemType.UInt64 => types.UInt64,
			VMElemType.Single => types.Single,
			VMElemType.Double => types.Double,
			VMElemType.Boolean => types.Boolean,
			VMElemType.IntPtr => types.IntPtr,
			VMElemType.UIntPtr => types.UIntPtr,
			VMElemType.String => types.String,
			VMElemType.Char => types.Char,
			VMElemType.Enum => types.Object,
			VMElemType.Unknown => types.Object,
			VMElemType.Void => types.Void,
			_ => throw new ArgumentOutOfRangeException(nameof(elemType), elemType, null)
		};
}
