using System.Collections.Immutable;
using System.Data;
using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.AgentFramework.Providers;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Tests.Integration.Runtime;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace CanDoItAll.Tests.Integration;

internal enum AgentPlanningPublicationGuard { CoarseCatalog = 1, SelectedAgent = 2 }

internal sealed class AgentPlanningPublicationCostFixture(string outputRoot, AgentPlanningPublicationGuard guard) {
    internal const string OutputRootEnvironment = "CANDOITALL_AGENT_PLANNING_COST_OUTPUT_ROOT";
    private readonly CancellationTokenSource deadline = new(TimeSpan.FromMinutes(10));
    private readonly List<FileWorkspace> workspaces = [];
    private CanDoItAllTestEnvironment? environment;
    private TestDatabaseProfile? profile;
    private ProjectTaskPlanOperationFixture? sql;
    private bool environmentAttempted;
    private bool databaseAttempted;
    private int sequence;
    private int holderReleased;
    private long origin;
    private OperationSample[] samples = [];
    private object? observation;
    private bool responsive;

    internal static async Task RunAsync(AgentPlanningPublicationGuard guard = AgentPlanningPublicationGuard.CoarseCatalog) {
        if (!Enum.IsDefined(guard)) {
            throw new ArgumentOutOfRangeException(nameof(guard));
        }
        var configured = Environment.GetEnvironmentVariable(OutputRootEnvironment);
        Assert.True(!string.IsNullOrWhiteSpace(configured) && Path.IsPathFullyQualified(configured) && Directory.Exists(configured),
            "Supply an existing parent-owned planning-cost output root before this opt-in row.");
        var parent = Path.TrimEndingDirectorySeparator(Path.GetFullPath(configured!));
        RequireNoLinks(parent);
        var prefix = guard == AgentPlanningPublicationGuard.CoarseCatalog ? "coarse" : "selected";
        var rowRoot = Path.Combine(parent, $"{prefix}-{Guid.NewGuid():N}");
        Assert.False(Directory.Exists(rowRoot));
        await WriteNewAsync(rowRoot + ".intent.json", new {
            Owner = nameof(AgentPlanningPublicationCostTests), Guard = guard.ToString(), RowRoot = rowRoot, Environment.ProcessId,
            RecordedAtUtc = DateTimeOffset.UtcNow, CooperativeRowSeconds = 600,
            Profile = "small-file/one-task/one-forced-SQL-wait", MaximumPostgresLeases = 1, MaximumFileWorkspaces = 2
        });
        Directory.CreateDirectory(rowRoot);
        var fixture = new AgentPlanningPublicationCostFixture(rowRoot, guard);
        Exception? primary = null;
        try {
            await fixture.InitializeAsync();
            await fixture.MeasureAsync();
        } catch (Exception failure) {
            primary = failure;
        }
        Exception? cleanup = null;
        try {
            await fixture.CleanupAsync();
        } catch (Exception failure) {
            cleanup = failure;
        }
        Exception? evidence = null;
        try {
            await fixture.RecordAsync("result", new {
                SourceOnlyReference = $"actual {guard} exclusion, synthetic task-plan authority; no live-provider provenance",
                fixture.observation, fixture.samples, ReferenceResponsivenessPassed = fixture.responsive,
                WorkloadFailureType = primary?.GetType().FullName, CleanupFailureType = cleanup?.GetType().FullName,
                CleanupConfirmed = cleanup is null, CapacityQualified = false,
                Limit = "One forced small-file cohort; no normal p95, maximum-size, sustained-load or fairness qualification"
            });
        } catch (Exception failure) {
            evidence = failure;
        } finally {
            fixture.deadline.Dispose();
        }
        var failures = new[] { primary, cleanup, evidence }.OfType<Exception>().ToArray();
        if (failures.Length > 1) {
            throw new AggregateException("Publication probe or its evidence is incomplete.", failures);
        }
        if (failures.Length == 1) {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failures[0]).Throw();
        }
        Assert.True(fixture.responsive,
            "The publication guard failed the declared one-second unrelated-writer reference; complete timing and cleanup evidence was retained.");
    }

    private async Task InitializeAsync() {
        var prefix = Guid.NewGuid().ToString("N")[..20];
        await RecordAsync("allocation-intent", new { DatabasePrefix = $"cditall_{prefix}_", MaximumDatabases = 1,
            EnvironmentParent = outputRoot, FileWorkspacePrefix = "tool-admission" });
        environmentAttempted = true;
        environment = CanDoItAllTestEnvironment.CreateUnder(outputRoot, "coarse-sql");
        RequireContained(environment.RootPath);
        await RecordAsync("environment", new { environment.RootPath });
        databaseAttempted = true;
        profile = environment.CreatePostgreSqlProfile(prefix);
        var database = new NpgsqlConnectionStringBuilder(profile.ConnectionString).Database;
        Assert.Matches($"^cditall_{prefix}_[0-9a-f]{{32}}$", database);
        await RecordAsync("database", new { DatabaseName = database, profile.ProfileRootPath });
        sql = await ProjectTaskPlanOperationFixture.CreateAsync(environment, profile);
        var canonical = sql.Services.GetRequiredService<ICanonicalRuntimeDatabase>();
        var binding = new AgentToolProfileBinding(canonical.Profile.Profile.Id,
            canonical.Profile.Profile.Runtime.Fingerprint, new(canonical.Generation));
        for (var index = 0; index < 2; index++) {
            var workspace = await FileWorkspace.CreateAsync(outputRoot, binding, sql, index, guard, deadline.Token);
            workspaces.Add(workspace);
            RequireContained(workspace.Files.WorkspaceRoot);
            await RecordAsync("workspace", await workspace.DescribeAsync(deadline.Token));
        }
    }

    private async Task MeasureAsync() {
        var token = deadline.Token;
        var fixture = sql!;
        var command = await fixture.CommandAsync(1, token);
        var authority = new PublicationAuthority(workspaces[0], command.Source, fixture.Project,
            fixture.Services.GetRequiredService<ICanonicalRuntimeDatabase>(), guard);
        await using var holder = await fixture.ContextAsync(token);
        var effectiveTimeout = holder.Database.GetCommandTimeout() ?? new NpgsqlConnectionStringBuilder(profile!.ConnectionString).CommandTimeout;
        Assert.InRange(effectiveTimeout, 1, 30);
        await using var heldTransaction = await holder.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, token);
        await holder.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({ProjectMutationScopeKeys.ForProject(fixture.Project.ProjectId)}, 0))", token);
        var holderPid = ((NpgsqlConnection)holder.Database.GetDbConnection()).ProcessID;
        fixture.Probe.Reset();
        origin = Stopwatch.GetTimestamp();
        using var trace = new AgentPlanningPublicationTrace();
        using var holderBudget = CancellationTokenSource.CreateLinkedTokenSource(token);
        holderBudget.CancelAfter(TimeSpan.FromSeconds(30));
        var timer = Task.Delay(Timeout.InfiniteTimeSpan, holderBudget.Token);
        var allocated = GC.GetTotalAllocatedBytes(precise: true);
        ProjectTaskPlanOutcome? outcome = null;
        var publishing = MeasureOperationAsync("publisher", async () => {
            outcome = await fixture.Publisher().PublishAsync(command, new(fixture.Caller, authority, authority), token);
        });
        Task<OperationSample>[] writers = [];
        Exception? primary = null;
        var blockedAt = 0d;
        var holderReleasedAt = 0d;
        try {
            await fixture.WaitForProjectWaiterAsync(holderPid, holderBudget.Token);
            blockedAt = Elapsed();
            Assert.NotEqual(0, fixture.Probe.ProjectWaiterPid);
            writers = workspaces.SelectMany((workspace, index) => new[] {
                MeasureOperationAsync($"{(index == 0 ? "same" : "independent")}-checkpoint", () => workspace.CheckpointAsync(token)),
                MeasureOperationAsync($"{(index == 0 ? "same" : "independent")}-cancel", () => workspace.CancelAsync(token)),
                MeasureOperationAsync($"{(index == 0 ? "same" : "independent")}-grant", () => workspace.GrantAsync(token))
            }).ToArray();
            await Task.WhenAny(publishing, timer);
        } catch (Exception failure) {
            primary = failure;
        } finally {
            await holderBudget.CancelAsync();
            try {
                await timer;
            } catch (OperationCanceledException) when (holderBudget.IsCancellationRequested) {
            }
            try {
                await heldTransaction.RollbackAsync(CancellationToken.None);
            } catch (Exception cleanup) {
                primary = primary is null ? cleanup : new AggregateException(primary, cleanup);
            }
            Volatile.Write(ref holderReleased, 1);
            holderReleasedAt = Elapsed();
            samples = await Task.WhenAll(writers.Prepend(publishing));
            observation = new {
                TasksRequested = 1, HolderPid = holderPid, PublisherPid = fixture.Probe.ProjectWaiterPid,
                ActualBlockingObservedAtMilliseconds = blockedAt, HolderReleasedAtMilliseconds = holderReleasedAt,
                EffectiveSqlCommandTimeoutSeconds = effectiveTimeout, CooperativeHolderBudgetSeconds = 30,
                Authority = authority.Evidence, OutcomeType = outcome?.GetType().Name,
                Rejection = (outcome as ProjectTaskPlanOutcome.Rejected)?.Reason.ToString(),
                ProcessAllocatedBytes = GC.GetTotalAllocatedBytes(precise: true) - allocated,
                AllocationScope = "whole cohort process-wide, not per-operation allocations or peak memory",
                ObservedWorkbenchEfCommands = fixture.Probe.Commands.Count,
                SqlCoverage = "Workbench interceptor only; raw ADO lifecycle/other enlisted commands excluded; not network roundtrips",
                LogicalRunReadBytes = authority.ReadBytes, PhysicalDeviceIoMeasured = false,
                FileReadCoverage = "Single saved run-file size only; repeated owner reads, pending scans and catalog bytes excluded"
            };
            try {
                await RecordAsync("cohort", new { observation, samples, Trace = trace.Snapshot() });
            } catch (Exception evidence) {
                primary = primary is null ? evidence : new AggregateException(primary, evidence);
            }
        }
        if (primary is not null) {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(primary).Throw();
        }
        Assert.All(samples, sample => Assert.Null(sample.FailureType));
        Assert.IsType<ProjectTaskPlanOutcome.Rejected>(outcome);
        await fixture.VerifyAsync(0, 0);
        foreach (var workspace in workspaces) {
            await workspace.VerifyAsync(token);
            var store = workspace.NewStore();
            await using var dispatch = await store.AcquireToolDispatchLeaseAsync(workspace.Files.Detail.Run.Id, token);
            await using var released = await store.AcquireAgentReadLeaseAsync(workspace.Files.Agent.Id, token);
            Assert.NotNull(released.Agent);
        }
        await using (var check = await fixture.ContextAsync(token)) {
            await using var transaction = await check.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, token);
            Assert.True(await check.Database.SqlQuery<bool>(
                $"SELECT pg_try_advisory_xact_lock(hashtextextended({ProjectMutationScopeKeys.ForProject(fixture.Project.ProjectId)}, 0)) AS \"Value\"").SingleAsync(token));
        }
        responsive = samples.Where(sample => sample.Name != "publisher")
            .All(sample => sample.ElapsedMilliseconds < 1000 && sample.CompletedWhileSqlHolderPresent);
        await RecordAsync("effects-verified", new { TaskRows = 0, ReceiptRows = 0, Writers = writers.Length,
            FileAndProjectGuardsReacquired = true, ReferenceResponsivenessPassed = responsive });
    }

    private async Task<OperationSample> MeasureOperationAsync(string name, Func<Task> operation) {
        using var activity = new Activity("planning-cost-operation").SetBaggage(AgentPlanningPublicationTrace.OperationKey, name).Start();
        var start = Elapsed();
        string? failure = null;
        try {
            await operation();
        } catch (Exception exception) {
            failure = exception.GetType().FullName;
        }
        var end = Elapsed();
        return new(name, start, end, end - start, Volatile.Read(ref holderReleased) == 0, failure);
    }

    private double Elapsed() => Stopwatch.GetElapsedTime(origin).TotalMilliseconds;

    private async Task CleanupAsync() {
        List<Exception> failures = [];
        foreach (var workspace in workspaces.AsEnumerable().Reverse()) {
            try {
                RequireContained(workspace.Files.WorkspaceRoot);
                await workspace.Files.DisposeAsync();
                Assert.False(Directory.Exists(workspace.Files.WorkspaceRoot));
            } catch (Exception failure) {
                failures.Add(failure);
            }
        }
        var fileParent = Path.Combine(outputRoot, "tool-admission");
        try {
            if (Directory.Exists(fileParent)) {
                RequireNoLinks(fileParent);
                Assert.Empty(Directory.EnumerateFileSystemEntries(fileParent));
            }
        } catch (Exception failure) {
            failures.Add(failure);
        }
        if (sql is not null) {
            try {
                await sql.DisposeAsync();
            } catch (Exception failure) {
                failures.Add(failure);
            }
        }
        if (profile is not null) {
            try {
                await VerifyDatabaseAsync(removed: false);
            } catch (Exception failure) {
                failures.Add(failure);
            }
        }
        if (environment is not null) {
            try {
                RequireContained(environment.RootPath);
                await environment.DisposeAsync();
                Assert.False(Directory.Exists(environment.RootPath));
                if (profile is not null) {
                    await VerifyDatabaseAsync(removed: true);
                }
            } catch (Exception failure) {
                failures.Add(failure);
            }
        }
        var unknown = environmentAttempted && environment is null || databaseAttempted && profile is null;
        if (unknown) {
            failures.Add(new InvalidOperationException("Partial allocation requires exact recorded namespace reconciliation."));
        }
        try {
            await RecordAsync("cleanup", new { Confirmed = failures.Count == 0, AllocationUnknown = unknown,
                Failures = failures.Select(failure => failure.GetType().FullName).ToArray() });
        } catch (Exception evidence) {
            failures.Add(evidence);
        }
        if (failures.Count != 0) {
            throw new AggregateException("Publication probe cleanup or its evidence is incomplete.", failures);
        }
    }

    private async Task VerifyDatabaseAsync(bool removed) {
        using (var pool = new NpgsqlConnection(profile!.ConnectionString)) {
            NpgsqlConnection.ClearPool(pool);
        }
        var options = new NpgsqlConnectionStringBuilder(profile.ConnectionString);
        var database = options.Database;
        options.Database = "postgres";
        options.Pooling = false;
        using var cleanupDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var connection = new NpgsqlConnection(options.ConnectionString);
        await connection.OpenAsync(cleanupDeadline.Token);
        await using var query = connection.CreateCommand();
        query.CommandText = removed ? "SELECT count(*) FROM pg_database WHERE datname = @database" :
            "SELECT count(*) FROM pg_stat_activity WHERE datname = @database AND backend_type = 'client backend'";
        query.Parameters.AddWithValue("database", database!);
        while (Convert.ToInt64(await query.ExecuteScalarAsync(cleanupDeadline.Token)) != 0) {
            await Task.Delay(TimeSpan.FromMilliseconds(25), cleanupDeadline.Token);
        }
    }

    private Task RecordAsync(string phase, object evidence)
        => WriteNewAsync(Path.Combine(outputRoot, $"{++sequence:D3}-{phase}.json"), new { Phase = phase, ObservedAtUtc = DateTimeOffset.UtcNow, Evidence = evidence });

    private static async Task WriteNewAsync(string path, object value) {
        await using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        await JsonSerializer.SerializeAsync(output, value);
        await output.FlushAsync();
        output.Flush(flushToDisk: true);
    }

    private void RequireContained(string path) {
        var relative = Path.GetRelativePath(outputRoot, Path.GetFullPath(path));
        Assert.False(Path.IsPathRooted(relative) || relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal));
        RequireNoLinks(path);
    }

    private static void RequireNoLinks(string path) {
        for (DirectoryInfo? current = new(Path.GetFullPath(path)); current is not null; current = current.Parent) {
            Assert.False(current.Attributes.HasFlag(FileAttributes.ReparsePoint));
        }
    }

    private sealed record OperationSample(string Name, double StartedAtMilliseconds, double EndedAtMilliseconds,
        double ElapsedMilliseconds, bool CompletedWhileSqlHolderPresent, string? FailureType);

    private sealed class FileWorkspace(AgentToolAdmissionJournalFixture files, ProjectTaskPlanOperationFixture sql,
        ExecutionRunRecord checkpoint, ExecutionRunRecord cancellation, AgentDefinition grantAgent) {
        internal AgentToolAdmissionJournalFixture Files => files;
        internal FileSandboxWorkspaceStorageLayout Layout { get; } = new(files.WorkspaceRoot, files.StorageScope);
        private AgentProjectStructureLifetime Lifetime => new(sql.Project.DatabaseProfileId, sql.Project.ProjectId, sql.Project.LifetimeId);
        internal FileSandboxWorkspaceStore NewStore() => new(files.WorkspaceRoot, files.StorageScope,
            new AgentProjectAccessCatalogPolicy(sql.Services.GetRequiredService<ProjectWriteAdmissionService>(), files.Profile.ProfileId));

        internal static async Task<FileWorkspace> CreateAsync(string parent, AgentToolProfileBinding profile,
            ProjectTaskPlanOperationFixture sql, int index, AgentPlanningPublicationGuard guard, CancellationToken token) {
            var files = await AgentToolAdmissionJournalFixture.CreateAsync(profileBinding: profile,
                configureAgent: agent => agent with { IsTemplate = false, Status = AgentLifecycleStatus.Active },
                storageScope: WorkspaceScopeDescriptor.Organization(guard == AgentPlanningPublicationGuard.CoarseCatalog
                    ? $"coarse-probe-{index}" : profile.ProfileId.ToString("N")), ownedParentRoot: parent);
            try {
                var agents = Enumerable.Range(0, 3).Select(number => files.Agent with {
                    Id = Guid.NewGuid(), Name = $"Coarse probe unrelated {number}", ConfigurationJson = "{}"
                }).ToArray();
                await files.Store.UpdateCatalogAsync(catalog => catalog with { Agents = [.. catalog.Agents, .. agents] }, token);
                var checkpoint = await SeedRunAsync(agents[0], effectsOnly: false);
                var cancellation = await SeedRunAsync(agents[1], effectsOnly: true);
                return new(files, sql, checkpoint, cancellation, agents[2]);

                async Task<ExecutionRunRecord> SeedRunAsync(AgentDefinition agent, bool effectsOnly) {
                    var id = Guid.NewGuid();
                    var chatId = Guid.NewGuid();
                    var now = DateTimeOffset.UtcNow;
                    var governance = new AgentExecutionAuthorityRecord(AgentExecutionAuthorityId.Create(), agent.Id,
                        profile.ProfileId, profile.Generation, WorkspaceScopeDescriptor.Sandbox, true, true,
                        "coarse-probe-policy", "synthetic-coarse-probe-authority", now,
                        schemaVersion: AgentExecutionAuthorityRecord.LegacySchemaVersion);
                    var run = files.Detail.Run with { Id = id, AgentId = agent.Id, ChatSessionId = chatId,
                        SourceId = chatId.ToString("N"), ToolAdmission = null,
                        MetadataJson = AgentTurnContextMetadata.Apply("{}",
                            AgentTurnContextMetadata.TryReadTurnContextReference(files.Detail.Run.MetadataJson)!, governance) };
                    var chat = new ChatSessionRecord(chatId, agent.Id, run.Title, run.CreatedAtUtc, run.UpdatedAtUtc, [], LatestExecutionRunId: id);
                    var journal = new AgentToolAdmissionJournal(files.Store, profile);
                    var admitted = journal.CreateForNewRun(run, chat);
                    if (effectsOnly) {
                        admitted = admitted with { SchemaVersion = AgentToolJournalRecord.EffectJournalSchemaVersion,
                            Support = AgentToolAdmissionSupport.EffectJournalOnly };
                    }
                    run = run with { ToolAdmission = admitted };
                    await files.Store.SaveExecutionRunDetailAsync(new(run, chat, [], []), token);
                    if (effectsOnly) {
                        await using var lease = await journal.AcquireRunAsync(admitted.Session.Reference, token);
                        using var bound = lease.Bind();
                        var started = await journal.BeginEffectSegmentAsync(lease, token);
                        var payload = AgentSavedEffectDispatchFixture.Payload(0);
                        await journal.AdmitBatchAsync(lease, payload.Digest, AgentSavedEffectDispatchFixture.Envelope(),
                            [new("coarse-call", payload, true)], token);
                        await journal.SaveEffectApprovalsAsync(lease, started.Segments[0].Id,
                            [new("coarse-approval", "coarse-call", payload.ToolName, "function", "Synthetic pending effect", payload.ArgumentsJson)], token);
                    }
                    return (await files.Store.GetExecutionRunAsync(id, token))!;
                }
            } catch (Exception primary) {
                try {
                    await files.DisposeAsync();
                } catch (Exception cleanup) {
                    throw new AggregateException(primary, cleanup);
                }
                throw;
            }
        }

        internal async Task<object> DescribeAsync(CancellationToken token) {
            var run = Layout.RunPath(files.Detail.Run.Id);
            var catalog = Layout.CatalogPath;
            var runBytes = await File.ReadAllBytesAsync(run, token);
            var catalogBytes = await File.ReadAllBytesAsync(catalog, token);
            Assert.InRange(runBytes.Length, 1, 1024 * 1024);
            Assert.InRange(catalogBytes.Length, 1, 1024 * 1024);
            return new { files.WorkspaceRoot, Scope = files.StorageScope.Key, RunBytes = runBytes.Length, CatalogBytes = catalogBytes.Length,
                RunSha256 = Convert.ToHexString(SHA256.HashData(runBytes)), CatalogSha256 = Convert.ToHexString(SHA256.HashData(catalogBytes)) };
        }

        internal async Task CheckpointAsync(CancellationToken token) {
            await ((ISandboxWorkspaceExecutionRunMutationStore)NewStore()).UpdateExecutionRunDetailAsync(checkpoint.Id, current => {
                Assert.Equal(checkpoint.Revision, current.Run.Revision);
                return current with { Run = current.Run with { Revision = checked(current.Run.Revision + 1),
                    UpdatedAtUtc = DateTimeOffset.UtcNow, ResultSummary = "coarse-probe-checkpoint" } };
            }, token);
        }

        internal async Task CancelAsync(CancellationToken token) {
            var owner = new AgentToolAdmissionJournal(NewStore(), files.Profile);
            await owner.CancelSavedEffectsAsync(new(cancellation.Id, cancellation.ChatSessionId, cancellation.Revision,
                cancellation.ToolAdmission!.Revision), (observation, ct) => {
                    ct.ThrowIfCancellationRequested();
                    return ValueTask.FromResult(observation.Session.Reference == cancellation.ToolAdmission.Session.Reference);
                }, token);
        }

        internal Task GrantAsync(CancellationToken token) {
            var catalog = new AgentFrameworkWorkspaceCatalogService(NewStore(), Unused<IAgentPackageService>(),
                Unused<ICapabilityProofService>(), Unused<IProviderProfileService>(), Unused<IProviderDiagnosticsService>(),
                Unused<IProviderProfileRegistry>(), Unused<IProviderRuntimeProfileSource>());
            return catalog.GrantAgentProjectStructureLifetimeAsync(grantAgent.Id, Lifetime, token);
        }

        internal async Task VerifyAsync(CancellationToken token) {
            var store = NewStore();
            var saved = (await store.GetExecutionRunAsync(checkpoint.Id, token))!;
            Assert.Equal(checkpoint.Revision + 1, saved.Revision);
            Assert.Equal("coarse-probe-checkpoint", saved.ResultSummary);
            var cancelled = (await store.GetExecutionRunDetailAsync(cancellation.Id, token))!;
            Assert.Equal(RunOutcome.Cancelled, cancelled.Run.Outcome);
            Assert.Empty(cancelled.Run.PendingApprovals);
            Assert.Equal(cancellation.Revision + 1, cancelled.Run.Revision);
            Assert.True(cancelled.Run.ToolAdmission!.Revision > cancellation.ToolAdmission!.Revision);
            var proposal = Assert.Single(cancelled.Run.ToolAdmission.Batches.SelectMany(batch => batch.Proposals));
            Assert.Equal(AgentToolProposalState.Cancelled, proposal.State);
            Assert.Equal(ExecutionApprovalStatus.Rejected, proposal.ApprovalStatus);
            Assert.Equal(AgentToolEffectState.NotCommitted, proposal.EffectState);
            Assert.Equal(new AgentToolCancellationResolution(AgentToolCancellationDisposition.NotDispatched,
                AgentToolCancellationReason.NeverDispatched), proposal.Cancellation);
            Assert.Null(proposal.DispatchClaimId);
            Assert.Null(proposal.Result);
            Assert.Null(proposal.OwnerReceipt);
            AgentSavedEffectDispatchFixture.AssertOriginPreserved(cancellation.ToolAdmission!.Segments[0],
                cancelled.Run.ToolAdmission.Segments[0]);
            Assert.Empty(cancelled.Metrics);
            var agent = (await store.LoadCatalogAsync(token)).Agents.Single(item => item.Id == grantAgent.Id);
            Assert.Contains(Lifetime, AgentProjectStructureAccessMetadata.ReadBindingScopeForMutation(agent.ConfigurationJson).Lifetimes);
            Assert.True(agent.UpdatedAtUtc > grantAgent.UpdatedAtUtc);
            Assert.NotEqual(grantAgent.ConfigurationJson, agent.ConfigurationJson);
        }
    }

    private sealed class PublicationAuthority(FileWorkspace workspace, ProjectTaskPlanSource source, ProjectWriteAdmission project,
        ICanonicalRuntimeDatabase canonical, AgentPlanningPublicationGuard guard) : IProjectTaskPlanPublicationAuthority, IProjectTaskPlanReceiptAccess {
        private readonly long generation = canonical.Generation;
        private readonly ICanonicalRuntimeDatabase runtime = canonical;
        private readonly FileSandboxWorkspaceStore store = workspace.NewStore();
        private readonly FileSandboxWorkspaceJsonStore json = new();
        private long entered;
        private long acquired;
        private long released;
        internal long ReadBytes { get; private set; }
        internal object Evidence => new { Guard = guard.ToString(), Entered = entered, Acquired = acquired, Released = released,
            AcquireMilliseconds = acquired == 0 ? (double?)null : Stopwatch.GetElapsedTime(entered, acquired).TotalMilliseconds,
            HeldMilliseconds = released == 0 || acquired == 0 ? (double?)null : Stopwatch.GetElapsedTime(acquired, released).TotalMilliseconds };

        public async Task<IProjectTaskPlanHeldAuthority> AcquireAsync(ProjectTaskPlanSource original, ProjectWriteAdmission target,
            CancellationToken cancellationToken) {
            Assert.Equal(source, original);
            Assert.Equal(project, target);
            entered = Stopwatch.GetTimestamp();
            var dispatch = await store.AcquireToolDispatchLeaseAsync(workspace.Files.Detail.Run.Id, cancellationToken);
            IAgentCatalogReadLease? catalog = null;
            try {
                catalog = guard == AgentPlanningPublicationGuard.CoarseCatalog
                    ? await store.AcquireAgentReadLeaseAsync(workspace.Files.Agent.Id, cancellationToken)
                    : await store.AcquirePlanningAuthoritySnapshotAsync(new(workspace.Files.StorageScope,
                        workspace.Files.Detail.Run.ToolAdmission!.Session, workspace.Files.Detail.Run.Revision,
                        workspace.Files.Detail.Run.ToolAdmission.Revision), cancellationToken);
                Assert.Equal(workspace.Files.Agent.Id, catalog.Agent!.Id);
                var path = workspace.Layout.RunPath(workspace.Files.Detail.Run.Id);
                var run = catalog is IAgentPlanningPublicationSnapshotLease selected
                    ? selected.OriginalRun : await json.ReadJsonAsync<ExecutionRunRecord>(path, cancellationToken);
                Assert.NotNull(run);
                Assert.Equal(workspace.Files.Detail.Run.Id, run.Id);
                Assert.Equal(workspace.Files.Detail.Run.Revision, run.Revision);
                Assert.Equal(workspace.Files.Session, run.ToolAdmission!.Session.Reference);
                run.ToolAdmission.Validate();
                ReadBytes = new FileInfo(path).Length;
                acquired = Stopwatch.GetTimestamp();
                return new Held(this, catalog, dispatch);
            } catch (Exception primary) {
                List<Exception> failures = [primary];
                if (catalog is not null) {
                    try {
                        await catalog.DisposeAsync();
                    } catch (Exception cleanup) {
                        failures.Add(cleanup);
                    }
                }
                try {
                    await dispatch.DisposeAsync();
                } catch (Exception cleanup) {
                    failures.Add(cleanup);
                }
                throw new AggregateException(failures);
            }
        }

        public Task<bool> CanReadAsync(ProjectTaskPlanSource original, ProjectWriteAdmission target, CancellationToken cancellationToken) {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(original == source && target == project && runtime.Generation == generation);
        }

        private sealed class Held(PublicationAuthority owner, IAgentCatalogReadLease catalog, IAsyncDisposable dispatch)
            : IProjectTaskPlanHeldAuthority {
            private bool disposed;
            public ImmutableArray<ProjectWriteAdmission> AdditionalProjects => [];
            public Task RequireCurrentAsync(CancellationToken cancellationToken) {
                cancellationToken.ThrowIfCancellationRequested();
                ObjectDisposedException.ThrowIf(disposed, this);
                Assert.Equal(owner.generation, owner.runtime.Generation);
                Assert.NotNull(catalog.Agent);
                return Task.CompletedTask;
            }
            public async ValueTask DisposeAsync() {
                if (disposed) {
                    return;
                }
                disposed = true;
                Exception? failure = null;
                try {
                    await catalog.DisposeAsync();
                } catch (Exception cleanup) {
                    failure = cleanup;
                }
                try {
                    await dispatch.DisposeAsync();
                } catch (Exception cleanup) {
                    failure = failure is null ? cleanup : new AggregateException(failure, cleanup);
                }
                owner.released = Stopwatch.GetTimestamp();
                if (failure is not null) {
                    throw failure;
                }
            }
        }
    }

    private static T Unused<T>() where T : class => DispatchProxy.Create<T, AgentPlanningCostUnusedDependency>();
}

public class AgentPlanningCostUnusedDependency : DispatchProxy {
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        => throw new InvalidOperationException("The coarse publication cost probe reached an unexpected provider dependency.");
}
