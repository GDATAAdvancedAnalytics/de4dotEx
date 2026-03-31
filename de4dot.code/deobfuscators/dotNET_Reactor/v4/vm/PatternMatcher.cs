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

// Some code in this file is loosely based on https://github.com/void-stack/VMAttack / https://github.com/puff/EazyDevirt.

using System;
using System.Collections.Generic;
using dnlib.DotNet.Emit;

namespace de4dot.code.deobfuscators.dotNET_Reactor.v4.vm;

#nullable enable

internal class PatternMatcher
{
	private readonly List<IOpcodePattern> _opcodePatterns;

	private readonly Dictionary<int, IOpcodePattern> _opcodes;

	public PatternMatcher()
	{
		_opcodes = new Dictionary<int, IOpcodePattern>();
		_opcodePatterns = new List<IOpcodePattern>();
		foreach (var type in typeof(PatternMatcher).Assembly.GetTypes())
			if (type.GetInterface(nameof(IOpcodePattern)) != null)
				if (Activator.CreateInstance(type) is IOpcodePattern instance)
					_opcodePatterns.Add(instance);
	}

	public IOpcodePattern? GetOpcode(int vmOpcode) => _opcodes.GetValueOrDefault(vmOpcode);

	public void MatchAll(HandlerMapper mapper) {
		int numMatched = 0;
		foreach (var entry in mapper.Handlers) {
			foreach (var pat in _opcodePatterns) {
				if (Match(pat, entry.Value.Instructions)) {
					Logger.v($"Opcode match: {entry.Key} -> {pat.Opcode}");
					_opcodes[entry.Key] = pat;
					numMatched++;
				}
			}
		}
		Logger.v($"Matched opcodes: {numMatched}");
	}

	public static bool Match(IPattern pattern, IList<Instruction> instructions) {
		var match = pattern.MatchAnywhere ? MatchAnywhere(pattern, instructions) : MatchesFromStart(pattern, instructions);
		return match && pattern.Verify(instructions);
	}

	private static bool MatchAnywhere(IPattern pattern, IList<Instruction> instructions)
	{
		var pat = pattern.Pattern;
		if (pat.Count > instructions.Count) return false;

		for (int i = 0; i < instructions.Count; i++) {
			var currentCount = 0;

			for (int j = i, k = 0; j < instructions.Count && k < pat.Count; j++, k++) {
				var instruction = instructions[j];
				if (instruction.OpCode != pat[k] && pat[k] != OpCodes.Nop && !CanInterchange(instruction, pat[k]))
					break;
				currentCount++;
			}

			if (currentCount == pat.Count)
				return true;
		}

		return false;
	}

	private static bool MatchesFromStart(IPattern pattern, IList<Instruction> instructions)
	{
		var pat = pattern.Pattern;
		if (pat.Count > instructions.Count) return false;

		for (int i = 0; i < pat.Count; i++) {
			if (pat[i] == OpCodes.Nop)
				continue;

			var instruction = instructions[i];
			if (instructions[i].OpCode != pat[i] && !CanInterchange(instruction, pat[i]))
				return false;
		}

		return true;
	}

	private static bool CanInterchange(Instruction ins, OpCode patOpCode)
	{
		var patIns = new Instruction(patOpCode);

		return (ins.IsLdloc() && patIns.IsLdloc())
			|| (ins.IsStloc() && patIns.IsStloc())
			|| (ins.IsConditionalBranch() && patOpCode.Name.Replace(".s", "") == ins.OpCode.Name.Replace(".s", ""));
	}
}
