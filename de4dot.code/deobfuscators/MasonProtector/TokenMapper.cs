using System;
using System.Collections.Generic;

namespace de4dot.code.deobfuscators.MasonProtector;

/// <summary>
/// Helper class for Vault that manages stolen tokens for vaulted IL code.
/// </summary>
public class TokenMapper {

	const int NumNonStrTables = 5; // Number of Handle arrays created in the Vault cctor

	private readonly List<int>[] _tokens = new List<int>[NumNonStrTables];
	private readonly List<string> _strings = new();

	public TokenMapper() {
		for (int i = 0; i < _tokens.Length; i++)
			_tokens[i] = new();
	}

	public void AddToken(int index, int token) => _tokens[index].Add(token);
	public void AddString(string str) => _strings.Add(str);

	public int Map(int theirToken) {
		if (MapInternal(theirToken) is int t)
			return t;
		throw new InvalidOperationException($"{theirToken:X8} does not map to int");
	}

	public string MapString(int theirToken) {
		if (MapInternal(theirToken) is string s)
			return s;
		throw new InvalidOperationException($"{theirToken:X8} does not map to string");
	}

	object MapInternal(int theirToken) {
		if (((uint)theirToken & 0x80000000u) == 0)
			return theirToken;

		int tokenType = (theirToken >> 24) & 0x7F;
		int index = theirToken & 0xFFFFFF;

		switch (tokenType) {
		case 0: // methods
			return _tokens[0][index]; // [1] is decltypes
		case 1: // fields
			return _tokens[2][index]; // [3] is decltypes
		case 2: // types
			return _tokens[4][index];
		case 3:
			return _strings[index];
		default:
			throw new IndexOutOfRangeException($"Got unknown token type {tokenType}");
		}
	}
}
