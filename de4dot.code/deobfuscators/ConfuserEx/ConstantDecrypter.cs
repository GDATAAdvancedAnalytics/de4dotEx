using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using de4dot.blocks;
using de4dot.blocks.cflow;
using de4dot.code.deobfuscators.ConfuserEx.x86;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using dnlib.DotNet.Writer;
using FieldAttributes = dnlib.DotNet.FieldAttributes;
using MethodAttributes = dnlib.DotNet.MethodAttributes;
using OpCodes = dnlib.DotNet.Emit.OpCodes;
using TypeAttributes = dnlib.DotNet.TypeAttributes;

namespace de4dot.code.deobfuscators.ConfuserEx
{
    public class ConstantDecrypterBase
    {
        private readonly InstructionEmulator _instructionEmulator = new InstructionEmulator();
        private X86Method _nativeMethod;

        public MethodDef Method { get; set; }
        public byte[] Decrypted { get; set; }
        public uint Magic1 { get; set; }
        public uint Magic2 { get; set; }
        public bool CanRemove { get; set; } = true;

        // native mode
        public MethodDef NativeMethod { get; internal set; }

        // normal mode
        public uint Num1 { get; internal set; }
        public uint Num2 { get; internal set; }

        private int? CalculateKey()
        {
            var popValue = _instructionEmulator.Peek();

            if (popValue == null || !popValue.IsInt32() || !(popValue as Int32Value).AllBitsValid())
                return null;

            _instructionEmulator.Pop();
            var result = _nativeMethod.Execute(((Int32Value) popValue).Value);
            return result;
        }

        private uint CalculateMagic(uint index)
        {
            uint uint_0;
            if (NativeMethod != null)
            {
                _instructionEmulator.Push(new Int32Value((int)index));
                _nativeMethod = new X86Method(NativeMethod, Method.Module as ModuleDefMD); //TODO: Possible null
                var key = CalculateKey();

                uint_0 = (uint)key.Value;
            }
            else
            {
                uint_0 = index * Num1 ^ Num2;
            }

            uint_0 &= 0x3fffffff;
            uint_0 <<= 2;
            return uint_0;
        }

        public string DecryptString(uint index)
        {
            index = CalculateMagic(index);
            var count = BitConverter.ToInt32(Decrypted, (int) index);
            return string.Intern(Encoding.UTF8.GetString(Decrypted, (int) index + 4, count));
        }

        public T DecryptConstant<T>(uint index)
        {
            index = CalculateMagic(index);
            var array = new T[1];
            Buffer.BlockCopy(Decrypted, (int) index, array, 0, Marshal.SizeOf(typeof(T)));
            return array[0];
        }

        public byte[] DecryptArray(uint index)
        {
            index = CalculateMagic(index);
            var count = BitConverter.ToInt32(Decrypted, (int) index);
            //int lengt = BitConverter.ToInt32(Decrypted, (int)index+4);  we actualy dont need that
            var buffer = new byte[count - 4];
            Buffer.BlockCopy(Decrypted, (int) index + 8, buffer, 0, count - 4);
            return buffer;
        }
    }

    public class ConstantsDecrypter
    {
        private readonly ISimpleDeobfuscator _deobfuscator;
        private readonly MethodDef _lzmaMethod;
        private readonly bool _isNewLzma;

        private readonly ModuleDef _module;

        private readonly string[] _strDecryptCalledMethods =
        {
            "System.Text.Encoding System.Text.Encoding::get_UTF8()",
            "System.String System.Text.Encoding::GetString(System.Byte[],System.Int32,System.Int32)",
            "System.Array System.Array::CreateInstance(System.Type,System.Int32)",
            "System.String System.String::Intern(System.String)",
            "System.Void System.Buffer::BlockCopy(System.Array,System.Int32,System.Array,System.Int32,System.Int32)",
            "System.Type System.Type::GetTypeFromHandle(System.RuntimeTypeHandle)",
            "System.Type System.Type::GetElementType()"
        };

        private byte[] _decryptedBytes;
        private FieldDef _decryptedField, _arrayField;
        private readonly List<int> _inlinedRemoval = new();
        internal TypeDef ArrayType;

        public ConstantsDecrypter(ModuleDef module, MethodDef lzmaMethod, ISimpleDeobfuscator deobfsucator, bool isNewLzma)
        {
            _module = module;
            _lzmaMethod = lzmaMethod;
            _deobfuscator = deobfsucator;
            _isNewLzma = isNewLzma;
        }

        public bool CanRemoveLzma { get; private set; }

        public TypeDef Type => ArrayType;

        public MethodDef Method { get; private set; }
        public bool MethodIsInlined { get; private set; }

        public List<FieldDef> Fields => new List<FieldDef> {_decryptedField, _arrayField};

        public List<ConstantDecrypterBase> Decrypters { get; } = new List<ConstantDecrypterBase>();

        public bool Detected => Method != null && _decryptedBytes != null && Decrypters.Count != 0 &&
                                _decryptedField != null && _arrayField != null;

        public void Find()
        {
            var moduleCctor = DotNetUtils.GetModuleTypeCctor(_module);
            if (moduleCctor == null)
                return;
            foreach (var inst in moduleCctor.Body.Instructions)
            {
                if (inst.OpCode != OpCodes.Call)
                    continue;
                if (inst.Operand is not MethodDef method)
                    continue;
                if (!method.HasBody || !method.IsStatic)
                    continue;
                if (!DotNetUtils.IsMethod(method, "System.Void", "()"))
                    continue;

                if (ProcessPossibleInitMethod(method, moduleCctor))
	                return;
            }

            // The initialization code may also be inlined directly into the cctor.
            if (DotNetUtils.CallsMethod(moduleCctor,
	                "System.Void System.Runtime.CompilerServices.RuntimeHelpers::InitializeArray(System.Array,System.RuntimeFieldHandle)"))
	            if (ProcessPossibleInitMethod(moduleCctor, moduleCctor)) {
		            MethodIsInlined = true;

		            // Somewhat dirty, but doing it later would require finding everything again because indexes change.
		            // If everything was found and array decryption succeeded, we can be pretty sure this is Confuser.Core anyway.
		            _inlinedRemoval.Reverse();
		            foreach (var index in _inlinedRemoval) {
			            Method.Body.Instructions.RemoveAt(index);
		            }
		            _inlinedRemoval.Clear();
	            }
        }

        private bool ProcessPossibleInitMethod(MethodDef method, MethodDef moduleCctor) {
	        _deobfuscator.Deobfuscate(method, SimpleDeobfuscatorFlags.Force);

	        if (!IsStringDecrypterInit(method, out var aField, out var dField, out var iStart, out var iEnd))
		        return false;
	        try
	        {
		        _decryptedBytes = DecryptArray(method, aField.InitialValue, iStart, iEnd);
	        }
	        catch (Exception e)
	        {
		        Console.WriteLine("ConfuserEx str decrypter found, but decryption failed: " + e.Message);
		        return false;
	        }

	        _arrayField = aField;
	        _decryptedField = dField;
	        ArrayType = DotNetUtils.GetType(_module, _arrayField.FieldSig.Type);
	        Method = method;
	        Decrypters.AddRange(FindStringDecrypters(moduleCctor.DeclaringType));
	        CanRemoveLzma = true;
	        return true;
        }

        private bool IsStringDecrypterInit(MethodDef method, out FieldDef aField, out FieldDef dField, out int iStart, out int iEnd)
        {
            aField = null;
            dField = null;
            iStart = -1;
            iEnd = -1;
            var instructions = method.Body.Instructions;
            if (instructions.Count < 15)
                return false;

            for (int i = 0; i < 10; i++) {
	            if (instructions[i].IsLdcI4() && instructions[i + 1].IsStloc() && instructions[i + 2].IsLdcI4()) {
		            iStart = i;
		            break;
	            }
            }
            if (iStart == -1)
	            return false;

            if (instructions[iStart].GetLdcI4Value() != instructions[iStart + 2].GetLdcI4Value())
                return false;
            if (instructions[iStart + 3].OpCode != OpCodes.Newarr)
                return false;
            if (instructions[iStart + 3].Operand.ToString() != "System.UInt32")
                return false;
            if (instructions[iStart + 4].OpCode != OpCodes.Dup)
                return false;
            if (instructions[iStart + 5].OpCode != OpCodes.Ldtoken)
                return false;
            aField = instructions[iStart + 5].Operand as FieldDef;
            if (aField?.InitialValue == null)
                return false;
            if (aField.Attributes != (FieldAttributes.Assembly | FieldAttributes.Static | FieldAttributes.HasFieldRVA))
                return false;
            if (instructions[iStart + 6].OpCode != OpCodes.Call)
                return false;
            if (instructions[iStart + 6].Operand.ToString() !=
                "System.Void System.Runtime.CompilerServices.RuntimeHelpers::InitializeArray(System.Array,System.RuntimeFieldHandle)"
            )
                return false;
            if (!instructions[iStart + 7].IsStloc()) // uint[] array = new uint[] {.....};
                return false;

            //<Module>.byte_0 = <Module>.smethod_X(array4);
            var stsfld = instructions.Skip(iStart + 7).FirstOrDefault(inst => inst.OpCode == OpCodes.Stsfld);
            if (stsfld == null)
	            return false;
            dField = stsfld.Operand as FieldDef;
            if (dField == null || dField.FieldType.FullName != "System.Byte[]")
	            return false;

            iEnd = instructions.IndexOf(stsfld);
            if (instructions[iEnd - 1].Operand != _lzmaMethod)
	            return false;

            _inlinedRemoval.Clear();
            for (int i = iStart; i <= iEnd; i++) {
	            _inlinedRemoval.Add(i);
            }
            var branch = method.Body.Instructions.Skip(iEnd).FirstOrDefault(inst => inst.IsBr());
            if (branch != null) {
	            var branchIndex = method.Body.Instructions.IndexOf(branch);
	            var branchTarget = method.Body.Instructions.IndexOf((Instruction)branch.Operand);
	            for (int i = branchIndex + 1; i < branchTarget; i++) {
		            _inlinedRemoval.Add(i);
	            }
            }

            return true;
        }

        private byte[] DecryptArray(MethodDef method, byte[] encryptedArray, int iStart, int iEnd)
        {
            ModuleDefUser tempModule = new ModuleDefUser("TempModule");
            
            AssemblyDef tempAssembly = new AssemblyDefUser("TempAssembly");
            tempAssembly.Modules.Add(tempModule);
            
            var tempType = new TypeDefUser("", "TempType", tempModule.CorLibTypes.Object.TypeDefOrRef);
            tempType.Attributes = TypeAttributes.Public | TypeAttributes.Class;

            MethodDef tempMethod = Utils.Clone(method);
            tempMethod.MethodSig = MethodSig.CreateStatic(new SZArraySig(tempModule.CorLibTypes.Byte), new SZArraySig(tempModule.CorLibTypes.Byte));
            tempMethod.Attributes = MethodAttributes.Public | MethodAttributes.Static;

            for (int i = 0; i < iStart; i++)
	            tempMethod.Body.Instructions.RemoveAt(i);
            for (int i = 0; i < 5; i++)
                tempMethod.Body.Instructions.RemoveAt(2); // read encrypted array from argument
            tempMethod.Body.Instructions.Insert(2, OpCodes.Ldarg_0.ToInstruction());

            iEnd -= iStart + 5 - 1;
            if (tempMethod.Body.Instructions[iEnd - 1].OpCode != OpCodes.Call)
	            throw new Exception("opcode assertion failed");

            // Return decrypted array instead of doing decompress call.
            tempMethod.Body.Instructions[iEnd - 1].OpCode = OpCodes.Ret;
            // Remove dead code.
            Instruction branchTarget = null;
            while (iEnd < tempMethod.Body.Instructions.Count) {
	            var ins = tempMethod.Body.Instructions[iEnd];
	            if (ins.IsBr()) {
					// This is some IL-spaghetti situation where code that logically runs later is interleaved into the decryption.
					branchTarget = (Instruction)ins.Operand;
					tempMethod.Body.Instructions.RemoveAt(iEnd);
					break;
	            }
	            tempMethod.Body.Instructions.RemoveAt(iEnd);
            }

            if (branchTarget != null) {
				int i = tempMethod.Body.Instructions.IndexOf(branchTarget);
				if (i < iEnd)
					throw new Exception("Dead code branch points to front instead of end");
				while (i < tempMethod.Body.Instructions.Count)
					tempMethod.Body.Instructions.RemoveAt(i);
            }

            tempType.Methods.Add(tempMethod);
            tempModule.Types.Add(tempType);
            
            using (var memoryStream = new MemoryStream()) {
                var moduleWriterOptions = new ModuleWriterOptions(tempModule) { MetadataOptions = new MetadataOptions() };

                tempModule.Write(memoryStream, moduleWriterOptions);

                var patchedAssembly = Assembly.Load(memoryStream.ToArray());
                var type = patchedAssembly.ManifestModule.GetType("TempType");
                var methods = type!.GetMethods();
                var patchedMethod = methods.First(m => m.IsPublic && m.IsStatic);
                byte[] decryptedBytes = (byte[]) patchedMethod.Invoke(null, new object[]{encryptedArray});
                return Lzma.Decompress(decryptedBytes, _isNewLzma);
            }
        }

        private IEnumerable<ConstantDecrypterBase> FindStringDecrypters(TypeDef type)
        {
            foreach (var method in type.Methods)
            {
                if (!method.HasBody)
                    continue;
                if (!method.Signature.ContainsGenericParameter)
                    continue;
                var sig = method.MethodSig;
                if (sig?.Params.Count != 1)
                    continue;
                if (sig.Params[0].GetElementType() is not (ElementType.U4 or ElementType.I4))
                    continue;
                if (sig.RetType.RemovePinnedAndModifiers() is not GenericMVar)
                    continue;
                if (sig.GenParamCount != 1)
                    continue;

                _deobfuscator.Deobfuscate(method, SimpleDeobfuscatorFlags.Force);

                if (IsNativeStringDecrypter(method, out MethodDef nativeMethod))
                {
                    yield return new ConstantDecrypterBase
                    {
                        Decrypted = _decryptedBytes,
                        Method = method,
                        NativeMethod = nativeMethod
                    };
                }
                if (IsNormalStringDecrypter(method, out int num1, out int num2))
                {
                    yield return new ConstantDecrypterBase
                    {
                        Decrypted = _decryptedBytes,
                        Method = method,
                        Num1 = (uint)num1,
                        Num2 = (uint)num2
                    };
                }
            }
        }

        private bool IsNormalStringDecrypter(MethodDef method, out int num1, out int num2)
        {
            num1 = 0;
            num2 = 0;
            var instr = method.Body.Instructions;
            if (instr.Count < 25)
                return false;

            var i = 0;
            if (instr[0].OpCode == OpCodes.Call && instr[0].Operand.ToString() ==
					"System.Reflection.Assembly System.Reflection.Assembly::GetExecutingAssembly()")
	            i = 8; // Skip Assembly.GetExecutingAssembly().Equals(Assembly.GetCallingAssembly()) check

            if (!instr[i++].IsLdarg())
                return false;
            if (!instr[i].IsLdcI4())
                return false;
            num1 = (int)instr[i++].Operand;
            if (instr[i++].OpCode != OpCodes.Mul)
                return false;
            if (!instr[i].IsLdcI4())
                return false;
            num2 = (int)instr[i++].Operand;
            if (instr[i++].OpCode != OpCodes.Xor)
                return false;

            if (!instr[i++].IsStarg()) //uint_0 = (uint_0 * 2857448701u ^ 1196001109u);
                return false;

            if (!instr[i++].IsLdarg())
                return false;
            if (!instr[i].IsLdcI4() || instr[i++].GetLdcI4Value() != 0x1E)
                return false;
            if (instr[i++].OpCode != OpCodes.Shr_Un)
                return false;
            if (!instr[i++].IsStloc()) //uint num = uint_0 >> 30;
                return false;

            foreach (var mtd in _strDecryptCalledMethods)
                if (!DotNetUtils.CallsMethod(method, mtd))
                    return false;
            //TODO: Implement
            //if (!DotNetUtils.LoadsField(method, decryptedField))
            //    return;
            return true;
        }

        private bool IsNativeStringDecrypter(MethodDef method, out MethodDef nativeMethod)
        {
            nativeMethod = null;
            var instr = method.Body.Instructions;
            if (instr.Count < 25)
                return false;

            var i = 0;

            if (!instr[i++].IsLdarg())
                return false;

            if (instr[i].OpCode != OpCodes.Call)
                return false;

            nativeMethod = instr[i++].Operand as MethodDef;

            if (nativeMethod == null || !nativeMethod.IsStatic || !nativeMethod.IsNative)
                return false;
            if (!DotNetUtils.IsMethod(nativeMethod, "System.Int32", "(System.Int32)"))
                return false;

            if (!instr[i++].IsStarg()) //uint_0 = (uint_0 * 2857448701u ^ 1196001109u);
                return false;

            if (!instr[i++].IsLdarg())
                return false;
            if (!instr[i].IsLdcI4() || instr[i++].GetLdcI4Value() != 0x1E)
                return false;
            if (instr[i++].OpCode != OpCodes.Shr_Un)
                return false;
            if (!instr[i++].IsStloc()) //uint num = uint_0 >> 30;
                return false;
            i++;
            //TODO: Implement
            //if (!instr[10].IsLdloca())
            //    return;
            if (instr[i++].OpCode != OpCodes.Initobj)
                return false;
            if (!instr[i++].IsLdarg())
                return false;
            if (!instr[i].IsLdcI4() || instr[i++].GetLdcI4Value() != 0x3FFFFFFF)
                return false;
            if (instr[i++].OpCode != OpCodes.And)
                return false;
            if (!instr[i++].IsStarg()) //uint_0 &= 1073741823u;
                return false;

            if (!instr[i++].IsLdarg())
                return false;
            if (!instr[i].IsLdcI4() || instr[i++].GetLdcI4Value() != 2)
                return false;
            if (instr[i++].OpCode != OpCodes.Shl)
                return false;
            if (!instr[i++].IsStarg()) //uint_0 <<= 2;
                return false;

            foreach (var mtd in _strDecryptCalledMethods)
                if (!DotNetUtils.CallsMethod(method, mtd))
                    return false;
            //TODO: Implement
            //if (!DotNetUtils.LoadsField(method, decryptedField))
            //    return;
            return true;
        }

        private static bool VerifyGenericArg(MethodSpec gim, ElementType etype)
        {
            var gims = gim?.GenericInstMethodSig;
            if (gims == null || gims.GenericArguments.Count != 1)
                return false;
            return gims.GenericArguments[0].GetElementType() == etype;
        }

        private static uint CastMagicParam(object param) => param switch {
	        uint u => u,
	        int i => unchecked((uint)i),
	        _ => throw new InvalidCastException("Expected Int32 or UInt32")
        };

        public string DecryptString(ConstantDecrypterBase info, MethodSpec gim, object magic1)
        {
            if (!VerifyGenericArg(gim, ElementType.String))
                return null;
            return info.DecryptString(CastMagicParam(magic1));
        }

        public object DecryptSByte(ConstantDecrypterBase info, MethodSpec gim, object magic1)
        {
            if (!VerifyGenericArg(gim, ElementType.I1))
                return null;
            return info.DecryptConstant<sbyte>(CastMagicParam(magic1));
        }

        public object DecryptByte(ConstantDecrypterBase info, MethodSpec gim, object magic1)
        {
            if (!VerifyGenericArg(gim, ElementType.U1))
                return null;
            return info.DecryptConstant<byte>(CastMagicParam(magic1));
        }

        public object DecryptInt16(ConstantDecrypterBase info, MethodSpec gim, object magic1)
        {
            if (!VerifyGenericArg(gim, ElementType.I2))
                return null;
            return info.DecryptConstant<short>(CastMagicParam(magic1));
        }

        public object DecryptUInt16(ConstantDecrypterBase info, MethodSpec gim, object magic1)
        {
            if (!VerifyGenericArg(gim, ElementType.U2))
                return null;
            return info.DecryptConstant<ushort>(CastMagicParam(magic1));
        }

        public object DecryptInt32(ConstantDecrypterBase info, MethodSpec gim, object magic1)
        {
            if (!VerifyGenericArg(gim, ElementType.I4))
                return null;
            return info.DecryptConstant<int>(CastMagicParam(magic1));
        }

        public object DecryptUInt32(ConstantDecrypterBase info, MethodSpec gim, object magic1)
        {
            if (!VerifyGenericArg(gim, ElementType.U4))
                return null;
            return info.DecryptConstant<uint>(CastMagicParam(magic1));
        }

        public object DecryptInt64(ConstantDecrypterBase info, MethodSpec gim, object magic1)
        {
            if (!VerifyGenericArg(gim, ElementType.I8))
                return null;
            return info.DecryptConstant<long>(CastMagicParam(magic1));
        }

        public object DecryptUInt64(ConstantDecrypterBase info, MethodSpec gim, object magic1)
        {
            if (!VerifyGenericArg(gim, ElementType.U8))
                return null;
            return info.DecryptConstant<ulong>(CastMagicParam(magic1));
        }

        public object DecryptSingle(ConstantDecrypterBase info, MethodSpec gim, object magic1)
        {
            if (!VerifyGenericArg(gim, ElementType.R4))
                return null;
            return info.DecryptConstant<float>(CastMagicParam(magic1));
        }

        public object DecryptDouble(ConstantDecrypterBase info, MethodSpec gim, object magic1)
        {
            if (!VerifyGenericArg(gim, ElementType.R8))
                return null;
            return info.DecryptConstant<double>(CastMagicParam(magic1));
        }

        public object DecryptArray(ConstantDecrypterBase info, MethodSpec gim, object magic1)
        {
            if (!VerifyGenericArg(gim, ElementType.SZArray))
                return null;
            return info.DecryptArray(CastMagicParam(magic1));
        }
    }
}
