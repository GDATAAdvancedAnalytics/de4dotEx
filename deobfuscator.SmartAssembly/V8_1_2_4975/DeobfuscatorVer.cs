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
using de4dot.blocks;
using de4dot.code;
using de4dot.code.deobfuscators;

namespace de4dot.plugin.deobfuscators.SmartAssembly.V8_1_2_4975 {
	class DeobfuscatorVer : IDeobfuscatorVer {
		IList<StringDecrypterInfo> stringDecrypterInfos = new List<StringDecrypterInfo>();
		IList<StringDecrypter> stringDecrypters = new List<StringDecrypter>();
		ResourceDecrypterInfo resourceDecrypterInfo;
		ResourceDecrypter resourceDecrypter;
		AssemblyResolverInfo assemblyResolverInfo;
		AssemblyResolver assemblyResolver;
		ResourceResolverInfo resourceResolverInfo;
		ResourceResolver resourceResolver;
		MemoryManagerInfo memoryManagerInfo;

		ProxyCallFixer proxyCallFixer;
		AutomatedErrorReportingFinder automatedErrorReportingFinder;
		TamperProtectionRemover tamperProtectionRemover;

		Deobfuscator parent;
		ModuleDefMD module;
		IDeobfuscatedFile DeobfuscatedFile;
		Deobfuscator.Options options;

		public ProxyCallFixer GetProxyCallFixer() { return proxyCallFixer; }

		public DeobfuscatorVer(Deobfuscator parent) {
			this.parent = parent;
			this.DeobfuscatedFile = parent.DeobfuscatedFile;
			this.module = parent.GetModule();
			this.options = parent.GetOptions();
		}

		public int DetectInternal() {
			if (memoryManagerInfo.Detected)
				return 100;
			else
				return 0;
		}

		public void ScanForObfuscator() {
			memoryManagerInfo = new MemoryManagerInfo(module);
			memoryManagerInfo.Find();
			proxyCallFixer = new ProxyCallFixer(module, DeobfuscatedFile);
			proxyCallFixer.FindDelegateCreator(module);
		}

		public void DeobfuscateBegin() {
			tamperProtectionRemover = new TamperProtectionRemover(module);
			automatedErrorReportingFinder = new AutomatedErrorReportingFinder(module);
			automatedErrorReportingFinder.Find();

			if (options.RemoveMemoryManager) {
				parent._AddModuleCctorInitCallToBeRemoved(memoryManagerInfo.CctorInitMethod);
				parent._AddCallToBeRemoved(module.EntryPoint, memoryManagerInfo.CctorInitMethod);
			}

			InitDecrypters();
			proxyCallFixer.Find();
		}

		void InitDecrypters() {
			assemblyResolverInfo = new AssemblyResolverInfo(module, DeobfuscatedFile, parent);
			assemblyResolverInfo.FindTypes();
			resourceDecrypterInfo = new ResourceDecrypterInfo(module, assemblyResolverInfo.SimpleZipTypeMethod, DeobfuscatedFile);
			resourceResolverInfo = new ResourceResolverInfo(module, DeobfuscatedFile, parent, assemblyResolverInfo);
			resourceResolverInfo.FindTypes();
			resourceDecrypter = new ResourceDecrypter(resourceDecrypterInfo);
			assemblyResolver = new AssemblyResolver(resourceDecrypter, assemblyResolverInfo);
			resourceResolver = new ResourceResolver(module, assemblyResolver, resourceResolverInfo);

			InitStringDecrypterInfos();
			assemblyResolverInfo.FindTypes();
			resourceResolverInfo.FindTypes();

			parent._AddModuleCctorInitCallToBeRemoved(assemblyResolverInfo.CallResolverMethod);
			parent._AddCallToBeRemoved(module.EntryPoint, assemblyResolverInfo.CallResolverMethod);
			parent._AddModuleCctorInitCallToBeRemoved(resourceResolverInfo.CallResolverMethod);
			parent._AddCallToBeRemoved(module.EntryPoint, resourceResolverInfo.CallResolverMethod);

			resourceDecrypterInfo.SetSimpleZipType(GetGlobalSimpleZipTypeMethod(), DeobfuscatedFile);

			if (!DecryptResources())
				throw new ApplicationException("Could not decrypt resources");

			DumpEmbeddedAssemblies();
		}

		void DumpEmbeddedAssemblies() {
			assemblyResolver.ResolveResources();
			foreach (var tuple in assemblyResolver.GetDecryptedResources()) {
				DeobfuscatedFile.CreateAssemblyFile(tuple.Item2, tuple.Item1.simpleName, null);
				parent._AddResourceToBeRemoved(tuple.Item1.resource, $"Embedded assembly: {tuple.Item1.assemblyName}");
			}
		}

		bool DecryptResources() {
			if (!resourceResolver.CanDecryptResource())
				return false;
			var info = resourceResolver.MergeResources();
			if (info == null)
				return true;
			parent._AddResourceToBeRemoved(info.resource, "Encrypted resources");
			assemblyResolver.ResolveResources();
			return true;
		}

		MethodDef GetGlobalSimpleZipTypeMethod() {
			if (assemblyResolverInfo.SimpleZipTypeMethod != null)
				return assemblyResolverInfo.SimpleZipTypeMethod;
			foreach (var info in stringDecrypterInfos) {
				if (info.SimpleZipTypeMethod != null)
					return info.SimpleZipTypeMethod;
			}
			return null;
		}

		void InitStringDecrypterInfos() {
			var stringEncoderClassFinder = new StringEncoderClassFinder(module, DeobfuscatedFile);
			stringEncoderClassFinder.Find();
			foreach (var info in stringEncoderClassFinder.StringsEncoderInfos) {
				var sinfo = new StringDecrypterInfo(module, info.StringDecrypterClass) {
					GetStringDelegate = info.GetStringDelegate,
					StringsType = info.StringsType,
					CreateStringDelegateMethod = info.CreateStringDelegateMethod,
				};
				stringDecrypterInfos.Add(sinfo);
			}

			// There may be more than one string decrypter. The strings in the first one's
			// methods may be decrypted by the other string decrypter.

			var initd = new Dictionary<StringDecrypterInfo, bool>(stringDecrypterInfos.Count);
			while (initd.Count != stringDecrypterInfos.Count) {
				StringDecrypterInfo initdInfo = null;
				for (int i = 0; i < 2; i++) {
					foreach (var info in stringDecrypterInfos) {
						if (initd.ContainsKey(info))
							continue;
						if (info.Initialize(parent, DeobfuscatedFile)) {
							resourceDecrypterInfo.SetSimpleZipType(info.SimpleZipTypeMethod, DeobfuscatedFile);
							initdInfo = info;
							break;
						}
					}
					if (initdInfo != null)
						break;

					assemblyResolverInfo.FindTypes();
					resourceResolverInfo.FindTypes();
					DecryptResources();
				}

				if (initdInfo == null)
					break;

				initd[initdInfo] = true;
				InitStringDecrypter(initdInfo);
			}

			// Sometimes there could be a string decrypter present that isn't called by anyone.
			foreach (var info in stringDecrypterInfos) {
				if (initd.ContainsKey(info))
					continue;
				Logger.v("String decrypter not initialized. Token {0:X8}", info.StringsEncodingClass.MDToken.ToInt32());
			}
		}

		void InitStringDecrypter(StringDecrypterInfo info) {
			Logger.v("Adding string decrypter. Resource: {0}", Utils.ToCsharpString(info.StringsResource.Name));
			var decrypter = new StringDecrypter(info);
			StaticStringInliner staticStringInliner = parent.GetStaticStringInliner();
			if (decrypter.CanDecrypt) {
				var invokeMethod = info.GetStringDelegate?.FindMethod("Invoke");
				staticStringInliner.Add(invokeMethod, (method, gim, args) => {
					var fieldDef = DotNetUtils.GetField(module, (IField)args[0]);
					return decrypter.Decrypt(fieldDef.MDToken.ToInt32(), (int)args[1]);
				});
				staticStringInliner.Add(info.StringDecrypterMethod, (method, gim, args) => {
					return decrypter.Decrypt(0, (int)args[0]);
				});
			}
			stringDecrypters.Add(decrypter);
			DeobfuscatedFile.StringDecryptersAdded();
		}

		public void DeobfuscateMethodEnd(Blocks blocks) {
			proxyCallFixer.Deobfuscate(blocks);
			RemoveAutomatedErrorReportingCode(blocks);
			RemoveTamperProtection(blocks);
			RemoveStringsInitCode(blocks);
		}

		public void DeobfuscateEnd() {
			parent._RemoveProxyDelegates(proxyCallFixer, parent.GetCanRemoveTypes());
			RemoveMemoryManagerStuff();
			RemoveTamperProtectionStuff();
			RemoveStringDecryptionStuff();
			RemoveResolverInfoTypes(assemblyResolverInfo, "Assembly");
			RemoveResolverInfoTypes(resourceResolverInfo, "Resource");
		}

		void RemoveResolverInfoTypes(ResolverInfoBase info, string typeName) {
			if (!parent.GetCanRemoveTypes())
				return;
			if (info.CallResolverType == null || info.Type == null)
				return;
			parent._AddTypeToBeRemoved(info.CallResolverType, $"{typeName} resolver type #1");
			parent._AddTypeToBeRemoved(info.Type, $"{typeName} resolver type #2");
		}

		void RemoveAutomatedErrorReportingCode(Blocks blocks) {
			if (!options.RemoveAutomatedErrorReporting)
				return;
			if (automatedErrorReportingFinder.Remove(blocks))
				Logger.v("Removed Automated Error Reporting code");
		}

		void RemoveTamperProtection(Blocks blocks) {
			if (!options.RemoveTamperProtection)
				return;
			if (tamperProtectionRemover.Remove(blocks))
				Logger.v("Removed Tamper Protection code");
		}

		void RemoveMemoryManagerStuff() {
			if (!parent.GetCanRemoveTypes() || !options.RemoveMemoryManager)
				return;
			parent._AddTypeToBeRemoved(memoryManagerInfo.Type, "Memory manager type");
		}

		void RemoveTamperProtectionStuff() {
			if (!options.RemoveTamperProtection)
				return;
			parent._AddMethodsToBeRemoved(tamperProtectionRemover.PinvokeMethods, "Tamper protection PInvoke method");
		}

		void RemoveStringDecryptionStuff() {
			if (!parent.GetCanRemoveStringDecrypterType())
				return;

			foreach (var decrypter in stringDecrypters) {
				var info = decrypter.StringDecrypterInfo;
				parent._AddResourceToBeRemoved(info.StringsResource, "Encrypted strings");
				parent._AddFieldsToBeRemoved(info.GetAllStringDelegateFields(), "String decrypter delegate field");

				if (parent.GetCanRemoveTypes()) {
					parent._AddTypeToBeRemoved(info.StringsEncodingClass, "String decrypter type");
					parent._AddTypeToBeRemoved(info.StringsType, "Creates the string decrypter delegates");
					parent._AddTypeToBeRemoved(info.GetStringDelegate, "String decrypter delegate type");
				}
			}
		}

		void RemoveStringsInitCode(Blocks blocks) {
			if (!parent.GetCanRemoveStringDecrypterType())
				return;

			if (blocks.Method.Name == ".cctor") {
				foreach (var decrypter in stringDecrypters)
					decrypter.StringDecrypterInfo.RemoveInitCode(blocks);
			}
		}

		public bool IsProxyCallFixerDetected() {
			return proxyCallFixer.Detected;
		}
	}
}
