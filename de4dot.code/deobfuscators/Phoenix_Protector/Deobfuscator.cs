using System.Collections.Generic;

namespace de4dot.code.deobfuscators.Phoenix_Protector {
	public class DeobfuscatorInfo : DeobfuscatorInfoBase {
		public const string THE_NAME = "Phoenix Protector";
		public const string THE_TYPE = "pp";

		const string DEFAULT_REGEX = DeobfuscatorBase.DEFAULT_ASIAN_VALID_NAME_REGEX;

		public DeobfuscatorInfo() : base(DEFAULT_REGEX) { }

		public override string Name => THE_NAME;
		public override string Type => THE_TYPE;

		public override IDeobfuscator CreateDeobfuscator() {
			return new Deobfuscator(new Deobfuscator.Options {
				RenameResourcesInCode = false, ValidNameRegex = validNameRegex.Get(),
			});
		}
	}

	class Deobfuscator : DeobfuscatorBase {
		Options options;
		string obfuscatorName = "Phoenix Protector";
		StringDecrypter stringDecrypter;
		bool foundPhoenixAttribute = false;

		internal class Options : OptionsBase {
		}

		public override string Type => DeobfuscatorInfo.THE_TYPE;
		public override string TypeLong => DeobfuscatorInfo.THE_NAME;
		public override string Name => obfuscatorName;
		public Deobfuscator(Options options) : base(options) { this.options = options; }

		protected override int DetectInternal() {
			int val = 0;
			if (stringDecrypter.Detected) val += 100;
			if (foundPhoenixAttribute) val += 10;
			return val;
		}

		protected override void ScanForObfuscator() {
			stringDecrypter = new StringDecrypter(module);
			stringDecrypter.Find(DeobfuscatedFile);
			FindPhoenixAttribute();
		}

		void FindPhoenixAttribute() {
			foreach (var type in module.Types) {
				if (type.Namespace.StartsWith("?") && type.Namespace.EndsWith("?")) {
					foundPhoenixAttribute = true;
					return;
				}
			}
		}

		public override void DeobfuscateBegin() {
			base.DeobfuscateBegin();
			foreach (var info in
			         stringDecrypter.StringDecrypterInfos)
				staticStringInliner.Add(info.method,
					(method, gim, args) =>
						stringDecrypter.Decrypt((string)args[0])); //Decrypting all Strings
			DeobfuscatedFile.StringDecryptersAdded();
		}

		public override void DeobfuscateEnd() {
			if (CanRemoveStringDecrypterType) {
				AddMethodsToBeRemoved(stringDecrypter.StringDecrypters,
					"String Decrypter Method"); //Removing All Calls for String Decrypt example: class1.decriptstring()
				AddTypeToBeRemoved(stringDecrypter.Type, "String Decrypter Type"); //Removing Phoenix Class
			}

			base.DeobfuscateEnd();
		}

		public override IEnumerable<int> GetStringDecrypterMethods() {
			var list = new List<int>();
			foreach (var method in stringDecrypter.StringDecrypters)
				list.Add(method.MDToken.ToInt32());
			return list;
		}
	}
}
