/*
    Copyright (C) 2011-2015 de4dot@gmail.com

    This file is part of de4dot.

    de4dot is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    de4dot is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with de4dot.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using dnlib.DotNet;
using de4dot.blocks;
using de4dot.code;
using de4dot.code.deobfuscators;

namespace de4dot.plugin.deobfuscators.SmartAssembly {
	public class DeobfuscatorInfo : DeobfuscatorInfoBase {
		public const string THE_NAME = "SmartAssembly";
		public const string THE_TYPE = "sa";
		const string DEFAULT_REGEX = DeobfuscatorBase.DEFAULT_ASIAN_VALID_NAME_REGEX;
		BoolOption removeAutomatedErrorReporting;
		BoolOption removeTamperProtection;
		BoolOption removeMemoryManager;

		public DeobfuscatorInfo()
			: base(DEFAULT_REGEX) {
			removeAutomatedErrorReporting = new BoolOption(null, MakeArgName("error"), "Remove automated error reporting code", true);
			removeTamperProtection = new BoolOption(null, MakeArgName("tamper"), "Remove tamper protection code", true);
			removeMemoryManager = new BoolOption(null, MakeArgName("memory"), "Remove memory manager code", true);
		}

		public override string Name => THE_NAME;
		public override string Type => THE_TYPE;

		public override IDeobfuscator CreateDeobfuscator() =>
			new Deobfuscator(new Deobfuscator.Options {
				ValidNameRegex = validNameRegex.Get(),
				RemoveAutomatedErrorReporting = removeAutomatedErrorReporting.Get(),
				RemoveTamperProtection = removeTamperProtection.Get(),
				RemoveMemoryManager = removeMemoryManager.Get(),
			});

		protected override IEnumerable<Option> GetOptionsInternal() =>
			new List<Option>() {
				removeAutomatedErrorReporting,
				removeTamperProtection,
				removeMemoryManager,
			};
	}

	class Deobfuscator : DeobfuscatorBase {
		Options options;
		bool foundVersion = false;
		Version approxVersion = new Version(0, 0, 0, 0);
		bool canRemoveTypes;
		string poweredByAttributeString = null;
		string obfuscatorName = DeobfuscatorInfo.THE_NAME;
		bool foundSmartAssemblyAttribute = false;

		internal class Options : OptionsBase {
			public bool RemoveAutomatedErrorReporting { get; set; }
			public bool RemoveTamperProtection { get; set; }
			public bool RemoveMemoryManager { get; set; }
		}

		public override string Type => DeobfuscatorInfo.THE_TYPE;
		public override string TypeLong => DeobfuscatorInfo.THE_NAME;
		public override string Name => obfuscatorName;

		string ObfuscatorName {
			set {
				obfuscatorName = value;
				foundVersion = true;
			}
		}

		IDeobfuscatorVer deobfuscatorVer;

		public Deobfuscator(Options options)
			: base(options) {
			this.options = options;
			StringFeatures = StringFeatures.AllowStaticDecryption;
		}

		public override void Initialize(ModuleDefMD module) => base.Initialize(module);

		protected override int DetectInternal() {
			int val = 0;

			val += deobfuscatorVer.DetectInternal();
			if (foundSmartAssemblyAttribute)
				val += 10;

			return val;
		}

		private int SelectVer() {
			Console.WriteLine("find version:" + obfuscatorName);
			if (obfuscatorName.Contains("8.1.2.4975")) {
				return 1;
			}

			Console.WriteLine("no smart assembly for this version, please select version:");
			Console.WriteLine("0: default");
			Console.WriteLine("1: 8.1.2.4975");

			string strVer = Console.ReadLine();
			int nVer = 0;
			if (int.TryParse(strVer, out nVer)) {
				nVer = 0;
			}
			return nVer;
		}

		private IDeobfuscatorVer GetDeobfuscator() {
			IDeobfuscatorVer deobfuscator = null;
			int ver = SelectVer();
			switch (ver) {
			case 1:
				deobfuscator = new V8_1_2_4975.DeobfuscatorVer(this);
				break;
			case 0:
			default:
				deobfuscator = new Default.DeobfuscatorVer(this);
				break;
			}
			return deobfuscator;
		}

		protected override void ScanForObfuscator() {
			FindSmartAssemblyAttributes();
			deobfuscatorVer = GetDeobfuscator();
			deobfuscatorVer.ScanForObfuscator();
			if (!foundVersion)
				GuessVersion();
		}

		void FindSmartAssemblyAttributes() {
			foreach (var type in module.Types) {
				if (Utils.StartsWith(type.FullName, "SmartAssembly.Attributes.PoweredByAttribute", StringComparison.Ordinal)) {
					foundSmartAssemblyAttribute = true;
					AddAttributeToBeRemoved(type, "Obfuscator attribute");
					InitializeVersion(type);
				}
			}
		}

		void InitializeVersion(TypeDef attr) {
			var s = DotNetUtils.GetCustomArgAsString(GetAssemblyAttribute(attr), 0);
			if (s == null)
				return;

			poweredByAttributeString = s;

			var val = System.Text.RegularExpressions.Regex.Match(s, @"^Powered by (SmartAssembly (\d+)\.(\d+)\.(\d+)\.(\d+))$");
			if (val.Groups.Count < 6)
				return;
			ObfuscatorName = val.Groups[1].ToString();
			approxVersion = new Version(int.Parse(val.Groups[2].ToString()),
										int.Parse(val.Groups[3].ToString()),
										int.Parse(val.Groups[4].ToString()),
										int.Parse(val.Groups[5].ToString()));
			return;
		}

		void GuessVersion() {
			if (poweredByAttributeString == "Powered by SmartAssembly") {
				ObfuscatorName = "SmartAssembly 5.0/5.1";
				approxVersion = new Version(5, 0, 0, 0);
				return;
			}

			if (poweredByAttributeString == "Powered by {smartassembly}") {
				// It's SA 1.x - 4.x

				if (deobfuscatorVer.IsProxyCallFixerDetected() || HasEmptyClassesInEveryNamespace()) {
					ObfuscatorName = "SmartAssembly 4.x";
					approxVersion = new Version(4, 0, 0, 0);
					return;
				}

				int ver = CheckTypeIdAttribute();
				if (ver == 2) {
					ObfuscatorName = "SmartAssembly 2.x";
					approxVersion = new Version(2, 0, 0, 0);
					return;
				}
				if (ver == 1) {
					ObfuscatorName = "SmartAssembly 1.x-2.x";
					approxVersion = new Version(1, 0, 0, 0);
					return;
				}

				if (HasModuleCctor()) {
					ObfuscatorName = "SmartAssembly 3.x";
					approxVersion = new Version(3, 0, 0, 0);
					return;
				}

				ObfuscatorName = "SmartAssembly 1.x-4.x";
				approxVersion = new Version(1, 0, 0, 0);
				return;
			}
		}

		int CheckTypeIdAttribute() {
			var type = GetTypeIdAttribute();
			if (type == null)
				return -1;

			var fields = type.Fields;
			if (fields.Count == 1)
				return 1;	// 1.x: int ID
			if (fields.Count == 2)
				return 2;	// 2.x: int ID, static int AssemblyID
			return -1;
		}

		TypeDef GetTypeIdAttribute() {
			Dictionary<TypeDef, bool> attrs = null;
			int counter = 0;
			foreach (var type in module.GetTypes()) {
				counter++;
				var cattrs = type.CustomAttributes;
				if (cattrs.Count == 0)
					return null;

				var attrs2 = new Dictionary<TypeDef, bool>();
				foreach (var cattr in cattrs) {
					if (!DotNetUtils.IsMethod(cattr.Constructor, "System.Void", "(System.Int32)"))
						continue;
					var attrType = cattr.AttributeType as TypeDef;
					if (attrType == null)
						continue;
					if (attrs != null && !attrs.ContainsKey(attrType))
						continue;
					attrs2[attrType] = true;
				}
				attrs = attrs2;

				if (attrs.Count == 0)
					return null;
				if (attrs.Count == 1 && counter >= 30)
					break;
			}

			if (attrs == null)
				return null;
			foreach (var type in attrs.Keys)
				return type;
			return null;
		}

		bool HasModuleCctor() => DotNetUtils.GetModuleTypeCctor(module) != null;

		bool HasEmptyClassesInEveryNamespace() {
			var namespaces = new Dictionary<string, int>(StringComparer.Ordinal);
			var moduleType = DotNetUtils.GetModuleType(module);
			foreach (var type in module.Types) {
				if (type == moduleType)
					continue;
				var ns = type.Namespace.String;
				if (!namespaces.ContainsKey(ns))
					namespaces[ns] = 0;
				if (type.Name != "" || type.IsPublic || type.HasFields || type.HasMethods || type.HasProperties || type.HasEvents)
					continue;
				namespaces[ns]++;
			}

			foreach (int count in namespaces.Values) {
				if (count < 1)
					return false;
			}
			return true;
		}

		public override void DeobfuscateBegin() {
			base.DeobfuscateBegin();
			deobfuscatorVer.DeobfuscateBegin();
		}
		
		public override void DeobfuscateMethodEnd(Blocks blocks) {
			deobfuscatorVer.DeobfuscateMethodEnd(blocks);
			base.DeobfuscateMethodEnd(blocks);
		}

		public override void DeobfuscateEnd() {
			canRemoveTypes = FindBigType() == null;
			deobfuscatorVer.DeobfuscateEnd();
			base.DeobfuscateEnd();
		}

		TypeDef FindBigType() {
			if (approxVersion <= new Version(6, 5, 3, 53))
				return null;

			TypeDef bigType = null;
			foreach (var type in module.Types) {
				if (IsBigType(type)) {
					if (bigType == null || type.Methods.Count > bigType.Methods.Count)
						bigType = type;
				}
			}
			return bigType;
		}

		bool IsBigType(TypeDef type) {
			if (type.Methods.Count < 50)
				return false;
			if (type.HasProperties || type.HasEvents)
				return false;
			if (type.Fields.Count > 3)
				return false;
			foreach (var method in type.Methods) {
				if (!method.IsStatic)
					return false;
			}
			return true;
		}
		public override IEnumerable<int> GetStringDecrypterMethods() {
			var list = new List<int>();
			foreach (var method in staticStringInliner.Methods)
				list.Add(method.MDToken.ToInt32());
			return list;
		}

		public ModuleDefMD GetModule() {
			return module;
		}
		public IDeobfuscatedFile GetDeobfuscatedFile() {
			return DeobfuscatedFile;
		}
		public StaticStringInliner GetStaticStringInliner() {
			return staticStringInliner;
		}

		public Options GetOptions() { return options; }

		public void _AddModuleCctorInitCallToBeRemoved(MethodDef methodToBeRemoved) {
			AddModuleCctorInitCallToBeRemoved(methodToBeRemoved);
		}
		public void _AddCallToBeRemoved(MethodDef method, MethodDef methodToBeRemoved) {
			AddCallToBeRemoved(method, methodToBeRemoved);
		}

		public void _AddResourceToBeRemoved(Resource resource, string reason) {
			AddResourceToBeRemoved(resource, reason);
		}

		public void _AddTypeToBeRemoved(TypeDef type, string reason) {
			AddTypeToBeRemoved(type, reason);
		}

		public void _AddMethodsToBeRemoved(IEnumerable<MethodDef> methods, string reason) {
			AddMethodsToBeRemoved(methods, reason);
		}

		public void _AddFieldsToBeRemoved(IEnumerable<FieldDef> fields, string reason) {
			AddFieldsToBeRemoved(fields, reason);
		}

		public bool GetCanRemoveTypes() {
			return canRemoveTypes;
		}

		public bool GetCanRemoveStringDecrypterType() {
			return CanRemoveStringDecrypterType;
		}

		public bool _RemoveProxyDelegates(ProxyCallFixerBase proxyCallFixer, bool removeCreators) {
			return RemoveProxyDelegates(proxyCallFixer, removeCreators);
		}
	}
}
