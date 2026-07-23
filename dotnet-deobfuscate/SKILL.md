---
name: dotnet-deobfuscate
description: Deobfuscate, unpack, and analyze protected .NET assemblies (EXE and DLL) using de4dotEx. Use when an AI agent needs to analyze or reverse-engineer a .NET binary that is packed, renamed, or has encrypted strings/control flow.
---

# .NET Deobfuscation with de4dotEx

This skill guides you through deobfuscating, unpacking, and cleaning protected `.NET` executables (`.exe`) and libraries (`.dll`) using `de4dotEx`.

---

## Static Analysis Mapping (Detect It Easy)

When triaging files using static analysis tools like **Detect It Easy (DiE)**, map the detections to `de4dotEx` execution as follows:

| Detect It Easy (DiE) Detection | Obfuscator / Protector | de4dotEx Command / Options |
| :--- | :--- | :--- |
| **Protector: Confuser (1.X)** | ConfuserEx (1.X) | `de4dot <file>` (Auto-detect) or force: `-d un_confuser` |
| **Protector: Babel.NET** | Babel.NET | `de4dot <file>` or force: `-d un_babel` |
| **Protector: SmartAssembly** | SmartAssembly | `de4dot <file>` or force: `-d un_sa` |
| **Protector: dotNET Reactor** | .NET Reactor | `de4dot <file>` or force: `-d un_reactor` |
| **Protector: ILProtector** | ILProtector | `de4dot <file>` *(Requires Strategy B Wine Container)* |
| **Protector: Agile.NET** | Agile.NET | `de4dot <file>` *(Requires Strategy B Wine Container)* |

---

## de4dotEx CLI / API Usage

Always try **Auto-detection** first, as de4dotEx parses assembly metadata and `ConfusedByAttribute` attributes to automatically load the correct unpacker:

### 1. Basic Auto-Detection (Standard Run)
```bash
de4dot MyAssembly.dll -o CleanAssembly.dll
```

### 2. Forcing a Specific Deobfuscator (`-d` flag)
If auto-detection fails or you want to override:
* **ConfuserEx:** `de4dot -d un_confuser MyAssembly.dll`
* **.NET Reactor:** `de4dot -d un_reactor MyAssembly.dll`
* **Babel.NET:** `de4dot -d un_babel MyAssembly.dll`
* **SmartAssembly:** `de4dot -d un_sa MyAssembly.dll`

### 3. Decrypting Strings (`-str` option)
If the obfuscator uses dynamic delegates or emulation for string decryption:
* **Emulation (Recommended):** `de4dot -str emulate MyAssembly.dll`
* **Delegates:** `de4dot -str delegate MyAssembly.dll`
* **Static:** `de4dot -str static MyAssembly.dll`

### 4. Renaming & Symbol Restoration
* **Disable Renaming:** If symbol renaming breaks references, disable it: `--rename-symbols false`
* **Force Renaming Schema:** Force a specific renaming style: `--rename-symbols true`
* **Preserve Metadata Tokens:** For seamless decompilation alignment: `--preserve-tokens`

---

## Execution Environments

### Mode A: Native .NET 8.0 Docker Container (Static / Fast)
Runs natively at peak speed. Fully supports ConfuserEx 1.X, Babel, and SmartAssembly.
```bash
docker run --rm -v "$(pwd):/work" -w /work de4dotex <input_file> -o <output_file>
```

### Mode B: Wine .NET 4.8 Container (JIT-hook / Dynamic)
Required for protectors using native Windows memory APIs (ILProtector, Agile.NET).
```bash
# On Intel/AMD hosts:
docker run --rm -v "$(pwd):/work" de4dotex-wine <input_file> -o <output_file>

# On ARM64 Apple Silicon Mac hosts (Forces Rosetta 2 x86 translation):
docker run --platform linux/amd64 --rm -v "$(pwd):/work" de4dotex-wine <input_file> -o <output_file>
```

### Mode C: Native C# Stdio MCP Server (AI Agent Integration)
Command for registering inside your MCP client config (`claude_desktop_config.json`):
```json
"mcpServers": {
  "de4dotex": {
    "command": "dotnet",
    "args": ["/absolute/path/to/publish-net8.0-mcp/de4dot.mcp.dll"]
  }
}
```
Exposes the **`deobfuscate`** tool with parameters: `file_path`, `output_path`, and `options` (for passing custom flags like `-str delegate`).
