using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using de4dot.blocks;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace de4dot.code.deobfuscators.MasonProtector;

public class ResourceDecrypter2 : IResourceDecrypter {
	readonly ModuleDefMD _module;
	readonly ISimpleDeobfuscator _methodDeobfuscator;

	MethodDef _decrypterMethod;
	public MethodDef VarsInitMethod { get; private set; }

	string[] _resourceNames;
	byte[] _array0, _array1;
	CiphertextTransform _extraTransform;

	public bool Found => VarsInitMethod != null && _decrypterMethod != null;
	public IList<MethodDef> MethodsToRemove { get; private set; } = Array.Empty<MethodDef>();

	public ResourceDecrypter2(ModuleDefMD module, ISimpleDeobfuscator methodDeobfuscator) {
		_module = module;
		_methodDeobfuscator = methodDeobfuscator;
	}

	public void Find() {
		foreach (var type in _module.Types) {
			foreach (var method in type.Methods) {
				if (!method.IsStatic)
					continue;
				if (!DotNetUtils.IsMethod(method, "System.IO.Stream", "(System.Reflection.Assembly,System.String)"))
					continue;
				if (!DotNetUtils.CallsMethod(method, "System.IO.Stream System.Reflection.Assembly::GetManifestResourceStream(System.String)"))
					continue;

				var ldins = method.Body.Instructions.FirstOrDefault(ins => ins.OpCode == OpCodes.Ldsfld
				                                                           && ins.Operand is FieldDef def
				                                                           && def.FieldType.FullName == "System.String[]");
				if (ldins == null)
					continue;

				foreach (var initMethod in type.Methods) {
					if (!initMethod.IsStatic || !initMethod.HasBody) continue;
					if (!DotNetUtils.IsMethod(initMethod, "System.Void", "()")) continue;
					if (!initMethod.Body.Instructions.Any(ins =>
						    ins.OpCode == OpCodes.Stsfld && ins.Operand == ldins.Operand)) continue;
					if (!DotNetUtils.CallsMethod(initMethod,
						    "System.Void System.Runtime.CompilerServices.RuntimeHelpers::InitializeArray(System.Array,System.RuntimeFieldHandle)"))
						return; // either the older variant (ResourceDecrypter) or something unknown

					VarsInitMethod = initMethod;
					break;
				}
				_decrypterMethod = method;
				return;
			}
		}
	}

	public void Initialize(IDeobfuscator deob) {
		if (!Found)
			throw new InvalidOperationException("Must not call Initialize() when Found is false");

		_methodDeobfuscator.Deobfuscate(VarsInitMethod);
		_methodDeobfuscator.DecryptStrings(VarsInitMethod, deob);

		var theFields = VarsInitMethod.Body.Instructions
			.Where(ins => ins.OpCode == OpCodes.Stsfld)
			.Select(ins => (FieldDef)ins.Operand)
			.Where(field => field.FieldType.FullName == "System.Byte[]");

		var aEx = new ArrayExtractor(VarsInitMethod, _module.Mvid!.Value.ToByteArray());
		int i = 0;
		foreach (var field in theFields) {
			if (!aEx.Run(field, aEx.InstructionIndex)) {
				throw new Exception("Run failed");
			}
			var data = aEx.GetArrayValues<byte>();
			if (data == null)
				throw new Exception("Emulation didn't yield data");

			if (i == 0) _array0 = data;
			else if (i == 1) _array1 = data;
			else break;
			i++;
		}

		if (_array1 == null)
			throw new Exception("Couldn't find both arrays");

		int arrIndex = aEx.InstructionIndex;
		if (!ArrayFinder.FindNewarr(VarsInitMethod, ref arrIndex, out int arrSize))
			throw new Exception("newarr for strings not found");
		var arr = ArrayFinder.GetInitializedByteArray(arrSize, VarsInitMethod, ref arrIndex);
		if (arr == null)
			throw new Exception("Unable to obtain resource name data");

		_resourceNames = new string[BitConverter.ToInt32(arr, 0)];
		int offset = 4;
		for (int s = 0; s < _resourceNames.Length; s++) {
			int strLen = BitConverter.ToInt32(arr, offset);
			offset += 4;
			_resourceNames[s] = Encoding.UTF8.GetString(arr, offset, strLen);
			offset += strLen;
		}

		var extra = _decrypterMethod.Body.Instructions.FirstOrDefault(ins =>
			ins.OpCode == OpCodes.Call && ins.Operand is MethodDef md && md.DeclaringType == _decrypterMethod.DeclaringType);
		if (extra != null)
			_extraTransform = CiphertextTransform.Analyze((MethodDef)extra.Operand);
	}

	public void DecryptResources() {
		foreach (var res in _resourceNames) {
			if (DotNetUtils.GetResource(_module, res) is not EmbeddedResource rsrc)
				throw new Exception($"Resource {res} not found");

			var data = rsrc.CreateReader().ToArray();
			//Console.WriteLine("Handling " + res);

			byte[] array2 = _array0;
			byte[] array3 = _array1;
			byte[] bytes = Encoding.UTF8.GetBytes(res);
			byte[] array4 = new byte[32 + array3.Length + bytes.Length];
			Array.Copy(array2, 0, array4, 0, 32);
			Array.Copy(array3, 0, array4, 32, array3.Length);
			Array.Copy(bytes, 0, array4, 32 + array3.Length, bytes.Length);
			var sha = SHA256.Create();
			byte[] array5 = sha.ComputeHash(array4);
			array4 = new byte[array5.Length + bytes.Length + 32];
			Array.Copy(array5, 0, array4, 0, array5.Length);
			Array.Copy(bytes, 0, array4, array5.Length, bytes.Length);
			Array.Copy(array2, 0, array4, array5.Length + bytes.Length, 32);
			sha = SHA256.Create();
			byte[] array6 = sha.ComputeHash(array4);
			array4 = new byte[array3.Length + array6.Length + array5.Length];
			Array.Copy(array3, 0, array4, 0, array3.Length);
			Array.Copy(array6, 0, array4, array3.Length, array6.Length);
			Array.Copy(array5, 0, array4, array3.Length + array6.Length, array5.Length);
			sha = SHA256.Create();
			byte[] array7 = sha.ComputeHash(array4);
			if (data.Length <= 16) {
				throw new Exception();
			}
			byte[] array8 = new byte[16];
			Array.Copy(data, array8, 16);

			byte[] ciphertext = new byte[data.Length - 16];
			Array.Copy(data, 16, ciphertext, 0, data.Length - 16);
			_extraTransform?.TransformArray(ciphertext);

			var aes = Aes.Create();
			aes.Mode = CipherMode.CBC;
			aes.Padding = PaddingMode.PKCS7;
			aes.Key = array7;
			aes.IV = array8;
			var cryptoTransform = aes.CreateDecryptor();
			byte[] decrypted = cryptoTransform.TransformFinalBlock(ciphertext, 0, ciphertext.Length);
			byte[] mvid = _module.Mvid!.Value.ToByteArray();
			array4 = new byte[mvid.Length + array3.Length + bytes.Length];
			Array.Copy(mvid, 0, array4, 0, mvid.Length);
			Array.Copy(array3, 0, array4, mvid.Length, array3.Length);
			Array.Copy(bytes, 0, array4, mvid.Length + array3.Length, bytes.Length);
			sha = SHA256.Create();
			byte[] array12 = sha.ComputeHash(array4);
			int j = 0;
			int num2 = 0;
			while (j < decrypted.Length) {
				array4 = new byte[array12.Length + 4];
				Array.Copy(array12, 0, array4, 0, array12.Length);
				array4[array12.Length] = (byte)num2;
				array4[array12.Length + 1] = (byte)((uint)num2 >> 8);
				array4[array12.Length + 2] = (byte)((uint)num2 >> 16);
				array4[array12.Length + 3] = (byte)((uint)num2 >> 24);
				sha = SHA256.Create();
				byte[] array13 = sha.ComputeHash(array4);
				int num3 = decrypted.Length - j;
				if (num3 > 32) {
					num3 = 32;
				}
				for (int k = 0; k < num3; k++) {
					decrypted[j + k] = (byte)(decrypted[j + k] ^ array13[k]);
				}
				j += num3;
				num2++;
			}

			byte[] decompressedBytes;
			using (var wrapper = new MemoryStream(decrypted)) {
				using (var deflateStream = new DeflateStream(wrapper, CompressionMode.Decompress)) {
					using (var decompressed = new MemoryStream()) {
						deflateStream.CopyTo(decompressed);
						decompressedBytes = decompressed.ToArray();
					}
				}
			}

			_module.Resources.Remove(rsrc);
			_module.Resources.Add(new EmbeddedResource(rsrc.Name, decompressedBytes, rsrc.Attributes));
		}

		// Replace decrypt call with plain Get()
		var gmrs = _module.GetMemberRefs().First(mr => mr.Name == "GetManifestResourceStream");
		foreach (var type in _module.GetTypes()) {
			foreach (var method in type.Methods) {
				if (!method.HasBody) continue;
				foreach (var ins in method.Body.Instructions) {
					if (ins.OpCode == OpCodes.Call && ins.Operand == _decrypterMethod) {
						ins.Operand = gmrs;
					}
				}
			}
		}

		MethodsToRemove = new List<MethodDef> { _decrypterMethod, VarsInitMethod };
	}
}
