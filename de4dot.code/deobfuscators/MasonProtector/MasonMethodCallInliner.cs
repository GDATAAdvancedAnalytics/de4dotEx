using System.Collections.Generic;
using de4dot.blocks.cflow;
using dnlib.DotNet;

namespace de4dot.code.deobfuscators.MasonProtector;

/// <summary>
/// Inlines trivial methods found in &lt;Module&gt; and proxy methods.
/// </summary>
public class MasonMethodCallInliner : MethodCallInliner {
	readonly Dictionary<MethodDef, bool> _inlined = new();
	public IEnumerable<MethodDef> InlinedMethods => _inlined.Keys;

	readonly List<MethodDef> _proxies;

	public MasonMethodCallInliner(List<MethodDef> proxies) : base(false) => _proxies = proxies;

	protected override bool CanInline(MethodDef method) => IsSimpleGlobalModuleMethod(method) || _proxies.Contains(method);

	static bool IsSimpleGlobalModuleMethod(MethodDef method) =>
		method.IsStatic
			&& method.HasBody
			&& method.Parameters.Count == 0
			&& method.HasReturnType
			&& method.IsPublic
			&& method.DeclaringType.IsGlobalModuleType;

	protected override void OnInlinedMethod(MethodDef methodToInline, bool inlinedMethod) {
		if (inlinedMethod)
			_inlined[methodToInline] = true;
	}
}
