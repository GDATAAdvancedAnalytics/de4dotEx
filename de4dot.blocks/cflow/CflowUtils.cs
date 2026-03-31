/*
    Copyright (C) 2011-2015 de4dot@gmail.com

    This file is part of de4dot.

    de4dot is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    de4dot is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with de4dot.  If not, see <http://www.gnu.org/licenses/>.
*/

using System.Collections.Generic;
using dnlib.DotNet.Emit;

namespace de4dot.blocks.cflow {
	static class CflowUtils {
		public static Block? GetSwitchTarget(IList<Block>? targets, Block? fallThrough, Int32Value intValue) {
			if (!intValue.AllBitsValid())
				return null;

			int index = intValue.Value;
			if (targets == null || index < 0 || index >= targets.Count)
				return TryResolveConditionalFallthrough(fallThrough, intValue);
			else
				return targets[index];
		}

		/**
		 * Some obfuscators have default blocks that dispatch additional cases.
		 * Example: 4e86d71a19f7f69471776817dc67585064b4b60542bc60e9450739bca63226ee
		 */
		private static Block? TryResolveConditionalFallthrough(Block? block, Int32Value value) {
			while (true) {
				if (block == null) return null;

				var instrs = block.Instructions;
				if (instrs.Count < 3) return block;
				if (!instrs[0].IsLdloc()) return block;
				if (!instrs[1].IsLdcI4()) return block;
				if (instrs[2].OpCode.Code is not (Code.Beq or Code.Beq_S or Code.Bne_Un or Code.Bne_Un_S)) return block;

				int constant = instrs[1].GetLdcI4Value();
				var branch = instrs[2];

				int v = value.Value;

				bool taken =
					branch.OpCode.Code is Code.Beq or Code.Beq_S ? (v == constant) :
					branch.OpCode.Code is Code.Bne_Un or Code.Bne_Un_S && (v != constant);

				if (taken)
					return block.Targets![0];
				block = block.FallThrough;
			}
		}
	}
}
