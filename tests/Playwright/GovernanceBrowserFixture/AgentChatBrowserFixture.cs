using System.Reflection;
using System.Runtime.CompilerServices;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Voice;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.SharedKernel.Streaming;
using CanDoItAll.Modules.AgentFramework;

internal enum AgentChatBrowserMode { Normal, HoldWorkspace, HoldExecution, Approvals, Failed, Long }

internal sealed class AgentChatBrowserFixture : IAgentChatExecutionOrchestrator, IAgentVoiceService, IAgentChatAttachmentStagingService {
    public static readonly Guid SessionA = Guid.Parse("41000000-0000-0000-0000-000000000001");
    public static readonly Guid SessionA2 = Guid.Parse("41000000-0000-0000-0000-000000000002");
    public static readonly Guid SessionB = Guid.Parse("41000000-0000-0000-0000-000000000003");
    private readonly Dictionary<Guid, ChatSessionRecord> sessions = new[] {
        Session(SessionA, GovernanceBrowserState.AgentA, "Fixture thread A"),
        Session(SessionA2, GovernanceBrowserState.AgentA, "Fixture thread A2"),
        Session(SessionB, GovernanceBrowserState.AgentB, "Fixture thread B")
    }.ToDictionary(item => item.Id);
    private readonly Dictionary<Guid, ExecutionRunDetail> runs = [];
    private readonly HashSet<Guid> favorites = [];
    private TaskCompletionSource held = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int sends, approvals, creates, renames, uploads, transcriptions, speech, workspaces, favoriteSaves;
    private readonly AgentExecutionActivityCoordinator activity = new(
        new PartitionedSequencedStream<AgentExecutionActivityStreamId, AgentExecutionActivity>(PartitionedSequencedStreamPolicy.Default, TimeProvider.System), TimeProvider.System);
    public AgentChatBrowserMode Mode { get; set; }
    public object Counters => new { sends, approvals, creates, renames, uploads, transcriptions, speech, workspaces, favoriteSaves };

    public static void Register(IServiceCollection services, AgentChatBrowserFixture fixture) {
        var original = services.Last(item => item.ServiceType == typeof(IAgentFrameworkWorkspaceService));
        services.AddScoped<IAgentFrameworkWorkspaceService>(provider => {
            var service = DispatchProxy.Create<IAgentFrameworkWorkspaceService, ChatWorkspaceBrowserProxy>();
            var proxy = (ChatWorkspaceBrowserProxy)service;
            proxy.Inner = (IAgentFrameworkWorkspaceService)(original.ImplementationInstance
                ?? original.ImplementationFactory?.Invoke(provider)
                ?? ActivatorUtilities.CreateInstance(provider, original.ImplementationType!));
            proxy.Fixture = fixture;
            return service;
        });
        services.AddSingleton<IAgentChatExecutionOrchestrator>(fixture);
        services.AddSingleton<IAgentChatAttachmentStagingService>(fixture);
        services.AddSingleton<IAgentVoiceService>(fixture);
        services.AddSingleton<IAgentExecutionActivityReader>(fixture.activity);
        services.AddScoped<IFloatingAgentChatCoordinator>(provider => provider.GetRequiredService<FloatingAgentChatCoordinator>());
    }

    public void Release() {
        var previous = held;
        held = new(TaskCreationOptions.RunContinuationsAsynchronously);
        previous.TrySetResult();
    }

    public async Task<IReadOnlyList<AgentDefinition>> Agents(IAgentFrameworkWorkspaceService inner, CancellationToken token) {
        var agents = await inner.ListAgentsAsync(false, token);
        return agents.Where(agent => agent.Id == GovernanceBrowserState.AgentA || agent.Id == GovernanceBrowserState.AgentB)
            .Select(agent => agent with {
                ConfigurationJson = AgentVoiceAccessMetadata.Write(agent.ConfigurationJson, new() { CanUseVoiceMode = true }),
                Tags = favorites.Contains(agent.Id) ? [AgentSpecialTags.Favorite] : []
            }).ToArray();
    }

    public async Task<ChatAgentWorkspaceSnapshot> Workspace(Guid agent, Guid? requested) {
        workspaces++;
        var mode = Mode;
        var thread = sessions.Values.FirstOrDefault(item => item.AgentId == agent && (!requested.HasValue || item.Id == requested));
        if (mode == AgentChatBrowserMode.HoldWorkspace && agent == GovernanceBrowserState.AgentA) {
            await held.Task;
        }
        if (thread is null) {
            return new(agent, [], null, requested, null);
        }
        if (mode == AgentChatBrowserMode.Long) {
            thread = thread with { Messages = [Message(ChatMessageRole.User, string.Concat(Enumerable.Repeat("Full legitimate message <encoded> ", 160)))] };
        }
        if (mode == AgentChatBrowserMode.Approvals) {
            var run = Run(thread) with { State = ExecutionState.WaitingOnTool, PendingApprovals = [
                new("fixture-read", "read", "workspace_write_file", "function", "MCP: Research server", "{\"path\":\"artifacts/fixture.txt\",\"content\":\"PRIVATE_BROWSER_CONTENT_482\",\"token\":\"PRIVATE_BROWSER_TOKEN_482\",\"request\":{\"projectId\":\"project-42\",\"password\":\"PRIVATE_BROWSER_NESTED_482\"}}"),
                new("fixture-write", "write", "Save result", "function", "Review the fixture result", "{malformed PRIVATE_BROWSER_MALFORMED_482")
            ] };
            runs[run.Id] = Detail(run, thread);
            thread = thread with { LatestExecutionRunId = run.Id };
            sessions[thread.Id] = thread;
        }
        var latest = thread.LatestExecutionRunId is { } runId && runs.TryGetValue(runId, out var value) ? value.Run : null;
        return new(agent, sessions.Values.Where(item => item.AgentId == agent).Select(item => new ChatSessionSummaryRecord(
            item.Id, agent, item.Title, item.CreatedAtUtc, item.UpdatedAtUtc, item.Messages.Count, item.Messages.LastOrDefault()?.Content ?? "", 0, false)).ToArray(), thread, thread.Id, null) { SelectedRun = latest };
    }

    public Task<ChatSessionRecord> Create(Guid agent) {
        creates++;
        var thread = Session(Guid.NewGuid(), agent, "New fixture thread");
        sessions.Add(thread.Id, thread);
        return Task.FromResult(thread);
    }

    public Task<ChatSessionRecord> Rename(Guid agent, Guid session, string title) {
        if (sessions[session].AgentId != agent) {
            throw new InvalidOperationException("Fixture rename target mismatch.");
        }
        renames++;
        sessions[session] = sessions[session] with { Title = title };
        return Task.FromResult(sessions[session]);
    }

    public Task<Guid> Favorite(AgentEditorModel editor) {
        favoriteSaves++;
        var id = editor.Id!.Value;
        if (editor.Tags.Any(AgentSpecialTags.IsFavorite)) {
            favorites.Add(id);
        } else {
            favorites.Remove(id);
        }
        return Task.FromResult(id);
    }

    public Task<ExecutionRunDetail> ReadDetail(Guid id) => Task.FromResult(runs[id]);

    public AgentChatOperationHandle StartSendMessage(AgentChatSendRequest request, CancellationToken cancellationToken = default) {
        sends++;
        return Operation(Complete(request, Mode));
    }
    public AgentChatOperationHandle StartSendMessage(Guid agentId, Guid? chatSessionId, string prompt, IReadOnlyList<string>? attachmentPaths = null, CancellationToken cancellationToken = default)
        => StartSendMessage(new(agentId, chatSessionId, prompt) { AttachmentPaths = attachmentPaths }, cancellationToken);
    public AgentChatOperationHandle StartApprovalContinuation(Guid agentId, Guid chatSessionId, IReadOnlyList<PendingToolApprovalDecision> decisions, bool autoApprovePendingToolCalls = false, CancellationToken cancellationToken = default) {
        approvals++;
        return Operation(Complete(new(agentId, chatSessionId, "Synthetic approval continuation"), AgentChatBrowserMode.Normal));
    }
    public Task<AgentChatRunResult> SendMessageAsync(AgentChatSendRequest request, CancellationToken cancellationToken = default) => StartSendMessage(request, cancellationToken).Completion;
    public Task<AgentChatRunResult> SendMessageAsync(Guid agentId, Guid? chatSessionId, string prompt, IReadOnlyList<string>? attachmentPaths = null, CancellationToken cancellationToken = default)
        => StartSendMessage(agentId, chatSessionId, prompt, attachmentPaths, cancellationToken).Completion;
    public Task<AgentChatRunResult> RespondToPendingApprovalsAsync(Guid agentId, Guid chatSessionId, IReadOnlyList<PendingToolApprovalDecision> decisions, bool autoApprovePendingToolCalls = false, CancellationToken cancellationToken = default)
        => StartApprovalContinuation(agentId, chatSessionId, decisions, autoApprovePendingToolCalls, cancellationToken).Completion;

    private async Task<AgentChatRunResult> Complete(AgentChatSendRequest request, AgentChatBrowserMode mode) {
        var thread = request.ChatSessionId is { } id ? sessions[id] : await Create(request.AgentId);
        var run = Run(thread);
        if (mode == AgentChatBrowserMode.HoldExecution) {
            await held.Task;
        }
        var answer = Message(ChatMessageRole.Assistant, "Synthetic answer: " + request.Prompt);
        thread = thread with { Messages = [.. thread.Messages, Message(ChatMessageRole.User, request.Prompt), answer], LatestExecutionRunId = run.Id };
        sessions[thread.Id] = thread;
        if (mode == AgentChatBrowserMode.Failed) {
            run = run with { State = ExecutionState.Failed, ResultSummary = "Synthetic persisted failure" };
        }
        runs[run.Id] = Detail(run, thread);
        if (mode == AgentChatBrowserMode.Failed) {
            throw new AgentChatRunFailedException(request.AgentId, run.Id, thread.Id, "fixture", "synthetic", new IOException("fixture internal error"), "Synthetic persisted failure");
        }
        return new(thread.Id, answer, runs[run.Id].Metrics[0]) { ExecutionRunId = run.Id, State = run.State };
    }

    private AgentChatOperationHandle Operation(Task<AgentChatRunResult> completion) {
        var profile = Guid.NewGuid();
        var stream = new AgentExecutionActivityStreamId(profile, WorkspaceScopeDescriptor.Organization(profile.ToString("N")), new DatabaseProfileGeneration(0), AgentExecutionOperationId.New());
        var admitted = (AgentExecutionActivityAdmitted)activity.AdmitOperation(stream, null, null, "Synthetic operation accepted");
        return new(stream, CompleteActivity(completion, admitted.Operation));
    }
    private static async Task<AgentChatRunResult> CompleteActivity(Task<AgentChatRunResult> completion, IAgentExecutionActivityOperationLease lease) {
        using (lease) {
            try {
                var result = await completion;
                lease.Report(AgentExecutionActivityPhase.PersistingResult, "Synthetic result recorded");
                lease.Complete("Synthetic operation completed");
                return result;
            } catch {
                lease.Fail("Synthetic operation failed");
                throw;
            }
        }
    }
    private static ChatSessionRecord Session(Guid id, Guid agent, string title)
        => new(id, agent, title, GovernanceBrowserState.Observed, GovernanceBrowserState.Observed, [Message(ChatMessageRole.User, "Initial fixture question"), Message(ChatMessageRole.Assistant, "Initial fixture answer")]);
    private static ChatMessageRecord Message(ChatMessageRole role, string text) => new(Guid.NewGuid(), role, text, DateTimeOffset.UtcNow, 10);
    private static ExecutionRunRecord Run(ChatSessionRecord thread)
        => GovernanceBrowserState.Run(Guid.NewGuid(), thread.AgentId, "Synthetic chat execution", 0) with { ChatSessionId = thread.Id };
    private static ExecutionRunDetail Detail(ExecutionRunRecord run, ChatSessionRecord thread)
        => new(run, thread, [new(Guid.NewGuid(), run.AgentId, thread.Id, DateTimeOffset.UtcNow, run.State, "Fixture execution", "Synthetic execution evidence. token=PRIVATE_BROWSER_LOG_482") { ExecutionRunId = run.Id }],
            [new(Guid.NewGuid(), run.AgentId, thread.Id, DateTimeOffset.UtcNow, RunOutcome.Succeeded, "fixture", "synthetic", 15, 10, 20, 0) { ExecutionRunId = run.Id }]) {
                Artifacts = [new(Guid.NewGuid(), run.Id, "text", "Fixture artifact", "  artifacts\\fixture.txt  ", "text/plain", "fixture", "Synthetic artifact", DateTimeOffset.UtcNow),
                    new(Guid.NewGuid(), run.Id, "text", "Internal artifact", "../PRIVATE_BROWSER_PATH_482", "text/plain", "fixture", "Synthetic artifact", DateTimeOffset.UtcNow)]
            };

    public Task<AgentChatAttachmentStagingResult> StageImageAsync(string fileName, string? contentType, long sizeBytes, Stream content, CancellationToken cancellationToken = default) {
        uploads++;
        return Task.FromResult(new AgentChatAttachmentStagingResult("  uploads\\fixture.png  ", "image/png", sizeBytes));
    }
    public Task<AgentVoiceSettings> GetSettingsAsync(CancellationToken cancellationToken = default) => Task.FromResult(new AgentVoiceSettings());
    public Task<AgentVoiceSettings> SaveSettingsAsync(AgentVoiceSettings settings, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<AgentVoiceSynthesisResult> SynthesizeSampleAsync(string? text = null, CancellationToken cancellationToken = default)
        => SynthesizeAsync(new(text ?? "Synthetic sample"), cancellationToken);
    public Task<AgentVoiceTranscriptionResult> TranscribeAsync(AgentVoiceTranscriptionRequest request, CancellationToken cancellationToken = default) {
        transcriptions++;
        return Task.FromResult(new AgentVoiceTranscriptionResult("Synthetic spoken prompt", "fixture"));
    }
    public Task<AgentVoiceSynthesisResult> SynthesizeAsync(AgentVoiceSynthesisRequest request, CancellationToken cancellationToken = default) {
        speech++;
        return Task.FromResult(new AgentVoiceSynthesisResult([1, 2, 3], "audio/wav", "fixture", "synthetic", "wav"));
    }
    public async IAsyncEnumerable<AgentVoiceSynthesisResult> SynthesizeChunksAsync(AgentVoiceSynthesisRequest request, [EnumeratorCancellation] CancellationToken cancellationToken = default) {
        yield return await SynthesizeAsync(request, cancellationToken);
    }
}

public class ChatWorkspaceBrowserProxy : DispatchProxy {
    internal IAgentFrameworkWorkspaceService Inner { get; set; } = default!;
    internal AgentChatBrowserFixture Fixture { get; set; } = default!;
    protected override object? Invoke(MethodInfo? method, object?[]? args) => method?.Name switch {
        nameof(IAgentFrameworkWorkspaceService.ListAgentsAsync) => Fixture.Agents(Inner, (CancellationToken)args![1]!),
        nameof(IAgentFrameworkWorkspaceService.GetChatAgentWorkspaceAsync) => Fixture.Workspace((Guid)args![0]!, (Guid?)args![1]),
        nameof(IAgentFrameworkWorkspaceService.GetOrCreateChatSessionAsync) => Fixture.Create((Guid)args![0]!),
        nameof(IAgentFrameworkWorkspaceService.RenameChatSessionAsync) => Fixture.Rename((Guid)args![0]!, (Guid)args[1]!, (string)args[2]!),
        nameof(IAgentFrameworkWorkspaceService.GetExecutionRunDetailAsync) => Fixture.ReadDetail((Guid)args![0]!),
        nameof(IAgentFrameworkWorkspaceService.SaveAgentAsync) => Fixture.Favorite((AgentEditorModel)args![0]!),
        nameof(IAgentFrameworkWorkspaceService.SendMessageAsync) or nameof(IAgentFrameworkWorkspaceService.RespondToPendingApprovalsAsync)
            or nameof(IAgentFrameworkWorkspaceService.ExecuteRunAsync) => throw new InvalidOperationException("Browser execution must use the synthetic orchestrator."),
        _ => method!.Invoke(Inner, args)
    };
}
