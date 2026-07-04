using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Extensions.AI;
using System.Reflection;

namespace CanDoItAll.Tests.Unit;

public sealed class MafAgentRuntimeAttachmentTests
{
    private static readonly AgentRuntimeInputAttachment TestImageAttachment =
        new("screen.png", "image/png", [1, 2, 3], "artifacts/screen.png");

    [Fact]
    public void CreateUserInputMessage_AddsImageAttachmentsAsDataContent()
    {
        var message = MafAgentRuntime.CreateUserInputMessage(
            "Inspect the screenshot.",
            [TestImageAttachment]);

        Assert.Equal(ChatRole.User, message.Role);
        var text = Assert.IsType<TextContent>(Assert.Single(message.Contents.OfType<TextContent>()));
        Assert.Equal("Inspect the screenshot.", text.Text);
        var image = Assert.IsType<DataContent>(Assert.Single(message.Contents.OfType<DataContent>()));
        Assert.Equal("image/png", image.MediaType);
        Assert.Equal("screen.png", image.Name);
        Assert.Equal(new byte[] { 1, 2, 3 }, image.Data.ToArray());
    }

    [Fact]
    public void RemoveRequestScopedDataContentFromSerializedSession_RemovesPersistedImagePayloads()
    {
        const string serializedSession = """
            {
              "stateBag": {
                "InMemoryChatHistoryProvider": {
                  "messages": [
                    {
                      "role": "user",
                      "contents": [
                        { "$type": "text", "text": "Inspect the screenshot." },
                        { "$type": "data", "uri": "data:image/png;base64,AAA" }
                      ]
                    },
                    {
                      "role": "user",
                      "contents": [
                        { "$type": "data", "uri": "data:image/png;base64,BBB" }
                      ]
                    }
                  ]
                }
              }
            }
            """;

        var scrubbed = MafAgentRuntime.RemoveRequestScopedDataContentFromSerializedSession(serializedSession);

        Assert.NotNull(scrubbed);
        Assert.Contains("Inspect the screenshot.", scrubbed);
        Assert.Contains("Request-scoped attachment omitted", scrubbed);
        Assert.DoesNotContain("data:image", scrubbed);
        Assert.DoesNotContain("\"$type\":\"data\"", scrubbed);
    }

    [Fact]
    public void EnsureInputAttachmentsSupported_rejects_text_only_provider()
    {
        var provider = CreateProvider(ProviderKind.Ollama, "gptoss32k:latest");
        var options = CreateExecutionOptions([TestImageAttachment]);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            MafAgentRuntime.EnsureInputAttachmentsSupported(provider, provider.DefaultModel, options));

        Assert.Contains("does not support vision/image input", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("gptoss32k:latest", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EnsureInputAttachmentsSupported_rejects_text_only_dispatch_model_when_provider_has_vision_suggestions()
    {
        var provider = CreateProvider(ProviderKind.Ollama, "gptoss32k:latest") with
        {
            SuggestedModels = ["gptoss32k:latest", "gemma4:12b"]
        };
        var options = CreateExecutionOptions([TestImageAttachment]);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            MafAgentRuntime.EnsureInputAttachmentsSupported(provider, provider.DefaultModel, options));

        Assert.Contains("does not support vision/image input", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("gptoss32k:latest", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EnsureInputAttachmentsSupported_rejects_text_only_ollama_model_when_provider_metadata_is_vision_capable()
    {
        var provider = CreateProvider(ProviderKind.Ollama, "gptoss32k:latest") with
        {
            SuggestedModels = ["gptoss32k:latest", "qwen3.5:9b"],
            ConfigurationJson = """{"supportsVision":true}""",
            Tags = ["chat", "vision"]
        };
        var options = CreateExecutionOptions([TestImageAttachment]);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            MafAgentRuntime.EnsureInputAttachmentsSupported(provider, provider.DefaultModel, options));

        Assert.Contains("does not support vision/image input", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("gptoss32k:latest", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EnsureInputAttachmentsSupported_allows_vision_model_override()
    {
        var provider = CreateProvider(ProviderKind.Ollama, "gptoss32k:latest");
        var options = CreateExecutionOptions([TestImageAttachment]);

        MafAgentRuntime.EnsureInputAttachmentsSupported(provider, "gemma4:12b", options);
    }

    [Fact]
    public void ResolveRuntimeModelForInputAttachments_keeps_selected_model_without_attachments()
    {
        var provider = CreateProvider(ProviderKind.Ollama, "qwen3.5:9b");
        var options = CreateExecutionOptions([]);

        var model = MafAgentRuntime.ResolveRuntimeModelForInputAttachments(provider, "llama3.2:3b", options);

        Assert.Equal("llama3.2:3b", model);
    }

    [Fact]
    public void ResolveRuntimeModelForInputAttachments_keeps_selected_vision_model()
    {
        var provider = CreateProvider(ProviderKind.Ollama, "qwen3.5:9b");
        var options = CreateExecutionOptions([TestImageAttachment]);

        var model = MafAgentRuntime.ResolveRuntimeModelForInputAttachments(provider, "qwen3.5:9b", options);

        Assert.Equal("qwen3.5:9b", model);
    }

    [Fact]
    public void ResolveRuntimeModelForInputAttachments_uses_provider_vision_model_for_text_only_selection()
    {
        var provider = CreateProvider(ProviderKind.Ollama, "qwen3.5:9b") with
        {
            SuggestedModels = ["qwen3.5:9b", "llama3.2:3b"],
            ConfigurationJson = """{"supportsVision":true}""",
            Tags = ["chat", "vision"]
        };
        var options = CreateExecutionOptions([TestImageAttachment]);

        var model = MafAgentRuntime.ResolveRuntimeModelForInputAttachments(provider, "llama3.2:3b", options);

        Assert.Equal("qwen3.5:9b", model);
    }

    [Fact]
    public void ResolveRuntimeModelForInputAttachments_returns_selected_model_when_provider_has_no_vision_model()
    {
        var provider = CreateProvider(ProviderKind.Ollama, "gptoss32k:latest");
        var options = CreateExecutionOptions([TestImageAttachment]);

        var model = MafAgentRuntime.ResolveRuntimeModelForInputAttachments(provider, provider.DefaultModel, options);

        Assert.Equal("gptoss32k:latest", model);
    }

    [Fact]
    public void AppendInputAttachmentAnalysis_places_authoritative_visual_evidence_before_user_prompt()
    {
        var prompt = """
            Use these workspace artifacts as input:
            - artifacts/chat-attachments/image.png

            Analyze the attached image.
            """;
        var analysis = CreateInputAttachmentAnalysis(
            "image.png",
            "artifacts/chat-attachments/image.png",
            "qwen3.5:9b",
            "A red square and a blue circle are visible.",
            12,
            34);

        var result = InvokeAppendInputAttachmentAnalysis(prompt, analysis);

        Assert.StartsWith("Request-scoped image attachment evidence:", result, StringComparison.Ordinal);
        Assert.Contains("Use this provider-generated visual evidence as authoritative", result);
        Assert.Contains("Vision model: qwen3.5:9b", result);
        Assert.Contains("Provider usage: inputTokens=12, outputTokens=34", result);
        Assert.True(
            result.IndexOf("A red square and a blue circle", StringComparison.Ordinal) <
            result.IndexOf("User request:", StringComparison.Ordinal));
        Assert.True(
            result.IndexOf("User request:", StringComparison.Ordinal) <
            result.IndexOf("Use these workspace artifacts", StringComparison.Ordinal));
    }

    [Fact]
    public void CreatePromptInputMessages_replaces_current_chat_turn_with_prepared_prompt()
    {
        var agent = CreateAgent();
        var provider = CreateProvider(ProviderKind.Ollama, "llama3.2:3b");
        var now = DateTimeOffset.UtcNow;
        var session = new ChatSessionRecord(
            Guid.NewGuid(),
            agent.Id,
            "Thread",
            now,
            now,
            [
                new ChatMessageRecord(Guid.NewGuid(), ChatMessageRole.User, "Original persisted prompt.", now, 3)
            ]);
        const string preparedPrompt = "Request-scoped image attachment evidence:\nUse this evidence.\n\nUser request:\nOriginal persisted prompt.";

        var messages = InvokeCreatePromptInputMessages(agent, provider, session, preparedPrompt, CreateExecutionOptions([]));

        var message = Assert.Single(messages);
        Assert.Equal(ChatRole.User, message.Role);
        Assert.Equal(preparedPrompt, Assert.Single(message.Contents.OfType<TextContent>()).Text);
    }

    [Fact]
    public void CreatePromptInputMessages_for_governed_process_step_ignores_prior_chat_transcript()
    {
        var agent = CreateAgent();
        var provider = CreateProvider(ProviderKind.Ollama, "llama3.2:3b");
        var now = DateTimeOffset.UtcNow;
        var session = new ChatSessionRecord(
            Guid.NewGuid(),
            agent.Id,
            "Thread",
            now,
            now,
            [
                new ChatMessageRecord(Guid.NewGuid(), ChatMessageRole.User, "Stale Tetris source context.", now.AddMinutes(-2), 1),
                new ChatMessageRecord(Guid.NewGuid(), ChatMessageRole.Assistant, "Prior assistant response.", now.AddMinutes(-1), 2),
                new ChatMessageRecord(Guid.NewGuid(), ChatMessageRole.User, "Original persisted prompt.", now, 3)
            ]);
        const string preparedPrompt = "Process step execution brief\n\nProject: Calculator";

        var messages = InvokeCreatePromptInputMessages(
            agent,
            provider,
            session,
            preparedPrompt,
            CreateExecutionOptions([], CreateGovernedProcessIntent()));

        var message = Assert.Single(messages);
        Assert.Equal(ChatRole.User, message.Role);
        var text = Assert.Single(message.Contents.OfType<TextContent>()).Text;
        Assert.Equal(preparedPrompt, text);
        Assert.DoesNotContain("Tetris", text, StringComparison.OrdinalIgnoreCase);
    }

    private static AgentRuntimeExecutionOptions CreateExecutionOptions(
        IReadOnlyList<AgentRuntimeInputAttachment> attachments,
        AgentRuntimeContextIntent? contextIntent = null)
    {
        return new AgentRuntimeExecutionOptions(
            StructuredOutput: null,
            FinalizerMode: AgentFinalizerMode.Disabled,
            RequireStructuredOutputValidation: true,
            MaxStructuredOutputRepairAttempts: 0,
            ContextIntent: contextIntent,
            InputAttachments: attachments);
    }

    private static AgentRuntimeContextIntent CreateGovernedProcessIntent()
    {
        return new AgentRuntimeContextIntent(
            SourceKind: "process-step",
            SourceId: "add-test-project",
            ProcessRunId: Guid.NewGuid().ToString("D"),
            ProcessStepId: Guid.NewGuid().ToString("D"),
            TargetScope: "ExternalProductTargetMutable",
            IsGovernedProcessStep: true,
            BrowserToolsAllowed: false,
            ScaffoldToolOnly: false,
            AllowsProductMutation: true,
            WorkspaceToolProfile: null,
            WorkspaceScope: WorkspaceScopeDescriptor.Project(Guid.NewGuid().ToString("D")),
            AllowedOperations: ["MutateProductTarget"]);
    }

    private static ProviderProfile CreateProvider(
        ProviderKind kind,
        string model)
    {
        return new ProviderProfile(
            Guid.NewGuid(),
            kind == ProviderKind.Ollama ? "Remote Ollama" : "OpenAI",
            kind,
            kind == ProviderKind.Ollama ? "http://localhost:11434" : "https://api.openai.com/v1",
            kind == ProviderKind.Ollama ? string.Empty : "OPENAI_API_KEY",
            model,
            ProviderTransportKind.ChatCompletions,
            IsEnabled: true,
            SupportsStreaming: true,
            SupportsTools: true,
            PreferFrameworkManagedChatHistory: true,
            SupportsBackgroundResponses: false,
            ConfigurationJson: "{}",
            Notes: string.Empty,
            HealthStatus: "Not checked",
            LastCheckedAtUtc: null,
            SuggestedModels: [model]);
    }

    private static AgentDefinition CreateAgent()
    {
        var now = DateTimeOffset.UtcNow;
        return new AgentDefinition(
            Guid.NewGuid(),
            "QA",
            "QA",
            "QA agent",
            "Inspect evidence.",
            AgentLifecycleStatus.Active,
            ProviderProfileId: null,
            Model: string.Empty,
            AgentWorkloadKind.Qa,
            AgentChatHistoryMode.FrameworkManaged,
            Temperature: 0,
            RequirePerServiceCallChatHistoryPersistence: false,
            EnableBackgroundResponses: false,
            ConfigurationJson: "{}",
            IsTemplate: false,
            TemplateKey: string.Empty,
            Permissions: AgentPermissionsPolicy.Default,
            Capabilities: [],
            Tags: [],
            CreatedAtUtc: now,
            UpdatedAtUtc: now);
    }

    private static object CreateInputAttachmentAnalysis(
        string name,
        string sourcePath,
        string model,
        string analysis,
        int inputTokens,
        int outputTokens)
    {
        var analysisType = typeof(MafAgentRuntime).GetNestedType("InputAttachmentAnalysis", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("InputAttachmentAnalysis nested type was not found.");

        return Activator.CreateInstance(analysisType, name, sourcePath, model, analysis, inputTokens, outputTokens)
            ?? throw new InvalidOperationException("InputAttachmentAnalysis could not be created.");
    }

    private static string InvokeAppendInputAttachmentAnalysis(
        string prompt,
        object analysis)
    {
        var analysisType = analysis.GetType();
        var analyses = Array.CreateInstance(analysisType, 1);
        analyses.SetValue(analysis, 0);
        var method = typeof(MafAgentRuntime).GetMethod("AppendInputAttachmentAnalysis", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("AppendInputAttachmentAnalysis method was not found.");

        return (string)(method.Invoke(null, [prompt, analyses])
            ?? throw new InvalidOperationException("AppendInputAttachmentAnalysis did not return a prompt."));
    }

    private static IReadOnlyList<ChatMessage> InvokeCreatePromptInputMessages(
        AgentDefinition agent,
        ProviderProfile provider,
        ChatSessionRecord session,
        string prompt,
        AgentRuntimeExecutionOptions runtimeOptions)
    {
        var method = typeof(MafAgentRuntime).GetMethod("CreatePromptInputMessages", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("CreatePromptInputMessages method was not found.");

        var result = method.Invoke(null, [agent, provider, session, prompt, runtimeOptions])
            ?? throw new InvalidOperationException("CreatePromptInputMessages did not return messages.");

        return ((IEnumerable<ChatMessage>)result).ToList();
    }
}
