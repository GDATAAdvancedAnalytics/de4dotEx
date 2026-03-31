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
using System.Text;
using dnlib.DotNet.Emit;
using dnlib.IO;

namespace de4dot.code.deobfuscators.dotNET_Reactor.v4.vm;

/// <summary>
/// Parses a .NET Reactor VM resource containing virtualized methods.
/// </summary>
internal class ResourceParser {
	private DataReader _stream;
	private byte[] _operands;
	private uint[] _offsets;
	readonly bool _withByte;

	public int MethodCount => _offsets.Length;
	public string[] Strings { get; private set; } = [];

	public ResourceParser(DataReader stream, bool withByte) {
		_stream = stream;
		_withByte = withByte;
		ReadHeader();
	}

	public VMMethod GetMethod(int index) => ReadMethod(_offsets[index]);

	private void ReadHeader()
	{
		if (_withByte && _stream.ReadByte() > 0)  // this influences ldtoken etc. - if >0, ResolveMethod()/ResolveMember()/... is not used
			Logger.n("First byte not zero");

		int numOperands = ReadPackedInt();
		byte[] operands = new byte[255];
		for (int i = 0; i < numOperands; i++)
			operands[_stream.ReadByte()] = _stream.ReadByte();

		int numStrings = ReadPackedInt();
		if (numStrings != 0) {
			Strings = new string[numStrings];
			for (int i = 0; i < numStrings; i++) {
				var data = _stream.ReadBytes(ReadPackedInt());
				Strings[i] = Encoding.Unicode.GetString(data);
			}
		}

		int numOffsets = ReadPackedInt();
		uint[] offsets = new uint[numOffsets];
		for (int i = 0; i < offsets.Length; i++)
			offsets[i] = (uint)ReadPackedInt();

		uint cursor = _stream.Position;
		for (int i = 0; i < offsets.Length; i++)
		{
			uint prev = offsets[i];
			offsets[i] = cursor;
			cursor += prev;
		}

		_operands = operands;
		_offsets = offsets;
	}

	private VMMethod ReadMethod(uint offset) {
        _stream.Position = offset;

		var result = new VMMethod { Token = ReadPackedInt() };
		int numLocals = ReadPackedInt();
        int numExceptionHandlers = ReadPackedInt();
        int numInstructions = ReadPackedInt();

        for (int i = 0; i < numLocals; i++)
            result.Locals.Add((VMElemType)ReadPackedInt());

        for (int i = 0; i < numExceptionHandlers; i++)
	        result.ExceptionHandlers.Add(ReadExceptionHandler());

        for (int i = 0; i < numInstructions; i++)
        {
            int opcode = _stream.ReadByte();
            if (opcode >= 176)
                throw new Exception($"Opcode {opcode} is out of range");

            object? operand = null;
            if (_operands[opcode] != 0)
            {
                operand = _operands[opcode] switch
                {
                    1 => ReadPackedInt(),
                    2 => _stream.ReadInt64(),
                    3 => _stream.ReadSingle(),
                    4 => _stream.ReadDouble(),
                    5 => ReadArrayOperand(),
                    _ => throw new Exception($"Invalid operand type {_operands[opcode]}")
                };
            }

            result.Instructions.Add(new VMInstruction(opcode, operand));
        }

        return result;
	}

	private VMExceptionHandler ReadExceptionHandler() {
		var eh = new VMExceptionHandler {
			TryStart = ReadPackedInt(),
			TryEnd = ReadPackedInt(),
			HandlerStart = ReadPackedInt(),
			HandlerEnd = ReadPackedInt(),
			HandlerType = (ExceptionHandlerType)ReadPackedInt()
		};

		switch (eh.HandlerType) {
		    case ExceptionHandlerType.Catch:
			    eh.CatchType = ReadPackedInt();
			    break;
		    case ExceptionHandlerType.Filter:
			    eh.FilterStart = ReadPackedInt();
			    break;
		    default:
			    ReadPackedInt(); // unused
			    break;
		}

		return eh;
	}

	private int[] ReadArrayOperand()
	{
		int count = ReadPackedInt();
		int[] array = new int[count];
		for (int i = 0; i < count; i++)
			array[i] = ReadPackedInt();

		return array;
	}

	private int ReadPackedInt()
	{
		int first = _stream.ReadByte();
		uint value = (uint)(first & 0x3F); // lower 6 bits
		bool negated = (first & 0x40) != 0;
		bool hasMore = (first & 0x80) != 0;

		int shift = 6;

		while (hasMore)
		{
			int b = _stream.ReadByte();
			value |= (uint)(b & 0x7F) << shift;

			hasMore = (b & 0x80) != 0;
			shift += 7;
		}

		int result = (int)value;

		return negated ? ~result : result;
	}
}
