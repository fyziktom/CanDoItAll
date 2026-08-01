using System.Text.Json;
using System.Text.Json.Nodes;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class FileSandboxWorkspaceStoreLockIntegrationTests
{
    [Fact]
    public async Task LoadCatalogAsync_reads_initialized_catalog_when_workspace_lock_is_held()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var workspaceFactory = scope.ServiceProvider.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();
        var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();
        var workspaceScope = workspaceFactory.GetOrganizationScope();

        _ = await workspaceService.ListAgentsAsync(includeTemplates: false);

        var store = new FileSandboxWorkspaceStore(application.ActiveProfile.WorkspaceRootPath, workspaceScope);
        var lockPath = BuildWorkspaceLockPath(application.ActiveProfile.WorkspaceRootPath, workspaceScope);
        await using var workspaceLock = OpenExclusiveWorkspaceLock(lockPath);
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        var catalog = await store.LoadCatalogAsync(cancellationTokenSource.Token);

        Assert.NotEmpty(catalog.Agents);
    }

    [Fact]
    public async Task GetExecutionRunDetailAsync_waits_for_workspace_lock_then_reads_initialized_run_detail()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var workspaceFactory = scope.ServiceProvider.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();
        var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();
        var workspaceScope = workspaceFactory.GetOrganizationScope();
        var store = new FileSandboxWorkspaceStore(application.ActiveProfile.WorkspaceRootPath, workspaceScope);
        var agents = await workspaceService.ListAgentsAsync(includeTemplates: false);
        Assert.NotEmpty(agents);
        var agent = agents[0];

        var executionRunId = Guid.NewGuid();
        var createdAtUtc = DateTimeOffset.UtcNow;
        await store.SaveExecutionRunDetailAsync(
            new ExecutionRunDetail(
                new ExecutionRunRecord(
                    executionRunId,
                    agent.Id,
                    null,
                    "Lock regression validation",
                    "manual",
                    "lock-regression",
                    Guid.NewGuid().ToString("N"),
                    string.Empty,
                    "integration-test",
                    "integration-test",
                    "{}",
                    "Verify execution reads observe the workspace commit boundary.",
                    "Completed",
                    "OpenAI default",
                    "gpt-4.1",
                    ExecutionState.Completed,
                    RunOutcome.Succeeded,
                    createdAtUtc,
                    createdAtUtc,
                    createdAtUtc,
                    createdAtUtc,
                    string.Empty,
                    null,
                    []),
                null,
                [
                    new ExecutionLogEntry(
                        Guid.NewGuid(),
                        agent.Id,
                        null,
                        createdAtUtc,
                        ExecutionState.Completed,
                        "validation",
                        "Workspace read should wait for the active commit boundary.")
                    {
                        ExecutionRunId = executionRunId
                    }
                ],
                []));

        var lockPath = BuildWorkspaceLockPath(application.ActiveProfile.WorkspaceRootPath, workspaceScope);
        var readStore = new FileSandboxWorkspaceStore(application.ActiveProfile.WorkspaceRootPath, workspaceScope);
        Task<ExecutionRunDetail?> readTask;

        await using (var workspaceLock = OpenExclusiveWorkspaceLock(lockPath))
        {
            readTask = readStore.GetExecutionRunDetailAsync(executionRunId);
            await Task.Delay(TimeSpan.FromMilliseconds(250));
            Assert.False(readTask.IsCompleted);
        }

        var detail = await readTask.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.NotNull(detail);

        var resolvedDetail = detail!;
        Assert.Equal(executionRunId, resolvedDetail.Run.Id);
        Assert.Single(resolvedDetail.ExecutionLog);
    }

    [Fact]
    public async Task ListExecutionRunsAsync_waits_for_workspace_lock_then_reads_run_summaries()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var workspaceFactory = scope.ServiceProvider.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();
        var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();
        var workspaceScope = workspaceFactory.GetOrganizationScope();
        var store = new FileSandboxWorkspaceStore(application.ActiveProfile.WorkspaceRootPath, workspaceScope);
        var agents = await workspaceService.ListAgentsAsync(includeTemplates: false);
        Assert.NotEmpty(agents);
        var agent = agents[0];
        var executionRunId = Guid.NewGuid();
        var createdAtUtc = DateTimeOffset.UtcNow;

        await store.SaveExecutionRunDetailAsync(
            new ExecutionRunDetail(
                new ExecutionRunRecord(
                    executionRunId,
                    agent.Id,
                    null,
                    "Run summary validation",
                    "manual",
                    "run-list-regression",
                    Guid.NewGuid().ToString("N"),
                    string.Empty,
                    "integration-test",
                    "integration-test",
                    "{}",
                    "Verify run summary listing observes the workspace commit boundary.",
                    "Completed",
                    "OpenAI default",
                    "gpt-4.1",
                    ExecutionState.Completed,
                    RunOutcome.Succeeded,
                    createdAtUtc,
                    createdAtUtc,
                    createdAtUtc,
                    createdAtUtc,
                    string.Empty,
                    null,
                    []),
                null,
                [
                    new ExecutionLogEntry(
                        Guid.NewGuid(),
                        agent.Id,
                        null,
                        createdAtUtc,
                        ExecutionState.Completed,
                        "validation",
                        "Run summary listing should not read the full execution state.")
                    {
                        ExecutionRunId = executionRunId
                    }
                ],
                []));

        var lockPath = BuildWorkspaceLockPath(application.ActiveProfile.WorkspaceRootPath, workspaceScope);
        ISandboxWorkspaceExecutionRunStore readStore = new FileSandboxWorkspaceStore(application.ActiveProfile.WorkspaceRootPath, workspaceScope);
        Task<IReadOnlyList<ExecutionRunRecord>> readTask;

        await using (var workspaceLock = OpenExclusiveWorkspaceLock(lockPath))
        {
            readTask = readStore.ListExecutionRunsAsync();
            await Task.Delay(TimeSpan.FromMilliseconds(250));
            Assert.False(readTask.IsCompleted);
        }

        var runs = await readTask.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Contains(runs, run => run.Id == executionRunId);
    }

    [Fact]
    public async Task ChatSessionStore_creates_and_updates_split_session_projection()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var workspaceFactory = scope.ServiceProvider.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();
        var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();
        var workspaceScope = workspaceFactory.GetOrganizationScope();
        var agent = (await workspaceService.ListAgentsAsync(includeTemplates: false)).First();
        var store = new FileSandboxWorkspaceStore(application.ActiveProfile.WorkspaceRootPath, workspaceScope);
        var chatSessionStore = (ISandboxWorkspaceChatSessionStore)store;
        var chatQueryStore = (ISandboxWorkspaceChatQueryStore)store;
        var now = DateTimeOffset.UtcNow;
        var session = new ChatSessionRecord(
            Id: Guid.NewGuid(),
            AgentId: agent.Id,
            Title: "Projection test",
            CreatedAtUtc: now,
            UpdatedAtUtc: now,
            RuntimeSessionKey: string.Empty,
            SerializedSessionStateJson: null,
            Messages: [],
            PendingApprovals: []);

        var createdSession = await chatSessionStore.CreateChatSessionAsync(session);
        var createdSummaries = await chatQueryStore.ListChatSessionSummariesAsync(agent.Id);

        Assert.Equal(session.Id, createdSession.Id);
        Assert.Contains(createdSummaries, summary => summary.Id == session.Id && summary.Title == "Projection test");

        var renamedSession = await chatSessionStore.UpdateChatSessionAsync(createdSession with
        {
            Title = "Projection test renamed",
            UpdatedAtUtc = now.AddMinutes(1)
        });
        var loadedSession = await chatQueryStore.GetChatSessionAsync(session.Id);
        var renamedSummaries = await chatQueryStore.ListChatSessionSummariesAsync(agent.Id);

        Assert.Equal("Projection test renamed", renamedSession.Title);
        Assert.Equal("Projection test renamed", loadedSession?.Title);
        Assert.Contains(renamedSummaries, summary => summary.Id == session.Id && summary.Title == "Projection test renamed");
    }

    [Fact]
    public async Task CreateChatSessionAsync_does_not_read_unrelated_run_file_when_chat_index_exists()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var workspaceFactory = scope.ServiceProvider.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();
        var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();
        var workspaceScope = workspaceFactory.GetOrganizationScope();
        var store = new FileSandboxWorkspaceStore(application.ActiveProfile.WorkspaceRootPath, workspaceScope);
        var layout = new FileSandboxWorkspaceStorageLayout(application.ActiveProfile.WorkspaceRootPath, workspaceScope);
        var agent = (await workspaceService.ListAgentsAsync(includeTemplates: false)).First();
        var unrelatedRunId = Guid.NewGuid();
        await store.SaveExecutionRunDetailAsync(CreateRunDetail(unrelatedRunId, agent.Id, "Unrelated run"));
        Assert.True(File.Exists(layout.ExecutionChatIndexPath));

        await File.WriteAllTextAsync(
            layout.RunPath(unrelatedRunId),
            "{ this is not valid json",
            CancellationToken.None);

        var now = DateTimeOffset.UtcNow;
        var session = new ChatSessionRecord(
            Id: Guid.NewGuid(),
            AgentId: agent.Id,
            Title: "New session without a run",
            CreatedAtUtc: now,
            UpdatedAtUtc: now,
            RuntimeSessionKey: string.Empty,
            SerializedSessionStateJson: null,
            Messages: [],
            PendingApprovals: []);

        var createdSession = await ((ISandboxWorkspaceChatSessionStore)store).CreateChatSessionAsync(session);

        Assert.Equal(session.Id, createdSession.Id);
    }

    [Fact]
    public async Task SaveExecutionRunDetailAsync_does_not_load_unrelated_run_slices_in_split_storage()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var workspaceFactory = scope.ServiceProvider.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();
        var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();
        var workspaceScope = workspaceFactory.GetOrganizationScope();
        var store = new FileSandboxWorkspaceStore(application.ActiveProfile.WorkspaceRootPath, workspaceScope);
        var layout = new FileSandboxWorkspaceStorageLayout(application.ActiveProfile.WorkspaceRootPath, workspaceScope);
        var agents = await workspaceService.ListAgentsAsync(includeTemplates: false);
        Assert.NotEmpty(agents);
        var agent = agents[0];

        var unrelatedRunId = Guid.NewGuid();
        await store.SaveExecutionRunDetailAsync(CreateRunDetail(unrelatedRunId, agent.Id, "Unrelated run"));

        var unrelatedReceiptsRoot = layout.RunReceiptsRoot(unrelatedRunId);
        Directory.CreateDirectory(unrelatedReceiptsRoot);
        await File.WriteAllTextAsync(
            Path.Combine(unrelatedReceiptsRoot, "corrupt-receipt.json"),
            "{ this is not valid json",
            CancellationToken.None);

        var targetRunId = Guid.NewGuid();
        await store.SaveExecutionRunDetailAsync(CreateRunDetail(targetRunId, agent.Id, "Target run"));

        var savedDetail = await store.GetExecutionRunDetailAsync(targetRunId);

        Assert.NotNull(savedDetail);
        Assert.Equal(targetRunId, savedDetail!.Run.Id);
    }

    [Fact]
    public async Task UpdateExecutionRunDetailAsync_serializes_competing_mutations_against_the_latest_run()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var workspaceFactory = scope.ServiceProvider.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();
        var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();
        var workspaceScope = workspaceFactory.GetOrganizationScope();
        var firstStore = new FileSandboxWorkspaceStore(application.ActiveProfile.WorkspaceRootPath, workspaceScope);
        var secondStore = new FileSandboxWorkspaceStore(application.ActiveProfile.WorkspaceRootPath, workspaceScope);
        var firstMutationStore = Assert.IsAssignableFrom<ISandboxWorkspaceExecutionRunMutationStore>(firstStore);
        var secondMutationStore = Assert.IsAssignableFrom<ISandboxWorkspaceExecutionRunMutationStore>(secondStore);
        var agent = (await workspaceService.ListAgentsAsync(includeTemplates: false)).First();
        var runId = Guid.NewGuid();
        await firstStore.SaveExecutionRunDetailAsync(CreateRunDetail(runId, agent.Id, "Atomic run mutation"));
        var appliedCount = 0;

        Task MutateAsync(ISandboxWorkspaceExecutionRunMutationStore mutationStore) => mutationStore.UpdateExecutionRunDetailAsync(
            runId,
            (_, detail) =>
            {
                if (!string.Equals(detail.Run.ResultSummary, "Completed", StringComparison.Ordinal))
                {
                    return detail;
                }

                Interlocked.Increment(ref appliedCount);
                return detail with
                {
                    Run = detail.Run with
                    {
                        ResultSummary = "Approved",
                        Revision = detail.Run.Revision + 1
                    }
                };
            });

        await Task.WhenAll(MutateAsync(firstMutationStore), MutateAsync(secondMutationStore));
        var savedDetail = await firstStore.GetExecutionRunDetailAsync(runId);

        Assert.Equal(1, appliedCount);
        Assert.NotNull(savedDetail);
        Assert.Equal("Approved", savedDetail!.Run.ResultSummary);
        Assert.Equal(2, savedDetail.Run.Revision);
    }

    [Fact]
    public async Task Catalog_revision_changes_once_for_a_change_and_retains_for_a_noop()
    {
        await using var environment = CanDoItAllTestEnvironment.Create(
            "agent-catalog-revision-change");
        var profile = environment.CreateInMemoryProfile("primary");
        var store = new FileSandboxWorkspaceStore(
            profile.WorkspaceRootPath,
            WorkspaceScopeDescriptor.Sandbox);
        var initial = await store.LoadCatalogSnapshotAsync();
        var changedNotes = $"revision-change-{Guid.NewGuid():N}";

        var changed = await store.UpdateCatalogAsync(
            catalog => WithFirstProviderNotes(catalog, changedNotes));
        var noOp = await store.UpdateCatalogAsync(
            catalog => catalog with
            {
                CatalogDataRevision = new CatalogDataRevision(long.MaxValue)
            });
        var saved = await store.LoadCatalogSnapshotAsync();

        Assert.Equal(initial.Revision.Next(), changed.CatalogDataRevision);
        Assert.Equal(changed.CatalogDataRevision, noOp.CatalogDataRevision);
        Assert.Equal(noOp.CatalogDataRevision, saved.Revision);
        Assert.Equal(saved.Revision, saved.Catalog.CatalogDataRevision);
        Assert.Equal(changedNotes, saved.Catalog.Providers[0].Notes);
    }

    [Fact]
    public async Task SaveCatalogAsync_returns_store_assigned_revision_and_ignores_caller_revision()
    {
        await using var environment = CanDoItAllTestEnvironment.Create(
            "agent-catalog-revision-save");
        var profile = environment.CreateInMemoryProfile("primary");
        var store = new FileSandboxWorkspaceStore(
            profile.WorkspaceRootPath,
            WorkspaceScopeDescriptor.Sandbox);
        var initial = await store.LoadCatalogSnapshotAsync();
        var supplied = WithFirstProviderNotes(
            initial.Catalog,
            $"caller-revision-{Guid.NewGuid():N}") with
        {
            CatalogDataRevision = new CatalogDataRevision(50_000)
        };

        var saved = await store.SaveCatalogAsync(supplied);

        Assert.Equal(initial.Revision.Next(), saved.CatalogDataRevision);
        Assert.NotEqual(supplied.CatalogDataRevision, saved.CatalogDataRevision);
        Assert.Equal(saved.CatalogDataRevision, (await store.LoadCatalogSnapshotAsync()).Revision);
    }

    [Fact]
    public async Task Execution_session_and_run_writes_retain_catalog_revision()
    {
        await using var environment = CanDoItAllTestEnvironment.Create(
            "agent-catalog-revision-execution");
        var profile = environment.CreateInMemoryProfile("primary");
        var store = new FileSandboxWorkspaceStore(
            profile.WorkspaceRootPath,
            WorkspaceScopeDescriptor.Sandbox);
        var initialCatalog = await store.LoadCatalogSnapshotAsync();
        var initialWorkspace = await store.LoadSnapshotAsync();
        var agent = initialCatalog.Catalog.Agents[0];
        var now = DateTimeOffset.UtcNow;

        await store.UpdateExecutionAsync(
            execution => execution with
            {
                ExecutionLog =
                [
                    .. execution.ExecutionLog,
                    new ExecutionLogEntry(
                        Guid.NewGuid(),
                        agent.Id,
                        null,
                        now,
                        ExecutionState.Completed,
                        "catalog-revision",
                        "Execution-only writes must not invalidate catalog data.")
                ]
            });
        Assert.Equal(
            initialCatalog.Revision,
            (await store.LoadCatalogSnapshotAsync()).Revision);

        await ((ISandboxWorkspaceChatSessionStore)store).CreateChatSessionAsync(
            new ChatSessionRecord(
                Id: Guid.NewGuid(),
                AgentId: agent.Id,
                Title: "Catalog revision isolation",
                CreatedAtUtc: now,
                UpdatedAtUtc: now,
                RuntimeSessionKey: string.Empty,
                SerializedSessionStateJson: null,
                Messages: [],
                PendingApprovals: []));
        Assert.Equal(
            initialCatalog.Revision,
            (await store.LoadCatalogSnapshotAsync()).Revision);

        await store.SaveExecutionRunDetailAsync(
            CreateRunDetail(
                Guid.NewGuid(),
                agent.Id,
                "Catalog revision isolation"));
        var finalCatalog = await store.LoadCatalogSnapshotAsync();
        var finalWorkspace = await store.LoadSnapshotAsync();

        Assert.Equal(initialCatalog.Revision, finalCatalog.Revision);
        Assert.True(finalWorkspace.Revision > initialWorkspace.Revision);
    }

    [Fact]
    public async Task Catalog_normalization_and_legacy_missing_revision_migrate_monotonically()
    {
        await using var environment = CanDoItAllTestEnvironment.Create(
            "agent-catalog-revision-migration");
        var profile = environment.CreateInMemoryProfile("primary");
        var normalizedStore = new FileSandboxWorkspaceStore(
            profile.WorkspaceRootPath,
            WorkspaceScopeDescriptor.Sandbox);
        var normalizedInitial = await normalizedStore.LoadCatalogSnapshotAsync();
        var normalizedLayout = new FileSandboxWorkspaceStorageLayout(
            profile.WorkspaceRootPath,
            WorkspaceScopeDescriptor.Sandbox);
        await ReplaceCatalogJsonAsync(
            normalizedLayout.CatalogPath,
            root => root[JsonName(nameof(SandboxWorkspaceCatalog.Version))] = string.Empty);

        var normalized = await normalizedStore.LoadCatalogSnapshotAsync();

        Assert.Equal(normalizedInitial.Revision.Next(), normalized.Revision);
        Assert.False(string.IsNullOrWhiteSpace(normalized.Catalog.Version));

        var legacyScope = WorkspaceScopeDescriptor.Project($"legacy-{Guid.NewGuid():N}");
        var legacyStore = new FileSandboxWorkspaceStore(
            profile.WorkspaceRootPath,
            legacyScope);
        _ = await legacyStore.LoadCatalogSnapshotAsync();
        var legacyLayout = new FileSandboxWorkspaceStorageLayout(
            profile.WorkspaceRootPath,
            legacyScope);
        await ReplaceCatalogJsonAsync(
            legacyLayout.CatalogPath,
            root => root.Remove(
                JsonName(nameof(SandboxWorkspaceCatalog.CatalogDataRevision))));

        var migrated = await legacyStore.LoadCatalogSnapshotAsync();
        var changed = await legacyStore.UpdateCatalogAsync(
            catalog => WithFirstProviderNotes(
                catalog,
                $"legacy-migrated-{Guid.NewGuid():N}"));

        Assert.Equal(CatalogDataRevision.Initial, migrated.Revision);
        Assert.Equal(migrated.Revision.Next(), changed.CatalogDataRevision);
        Assert.Equal(
            changed.CatalogDataRevision,
            (await legacyStore.LoadCatalogSnapshotAsync()).Revision);
    }

    [Fact]
    public async Task Competing_catalog_changes_publish_coherent_monotonic_snapshots()
    {
        await using var environment = CanDoItAllTestEnvironment.Create(
            "agent-catalog-revision-concurrency");
        var profile = environment.CreateInMemoryProfile("primary");
        var scope = WorkspaceScopeDescriptor.Sandbox;
        var firstStore = new FileSandboxWorkspaceStore(
            profile.WorkspaceRootPath,
            scope);
        var secondStore = new FileSandboxWorkspaceStore(
            profile.WorkspaceRootPath,
            scope);
        var initial = await firstStore.LoadCatalogSnapshotAsync();

        var results = await Task.WhenAll(
            firstStore.UpdateCatalogAsync(
                catalog => WithFirstProviderNotes(catalog, "first change")),
            secondStore.UpdateCatalogAsync(
                catalog => WithFirstProviderNotes(catalog, "second change")));
        var saved = await firstStore.LoadCatalogSnapshotAsync();
        var revisions = results
            .Select(catalog => catalog.CatalogDataRevision.Value)
            .Order()
            .ToArray();

        Assert.Equal(
            [initial.Revision.Value + 1, initial.Revision.Value + 2],
            revisions);
        Assert.Equal(initial.Revision.Value + 2, saved.Revision.Value);
        Assert.Equal(saved.Revision, saved.Catalog.CatalogDataRevision);
    }

    [Fact]
    public async Task Atomic_chat_run_starts_preserve_every_concurrent_user_message()
    {
        await using var environment = CanDoItAllTestEnvironment.Create(
            "agent-chat-run-start-concurrency");
        var profile = environment.CreateInMemoryProfile("primary");
        var scope = WorkspaceScopeDescriptor.Sandbox;
        var firstStore = new FileSandboxWorkspaceStore(
            profile.WorkspaceRootPath,
            scope);
        var secondStore = new FileSandboxWorkspaceStore(
            profile.WorkspaceRootPath,
            scope);
        var catalog = await firstStore.LoadCatalogSnapshotAsync();
        var agent = catalog.Catalog.Agents.First(item => item.ProviderProfileId.HasValue);
        var initialMessage = new ChatMessageRecord(
            Guid.NewGuid(),
            ChatMessageRole.Assistant,
            "Existing history",
            DateTimeOffset.UtcNow,
            2);
        var session = await ((ISandboxWorkspaceChatSessionStore)firstStore)
            .CreateChatSessionAsync(
                new ChatSessionRecord(
                    Guid.NewGuid(),
                    agent.Id,
                    "Atomic chat start",
                    DateTimeOffset.UtcNow,
                    DateTimeOffset.UtcNow,
                    [initialMessage]));
        var request = new ChatBackedRunStartRequest(
            agent.Id,
            agent.ProviderProfileId!.Value,
            catalog.Revision,
            session.Id);

        var starts = await Task.WhenAll(
            ((ISandboxWorkspaceChatRunStartStore)firstStore).BeginChatBackedRunAsync(
                request,
                context => CreateChatRunStartMutation(context, "first concurrent prompt")),
            ((ISandboxWorkspaceChatRunStartStore)secondStore).BeginChatBackedRunAsync(
                request,
                context => CreateChatRunStartMutation(context, "second concurrent prompt")));
        var savedSession = await ((ISandboxWorkspaceChatQueryStore)firstStore)
            .GetChatSessionAsync(session.Id);

        Assert.NotNull(savedSession);
        Assert.Equal(initialMessage, savedSession.Messages[0]);
        Assert.Equal(3, savedSession.Messages.Count);
        Assert.Equal(
            ["first concurrent prompt", "second concurrent prompt"],
            savedSession.Messages
                .Skip(1)
                .Select(message => message.Content)
                .Order(StringComparer.Ordinal)
                .ToArray());
        Assert.All(starts, result => Assert.IsType<ChatBackedRunStarted>(result));
        Assert.Equal(
            2,
            starts
                .OfType<ChatBackedRunStarted>()
                .Select(result => result.Detail.Run.Id)
                .Distinct()
                .Count());
        Assert.Equal(
            catalog.Revision,
            (await firstStore.LoadCatalogSnapshotAsync()).Revision);
    }

    [Fact]
    public async Task Atomic_chat_run_start_rejects_stale_catalog_revision_without_persisting()
    {
        await using var environment = CanDoItAllTestEnvironment.Create(
            "agent-chat-run-start-stale-catalog");
        var profile = environment.CreateInMemoryProfile("primary");
        var store = new FileSandboxWorkspaceStore(
            profile.WorkspaceRootPath,
            WorkspaceScopeDescriptor.Sandbox);
        var initialCatalog = await store.LoadCatalogSnapshotAsync();
        var agent = initialCatalog.Catalog.Agents.First(item => item.ProviderProfileId.HasValue);
        var initialMessage = new ChatMessageRecord(
            Guid.NewGuid(),
            ChatMessageRole.Assistant,
            "Existing history",
            DateTimeOffset.UtcNow,
            2);
        var session = await ((ISandboxWorkspaceChatSessionStore)store)
            .CreateChatSessionAsync(
                new ChatSessionRecord(
                    Guid.NewGuid(),
                    agent.Id,
                    "Stale catalog",
                    DateTimeOffset.UtcNow,
                    DateTimeOffset.UtcNow,
                    [initialMessage]));
        var changedCatalog = await store.UpdateCatalogAsync(
            catalog => WithFirstProviderNotes(
                catalog,
                $"stale-catalog-{Guid.NewGuid():N}"));
        var factoryInvoked = false;

        var exception = await Assert.ThrowsAsync<SandboxWorkspaceCatalogConcurrencyException>(
            () => ((ISandboxWorkspaceChatRunStartStore)store).BeginChatBackedRunAsync(
                new ChatBackedRunStartRequest(
                    agent.Id,
                    agent.ProviderProfileId!.Value,
                    initialCatalog.Revision,
                    session.Id),
                context =>
                {
                    factoryInvoked = true;
                    return CreateChatRunStartMutation(context, "must not persist");
                }));
        var savedSession = await ((ISandboxWorkspaceChatQueryStore)store)
            .GetChatSessionAsync(session.Id);

        Assert.Equal(initialCatalog.Revision, exception.ExpectedRevision);
        Assert.Equal(changedCatalog.CatalogDataRevision, exception.ActualRevision);
        Assert.False(factoryInvoked);
        Assert.NotNull(savedSession);
        Assert.Equal([initialMessage], savedSession.Messages);
    }

    [Fact]
    public async Task Atomic_chat_run_start_returns_blocked_without_invoking_the_factory()
    {
        await using var environment = CanDoItAllTestEnvironment.Create(
            "agent-chat-run-start-blocked");
        var profile = environment.CreateInMemoryProfile("primary");
        var store = new FileSandboxWorkspaceStore(
            profile.WorkspaceRootPath,
            WorkspaceScopeDescriptor.Sandbox);
        var catalog = await store.LoadCatalogSnapshotAsync();
        var agent = catalog.Catalog.Agents.First(item => item.ProviderProfileId.HasValue);
        var initialMessage = new ChatMessageRecord(
            Guid.NewGuid(),
            ChatMessageRole.Assistant,
            "Existing history",
            DateTimeOffset.UtcNow,
            2);
        var session = await ((ISandboxWorkspaceChatSessionStore)store)
            .CreateChatSessionAsync(
                new ChatSessionRecord(
                    Guid.NewGuid(),
                    agent.Id,
                    "Blocked start",
                    DateTimeOffset.UtcNow,
                    DateTimeOffset.UtcNow,
                    [initialMessage]));
        var request = new ChatBackedRunStartRequest(
            agent.Id,
            agent.ProviderProfileId!.Value,
            catalog.Revision,
            session.Id);
        var started = Assert.IsType<ChatBackedRunStarted>(
            await ((ISandboxWorkspaceChatRunStartStore)store).BeginChatBackedRunAsync(
                request,
                context => CreateChatRunStartMutation(
                    context,
                    "active prompt",
                    ExecutionState.Preparing)));
        var factoryInvoked = false;

        var result = await ((ISandboxWorkspaceChatRunStartStore)store)
            .BeginChatBackedRunAsync(
                request,
                context =>
                {
                    factoryInvoked = true;
                    return CreateChatRunStartMutation(context, "must be blocked");
                });
        var blocked = Assert.IsType<ChatBackedRunBlocked>(result);
        var savedSession = await ((ISandboxWorkspaceChatQueryStore)store)
            .GetChatSessionAsync(session.Id);

        Assert.False(factoryInvoked);
        Assert.Equal(started.Detail.Run.Id, blocked.BlockingRun.Id);
        Assert.NotNull(savedSession);
        Assert.Equal(
            [initialMessage, started.UserMessage],
            savedSession.Messages);
    }

    [Fact]
    public async Task Atomic_chat_run_start_rejects_a_mutation_that_drops_session_history()
    {
        await using var environment = CanDoItAllTestEnvironment.Create(
            "agent-chat-run-start-history");
        var profile = environment.CreateInMemoryProfile("primary");
        var store = new FileSandboxWorkspaceStore(
            profile.WorkspaceRootPath,
            WorkspaceScopeDescriptor.Sandbox);
        var catalog = await store.LoadCatalogSnapshotAsync();
        var agent = catalog.Catalog.Agents.First(item => item.ProviderProfileId.HasValue);
        var initialMessage = new ChatMessageRecord(
            Guid.NewGuid(),
            ChatMessageRole.Assistant,
            "History must survive",
            DateTimeOffset.UtcNow,
            3);
        var session = await ((ISandboxWorkspaceChatSessionStore)store)
            .CreateChatSessionAsync(
                new ChatSessionRecord(
                    Guid.NewGuid(),
                    agent.Id,
                    "History validation",
                    DateTimeOffset.UtcNow,
                    DateTimeOffset.UtcNow,
                    [initialMessage]));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => ((ISandboxWorkspaceChatRunStartStore)store).BeginChatBackedRunAsync(
                new ChatBackedRunStartRequest(
                    agent.Id,
                    agent.ProviderProfileId!.Value,
                    catalog.Revision,
                    session.Id),
                context =>
                {
                    var mutation = CreateChatRunStartMutation(context, "invalid overwrite");
                    return mutation with
                    {
                        Detail = mutation.Detail with
                        {
                            ChatSession = mutation.Detail.ChatSession! with
                            {
                                Messages = [mutation.UserMessage]
                            }
                        }
                    };
                }));
        var savedSession = await ((ISandboxWorkspaceChatQueryStore)store)
            .GetChatSessionAsync(session.Id);

        Assert.NotNull(savedSession);
        Assert.Equal([initialMessage], savedSession.Messages);
    }

    private static FileStream OpenExclusiveWorkspaceLock(string lockPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(lockPath)!);
        return new FileStream(
            lockPath,
            FileMode.OpenOrCreate,
            FileAccess.ReadWrite,
            FileShare.None,
            bufferSize: 1,
            options: FileOptions.Asynchronous);
    }

    private static string BuildWorkspaceLockPath(string workspaceRoot, WorkspaceScopeDescriptor scope)
    {
        return Path.Combine(
            workspaceRoot,
            scope.DataRootRelativePath.Replace('/', Path.DirectorySeparatorChar),
            "workspace.lock");
    }

    private static ExecutionRunDetail CreateRunDetail(Guid executionRunId, Guid agentId, string title)
    {
        var createdAtUtc = DateTimeOffset.UtcNow;
        return new ExecutionRunDetail(
            new ExecutionRunRecord(
                executionRunId,
                agentId,
                null,
                title,
                "manual",
                "run-slice-regression",
                Guid.NewGuid().ToString("N"),
                string.Empty,
                "integration-test",
                "integration-test",
                "{}",
                "Verify run-detail saves do not load unrelated run slices.",
                "Completed",
                "OpenAI default",
                "gpt-4.1",
                ExecutionState.Completed,
                RunOutcome.Succeeded,
                createdAtUtc,
                createdAtUtc,
                createdAtUtc,
                createdAtUtc,
                string.Empty,
                null,
                []),
            null,
            [
                new ExecutionLogEntry(
                    Guid.NewGuid(),
                    agentId,
                    null,
                    createdAtUtc,
                    ExecutionState.Completed,
                    "validation",
                    "Run detail should save without inspecting unrelated runs.")
                {
                    ExecutionRunId = executionRunId
                }
            ],
            []);
    }

    private static ChatBackedRunStartMutation CreateChatRunStartMutation(
        ChatBackedRunStartContext context,
        string prompt,
        ExecutionState state = ExecutionState.Completed)
    {
        var now = DateTimeOffset.UtcNow;
        var userMessage = new ChatMessageRecord(
            Guid.NewGuid(),
            ChatMessageRole.User,
            prompt,
            now,
            4);
        var session = context.Session
            ?? new ChatSessionRecord(
                Guid.NewGuid(),
                context.Agent.Id,
                prompt,
                now,
                now,
                []);
        var runId = Guid.NewGuid();
        var updatedSession = session with
        {
            UpdatedAtUtc = now,
            LatestExecutionRunId = runId,
            Messages = [.. session.Messages, userMessage]
        };
        var run = new ExecutionRunRecord(
            runId,
            context.Agent.Id,
            updatedSession.Id,
            updatedSession.Title,
            "integration-test",
            updatedSession.Id.ToString("N"),
            Guid.NewGuid().ToString("N"),
            string.Empty,
            "integration-test",
            "integration-test",
            "{}",
            prompt,
            "Completed atomically.",
            "Integration test",
            "gpt-5.4-mini",
            state,
            state == ExecutionState.Completed ? RunOutcome.Succeeded : null,
            now,
            now,
            now,
            state == ExecutionState.Completed ? now : null,
            string.Empty,
            null,
            [])
        {
            ProviderProfileId = context.Agent.ProviderProfileId
        };

        return new ChatBackedRunStartMutation(
            new ExecutionRunDetail(
                run,
                updatedSession,
                [],
                []),
            userMessage);
    }

    private static SandboxWorkspaceCatalog WithFirstProviderNotes(
        SandboxWorkspaceCatalog catalog,
        string notes)
    {
        Assert.NotEmpty(catalog.Providers);
        return catalog with
        {
            Providers =
            [
                catalog.Providers[0] with
                {
                    Notes = notes
                },
                .. catalog.Providers.Skip(1)
            ]
        };
    }

    private static async Task ReplaceCatalogJsonAsync(
        string catalogPath,
        Action<JsonObject> update)
    {
        var root = JsonNode.Parse(await File.ReadAllTextAsync(catalogPath))
            ?.AsObject()
            ?? throw new InvalidDataException(
                $"Catalog JSON '{catalogPath}' did not contain an object.");
        update(root);
        await File.WriteAllTextAsync(
            catalogPath,
            root.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = true
            }));
    }

    private static string JsonName(string propertyName)
        => JsonNamingPolicy.CamelCase.ConvertName(propertyName);
}
