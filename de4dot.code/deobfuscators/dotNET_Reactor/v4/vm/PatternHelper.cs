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

// Some code in this file is loosely based on https://github.com/void-stack/VMAttack.

using System.Linq;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace de4dot.code.deobfuscators.dotNET_Reactor.v4.vm;

#nullable enable

internal static class PatternHelper {
	public static bool FindPatternInOverrides(this MethodDef? virtualMethod, IPattern pattern) {
		if (virtualMethod is not { IsVirtual: true, IsAbstract: true })
			return false;

		foreach (var t in virtualMethod.Module.GetTypes()) {
			foreach (var method in t.Methods.Where(m => m.IsVirtual && m.HasBody && m.Name == virtualMethod.Name)) {
				if (PatternMatcher.Match(pattern, method.Body.Instructions))
					return true;
			}
		}

		return false;
	}

	/**
	 * Used for special situations where the overridden method calls another abstract method.
	 */
	public static bool FindPatternInOverridesCall(this MethodDef? virtualMethod, IPattern pattern) {
		if (virtualMethod is not { IsVirtual: true, IsAbstract: true })
			return false;

		foreach (var t in virtualMethod.Module.GetTypes()) {
			// (...sometimes there is some nop/br trash in front of here)
			// ldarg.0
			// callvirt this.AsByte()
			// ret
			var firstOverride = t.Methods.FirstOrDefault(m => m.IsVirtual && m.HasBody
			                                                              && m.Name == virtualMethod.Name
			                                                              && m.Body.Instructions.Count >= 3
			                                                              && m.Body.Instructions[m.Body.Instructions.Count - 2].OpCode == OpCodes.Callvirt); // net48 compat
			if (firstOverride == null)
				continue;

			var calledInOverride = ((MethodDef)firstOverride.Body.Instructions[firstOverride.Body.Instructions.Count - 2].Operand).Name; // net48 compat
			foreach (var t2 in virtualMethod.Module.GetTypes()) {
				foreach (var callee in t2.Methods.Where(m => m.IsVirtual && m.HasBody && m.Name == calledInOverride)) {
					if (PatternMatcher.Match(pattern, callee.Body.Instructions))
						return true;
				}
			}
			return false;
		}

		// In samples like 133fc00de41f4d14caf8a9473dfa14d72588c48db4bf4a7f9b1978865888c7df, the intermediate callvirt is sometimes skipped.
		return FindPatternInOverrides(virtualMethod, pattern);
	}
}
