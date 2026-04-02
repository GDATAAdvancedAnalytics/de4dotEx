using System;
using System.Collections.Generic;
using System.Globalization;
using de4dot.Bea;

namespace de4dot.code.deobfuscators.ConfuserEx.x86
{
	public enum X86OpCode
	{
		MOV,
		ADD,
		SUB,
		IMUL,
		DIV,
		NEG,
		NOT,
		XOR,
		POP,
		PUSH
	}

	public enum X86Register
	{
		EAX = (int)BeaConstants.ArgumentType.REG0,
		ECX = (int)BeaConstants.ArgumentType.REG1,
		EDX = (int)BeaConstants.ArgumentType.REG2,
		EBX = (int)BeaConstants.ArgumentType.REG3,
		ESP = (int)BeaConstants.ArgumentType.REG4,
		EBP = (int)BeaConstants.ArgumentType.REG5,
		ESI = (int)BeaConstants.ArgumentType.REG6,
		EDI = (int)BeaConstants.ArgumentType.REG7
	}

	public interface IX86Operand
	{
	}

	public class X86RegisterOperand : IX86Operand
	{
		public X86Register Register { get; set; }

		public X86RegisterOperand(X86Register reg)
		{
			Register = reg;
		}
	}

	public class X86ImmediateOperand : IX86Operand
	{
		public int Immediate { get; set; }

		public X86ImmediateOperand(int imm)
		{
			Immediate = imm;
		}
	}

	public abstract class X86Instruction
	{
		public abstract X86OpCode OpCode { get; }
		public IX86Operand[] Operands { get; set; }
		public abstract void Execute(Dictionary<string, int> registers, Stack<int> localStack);

		public static IX86Operand GetOperand(ArgumentType argument)
		{
			if ((argument.OpType & 0xFFFFFF) == (long)BeaConstants.ArgumentType.CONSTANT_TYPE)
				return
					new X86ImmediateOperand(int.Parse(argument.OpMnemonic.TrimEnd('h'),
						NumberStyles.HexNumber));
			if (argument.OpType == (long)BeaConstants.ArgumentType.REGISTER_TYPE)
				if (argument.Registers.type == (long)BeaConstants.ArgumentType.GENERAL_REG)
					return new X86RegisterOperand((X86Register)argument.Registers.gpr);
				else
					throw new Exception("Unexpected register type: " + (BeaConstants.ArgumentType)argument.Registers.type);
			throw new Exception("Unexepcted operand type: " + (BeaConstants.ArgumentType)argument.OpType);
		}
	}
}
