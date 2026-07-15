using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using de4dot.blocks;
using de4dot.blocks.cflow;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace de4dot.code.deobfuscators.MasonProtector;

public class DeobfuscatorInfo : DeobfuscatorInfoBase {
    internal const string THE_NAME = "Mason Protector";
    public const string THE_TYPE = "mp";
    private const string DEFAULT_REGEX = DeobfuscatorBase.DEFAULT_ASIAN_VALID_NAME_REGEX;

    public DeobfuscatorInfo()
        : base(DEFAULT_REGEX) {
    }

    public override string Name => THE_NAME;
    public override string Type => THE_TYPE;

    public override IDeobfuscator CreateDeobfuscator() {
        return new Deobfuscator(new DeobfuscatorBase.OptionsBase {
            RenameResourcesInCode = false, ValidNameRegex = validNameRegex.Get()
        });
    }
}

internal class Deobfuscator : DeobfuscatorBase {
    private int _score;

    public Deobfuscator(OptionsBase options)
        : base(options)
    {
    }

    public override string Type => DeobfuscatorInfo.THE_TYPE;
    public override string TypeLong => DeobfuscatorInfo.THE_NAME;
    public override string Name => TypeLong;

    private ConstantsInliner _constantsInliner;
    private MasonMethodCallInliner _methodCallInliner;

    private readonly List<MethodDef> _proxies = new();

    public override IEnumerable<IBlocksDeobfuscator> BlocksDeobfuscators {
	    get {
		    var list = new List<IBlocksDeobfuscator>();
		    if (_constantsInliner != null)
				list.Add(_constantsInliner);
		    list.Add(new AntiDebugKiller());
		    list.Add(_methodCallInliner ??= new MasonMethodCallInliner(_proxies));
		    list.Add(new InlineStringDeobfuscator());
		    return list;
	    }
    }

    protected override int DetectInternal() => _score;

    protected override void ScanForObfuscator() {
	    var v = new Vault(module, DeobfuscatedFile);
	    v.Find();
	    if (v.Found)
		    _score += 100;

	    var strDec2 = new StringDecrypter2(module);
	    strDec2.Find();
	    if (strDec2.StringDecrypterInfos.Count > 0)
		    _score += 100;

	    // Old versions have obvious [GeneratedCode("Freemasonry", "(c) Mason Protector")] attributes
	    // Newer versions use a custom attribute type defined in the assembly, with AttributeUsage(AttributeTargets.All)
    }

    static readonly Regex IsRandomName = new("^[A-Za-z]{40,90}[0-9]{4,8}$");

    public override bool IsValidMethodName(string name) => name != null && !IsRandomName.IsMatch(name) && CheckValidName(name);
    public override bool IsValidFieldName(string name) => name != null && !IsRandomName.IsMatch(name) && CheckValidName(name);
    public override bool IsValidTypeName(string name) => name != null && !IsRandomName.IsMatch(name) && CheckValidName(name);
    public override bool IsValidMethodArgName(string name) => name != null && !IsRandomName.IsMatch(name) && CheckValidName(name);

    public override void DeobfuscateBegin() {
        base.DeobfuscateBegin();

        var moduleCctor = DotNetUtils.GetModuleTypeCctor(module);

        _proxies.AddRange(ProxyDeclutterer.Run(module));

        _constantsInliner = new ConstantsInliner(module, DeobfuscatedFile);

        var strDec = new StringDecrypter(module);
        strDec.Find();
        foreach (var info in strDec.StringDecrypterInfos)
	        staticStringInliner.Add(info.Method, (method, gim, args) => strDec.Decrypt(info, (string)args[0], (string)args[1], (int)args[2]));

        var strDec2 = new StringDecrypter2(module);
        strDec2.Find();
        foreach (var info in strDec2.StringDecrypterInfos)
	        staticStringInliner.Add(info.Method, (method, gim, args) => strDec2.Decrypt(info, (int)args[0], (int)args[1], (int)args[2], (int)args[3]));

        var vault = new Vault(module, DeobfuscatedFile);
        vault.Find();
        if (vault.Found) {
	        try {
		        vault.Initialize(this);
		        vault.UnvaultAll();
	        }
	        catch (Exception ex) {
		        Logger.w("Mason Protector: Unvault failed: {0}", ex.Message);
		        //Console.WriteLine(ex.StackTrace);
	        }
        }
        if (vault.CanRemove) {
	        AddTypeToBeRemoved(vault.VaultType, "Vault runtime type");
	        AddResourceToBeRemoved(vault.VaultResource, "Vault resource");
	        if (moduleCctor != null) {
		        foreach (var called in moduleCctor.Body.Instructions.Where(ins => ins.OpCode == OpCodes.Call
		                 && ins.Operand is MethodDef md && md.DeclaringType == vault.VaultType).Select(ins => (MethodDef)ins.Operand))
			        AddCallToBeRemoved(moduleCctor, called);
	        }
        }

	    IResourceDecrypter resDec = new ResourceDecrypter(module, DeobfuscatedFile);
	    resDec.Find();
	    if (!resDec.Found) {
		    resDec = new ResourceDecrypter2(module, DeobfuscatedFile);
		    resDec.Find();
	    }
	    if (resDec.Found) {
		    try {
			    resDec.Initialize(this);
			    resDec.DecryptResources();
			    AddMethodsToBeRemoved(resDec.MethodsToRemove, "Resource decrypter");
			    if (moduleCctor != null)
				    AddCallToBeRemoved(moduleCctor, resDec.VarsInitMethod);
		    }
		    catch (Exception ex) {
			    Logger.e("Resource decryption failed: {0}", ex.Message);
		    }
	    }
    }

    public override void DeobfuscateEnd() {
	    IntShenanigans.Deobfuscate(module);

	    if (_constantsInliner != null)
		    RemoveInlinedMethods(_constantsInliner.Methods);
	    if (_methodCallInliner != null)
			RemoveInlinedMethods(_methodCallInliner.InlinedMethods);

	    var antiMethods = new HashSet<IMethod>();
	    foreach (var type in module.GetTypes())
		    foreach (var method in type.Methods)
			    if (AntiMiscKiller.ShouldKill(method)) {
				    AddMethodToBeRemoved(method, "Anti");
				    antiMethods.Add(method);
			    }

	    foreach (var type in module.GetTypes())
		    foreach (var method in type.Methods)
			    if (method.HasBody)
				    foreach (var instruction in method.Body.Instructions)
					    if (instruction.OpCode == OpCodes.Call && antiMethods.Contains((IMethod)instruction.Operand))
						    instruction.OpCode = OpCodes.Nop;

	    base.DeobfuscateEnd();
    }

    public override IEnumerable<int> GetStringDecrypterMethods() => new List<int>();
}
