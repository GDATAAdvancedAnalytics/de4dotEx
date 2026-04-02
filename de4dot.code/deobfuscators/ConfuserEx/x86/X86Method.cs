using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using de4dot.Bea;
using de4dot.code.deobfuscators.ConfuserEx.x86.Instructions;
using dnlib.DotNet;

namespace de4dot.code.deobfuscators.ConfuserEx.x86
{
    public sealed class X86Method
    {
        public List<X86Instruction> Instructions;

        public Stack<int> LocalStack = new Stack<int>();
        public Dictionary<string, int> Registers = new Dictionary<string, int>
        {
            {"EAX", 0},
            {"EBX", 0},
            {"ECX", 0},
            {"EDX", 0},
            {"ESP", 0},
            {"EBP", 0},
            {"ESI", 0},
            {"EDI", 0}
        };

        private readonly ModuleDefMD _module;
        public X86Method(MethodDef method,ModuleDefMD module)
        {
            this._module = module;
            Instructions = new List<X86Instruction>();
            ParseInstructions(method);
        }

        private const int X86MaxInsSize = 15;

        private void ParseInstructions(MethodDef method)
        {
            var rawInstructions = new List<Disasm>();

            while (true)
            { 
                byte[] bytes = ReadChunk(method, _module);
                var buff = new UnmanagedBuffer(bytes);

                var disasm = new Disasm { Archi = 32, EIP = new IntPtr(buff.Ptr.ToInt64()) };

                var disasmResult = BeaEngine.Disasm(disasm);
                if (disasmResult < 0) {
	                break;
                }

                _readOffset -= (uint)(X86MaxInsSize - disasmResult); // revert offset back for each byte that was not a part of this instruction
                var mnemonic = disasm.Instruction.Mnemonic.Trim();

                if (mnemonic == "ret") //TODO: Check if this is the only return in function, e.g. check for jumps that go beyond this address
                {
                    Marshal.FreeHGlobal(buff.Ptr);
                    break;
                }

                rawInstructions.Add(disasm);

                Marshal.FreeHGlobal(buff.Ptr);
            }

            while (rawInstructions.Last().Instruction.Mnemonic.Trim() == "pop")
                rawInstructions.RemoveAt(rawInstructions.Count - 1);

            foreach (var instr in rawInstructions)
            {
                switch (instr.Instruction.Mnemonic.Trim())
                {
                    case "mov":
                        Instructions.Add(new X86MOV(instr));
                        break;
                    case "add":
                        Instructions.Add(new X86ADD(instr));
                        break;
                    case "sub":
                        Instructions.Add(new X86SUB(instr));
                        break;
                    case "imul":
                        Instructions.Add(new X86IMUL(instr));
                        break;
                    case "div":
                        Instructions.Add(new X86DIV(instr));
                        break;
                    case "neg":
                        Instructions.Add(new X86NEG(instr));
                        break;
                    case "not":
                        Instructions.Add(new X86NOT(instr));
                        break;
                    case "xor":
                        Instructions.Add(new X86XOR(instr));
                        break;
                    case "pop":
                        Instructions.Add(new X86POP(instr));
                        break;
                    default:
	                    Logger.w("ConfuserEx native: Unhandled instruction {0}", instr.CompleteInstr.Trim());
	                    break;
                }
            }
        }

        private uint _readOffset;

        private byte[] ReadChunk(MethodDef method, ModuleDefMD module)
        {
            var stream = module.Metadata.PEImage.CreateReader();
            var offset = module.Metadata.PEImage.ToFileOffset(method.RVA);

            byte[] buffer = new byte[X86MaxInsSize];

            if (_readOffset == 0u) //TODO: Don't use hardcoded offset
                _readOffset = (uint) offset + 20u; // skip to actual calculation code

            stream.Position = _readOffset;

            stream.ReadBytes(buffer, 0, X86MaxInsSize);
            _readOffset += X86MaxInsSize;

            return buffer;
        }

        public int Execute(params int[] @params)
        {
            foreach (var param in @params)
                LocalStack.Push(param);

            foreach (var instr in Instructions)
                instr.Execute(Registers, LocalStack);

            return Registers["EAX"];
        }
    }
}
