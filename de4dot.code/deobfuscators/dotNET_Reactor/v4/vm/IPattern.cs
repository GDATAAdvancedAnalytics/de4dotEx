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

// Pattern code inspired by https://github.com/void-stack/VMAttack and https://github.com/puff/EazyDevirt.

using System.Collections.Generic;
using dnlib.DotNet.Emit;

namespace de4dot.code.deobfuscators.dotNET_Reactor.v4.vm;

// Note: These should be interfaces in newer .NET versions, but they aren't
// because .NET 4.8 doesn't support default implementations.

public abstract record IPattern {
	/// <summary>
	/// Contiguous list of opcodes that should match. Nops are wildcards.
	/// The pattern does not need to overlap the input completely, i.e., it can be a prefix of the input.
	/// </summary>
	public virtual IList<OpCode> Pattern { get; }

	/// <summary>
	/// If true, the pattern doesn't need to match from the start.
	/// </summary>
	public virtual bool MatchAnywhere => false;

	/// <summary>
	/// Does (optional) additional verification on the input if the pattern would not be unique otherwise.
	/// </summary>
	/// <param name="instructions">Matched instructions from the input.</param>
	/// <returns>True if this should indeed be a match.</returns>
	public virtual bool Verify(IList<Instruction> instructions) => true;
}

public abstract record IOpcodePattern : IPattern {
	/// <summary>
	/// Resulting opcode assigned to the match.
	/// </summary>
	public virtual OpCode Opcode { get; }
}
