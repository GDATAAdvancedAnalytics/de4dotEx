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
using de4dot.blocks;

namespace de4dot.code.deobfuscators.dotNET_Reactor.v4 {
	class ResourceResolver {
		ModuleDefMD module;
		EncryptedResource encryptedResource;
		MethodDef initMethod;

		public bool Detected => encryptedResource.Method != null;
		public TypeDef Type => encryptedResource.Type;
		public MethodDef InitMethod => initMethod;
		public bool FoundResource => encryptedResource.FoundResource;

		public ResourceResolver(ModuleDefMD module) {
			this.module = module;
			encryptedResource = new EncryptedResource(module);
		}

		public ResourceResolver(ModuleDefMD module, ResourceResolver oldOne) {
			this.module = module;
			encryptedResource = new EncryptedResource(module, oldOne.encryptedResource);
		}

		public void Find(ISimpleDeobfuscator simpleDeobfuscator) {
			foreach (var type in module.Types) {
				if (type.BaseType == null || type.BaseType.FullName != "System.Object")
					continue;
				if (!CheckFields(type.Fields))
					continue;
				foreach (var method in type.Methods) {
					if (!method.IsStatic || !method.HasBody)
						continue;
					if (!DotNetUtils.IsMethod(method, "System.Reflection.Assembly", "(System.Object,System.ResolveEventArgs)") &&
						!DotNetUtils.IsMethod(method, "System.Reflection.Assembly", "(System.Object,System.Object)"))
						continue;
					var initMethod = GetResourceDecrypterInitMethod(method, true) ??
					                 GetResourceDecrypterInitMethod(method, false);
					if (initMethod == null)
						continue;

					encryptedResource.Method = initMethod;
					return;
				}
			}
		}

		MethodDef GetResourceDecrypterInitMethod(MethodDef method, bool checkResource) {
			if (encryptedResource.CouldBeResourceDecrypter(method, null, checkResource))
				return method;

			foreach (var calledMethod in DotNetUtils.GetCalledMethods(module, method)) {
				if (!DotNetUtils.IsMethod(calledMethod, "System.Void", "()"))
					continue;
				if (encryptedResource.CouldBeResourceDecrypter(calledMethod, null, checkResource))
					return calledMethod;
			}

			return null;
		}

		bool CheckFields(IList<FieldDef> fields) {
			if (fields.Count != 3 && fields.Count != 4)
				return false;

			int numBools = fields.Count == 3 ? 1 : 2;
			var fieldTypes = new FieldTypes(fields);
			if (fieldTypes.Count("System.Boolean") != numBools)
				return false;
			if (fieldTypes.Count("System.Object") == 2)
				return true;
			if (fieldTypes.Count("System.String[]") != 1)
				return false;
			return fieldTypes.Count("System.Reflection.Assembly") == 1 || fieldTypes.Count("System.Object") == 1;
		}

		public void Initialize(ISimpleDeobfuscator simpleDeobfuscator, IDeobfuscator deob) {
			if (encryptedResource.Method == null)
				return;

			initMethod = FindInitMethod(simpleDeobfuscator);
			if (initMethod == null)
				throw new ApplicationException("Could not find resource resolver init method");

			simpleDeobfuscator.Deobfuscate(encryptedResource.Method);
			simpleDeobfuscator.DecryptStrings(encryptedResource.Method, deob);
			encryptedResource.Initialize(simpleDeobfuscator);
		}

		MethodDef FindInitMethod(ISimpleDeobfuscator simpleDeobfuscator) {
			var ctor = Type.FindMethod(".ctor");
			foreach (var method in Type.Methods) {
				if (!method.IsStatic || method.Body == null)
					continue;
				if (!DotNetUtils.IsMethod(method, "System.Void", "()"))
					continue;
				if (method.Body.Variables.Count > 1)
					continue;

				simpleDeobfuscator.Deobfuscate(method);
				bool stsfldUsed = false, newobjUsed = false;
				foreach (var instr in method.Body.Instructions) {
					if (instr.OpCode.Code == Code.Stsfld) {
						var field = instr.Operand as IField;
						if (field == null || field.FieldSig.GetFieldType().GetElementType() != ElementType.Boolean)
							continue;
						if (!new SigComparer().Equals(Type, field.DeclaringType))
							continue;
						stsfldUsed = true;
					}
					else if (instr.OpCode.Code == Code.Newobj) {
						var calledCtor = instr.Operand as IMethod;
						if (calledCtor == null)
							continue;
						if (!MethodEqualityComparer.CompareDeclaringTypes.Equals(calledCtor, ctor))
							if (calledCtor.DeclaringType.FullName != "System.ResolveEventHandler") 
								continue;
						newobjUsed = true;
					}
				}
				if (!stsfldUsed || !newobjUsed)
					continue;

				return method;
			}
			return null;
		}

		public EmbeddedResource MergeResources() {
			if (encryptedResource.Resource == null)
				return null;
			DeobUtils.DecryptAndAddResources(module, encryptedResource.Resource.Name.String, () => {
				byte[] decrypted;
				try {
					decrypted = encryptedResource.Decrypt();
				}
				catch {
					return null;
				}

				if (decrypted.Length < 64)
					throw new Exception("Decrypted resource data has length " + decrypted.Length);
				if (decrypted[0] == 0x4D && decrypted[1] == 0x5A && decrypted[62] == 0 && decrypted[63] == 0)
					return decrypted;

				try {
					return QuickLZ.Decompress(decrypted);
				}
				catch {
					try {
						return DeobUtils.Inflate(decrypted, true);
					}
					catch {
						return null;
					}
				}
			});
			return encryptedResource.Resource;
		}
	}
}
