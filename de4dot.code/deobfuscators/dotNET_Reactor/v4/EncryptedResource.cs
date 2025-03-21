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
using System.Security.Cryptography;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using de4dot.blocks;
using de4dot.blocks.cflow;
using dnlib.IO;

namespace de4dot.code.deobfuscators.dotNET_Reactor.v4 {
	enum DnrDecrypterType {
		Unknown,
		V1,
		V2,
		V3,
		V4,
	}

	class EncryptedResource {
		ModuleDefMD module;
		MethodDef resourceDecrypterMethod;
		EmbeddedResource encryptedDataResource;
		IDecrypter decrypter;

		public DnrDecrypterType DecrypterTypeVersion => decrypter == null ? DnrDecrypterType.Unknown : decrypter.DecrypterType;
		public TypeDef Type => resourceDecrypterMethod?.DeclaringType;

		public MethodDef Method {
			get => resourceDecrypterMethod;
			set => resourceDecrypterMethod = value;
		}

		public EmbeddedResource Resource => encryptedDataResource;
		public bool FoundResource => encryptedDataResource != null;
		public EncryptedResource(ModuleDefMD module) => this.module = module;

		public EncryptedResource(ModuleDefMD module, EncryptedResource oldOne) {
			this.module = module;
			resourceDecrypterMethod = Lookup(oldOne.resourceDecrypterMethod, "Could not find resource decrypter method");
			if (oldOne.encryptedDataResource != null)
				encryptedDataResource = DotNetUtils.GetResource(module, oldOne.encryptedDataResource.Name.String) as EmbeddedResource;
			decrypter = oldOne.decrypter;

			if (encryptedDataResource == null && oldOne.encryptedDataResource != null)
				throw new ApplicationException("Could not initialize EncryptedResource");
		}

		public void SetNewResource(byte[] data) {
			var dataReaderFactory = ByteArrayDataReaderFactory.Create(data, filename: null);
			var newResource = new EmbeddedResource(encryptedDataResource.Name, dataReaderFactory, 0, (uint)data.Length, encryptedDataResource.Attributes);
			int index = module.Resources.IndexOf(encryptedDataResource);
			encryptedDataResource = newResource;
			module.Resources[index] = encryptedDataResource;
		}

		T Lookup<T>(T def, string errorMessage) where T : class, ICodedToken =>
			DeobUtils.Lookup(module, def, errorMessage);

		public bool CouldBeResourceDecrypter(MethodDef method, IList<string> additionalTypes) =>
			CouldBeResourceDecrypter(method, additionalTypes, true);

		public bool CouldBeResourceDecrypter(MethodDef method, IList<string> additionalTypes, bool checkResource) {
			if (GetDecrypterType(method, additionalTypes) == DnrDecrypterType.Unknown)
				return false;

			if (checkResource && FindMethodsDecrypterResource(method) == null)
				return false;

			return true;
		}

		public DnrDecrypterType GuessDecrypterType() => GetDecrypterType(resourceDecrypterMethod, null);

		static DnrDecrypterType GetDecrypterType(MethodDef method, IList<string> additionalTypes) {
			if (method == null || !method.IsStatic || method.Body == null)
				return DnrDecrypterType.Unknown;

			if (additionalTypes == null)
				additionalTypes = new string[0];
			var localTypes = new LocalTypes(method);
			if (DecrypterV1.CouldBeResourceDecrypter(method, localTypes, additionalTypes))
				return DnrDecrypterType.V1;
			else if (DecrypterV3.CouldBeResourceDecrypter(method, localTypes, additionalTypes))
				return DnrDecrypterType.V3;
			else if (DecrypterV4.CouldBeResourceDecrypter(method, localTypes, additionalTypes))
				return DnrDecrypterType.V4;
			else if (DecrypterV2.CouldBeResourceDecrypter(method, localTypes, additionalTypes))
				return DnrDecrypterType.V2;

			return DnrDecrypterType.Unknown;
		}

		public void Initialize(ISimpleDeobfuscator simpleDeobfuscator) {
			if (resourceDecrypterMethod == null)
				return;

			simpleDeobfuscator.Deobfuscate(resourceDecrypterMethod);

			encryptedDataResource = FindMethodsDecrypterResource(resourceDecrypterMethod);
			if (encryptedDataResource == null)
				return;

			var decrypterType = GetDecrypterType(resourceDecrypterMethod, new string[0]);

			if (decrypterType == DnrDecrypterType.V3) {
				decrypter = new DecrypterV3(resourceDecrypterMethod);
			}
			else if (decrypterType == DnrDecrypterType.V4) {
				decrypter = new DecrypterV4(module, simpleDeobfuscator, resourceDecrypterMethod);
			}
			else {
				var key = ArrayFinder.GetInitializedByteArray(resourceDecrypterMethod, 32);
				if (key == null)
					throw new ApplicationException("Could not find resource decrypter key");
				var iv = ArrayFinder.GetInitializedByteArray(resourceDecrypterMethod, 16);
				if (iv == null)
					throw new ApplicationException("Could not find resource decrypter IV");
				if (NeedReverse())
					Array.Reverse(iv);  // DNR 4.5.0.0
				if (UsesPublicKeyToken()) {
					var publicKeyToken = module.Assembly.PublicKeyToken;
					if (publicKeyToken != null && publicKeyToken.Data.Length > 0) {
						for (int i = 0; i < 8; i++)
							iv[i * 2 + 1] = publicKeyToken.Data[i];
					}
				}

				switch (decrypterType) {
				case DnrDecrypterType.V1: decrypter = new DecrypterV1(iv, key); break;
				case DnrDecrypterType.V2: decrypter = new DecrypterV2(iv, key, resourceDecrypterMethod); break;
				default: throw new ApplicationException("Unknown decrypter type");
				}
			}
		}

		static int[] pktIndexes = new int[16] { 1, 0, 3, 1, 5, 2, 7, 3, 9, 4, 11, 5, 13, 6, 15, 7 };
		bool UsesPublicKeyToken() {
			int pktIndex = 0;
			foreach (var instr in resourceDecrypterMethod.Body.Instructions) {
				if (instr.OpCode.FlowControl != FlowControl.Next) {
					pktIndex = 0;
					continue;
				}
				if (!instr.IsLdcI4())
					continue;
				int val = instr.GetLdcI4Value();
				if (val != pktIndexes[pktIndex++]) {
					pktIndex = 0;
					continue;
				}
				if (pktIndex >= pktIndexes.Length)
					return true;
			}
			return false;
		}

		bool NeedReverse() => DotNetUtils.CallsMethodContains(resourceDecrypterMethod, "System.Array::Reverse");

		EmbeddedResource FindMethodsDecrypterResource(MethodDef method) {
			foreach (var s in DotNetUtils.GetCodeStrings(method)) {
				if (DotNetUtils.GetResource(module, s) is EmbeddedResource resource)
					return resource;
			}
			return null;
		}

		interface IDecrypter {
			DnrDecrypterType DecrypterType { get; }
			byte[] Decrypt(EmbeddedResource resource);
			byte[] Encrypt(byte[] data);
		}

		class DecrypterV1 : IDecrypter {
			readonly byte[] key, iv;

			public DnrDecrypterType DecrypterType => DnrDecrypterType.V1;

			public DecrypterV1(byte[] iv, byte[] key) {
				this.iv = iv;
				this.key = key;
			}

			public static bool CouldBeResourceDecrypter(MethodDef method, LocalTypes localTypes, IList<string> additionalTypes) {
				var requiredTypes = new List<string> {
					"System.Byte[]",
					"System.Security.Cryptography.CryptoStream",
					"System.Security.Cryptography.ICryptoTransform",
					"System.String",
					"System.Boolean",
				};
				var requiredTypes2 = new List<string> {
					"System.Security.Cryptography.ICryptoTransform",
					"System.IO.Stream",
					"System.Int32",
					"System.Byte[]",
					"System.Boolean",
				};
				requiredTypes.AddRange(additionalTypes);
				if (!localTypes.All(requiredTypes) && !localTypes.All(requiredTypes2))
					return false;

				if (DotNetUtils.GetMethod(method.DeclaringType, "System.Security.Cryptography.SymmetricAlgorithm", "()") != null) {
					if (localTypes.Exists("System.UInt64") || (localTypes.Exists("System.UInt32") && !localTypes.Exists("System.Reflection.Assembly")))
						return false;
				}

				return true;
			}

			public byte[] Decrypt(EmbeddedResource resource) => DeobUtils.AesDecrypt(resource.CreateReader().ToArray(), key, iv);

			public byte[] Encrypt(byte[] data) {
				using (var aes = new RijndaelManaged { Mode = CipherMode.CBC }) {
					using (var transform = aes.CreateEncryptor(key, iv)) {
						return transform.TransformFinalBlock(data, 0, data.Length);
					}
				}
			}
		}

		class DecrypterV2 : IDecrypter {
			readonly byte[] key, iv;
			MethodDef method;
			List<Instruction> instructions;
			List<Local> locals;
			readonly InstructionEmulator instrEmulator = new InstructionEmulator();
			Local emuLocal;
			Parameter emuArg;
			MethodDef emuMethod;
			bool isNewDecrypter;

			public DnrDecrypterType DecrypterType => DnrDecrypterType.V2;

			public DecrypterV2(byte[] iv, byte[] key, MethodDef method) {
				this.iv = iv;
				this.key = key;
				this.method = method;
				locals = new List<Local>(method.Body.Variables);
				if (!Initialize())
					throw new ApplicationException("Could not initialize decrypter");
			}

			public static bool CouldBeResourceDecrypter(MethodDef method, LocalTypes localTypes, IList<string> additionalTypes) {
				var requiredTypes = new List<string> {
					"System.Int32",
					"System.Byte[]",
				};
				requiredTypes.AddRange(additionalTypes);
				if (!localTypes.All(requiredTypes))
					return false;

				return true;
			}

			bool Initialize() {
				var origInstrs = method.Body.Instructions;

				if (!Find(origInstrs, out int emuStartIndex, out int emuEndIndex, out emuLocal)) {
					if (!FindStartEnd(origInstrs, out emuStartIndex, out emuEndIndex, out emuLocal))
						if (!FindStartEnd2(ref origInstrs, out emuStartIndex, out emuEndIndex, out emuLocal, out emuArg, ref emuMethod, ref locals))
							return false;
						else
							isNewDecrypter = true;
				}

				if (!isNewDecrypter) {
					for (int i = 0; i < iv.Length; i++)
						key[i] ^= iv[i];
				}

				int count = emuEndIndex - emuStartIndex + 1;
				instructions = new List<Instruction>(count);
				for (int i = 0; i < count; i++)
					instructions.Add(origInstrs[emuStartIndex + i].Clone());

				return true;
			}

			bool Find(IList<Instruction> instrs, out int startIndex, out int endIndex, out Local tmpLocal) {
				startIndex = 0;
				endIndex = 0;
				tmpLocal = null;

				if (!FindStart(instrs, out int emuStartIndex, out emuLocal))
					return false;
				if (!FindEnd(instrs, emuStartIndex, out int emuEndIndex))
					return false;
				startIndex = emuStartIndex;
				endIndex = emuEndIndex;
				tmpLocal = emuLocal;
				return true;
			}

			bool FindStartEnd(IList<Instruction> instrs, out int startIndex, out int endIndex, out Local tmpLocal) {
				for (int i = 0; i + 8 < instrs.Count; i++) {
					if (instrs[i].OpCode.Code != Code.Conv_R_Un)
						continue;
					if (instrs[i + 1].OpCode.Code != Code.Conv_R8)
						continue;
					if (instrs[i + 2].OpCode.Code != Code.Conv_U4)
						continue;
					if (instrs[i + 3].OpCode.Code != Code.Add)
						continue;
					int newEndIndex = i + 3;
					int newStartIndex = -1;
					for (int x = newEndIndex; x > 0; x--)
						if (instrs[x].OpCode.FlowControl != FlowControl.Next) {
							newStartIndex = x + 1;
							break;
						}
					if (newStartIndex < 0)
						continue;

					var checkLocs = new List<Local>();
					int ckStartIndex = -1;
					for (int y = newEndIndex; y >= newStartIndex; y--) {
						var loc = CheckLocal(instrs[y], true);
						if (loc == null)
							continue;
						if (!checkLocs.Contains(loc))
							checkLocs.Add(loc);
						if (checkLocs.Count == 3)
							break;
						ckStartIndex = y;
					}
					endIndex = newEndIndex;
					startIndex = Math.Max(ckStartIndex, newStartIndex);
					tmpLocal = CheckLocal(instrs[startIndex], true);
					return true;
				}
				endIndex = 0;
				startIndex = 0;
				tmpLocal = null;
				return false;
			}

			bool FindStartEnd2(ref IList<Instruction> instrs, out int startIndex, out int endIndex, out Local tmpLocal, out Parameter tmpArg, ref MethodDef methodDef, ref List<Local> locals) {
				foreach (var instr in instrs) {
					if (instr.OpCode == OpCodes.Call) {
						if (instr.Operand is MethodDef method && method.ReturnType.FullName == "System.Byte[]") {
							foreach (var call in DotNetUtils.GetMethodCalls(method)) {
								if (call is MethodDef calledMethod && calledMethod.Parameters.Count == 2) {
									instrs = calledMethod.Body.Instructions;
									methodDef = calledMethod;
									locals = new List<Local>(calledMethod.Body.Variables);

									startIndex = 0;
									endIndex = instrs.Count - 1;
									tmpLocal = null;
									tmpArg = calledMethod.Parameters[1];
									return true;
								}
							}
						}
					}
				}

				endIndex = 0;
				startIndex = 0;
				tmpLocal = null;
				tmpArg = null;
				return false;
			}

			bool FindStart(IList<Instruction> instrs, out int startIndex, out Local tmpLocal) {
				for (int i = 0; i + 8 < instrs.Count; i++) {
					if (instrs[i].OpCode.Code != Code.Conv_U)
						continue;
					if (instrs[i + 1].OpCode.Code != Code.Ldelem_U1)
						continue;
					if (instrs[i + 2].OpCode.Code != Code.Or)
						continue;
					if (CheckLocal(instrs[i + 3], false) == null)
						continue;
					Local local;
					if ((local = CheckLocal(instrs[i + 4], true)) == null)
						continue;
					if (CheckLocal(instrs[i + 5], true) == null)
						continue;
					if (instrs[i + 6].OpCode.Code != Code.Add)
						continue;
					if (CheckLocal(instrs[i + 7], false) != local)
						continue;
					var instr = instrs[i + 8];
					int newStartIndex = i + 8;
					if (instr.IsBr()) {
						instr = instr.Operand as Instruction;
						newStartIndex = instrs.IndexOf(instr);
					}
					if (newStartIndex < 0 || instr == null)
						continue;
					if (CheckLocal(instr, true) != local)
						continue;

					startIndex = newStartIndex;
					tmpLocal = local;
					return true;
				}

				startIndex = 0;
				tmpLocal = null;
				return false;
			}

			bool FindEnd(IList<Instruction> instrs, int startIndex, out int endIndex) {
				for (int i = startIndex; i < instrs.Count; i++) {
					var instr = instrs[i];
					if (instr.OpCode.FlowControl != FlowControl.Next)
						break;
					if (instr.IsStloc() && instr.GetLocal(locals) == emuLocal) {
						endIndex = i - 1;
						return true;
					}
				}

				endIndex = 0;
				return false;
			}

			Local CheckLocal(Instruction instr, bool isLdloc) {
				if (isLdloc && !instr.IsLdloc())
					return null;
				else if (!isLdloc && !instr.IsStloc())
					return null;

				return instr.GetLocal(locals);
			}

			public byte[] Decrypt(EmbeddedResource resource) {
				var encrypted = resource.CreateReader().ToArray();
				var decrypted = new byte[encrypted.Length];

				uint sum = 0;
				if (isNewDecrypter) {
					for (int i = 0; i < encrypted.Length; i += 4) {
						var value = ReadUInt32(key, i % key.Length);
						sum += value + CalculateMagic(sum + value);
						WriteUInt32(decrypted, i, sum ^ ReadUInt32(encrypted, i));
					}
				}
				else {
					for (int i = 0; i < encrypted.Length; i += 4) {
						sum = CalculateMagic(sum + ReadUInt32(key, i % key.Length));
						WriteUInt32(decrypted, i, sum ^ ReadUInt32(encrypted, i));
					}
				}

				return decrypted;
			}

			uint CalculateMagic(uint input) {
				if (emuArg == null) {
					instrEmulator.Initialize(method, method.Parameters, locals, method.Body.InitLocals, false);
					instrEmulator.SetLocal(emuLocal, new Int32Value((int)input));
				}
				else {
					instrEmulator.Initialize(emuMethod, emuMethod.Parameters, locals, emuMethod.Body.InitLocals, false);
					instrEmulator.SetArg(emuArg, new Int32Value((int)input));
				}

				foreach (var instr in instructions)
					instrEmulator.Emulate(instr);

				var tos = instrEmulator.Pop() as Int32Value;
				if (tos == null || !tos.AllBitsValid())
					throw new ApplicationException("Couldn't calculate magic value");
				return (uint)tos.Value;
			}

			static uint ReadUInt32(byte[] ary, int index) {
				int sizeLeft = ary.Length - index;
				if (sizeLeft >= 4)
					return BitConverter.ToUInt32(ary, index);
				switch (sizeLeft) {
				case 1: return ary[index];
				case 2: return (uint)(ary[index] | (ary[index + 1] << 8));
				case 3: return (uint)(ary[index] | (ary[index + 1] << 8) | (ary[index + 2] << 16));
				default: throw new ApplicationException("Can't read data");
				}
			}

			static void WriteUInt32(byte[] ary, int index, uint value) {
				int sizeLeft = ary.Length - index;
				if (sizeLeft >= 1)
					ary[index] = (byte)value;
				if (sizeLeft >= 2)
					ary[index + 1] = (byte)(value >> 8);
				if (sizeLeft >= 3)
					ary[index + 2] = (byte)(value >> 16);
				if (sizeLeft >= 4)
					ary[index + 3] = (byte)(value >> 24);
			}

			public byte[] Encrypt(byte[] data) {
				//TODO: Support re-encryption
				Logger.e("Re-encryption is not supported. Assembly will probably crash at runtime.");
				return (byte[])data.Clone();
			}
		}

		class DecrypterV3 : IDecrypter {
			readonly MethodDef method;
			List<Instruction> instructions;
			readonly List<Local> locals;
			readonly InstructionEmulator instrEmulator = new InstructionEmulator();
			Local emuLocal;

			public DnrDecrypterType DecrypterType => DnrDecrypterType.V3;

			public DecrypterV3(MethodDef method) {
				this.method = method;
				locals = new List<Local>(method.Body.Variables);
				if (!Initialize())
					throw new ApplicationException("Could not initialize decrypter");
			}

			public static bool CouldBeResourceDecrypter(MethodDef method, LocalTypes localTypes, IList<string> additionalTypes) {
				var requiredTypes = new List<string> {
					"System.Reflection.Emit.DynamicMethod",
					"System.Reflection.Emit.ILGenerator",
				};
				requiredTypes.AddRange(additionalTypes);
				if (!localTypes.All(requiredTypes))
					return false;

				return true;
			}

			bool Initialize() {
				var origInstrs = method.Body.Instructions;

				if (!Find(origInstrs, out int emuStartIndex, out int emuEndIndex, out emuLocal)) {
					if (!FindStartEnd(origInstrs, out emuStartIndex, out emuEndIndex, out emuLocal))
						return false;
				}

				int count = emuEndIndex - emuStartIndex + 1;
				instructions = new List<Instruction>(count);
				for (int i = 0; i < count; i++)
					instructions.Add(origInstrs[emuStartIndex + i].Clone());

				return true;
			}

			bool Find(IList<Instruction> instrs, out int startIndex, out int endIndex, out Local tmpLocal) {
				startIndex = 0;
				endIndex = 0;
				tmpLocal = null;

				if (!FindStart(instrs, out int emuStartIndex, out emuLocal))
					return false;
				if (!FindEnd(instrs, emuStartIndex, out int emuEndIndex))
					return false;
				startIndex = emuStartIndex;
				endIndex = emuEndIndex;
				tmpLocal = emuLocal;
				return true;
			}

			/**
			 * Locates the instructions that are responsible for producing the key stream for decrypting the resource.
			 */
			bool FindStartEnd(IList<Instruction> instrs, out int startIndex, out int endIndex, out Local tmpLocal) {
				for (int i = 0; i + 8 < instrs.Count; i++) {
					if (instrs[i].OpCode.Code != Code.Conv_R_Un)
						continue;
					if (instrs[i + 1].OpCode.Code != Code.Conv_R8)
						continue;
					if (instrs[i + 2].OpCode.Code != Code.Conv_U4)
						continue;
					if (instrs[i + 3].OpCode.Code != Code.Add)
						continue;
					int newEndIndex = i + 3;

					int newStartIndex = -1;
					for (int j = newEndIndex; j >= 0; j--) {
						// Search upwards for array access.
						if (instrs[j].OpCode.Code != Code.Ldelem_U1)
							continue;

						// Go down to next local load, where actual decryption should begin.
						for (int k = j + 1; k < newEndIndex; k++) {
							if (instrs[k].IsLdloc()) {
								newStartIndex = k;
								break;
							}
						}
						break;
					}

					endIndex = newEndIndex;
					startIndex = newStartIndex;
					tmpLocal = CheckLocal(instrs[startIndex], true);
					return true;
				}
				endIndex = 0;
				startIndex = 0;
				tmpLocal = null;
				return false;
			}

			bool FindStart(IList<Instruction> instrs, out int startIndex, out Local tmpLocal) {
				for (int i = 0; i + 8 < instrs.Count; i++) {
					if (instrs[i].OpCode.Code != Code.Conv_U)
						continue;
					if (instrs[i + 1].OpCode.Code != Code.Ldelem_U1)
						continue;
					if (instrs[i + 2].OpCode.Code != Code.Or)
						continue;
					if (CheckLocal(instrs[i + 3], false) == null)
						continue;
					Local local;
					if ((local = CheckLocal(instrs[i + 4], true)) == null)
						continue;
					if (CheckLocal(instrs[i + 5], true) == null)
						continue;
					if (instrs[i + 6].OpCode.Code != Code.Add)
						continue;
					if (CheckLocal(instrs[i + 7], false) != local)
						continue;
					var instr = instrs[i + 8];
					int newStartIndex = i + 8;
					if (instr.IsBr()) {
						instr = instr.Operand as Instruction;
						newStartIndex = instrs.IndexOf(instr);
					}
					if (newStartIndex < 0 || instr == null)
						continue;
					if (CheckLocal(instr, true) != local)
						continue;

					startIndex = newStartIndex;
					tmpLocal = local;
					return true;
				}

				startIndex = 0;
				tmpLocal = null;
				return false;
			}

			bool FindEnd(IList<Instruction> instrs, int startIndex, out int endIndex) {
				for (int i = startIndex; i < instrs.Count; i++) {
					var instr = instrs[i];
					if (instr.OpCode.FlowControl != FlowControl.Next)
						break;
					if (instr.IsStloc() && instr.GetLocal(locals) == emuLocal) {
						endIndex = i - 1;
						return true;
					}
				}

				endIndex = 0;
				return false;
			}

			/**
			 * Gets the Local referenced by the instruction if it's either ldloc or stloc (determined by isLdloc).
			 */
			Local CheckLocal(Instruction instr, bool isLdloc) {
				if (isLdloc && !instr.IsLdloc())
					return null;
				if (!isLdloc && !instr.IsStloc())
					return null;

				return instr.GetLocal(locals);
			}

			public byte[] Decrypt(EmbeddedResource resource) {
				var encrypted = resource.CreateReader().ToArray();
				var decrypted = new byte[encrypted.Length];

				uint sum = 0;
				for (int i = 0; i < encrypted.Length; i += 4) {
					sum = CalculateMagic(sum);
					WriteUInt32(decrypted, i, sum ^ ReadUInt32(encrypted, i));
				}

				return decrypted;
			}

			uint CalculateMagic(uint input) {
				instrEmulator.Initialize(method, method.Parameters, locals, method.Body.InitLocals, false);
				instrEmulator.SetLocal(emuLocal, new Int32Value((int)input));

				foreach (var instr in instructions)
					instrEmulator.Emulate(instr);

				var tos = instrEmulator.Pop() as Int32Value;
				if (tos == null || !tos.AllBitsValid())
					throw new ApplicationException("Couldn't calculate magic value");
				return (uint)tos.Value;
			}

			static uint ReadUInt32(byte[] ary, int index) {
				int sizeLeft = ary.Length - index;
				if (sizeLeft >= 4)
					return BitConverter.ToUInt32(ary, index);
				switch (sizeLeft) {
				case 1: return ary[index];
				case 2: return (uint)(ary[index] | (ary[index + 1] << 8));
				case 3: return (uint)(ary[index] | (ary[index + 1] << 8) | (ary[index + 2] << 16));
				default: throw new ApplicationException("Can't read data");
				}
			}

			static void WriteUInt32(byte[] ary, int index, uint value) {
				int sizeLeft = ary.Length - index;
				if (sizeLeft >= 1)
					ary[index] = (byte)value;
				if (sizeLeft >= 2)
					ary[index + 1] = (byte)(value >> 8);
				if (sizeLeft >= 3)
					ary[index + 2] = (byte)(value >> 16);
				if (sizeLeft >= 4)
					ary[index + 3] = (byte)(value >> 24);
			}

			public byte[] Encrypt(byte[] data) {
				//TODO: Support re-encryption
				Logger.e("Re-encryption is not supported. Assembly will probably crash at runtime.");
				return (byte[])data.Clone();
			}
		}

		class DecrypterV4 : IDecrypter {
			readonly byte[] key, iv;
			MethodDef decryptMethod;
			MethodDef emuMethod;
			List<Instruction> instructions;
			List<Local> locals;
			readonly InstructionEmulator instrEmulator = new InstructionEmulator();
			Local emuLocal;

			public DnrDecrypterType DecrypterType => DnrDecrypterType.V4;

			public DecrypterV4(ModuleDefMD module, ISimpleDeobfuscator simpleDeobfuscator, MethodDef origMethod) {
				if (!FindDecrypterMethod(origMethod))
					throw new ApplicationException("Could not find decrypter method");
				simpleDeobfuscator.Deobfuscate(decryptMethod);

				if (!FindEmulateMethod(decryptMethod))
					throw new ApplicationException("Could not find emulate method");
				simpleDeobfuscator.Deobfuscate(emuMethod);

				key = ArrayFinder.GetInitializedByteArray(decryptMethod, 32);
				if (key == null)
					throw new ApplicationException("Could not find resource decrypter key");
				iv = ArrayFinder.GetInitializedByteArray(decryptMethod, 16);
				if (iv == null)
					throw new ApplicationException("Could not find resource decrypter IV");
				if (NeedReverse())
					Array.Reverse(iv);  // DNR 4.5.0.0
				if (UsesPublicKeyToken()) {
					var publicKeyToken = module.Assembly.PublicKeyToken;
					if (publicKeyToken != null && publicKeyToken.Data.Length > 0) {
						for (int i = 0; i < 8; i++)
							iv[i * 2 + 1] = publicKeyToken.Data[i];
					}
				}

				locals = new List<Local>(emuMethod.Body.Variables);
				if (!Initialize())
					throw new ApplicationException("Could not initialize decrypter");
			}
	
			public static bool CouldBeResourceDecrypter(MethodDef method, LocalTypes localTypes, IList<string> additionalTypes) {
				var requiredTypes = new List<string> {
					"System.Int32",
					"System.Byte[]",
				};
				requiredTypes.AddRange(additionalTypes);
				if (!localTypes.All(requiredTypes))
					return false;

				var instrs = method.Body.Instructions;

				foreach (var instr in instrs) {
					if (instr.OpCode != OpCodes.Newobj)
						continue;

					if (instr.Operand is IMethod newObj 
					    && newObj.FullName == "System.Void System.Diagnostics.StackFrame::.ctor(System.Int32)")
						return true;
				}

				return false;
			}

			bool FindDecrypterMethod(MethodDef method) {
				var instrs = method.Body.Instructions;
				for (var i = 0; i < instrs.Count; i++) {
					if (instrs[i].OpCode != OpCodes.Ldsfld)
						continue;
					if (instrs[i + 1].OpCode != OpCodes.Ldstr)
						continue;
					if (instrs[i + 2].OpCode != OpCodes.Callvirt)
						continue;
					if (instrs[i + 3].OpCode != OpCodes.Ldarg_0)
						continue;
					var call = instrs[i + 4];
					if (call.OpCode != OpCodes.Call)
						continue;

					decryptMethod = call.Operand as MethodDef;
					return true;
				}

				return false;
			}

			bool FindEmulateMethod(MethodDef method) {
				var instrs = method.Body.Instructions;
				for (var i = 0; i < instrs.Count; i++) {
					if (instrs[i].OpCode != OpCodes.Newobj)
						continue;
					if (!instrs[i + 1].IsLdloc())
						continue;
					if (!instrs[i + 2].IsLdloc())
						continue;
					if (!instrs[i + 3].IsLdloc())
						continue;
					var call = instrs[i + 4];
					if (call.OpCode != OpCodes.Call)
						continue;

					emuMethod = call.Operand as MethodDef;
					return true;
				}

				return false;
			}

			bool Initialize() {
				var origInstrs = emuMethod.Body.Instructions;

				if (!Find(origInstrs, out int emuStartIndex, out int emuEndIndex, out emuLocal)) {
					if (!FindStartEnd(origInstrs, out emuStartIndex, out emuEndIndex, out emuLocal))
							return false;
				}

				for (int i = 0; i < iv.Length; i++)
					key[i] ^= iv[i];

				int count = emuEndIndex - emuStartIndex + 1;
				instructions = new List<Instruction>(count);
				for (int i = 0; i < count; i++)
					instructions.Add(origInstrs[emuStartIndex + i].Clone());

				return true;
			}

			bool Find(IList<Instruction> instrs, out int startIndex, out int endIndex, out Local tmpLocal) {
				startIndex = 0;
				endIndex = 0;
				tmpLocal = null;

				if (!FindStart(instrs, out int emuStartIndex, out emuLocal))
					return false;
				if (!FindEnd(instrs, emuStartIndex, out int emuEndIndex))
					return false;
				startIndex = emuStartIndex;
				endIndex = emuEndIndex;
				tmpLocal = emuLocal;
				return true;
			}

			bool FindStartEnd(IList<Instruction> instrs, out int startIndex, out int endIndex, out Local tmpLocal) {
				for (int i = 0; i + 8 < instrs.Count; i++) {
					if (instrs[i].OpCode.Code != Code.Conv_R_Un)
						continue;
					if (instrs[i + 1].OpCode.Code != Code.Conv_R8)
						continue;
					if (instrs[i + 2].OpCode.Code != Code.Conv_U4)
						continue;
					if (instrs[i + 3].OpCode.Code != Code.Add)
						continue;
					int newEndIndex = i + 3;
					int newStartIndex = -1;
					for (int j = newEndIndex; j >= 0; j--) {
						// Search upwards for array access.
						if (instrs[j].OpCode.Code != Code.Ldelem_U1)
							continue;

						// Go down to next local load, where actual decryption should begin.
						for (int k = j + 1; k < newEndIndex; k++) {
							if (instrs[k].IsLdloc()) {
								newStartIndex = k;
								break;
							}
						}
						break;
					}
					endIndex = newEndIndex;
					startIndex = newStartIndex;
					tmpLocal = CheckLocal(instrs[startIndex], true);
					return true;
				}
				endIndex = 0;
				startIndex = 0;
				tmpLocal = null;
				return false;
			}

			bool FindStart(IList<Instruction> instrs, out int startIndex, out Local tmpLocal) {
				for (int i = 0; i + 8 < instrs.Count; i++) {
					if (instrs[i].OpCode.Code != Code.Conv_U)
						continue;
					if (instrs[i + 1].OpCode.Code != Code.Ldelem_U1)
						continue;
					if (instrs[i + 2].OpCode.Code != Code.Or)
						continue;
					if (CheckLocal(instrs[i + 3], false) == null)
						continue;
					Local local;
					if ((local = CheckLocal(instrs[i + 4], true)) == null)
						continue;
					if (CheckLocal(instrs[i + 5], true) == null)
						continue;
					if (instrs[i + 6].OpCode.Code != Code.Add)
						continue;
					if (CheckLocal(instrs[i + 7], false) != local)
						continue;
					var instr = instrs[i + 8];
					int newStartIndex = i + 8;
					if (instr.IsBr()) {
						instr = instr.Operand as Instruction;
						newStartIndex = instrs.IndexOf(instr);
					}
					if (newStartIndex < 0 || instr == null)
						continue;
					if (CheckLocal(instr, true) != local)
						continue;

					startIndex = newStartIndex;
					tmpLocal = local;
					return true;
				}

				startIndex = 0;
				tmpLocal = null;
				return false;
			}

			bool FindEnd(IList<Instruction> instrs, int startIndex, out int endIndex) {
				for (int i = startIndex; i < instrs.Count; i++) {
					var instr = instrs[i];
					if (instr.OpCode.FlowControl != FlowControl.Next)
						break;
					if (instr.IsStloc() && instr.GetLocal(locals) == emuLocal) {
						endIndex = i - 1;
						return true;
					}
				}

				endIndex = 0;
				return false;
			}

			Local CheckLocal(Instruction instr, bool isLdloc) {
				if (isLdloc && !instr.IsLdloc())
					return null;
				else if (!isLdloc && !instr.IsStloc())
					return null;

				return instr.GetLocal(locals);
			}

			public byte[] Decrypt(EmbeddedResource resource) {
				var encrypted = resource.CreateReader().ToArray();
				var decrypted = new byte[encrypted.Length];

				uint sum = 0;
				for (int i = 0; i < encrypted.Length; i += 4) {
					sum = CalculateMagic(sum + ReadUInt32(key, i % key.Length));
					WriteUInt32(decrypted, i, sum ^ ReadUInt32(encrypted, i));
				}

				return decrypted;
			}

			uint CalculateMagic(uint input) {
				instrEmulator.Initialize(emuMethod, emuMethod.Parameters, locals, emuMethod.Body.InitLocals, false);
				instrEmulator.SetLocal(emuLocal, new Int32Value((int)input));

				foreach (var instr in instructions)
					instrEmulator.Emulate(instr);

				var tos = instrEmulator.Pop() as Int32Value;
				if (tos == null || !tos.AllBitsValid())
					throw new ApplicationException("Couldn't calculate magic value");
				return (uint)tos.Value;
			}

			static uint ReadUInt32(byte[] ary, int index) {
				int sizeLeft = ary.Length - index;
				if (sizeLeft >= 4)
					return BitConverter.ToUInt32(ary, index);
				switch (sizeLeft) {
				case 1: return ary[index];
				case 2: return (uint)(ary[index] | (ary[index + 1] << 8));
				case 3: return (uint)(ary[index] | (ary[index + 1] << 8) | (ary[index + 2] << 16));
				default: throw new ApplicationException("Can't read data");
				}
			}

			static void WriteUInt32(byte[] ary, int index, uint value) {
				int sizeLeft = ary.Length - index;
				if (sizeLeft >= 1)
					ary[index] = (byte)value;
				if (sizeLeft >= 2)
					ary[index + 1] = (byte)(value >> 8);
				if (sizeLeft >= 3)
					ary[index + 2] = (byte)(value >> 16);
				if (sizeLeft >= 4)
					ary[index + 3] = (byte)(value >> 24);
			}

			public byte[] Encrypt(byte[] data) {
				//TODO: Support re-encryption
				Logger.e("Re-encryption is not supported. Assembly will probably crash at runtime.");
				return (byte[])data.Clone();
			}

			bool UsesPublicKeyToken() {
				int pktIndex = 0;
				foreach (var instr in decryptMethod.Body.Instructions) {
					if (instr.OpCode.FlowControl != FlowControl.Next) {
						pktIndex = 0;
						continue;
					}
					if (!instr.IsLdcI4())
						continue;
					int val = instr.GetLdcI4Value();
					if (val != pktIndexes[pktIndex++]) {
						pktIndex = 0;
						continue;
					}
					if (pktIndex >= pktIndexes.Length)
						return true;
				}
				return false;
			}

			bool NeedReverse() => DotNetUtils.CallsMethodContains(decryptMethod, "System.Array::Reverse");
		}

		public byte[] Decrypt() {
			if (encryptedDataResource == null || decrypter == null)
				throw new ApplicationException("Can't decrypt resource");
			return decrypter.Decrypt(encryptedDataResource);
		}

		public byte[] Encrypt(byte[] data) {
			if (decrypter == null)
				throw new ApplicationException("Can't encrypt resource");
			return decrypter.Encrypt(data);
		}
	}
}
