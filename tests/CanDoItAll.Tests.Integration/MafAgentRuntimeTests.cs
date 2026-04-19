using System.Reflection;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace CanDoItAll.Tests.Integration;

public sealed class MafAgentRuntimeTests
{
    [Fact]
    public void SnapshotUpdate_copies_mutable_content_collections()
    {
        var snapshotMethod = typeof(MafAgentRuntime).GetMethod(
                                 "SnapshotUpdate",
                                 BindingFlags.NonPublic | BindingFlags.Static)
                             ?? throw new InvalidOperationException("SnapshotUpdate method was not found.");

        var update = new AgentResponseUpdate(
            ChatRole.Assistant,
            [
                new TextContent("Initial content")
            ])
        {
            AuthorName = "runtime",
            ResponseId = "response-1"
        };

        var snapshot = Assert.IsType<AgentResponseUpdate>(snapshotMethod.Invoke(null, [update]));
        update.Contents.Add(new TextContent("Late mutation"));

        Assert.NotSame(update.Contents, snapshot.Contents);
        Assert.Single(snapshot.Contents);
        Assert.Equal("response-1", snapshot.ResponseId);
        Assert.Equal("runtime", snapshot.AuthorName);
    }

    [Fact]
    public void SnapshotUpdate_copies_tool_call_argument_graph()
    {
        var snapshotMethod = typeof(MafAgentRuntime).GetMethod(
                                 "SnapshotUpdate",
                                 BindingFlags.NonPublic | BindingFlags.Static)
                             ?? throw new InvalidOperationException("SnapshotUpdate method was not found.");

        var nestedArguments = new Dictionary<string, object?>
        {
            ["path"] = "artifacts/demo.md",
            ["options"] = new Dictionary<string, object?>
            {
                ["recursive"] = true
            },
            ["tags"] = new List<object?>
            {
                "architecture"
            }
        };
        var toolCall = new FunctionCallContent("call-1", "workspace_write_file", nestedArguments);
        var update = new AgentResponseUpdate(ChatRole.Assistant, [toolCall]);

        var snapshot = Assert.IsType<AgentResponseUpdate>(snapshotMethod.Invoke(null, [update]));

        nestedArguments["path"] = "artifacts/changed.md";
        ((Dictionary<string, object?>)nestedArguments["options"]!)["recursive"] = false;
        ((List<object?>)nestedArguments["tags"]!).Add("late-mutation");

        var snapshottedToolCall = Assert.IsType<FunctionCallContent>(Assert.Single(snapshot.Contents));
        Assert.NotSame(toolCall.Arguments, snapshottedToolCall.Arguments);
        Assert.Equal("artifacts/demo.md", snapshottedToolCall.Arguments!["path"]);

        var snapshottedOptions = Assert.IsType<Dictionary<string, object?>>(snapshottedToolCall.Arguments["options"]);
        Assert.True((bool)snapshottedOptions["recursive"]!);

        var snapshottedTags = Assert.IsType<List<object?>>(snapshottedToolCall.Arguments["tags"]);
        Assert.Single(snapshottedTags);
        Assert.Equal("architecture", snapshottedTags[0]);
    }

    [Fact]
    public void SnapshotUpdate_converts_opaque_tool_calls_into_detached_function_calls()
    {
        var snapshotMethod = typeof(MafAgentRuntime).GetMethod(
                                 "SnapshotUpdate",
                                 BindingFlags.NonPublic | BindingFlags.Static)
                             ?? throw new InvalidOperationException("SnapshotUpdate method was not found.");

        var opaqueToolCall = new OpaqueToolCallContent(
            "call-opaque",
            "provider-native-web-search",
            new Dictionary<string, object?>
            {
                ["query"] = "basic unit conversion best practices"
            });
        var update = new AgentResponseUpdate(ChatRole.Assistant, [opaqueToolCall]);

        var snapshot = Assert.IsType<AgentResponseUpdate>(snapshotMethod.Invoke(null, [update]));
        var snapshottedToolCall = Assert.IsType<FunctionCallContent>(Assert.Single(snapshot.Contents));

        Assert.NotSame(opaqueToolCall, snapshottedToolCall);
        Assert.Equal("call-opaque", snapshottedToolCall.CallId);
        Assert.Equal("provider-native-web-search", snapshottedToolCall.Name);
        Assert.Equal("basic unit conversion best practices", snapshottedToolCall.Arguments!["query"]);
    }

    [Fact]
    public void Stored_output_disabled_responses_do_not_request_reasoning_encrypted_content()
    {
        var decisionMethod = typeof(MafAgentRuntime).GetMethod(
                                 "ShouldIncludeReasoningEncryptedContentForStoredOutputDisabledResponses",
                                 BindingFlags.NonPublic | BindingFlags.Static)
                             ?? throw new InvalidOperationException("Stored-output reasoning-content decision method was not found.");

        var provider = new ProviderProfile(
            Guid.NewGuid(),
            "OpenAI default",
            ProviderKind.OpenAi,
            "https://api.openai.com/v1",
            "OPENAI_API_KEY",
            "gpt-4.1",
            ProviderTransportKind.Responses,
            true,
            true,
            true,
            false,
            true,
            "{}",
            string.Empty,
            "Not checked",
            null,
            []);

        var includeReasoningEncryptedContent = Assert.IsType<bool>(decisionMethod.Invoke(null, [provider, true]));

        Assert.False(includeReasoningEncryptedContent);
    }

    private sealed class OpaqueToolCallContent(
        string? callId,
        string name,
        IDictionary<string, object?> arguments) : ToolCallContent(callId)
    {
        public string Name { get; } = name;

        public IDictionary<string, object?> Arguments { get; } = arguments;
    }
}
