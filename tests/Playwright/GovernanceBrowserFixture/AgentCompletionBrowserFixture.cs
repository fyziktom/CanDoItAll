using System.Threading.Channels;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Application;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Common;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Components;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Conversations;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Operations;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Voice;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.ProviderManagement;
using ProviderProfile = CanDoItAll.AgentFramework.Models.ProviderProfile;
using ProviderProfileEditorModel = CanDoItAll.AgentFramework.Models.ProviderProfileEditorModel;
using ProviderKind = CanDoItAll.AgentFramework.Models.ProviderKind;

internal enum AgentCompletionBrowserMode { Normal, Stream, Recovery, VoiceProvidersFailure, VoiceSaveFailure, VoiceSynthesisFailure, FloatingApplyWarning }

internal static class AgentCompletionBrowserServices {
    public static void Register(IServiceCollection services, GovernanceBrowserState fixture) {
        services.AddSingleton<ILlmChatConversationUiGateway>(fixture.Conversations);
        services.AddSingleton<ILlmChatOperationUiGateway>(fixture.Conversations);
        services.AddSingleton<ILlmChatUiEventSessionGateway>(fixture.Conversations);
        services.AddSingleton<IAgentVoiceService>(fixture.Settings);
        services.AddSingleton<IFloatingAgentChatSettingsService>(fixture.Settings);
        services.AddSingleton<IProviderRuntimeAdministrationService>(new VoiceProviderBrowserFixture(fixture.Settings));
        services.AddScoped<FloatingAgentChatCoordinator>();
        services.AddScoped<IFloatingAgentChatCoordinator>(provider => new FloatingCoordinatorBrowserFixture(provider.GetRequiredService<FloatingAgentChatCoordinator>(), fixture.Settings));
    }
}

internal sealed class ConversationBrowserFixture : ILlmChatConversationUiGateway, ILlmChatOperationUiGateway, ILlmChatUiEventSessionGateway {
    private static readonly Guid DefinitionId = Guid.Parse("31000000-0000-0000-0000-000000000001");
    private static readonly Guid ProviderId = Guid.Parse("31000000-0000-0000-0000-000000000099");
    private readonly List<LlmChatConversationListItem> conversations = Enumerable.Range(1, 26).Select(index => Conversation(
        Guid.Parse($"35000000-0000-0000-0000-{index:D12}"), index == 1 ? "Browser conversation A" : index == 2 ? "Browser conversation B" : $"Browser conversation {index:D2}")).ToList();
    private readonly Dictionary<Guid, LlmChatOperationView> operations = [];
    private readonly Dictionary<Guid, EventSession> sessions = [];
    public int Sends { get; private set; }
    public int Creates { get; private set; }
    public int Renames { get; private set; }
    public int Archives { get; private set; }
    public void SetMode(AgentCompletionBrowserMode mode) {
        if (mode == AgentCompletionBrowserMode.Stream) {
            foreach (var (id, session) in sessions) {
                if (operations[id].Status == LlmChatOperationStatus.Running) {
                    session.Publish(new(id, LlmChatOperationStatus.Running, false, "", [
                        new LlmChatOperationAttemptStartedEvent(new(id), 1, 1, "synthetic", LlmStreamingDeliveryMode.Incremental, DateTimeOffset.UtcNow),
                        new LlmChatOperationTextDeltaEvent(new(id), 2, 1, "Synthetic streamed answer", DateTimeOffset.UtcNow)
                    ], 1, 2));
                }
            }
        } else if (mode == AgentCompletionBrowserMode.Recovery) {
            var conversation = conversations[0];
            var id = Guid.NewGuid();
            conversations[0] = conversation with { ActiveOperationId = id };
            operations[id] = Operation(id, conversation.ConversationId, LlmChatOperationStatus.RecoveryRequired);
        }
    }
    public Task<LlmChatUiResult<LlmChatPage<LlmChatConversationListItem, LlmChatConversationCursor>>> ListPageAsync(LlmChatConversationQuery query, CancellationToken cancellationToken = default) {
        var offset = query.Cursor is { } cursor ? conversations.FindIndex(item => item.ConversationId == cursor.ConversationId.Value) + 1 : 0;
        var page = conversations.Skip(offset).Take(query.Take).ToArray();
        LlmChatConversationCursor? next = offset + page.Length < conversations.Count ? new(page[^1].UpdatedAtUtc, new(page[^1].ConversationId)) : null;
        return Result(new LlmChatPage<LlmChatConversationListItem, LlmChatConversationCursor>(page, next));
    }
    public Task<LlmChatUiResult<LlmChatConversationView>> GetAsync(Guid conversationId, LlmChatTranscriptQuery query, CancellationToken cancellationToken = default) {
        var conversation = conversations.FirstOrDefault(item => item.ConversationId == conversationId);
        if (conversation is null) {
            return Task.FromResult(LlmChatUiResult<LlmChatConversationView>.Failure(new LlmChatUiFailure(LlmChatErrorCodes.ConversationNotFound, "Synthetic missing conversation")));
        }
        return Result(new LlmChatConversationView(conversation,
            query.Cursor is null ? [Message("First canonical question", LlmMessageRole.User), Message("First canonical answer", LlmMessageRole.Assistant)]
                : [Message("Paged canonical answer", LlmMessageRole.Assistant)], query.Cursor is null ? new(2) : null));
    }
    public Task<LlmChatUiResult<LlmChatConversationView>> CreateAsync(Guid definitionId, string title, CancellationToken cancellationToken = default) {
        Creates++;
        var conversation = Conversation(Guid.NewGuid(), title) with { DefinitionId = definitionId };
        conversations.Insert(0, conversation);
        return Result(new LlmChatConversationView(conversation, [], null));
    }
    public Task<LlmChatUiResult<LlmChatConversationView>> RenameAsync(Guid conversationId, string title, long expectedConcurrencyToken, long expectedTranscriptRevision, CancellationToken cancellationToken = default) {
        Renames++;
        var index = conversations.FindIndex(item => item.ConversationId == conversationId);
        conversations[index] = conversations[index] with { Title = title, ConcurrencyToken = expectedConcurrencyToken + 1 };
        return Result(new LlmChatConversationView(conversations[index], [], null));
    }
    public Task<LlmChatUiResult<LlmChatConversationView>> ArchiveAsync(Guid conversationId, long expectedConcurrencyToken, CancellationToken cancellationToken = default) {
        Archives++;
        var index = conversations.FindIndex(item => item.ConversationId == conversationId);
        conversations[index] = conversations[index] with { Status = LlmChatConversationStatus.Archived, ConcurrencyToken = expectedConcurrencyToken + 1 };
        return Result(new LlmChatConversationView(conversations[index], [], null));
    }
    public Task<LlmChatUiResult<LlmChatOperationView>> SendAsync(Guid operationId, Guid conversationId, long expectedTranscriptRevision, string message, CancellationToken cancellationToken = default) {
        Sends++;
        var index = conversations.FindIndex(item => item.ConversationId == conversationId);
        conversations[index] = conversations[index] with { ActiveOperationId = operationId };
        operations[operationId] = Operation(operationId, conversationId, LlmChatOperationStatus.Running);
        return Result(operations[operationId]);
    }
    public Task<LlmChatUiResult<LlmChatOperationView>> GetAsync(Guid operationId, CancellationToken cancellationToken = default) => Result(operations[operationId]);
    public Task<LlmChatUiResult<LlmChatOperationView>> CancelAsync(Guid operationId, CancellationToken cancellationToken = default) {
        operations[operationId] = operations[operationId] with { Status = LlmChatOperationStatus.CancellationRequested };
        return Result(operations[operationId]);
    }
    public Task<LlmChatUiResult<LlmChatOperationView>> ReconcileAsync(Guid operationId, CancellationToken cancellationToken = default) => Result(operations[operationId]);
    public Task<LlmChatUiResult<LlmChatOperationView>> AbandonAsync(Guid conversationId, Guid operationId, CancellationToken cancellationToken = default) {
        var index = conversations.FindIndex(item => item.ConversationId == conversationId);
        conversations[index] = conversations[index] with { ActiveOperationId = null };
        operations[operationId] = operations[operationId] with { Status = LlmChatOperationStatus.Failed, CompletedAtUtc = DateTimeOffset.UtcNow };
        return Result(operations[operationId]);
    }
    public ValueTask<LlmChatUiResult<ILlmChatUiEventSession>> OpenAsync(Guid operationId, CancellationToken cancellationToken = default) {
        var session = new EventSession();
        sessions[operationId] = session;
        return ValueTask.FromResult(LlmChatUiResult<ILlmChatUiEventSession>.Success(session));
    }
    private static Task<LlmChatUiResult<T>> Result<T>(T value) => Task.FromResult(LlmChatUiResult<T>.Success(value));
    private static LlmChatConversationListItem Conversation(Guid id, string title) => new(id, DefinitionId, 3, title, "Synthetic research assistant",
        new(ProviderId, "Synthetic provider", ProviderKind.OpenAi, "synthetic"), LlmChatConversationStatus.Active, LlmChatConversationOrigin.Application, 1, 2, null, DateTimeOffset.UnixEpoch);
    private static LlmChatMessageListItem Message(string text, LlmMessageRole role) => new(Guid.NewGuid(), Guid.NewGuid(), role, text, DateTimeOffset.UnixEpoch, "synthetic");
    private static LlmChatOperationView Operation(Guid id, Guid conversationId, LlmChatOperationStatus status) => new(id, conversationId, status, DateTimeOffset.UtcNow, null, null, 0, "", "synthetic", null);
    private sealed class EventSession : ILlmChatUiEventSession {
        private readonly Channel<LlmChatUiOperationEventPage> pages = Channel.CreateUnbounded<LlmChatUiOperationEventPage>();
        public CancellationToken ProfileLifetime => CancellationToken.None;
        public int MaximumPageSize => 50;
        public void Publish(LlmChatUiOperationEventPage page) => pages.Writer.TryWrite(page);
        public async ValueTask<LlmChatUiOperationEventPage> ReadAsync(long afterSequence, int take, TimeSpan maximumWait, CancellationToken cancellationToken = default) => await pages.Reader.ReadAsync(cancellationToken);
        public ValueTask DisposeAsync() {
            pages.Writer.TryComplete();
            return ValueTask.CompletedTask;
        }
    }
}

internal sealed class AgentSettingsBrowserFixture : IAgentVoiceService, IFloatingAgentChatSettingsService {
    public AgentCompletionBrowserMode Mode { get; set; }
    public int VoiceSaves { get; private set; }
    public int Samples { get; private set; }
    public int FloatingSaves { get; private set; }
    public int Applies { get; private set; }
    private AgentVoiceSettings voice = AgentVoiceSettings.Default;
    private FloatingAgentChatSettings floating = FloatingAgentChatSettings.Default;
    public Task<AgentVoiceSettings> GetSettingsAsync(CancellationToken cancellationToken = default) => Task.FromResult(AgentVoiceSettingsNormalizer.Normalize(voice));
    public Task<AgentVoiceSettings> SaveSettingsAsync(AgentVoiceSettings settings, CancellationToken cancellationToken = default) {
        VoiceSaves++;
        if (Mode == AgentCompletionBrowserMode.VoiceSaveFailure) {
            throw new IOException("Synthetic voice save failure");
        }
        voice = AgentVoiceSettingsNormalizer.Normalize(settings);
        return GetSettingsAsync(cancellationToken);
    }
    public Task<AgentVoiceSynthesisResult> SynthesizeSampleAsync(string? sampleText = null, CancellationToken cancellationToken = default) {
        Samples++;
        if (Mode == AgentCompletionBrowserMode.VoiceSynthesisFailure) {
            throw new IOException("Synthetic synthesis failure");
        }
        return Task.FromResult(new AgentVoiceSynthesisResult([1, 2, 3], "audio/wav", "synthetic", voice.TextToSpeech.VoiceId, "wav"));
    }
    public Task<AgentVoiceTranscriptionResult> TranscribeAsync(AgentVoiceTranscriptionRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<AgentVoiceSynthesisResult> SynthesizeAsync(AgentVoiceSynthesisRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public IAsyncEnumerable<AgentVoiceSynthesisResult> SynthesizeChunksAsync(AgentVoiceSynthesisRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    Task<FloatingAgentChatSettings> IFloatingAgentChatSettingsService.GetSettingsAsync(CancellationToken cancellationToken) => Task.FromResult(floating);
    public Task<FloatingAgentChatSettings> SaveSettingsAsync(FloatingAgentChatSettings settings, CancellationToken cancellationToken = default) {
        FloatingAgentChatSettingsValidator.Validate(settings);
        FloatingSaves++;
        floating = settings;
        return Task.FromResult(settings);
    }
    public void Applying() {
        Applies++;
        if (Mode == AgentCompletionBrowserMode.FloatingApplyWarning) {
            throw new IOException("Synthetic coordinator apply failure");
        }
    }
}

internal sealed class VoiceProviderBrowserFixture(AgentSettingsBrowserFixture fixture) : IProviderRuntimeAdministrationService {
    public Task<IReadOnlyList<ProviderProfile>> ListProvidersAsync(CancellationToken cancellationToken = default) {
        if (fixture.Mode == AgentCompletionBrowserMode.VoiceProvidersFailure) {
            throw new IOException("Synthetic provider read failure");
        }
        return Task.FromResult<IReadOnlyList<ProviderProfile>>([
            new(Guid.Parse("34000000-0000-0000-0000-000000000001"), "OpenAI default", ProviderKind.OpenAi, "http://127.0.0.1:1", "", "synthetic", ProviderTransportKind.ChatCompletions, true, false, false, false, false, "{}", "Fixture only", "", null, ["synthetic"])
        ]);
    }
    public Task<ProviderProfileEditorModel> GetProviderEditorAsync(Guid? providerId = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<Guid> SaveProviderAsync(ProviderProfileEditorModel model, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task DeleteProviderAsync(Guid providerId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<ProviderHealthResult> TestProviderAsync(Guid providerId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<ProviderTestChatResult> RunProviderTestChatAsync(Guid providerId, ProviderTestChatRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<ProviderModelMaintenanceEditorResult> CreateOrUpdateProviderModelAsync(Guid providerId, ProviderModelMaintenanceEditorRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
}

internal sealed class FloatingCoordinatorBrowserFixture(IFloatingAgentChatCoordinator inner, AgentSettingsBrowserFixture fixture) : IFloatingAgentChatCoordinator {
    public event EventHandler? Changed {
        add => inner.Changed += value;
        remove => inner.Changed -= value;
    }
    public FloatingAgentChatSettings CurrentSettings => inner.CurrentSettings;
    public FloatingAgentChatState Snapshot() => inner.Snapshot();
    public Task InitializeAsync(CancellationToken cancellationToken = default) => inner.InitializeAsync(cancellationToken);
    public void ApplySettings(FloatingAgentChatSettings settings) {
        fixture.Applying();
        inner.ApplySettings(settings);
    }
    public void ShowCatalog(AgentChatCatalogTab tab = AgentChatCatalogTab.Agents) => inner.ShowCatalog(tab);
    public void HideCatalog() => inner.HideCatalog();
    public Task<ActiveAgentChat> StartNewChatAsync(Guid agentId, CancellationToken cancellationToken = default) => throw new NotSupportedException("The settings fixture cannot start agent execution.");
    public Task<ActiveAgentChat> OpenChatAsync(Guid agentId, Guid chatSessionId, CancellationToken cancellationToken = default) => throw new NotSupportedException("The settings fixture cannot open agent execution.");
    public ActiveAgentChat ShowChat(AgentChatHandleId handleId) => inner.ShowChat(handleId);
    public ActiveAgentChat KeepActive(AgentChatHandleId handleId) => inner.KeepActive(handleId);
    public void Stop(AgentChatHandleId handleId) => inner.Stop(handleId);
    public ActiveAgentChat SetRunState(AgentChatHandleId handleId, ActiveAgentChatRunState runState) => inner.SetRunState(handleId, runState);
    public bool TryBeginOperation(AgentChatHandleId handleId) => inner.TryBeginOperation(handleId);
    public void ReconcileRunStateAfterOperation(AgentChatHandleId handleId) => inner.ReconcileRunStateAfterOperation(handleId);
    public ActiveAgentChat AttachSession(AgentChatHandleId handleId, Guid chatSessionId) => inner.AttachSession(handleId, chatSessionId);
    public int PruneExpired() => inner.PruneExpired();
}
