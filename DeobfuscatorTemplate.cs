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

using System.Collections.Generic;
using de4dot.blocks.cflow;

namespace de4dot.code.deobfuscators.INSERT_NAMESPACE_NAME_HERE { // Change this namespace name
	public class DeobfuscatorInfo : DeobfuscatorInfoBase {
		public const string THE_NAME = "INSERT_LONG_OFBUSCATOR_NAME_HERE"; // Obfuscator name
		public const string THE_TYPE = "INSERT_SHORT_OFBUSCATOR_NAME_HERE"; // Obfuscator short name
		const string DEFAULT_REGEX = @"(^<.*)|(^[a-zA-Z_<{$][a-zA-Z_0-9<>{}$.`-]*$)";

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

	public class Deobfuscator : DeobfuscatorBase {
		internal class Options : OptionsBase {
		}

		public override string Type => DeobfuscatorInfo.THE_TYPE;
		public override string TypeLong => DeobfuscatorInfo.THE_NAME;
		public override string Name => DeobfuscatorInfo.THE_NAME;
		internal Deobfuscator(Options options)
			: base(options) { 
		}

		protected override void ScanForObfuscator() {
		}

		protected override int DetectInternal() {
			return 0;
		}

		public override IEnumerable<int> GetStringDecrypterMethods() => new List<int>();
	}
}
