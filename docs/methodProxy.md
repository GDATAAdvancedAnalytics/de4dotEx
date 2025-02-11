# Method proxying

Some obfuscators replace methods calls, accessing fields, expressions by proxy functions. for example, all call to `String::Combine` replaced to calls to this method.

Let's take original code
```cil
.method public static string  test() cil managed
  {
    .maxstack  2
    .locals init (
			string V_0,
            string V_1)
    IL_0000:  ldnull
    IL_0001:  stloc.0
    IL_0002:  ldnull
    IL_0003:  stloc.1
    IL_0004:  ldloc.0
    IL_0005:  ldloc.1
    IL_0006:  call       string [mscorlib]System.String::Concat(string,
                                                                string)
    IL_000b:  ret
  }
```

For each call seen, we create method proxy like that.

```cil
.method static privatescope 
        string MethodProxy (
            string '',
            string ''
        ) cil managed 
{
    // Header Size: 12 bytes
    // Code Size: 8 (0x8) bytes
    // LocalVarSig Token: 0x11000006 RID: 6
    .maxstack 4
    .locals init (
        [0] uint32
    )

    IL_0000: ldarg.0
    IL_0001: ldarg.1
    IL_0002: call      string [mscorlib]System.String::Concat(string, string)
    IL_0007: ret
}

.method static public string test() cil managed 
{
    .locals init (
        [0] string v1,
        [1] string v2
    )
    ldnull
    stloc.0
    ldnull
    stloc.1
    ldloc.0
    ldloc.1
    call      string C::MethodProxy(string, string)
    ret
}
```

With current JIT this proxy functions would be simply inlined on the IL level inside the body 
of the function, and thus performance impact may be observed only during Jitting.

## Solution

Call to MethodProxy, can be simply replaced to original call inside, to decrypt.

## Cleanup snippet

```csharp
using dnlib.DotNet;
using dnlib.DotNet.Emit;

var assemblyFile = args[0];
var assemblyDefinition = AssemblyDef.Load(assemblyFile);
Dictionary<string, MethodDef> inlineCandidate = new();
foreach (var m in GetAllMethods().Where(_ => _.HasBody && _.Body.HasInstructions))
{
    if (m.IsStatic && m.Parameters.Count + 2 == m.Body.Instructions.Count)
    {
        var callInstruction = m.Body.Instructions[m.Parameters.Count];
        if (callInstruction.OpCode == OpCodes.Call || callInstruction.OpCode == OpCodes.Callvirt)
        {
            bool isValidInlineTarget = true;
            for (int i = 0; i < m.Parameters.Count; i++)
            {
                var ldarg = m.Body.Instructions[i];
                if ((i == 0 && ldarg.OpCode != OpCodes.Ldarg_0))
                {
                    isValidInlineTarget = false;
                    break;
                }
                if ((i == 1 && ldarg.OpCode != OpCodes.Ldarg_1))
                {
                    isValidInlineTarget = false;
                    break;
                }
                if ((i == 2 && ldarg.OpCode != OpCodes.Ldarg_2))
                {
                    isValidInlineTarget = false;
                    break;
                }
                if ((i == 3 && ldarg.OpCode != OpCodes.Ldarg_3))
                {
                    isValidInlineTarget = false;
                    break;
                }
                if (i > 3 && !(ldarg.OpCode == OpCodes.Ldarg && i == (int)ldarg.Operand))
                {
                    isValidInlineTarget = false;
                    break;
                }
            }

            if (isValidInlineTarget)
            {
                inlineCandidate.Add(m.FullName, m);
            }
        }
    }
}

foreach (var m in GetAllMethods().Where(_ => _.HasBody && _.Body.HasInstructions))
{
    for (var i = 0; i < m.Body.Instructions.Count; i++)
    {
        var instr = m.Body.Instructions[i];
        if (instr.OpCode == OpCodes.Call)
        {
            var targetMethod = (IMethod)instr.Operand;
            if (inlineCandidate.TryGetValue(targetMethod.ResolveMethodDef()?.FullName ?? targetMethod.FullName, out var methodToInline))
            {
                var newInstr = instr.Clone();
                newInstr.Operand = methodToInline.Body.Instructions[methodToInline.Parameters.Count].Operand;
                m.Body.Instructions[i] = newInstr;
            }
        }
    }
}

foreach (var m in inlineCandidate)
{
    m.Value.DeclaringType.Remove(m.Value);
}

assemblyDefinition.Write(assemblyFile + ".cleaned.exe");

IList<MethodDef> GetAllMethods()
{
    var list = new List<MethodDef>();

    foreach (var type in assemblyDefinition.ManifestModule.GetTypes())
    {
        foreach (var method in type.Methods)
            list.Add(method);
    }

    return list;
}

```