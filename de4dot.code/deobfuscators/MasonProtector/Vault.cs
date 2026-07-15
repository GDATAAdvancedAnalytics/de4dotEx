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
using dnlib.IO;

namespace de4dot.code.deobfuscators.MasonProtector;

public class Vault {
	readonly ModuleDefMD _module;
	readonly ISimpleDeobfuscator _methodDeobfuscator;

	public TypeDef VaultType { get; private set; }
	public Resource VaultResource { get; private set; }
	MethodDef _runMethod;

	int[] _decryptedTable;
	byte[] _decryptedBlob;
	int _intFromCctor;
	byte[] _bytesFromCctor;
	int _numBlockCopy;
	CiphertextTransform _extraTransform;
	byte[] _extraHmacKey;
	bool _complicatedDerive;

	readonly TokenMapper _tokenMapper = new();

	public bool CanRemove { get; private set; }
	public bool Found => VaultType != null;

	public Vault(ModuleDefMD module, ISimpleDeobfuscator methodDeobfuscator) {
		_module = module;
		_methodDeobfuscator = methodDeobfuscator;
	}

	public void Find() {
		foreach (var type in _module.Types) {
			foreach (var method in type.Methods) {
				if (!method.IsStatic)
					continue;
				if (!DotNetUtils.IsMethod(method, "System.Void", "()"))
					continue;
				if (!DotNetUtils.CallsMethod(method, "System.IO.Stream System.Reflection.Assembly::GetManifestResourceStream(System.String)"))
					continue;
				if (!DotNetUtils.CallsMethod(method, "System.Byte[] System.Security.Cryptography.HashAlgorithm::ComputeHash(System.Byte[])"))
					continue;
				if (!DotNetUtils.CallsMethod(method,"System.Byte[] System.Security.Cryptography.ICryptoTransform::TransformFinalBlock(System.Byte[],System.Int32,System.Int32)"))
					continue;
				if (!DotNetUtils.CallsMethod(method,"System.Void System.IO.Compression.DeflateStream::.ctor(System.IO.Stream,System.IO.Compression.CompressionMode)"))
					continue;
				if (!DotNetUtils.CallsMethod(method,"System.Void System.Array::Clear(System.Array,System.Int32,System.Int32)"))
					continue;

				_numBlockCopy = DotNetUtils.GetMethodCalls(method,
					"System.Void System.Buffer::BlockCopy(System.Array,System.Int32,System.Array,System.Int32,System.Int32)");
				_complicatedDerive = !DotNetUtils.CallsMethod(method, "System.Byte[] System.Security.Cryptography.DeriveBytes::GetBytes(System.Int32)");

				VaultType = type;
				return;
			}
		}
	}

	public void Initialize(IDeobfuscator deob) {
		if (VaultType == null)
			throw new InvalidOperationException("Vault type was not found");

		_runMethod = VaultType.Methods.FirstOrDefault(m => m.IsStatic
		    && DotNetUtils.IsMethod(m, "System.Object", "(System.Int32,System.RuntimeMethodHandle,System.Object[])"));
		if (_runMethod == null)
			throw new Exception("Vault run method not found");

		var vaultCctor = VaultType.FindStaticConstructor();
		if (vaultCctor == null)
			throw new Exception("Vault does not have cctor");

		_methodDeobfuscator.Deobfuscate(vaultCctor);
		_methodDeobfuscator.DecryptStrings(vaultCctor, deob);

		if (DotNetUtils.GetResource(_module, DotNetUtils.GetCodeStrings(vaultCctor)) is not EmbeddedResource rsrc)
			throw new Exception("Vault resource not found");

		var insns = vaultCctor.Body.Instructions;
		int i, stlocHmacKey = -1;
		for (i = 1; i < insns.Count - 2; i++) {
			var insn = insns[i];
			if (insn.OpCode == OpCodes.Stsfld && insns[i - 1].IsLdcI4()) {
				_intFromCctor = insns[i - 1].GetLdcI4Value();
			}
			else if (insn.OpCode == OpCodes.Ldtoken && insn.Operand is FieldDef fieldDef
			                                        && fieldDef.Attributes.HasFlag(FieldAttributes.HasFieldRVA)) {
				_bytesFromCctor = fieldDef.InitialValue;
				if (insns[i + 2].IsStloc())
					stlocHmacKey = i + 2;
			}
			if (_intFromCctor != 0 && _bytesFromCctor != null)
				break;
		}

		if (_intFromCctor == 0 || _bytesFromCctor == null)
			throw new Exception("Required data not found in vault cctor");

		// Newer versions of the protector do some transformations involving the ModuleVersionId.
		if (stlocHmacKey != -1 && _module.Mvid.HasValue) {
			int stlocMvid = -1;
			for (i = stlocHmacKey + 1; i < Math.Min(stlocHmacKey + 15, insns.Count); i++) {
				if (insns[i].OpCode == OpCodes.Call && insns[i].Operand is IMethod { FullName: "System.Byte[] System.Guid::ToByteArray()" }
				                                    && insns[i + 1].IsStloc()) {
					stlocMvid = i + 1;
					break;
				}
			}

			if (stlocMvid != -1) {
				var emu = new HmacKeyEmu(vaultCctor);
				emu.Emulator.SetLocal((Local)insns[stlocHmacKey].Operand, HmacKeyEmu.ByteArrayToValue(_bytesFromCctor));
				emu.Emulator.SetLocal((Local)insns[stlocMvid].Operand, HmacKeyEmu.ByteArrayToValue(_module.Mvid.Value.ToByteArray()));
				emu.Run(stlocMvid + 1);
				var arrayValue = emu.Emulator.Pop();
				if (arrayValue is ObjectValue { obj: List<Value> vals } && vals.All(v => v is Int32Value i32 && i32.AllBitsValid())) {
					_bytesFromCctor = vals.Select(v => (byte)((Int32Value)v).Value).ToArray();
				}
				else {
					Logger.w("ModuleVersionId-based HmacKey transforms failed");
				}
			}
		}

		FindExtraHmacKeyProtection();

		for (i = 0; i < insns.Count - 2; i++) {
			var insn = insns[i];
			if (insn.OpCode == OpCodes.Ldftn && insns[i + 2].OpCode == OpCodes.Stsfld
			                                 && insn.Operand is MethodDef md) {
				_extraTransform = CiphertextTransform.Analyze(md);
				break;
			}
		}

		WalkCctor(vaultCctor, deob);
		DecryptResource(rsrc.CreateReader().ToArray());

		VaultResource = rsrc;
	}

	void DecryptResource(byte[] rsrcBlob) {
		if (rsrcBlob.Length < 65)
			throw new InvalidOperationException("Resource too short: " + rsrcBlob.Length);

		byte[] salt = new byte[16];
		Buffer.BlockCopy(rsrcBlob, 0, salt, 0, 16);
		byte[] ivRaw = new byte[16];
		byte[] keyRaw = new byte[32];
		Buffer.BlockCopy(rsrcBlob, 16, ivRaw, 0, 16);
		Buffer.BlockCopy(rsrcBlob, 32, keyRaw, 0, 32);
		byte b = rsrcBlob[64];
		int num = (salt[0] & 63) + 1;
		int cryptedOffset = 65 + num;
		if (_numBlockCopy > 5)
			cryptedOffset += 32;
		if (rsrcBlob.Length <= cryptedOffset)
			throw new InvalidOperationException("Resource too short: " + rsrcBlob.Length);

		int cryptedCount = rsrcBlob.Length - cryptedOffset;
		byte[] crypted = new byte[cryptedCount];
		Buffer.BlockCopy(rsrcBlob, cryptedOffset, crypted, 0, cryptedCount);

		_extraTransform?.TransformArray(crypted);

		byte[] hmacKey = new byte[_bytesFromCctor.Length];
		Buffer.BlockCopy(_bytesFromCctor, 0, hmacKey, 0, hmacKey.Length);
		if (_extraHmacKey != null)
			for (int i = 0; i < hmacKey.Length; i++)
				hmacKey[i] ^= _extraHmacKey[i % _extraHmacKey.Length];

		byte[] password;
		using (var hmacsha = new HMACSHA256(hmacKey))
			password = hmacsha.ComputeHash(salt);

		byte[] bytes;
		if (_complicatedDerive) {
			bytes = ComplicatedDerive(password, salt, _intFromCctor);
		}
		else {
#pragma warning disable SYSLIB0041
			using var rfc2898DeriveBytes = new Rfc2898DeriveBytes(password, salt, _intFromCctor);
			bytes = rfc2898DeriveBytes.GetBytes(49);
#pragma warning restore SYSLIB0041
		}

		byte[] aesIv = new byte[16];
		byte[] aesKey = new byte[32];
		for (int i = 0; i < 16; i++)
			aesIv[i] = (byte)(ivRaw[i] ^ bytes[i]);

		for (int j = 0; j < 32; j++)
			aesKey[j] = (byte)(keyRaw[j] ^ bytes[16 + j]);

		byte cryptedXor = (byte)(b ^ bytes[48]);
		for (int k = 0; k < crypted.Length; k++)
			crypted[k] ^= (byte)(cryptedXor ^ (k & 255));

		byte[] decrypted;
		using (var aes = Aes.Create()) {
			aes.Key = aesKey;
			aes.IV = aesIv;
			aes.Mode = CipherMode.CBC;
			aes.Padding = PaddingMode.PKCS7;
			using (var cryptoTransform = aes.CreateDecryptor())
				decrypted = cryptoTransform.TransformFinalBlock(crypted, 0, crypted.Length);
		}

		byte[] blob;
		using (var wrapper = new MemoryStream(decrypted)) {
			using (var deflateStream = new DeflateStream(wrapper, CompressionMode.Decompress)) {
				using (var decompressed = new MemoryStream()) {
					deflateStream.CopyTo(decompressed);
					blob = decompressed.ToArray();
				}
			}
		}

		int tableCount = ReadInt(blob, 0);
		int tableIndex = 4;
		int[] table = new int[tableCount];
		for (int l = 0; l < tableCount; l++) {
			table[l] = ReadInt(blob, tableIndex);
			tableIndex += 4;
		}

		_decryptedTable = table;
		_decryptedBlob = blob;
	}

	static int ReadInt(byte[] array, int offset)
		=> array[offset] | (array[offset + 1] << 8) | (array[offset + 2] << 16) | (array[offset + 3] << 24);

	void Unvault(MethodDef method, int index) {
		if (index < 0 || index >= _decryptedTable.Length)
			throw new IndexOutOfRangeException("Illegal vault index: " + index);

		int start = _decryptedTable[index];
		var readerFactory = ByteArrayDataReaderFactory.Create(_decryptedBlob, null);
		var reader = readerFactory.CreateReader(start, 36);

		int ilOffset = reader.ReadInt32();
		int ilLength = reader.ReadInt32(); // 4
		int localSigOffset = reader.ReadInt32(); // 8
		int localSigLength = reader.ReadInt32(); // 12
		int maxStack = reader.ReadInt32(); // 16
		/*int tokenOffset = */reader.ReadInt32(); // contains a list of places in IL that have tokens; handled automatically by MethodBodyReader
		/*int tokenCount = */reader.ReadInt32(); // 24
		int ehOffset = reader.ReadInt32(); // 28
		int ehCount = reader.ReadInt32(); // 32

		IList<Local> theLocals;
		if (localSigLength > 0) {
			var sigDataReader = readerFactory.CreateReader(localSigOffset, localSigLength);
			var parsedSig = LocalSigReader.ReadSig(_module, _tokenMapper, sigDataReader);
			theLocals = parsedSig.Locals.Select(sig => new Local(sig)).ToArray();
		}
		else
			theLocals = Array.Empty<Local>();

		var ilReader = readerFactory.CreateReader(ilOffset, ilLength);
		var mReader = new MethodBodyReader(_module, _tokenMapper, ilReader);
		mReader.ReadBody(method, theLocals, maxStack);

		if (ehCount > 0) {
			var ehReader = readerFactory.CreateReader(ehOffset, ehCount * 28);
			mReader.ReadExceptionHandlers(ehCount, ehReader);
		}

		mReader.RestoreMethod(method);
	}

	public void UnvaultAll() {
		int numFound = 0, numProcessed = 0;
		foreach (var type in _module.GetTypes()) {
			foreach (var method in type.Methods) {
				if (!method.HasBody) continue;

				if (!method.Body.Instructions.Any(i => i.OpCode == OpCodes.Call && i.Operand == _runMethod))
					continue;

				numFound++;

				_methodDeobfuscator.Deobfuscate(method);

				int callIndex = -1;
				for (int i = 0; i < method.Body.Instructions.Count; i++) {
					var instr = method.Body.Instructions[i];
					if (instr.OpCode == OpCodes.Call && instr.Operand == _runMethod) {
						callIndex = i;
						break;
					}
				}
				if (callIndex == -1)
					throw new Exception("Call lost during deobfuscation");

				var emu = new InstructionEmulator(method);
				var instrs = method.Body.Instructions;
				for (int i = 0; i < callIndex; i++)
					emu.Emulate(instrs[i]);
				emu.Pop(2);

				var indexValue = emu.Pop();
				if (indexValue is not Int32Value indexI32 || !indexI32.AllBitsValid()) {
					Logger.w("Failed to determine vault index for {0:X8}", method.MDToken.ToInt32());
					continue;
				}

				Logger.n("[Vault] Restoring index " + indexI32.Value);
				Unvault(method, indexI32.Value);
				numProcessed++;
			}
		}

		CanRemove = numFound == numProcessed;
	}

	void WalkCctor(MethodDef method, IDeobfuscator deob) {
		int arrayCounter = 0, start = 0;
		var instrs = method.Body.Instructions;

		for (int i = 0; i < instrs.Count; i++) {
			var instr = instrs[i];
			if (instr.OpCode == OpCodes.Call && instr.Operand is MethodDef { IsStatic: true } called
					&& called.DeclaringType == VaultType) {
				start = i;
				break;
			}
		}

		for (int i = start; i < instrs.Count; i++) {
			var instr = instrs[i];
			if (instr.OpCode == OpCodes.Newarr) {
				arrayCounter++;
				continue;
			}

			if (instr.OpCode == OpCodes.Call && instr.Operand is MethodDef { IsStatic: true } called) {
				AnalyzeInitializer(called, deob, arrayCounter);
			}
		}
	}

	void AnalyzeInitializer(MethodDef method, IDeobfuscator deob, int arrayIndex) {
		_methodDeobfuscator.Deobfuscate(method);
		_methodDeobfuscator.DecryptStrings(method, deob);

		var instrs = method.Body.Instructions;
		//int index = -1;
		foreach (var insn in instrs) {
			/*if (insn.IsLdcI4()) {
				index = insn.GetLdcI4Value();
			}
			else */if (insn.OpCode == OpCodes.Ldtoken) {
				object value = insn.Operand;
				_tokenMapper.AddToken(arrayIndex, ((IMDTokenProvider)value).MDToken.ToInt32());
			}
			else if (insn.OpCode == OpCodes.Ldstr) {
				_tokenMapper.AddString((string)insn.Operand);
			}
		}
	}

	void FindExtraHmacKeyProtection() {
		foreach (var method in VaultType.Methods) {
			if (method.Parameters.Count != 0 || !method.HasBody || method.Body.Instructions.Count > 200)
				continue;
			var calls = DotNetUtils.GetMethodCalls(method);
			if (calls.Count > 1 && calls.All(m =>
				    DotNetUtils.IsMethod(m, "System.Int32",
					    "(System.Byte[],System.Byte[],System.Int32,System.Int32)"))) {
				_extraHmacKey = new ExtraHmacKeyProtection(method).ConstructKey();
				break;
			}
		}
	}

	static byte[] ComplicatedDerive(byte[] password, byte[] salt, int iters) {
		byte[] first;
#pragma warning disable SYSLIB0041
		using (var deriver1 = new Rfc2898DeriveBytes(password, salt, iters))
			first = deriver1.GetBytes(32);
		byte[] nextPassword = ScrambleDerive(first, salt, 32768);
		byte[] result;
		using (var deriver2 = new Rfc2898DeriveBytes(nextPassword, salt, 2048))
			result = deriver2.GetBytes(49);
#pragma warning restore SYSLIB0041
		return result;
	}

	static byte[] ComputeSha(byte[] data)
	{
		using var sha = SHA256.Create();
		return sha.ComputeHash(data);
	}

	static byte[] HashBlocks(byte[] arrayIn)
	{
		byte[] hash1 = ComputeSha(arrayIn);
		byte[] hash2 = ComputeSha(hash1);
		byte[] output = new byte[64];
		Buffer.BlockCopy(hash1, 0, output, 0, 32);
		Buffer.BlockCopy(hash2, 0, output, 32, 32);
		return output;
	}

	static byte[] ScrambleDerive(byte[] firstDerive, byte[] salt, int number)
	{
		byte[] array = new byte[firstDerive.Length + salt.Length];
		Buffer.BlockCopy(firstDerive, 0, array, 0, firstDerive.Length);
		Buffer.BlockCopy(salt, 0, array, firstDerive.Length, salt.Length);
		byte[] array2 = HashBlocks(array);
		byte[][] array3 = new byte[number][];
		for (int i = 0; i < number; i++) {
			array3[i] = array2;
			array2 = HashBlocks(array2);
		}
		for (int j = 0; j < number; j++) {
			int num = (array2[0] | (array2[1] << 8) | (array2[2] << 16) | (array2[3] << 24)) & (number - 1);
			byte[] array4 = new byte[64];
			byte[] array5 = array3[num];
			for (int k = 0; k < 64; k++) {
				array4[k] = (byte)(array2[k] ^ array5[k]);
			}
			array2 = HashBlocks(array4);
		}
		return array2;
	}

	private class HmacKeyEmu : InstructionAndBranchEmulator {
		public HmacKeyEmu(MethodDef method) : base(method) {
		}

		protected override bool OnInstruction(Instruction instr, out bool shouldSkip) {
			shouldSkip = false;
			return instr.OpCode != OpCodes.Stsfld;
		}

		public static Value ByteArrayToValue(byte[] array)
			=> new ObjectValue(new List<Value>(array.Select(b => new Int32Value(b))));
	}

	private class ExtraHmacKeyProtection {
		readonly MethodDef _method;
		readonly int _startIndex;
		readonly ObjectValue _field;

		public ExtraHmacKeyProtection(MethodDef method) {
			_method = method;

			var insns = _method.Body.Instructions;
			if (insns[0].OpCode != OpCodes.Ldsfld
			    || insns[0].Operand is not FieldDef fd
			    || !insns[1].IsStloc())
				throw new Exception("[ExtraHmacKeyProtection] First instr is not ldsfld");

			for (int i = 2; i < insns.Count; i++) {
				var ins = insns[i];
				if (ins.OpCode == OpCodes.Newarr && insns[i - 1].IsLdcI4()) {
					_startIndex = i - 1;
					break;
				}
			}
			if (_startIndex == 0)
				throw new Exception("[ExtraHmacKeyProtection] Start index not found");

			var ex = new ArrayExtractor(fd.DeclaringType.FindStaticConstructor(), method.Module.Mvid!.Value.ToByteArray());
			if (!ex.Run(fd))
				throw new Exception("[ExtraHmacKeyProtection] Failed to get required field data");

			_field = ex.GetListObjectValue();
			if (_field == null)
				throw new Exception("[ExtraHmacKeyProtection] Failed to get required field data (2)");
		}

		public byte[] ConstructKey() {
			var emu = new InstructionEmulator(_method);
			emu.SetLocal(_method.Body.Variables[0], _field);

			var insns = _method.Body.Instructions;
			for (int i = _startIndex; i < insns.Count; i++) {
				if (insns[i].OpCode == OpCodes.Call) {
					var called = (MethodDef)insns[i].Operand;
					var subEmu = new InstructionEmulator(called);
					subEmu.SetArg(new Parameter(3), emu.Pop());
					subEmu.SetArg(new Parameter(2), emu.Pop());
					subEmu.SetArg(new Parameter(1), emu.Pop());
					subEmu.SetArg(new Parameter(0), emu.Pop());
					foreach (var subIns in called.Body.Instructions) {
						if (subIns.OpCode == OpCodes.Ret) {
							emu.Push(subEmu.Pop());
							break;
						}
						subEmu.Emulate(subIns);
					}

					continue;
				}

				if (insns[i].OpCode == OpCodes.Stsfld) {
					var value = emu.Pop();
					if (value is not ObjectValue { obj: List<Value> vals }
					    || vals.Any(v => v is not Int32Value i32 || !i32.AllBitsValid())) {
						throw new Exception("[ExtraHmacKeyProtection] Non-concrete values");
					}
					return vals.Select(v => (byte)((Int32Value)v).Value).ToArray();
				}

				emu.Emulate(insns[i]);
			}

			throw new Exception("[ExtraHmacKeyProtection] Stsfld not encountered");
		}
	}
}
