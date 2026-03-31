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
using System.Linq;
using dnlib.DotNet.Emit;

namespace de4dot.code.deobfuscators.dotNET_Reactor.v4.vm;

/**
 * Represents a sequence of instructions that implement a virtual opcode.
 */
internal readonly struct VMHandler {
	public IList<Instruction> Instructions { get; }

	public VMHandler(IList<Instruction> instructions) => Instructions = instructions;

	public override string ToString() =>
		Instructions.Count > 0
			? "public override IList<OpCode> Pattern => new List<OpCode>\n{\n" + string.Join(",\n", Instructions.Select(x => $"OpCodes.{x.OpCode.Code}")) + "\n};"
			: "None";
}
