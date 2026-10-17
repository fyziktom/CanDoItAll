using System.Data.Common;
using System.Text.Json;
using CanDoItAll.Infrastructure.Search;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.ProjectStructure;

public sealed partial class ProjectDefinitionOperationTests {
    [Fact]
    public async Task Stored_result_cannot_advertise_a_replay_horizon_different_from_its_issued_epoch() {
        await using var app = await TestApplication.CreateAsync();
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var invocation = Invocation();
        var request = new ProjectDefinitionRequest<CreateProjectDefinitionCommand>(await NewOperationAsync(services, invocation), Version, Creation());
        var writer = services.GetRequiredService<ProjectDefinitionWriter>();
        var accepted = Assert.IsType<ProjectDefinitionOutcome<ProjectEditorAcknowledgement>.Accepted>(await writer.CreateAsync(request, invocation));
        var altered = new ProjectDefinitionAccepted<ProjectEditorAcknowledgement>(accepted.Result.Operation, accepted.Result.Version,
            accepted.Result.Kind, accepted.Result.AcceptedAtUtc, accepted.Result.ReplayUntilUtc.AddDays(1), accepted.Result.Acknowledgement);
        await using (var database = await DatabaseAsync(services)) {
            var receipt = await database.Set<ProjectDefinitionReceiptRecord>().SingleAsync();
            receipt.AcceptedPayload = Encoded(altered);
            await database.SaveChangesAsync();
        }

        var lookup = Assert.IsType<ProjectDefinitionLookup.DetailUnavailable>(await services.GetRequiredService<ProjectDefinitionReceiptReader>()
            .LookupAsync(request.Operation, invocation));
        Assert.Equal(ProjectDefinitionDetailUnavailableReason.InvalidStoredResult, lookup.Reason);
        var replay = Assert.IsType<ProjectDefinitionOutcome<ProjectEditorAcknowledgement>.Rejected>(await writer.CreateAsync(request, invocation));
        Assert.Contains(replay.Errors, error => error.Code == ProjectErrorCodes.DefinitionReceiptUnavailable);
        var current = await CurrentAsync(services, accepted.Result.Acknowledgement.Project.ProjectId);
        Assert.Equal(accepted.Result.Acknowledgement.Definition, current.Expectation);
        await using var verification = await DatabaseAsync(services);
        Assert.Single(await verification.Set<Project>().ToArrayAsync());
        Assert.Single(await verification.Set<ProjectDefinitionReceiptRecord>().ToArrayAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Concurrent_duplicate_requests_commit_one_effect_and_return_the_exact_receipt(bool replace) {
        var rendezvous = new MissingReceiptRendezvous();
        await using var app = await TestApplication.CreateAsync(WithInterceptors(rendezvous));
        await using var firstScope = app.Services.CreateAsyncScope();
        await using var secondScope = app.Services.CreateAsyncScope();
        var first = firstScope.ServiceProvider;
        var second = secondScope.ServiceProvider;
        var invocation = Invocation();
        var operation = await NewOperationAsync(first, invocation);
        var create = new ProjectDefinitionRequest<CreateProjectDefinitionCommand>(operation, Version, Creation());
        var original = replace ? await CreateOriginalAsync(first) : null;
        var replacement = original is null ? null : new ProjectDefinitionRequest<ReplaceProjectDefinitionCommand>(operation,
            Version, Replacement(original, "Accepted replacement"));
        rendezvous.Arm();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        var firstWrite = WriteAsync(first);
        var secondWrite = WriteAsync(second);
        ProjectDefinitionOutcome<ProjectEditorAcknowledgement>[] outcomes;
        try {
            await rendezvous.BothMissing.Task.WaitAsync(deadline.Token);
        } finally {
            rendezvous.Release.TrySetResult();
            outcomes = await Task.WhenAll(firstWrite, secondWrite);
        }

        var accepted = outcomes.Select(outcome => Assert.IsType<ProjectDefinitionOutcome<ProjectEditorAcknowledgement>.Accepted>(outcome)).ToArray();
        Assert.Single(accepted, outcome => !outcome.Replayed);
        Assert.Single(accepted, outcome => outcome.Replayed);
        Assert.Equal(Encoded(accepted[0].Result), Encoded(accepted[1].Result));
        await using var database = await DatabaseAsync(first);
        var receipt = Assert.Single(await database.Set<ProjectDefinitionReceiptRecord>().AsNoTracking().ToArrayAsync());
        Assert.Equal(Encoded(accepted[0].Result), receipt.AcceptedPayload);
        Assert.Single(await database.Set<Project>().AsNoTracking().ToArrayAsync());
        var current = await CurrentAsync(first, receipt.ProjectId);
        Assert.Equal(accepted[0].Result.Acknowledgement.Definition, current.Expectation);
        Assert.Equal(accepted[0].Result.Acknowledgement.Phases.Select(row => row.Id), current.Phases.Select(row => row.Id));
        Assert.Equal(accepted[0].Result.Acknowledgement.Options.Select(row => row.Id), current.Options.Select(row => row.Id));
        Assert.Equal(2, rendezvous.Contexts.Distinct().Count());

        Task<ProjectDefinitionOutcome<ProjectEditorAcknowledgement>> WriteAsync(IServiceProvider services)
            => replace ? services.GetRequiredService<ProjectDefinitionWriter>().ReplaceAsync(replacement!, invocation, deadline.Token)
                : services.GetRequiredService<ProjectDefinitionWriter>().CreateAsync(create, invocation, deadline.Token);
    }

    [Fact]
    public async Task Original_retry_after_a_later_edit_returns_historical_acknowledgement_without_replacing_current_state() {
        await using var app = await TestApplication.CreateAsync();
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var invocation = Invocation();
        var request = new ProjectDefinitionRequest<CreateProjectDefinitionCommand>(await NewOperationAsync(services, invocation), Version, Creation());
        var writer = services.GetRequiredService<ProjectDefinitionWriter>();
        var accepted = Assert.IsType<ProjectDefinitionOutcome<ProjectEditorAcknowledgement>.Accepted>(await writer.CreateAsync(request, invocation));
        var original = await CurrentAsync(services, accepted.Result.Acknowledgement.Project.ProjectId);
        var edit = new ProjectDefinitionRequest<ReplaceProjectDefinitionCommand>(await NewOperationAsync(services, invocation), Version,
            Replacement(original, "Later accepted definition"));
        var later = Assert.IsType<ProjectDefinitionOutcome<ProjectEditorAcknowledgement>.Accepted>(await writer.ReplaceAsync(edit, invocation));

        var replay = Assert.IsType<ProjectDefinitionOutcome<ProjectEditorAcknowledgement>.Accepted>(await writer.CreateAsync(request, invocation));
        var found = Assert.IsType<ProjectDefinitionLookup.FoundFull>(await services.GetRequiredService<ProjectDefinitionReceiptReader>()
            .LookupAsync(request.Operation, invocation));

        Assert.True(replay.Replayed);
        Assert.Equal(Encoded(accepted.Result), Encoded(replay.Result));
        Assert.Equal(Encoded(accepted.Result), Encoded(found.Result));
        var current = await CurrentAsync(services, original.Expectation.Project.ProjectId);
        Assert.Equal(later.Result.Acknowledgement.Definition, current.Expectation);
        Assert.Equal("Later accepted definition", current.Scalars.Name);
        await using var database = await DatabaseAsync(services);
        Assert.Equal(2, await database.Set<ProjectDefinitionReceiptRecord>().CountAsync());
    }

    [Fact]
    public async Task Reusing_an_accepted_address_for_changed_intent_rejects_without_another_effect() {
        await using var app = await TestApplication.CreateAsync();
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var invocation = Invocation();
        var operation = await NewOperationAsync(services, invocation);
        var writer = services.GetRequiredService<ProjectDefinitionWriter>();
        var original = Creation();
        Assert.IsType<ProjectDefinitionOutcome<ProjectEditorAcknowledgement>.Accepted>(await writer.CreateAsync(new(operation, Version, original), invocation));
        var before = await StateAsync(services);
        var changed = new CreateProjectDefinitionCommand(original.Scalars with { Name = "Different intent" }, original.Phases, original.Options, null);

        var rejected = Assert.IsType<ProjectDefinitionOutcome<ProjectEditorAcknowledgement>.Rejected>(
            await writer.CreateAsync(new(operation, Version, changed), invocation));

        Assert.Contains(rejected.Errors, error => error.Code == ProjectErrorCodes.DefinitionOperationMismatch);
        Assert.Equal(before, await StateAsync(services));
    }

    [Fact]
    public async Task Full_and_scalar_no_op_receipts_preserve_children_and_do_not_retoken_the_root_during_receipt_flush() {
        var writes = new RootUpdateCounter();
        await using var app = await TestApplication.CreateAsync(WithInterceptors(writes));
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var invocation = Invocation();
        var original = await CreateOriginalAsync(services);
        var writer = services.GetRequiredService<ProjectDefinitionWriter>();
        writes.Armed = true;
        var full = Assert.IsType<ProjectDefinitionOutcome<ProjectEditorAcknowledgement>.Accepted>(await writer.ReplaceAsync(
            new(await NewOperationAsync(services, invocation), Version, Replacement(original, original.Scalars.Name)), invocation));
        var afterFull = await CurrentAsync(services, original.Expectation.Project.ProjectId);
        var scalar = Assert.IsType<ProjectDefinitionOutcome<ProjectDefinitionScalarsAcknowledgement>.Accepted>(await writer.UpdateScalarsAsync(
            new(await NewOperationAsync(services, invocation), Version, new(afterFull.Expectation, afterFull.Scalars)), invocation));
        var current = await CurrentAsync(services, original.Expectation.Project.ProjectId);

        Assert.NotEqual(original.Expectation.Revision, full.Result.Acknowledgement.Definition.Revision);
        Assert.Equal(full.Result.Acknowledgement.Definition, afterFull.Expectation);
        Assert.NotEqual(afterFull.Expectation.Revision, scalar.Result.Acknowledgement.Definition.Revision);
        Assert.Equal(scalar.Result.Acknowledgement.Definition, current.Expectation);
        Assert.Equal(original.Phases, current.Phases);
        Assert.Equal(original.Options, current.Options);
        Assert.Equal(2, writes.Count);
        await using var database = await DatabaseAsync(services);
        var receipts = await database.Set<ProjectDefinitionReceiptRecord>().AsNoTracking().ToArrayAsync();
        Assert.Equal(2, receipts.Length);
        Assert.Contains(receipts, row => row.AcceptedRevision == full.Result.Acknowledgement.Definition.Revision && row.AcceptedPayload.SequenceEqual(Encoded(full.Result)));
        Assert.Contains(receipts, row => row.AcceptedRevision == scalar.Result.Acknowledgement.Definition.Revision &&
            row.AcceptedPayload.SequenceEqual(ProjectDefinitionOperationCodec.EncodeAccepted(scalar.Result, Limits).ToArray()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Required_search_failure_rolls_back_root_children_and_receipt(bool replace) {
        var failure = new SearchWriteFailure();
        await using var app = await TestApplication.CreateAsync(new TestHarnessOptions {
            ConfigureServices = services => services.ConfigureDbContext<SearchDbContext>(options => options.AddInterceptors(failure), ServiceLifetime.Singleton)
        });
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var original = replace ? await CreateOriginalAsync(services) : null;
        var before = await StateAsync(services);
        var invocation = Invocation();
        var operation = await NewOperationAsync(services, invocation);
        failure.Armed = true;
        var writer = services.GetRequiredService<ProjectDefinitionWriter>();

        var changedChildren = original is null ? null : new ReplaceProjectDefinitionCommand(original.Expectation,
            original.Scalars with { Name = "Must roll back" },
            [new(original.Phases[0].Id, "Changed phase", "Changed goal", default, null, null),
                new(null, "New phase", "New goal", default, null, null)], [], null);
        var outcome = replace
            ? await writer.ReplaceAsync(new(operation, Version, changedChildren!), invocation)
            : await writer.CreateAsync(new(operation, Version, Creation()), invocation);

        Assert.IsType<ProjectDefinitionOutcome<ProjectEditorAcknowledgement>.Rejected>(outcome);
        Assert.True(failure.Reached);
        Assert.Equal(before, await StateAsync(services));
        Assert.IsType<ProjectDefinitionLookup.NotObserved>(await services.GetRequiredService<ProjectDefinitionReceiptReader>().LookupAsync(operation, invocation));
    }

    [Fact]
    public async Task Current_read_revocation_hides_receipt_detail_and_cannot_reexecute_the_accepted_command() {
        await using var app = await TestApplication.CreateAsync();
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var access = new ReceiptAccess();
        var invocation = Invocation(access);
        var request = new ProjectDefinitionRequest<CreateProjectDefinitionCommand>(await NewOperationAsync(services, invocation), Version, Creation());
        var writer = services.GetRequiredService<ProjectDefinitionWriter>();
        Assert.IsType<ProjectDefinitionOutcome<ProjectEditorAcknowledgement>.Accepted>(await writer.CreateAsync(request, invocation));
        var before = await StateAsync(services);
        access.Allowed = false;

        var lookup = Assert.IsType<ProjectDefinitionLookup.DetailUnavailable>(await services.GetRequiredService<ProjectDefinitionReceiptReader>()
            .LookupAsync(request.Operation, invocation));
        var retry = Assert.IsType<ProjectDefinitionOutcome<ProjectEditorAcknowledgement>.Rejected>(await writer.CreateAsync(request, invocation));

        Assert.Equal(ProjectDefinitionDetailUnavailableReason.AccessDenied, lookup.Reason);
        Assert.Contains(retry.Errors, error => error.Code == ProjectErrorCodes.DefinitionReceiptUnavailable);
        Assert.Equal(before, await StateAsync(services));
    }

    [Theory]
    [InlineData(InvalidContext.Namespace)]
    [InlineData(InvalidContext.Caller)]
    [InlineData(InvalidContext.Version)]
    [InlineData(InvalidContext.FutureEpoch)]
    [InlineData(InvalidContext.DrainingEpoch)]
    public async Task Invalid_operation_context_cannot_create_any_owner_or_receipt(InvalidContext invalid) {
        await using var app = await TestApplication.CreateAsync();
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var invocation = Invocation();
        var issued = await NewOperationAsync(services, invocation);
        ProjectDefinitionOperation operation = invalid switch {
            InvalidContext.Namespace => new(new DatasetNamespaceId(Guid.NewGuid()), issued.Epoch, issued.Id, issued.CallerBinding),
            InvalidContext.Caller => new(issued.Namespace, issued.Epoch, issued.Id, ProjectMutationCallerScope.OpenApi.Bind(issued.Namespace)),
            InvalidContext.FutureEpoch => new(issued.Namespace, new(issued.Epoch.Value + 1), issued.Id, issued.CallerBinding),
            _ => issued
        };
        if (invalid == InvalidContext.DrainingEpoch) {
            await using var database = await DatabaseAsync(services);
            await database.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE "Infrastructure_FiniteOperationEpochs" SET "OpenedAtUtc" = clock_timestamp() - interval '2 hours',
                    "AdmissionClosesAtUtc" = clock_timestamp() - interval '1 hour'
                WHERE "NamespaceId" = {issued.Namespace.Value} AND "PoolId" = {ProjectFiniteOperationPool.Pool.Value}
                    AND "Epoch" = {issued.Epoch.Value}
                """);
        }
        var expectedCode = invalid switch {
            InvalidContext.Namespace => ProjectErrorCodes.DefinitionDatasetChanged,
            InvalidContext.Caller => ProjectErrorCodes.DefinitionCallerChanged,
            InvalidContext.Version => ProjectErrorCodes.DefinitionVersionUnsupported,
            InvalidContext.FutureEpoch => ProjectErrorCodes.DefinitionOperationUnknown,
            _ => ProjectErrorCodes.DefinitionOperationClosed
        };
        var before = await StateAsync(services);

        var rejected = Assert.IsType<ProjectDefinitionOutcome<ProjectEditorAcknowledgement>.Rejected>(await services.GetRequiredService<ProjectDefinitionWriter>()
            .CreateAsync(new(operation, invalid == InvalidContext.Version ? new(2, 1) : Version, Creation()), invocation));

        Assert.Contains(rejected.Errors, error => error.Code == expectedCode);
        Assert.Equal(before, await StateAsync(services));
    }

    public enum InvalidContext { Namespace, Caller, Version, FutureEpoch, DrainingEpoch }
    private static ProjectDefinitionRequestVersion Version => ProjectDefinitionRequestVersion.VersionOne;
    private static ProjectDefinitionLimits Limits => ProjectDefinitionLimits.VersionOneMaximum;
    private static ProjectDefinitionInvocation Invocation(ReceiptAccess? access = null) => new(ProjectMutationCallerScope.LocalWorkspace, access ?? new());
    private static byte[] Encoded(ProjectDefinitionAccepted<ProjectEditorAcknowledgement> accepted) => ProjectDefinitionOperationCodec.EncodeAccepted(accepted, Limits).ToArray();
    private static TestHarnessOptions WithInterceptors(params IInterceptor[] interceptors) => new() {
        ConfigureServices = services => services.ConfigureDbContext<ProjectsDbContext>(options => options.AddInterceptors(interceptors), ServiceLifetime.Singleton)
    };

    private static async Task<ProjectDefinitionOperation> NewOperationAsync(IServiceProvider services, ProjectDefinitionInvocation invocation) {
        var context = await services.GetRequiredService<ProjectDefinitionOperationProtocol>().GetWriteContextAsync(invocation.Caller);
        return new(context.Namespace, context.Epoch, Guid.NewGuid(), context.CallerBinding);
    }

    private static CreateProjectDefinitionCommand Creation() => new(
        new("Protected project", "Description", "Objective", default, "Planning", null),
        [new(null, "Phase", "Goal", default, null, null)], [new(null, ProjectOptionCategory.Language, "C#", "Notes")], null);

    private static ReplaceProjectDefinitionCommand Replacement(ProjectDefinitionSnapshot original, string name) => new(original.Expectation,
        original.Scalars with { Name = name }, original.Phases.Select(row => new ProjectDefinitionPhaseInput(row.Id, row.Name, row.Goal,
            row.Status, row.StartDateUtc, row.EndDateUtc)).ToArray(), original.Options.Select(row => new ProjectDefinitionOptionInput(row.Id,
            row.Category, row.OptionName, row.Notes)).ToArray(), null);

    private static async Task<ProjectDefinitionSnapshot> CreateOriginalAsync(IServiceProvider services) {
        var result = await services.GetRequiredService<ProjectsService>().SaveAsync(new ProjectEditorModel {
            Name = "Original project", Objective = "Original objective", Phases = [new() { Name = "Phase", Goal = "Goal" }],
            Options = [new() { Category = ProjectOptionCategory.Language, OptionName = "C#", Notes = "Notes" }]
        });
        Assert.True(result.IsSuccess);
        return await CurrentAsync(services, result.Value);
    }

    private static async Task<ProjectDefinitionSnapshot> CurrentAsync(IServiceProvider services, Guid id) {
        var result = await services.GetRequiredService<ProjectsService>().GetDefinitionAsync(id);
        Assert.True(result.IsSuccess);
        return result.Value!;
    }

    private static Task<ProjectsDbContext> DatabaseAsync(IServiceProvider services)
        => services.GetRequiredService<IDbContextFactory<ProjectsDbContext>>().CreateDbContextAsync();

    private static async Task<string> StateAsync(IServiceProvider services) {
        await using var database = await DatabaseAsync(services);
        var projects = await database.Set<Project>().AsNoTracking().OrderBy(row => row.Id).Select(row => row.Id).ToArrayAsync();
        var definitions = new List<ProjectDefinitionSnapshot>();
        foreach (var id in projects) {
            definitions.Add(await CurrentAsync(services, id));
        }
        await using var search = await services.GetRequiredService<IDbContextFactory<SearchDbContext>>().CreateDbContextAsync();
        return JsonSerializer.Serialize(new {
            Definitions = definitions,
            Phases = await database.Set<ProjectPhase>().AsNoTracking().OrderBy(row => row.Id).ToArrayAsync(),
            Options = await database.Set<ProjectOptionSelection>().AsNoTracking().OrderBy(row => row.Id).ToArrayAsync(),
            Receipts = await database.Set<ProjectDefinitionReceiptRecord>().AsNoTracking().OrderBy(row => row.OperationId).ToArrayAsync(),
            Search = await search.Set<SearchDocument>().AsNoTracking().OrderBy(row => row.Id).ToArrayAsync()
        });
    }

    private sealed class ReceiptAccess : IProjectDefinitionReceiptAccess {
        public bool Allowed { get; set; } = true;
        public Task<bool> CanReadAsync(ProjectWriteAdmission target, CancellationToken cancellationToken) => Task.FromResult(Allowed);
    }

    private sealed class MissingReceiptRendezvous : DbCommandInterceptor {
        private int remaining;
        public List<Guid> Contexts { get; } = [];
        public TaskCompletionSource BothMissing { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public void Arm() => remaining = 2;

        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
            DbDataReader result, CancellationToken cancellationToken = default) {
            if (command.CommandText.Contains("AS \"ActiveNamespaceId\"", StringComparison.Ordinal) && Volatile.Read(ref remaining) > 0) {
                var left = Interlocked.Decrement(ref remaining);
                if (left >= 0) {
                    lock (Contexts) {
                        Contexts.Add(eventData.Context!.ContextId.InstanceId);
                    }
                    if (left == 0) {
                        BothMissing.TrySetResult();
                    }
                    await Release.Task.WaitAsync(cancellationToken);
                }
            }
            return result;
        }
    }

    private sealed class RootUpdateCounter : DbCommandInterceptor {
        public bool Armed { get; set; }
        public int Count { get; private set; }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) {
            if (Armed && command.CommandText.Contains("UPDATE \"Projects_Projects\"", StringComparison.Ordinal)) {
                Count++;
            }
            return ValueTask.FromResult(result);
        }
    }

    private sealed class SearchWriteFailure : DbCommandInterceptor {
        public bool Armed { get; set; }
        public bool Reached { get; private set; }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) {
            Fail(command);
            return ValueTask.FromResult(result);
        }
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default) {
            Fail(command);
            return ValueTask.FromResult(result);
        }
        private void Fail(DbCommand command) {
            if (Armed && (command.CommandText.Contains("INSERT INTO \"Infrastructure_SearchDocuments\"", StringComparison.Ordinal) ||
                command.CommandText.Contains("UPDATE \"Infrastructure_SearchDocuments\"", StringComparison.Ordinal))) {
                Armed = false;
                Reached = true;
                throw new InvalidOperationException("Owned required-search SQL failure.");
            }
        }
    }
}
