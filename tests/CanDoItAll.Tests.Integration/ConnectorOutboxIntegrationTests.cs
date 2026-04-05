using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Resources;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class ConnectorOutboxIntegrationTests
{
    [Fact]
    public async Task EnqueueAsync_is_idempotent_and_retries_until_completion()
    {
        var handler = new TestConnectorCommandHandler(
            ConnectorCommandExecutionResult.RetryableFailure("Temporary webhook failure."),
            ConnectorCommandExecutionResult.Completed("""{"delivery":"ok"}"""));

        await using var harness = await ConnectorOutboxHarness.CreateAsync(handler);
        await using var scope = harness.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var outbox = scope.ServiceProvider.GetRequiredService<ConnectorOutboxService>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();

        var projectId = await CreateProjectAsync(projects, "Connector outbox idempotency");
        var request = new ConnectorCommandEnqueueRequest(
            projectId,
            WebhookResourceConnectorPlugin.PluginKey,
            "deliver",
            """{"endpointUrl":"https://example.com/hooks/orders"}""",
            "idem-webhook-deliver-001",
            "integration-tests");

        var first = await outbox.EnqueueAsync(request);
        var duplicate = await outbox.EnqueueAsync(request);

        Assert.False(first.IsDuplicate);
        Assert.True(duplicate.IsDuplicate);
        Assert.Equal(first.CommandId, duplicate.CommandId);

        var firstStatus = await outbox.ProcessAsync(first.CommandId);
        Assert.Equal(ConnectorCommandStatus.Pending, firstStatus);

        await ForceNextAttemptDueAsync(dbContextFactory, first.CommandId);

        var secondStatus = await outbox.ProcessAsync(first.CommandId);
        Assert.Equal(ConnectorCommandStatus.Completed, secondStatus);

        var snapshot = await outbox.GetAsync(first.CommandId);
        Assert.NotNull(snapshot);
        Assert.Equal(ConnectorCommandStatus.Completed, snapshot!.Status);
        Assert.Equal(2, snapshot.AttemptCount);
        Assert.Equal("""{"delivery":"ok"}""", snapshot.ResultJson);
        Assert.Equal(2, handler.Requests.Count);

        var audit = await outbox.ListAuditAsync(first.CommandId);
        Assert.Contains(audit, entry => entry.EventKind == ConnectorCommandAuditEventKind.IdempotencyHit);
        Assert.Contains(audit, entry => entry.EventKind == ConnectorCommandAuditEventKind.AttemptFailed);
        Assert.Contains(audit, entry => entry.EventKind == ConnectorCommandAuditEventKind.Completed);
    }

    [Fact]
    public async Task ReplayAsync_requeues_dead_lettered_commands_and_allows_manual_recovery()
    {
        var handler = new TestConnectorCommandHandler(
            ConnectorCommandExecutionResult.RetryableFailure("Attempt one failed."),
            ConnectorCommandExecutionResult.RetryableFailure("Attempt two failed."),
            ConnectorCommandExecutionResult.RetryableFailure("Attempt three failed."),
            ConnectorCommandExecutionResult.Completed("""{"delivery":"replayed"}"""));

        await using var harness = await ConnectorOutboxHarness.CreateAsync(handler);
        await using var scope = harness.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var outbox = scope.ServiceProvider.GetRequiredService<ConnectorOutboxService>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();

        var projectId = await CreateProjectAsync(projects, "Connector outbox replay");
        var enqueue = await outbox.EnqueueAsync(new ConnectorCommandEnqueueRequest(
            projectId,
            WebhookResourceConnectorPlugin.PluginKey,
            "deliver",
            """{"endpointUrl":"https://example.com/hooks/replay"}""",
            "idem-webhook-replay-001",
            "integration-tests"));

        Assert.Equal(ConnectorCommandStatus.Pending, await outbox.ProcessAsync(enqueue.CommandId));
        await ForceNextAttemptDueAsync(dbContextFactory, enqueue.CommandId);
        Assert.Equal(ConnectorCommandStatus.Pending, await outbox.ProcessAsync(enqueue.CommandId));
        await ForceNextAttemptDueAsync(dbContextFactory, enqueue.CommandId);
        Assert.Equal(ConnectorCommandStatus.DeadLettered, await outbox.ProcessAsync(enqueue.CommandId));

        var deadLetterSnapshot = await outbox.GetAsync(enqueue.CommandId);
        Assert.NotNull(deadLetterSnapshot);
        Assert.Equal(ConnectorCommandStatus.DeadLettered, deadLetterSnapshot!.Status);
        Assert.Equal(3, deadLetterSnapshot.AttemptCount);
        Assert.NotEmpty(deadLetterSnapshot.LastError);

        Assert.True(await outbox.ReplayAsync(enqueue.CommandId, "integration-tests", "Manual replay after dead-letter."));
        Assert.Equal(ConnectorCommandStatus.Completed, await outbox.ProcessAsync(enqueue.CommandId));

        var completedSnapshot = await outbox.GetAsync(enqueue.CommandId);
        Assert.NotNull(completedSnapshot);
        Assert.Equal(ConnectorCommandStatus.Completed, completedSnapshot!.Status);
        Assert.Equal(4, completedSnapshot.AttemptCount);
        Assert.Equal("""{"delivery":"replayed"}""", completedSnapshot.ResultJson);

        var audit = await outbox.ListAuditAsync(enqueue.CommandId);
        Assert.Contains(audit, entry => entry.EventKind == ConnectorCommandAuditEventKind.DeadLettered);
        Assert.Contains(audit, entry => entry.EventKind == ConnectorCommandAuditEventKind.Replayed);
        Assert.Equal(4, handler.Requests.Count);
    }

    [Fact]
    public async Task Approval_gates_execution_and_records_audit_history()
    {
        var handler = new TestConnectorCommandHandler(
            ConnectorCommandExecutionResult.Completed("""{"delivery":"approved"}"""));

        await using var harness = await ConnectorOutboxHarness.CreateAsync(handler);
        await using var scope = harness.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var outbox = scope.ServiceProvider.GetRequiredService<ConnectorOutboxService>();

        var projectId = await CreateProjectAsync(projects, "Connector outbox approval");
        var enqueue = await outbox.EnqueueAsync(new ConnectorCommandEnqueueRequest(
            projectId,
            WebhookResourceConnectorPlugin.PluginKey,
            "deliver",
            """{"endpointUrl":"https://example.com/hooks/approval"}""",
            "idem-webhook-approval-001",
            "integration-tests",
            RequiresApproval: true));

        Assert.Equal(ConnectorCommandApprovalState.Pending, enqueue.ApprovalState);
        Assert.Equal(ConnectorCommandStatus.Pending, await outbox.ProcessAsync(enqueue.CommandId));
        Assert.Empty(handler.Requests);

        Assert.True(await outbox.ApproveAsync(enqueue.CommandId, "architect-review", "Approved for execution."));
        Assert.Equal(ConnectorCommandStatus.Completed, await outbox.ProcessAsync(enqueue.CommandId));

        var snapshot = await outbox.GetAsync(enqueue.CommandId);
        Assert.NotNull(snapshot);
        Assert.Equal(ConnectorCommandApprovalState.Approved, snapshot!.ApprovalState);
        Assert.Equal(ConnectorCommandStatus.Completed, snapshot.Status);
        Assert.Equal(1, snapshot.AttemptCount);

        var audit = await outbox.ListAuditAsync(enqueue.CommandId);
        Assert.Contains(audit, entry => entry.EventKind == ConnectorCommandAuditEventKind.ApprovalRequested);
        Assert.Contains(audit, entry => entry.EventKind == ConnectorCommandAuditEventKind.Approved);
        Assert.Contains(audit, entry => entry.EventKind == ConnectorCommandAuditEventKind.Completed);
        Assert.Single(handler.Requests);
    }

    private static async Task<Guid> CreateProjectAsync(ProjectsService projectsService, string name)
    {
        var result = await projectsService.SaveAsync(new ProjectEditorModel
        {
            Name = name,
            Description = $"{name} description",
            Objective = $"{name} objective",
            CurrentPhase = "Execution"
        });

        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private static async Task ForceNextAttemptDueAsync(
        IDbContextFactory<AppDbContext> dbContextFactory,
        Guid commandId)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        await ConnectorCommandSchemaInitializer.EnsureAsync(dbContext);
        var command = await dbContext.Set<ConnectorCommandRecord>()
            .SingleAsync(item => item.Id == commandId);
        command.NextAttemptAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1);
        await dbContext.SaveChangesAsync();
    }

    private sealed class ConnectorOutboxHarness : IAsyncDisposable
    {
        private ConnectorOutboxHarness(
            CanDoItAllTestEnvironment testEnvironment,
            ServiceProvider services)
        {
            TestEnvironment = testEnvironment;
            Services = services;
        }

        public CanDoItAllTestEnvironment TestEnvironment { get; }

        public ServiceProvider Services { get; }

        public static async Task<ConnectorOutboxHarness> CreateAsync(TestConnectorCommandHandler handler)
        {
            var testEnvironment = CanDoItAllTestEnvironment.Create("connector-outbox-tests");
            var profile = testEnvironment.CreateManagedSqliteProfile("primary");
            var services = await TestApplicationBootstrap.BuildServiceProviderAsync(
                profile,
                "CanDoItAll.Tests",
                TestSchemaBootstrapModules.Full,
                configureServices: collection =>
                {
                    collection.AddSingleton(handler);
                    collection.AddSingleton<IConnectorCommandHandler>(serviceProvider => serviceProvider.GetRequiredService<TestConnectorCommandHandler>());
                });
            return new ConnectorOutboxHarness(testEnvironment, services);
        }

        public async ValueTask DisposeAsync()
        {
            await Services.DisposeAsync();
            await TestEnvironment.DisposeAsync();
        }
    }

    private sealed class TestConnectorCommandHandler : IConnectorCommandHandler
    {
        private readonly Queue<ConnectorCommandExecutionResult> _results;

        public TestConnectorCommandHandler(params ConnectorCommandExecutionResult[] results)
        {
            _results = new Queue<ConnectorCommandExecutionResult>(results);
        }

        public List<ConnectorCommandExecutionRequest> Requests { get; } = [];

        public bool CanHandle(string connectorPluginKey, string commandKey)
        {
            return string.Equals(connectorPluginKey, WebhookResourceConnectorPlugin.PluginKey, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(commandKey, "deliver", StringComparison.OrdinalIgnoreCase);
        }

        public Task<ConnectorCommandExecutionResult> ExecuteAsync(
            ConnectorCommandExecutionRequest request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(
                _results.Count > 0
                    ? _results.Dequeue()
                    : ConnectorCommandExecutionResult.Completed("""{"delivery":"default"}"""));
        }
    }
}
