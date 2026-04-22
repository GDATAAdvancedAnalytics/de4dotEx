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
using ICSharpCode.SharpZipLib;
using ICSharpCode.SharpZipLib.Zip.Compression;

namespace de4dot.code.deobfuscators.Babel_NET {
	class BabelInflater : Inflater {
		static readonly int[] DefaultMapping = new int[] { 1, 5, 6 }; // STORED_BLOCK (0), STATIC_TREES (1), DYN_TREES (2)
		readonly int _typeSize;
		readonly int _magic;
		readonly int[] _blockTypeMapping;

		public BabelInflater(bool noHeader, int typeSize, int magic, int[] blockTypeMapping) : base(noHeader) {
			_typeSize = typeSize;
			_magic = magic;
			_blockTypeMapping = blockTypeMapping ?? DefaultMapping;
		}

		protected override bool ReadHeader(ref bool isLastBlock, out int blockType) {
			int numBits = 1 + _typeSize;

			int type = input.PeekBits(numBits);
			if (type < 0) {
				blockType = -1;
				return false;
			}
			input.DropBits(numBits);

			if ((type & 1) != 0)
				isLastBlock = true;

			blockType = Array.IndexOf(_blockTypeMapping, type >> 1);
			if (blockType == -1)
				throw new SharpZipBaseException("Unknown block type: " + type);

			return true;
		}

		protected override bool DecodeStoredLength() {
			if ((uncomprLen = input.PeekBits(16)) < 0)
				return false;
			input.DropBits(16);

			uncomprLen ^= _magic;

			return true;
		}
	}
}
