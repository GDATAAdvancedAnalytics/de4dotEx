using System.Collections.Generic;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using dnlib.IO;

namespace de4dot.code.deobfuscators.MasonProtector;

/// <summary>
/// Custom reader that is used for decrypted IL code. Tokens in there are non-standard and need special handling.
/// </summary>
public class MethodBodyReader : MethodBodyReaderBase {
	ModuleDefMD _module;
	TokenMapper _tokenMapper;
	ushort _maxStackSize;
	GenericParamContext gpContext;

	public MethodBodyReader(ModuleDefMD module, TokenMapper tokenMapper, DataReader reader) : base(reader) {
		_module = module;
		_tokenMapper = tokenMapper;
	}

	public void ReadBody(MethodDef method, IList<Local> realLocals, int maxStackSize) {
		gpContext = GenericParamContext.Create(method);
		parameters = method.Parameters;
		SetLocals(realLocals);

		_maxStackSize = (ushort)maxStackSize;
		ReadInstructionsNumBytes(reader.Length);
	}

	protected override IField ReadInlineField(Instruction instr) {
		var realToken = _tokenMapper.Map(reader.ReadInt32());
		return _module.ResolveToken(realToken, gpContext) as IField;
	}

	protected override IMethod ReadInlineMethod(Instruction instr) {
		var realToken = _tokenMapper.Map(reader.ReadInt32());
		return _module.ResolveToken(realToken, gpContext) as IMethod;
	}

	protected override MethodSig ReadInlineSig(Instruction instr) => throw new System.NotImplementedException();

	protected override string ReadInlineString(Instruction instr) => _tokenMapper.MapString(reader.ReadInt32());

	protected override ITokenOperand ReadInlineTok(Instruction instr) {
		var realToken = _tokenMapper.Map(reader.ReadInt32());
		return _module.ResolveToken(realToken, gpContext) as ITokenOperand;
	}

	protected override ITypeDefOrRef ReadInlineType(Instruction instr) {
		var realToken = _tokenMapper.Map(reader.ReadInt32());
		return _module.ResolveToken(realToken, gpContext) as ITypeDefOrRef;
	}

	public override void RestoreMethod(MethodDef method) {
		base.RestoreMethod(method);
		method.Body.MaxStack = _maxStackSize;
	}

	public void ReadExceptionHandlers(int ehCount, DataReader ehReader) {
		exceptionHandlers = new ExceptionHandler[ehCount];
		for (int i = 0; i < ehCount; i++) {
			int flags = ehReader.ReadInt32();
			uint tryStart = ehReader.ReadUInt32();
			uint tryLen = ehReader.ReadUInt32();
			uint handlerStart = ehReader.ReadUInt32();
			uint handlerLen = ehReader.ReadUInt32();
			uint filterStart = ehReader.ReadUInt32();
			int catchType = ehReader.ReadInt32();

			var eh = new ExceptionHandler((ExceptionHandlerType)flags);
			eh.TryStart = GetInstructionThrow(tryStart);
			eh.TryEnd = GetInstruction(tryStart + tryLen);
			eh.HandlerStart = GetInstructionThrow(handlerStart);
			eh.HandlerEnd = GetInstruction(handlerStart + handlerLen);
			if (flags == 0) {
				eh.CatchType = _module.ResolveToken(_tokenMapper.Map(catchType)) as ITypeDefOrRef;
			} else if (flags == 1)
				eh.FilterStart = GetInstructionThrow(filterStart);

			exceptionHandlers[i] = eh;
		}
	}
}
