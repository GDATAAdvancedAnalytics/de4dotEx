using System.Linq;
using de4dot.blocks;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace de4dot.code.deobfuscators.MasonProtector;

public static class AntiMiscKiller {
	static readonly string[] MasonCheck = new[] {
		"System.Boolean System.Diagnostics.Debugger::get_IsAttached()",
		"System.String System.Reflection.Assembly::get_Location()",
		"System.Byte[] System.IO.File::ReadAllBytes(System.String)",
	};

	public static bool ShouldKill(MethodDef method) {
		if (!method.IsStatic || !method.HasBody || method.Parameters.Count != 0)
			return false;

		var called = method.Body.Instructions
			.Where(ins => ins.OpCode.Code is Code.Call or Code.Callvirt && ins.Operand is IMethod)
			.Select(ins => ((IMethod)ins.Operand).FullName).ToHashSet();

		if (called.IsSupersetOf(MasonCheck) && HasNativeCall(method)) {
			return true;
		}

		if (DotNetUtils.CallsMethod(method, "System.Diagnostics.Process[] System.Diagnostics.Process::GetProcesses()")
		    && DotNetUtils.HasString(method, "extremedumper")
		    && HasNativeCall(method)) {
			return true;
		}

		return false;
	}

	private static bool HasNativeCall(MethodDef method) => method.Body.Instructions.Any(ins =>
		ins.OpCode == OpCodes.Call && ins.Operand is MethodDef { HasBody: true, IsStatic: true } md
		                           && md.Body.Instructions.Any(ins2 => ins2.OpCode == OpCodes.Calli));
}
