using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using de4dot.blocks;
using de4dot.blocks.cflow;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace de4dot.code.deobfuscators.MasonProtector;

public interface IResourceDecrypter {
	public void Find();
	public void Initialize(IDeobfuscator deob);
	public void DecryptResources();

	public bool Found { get; }
	public IList<MethodDef> MethodsToRemove { get; }
	public MethodDef VarsInitMethod { get; }
}

public class ResourceDecrypter : IResourceDecrypter {
	readonly ModuleDefMD _module;
	readonly ISimpleDeobfuscator _methodDeobfuscator;

	MethodDef _decrypterMethod;
	public MethodDef VarsInitMethod { get; private set; }

	string[] _resourceNames;
	byte[] _key, _iv;
	byte _xorKey;

	public bool Found => VarsInitMethod != null && _decrypterMethod != null;
	public IList<MethodDef> MethodsToRemove { get; private set; } = Array.Empty<MethodDef>();

	public ResourceDecrypter(ModuleDefMD module, ISimpleDeobfuscator methodDeobfuscator) {
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
					if (DotNetUtils.CallsMethod(initMethod,
						    "System.Void System.Runtime.CompilerServices.RuntimeHelpers::InitializeArray(System.Array,System.RuntimeFieldHandle)"))
						return; // then it's the newer variant, handled by ResourceDecrypter2
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

		int arrIndex = 0;
		var instrs = VarsInitMethod.Body.Instructions;
		while (ArrayFinder.FindNewarr(VarsInitMethod, ref arrIndex, out int arrSize)) {
			var cts = _module.CorLibTypes.GetCorLibTypeSig((ITypeDefOrRef)instrs[arrIndex].Operand);
			if (cts == null)
				continue;
			if (cts.ElementType == ElementType.U1) {
				var ba = ArrayFinder.GetInitializedByteArray(arrSize, VarsInitMethod, ref arrIndex);
				if (arrSize == 32)
					_key = ba;
				else if (arrSize == 16)
					_iv = ba;
			}
			else if (cts.ElementType == ElementType.String) {
				var sa = ArrayFinder.GetInitializedArray(arrSize, VarsInitMethod, ref arrIndex, Code.Stelem_Ref);
				_resourceNames = sa.OfType<StringValue>().Select(v => v.value).ToArray();
			}
			else {
				arrIndex++;
			}
		}

		bool hasXorKey = false;
		for (int i = 0; i < VarsInitMethod.Body.Instructions.Count; i++) {
			var ins = VarsInitMethod.Body.Instructions[i];
			if (ins.OpCode == OpCodes.Stsfld && VarsInitMethod.Body.Instructions[i - 1].IsLdcI4()) {
				_xorKey = (byte)VarsInitMethod.Body.Instructions[i - 1].GetLdcI4Value();
				hasXorKey = true;
			}
		}

		if (_key == null || _iv == null || _resourceNames == null || !hasXorKey)
			throw new Exception($"Not all Mason resource details found (key: {_key != null}, iv: {_iv != null}, names: {_resourceNames != null}, xorKey: {hasXorKey})");
	}

	public void DecryptResources() {
		foreach (var res in _resourceNames) {
			if (DotNetUtils.GetResource(_module, res) is not EmbeddedResource rsrc)
				throw new Exception($"Resource {res} not found");

			var data = rsrc.CreateReader().ToArray();
			for (int i = 0; i < data.Length; i++) {
				data[i] ^= (byte)(_xorKey ^ (i & 255));
			}

			byte[] decrypted;
			using (var aes = Aes.Create()) {
				aes.Mode = CipherMode.CBC;
				aes.Padding = PaddingMode.PKCS7;
				aes.Key = _key;
				aes.IV = _iv;
				using (var cryptoTransform = aes.CreateDecryptor())
					decrypted = cryptoTransform.TransformFinalBlock(data, 0, data.Length);
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
