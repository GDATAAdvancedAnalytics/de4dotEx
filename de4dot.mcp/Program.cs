using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;

namespace de4dot.mcp {
    class Program {
        static async Task Main(string[] args) {
            // Check if user requested HTTP Web API mode
            if (args.Length > 0 && (args[0] == "--http" || args[0] == "-h")) {
                int port = 8080;
                if (args.Length > 1 && int.TryParse(args[1], out int parsedPort)) {
                    port = parsedPort;
                }
                await StartHttpServer(port);
            } else {
                // Otherwise run in standard Stdio-based MCP mode
                await StartMcpStdioLoop();
            }
        }

        #region HTTP Web API Mode
        static async Task StartHttpServer(int port) {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.ConfigureKestrel(options => {
                options.ListenAnyIP(port);
            });

            var app = builder.Build();

            // Root info route
            app.MapGet("/", () => $"de4dotEx Web API is active and listening on port {port}!\n\nTo deobfuscate, make a POST request to /deobfuscate with a 'file' parameter containing your obfuscated assembly.");

            // Main /deobfuscate POST file handler
            app.MapPost("/deobfuscate", async (HttpContext context) => {
                if (!context.Request.HasFormContentType) {
                    return Results.BadRequest("Error: Request must be multipart/form-data containing a 'file' field.");
                }

                var form = await context.Request.ReadFormAsync();
                var file = form.Files.GetFile("file");

                if (file == null || file.Length == 0) {
                    return Results.BadRequest("Error: No file uploaded under form-key 'file'.");
                }

                // 1. Create unique paths in the Temp directory
                var tempId = Guid.NewGuid().ToString("N");
                var tempInput = Path.Combine(Path.GetTempPath(), $"{tempId}_{file.FileName}");
                var tempOutput = Path.Combine(Path.GetTempPath(), $"{tempId}_cleaned_{file.FileName}");

                // 2. Stream uploaded file directly to disk
                using (var stream = new FileStream(tempInput, FileMode.Create)) {
                    await file.CopyToAsync(stream);
                }

                try {
                    // 3. Build native arguments list
                    var argsList = new List<string> { tempInput, "-o", tempOutput };
                    
                    // Read optional 'options' form field and append if present
                    var extraOptions = form["options"].ToString();
                    if (!string.IsNullOrEmpty(extraOptions)) {
                        var tokens = extraOptions.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        argsList.AddRange(tokens);
                    }

                    // 4. Invoke de4dotEx's native engine in-process
                    int exitCode = de4dot.cui.Program.Main(argsList.ToArray());

                    if (exitCode != 0 || !File.Exists(tempOutput)) {
                        return Results.Problem("Deobfuscation failed inside the de4dotEx analysis engine.");
                    }

                    // 5. Read the processed bytes and send back as binary file stream
                    var fileBytes = await File.ReadAllBytesAsync(tempOutput);
                    return Results.File(fileBytes, "application/octet-stream", $"cleaned_{file.FileName}");
                }
                catch (Exception ex) {
                    return Results.Problem($"An unexpected error occurred during deobfuscation: {ex.Message}");
                }
                finally {
                    // 5. Always securely clean up our temp files
                    try {
                        if (File.Exists(tempInput)) File.Delete(tempInput);
                        if (File.Exists(tempOutput)) File.Delete(tempOutput);
                    }
                    catch { }
                }
            });

            Console.WriteLine($"[de4dotEx] Starting HTTP Web API Server on port {port}...");
            await app.RunAsync();
        }
        #endregion

        #region Stdio MCP Mode
        static async Task StartMcpStdioLoop() {
            // Set input and output streams to UTF-8
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;

            var reader = Console.In;
            var writer = Console.Out;

            while (true) {
                var line = await reader.ReadLineAsync();
                if (line == null) break;

                try {
                    var jsonNode = JsonNode.Parse(line);
                    if (jsonNode == null) continue;

                    var method = jsonNode["method"]?.ToString();
                    var id = jsonNode["id"]?.ToString();

                    if (method == "initialize") {
                        SendResponse(writer, id, new {
                            protocolVersion = "2024-11-05",
                            capabilities = new {
                                tools = new { }
                            },
                            serverInfo = new {
                                name = "de4dotEx",
                                version = "1.0.0"
                            }
                        });
                    }
                    else if (method == "notifications/initialized") {
                        // Client notification. No response required.
                    }
                    else if (method == "tools/list") {
                        SendResponse(writer, id, new {
                            tools = new[] {
                                new {
                                    name = "deobfuscate",
                                    description = "Deobfuscate and unpack a .NET assembly (EXE or DLL) using de4dotEx",
                                    inputSchema = new {
                                        type = "object",
                                        properties = new {
                                            file_path = new {
                                                type = "string",
                                                description = "The absolute file path of the .NET assembly to deobfuscate."
                                            },
                                            output_path = new {
                                                type = "string",
                                                description = "Optional custom output path for the deobfuscated assembly."
                                            },
                                            options = new {
                                                type = "string",
                                                description = "Optional extra CLI arguments to pass directly to de4dotEx (e.g. '-str delegate' or '--preserve-tokens')."
                                            }
                                        },
                                        required = new[] { "file_path" }
                                    }
                                }
                            }
                        });
                    }
                    else if (method == "tools/call") {
                        var toolName = jsonNode["params"]?["name"]?.ToString();
                        if (toolName == "deobfuscate") {
                            var argsObj = jsonNode["params"]?["arguments"];
                            var filePath = argsObj?["file_path"]?.ToString();
                            var outputPath = argsObj?["output_path"]?.ToString();
                            var extraOptions = argsObj?["options"]?.ToString();

                            var result = ExecuteDeobfuscation(filePath, outputPath, extraOptions);
                            SendResponse(writer, id, result);
                        } else {
                            SendError(writer, id, -32601, $"Tool '{toolName}' not found.");
                        }
                    }
                    else if (id != null) {
                        SendError(writer, id, -32601, $"Method '{method}' not found.");
                    }
                }
                catch (Exception ex) {
                    SendError(writer, null, -32603, ex.Message);
                }
            }
        }

        static void SendResponse(TextWriter writer, string id, object result) {
            var response = new {
                jsonrpc = "2.0",
                id = id != null ? JsonValue.Create(id) : null,
                result = result
            };
            var json = JsonSerializer.Serialize(response);
            writer.WriteLine(json);
            writer.Flush();
        }

        static void SendError(TextWriter writer, string id, int code, string message) {
            var response = new {
                jsonrpc = "2.0",
                id = id != null ? JsonValue.Create(id) : null,
                error = new {
                    code = code,
                    message = message
                }
            };
            var json = JsonSerializer.Serialize(response);
            writer.WriteLine(json);
            writer.Flush();
        }

        static object ExecuteDeobfuscation(string filePath, string outputPath, string extraOptions) {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) {
                return new {
                    content = new[] {
                        new {
                            type = "text",
                            text = $"Error: File '{filePath}' does not exist or is not accessible."
                        }
                    },
                    isError = true
                };
            }

            var argsList = new List<string> { filePath };
            if (!string.IsNullOrEmpty(outputPath)) {
                argsList.Add("-o");
                argsList.Add(outputPath);
            }

            if (!string.IsNullOrEmpty(extraOptions)) {
                var tokens = extraOptions.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                argsList.AddRange(tokens);
            }

            var originalOut = Console.Out;
            var originalError = Console.Error;

            using (var sw = new StringWriter()) {
                Console.SetOut(sw);
                Console.SetError(sw);

                int exitCode = 1;
                try {
                    exitCode = de4dot.cui.Program.Main(argsList.ToArray());
                }
                catch (Exception ex) {
                    sw.WriteLine();
                    sw.WriteLine("Exception caught during deobfuscation execution:");
                    sw.WriteLine(ex.ToString());
                }

                Console.SetOut(originalOut);
                Console.SetError(originalError);

                string outputLog = sw.ToString();

                return new {
                    content = new[] {
                        new {
                            type = "text",
                            text = $"Execution completed with exit code: {exitCode}\n\nConsole Log:\n{outputLog}"
                        }
                    },
                    isError = exitCode != 0
                };
            }
        }
        #endregion
    }
}
