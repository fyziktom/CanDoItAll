using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.Tests.Support;
using Xunit.Abstractions;

namespace CanDoItAll.Tests.Integration;

public sealed class FileSandboxWorkspaceAdmissionReadScalingIntegrationTests(
    ITestOutputHelper output)
{
    private const int HistoricalInputTokens = 10;
    private const int HistoricalOutputTokens = 5;
    private const int AdmissionInputTokens = 23;
    private const int AdmissionOutputTokens = 7;
    private const decimal HistoricalCostUsd = 0.01m;
    private const decimal AdmissionCostUsd = 0.02m;
    private static readonly StringComparer PathComparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    [Theory]
    [InlineData(4)]
    [InlineData(96)]
    public async Task BeginChatBackedRunAsync_NewSession_DoesNotReadHistoricalRunOrUsagePayloads(
        int historicalRunCount)
    {
        await using var scenario = await CreateScenarioAsync(
            historicalRunCount,
            includeExistingSession: false);

        var result = await ((ISandboxWorkspaceChatRunStartStore)scenario.Store)
            .BeginChatBackedRunAsync(
                scenario.CreateRequest(),
                context => CreateAdmissionMutation(context, "new-session admission"));

        var started = Assert.IsType<ChatBackedRunStarted>(result);
        Assert.Null(scenario.Session);
        Assert.NotNull(started.Detail.ChatSession);
        Assert.Empty(GetHistoricalRunHeaderReads(scenario));
        Assert.Empty(GetHistoricalUsageReads(scenario));
        AssertReadBudget(
            scenario,
            expectedPhysicalReadCount: 11,
            "new-session");
    }

    [Theory]
    [InlineData(4)]
    [InlineData(96)]
    public async Task BeginChatBackedRunAsync_ExistingSession_ReadsOnlyLatestHistoricalRunHeader(
        int historicalRunCount)
    {
        await using var scenario = await CreateScenarioAsync(
            historicalRunCount,
            includeExistingSession: true);

        var result = await ((ISandboxWorkspaceChatRunStartStore)scenario.Store)
            .BeginChatBackedRunAsync(
                scenario.CreateRequest(),
                context => CreateAdmissionMutation(context, "existing-session admission"));

        Assert.IsType<ChatBackedRunStarted>(result);
        var headerRead = Assert.Single(GetHistoricalRunHeaderReads(scenario));
        Assert.Equal(
            NormalizePath(
                scenario.Layout.RunPath(
                    scenario.LatestHistoricalRunId)),
            headerRead.FullPath,
            PathComparer);
        Assert.Equal(
            typeof(ExecutionRunRecord),
            headerRead.PayloadType);
        Assert.Equal(
            FileSandboxWorkspaceJsonReadKind.Deserialization,
            headerRead.Kind);
        Assert.Empty(GetHistoricalUsageReads(scenario));
        AssertReadBudget(
            scenario,
            expectedPhysicalReadCount: 15,
            "existing-terminal-latest");
    }

    [Fact]
    public async Task BeginChatBackedRunAsync_ActiveLatestRun_BlocksFromSingleHeaderRead()
    {
        await using var scenario = await CreateScenarioAsync(
            historicalRunCount: 96,
            includeExistingSession: true,
            latestRunState: ExecutionState.Running);
        var mutationFactoryCalled = false;

        var result = await ((ISandboxWorkspaceChatRunStartStore)scenario.Store)
            .BeginChatBackedRunAsync(
                scenario.CreateRequest(),
                context =>
                {
                    mutationFactoryCalled = true;
                    return CreateAdmissionMutation(context, "must remain blocked");
                });

        var blocked = Assert.IsType<ChatBackedRunBlocked>(result);
        Assert.False(mutationFactoryCalled);
        Assert.Equal(
            scenario.LatestHistoricalRunId,
            blocked.BlockingRun.Id);
        var headerRead = Assert.Single(GetHistoricalRunHeaderReads(scenario));
        Assert.Equal(
            NormalizePath(
                scenario.Layout.RunPath(
                    scenario.LatestHistoricalRunId)),
            headerRead.FullPath,
            PathComparer);
        Assert.Empty(GetHistoricalUsageReads(scenario));
    }

    [Fact]
    public async Task PendingNewRunRecovery_AppliesUsageDeltaWithoutHistoricalPayloadScan()
    {
        await using var scenario = await CreateScenarioAsync(
            historicalRunCount: 24,
            includeExistingSession: false);
        var failureInjected = false;
        var failingStore = new FileSandboxWorkspaceStore(
            scenario.WorkspaceRoot,
            scenario.Scope,
            stage =>
            {
                if (!failureInjected &&
                    stage == ChatBackedRunCommitStage.ExecutionSlicesPersisted)
                {
                    failureInjected = true;
                    throw new InjectedCommitFailureException(stage);
                }
            },
            existingRunDetailCommitBoundary: null,
            jsonReadDiagnostics: scenario.Diagnostics);

        await Assert.ThrowsAsync<InjectedCommitFailureException>(
            () => ((ISandboxWorkspaceChatRunStartStore)failingStore)
                .BeginChatBackedRunAsync(
                    scenario.CreateRequest(),
                    context => CreateAdmissionMutation(
                        context,
                        "usage recovery admission")));
        Assert.True(failureInjected);
        Assert.True(File.Exists(scenario.PendingChatRunJournalPath));
        scenario.ReadRecorder.Clear();

        var recoveryStore = new FileSandboxWorkspaceStore(
            scenario.WorkspaceRoot,
            scenario.Scope,
            chatBackedRunCommitBoundary: null,
            existingRunDetailCommitBoundary: null,
            jsonReadDiagnostics: scenario.Diagnostics);
        var recoveredProjection =
            await recoveryStore.LoadUsageProjectionAsync();

        Assert.False(File.Exists(scenario.PendingChatRunJournalPath));
        Assert.Equal(
            scenario.InitialUsageProjection.Revision + 1L,
            recoveredProjection.Revision);
        Assert.Equal(
            scenario.InitialUsageProjection.UsageObservationCount + 1,
            recoveredProjection.UsageObservationCount);
        Assert.Equal(
            scenario.InitialUsageProjection.TotalTokens +
            AdmissionInputTokens +
            AdmissionOutputTokens,
            recoveredProjection.TotalTokens);
        Assert.Equal(
            scenario.InitialUsageProjection.KnownCostUsd +
            AdmissionCostUsd,
            recoveredProjection.KnownCostUsd);
        var agentProjection = Assert.Single(
            recoveredProjection.Agents,
            row => row.AgentId == scenario.Agent.Id);
        Assert.Equal(
            scenario.HistoricalRunCount + 1,
            agentProjection.RunCount);
        Assert.Equal(
            scenario.HistoricalRunCount + 1,
            agentProjection.UsageObservationCount);
        Assert.Empty(GetHistoricalRunHeaderReads(scenario));
        Assert.Empty(GetHistoricalUsageReads(scenario));
    }

    private static async Task<AdmissionScenario> CreateScenarioAsync(
        int historicalRunCount,
        bool includeExistingSession,
        ExecutionState latestRunState = ExecutionState.Completed)
    {
        if (historicalRunCount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(historicalRunCount),
                historicalRunCount,
                "At least one historical run is required.");
        }

        if (latestRunState is not ExecutionState.Completed and
            not ExecutionState.Running)
        {
            throw new ArgumentOutOfRangeException(
                nameof(latestRunState),
                latestRunState,
                "The test scenario supports completed or running latest runs.");
        }

        var environment = CanDoItAllTestEnvironment.Create(
            $"workspace-admission-read-scaling-{Guid.NewGuid():N}");
        try
        {
            var profile = environment.CreateInMemoryProfile("primary");
            var scope = WorkspaceScopeDescriptor.Sandbox;
            var setupStore = new FileSandboxWorkspaceStore(
                profile.WorkspaceRootPath,
                scope);
            var catalogSnapshot =
                await setupStore.LoadCatalogSnapshotAsync();
            var agent = catalogSnapshot.Catalog.Agents.First(
                candidate => candidate.ProviderProfileId.HasValue);
            var sessionId = includeExistingSession
                ? Guid.NewGuid()
                : (Guid?)null;
            var baseTimeUtc = DateTimeOffset.UtcNow.AddDays(-2);
            var runs = new List<ExecutionRunRecord>(
                historicalRunCount);
            var usageObservations = new List<ProviderUsageObservation>(
                historicalRunCount);

            for (var index = 0; index < historicalRunCount; index++)
            {
                var runId = Guid.NewGuid();
                var isLatest = index == historicalRunCount - 1;
                var state = isLatest
                    ? latestRunState
                    : ExecutionState.Completed;
                var observedAtUtc = baseTimeUtc.AddMinutes(index);
                var run = CreateHistoricalRun(
                    runId,
                    agent,
                    sessionId,
                    state,
                    observedAtUtc);
                runs.Add(run);
                usageObservations.Add(
                    CreateUsageObservation(
                        runId,
                        agent.Id,
                        sessionId,
                        observedAtUtc,
                        HistoricalInputTokens,
                        HistoricalOutputTokens,
                        HistoricalCostUsd));
            }

            ChatSessionRecord? session = null;
            if (sessionId is { } existingSessionId)
            {
                session = new ChatSessionRecord(
                    existingSessionId,
                    agent.Id,
                    "High-cardinality admission session",
                    baseTimeUtc,
                    runs[^1].UpdatedAtUtc,
                    [
                        new ChatMessageRecord(
                            Guid.NewGuid(),
                            ChatMessageRole.Assistant,
                            "Historical session context.",
                            baseTimeUtc,
                            3)
                    ],
                    runs[^1].Id);
            }

            IReadOnlyList<ChatSessionRecord> sessions = session is null
                ? []
                : [session];
            var executionState = new SandboxWorkspaceExecutionState(
                Version: "3.0",
                ChatSessions: sessions,
                ExecutionLog: [],
                Metrics: [])
            {
                ExecutionRuns = runs,
                ProviderUsageObservations = usageObservations
            };
            await setupStore.SaveExecutionAsync(executionState);
            var initialUsageProjection =
                await setupStore.LoadUsageProjectionAsync();
            var layout = new FileSandboxWorkspaceStorageLayout(
                profile.WorkspaceRootPath,
                scope);
            Assert.True(File.Exists(layout.ExecutionIndexPath));
            Assert.True(File.Exists(layout.ExecutionChatIndexPath));
            Assert.True(File.Exists(layout.ExecutionUsageIndexPath));
            Assert.Equal(
                historicalRunCount,
                Directory.EnumerateDirectories(
                        layout.ExecutionRunsRoot)
                    .Count(runDirectory =>
                        File.Exists(
                            Path.Combine(
                                runDirectory,
                                "run.json"))));
            var historicalRunHeaderPaths = runs
                .Select(run => NormalizePath(layout.RunPath(run.Id)))
                .ToHashSet(PathComparer);
            var historicalUsagePaths = usageObservations
                .Select(observation => NormalizePath(
                    Path.Combine(
                        layout.RunUsageRoot(
                            observation.ExecutionRunId!.Value),
                        $"{observation.Id:N}.json")))
                .ToHashSet(PathComparer);
            Assert.Equal(
                historicalRunCount,
                historicalUsagePaths.Count(
                    path => File.Exists(path)));
            var readRecorder = new PhysicalJsonReadRecorder();
            var diagnostics =
                new FileSandboxWorkspaceJsonReadDiagnostics(
                    readRecorder.Record);
            var store = new FileSandboxWorkspaceStore(
                profile.WorkspaceRootPath,
                scope,
                chatBackedRunCommitBoundary: null,
                existingRunDetailCommitBoundary: null,
                jsonReadDiagnostics: diagnostics);

            return new AdmissionScenario(
                environment,
                profile.WorkspaceRootPath,
                scope,
                layout,
                store,
                diagnostics,
                readRecorder,
                catalogSnapshot.Revision,
                agent,
                session,
                runs[^1].Id,
                historicalRunCount,
                historicalRunHeaderPaths,
                historicalUsagePaths,
                initialUsageProjection);
        }
        catch
        {
            await environment.DisposeAsync();
            throw;
        }
    }

    private static ExecutionRunRecord CreateHistoricalRun(
        Guid runId,
        AgentDefinition agent,
        Guid? chatSessionId,
        ExecutionState state,
        DateTimeOffset observedAtUtc)
    {
        var isCompleted = state == ExecutionState.Completed;
        return new ExecutionRunRecord(
            Id: runId,
            AgentId: agent.Id,
            ChatSessionId: chatSessionId,
            Title: $"Historical run {runId:N}",
            SourceKind: "integration-test",
            SourceId: $"history-{runId:N}",
            CorrelationId: $"history-correlation-{runId:N}",
            CausationId: string.Empty,
            RequestedBy: "integration-test",
            RequestedByKind: "test",
            MetadataJson: "{}",
            InputSummary: "Historical admission input.",
            ResultSummary: isCompleted
                ? "Historical run completed."
                : "Historical run remains active.",
            ProviderName: "Historical provider",
            Model: "gpt-5.4-mini",
            State: state,
            Outcome: isCompleted
                ? RunOutcome.Succeeded
                : null,
            CreatedAtUtc: observedAtUtc,
            UpdatedAtUtc: observedAtUtc,
            StartedAtUtc: observedAtUtc,
            CompletedAtUtc: isCompleted
                ? observedAtUtc
                : null,
            RuntimeSessionKey: string.Empty,
            SerializedSessionStateJson: null,
            PendingApprovals: [])
        {
            ProviderProfileId = agent.ProviderProfileId
        };
    }

    private static ChatBackedRunStartMutation CreateAdmissionMutation(
        ChatBackedRunStartContext context,
        string prompt)
    {
        var nowUtc = DateTimeOffset.UtcNow;
        var userMessage = new ChatMessageRecord(
            Guid.NewGuid(),
            ChatMessageRole.User,
            prompt,
            nowUtc,
            4);
        var session = context.Session ??
                      new ChatSessionRecord(
                          Guid.NewGuid(),
                          context.Agent.Id,
                          "New admission session",
                          nowUtc,
                          nowUtc,
                          []);
        var runId = Guid.NewGuid();
        var updatedSession = session with
        {
            UpdatedAtUtc = nowUtc,
            LatestExecutionRunId = runId,
            Messages = [.. session.Messages, userMessage]
        };
        var run = new ExecutionRunRecord(
            Id: runId,
            AgentId: context.Agent.Id,
            ChatSessionId: updatedSession.Id,
            Title: updatedSession.Title,
            SourceKind: "integration-test",
            SourceId: $"admission-{runId:N}",
            CorrelationId: $"admission-correlation-{runId:N}",
            CausationId: string.Empty,
            RequestedBy: "integration-test",
            RequestedByKind: "test",
            MetadataJson: "{}",
            InputSummary: prompt,
            ResultSummary: "Admission prepared.",
            ProviderName: "Admission provider",
            Model: "gpt-5.4-mini",
            State: ExecutionState.Preparing,
            Outcome: null,
            CreatedAtUtc: nowUtc,
            UpdatedAtUtc: nowUtc,
            StartedAtUtc: nowUtc,
            CompletedAtUtc: null,
            RuntimeSessionKey: string.Empty,
            SerializedSessionStateJson: null,
            PendingApprovals: [])
        {
            ProviderProfileId = context.Agent.ProviderProfileId
        };
        var usageObservation = CreateUsageObservation(
            runId,
            context.Agent.Id,
            updatedSession.Id,
            nowUtc,
            AdmissionInputTokens,
            AdmissionOutputTokens,
            AdmissionCostUsd);

        return new ChatBackedRunStartMutation(
            new ExecutionRunDetail(
                run,
                updatedSession,
                ExecutionLog: [],
                Metrics: [])
            {
                UsageObservations = [usageObservation]
            },
            userMessage);
    }

    private static ProviderUsageObservation CreateUsageObservation(
        Guid runId,
        Guid agentId,
        Guid? chatSessionId,
        DateTimeOffset observedAtUtc,
        int inputTokens,
        int outputTokens,
        decimal calculatedCostUsd)
    {
        return new ProviderUsageObservation(
            Id: Guid.NewGuid(),
            CreatedAtUtc: observedAtUtc,
            ProviderName: "Admission scaling provider",
            ProviderKind: ProviderKind.OpenAi,
            Model: "gpt-5.4-mini",
            TransportKind: ProviderTransportKind.Responses,
            SourcePhase: ProviderUsageSourcePhases.AgentRuntime,
            UsageStatus: ProviderUsageObservationStatus.Observed,
            InputTokens: inputTokens,
            CachedInputTokens: 0,
            OutputTokens: outputTokens,
            ReasoningTokens: 0,
            TotalTokens: inputTokens + outputTokens,
            ToolCallCount: 0)
        {
            ExecutionRunId = runId,
            AgentId = agentId,
            ChatSessionId = chatSessionId,
            CalculatedCostUsd = calculatedCostUsd
        };
    }

    private static IReadOnlyList<FileSandboxWorkspacePhysicalJsonRead>
        GetHistoricalRunHeaderReads(AdmissionScenario scenario)
    {
        return scenario.ReadRecorder
            .Snapshot()
            .Where(read =>
                scenario.HistoricalRunHeaderPaths.Contains(
                    read.FullPath))
            .ToArray();
    }

    private static IReadOnlyList<FileSandboxWorkspacePhysicalJsonRead>
        GetHistoricalUsageReads(AdmissionScenario scenario)
    {
        return scenario.ReadRecorder
            .Snapshot()
            .Where(read =>
                scenario.HistoricalUsagePaths.Contains(
                    read.FullPath))
            .ToArray();
    }

    private static string NormalizePath(string path)
        => Path.GetFullPath(path);

    private void AssertReadBudget(
        AdmissionScenario scenario,
        int expectedPhysicalReadCount,
        string admissionKind)
    {
        var reads = scenario.ReadRecorder.Snapshot();
        var totalBytes = reads.Sum(read => read.LengthBytes);
        output.WriteLine(
            $"admission-read-budget kind={admissionKind} historical-runs={scenario.HistoricalRunCount} successful-opens={reads.Count} total-bytes={totalBytes}");
        Assert.Equal(expectedPhysicalReadCount, reads.Count);
        Assert.All(
            reads,
            read => Assert.True(
                read.LengthBytes > 0,
                $"Expected a positive physical read length for '{read.FullPath}'."));
    }

    private sealed class PhysicalJsonReadRecorder
    {
        private readonly object gate = new();
        private readonly List<FileSandboxWorkspacePhysicalJsonRead> reads = [];

        public void Record(
            FileSandboxWorkspacePhysicalJsonRead physicalRead)
        {
            lock (gate)
            {
                reads.Add(
                    physicalRead with
                    {
                        FullPath = NormalizePath(
                            physicalRead.FullPath)
                    });
            }
        }

        public IReadOnlyList<FileSandboxWorkspacePhysicalJsonRead> Snapshot()
        {
            lock (gate)
            {
                return [.. reads];
            }
        }

        public void Clear()
        {
            lock (gate)
            {
                reads.Clear();
            }
        }
    }

    private sealed class InjectedCommitFailureException(
        ChatBackedRunCommitStage stage)
        : IOException(
            $"Injected chat-run commit failure after '{stage}'.");

    private sealed record AdmissionScenario(
        CanDoItAllTestEnvironment Environment,
        string WorkspaceRoot,
        WorkspaceScopeDescriptor Scope,
        FileSandboxWorkspaceStorageLayout Layout,
        FileSandboxWorkspaceStore Store,
        FileSandboxWorkspaceJsonReadDiagnostics Diagnostics,
        PhysicalJsonReadRecorder ReadRecorder,
        CatalogDataRevision CatalogRevision,
        AgentDefinition Agent,
        ChatSessionRecord? Session,
        Guid LatestHistoricalRunId,
        int HistoricalRunCount,
        IReadOnlySet<string> HistoricalRunHeaderPaths,
        IReadOnlySet<string> HistoricalUsagePaths,
        AgentUsageProjection InitialUsageProjection) : IAsyncDisposable
    {
        public string PendingChatRunJournalPath
            => Path.Combine(
                Layout.ExecutionStorageRoot,
                "pending-chat-run-start.json");

        public ChatBackedRunStartRequest CreateRequest()
            => new(
                Agent.Id,
                Agent.ProviderProfileId!.Value,
                CatalogRevision,
                Session?.Id);

        public ValueTask DisposeAsync()
            => Environment.DisposeAsync();
    }
}
