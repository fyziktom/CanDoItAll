using System.Text.Json;
using System.Text.Json.Nodes;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.Tests.Support;
using Xunit.Abstractions;

namespace CanDoItAll.Tests.Integration.Runtime;

[Trait("Category", "FileSystemPortability")]
public sealed class FileSandboxWorkspacePreparedCommitReadIntegrationTests(ITestOutputHelper output) {
    [Theory]
    [InlineData(1)]
    [InlineData(32)]
    public async Task Prepared_progress_commit_retains_five_conflict_reads_without_duplicate_raw_comparisons(int logCount) {
        await using var scenario = await CreateScenarioAsync(logCount);
        var beforeUsage = await scenario.Store.LoadUsageProjectionAsync();
        string usagePath = Path.Combine(
            scenario.Layout.RunUsageRoot(scenario.Before.Run.Id),
            $"{scenario.Before.UsageObservations[0].Id:N}.json");
        string beforeUsageRecord = await File.ReadAllTextAsync(usagePath);

        var saved = await scenario.Store.UpdateExecutionRunDetailAsync(scenario.Before.Run.Id, AppendProgress);

        WriteReadCounts(scenario);
        foreach (var kind in Enum.GetValues<PayloadKind>()) {
            var reads = scenario.ReadsFor(kind);
            Assert.Single(reads, read => read.Kind == FileSandboxWorkspaceJsonReadKind.Deserialization);
            Assert.Empty(reads.Where(read => read.Kind == FileSandboxWorkspaceJsonReadKind.RawText));
        }

        Assert.Equal(logCount + 1, saved.ExecutionLog.Count);
        Assert.Equal(scenario.Before.ChatSession!.Messages, saved.ChatSession!.Messages);
        Assert.Equal(beforeUsageRecord, await File.ReadAllTextAsync(usagePath));
        var afterUsage = await scenario.Store.LoadUsageProjectionAsync();
        Assert.Equal(beforeUsage.UsageObservationCount, afterUsage.UsageObservationCount);
        Assert.Equal(beforeUsage.TotalTokens, afterUsage.TotalTokens);
        Assert.Equal(beforeUsage.KnownCostUsd, afterUsage.KnownCostUsd);
        Assert.True(afterUsage.Revision > beforeUsage.Revision);
        Assert.Equal(
            saved.Run.UpdatedAtUtc,
            Assert.Single(afterUsage.Agents, agent => agent.AgentId == saved.Run.AgentId).LastUsedAtUtc);
        var executionIndex = await scenario.ReadAsync<ExecutionStorageIndex>(scenario.Layout.ExecutionIndexPath);
        var chatIndex = await scenario.ReadAsync<ExecutionChatIndex>(scenario.Layout.ExecutionChatIndexPath);
        Assert.Equal(executionIndex.Revision, afterUsage.Revision);
        Assert.Equal(executionIndex.Revision, chatIndex.Revision);
        Assert.False(File.Exists(scenario.PendingJournalPath));
    }

    [Theory]
    [InlineData(PayloadKind.Run, JsonShape.Canonical)]
    [InlineData(PayloadKind.Run, JsonShape.Compact)]
    [InlineData(PayloadKind.Run, JsonShape.UnknownProperty)]
    [InlineData(PayloadKind.ChatIndex, JsonShape.Canonical)]
    [InlineData(PayloadKind.ChatIndex, JsonShape.Compact)]
    [InlineData(PayloadKind.ChatIndex, JsonShape.UnknownProperty)]
    public async Task Matching_typed_target_retains_raw_comparison_and_canonicalization(PayloadKind kind, JsonShape shape) {
        await using var scenario = await CreateScenarioAsync();
        string? expectedCanonical = null;
        scenario.OnStage = stage => {
            if (stage != ExistingRunDetailCommitStage.JournalPersisted) {
                return;
            }

            var journal = scenario.ReadJournal();
            expectedCanonical = kind switch {
                PayloadKind.Run => JsonSerializer.Serialize(
                    journal.PersistencePlan.TargetDetail.Run, scenario.Json.SerializerOptions),
                PayloadKind.ChatIndex => JsonSerializer.Serialize(
                    journal.ChatProjectionPlan.TargetIndex, scenario.Json.SerializerOptions),
                _ => throw new ArgumentOutOfRangeException(nameof(kind))
            };
            string raw = shape switch {
                JsonShape.Canonical => expectedCanonical,
                JsonShape.Compact => JsonNode.Parse(expectedCanonical)!.ToJsonString(),
                JsonShape.UnknownProperty => AddUnknownProperty(expectedCanonical),
                _ => throw new ArgumentOutOfRangeException(nameof(shape))
            };
            File.WriteAllText(scenario.PathFor(kind), raw);
        };

        await scenario.Store.UpdateExecutionRunDetailAsync(scenario.Before.Run.Id, AppendProgress);

        Assert.Single(scenario.ReadsFor(kind),
            read => read.Kind == FileSandboxWorkspaceJsonReadKind.RawText);
        Assert.Equal(expectedCanonical, await File.ReadAllTextAsync(scenario.PathFor(kind)));
        Assert.False(File.Exists(scenario.PendingJournalPath));
    }

    [Theory]
    [InlineData(PayloadKind.Session, false)]
    [InlineData(PayloadKind.Run, false)]
    [InlineData(PayloadKind.ExecutionIndex, false)]
    [InlineData(PayloadKind.UsageIndex, false)]
    [InlineData(PayloadKind.ChatIndex, false)]
    [InlineData(PayloadKind.Session, true)]
    [InlineData(PayloadKind.Run, true)]
    [InlineData(PayloadKind.ExecutionIndex, true)]
    [InlineData(PayloadKind.UsageIndex, true)]
    [InlineData(PayloadKind.ChatIndex, true)]
    public async Task Noncooperating_change_before_fresh_validation_fails_and_retains_journal(PayloadKind kind, bool remove) {
        await using var scenario = await CreateScenarioAsync();
        scenario.OnStage = stage => {
            if (stage != ExistingRunDetailCommitStage.JournalPersisted) {
                return;
            }

            string path = scenario.PathFor(kind);
            if (remove) {
                File.Delete(path);
                return;
            }

            var node = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
            if (kind is PayloadKind.Run or PayloadKind.Session) {
                node["title"] = "Unrelated external edit";
            } else {
                node["revision"] = node["revision"]!.GetValue<long>() + 1000;
            }

            File.WriteAllText(path, node.ToJsonString());
        };

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            scenario.Store.UpdateExecutionRunDetailAsync(scenario.Before.Run.Id, AppendProgress));

        Assert.True(File.Exists(scenario.PendingJournalPath));
        if (remove) {
            Assert.False(File.Exists(scenario.PathFor(kind)));
        } else {
            Assert.Contains(scenario.ReadsFor(kind), read =>
                read.Kind == FileSandboxWorkspaceJsonReadKind.Deserialization);
        }
    }

    [Fact]
    public async Task Recovered_journal_retains_five_raw_comparisons_and_rolls_forward_once() {
        await using var scenario = await CreateScenarioAsync();
        scenario.OnStage = stage => {
            if (stage == ExistingRunDetailCommitStage.JournalPersisted) {
                throw new InjectedCommitFailureException();
            }
        };

        await Assert.ThrowsAsync<InjectedCommitFailureException>(() =>
            scenario.Store.UpdateExecutionRunDetailAsync(scenario.Before.Run.Id, AppendProgress));
        Assert.True(File.Exists(scenario.PendingJournalPath));
        scenario.OnStage = null;
        scenario.Reads.Clear();
        scenario.CaptureReads = true;
        var recoveryStore = scenario.CreateStore();

        var recovered = await recoveryStore.GetExecutionRunDetailAsync(scenario.Before.Run.Id);

        Assert.NotNull(recovered);
        WriteReadCounts(scenario);
        foreach (var kind in Enum.GetValues<PayloadKind>()) {
            Assert.Single(scenario.ReadsFor(kind),
                read => read.Kind == FileSandboxWorkspaceJsonReadKind.RawText);
        }

        Assert.Equal(scenario.Before.ExecutionLog.Count + 1, recovered.ExecutionLog.Count);
        Assert.False(File.Exists(scenario.PendingJournalPath));
        var reloaded = await recoveryStore.GetExecutionRunDetailAsync(scenario.Before.Run.Id);
        Assert.NotNull(reloaded);
        Assert.Equal(recovered.ExecutionLog, reloaded.ExecutionLog);
    }

    private void WriteReadCounts(CommitScenario scenario) {
        foreach (var kind in Enum.GetValues<PayloadKind>()) {
            var reads = scenario.ReadsFor(kind);
            output.WriteLine(
                $"{kind}: validation={reads.Count(read => read.Kind == FileSandboxWorkspaceJsonReadKind.Deserialization)}, comparison={reads.Count(read => read.Kind == FileSandboxWorkspaceJsonReadKind.RawText)}");
        }
    }

    private static string AddUnknownProperty(string canonical) {
        var node = JsonNode.Parse(canonical)!.AsObject();
        node["unknownFutureField"] = "must not survive canonicalization";
        return node.ToJsonString();
    }

    private static ExecutionRunDetail AppendProgress(ExecutionRunDetail detail) {
        var timestamp = detail.Run.UpdatedAtUtc.AddTicks(1);
        return detail with {
            Run = detail.Run with {
                Revision = detail.Run.Revision + 1,
                UpdatedAtUtc = timestamp
            },
            ChatSession = detail.ChatSession! with { UpdatedAtUtc = timestamp },
            ExecutionLog = [
                new ExecutionLogEntry(
                    Guid.NewGuid(),
                    detail.Run.AgentId,
                    detail.ChatSession!.Id,
                    timestamp,
                    ExecutionState.Preparing,
                    "Framework",
                    "Framework preparation completed.") { ExecutionRunId = detail.Run.Id },
                .. detail.ExecutionLog
            ]
        };
    }

    private static async Task<CommitScenario> CreateScenarioAsync(int logCount = 1) {
        var environment = CanDoItAllTestEnvironment.Create($"prepared-commit-reads-{Guid.NewGuid():N}");
        try {
            var profile = environment.CreateInMemoryProfile("primary");
            var scope = WorkspaceScopeDescriptor.Sandbox;
            var setupStore = new FileSandboxWorkspaceStore(profile.WorkspaceRootPath, scope);
            var catalog = await setupStore.LoadCatalogSnapshotAsync();
            var agent = catalog.Catalog.Agents.First(candidate => candidate.ProviderProfileId.HasValue);
            var runId = Guid.NewGuid();
            var timestamp = DateTimeOffset.UtcNow;
            var updatedAt = timestamp.AddTicks(logCount);
            var session = new ChatSessionRecord(
                Guid.NewGuid(),
                agent.Id,
                "Prepared progress commit",
                timestamp,
                updatedAt,
                [new ChatMessageRecord(Guid.NewGuid(), ChatMessageRole.User, "Prepare the workspace.", timestamp, 5)],
                LatestExecutionRunId: runId);
            var run = new ExecutionRunRecord(
                Id: runId,
                AgentId: agent.Id,
                ChatSessionId: session.Id,
                Title: session.Title,
                SourceKind: "integration-test",
                SourceId: session.Id.ToString("N"),
                CorrelationId: Guid.NewGuid().ToString("N"),
                CausationId: string.Empty,
                RequestedBy: "integration-test",
                RequestedByKind: "test",
                MetadataJson: "{}",
                InputSummary: "Prepare the workspace.",
                ResultSummary: string.Empty,
                ProviderName: "Prepared commit provider",
                Model: "gpt-5.4-mini",
                State: ExecutionState.Preparing,
                Outcome: null,
                CreatedAtUtc: timestamp,
                UpdatedAtUtc: updatedAt,
                StartedAtUtc: timestamp,
                CompletedAtUtc: null,
                RuntimeSessionKey: string.Empty,
                SerializedSessionStateJson: null,
                PendingApprovals: []) { ProviderProfileId = agent.ProviderProfileId };
            var logs = Enumerable.Range(0, logCount).Select(index =>
                new ExecutionLogEntry(
                    Guid.NewGuid(),
                    agent.Id,
                    session.Id,
                    timestamp.AddTicks(index),
                    ExecutionState.Preparing,
                    "Planning",
                    $"Prior stage {index}.") { ExecutionRunId = runId }).ToArray();
            var usage = new ProviderUsageObservation(
                Id: Guid.NewGuid(),
                CreatedAtUtc: timestamp,
                ProviderName: run.ProviderName,
                ProviderKind: ProviderKind.OpenAi,
                Model: run.Model,
                TransportKind: ProviderTransportKind.Responses,
                SourcePhase: ProviderUsageSourcePhases.AgentRuntime,
                UsageStatus: ProviderUsageObservationStatus.Observed,
                InputTokens: 10,
                CachedInputTokens: 0,
                OutputTokens: 5,
                ReasoningTokens: 0,
                TotalTokens: 15,
                ToolCallCount: 0) {
                ExecutionRunId = runId,
                AgentId = agent.Id,
                ChatSessionId = session.Id,
                CalculatedCostUsd = 0.01m
            };
            var before = await setupStore.SaveExecutionRunDetailAsync(
                new ExecutionRunDetail(run, session, logs, []) { UsageObservations = [usage] });
            return new CommitScenario(environment, profile.WorkspaceRootPath, scope, before);
        } catch {
            await environment.DisposeAsync();
            throw;
        }
    }

    public enum PayloadKind {
        Session,
        Run,
        ExecutionIndex,
        UsageIndex,
        ChatIndex
    }

    public enum JsonShape {
        Canonical,
        Compact,
        UnknownProperty
    }

    private sealed class InjectedCommitFailureException : IOException;

    private sealed class CommitScenario : IAsyncDisposable {
        private readonly CanDoItAllTestEnvironment environment;
        private readonly string workspaceRoot;
        private readonly WorkspaceScopeDescriptor scope;
        private readonly FileSandboxWorkspaceJsonReadDiagnostics diagnostics;

        public CommitScenario(
            CanDoItAllTestEnvironment environment,
            string workspaceRoot,
            WorkspaceScopeDescriptor scope,
            ExecutionRunDetail before) {
            this.environment = environment;
            this.workspaceRoot = workspaceRoot;
            this.scope = scope;
            Before = before;
            Layout = new FileSandboxWorkspaceStorageLayout(workspaceRoot, scope);
            diagnostics = new FileSandboxWorkspaceJsonReadDiagnostics(read => {
                if (CaptureReads) {
                    Reads.Add(read);
                }
            });
            Store = CreateStore();
        }

        public ExecutionRunDetail Before { get; }
        public FileSandboxWorkspaceStore Store { get; }
        public FileSandboxWorkspaceStorageLayout Layout { get; }
        public FileSandboxWorkspaceJsonStore Json { get; } = new();
        public List<FileSandboxWorkspacePhysicalJsonRead> Reads { get; } = [];
        public bool CaptureReads { get; set; }
        public Action<ExistingRunDetailCommitStage>? OnStage { get; set; }
        public string PendingJournalPath => Path.Combine(Layout.ExecutionStorageRoot, "pending-run-detail-update.json");

        public FileSandboxWorkspaceStore CreateStore() => new(
            workspaceRoot,
            scope,
            chatBackedRunCommitBoundary: null,
            existingRunDetailCommitBoundary: stage => {
                if (stage == ExistingRunDetailCommitStage.JournalPersisted) {
                    Reads.Clear();
                    CaptureReads = true;
                }

                OnStage?.Invoke(stage);
                if (stage == ExistingRunDetailCommitStage.ChatIndexPersisted) {
                    CaptureReads = false;
                }
            },
            jsonReadDiagnostics: diagnostics);

        public string PathFor(PayloadKind kind) => kind switch {
            PayloadKind.Session => Layout.SessionPath(Before.ChatSession!.Id),
            PayloadKind.Run => Layout.RunPath(Before.Run.Id),
            PayloadKind.ExecutionIndex => Layout.ExecutionIndexPath,
            PayloadKind.UsageIndex => Layout.ExecutionUsageIndexPath,
            PayloadKind.ChatIndex => Layout.ExecutionChatIndexPath,
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };

        public IReadOnlyList<FileSandboxWorkspacePhysicalJsonRead> ReadsFor(PayloadKind kind) =>
            Reads.Where(read => string.Equals(read.FullPath, PathFor(kind), StringComparison.Ordinal)).ToArray();

        public ExistingRunDetailCommitJournal ReadJournal() =>
            JsonSerializer.Deserialize<ExistingRunDetailCommitJournal>(
                File.ReadAllText(PendingJournalPath),
                Json.SerializerOptions) ?? throw new InvalidDataException("The prepared journal was empty.");

        public async Task<T> ReadAsync<T>(string path) where T : class =>
            await Json.ReadJsonAsync<T>(path, CancellationToken.None) ??
            throw new InvalidDataException($"Expected {typeof(T).Name} at the scenario path.");

        public ValueTask DisposeAsync() => environment.DisposeAsync();
    }
}