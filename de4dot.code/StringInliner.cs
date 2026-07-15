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

using System;
using System.Collections.Generic;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using de4dot.code.AssemblyClient;
using de4dot.blocks;

namespace de4dot.code {
	public abstract class StringInlinerBase : MethodReturnValueInliner {
		protected override void InlineReturnValues(IList<CallResult> callResults) {
			foreach (var callResult in callResults) {
				var block = callResult.block;
				int num = callResult.callEndIndex - callResult.callStartIndex + 1;

				var decryptedString = callResult.returnValue as string;
				if (decryptedString == null)
					continue;

				int ldstrIndex = callResult.callStartIndex;
				block.Replace(ldstrIndex, num, OpCodes.Ldstr.ToInstruction(decryptedString));

				// If it's followed by castclass string, remove it
				if (ldstrIndex + 1 < block.Instructions.Count) {
					var instr = block.Instructions[ldstrIndex + 1];
					if (instr.OpCode.Code == Code.Castclass && instr.Operand.ToString() == "System.String")
						block.Remove(ldstrIndex + 1, 1);
				}

				// If it's followed by String.Intern(), then nop out that call
				if (ldstrIndex + 1 < block.Instructions.Count) {
					var instr = block.Instructions[ldstrIndex + 1];
					if (instr.OpCode.Code == Code.Call) {
						if (instr.Operand is IMethod calledMethod &&
							calledMethod.FullName == "System.String System.String::Intern(System.String)") {
							block.Remove(ldstrIndex + 1, 1);
						}
					}
				}

				Logger.v("Decrypted string: {0}", Utils.ToCsharpString(decryptedString));
			}
		}
	}

	public class DynamicStringInliner : StringInlinerBase {
		IAssemblyClient assemblyClient;
		Dictionary<int, int> methodTokenToId = new Dictionary<int, int>();
		Dictionary<int, MethodUseInfo> methodIdToUseInfo = new Dictionary<int, MethodUseInfo>();
		private class MethodUseInfo {
			public long calls;
			public long not_nulls;
		}

		class MyCallResult : CallResult {
			public int methodId;
			public MethodSpec gim;
			public MyCallResult(Block block, int callEndIndex, int methodId, MethodSpec gim)
				: base(block, callEndIndex) {
				this.methodId = methodId;
				this.gim = gim;
			}
		}

		public override bool HasHandlers => methodTokenToId.Count != 0;

		public DynamicStringInliner(IAssemblyClient assemblyClient) => this.assemblyClient = assemblyClient;

		public void Initialize(IEnumerable<int> methodTokens) {
			methodTokenToId.Clear();
			foreach (var methodToken in methodTokens) {
				if (methodTokenToId.ContainsKey(methodToken))
					continue;
				var methodId = methodTokenToId[methodToken] = assemblyClient.StringDecrypterService.DefineStringDecrypter(methodToken);
				if (!methodIdToUseInfo.ContainsKey(methodId))
					methodIdToUseInfo[methodId] = new MethodUseInfo { };
			}
		}
		public void LogUnusedDecrypters() {
			foreach (var kvp in methodTokenToId) {
				var use_data = methodIdToUseInfo[kvp.Value];
				if (methodIdToUseInfo.TryGetValue(kvp.Value, out var useInfo) && useInfo.calls > 0 && useInfo.not_nulls == 0) {
					Logger.n("Decrypter with Metadata token: 0x{0:X8} we called {1} times and always returned null, may not be correctly working", kvp.Key, use_data.calls);
				}
				
			}
		}
		protected override CallResult CreateCallResult(IMethod method, MethodSpec gim, Block block, int callInstrIndex) {
			if (!methodTokenToId.TryGetValue(method.MDToken.ToInt32(), out int methodId))
				return null;
			return new MyCallResult(block, callInstrIndex, methodId, gim);
		}

		protected override void InlineAllCalls() {
			var sortedCalls = new Dictionary<int, List<MyCallResult>>();
			foreach (var tmp in callResults) {
				var callResult = (MyCallResult)tmp;
				if (!sortedCalls.TryGetValue(callResult.methodId, out var list))
					sortedCalls[callResult.methodId] = list = new List<MyCallResult>(callResults.Count);
				list.Add(callResult);
			}

			foreach (var methodId in sortedCalls.Keys) {
				var list = sortedCalls[methodId];
				var args = new object[list.Count];
				for (int i = 0; i < list.Count; i++) {
					AssemblyData.SimpleData.Pack(list[i].args);
					args[i] = list[i].args;
				}
				
				var decryptedStrings = assemblyClient.StringDecrypterService.DecryptStrings(methodId, args, Method.MDToken.ToInt32());
				if (decryptedStrings.Length != args.Length)
					throw new ApplicationException("Invalid decrypted strings array length");
				AssemblyData.SimpleData.Unpack(decryptedStrings);
				var total_not_null = 0;
				for (int i = 0; i < list.Count; i++) {
					if ((list[i].returnValue = (string)decryptedStrings[i]) != null)
						total_not_null++;
				}
				var use_info = methodIdToUseInfo[methodId];
				use_info.not_nulls += total_not_null;
				use_info.calls += list.Count;



				
			}

		}
	}

	public class StaticStringInliner : StringInlinerBase {
		MethodDefAndDeclaringTypeDict<Func<MethodDef, MethodSpec, object[], string>> stringDecrypters = new MethodDefAndDeclaringTypeDict<Func<MethodDef, MethodSpec, object[], string>>();

		public override bool HasHandlers => stringDecrypters.Count != 0;
		public IEnumerable<MethodDef> Methods => stringDecrypters.GetKeys();

		class MyCallResult : CallResult {
			public IMethod IMethod;
			public MethodSpec gim;
			public MyCallResult(Block block, int callEndIndex, IMethod method, MethodSpec gim)
				: base(block, callEndIndex) {
				IMethod = method;
				this.gim = gim;
			}
		}

		public void Add(MethodDef method, Func<MethodDef, MethodSpec, object[], string> handler) {
			if (method != null)
				stringDecrypters.Add(method, handler);
		}

		protected override void InlineAllCalls() {
			foreach (var tmp in callResults) {
				var callResult = (MyCallResult)tmp;
				var handler = stringDecrypters.Find(callResult.IMethod);
				callResult.returnValue = handler((MethodDef)callResult.IMethod, callResult.gim, callResult.args);
			}
		}

		protected override CallResult CreateCallResult(IMethod method, MethodSpec gim, Block block, int callInstrIndex) {
			if (method is MemberRef memberRef)
				method = memberRef.ResolveMethod();
			if (stringDecrypters.Find(method) == null)
				return null;
			return new MyCallResult(block, callInstrIndex, method, gim);
		}
	}
}
