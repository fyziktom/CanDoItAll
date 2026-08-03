using System.Net;
using System.Text;
using System.Text.Json;
using CanDoItAll.AgentFramework.Maf;
using Microsoft.Extensions.AI;
using OllamaSharp;

namespace CanDoItAll.Tests.Unit;

public sealed class OllamaToolResultProtocolHandlerTests
{
    [Fact]
    public async Task GetResponseAsync_OllamaSharpMapperAndProtocolHandler_ProduceValidToolResultMessage()
    {
        var transport = new RecordingHttpMessageHandler(throwAfterRecording: true);
        using var httpClient = new HttpClient(
            new OllamaToolResultProtocolHandler(transport))
        {
            BaseAddress = new Uri("http://localhost:11434")
        };
        using IChatClient chatClient = new OllamaApiClient(
            httpClient,
            "gptoss20b64k",
            jsonSerializerContext: null);
        var function = AIFunctionFactory.Create(
            () => "unused",
            "workspace_read_file",
            "Reads a workspace file.");
        var messages = new ChatMessage[]
        {
            new(ChatRole.User, "Read the project summary."),
            new(
                ChatRole.Assistant,
                [
                    new FunctionCallContent(
                        "call-001",
                        function.Name,
                        new Dictionary<string, object?>
                        {
                            ["path"] = "project.md"
                        })
                ]),
            new(
                ChatRole.Tool,
                [
                    new FunctionResultContent(
                        "call-001",
                        new Dictionary<string, object?>
                        {
                            ["succeeded"] = true,
                            ["message"] = "Read project.md"
                        })
                ])
        };

        await Assert.ThrowsAsync<RequestRecordedException>(() =>
            chatClient.GetResponseAsync(
                messages,
                new ChatOptions
                {
                    Tools = [function]
                }));

        Assert.NotNull(transport.RequestContent);
        using var request = JsonDocument.Parse(transport.RequestContent);
        var toolMessage = request.RootElement
            .GetProperty("messages")
            .EnumerateArray()
            .Single(message => message.GetProperty("role").GetString() == "tool");
        Assert.Equal("workspace_read_file", toolMessage.GetProperty("tool_name").GetString());
        Assert.Equal(
            "{\"succeeded\":true,\"message\":\"Read project.md\"}",
            toolMessage.GetProperty("content").GetString());
    }

    [Fact]
    public async Task SendAsync_OllamaFunctionResult_AddsToolNameAndUnwrapsContent()
    {
        const string requestJson =
            """
            {
              "model": "gptoss20b64k",
              "messages": [
                {
                  "role": "assistant",
                  "content": "",
                  "tool_calls": [
                    {
                      "function": {
                        "name": "workspace_read_file",
                        "arguments": {
                          "path": "project.md"
                        }
                      }
                    }
                  ]
                },
                {
                  "role": "tool",
                  "content": "{\"callId\":\"generated-call-id\",\"result\":{\"succeeded\":true,\"message\":\"Read project.md\"}}"
                }
              ],
              "stream": true
            }
            """;
        var transport = new RecordingHttpMessageHandler();
        using var client = new HttpClient(
            new OllamaToolResultProtocolHandler(transport))
        {
            BaseAddress = new Uri("http://localhost:11434")
        };
        using var content = new StringContent(requestJson, Encoding.UTF8, "application/json");

        using var response = await client.PostAsync("/api/chat", content, CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(transport.RequestContent);
        using var normalizedRequest = JsonDocument.Parse(transport.RequestContent);
        var toolMessage = normalizedRequest.RootElement
            .GetProperty("messages")[1];
        Assert.Equal("workspace_read_file", toolMessage.GetProperty("tool_name").GetString());
        Assert.Equal(
            "{\"succeeded\":true,\"message\":\"Read project.md\"}",
            toolMessage.GetProperty("content").GetString());
    }

    [Fact]
    public async Task SendAsync_two_sequential_tool_results_remain_correlated_and_normalized()
    {
        const string requestJson =
            """
            {
              "model": "gptoss20b64k",
              "messages": [
                {
                  "role": "assistant",
                  "content": "",
                  "tool_calls": [
                    {
                      "id": "call-001",
                      "function": {
                        "name": "workspace_read_file",
                        "arguments": { "path": "first.md" }
                      }
                    }
                  ]
                },
                {
                  "role": "tool",
                  "content": "{\"callId\":\"call-001\",\"result\":{\"message\":\"first result\"}}"
                },
                {
                  "role": "assistant",
                  "content": "",
                  "tool_calls": [
                    {
                      "id": "call-002",
                      "function": {
                        "name": "project_structure_read",
                        "arguments": {}
                      }
                    }
                  ]
                },
                {
                  "role": "tool",
                  "content": "{\"callId\":\"call-002\",\"result\":{\"nodeCount\":22}}"
                }
              ],
              "stream": true
            }
            """;
        var transport = new RecordingHttpMessageHandler();
        using var client = new HttpClient(
            new OllamaToolResultProtocolHandler(transport))
        {
            BaseAddress = new Uri("http://localhost:11434")
        };
        using var content = new StringContent(requestJson, Encoding.UTF8, "application/json");

        using var response = await client.PostAsync("/api/chat", content, CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(transport.RequestContent);
        using var normalizedRequest = JsonDocument.Parse(transport.RequestContent);
        var messages = normalizedRequest.RootElement.GetProperty("messages");
        Assert.Equal("workspace_read_file", messages[1].GetProperty("tool_name").GetString());
        Assert.Equal("{\"message\":\"first result\"}", messages[1].GetProperty("content").GetString());
        Assert.Equal("project_structure_read", messages[3].GetProperty("tool_name").GetString());
        Assert.Equal("{\"nodeCount\":22}", messages[3].GetProperty("content").GetString());
    }

    [Fact]
    public async Task SendAsync_UnmatchedOllamaFunctionResult_FailsExplicitly()
    {
        const string requestJson =
            """
            {
              "model": "gptoss20b64k",
              "messages": [
                {
                  "role": "tool",
                  "content": "{\"callId\":\"orphaned-call\",\"result\":\"orphaned result\"}"
                }
              ],
              "stream": true
            }
            """;
        var transport = new RecordingHttpMessageHandler();
        using var client = new HttpClient(
            new OllamaToolResultProtocolHandler(transport))
        {
            BaseAddress = new Uri("http://localhost:11434")
        };
        using var content = new StringContent(requestJson, Encoding.UTF8, "application/json");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.PostAsync("/api/chat", content, CancellationToken.None));

        Assert.Contains("cannot be correlated", exception.Message, StringComparison.Ordinal);
        Assert.Null(transport.RequestContent);
    }

    [Fact]
    public async Task SendAsync_DomainResultObject_PreservesCompleteToolContent()
    {
        const string requestJson =
            """
            {
              "model": "gptoss20b64k",
              "messages": [
                {
                  "role": "assistant",
                  "content": "",
                  "tool_calls": [
                    {
                      "function": {
                        "name": "project_structure_read",
                        "arguments": {}
                      }
                    }
                  ]
                },
                {
                  "role": "tool",
                  "content": "{\"result\":{\"nodeCount\":4},\"status\":\"complete\"}"
                }
              ],
              "stream": true
            }
            """;
        var transport = new RecordingHttpMessageHandler();
        using var client = new HttpClient(
            new OllamaToolResultProtocolHandler(transport))
        {
            BaseAddress = new Uri("http://localhost:11434")
        };
        using var content = new StringContent(requestJson, Encoding.UTF8, "application/json");
        content.Headers.Add("X-Ollama-Request-Marker", "preserve-me");

        using var response = await client.PostAsync("/api/chat", content, CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("preserve-me", transport.RequestHeaders["X-Ollama-Request-Marker"]);
        Assert.NotNull(transport.RequestContent);
        using var normalizedRequest = JsonDocument.Parse(transport.RequestContent);
        var toolMessage = normalizedRequest.RootElement.GetProperty("messages")[1];
        Assert.Equal("project_structure_read", toolMessage.GetProperty("tool_name").GetString());
        Assert.Equal(
            "{\"result\":{\"nodeCount\":4},\"status\":\"complete\"}",
            toolMessage.GetProperty("content").GetString());
    }

    [Fact]
    public async Task SendAsync_AlreadyNormalizedToolResult_PreservesContent()
    {
        const string requestJson =
            """
            {
              "model": "gptoss20b64k",
              "messages": [
                {
                  "role": "assistant",
                  "content": "",
                  "tool_calls": [
                    {
                      "function": {
                        "name": "project_structure_read",
                        "arguments": {}
                      }
                    }
                  ]
                },
                {
                  "role": "tool",
                  "tool_name": "project_structure_read",
                  "content": "{\"callId\":\"domain-id\",\"result\":42,\"status\":\"complete\"}"
                }
              ],
              "stream": true
            }
            """;
        var transport = new RecordingHttpMessageHandler();
        using var client = new HttpClient(
            new OllamaToolResultProtocolHandler(transport))
        {
            BaseAddress = new Uri("http://localhost:11434")
        };
        using var content = new StringContent(requestJson, Encoding.UTF8, "application/json");

        using var response = await client.PostAsync("/api/chat", content, CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(transport.RequestContent);
        using var normalizedRequest = JsonDocument.Parse(transport.RequestContent);
        var toolMessage = normalizedRequest.RootElement.GetProperty("messages")[1];
        Assert.Equal("project_structure_read", toolMessage.GetProperty("tool_name").GetString());
        Assert.Equal(
            "{\"callId\":\"domain-id\",\"result\":42,\"status\":\"complete\"}",
            toolMessage.GetProperty("content").GetString());
    }

    private sealed class RecordingHttpMessageHandler(bool throwAfterRecording = false) : HttpMessageHandler
    {
        public string? RequestContent { get; private set; }

        public Dictionary<string, string> RequestHeaders { get; } = new(StringComparer.OrdinalIgnoreCase);

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request.Content is not null)
            {
                foreach (var header in request.Content.Headers)
                {
                    RequestHeaders[header.Key] = string.Join(",", header.Value);
                }
            }

            RequestContent = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            if (throwAfterRecording)
            {
                throw new RequestRecordedException();
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed class RequestRecordedException : Exception
    {
    }
}
