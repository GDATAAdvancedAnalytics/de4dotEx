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
using de4dot.blocks.cflow;

namespace de4dot.code.deobfuscators.Dotfuscator {
	public class DeobfuscatorInfo : DeobfuscatorInfoBase {
		public const string THE_NAME = "Dotfuscator";
		public const string THE_TYPE = "df";
		const string DEFAULT_REGEX = @"!^(?:eval_)?[a-z][a-z0-9]{0,2}$&!^A_[0-9]+$&" + @"^[\u2E80-\u8FFFa-zA-Z_<{$][\u2E80-\u8FFFa-zA-Z_0-9<>{}$.`-]*$";
	
		BoolOption inlineMethods;
		BoolOption removeInlinedMethods;

		public DeobfuscatorInfo()
			: base(DEFAULT_REGEX) {
			inlineMethods = new BoolOption(null, MakeArgName("inline"), "Inline short methods", true);
			removeInlinedMethods = new BoolOption(null, MakeArgName("remove-inlined"), "Remove inlined methods", true);
		}

		public override string Name => THE_NAME;
		public override string Type => THE_TYPE;
	
		public override IDeobfuscator CreateDeobfuscator() =>
			new Deobfuscator(new Deobfuscator.Options {
				RenameResourcesInCode = false,
				ValidNameRegex = validNameRegex.Get(),
				InlineMethods = inlineMethods.Get(),
				RemoveInlinedMethods = removeInlinedMethods.Get(),
			});

		protected override IEnumerable<Option> GetOptionsInternal() =>
			new List<Option>() {
				inlineMethods,
				removeInlinedMethods,
			};
	}

	class Deobfuscator : DeobfuscatorBase {
		Options options;

		string obfuscatorName = "Dotfuscator";

		StringDecrypter stringDecrypter;
		bool foundDotfuscatorAttribute = false;
		bool startedDeobfuscating = false;

		internal class Options : OptionsBase {
			public bool InlineMethods { get; set; }
			public bool RemoveInlinedMethods { get; set; }
		}

		public override string Type => DeobfuscatorInfo.THE_TYPE;
		public override string TypeLong => DeobfuscatorInfo.THE_NAME;
		public override string Name => obfuscatorName;
		protected override bool CanInlineMethods => startedDeobfuscating ? options.InlineMethods : true;

		public override IEnumerable<IBlocksDeobfuscator> BlocksDeobfuscators {
			get {
				var list = new List<IBlocksDeobfuscator>();
				if (CanInlineMethods)
					list.Add(new DfMethodCallInliner());
				return list;
			}
		}

		public Deobfuscator(Options options) : base(options) => this.options = options;

		protected override int DetectInternal() {
			int val = 0;

			if (stringDecrypter.Detected)
				val += 100;
			if (foundDotfuscatorAttribute)
				val += 10;

			return val;
		}

		protected override void ScanForObfuscator() {
			stringDecrypter = new StringDecrypter(module);
			stringDecrypter.Find(DeobfuscatedFile);
			FindDotfuscatorAttribute();
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

		void RemoveInlinedMethods() {
			if (!options.InlineMethods || !options.RemoveInlinedMethods)
				return;
			RemoveInlinedMethods(DfMethodCallInliner.Find(module, staticStringInliner.Methods));
		}

		public override void DeobfuscateBegin() {
			base.DeobfuscateBegin();
			DoCflowClean();
			DoStringBuilderClean();
			foreach (var info in stringDecrypter.StringDecrypterInfos)
				staticStringInliner.Add(info.method, (method, gim, args) => stringDecrypter.Decrypt(method, (string)args[0], (int)args[1]));
			DeobfuscatedFile.StringDecryptersAdded();

			startedDeobfuscating = true;
		}

		public override void DeobfuscateEnd() {
			RemoveInlinedMethods();

			if (CanRemoveStringDecrypterType)
				AddMethodsToBeRemoved(stringDecrypter.StringDecrypters, "String decrypter method");

			base.DeobfuscateEnd();
		}

		public override IEnumerable<int> GetStringDecrypterMethods() {
			var list = new List<int>();
			foreach (var method in stringDecrypter.StringDecrypters)
				list.Add(method.MDToken.ToInt32());
			return list;
		}

		void DoCflowClean() {
			var cflowDescrypter = new CflowDecrypter(module);
			cflowDescrypter.CflowClean();
		}
		 
		void DoStringBuilderClean() {
			var decrypter = new StringBuilderDecrypter(module);
			decrypter.StringBuilderClean();
		}
	}
}
