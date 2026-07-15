using System;
using System.Collections.Generic;
using dnlib.DotNet;
using dnlib.IO;

namespace de4dot.code.deobfuscators.MasonProtector;

/// <summary>
/// This is a heavily stripped down version of dnlib's SignatureReader that only supports reading LocalSig.
/// The difference is that Class and ValueType have obfuscator-specific handling for their tokens.
/// </summary>
public struct LocalSigReader {
	const uint MaxArrayRank = 64;

	readonly ISignatureReaderHelper helper;
	readonly ICorLibTypes corLibTypes;
	DataReader reader;
	TokenMapper _tokenMapper;

	public static LocalSig ReadSig(ModuleDefMD module, TokenMapper tokenMapper, byte[] signature) =>
		ReadSig(module, module.CorLibTypes, tokenMapper, ByteArrayDataReaderFactory.CreateReader(signature));

	public static LocalSig ReadSig(ModuleDefMD module, TokenMapper tokenMapper, DataReader signature) =>
		ReadSig(module, module.CorLibTypes, tokenMapper, signature);

	public static LocalSig ReadSig(ISignatureReaderHelper helper, ICorLibTypes corLibTypes, TokenMapper tokenMapper, DataReader signature) {
		try {
			var reader = new LocalSigReader(helper, corLibTypes, tokenMapper, ref signature);
			if (reader.reader.Length == 0)
				return null;
			return reader.ReadSig();
		}
		catch {
			return null;
		}
	}

	LocalSigReader(ISignatureReaderHelper helper, ICorLibTypes corLibTypes, TokenMapper tokenMapper, ref DataReader reader) {
		this.helper = helper;
		this.corLibTypes = corLibTypes;
		this._tokenMapper = tokenMapper;
		this.reader = reader;
	}

	LocalSig ReadSig() {
		var callingConvention = (CallingConvention)reader.ReadByte();
		if ((callingConvention & CallingConvention.Mask) == CallingConvention.LocalSig)
			return ReadLocalSig();

		throw new InvalidOperationException("Tried to read non-LocalSig");
	}

	LocalSig ReadLocalSig() {
		if (!reader.TryReadCompressedUInt32(out uint count) || count > 0x10000 || count > reader.BytesLeft)
			return null;
		var sig = new LocalSig();
		var locals = sig.Locals;
		for (uint i = 0; i < count; i++)
			locals.Add(ReadType());
		return sig;
	}

	/// <summary>
	/// Reads the next type
	/// </summary>
	/// <returns>A new <see cref="TypeSig"/> instance or <c>null</c> if invalid element type</returns>
	TypeSig ReadType() {
		TypeSig result = null;
		switch ((ElementType)reader.ReadByte()) {
		case ElementType.Void:		result = corLibTypes.Void; break;
		case ElementType.Boolean:	result = corLibTypes.Boolean; break;
		case ElementType.Char:		result = corLibTypes.Char; break;
		case ElementType.I1:		result = corLibTypes.SByte; break;
		case ElementType.U1:		result = corLibTypes.Byte; break;
		case ElementType.I2:		result = corLibTypes.Int16; break;
		case ElementType.U2:		result = corLibTypes.UInt16; break;
		case ElementType.I4:		result = corLibTypes.Int32; break;
		case ElementType.U4:		result = corLibTypes.UInt32; break;
		case ElementType.I8:		result = corLibTypes.Int64; break;
		case ElementType.U8:		result = corLibTypes.UInt64; break;
		case ElementType.R4:		result = corLibTypes.Single; break;
		case ElementType.R8:		result = corLibTypes.Double; break;
		case ElementType.String:	result = corLibTypes.String; break;
		case ElementType.TypedByRef:result = corLibTypes.TypedReference; break;
		case ElementType.I:			result = corLibTypes.IntPtr; break;
		case ElementType.U:			result = corLibTypes.UIntPtr; break;
		case ElementType.Object:	result = corLibTypes.Object; break;

		case ElementType.Ptr:		result = new PtrSig(ReadType()); break;
		case ElementType.ByRef:		result = new ByRefSig(ReadType()); break;
		case ElementType.ValueType:	result = ReadToken(ElementType.ValueType); break;
		case ElementType.Class:     result = ReadToken(ElementType.Class); break;
		case ElementType.SZArray:	result = new SZArraySig(ReadType()); break;
		case ElementType.Pinned:	result = new PinnedSig(ReadType()); break;

		case ElementType.Array:
			var nextType = ReadType();
			uint rank;
			if (!reader.TryReadCompressedUInt32(out rank))
				break;
			if (rank > MaxArrayRank)
				break;
			uint num;
			if (!reader.TryReadCompressedUInt32(out num))
				break;
			if (num > rank)
				break;
			var sizes = new List<uint>((int)num);
			uint i;
			for (i = 0; i < num; i++) {
				if (!reader.TryReadCompressedUInt32(out uint size))
					goto exit;
				sizes.Add(size);
			}
			if (!reader.TryReadCompressedUInt32(out num))
				break;
			if (num > rank)
				break;
			var lowerBounds = new List<int>((int)num);
			for (i = 0; i < num; i++) {
				if (!reader.TryReadCompressedInt32(out int size))
					goto exit;
				lowerBounds.Add(size);
			}
			result = new ArraySig(nextType, rank, sizes, lowerBounds);
			break;

		default:
			throw new InvalidOperationException();
		}
exit:
		return result;
	}

	TypeSig ReadToken(ElementType et) {
		int theirToken = reader.ReadInt32();
		int realToken = _tokenMapper.Map(theirToken);

		uint codedToken = new MDToken(realToken).Rid << 2;
		switch (realToken >> 24) {
		case 1: // TypeRef
			codedToken |= 1;
			break;
		case 2: // TypeDef
			break; // |= 0
		case 0x1B: // TypeSpec
			codedToken |= 2;
			break;
		default:
			throw new InvalidOperationException("Unexpected token kind: " + (realToken >> 24));
		}

		var resolved = helper.ResolveTypeDefOrRef(codedToken, default);
		//Console.WriteLine($"Resolved {theirToken:X8} to {realToken:X8} --> {resolved}");
		if (realToken >> 24 == 0x1B)
			return resolved.TryGetGenericInstSig();

		return et switch
		{
			ElementType.Class => new ClassSig(resolved),
			ElementType.ValueType => new ValueTypeSig(resolved),
			_ => throw new InvalidOperationException()
		};
	}
}
