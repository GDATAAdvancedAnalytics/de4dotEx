using System.Collections.Generic;
using de4dot.Bea;

namespace de4dot.code.deobfuscators.ConfuserEx.x86.Instructions
{
    class X86POP : X86Instruction
    {
        public X86POP(Disasm rawInstruction)
        {
            Operands = new IX86Operand[1];
            Operands[0] = GetOperand(rawInstruction.Operand1);
        }

        public override X86OpCode OpCode { get { return X86OpCode.POP; } }

        public override void Execute(Dictionary<string, int> registers, Stack<int> localStack)
        {
            // Pretend to pop stack
            if (localStack.Count < 1)
                return;

            registers[((X86RegisterOperand) Operands[0]).Register.ToString()] = localStack.Pop();
        }
        
    }
}
