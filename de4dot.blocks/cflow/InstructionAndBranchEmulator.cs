using System.Collections.Generic;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace de4dot.blocks.cflow;

public class InstructionAndBranchEmulator : IBranchHandler {
	public InstructionEmulator Emulator { get; }
	readonly BranchEmulator _branchEmulator;
	readonly IList<Instruction> _instructions;

	protected int InsnIndex;

	public InstructionAndBranchEmulator(MethodDef method) {
		Emulator = new InstructionEmulator(method);
		_branchEmulator = new BranchEmulator(Emulator, this);
		_instructions = method.Body.Instructions;
	}

	public Value? Run(int startIndex = 0, params int[] @params) {
		int x = 0;
		foreach (int param in @params)
			Emulator.SetArg(new Parameter(x++), new Int32Value(param));

		for (InsnIndex = startIndex; InsnIndex < _instructions.Count; InsnIndex++) {
			var instr = _instructions[InsnIndex];

			if (!OnInstruction(instr, out var shouldSkip))
				return null;
			if (shouldSkip)
				continue;

			if (instr.OpCode == OpCodes.Ret)
				return Emulator.Pop();

			if (instr.OpCode.FlowControl is FlowControl.Branch or FlowControl.Cond_Branch) {
				if (_branchEmulator.Emulate(instr))
					InsnIndex--; // Loop update will do ++, which is bad
				else
					return null;
			}
			else
				Emulator.Emulate(instr);
		}

		return null;
	}

	/// <summary>
	/// Called before an instruction is processed. If false is returned, emulation is stopped.
	/// </summary>
	/// <param name="instr">The next instruction to be emulated.</param>
	/// <param name="shouldSkip">Whether to continue with the next instruction without passing this one to
	/// the emulator.</param>
	/// <returns>Whether to continue emulation.</returns>
	protected virtual bool OnInstruction(Instruction instr, out bool shouldSkip) {
		shouldSkip = false;
		return true;
	}

	void IBranchHandler.HandleNormal(int stackArgs, bool isTaken) {
		if (!isTaken)
			InsnIndex++;
		else
			InsnIndex = _instructions.IndexOf((Instruction)_instructions[InsnIndex].Operand);
	}

	bool IBranchHandler.HandleSwitch(Int32Value switchIndex) {
		if (!switchIndex.AllBitsValid())
			return false;
		var instr = _instructions[InsnIndex];
		var targets = (Instruction[])instr.Operand;
		if (switchIndex.Value >= 0 && switchIndex.Value < targets.Length)
			InsnIndex = _instructions.IndexOf(targets[switchIndex.Value]);
		else
			InsnIndex++;
		return true;
	}
}
