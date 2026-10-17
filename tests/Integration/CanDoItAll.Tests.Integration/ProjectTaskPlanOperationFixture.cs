using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Data;
using System.Data.Common;
using CanDoItAll.Composition;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Hosting;
using Npgsql;

namespace CanDoItAll.Tests.Integration;

internal sealed class ProjectTaskPlanOperationFixture(TestApplication application, AsyncServiceScope scope,
    ProjectWriteAdmission project, DatasetActivationBinding binding, TaskPlanProbe probe, TaskPlanFaults faults) : IAsyncDisposable {
    internal IServiceProvider Services => scope.ServiceProvider;
    internal ProjectWriteAdmission Project => project;
    internal DatasetActivationBinding Binding => binding;
    internal TaskPlanProbe Probe => probe;
    internal TaskPlanFaults Faults => faults;
    internal TaskPlanAuthority Authority { get; } = new();
    internal ProjectTaskPlanCaller Caller { get; } = new(ProjectTaskPlanCallerKind.OrdinaryAgent,
        Enumerable.Repeat((byte)41, 32).ToArray());
    internal ProjectTaskPlanInvocation Invocation => new(Caller, Authority, Authority);
    internal CoordinatedDatabaseTransaction Transactions => Services.GetRequiredService<CoordinatedDatabaseTransaction>();
    internal FiniteOperationEpochStore Epochs => Services.GetRequiredService<FiniteOperationEpochStore>();
    internal FiniteOperationPoolRegistration Pool => WorkbenchTaskPlanFiniteOperations.Create(new());
    internal ProjectTaskPlanBasisCapture Capture => new(Services.GetRequiredService<IDbContextFactory<WorkbenchDbContext>>(),
        Services.GetRequiredService<ProjectDefinitionReader>(), Services.GetRequiredService<ProjectWriteAdmissionService>(),
        Transactions, Services.GetRequiredService<DatasetActivationService>());
    internal ProjectTaskPlanReceiptReader Reader => new(Services.GetRequiredService<IDbContextFactory<WorkbenchDbContext>>(),
        Services.GetRequiredService<DatasetActivationService>(), Services.GetRequiredService<ProjectWriteAdmissionService>(),
        NullLogger<ProjectTaskPlanReceiptReader>.Instance);
    internal ProjectTaskPlanPublisher Publisher(DecisionDataPolicyBudget? ceiling = null) => new(
        Services.GetRequiredService<IDbContextFactory<WorkbenchDbContext>>(), Services.GetRequiredService<DatasetActivationService>(),
        Services.GetRequiredService<ProjectWriteAdmissionService>(), Transactions, Epochs, new(), Capture, Reader,
        ceiling ?? new(0, TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(10)), NullLogger<ProjectTaskPlanPublisher>.Instance);

    internal static async Task<ProjectTaskPlanOperationFixture> CreateAsync(CanDoItAllTestEnvironment? testEnvironment = null,
        TestDatabaseProfile? activeProfile = null) {
        if ((testEnvironment is null) != (activeProfile is null)) {
            throw new ArgumentException("An externally owned fixture requires both its environment and profile.");
        }
        var probe = new TaskPlanProbe();
        var faults = new TaskPlanFaults();
        var application = await TestApplication.CreateAsync(new TestHarnessOptions {
            TestEnvironment = testEnvironment,
            ActiveProfile = activeProfile,
            ConfigureServices = services => {
                services.ConfigureDbContext<WorkbenchDbContext>(options =>
                    options.AddInterceptors(probe, faults.Save, faults.Commit), ServiceLifetime.Singleton);
                foreach (var descriptor in services.Where(item => item.ServiceType == typeof(IHostedService) &&
                        item.ImplementationType == typeof(FiniteOperationMaintenance)).ToArray()) {
                    services.Remove(descriptor);
                }
            }
        });
        var scope = application.Services.CreateAsyncScope();
        try {
            var services = scope.ServiceProvider;
            await using var context = await services.GetRequiredService<IDbContextFactory<ProjectsDbContext>>().CreateDbContextAsync();
            var row = new Project { Name = "Task-plan owner proof", Slug = Guid.NewGuid().ToString("N") };
            context.Add(row);
            await context.SaveChangesAsync();
            var admissions = services.GetRequiredService<ProjectWriteAdmissionService>();
            var project = await admissions.CaptureAsync(row.Id) ?? throw new InvalidOperationException();
            var binding = await services.GetRequiredService<DatasetActivationService>().ReadActiveBindingAsync(admissions.DatabaseProfileId);
            return new(application, scope, project, binding, probe, faults);
        } catch {
            await scope.DisposeAsync();
            await application.DisposeAsync();
            throw;
        }
    }

    internal async Task<ProjectTaskPlanCommand> CommandAsync(int count = 2, CancellationToken cancellationToken = default,
        ProjectWriteAdmission? target = null) {
        var basis = Assert.IsType<ProjectTaskPlanBasisReadResult.Captured>(await Capture.CaptureAsync(target ?? Project,
            (_, _) => Task.CompletedTask, cancellationToken)).Basis;
        var metadata = await Publisher().GetWriteContextAsync(Caller, cancellationToken);
        return new(new(metadata.Namespace, metadata.Epoch, Guid.NewGuid(), metadata.CallerBinding), basis,
            ProjectTaskPlanProposal.Normalize(new(1, Enumerable.Range(0, count)
                .Select(index => new ProjectTaskPlanDraftItem($"Task {index}", "Description", "Notes", index % 7)).ToArray())),
            DecisionDataPolicy.PlanningDefault(TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5)),
            new(Guid.NewGuid(), 1, basis.CapturedAtUtc, ProjectTaskPlanSourceKind.OrdinaryAgentDecision, 1, new('c', 64)));
    }

    internal Task<WorkbenchDbContext> ContextAsync(CancellationToken cancellationToken = default)
        => Services.GetRequiredService<IDbContextFactory<WorkbenchDbContext>>().CreateDbContextAsync(cancellationToken);
    internal Task<ProjectTaskPlanLookup> LookupAsync(ProjectTaskPlanOperation operation, CancellationToken cancellationToken = default)
        => Reader.LookupAsync(operation, new(Caller, Authority), cancellationToken);

    internal async Task VerifyAsync(int taskCount, int receiptCount, ProjectTaskPlanAccepted? accepted = null) {
        await using var read = await ContextAsync();
        var rows = await read.Set<ProjectObjectRecord>().AsNoTracking().Where(row => row.ProjectId == Project.ProjectId).ToArrayAsync();
        Assert.Equal(taskCount, rows.Length);
        var ids = rows.Select(row => row.Id).ToArray();
        Assert.Equal(taskCount, await read.Set<ProjectNodeBindingRecord>().CountAsync(row => ids.Contains(row.ProjectObjectId)));
        Assert.Equal(receiptCount, await read.Set<ProjectTaskPlanReceiptRecord>().CountAsync());
        if (accepted is not null) {
            var receipt = await read.Set<ProjectTaskPlanReceiptRecord>().SingleAsync(row => row.OperationId == accepted.Operation.Id);
            Assert.Equal(ProjectTaskPlanOperationCodec.EncodeAccepted(accepted), receipt.AcceptedPayload);
            Assert.All(accepted.Tasks, task => {
                var row = Assert.Single(rows, row => row.Id == task.Fact.RecordId);
                Assert.Equal(task.Fact.NodeKey, row.NodeKey);
                Assert.Equal(task.Fact.Title, row.Title);
                Assert.Equal(task.MetadataJson, row.MetadataJson);
                Assert.Equal(task.CreatedAtUtc, row.CreatedAtUtc);
            });
        }
    }

    internal async Task WaitForProjectWaiterAsync(int holderPid, CancellationToken cancellationToken) {
        await using var observer = await ContextAsync(cancellationToken);
        while (true) {
            var pid = Probe.ProjectWaiterPid;
            if (pid != 0 && await observer.Database.SqlQuery<bool>($"SELECT {holderPid} = ANY(pg_blocking_pids({pid})) AS \"Value\"")
                    .SingleAsync(cancellationToken)) {
                return;
            }
            await Task.Delay(TimeSpan.FromMilliseconds(10), cancellationToken);
        }
    }

    public async ValueTask DisposeAsync() {
        Authority.Dispose();
        await scope.DisposeAsync();
        await application.DisposeAsync();
    }
}

internal sealed class TaskPlanAuthority : IProjectTaskPlanPublicationAuthority, IProjectTaskPlanReceiptAccess, IDisposable {
    private readonly SemaphoreSlim gate = new(1, 1);
    internal bool AllowWrite { get; set; } = true;
    internal bool AllowRead { get; set; } = true;
    internal bool ReadUnavailable { get; set; }
    internal bool ReleaseFails { get; set; }
    internal bool SourceChanged { get; set; }
    internal int Acquisitions { get; private set; }
    internal int CurrentChecks { get; private set; }
    internal int Disclosures { get; private set; }
    internal Func<CancellationToken, Task>? BeforeAcquire { get; set; }
    internal Func<CancellationToken, Task>? BeforeRead { get; set; }
    internal Action? OnRelease { get; set; }
    internal ImmutableArray<ProjectWriteAdmission> AdditionalProjects { get; set; } = [];

    public async Task<IProjectTaskPlanHeldAuthority> AcquireAsync(ProjectTaskPlanSource original, ProjectWriteAdmission target, CancellationToken cancellationToken) {
        if (BeforeAcquire is { } before) {
            await before(cancellationToken);
        }
        await gate.WaitAsync(cancellationToken);
        Acquisitions++;
        try {
            Require();
            return new Held(this, AdditionalProjects);
        } catch {
            gate.Release();
            throw;
        }
    }

    public async Task<bool> CanReadAsync(ProjectTaskPlanSource original, ProjectWriteAdmission target, CancellationToken cancellationToken) {
        Disclosures++;
        if (BeforeRead is { } before) {
            await before(cancellationToken);
        }
        await gate.WaitAsync(cancellationToken);
        try {
            if (ReadUnavailable) {
                throw new IOException("Injected read failure.");
            }
            return AllowRead;
        } finally {
            gate.Release();
        }
    }

    internal async Task WriteSourceAsync(Action action, CancellationToken cancellationToken) {
        await gate.WaitAsync(cancellationToken);
        try {
            action();
        } finally {
            gate.Release();
        }
    }

    private void Require() {
        CurrentChecks++;
        if (!AllowWrite || SourceChanged) {
            throw new ProjectTaskPlanAuthorityException(SourceChanged ? ProjectTaskPlanRefusal.SourceChanged : ProjectTaskPlanRefusal.AuthorityDenied);
        }
    }

    public void Dispose() => gate.Dispose();

    private sealed class Held(TaskPlanAuthority owner, ImmutableArray<ProjectWriteAdmission> projects) : IProjectTaskPlanHeldAuthority {
        public ImmutableArray<ProjectWriteAdmission> AdditionalProjects => projects;
        public Task RequireCurrentAsync(CancellationToken cancellationToken) {
            cancellationToken.ThrowIfCancellationRequested();
            owner.Require();
            return Task.CompletedTask;
        }
        public ValueTask DisposeAsync() {
            owner.gate.Release();
            owner.OnRelease?.Invoke();
            if (owner.ReleaseFails) {
                throw new IOException("Injected source release failure.");
            }
            return ValueTask.CompletedTask;
        }
    }
}

internal sealed class TaskPlanFaults {
    internal TaskPlanSaveFault Save { get; } = new();
    internal TaskPlanCommitFault Commit { get; } = new();
}

internal sealed class TaskPlanSaveFault : SaveChangesInterceptor {
    internal Func<CancellationToken, Task>? AfterFlush { get; set; }
    public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default) {
        if (AfterFlush is { } action) {
            AfterFlush = null;
            await action(cancellationToken);
        }
        return result;
    }
}

internal sealed class TaskPlanCommitFault : DbTransactionInterceptor {
    internal Action? AfterCommit { get; set; }
    public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default) {
        if (AfterCommit is { } action) {
            AfterCommit = null;
            action();
        }
        return Task.CompletedTask;
    }
}

internal sealed class TaskPlanProbe : DbCommandInterceptor {
    internal ConcurrentQueue<string> Commands { get; } = new();
    internal Func<DbCommand, CancellationToken, Task>? BeforeCommand { get; set; }
    internal Func<DbCommand, CancellationToken, Task>? AfterReader { get; set; }
    internal int ProjectWaiterPid { get; private set; }
    internal void Reset() {
        Commands.Clear();
        ProjectWaiterPid = 0;
    }
    private async Task ObserveAsync(DbCommand command, CancellationToken cancellationToken) {
        Commands.Enqueue(command.CommandText);
        if (command.CommandText.Contains("pg_advisory_xact_lock", StringComparison.Ordinal) &&
                command.Parameters.Cast<DbParameter>().Any(parameter => parameter.Value is string key && key.StartsWith("project:", StringComparison.Ordinal))) {
            ProjectWaiterPid = ((NpgsqlConnection)command.Connection!).ProcessID;
        }
        if (BeforeCommand is { } action) {
            await action(command, cancellationToken);
        }
    }
    public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
        InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) {
        await ObserveAsync(command, cancellationToken);
        return result;
    }
    public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default) {
        await ObserveAsync(command, cancellationToken);
        return result;
    }
    public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
        DbDataReader result, CancellationToken cancellationToken = default) {
        if (AfterReader is { } action) {
            await action(command, cancellationToken);
        }
        return result;
    }
}
