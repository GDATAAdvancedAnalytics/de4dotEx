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

namespace de4dot.plugin.deobfuscators.Dotfuscator {
	public class StringDecrypterInfo {
		public MethodDef method;
		public int magic;
		public StringDecrypterInfo(MethodDef method, int magic) {
			this.method = method;
			this.magic = magic;
		}
	}

	public class DeobfuscatorInfo : DeobfuscatorInfoBase {
		public const string THE_NAME = "Dotfuscator";
		public const string THE_TYPE = "df";
		const string DEFAULT_REGEX = @"!^(?:eval_)?[a-z][a-z0-9]{0,2}$&!^A_[0-9]+$&" + DeobfuscatorBase.DEFAULT_ASIAN_VALID_NAME_REGEX;
		public DeobfuscatorInfo()
			: base(DEFAULT_REGEX) {
		}

		public override string Name => THE_NAME;
		public override string Type => THE_TYPE;

		public override IDeobfuscator CreateDeobfuscator() =>
			new Deobfuscator(new Deobfuscator.Options {
				RenameResourcesInCode = false,
				ValidNameRegex = validNameRegex.Get(),
			});
	}

	class Deobfuscator : DeobfuscatorBase {
		string obfuscatorName = "Dotfuscator";

		IDeobfuscatorVer verDeobfuscator;
		bool foundDotfuscatorAttribute = false;

		internal class Options : OptionsBase {
		}

		public override string Type => DeobfuscatorInfo.THE_TYPE;
		public override string TypeLong => DeobfuscatorInfo.THE_NAME;
		public override string Name => obfuscatorName;
		public Deobfuscator(Options options) : base(options) { }

		protected override int DetectInternal() {
			int val = 0;

			val += verDeobfuscator.DetectInternal();
			if (foundDotfuscatorAttribute)
				val += 10;

			return val;
		}

		private int SelectVer() {
			Console.WriteLine("find version:" + obfuscatorName);
			if (obfuscatorName.Contains("239392:1:0:4.43.3.9947")) {
				return 1;
			}

			Console.WriteLine("no dotfuscator for this version, please select version:");
			Console.WriteLine("0: default");
			Console.WriteLine("1: 239392:1:0:4.43.3.9947");
			
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
				deobfuscator = new V239392_1_0_4_43_3_9947.DeobfuscatorVer(this);
				break;
			case 0:
			default:
				deobfuscator = new Default.DeobfuscatorVer(this);
				break;
			}
			return deobfuscator;
		}

		protected override void ScanForObfuscator() {
			FindDotfuscatorAttribute();
			verDeobfuscator = GetDeobfuscator();
			verDeobfuscator.ScanForObfuscator();
		}

		void FindDotfuscatorAttribute() {
			foreach (var type in module.Types) {
				if (type.FullName == "DotfuscatorAttribute") {
					foundDotfuscatorAttribute = true;
					AddAttributeToBeRemoved(type, "Obfuscator attribute");
					InitializeVersion(type);
					return;
				}
			}
		}

		void InitializeVersion(TypeDef attr) {
			var s = DotNetUtils.GetCustomArgAsString(GetAssemblyAttribute(attr), 0);
			if (s == null)
				return;

			var val = System.Text.RegularExpressions.Regex.Match(s, @"^(\d+(?::\d+)*\.\d+(?:\.\d+)*)$");
			if (val.Groups.Count < 2)
				return;
			obfuscatorName = "Dotfuscator " + val.Groups[1].ToString();
		}

		public override void DeobfuscateBegin() {
			base.DeobfuscateBegin();
			verDeobfuscator.DeobfuscateBegin();
			DeobfuscatedFile.StringDecryptersAdded();
		}

		public override void DeobfuscateEnd() {
			if (CanRemoveStringDecrypterType)
				AddMethodsToBeRemoved(verDeobfuscator.GetStringDecrypters(), "String decrypter method");
			verDeobfuscator.DeobfuscateEnd();
			base.DeobfuscateEnd();
		}

		public override IEnumerable<int> GetStringDecrypterMethods() {
			return verDeobfuscator.GetStringDecrypterMethods();
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
	}
}
