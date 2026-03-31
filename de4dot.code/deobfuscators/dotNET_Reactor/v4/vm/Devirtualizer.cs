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
using System.Collections.Generic;
using System.Linq;
using de4dot.blocks;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace de4dot.code.deobfuscators.dotNET_Reactor.v4.vm;

public class Devirtualizer {
	readonly ISimpleDeobfuscator _deobfuscator;
	readonly ModuleDefMD _module;
	TypeDef _vmType;
	EmbeddedResource _resource;

	public bool Detected => _vmType != null && _resource != null;
	public bool StreamHasPrependedByte { get; private set; }

	public bool CanRemoveType { get; private set; }
	public EmbeddedResource Resource => _resource;
	public TypeDef VMType => _vmType;

	public List<MethodDef> DevirtualizedMethods { get; } = new();

	public Devirtualizer(ISimpleDeobfuscator deobfuscator, ModuleDefMD module) {
		_deobfuscator = deobfuscator;
		_module = module;
	}

	public Devirtualizer(ModuleDefMD module, Devirtualizer oldOne) {
		_module = module;
		_deobfuscator = oldOne._deobfuscator;
		_vmType = DeobUtils.Lookup(module, oldOne._vmType, "Could not find VM type");
		if (oldOne._resource != null)
			_resource = DotNetUtils.GetResource(module, oldOne._resource.Name.String) as EmbeddedResource;
		StreamHasPrependedByte = oldOne.StreamHasPrependedByte;
	}

	public void Find() {
		_vmType = _module.Types.FirstOrDefault(type =>
			type.Methods.Any(method =>
				method.IsStatic
				&& method.HasGenericParameters
				&& method.Parameters.Count == 4
				&& method.Parameters[0].Type.FullName == "System.Int32"
				&& method.Parameters[3].Type.IsByRef
				&& method.ReturnType.FullName == "System.Object[]"));

		if (_vmType == null)
			return;

		MethodDef resourceMethod = null, parseMethod = null;
		foreach (var method in _vmType.Methods.Where(method =>
			         DotNetUtils.CallsMethod(method, "System.Void System.IO.BinaryReader::.ctor(System.IO.Stream)"))) {
			if (DotNetUtils.CallsMethod(method, "System.Void System.IO.MemoryStream::.ctor(System.Byte[])"))
				parseMethod = method;
			else
				resourceMethod = method;
		}

		if (resourceMethod == null || parseMethod == null)
			return;

		var strings = DotNetUtils.GetCodeStrings(resourceMethod);
		if (strings.Count == 0) {
			Logger.w("DRVM: No strings in resource method {0}", resourceMethod);
			return;
		}

		_resource = DotNetUtils.GetResource(_module, strings) as EmbeddedResource;

		var insns = parseMethod.Body.Instructions;
		for (int i = 0; i < insns.Count - 2; i++) {
			if (insns[i].IsLdcI4() && insns[i].GetLdcI4Value() == 0) {
				if (insns[i + 1].OpCode == OpCodes.Cgt_Un && insns[i + 2].OpCode == OpCodes.Stsfld) {
					StreamHasPrependedByte = true;
					break;
				}
			}
		}
	}

	/// <summary>
	/// Parses the VM resource and attempts to devirtualize all virtualized methods.
	/// </summary>
	public void Devirtualize() {
		var parser = new ResourceParser(_resource.CreateReader(), StreamHasPrependedByte);

		var mpr = new HandlerMapper(_vmType, _deobfuscator);
		var matcher = new PatternMatcher();
		matcher.MatchAll(mpr);

		for (int i = 0; i < parser.MethodCount; i++) {
			var vmMethod = parser.GetMethod(i);
			var target = _module.ResolveMethod(new MDToken(vmMethod.Token).Rid);
			Logger.n("Devirtualizing method: {0} ({1:X8})", target, vmMethod.Token);

			try {
				bool allKnown = true;
				foreach (var ins in vmMethod.Instructions) {
					var matched = matcher.GetOpcode(ins.VirtualOpcode);
					if (matched != null)
						ins.Opcode = matched.Opcode;
					else {
						allKnown = false;
						Logger.w("Unknown virtual opcode {0}", ins.VirtualOpcode);
					}
				}

				if (!allKnown)
					continue;

				vmMethod.ResolveTokens(_module);
				vmMethod.ResolveStrings(parser.Strings, _module);
				vmMethod.ConcretizeStelem();
				//foreach (var ins in vmMethod.Instructions) Console.WriteLine(ins);

				target.Body = GenCilBody(vmMethod, target.Parameters);
				DevirtualizedMethods.Add(target);
			}
			catch (Exception ex) {
				Logger.w("Error devirtualizing {0}: {1}", target, ex.Message);
			}
		}

		CanRemoveType = DevirtualizedMethods.Count == parser.MethodCount;
	}

	private CilBody GenCilBody(VMMethod method, ParameterList parameters) {
		// Map VM instructions to dnlib instructions 1:1 at first.
		var dnList = method.Instructions.Select(ins => new Instruction(ins.Opcode!, ins.Operand)).ToList();
		var dnLocals = method.Locals.Select(elemType => new Local(elemType.ToTypeSig(_module.CorLibTypes))).ToList();

		// Resolve operands that reference other instructions or locals.
		foreach (var ins in dnList) {
			if (ins.IsBr() || ins.IsConditionalBranch() || ins.IsLeave()) {
				ins.Operand = dnList[(int)ins.Operand];
			} else if (ins.OpCode == OpCodes.Switch) {
				ins.Operand = ((int[])ins.Operand).Select(index => dnList[index]).ToArray();
			} else if (ins.IsLdloc() || ins.IsStloc()) {
				ins.Operand = dnLocals[(int)ins.Operand];
			} else if (ins.IsLdarg() || ins.IsStarg()) {
				ins.Operand = parameters[(int)ins.Operand];
			}
		}

		// Resolve instructions and types in exception handlers.
		var dnEh = method.ExceptionHandlers.Select(eh => {
			var handler = new ExceptionHandler(eh.HandlerType) {
				TryStart = dnList[eh.TryStart],
				TryEnd = dnList[eh.TryEnd + 1],
				HandlerStart = dnList[eh.HandlerStart],
				HandlerEnd = dnList[eh.HandlerEnd + 1]
			};
			if (handler.HandlerType == ExceptionHandlerType.Filter)
				handler.FilterStart = dnList[eh.FilterStart];
			if (handler.HandlerType == ExceptionHandlerType.Catch)
				handler.CatchType = (ITypeDefOrRef)_module.ResolveToken(new MDToken(eh.CatchType));

			return handler;
		}).ToList();

		return new CilBody(true, dnList, dnEh, dnLocals);
	}
}
