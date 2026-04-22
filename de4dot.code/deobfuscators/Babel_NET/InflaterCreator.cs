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
using System.Linq;
using ICSharpCode.SharpZipLib.Zip.Compression;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using de4dot.blocks;

namespace de4dot.code.deobfuscators.Babel_NET {
	class InflaterCreator {
		public static Inflater Create(MethodDef method, ISimpleDeobfuscator deobfuscator, bool noHeader) => Create(FindInflaterType(method), deobfuscator, noHeader);

		public static Inflater Create(TypeDef inflaterType, ISimpleDeobfuscator deobfuscator, bool noHeader) {
			if (inflaterType == null)
				return CreateNormal(noHeader);
			var initHeaderMethod = FindInitHeaderMethod(inflaterType);
			if (initHeaderMethod == null)
				return CreateNormal(noHeader, "Could not find inflater init header method");
			deobfuscator.Deobfuscate(initHeaderMethod);
			var magic = GetMagic(initHeaderMethod);
			if (!magic.HasValue)
				return CreateNormal(noHeader);

			var mapping = GetBlockTypeMapping(initHeaderMethod);
			var size = GetTypeSize(initHeaderMethod);
			return new BabelInflater(noHeader, size ?? 3, magic.Value, mapping);
		}

		static Inflater CreateNormal(bool noHeader) => CreateNormal(noHeader, null);

		static Inflater CreateNormal(bool noHeader, string errorMessage) {
			if (errorMessage != null)
				Logger.w("{0}", errorMessage);
			return new Inflater(noHeader);
		}

		static TypeDef FindInflaterType(MethodDef method) {
			if (method == null || method.Body == null)
				return null;
			foreach (var instr in method.Body.Instructions) {
				if (instr.OpCode.Code != Code.Call)
					continue;
				var calledMethod = instr.Operand as MethodDef;
				if (calledMethod == null || !calledMethod.IsStatic)
					continue;

				var type = calledMethod.DeclaringType;
				foreach (var nested in type.NestedTypes) {
					if (DeobUtils.HasInteger(nested.FindMethod(".ctor"), 0x8001))
						return type;
				}
			}

			return null;
		}

		static MethodDef FindInitHeaderMethod(TypeDef inflaterType) {
			foreach (var nested in inflaterType.NestedTypes) {
				var method = FindInitHeaderMethod2(nested);
				if (method != null)
					return method;
			}
			return null;
		}

		static MethodDef FindInitHeaderMethod2(TypeDef nested) {
			foreach (var method in nested.Methods) {
				if (method.IsStatic || method.Body == null)
					continue;
				if (!DotNetUtils.IsMethod(method, "System.Boolean", "()"))
					continue;

				return method;
			}

			return null;
		}

		static int? GetMagic(MethodDef method) {
			if (method == null || method.Body == null)
				return null;
			var instrs = method.Body.Instructions;
			for (int i = 0; i < instrs.Count - 3; i++) {
				var ldci4_1 = instrs[i];
				if (!ldci4_1.IsLdcI4() || ldci4_1.GetLdcI4Value() != 16)
					continue;

				var callvirt = instrs[i + 1];
				if (callvirt.OpCode.Code != Code.Callvirt)
					continue;

				var ldci4_2 = instrs[i + 2];
				if (!ldci4_2.IsLdcI4())
					continue;

				if (instrs[i + 3].OpCode.Code != Code.Xor)
					continue;

				return ldci4_2.GetLdcI4Value();
			}

			return null;
		}

		/// <summary>
		/// Analyzes a chain of "if (this.blockType == x)" checks in the initHeader method.
		/// </summary>
		/// <param name="method">initHeader method</param>
		/// <returns>An array with 3 values corresponding to STORED_BLOCK, STATIC_TREES and DYN_TREES; null if analysis failed.</returns>
		static int[] GetBlockTypeMapping(MethodDef method) {
			if (!method.HasBody || !method.Body.HasInstructions)
				return null;

			var instrs = method.Body.Instructions;
			var blocks = new List<(Instruction Ldarg, Instruction Target, int Value)>();
			for (int i = 0; i < instrs.Count - 3; i++) {
				var i0 = instrs[i];     // load "this"
				var i1 = instrs[i + 1]; // field load for blockType
				var i2 = instrs[i + 2]; // type value to check against
				var i3 = instrs[i + 3]; // bne to next check

				if (!i0.IsLdarg()) continue;
				if (i1.OpCode.Code != Code.Ldfld) continue;
				if (!i2.IsLdcI4()) continue;
				if (i3.OpCode.Code is not (Code.Bne_Un or Code.Bne_Un_S)) continue;
				if (i3.Operand is not Instruction target) continue;

				blocks.Add((i0, target, i2.GetLdcI4Value()));
			}

			if (blocks.Count != 3)
				return null;

			// Each block starts with ldarg; create a mapping ldarg -> block
			var map = blocks.ToDictionary(b => b.Ldarg);

			// Find start of chain (where ldarg is not a target of any other)
			var start = blocks.FirstOrDefault(b => !blocks.Any(x => x.Target == b.Ldarg));
			if (start.Ldarg == null)
				return null;

			var result = new List<int>();
			var current = start;
			var visited = new HashSet<Instruction>();

			while (true) {
				if (!visited.Add(current.Ldarg))
					return null; // cycle, shouldn't happen

				result.Add(current.Value);

				if (!map.TryGetValue(current.Target, out var next))
					break; // last bne.un ends the chain

				current = next;
			}

			return result.Count == 3 ? result.ToArray() : null;
		}

		/// <summary>
		/// Determines the size (in bits) of the block type. This does not include the "last block" bit.
		/// </summary>
		/// <param name="method">initHeader method</param>
		/// <returns>Number of bits or null</returns>
		static int? GetTypeSize(MethodDef method) {
			if (method == null || method.Body == null)
				return null;
			var instrs = method.Body.Instructions;
			for (int i = 0; i < instrs.Count - 6; i++) {
				if (!instrs[i].IsLdarg()) continue;
				if (!instrs[i + 1].IsLdarg()) continue;
				if (instrs[i + 2].OpCode != OpCodes.Ldfld) continue;
				if (!instrs[i + 3].IsLdcI4()) continue;
				if (instrs[i + 4].OpCode != OpCodes.Callvirt) continue;
				if (instrs[i + 5].OpCode != OpCodes.Stfld) continue;

				return instrs[i + 3].GetLdcI4Value();
			}

			return null;
		}
	}
}
