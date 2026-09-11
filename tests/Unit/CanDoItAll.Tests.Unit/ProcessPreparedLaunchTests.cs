using System.Text.Json;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Builder;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Templates;
using CanDoItAll.SharedKernel;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CanDoItAll.Tests.Unit.Processes;

public sealed class ProcessPreparedLaunchTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Public_request_json_cannot_supply_authority_or_project_and_link_scope(bool inject) {
        var project = new ProcessProjectAdmission(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var authority = ProcessPreparedLaunchFixture.Local(project.DatabaseProfileId, project);
        var prepared = ProcessPreparedLaunchFixture.Create(authority, new(Guid.NewGuid()), new(project.ProjectId, "node", "binding"));
        var json = JsonSerializer.Serialize(prepared.Request);
        using var document = JsonDocument.Parse(json);
        Assert.False(document.RootElement.TryGetProperty(nameof(ProcessLaunchRequest.Authority), out _));
        Assert.False(document.RootElement.TryGetProperty(nameof(ProcessLaunchRequest.ProjectAdmission), out _));
        Assert.False(document.RootElement.TryGetProperty(nameof(ProcessLaunchRequest.LinkTarget), out _));
        if (inject) {
            json = json[..^1] + ",\"Authority\":" + JsonSerializer.Serialize(authority) +
                ",\"ProjectAdmission\":" + JsonSerializer.Serialize(project) +
                ",\"LinkTarget\":{\"ProjectId\":\"" + project.ProjectId + "\",\"SourceNodeKey\":\"forged\",\"SourceBindingFingerprint\":\"forged\"}}";
        }
        var read = Assert.IsType<ProcessLaunchRequest>(JsonSerializer.Deserialize<ProcessLaunchRequest>(json));
        Assert.Null(read.Authority);
        Assert.Null(read.ProjectAdmission);
        Assert.Null(read.LinkTarget);
    }

    [Fact]
    public void Request_fingerprint_ignores_dictionary_order_execute_choice_and_refreshed_expiry_but_binds_semantic_target() {
        var profile = Guid.NewGuid();
        var authority = new ProcessLaunchAuthority(
            new ProcessLaunchPrincipal.AuthenticatedOperator("operator", ProcessProjectAdmissionFixture.Now.AddHours(1)),
            profile, null, true, true, "policy");
        var first = ProcessPreparedLaunchFixture.Create(authority, new(Guid.NewGuid())).Request with {
            Variables = new Dictionary<string, string> { ["b"] = "2", ["a"] = "1" }
        };
        var reordered = first with {
            Execute = true,
            Variables = new Dictionary<string, string> { ["a"] = "1", ["b"] = "2" },
            Authority = authority with { Principal = new ProcessLaunchPrincipal.AuthenticatedOperator("operator", ProcessProjectAdmissionFixture.Now.AddHours(2)) }
        };
        Assert.Equal(ProcessLaunchIntentFingerprint.Compute(first), ProcessLaunchIntentFingerprint.Compute(reordered));
        Assert.NotEqual(ProcessLaunchIntentFingerprint.Compute(first), ProcessLaunchIntentFingerprint.Compute(first with { ProjectNodeId = "different-node" }));
        Assert.NotEqual(ProcessLaunchIntentFingerprint.Compute(first), ProcessLaunchIntentFingerprint.Compute(first with {
            Authority = authority with { Principal = new ProcessLaunchPrincipal.AuthenticatedOperator("another-operator", ProcessProjectAdmissionFixture.Now.AddHours(1)) }
        }));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Prepared_review_round_trip_retains_exact_plan_and_canonical_microsecond_timestamp(bool includeAuthority) {
        var storeName = Guid.NewGuid().ToString("N");
        var options = new DbContextOptionsBuilder<ProcessPersistenceDbContext>().UseInMemoryDatabase(storeName, new InMemoryDatabaseRoot()).Options;
        var authority = includeAuthority ? ProcessPreparedLaunchFixture.Local(Guid.NewGuid()) : null;
        var prepared = ProcessPreparedLaunchFixture.Create(authority, includeAuthority ? new(Guid.NewGuid()) : null) with {
            PreparedAtUtc = ProcessProjectAdmissionFixture.Now.AddTicks(17)
        };
        var store = new EfProcessPreparedLaunchStore(new Factory(options), options);
        var saved = await store.PrepareAsync(prepared);
        var restarted = new EfProcessPreparedLaunchStore(new Factory(options), options);
        var reloaded = Assert.IsType<ProcessPreparedLaunchSnapshot>(await restarted.GetAsync(saved.Preparation.AdmissionId));
        Assert.Equal(saved.PreparationFingerprint, reloaded.PreparationFingerprint);
        Assert.Equal(prepared.InitialCommit.Mutation.State.RunId, reloaded.Preparation.InitialCommit.Mutation.State.RunId);
        Assert.Equal(prepared.Review.PlanHash, reloaded.Preparation.Review.PlanHash);
        Assert.Equal(ProcessProjectAdmissionFixture.Now.AddTicks(10), reloaded.Preparation.PreparedAtUtc);
        Assert.Equal(authority, reloaded.Preparation.Authority);
        Assert.Equal(prepared.InitialCommit.InitialAssignments!.Single().ReadinessHash,
            reloaded.Preparation.InitialCommit.InitialAssignments!.Single().ReadinessHash);
        Assert.Null(reloaded.AcceptedAtUtc);
    }

    [Fact]
    public async Task Querying_prepared_launch_status_has_no_acceptance_projection_or_continuation_side_effect() {
        var options = new DbContextOptionsBuilder<ProcessPersistenceDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options;
        var store = new EfProcessPreparedLaunchStore(new Factory(options), options);
        var authority = ProcessPreparedLaunchFixture.Local(Guid.NewGuid());
        var saved = await store.PrepareAsync(ProcessPreparedLaunchFixture.Create(authority, new(Guid.NewGuid())));
        var service = new ProcessLaunchApplicationService(null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!,
            preparedLaunchStore: store);
        var status = await service.GetLaunchStatusAsync(saved.Preparation.AdmissionId, authority);
        Assert.Null(status.AcceptedRunId);
        Assert.Equal(ProcessLaunchContinuationState.Prepared, status.ContinuationState);
        await using var context = new ProcessPersistenceDbContext(options);
        Assert.Empty(await context.RuntimeStates.ToArrayAsync());
        Assert.Empty(await context.InstancePlans.ToArrayAsync());
        var retained = await context.PreparedLaunches.SingleAsync(item => item.Id == saved.Preparation.AdmissionId.Value);
        Assert.Null(retained.ContinuationOwner);
        Assert.Equal(0, retained.ContinuationGeneration);
        Assert.Null(retained.AcceptedAtUtc);
    }

    [Fact]
    public async Task New_explicit_intent_requires_a_durable_owner_store_for_preview_and_launch() {
        var service = new ProcessLaunchApplicationService(null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!);
        var request = ProcessPreparedLaunchFixture.Create(ProcessPreparedLaunchFixture.Local(Guid.NewGuid()), new(Guid.NewGuid())).Request;
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.LaunchAsync(request));
        Assert.Contains("durable owner store", error.Message, StringComparison.Ordinal);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.PreviewAsync(request));
    }

    [Fact]
    public async Task Ordinary_launch_without_caller_intent_prepares_a_distinct_intentional_run_on_every_call() {
        var options = new DbContextOptionsBuilder<ProcessPersistenceDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options;
        var store = new EfProcessPreparedLaunchStore(new Factory(options), options);
        var resolver = new AllStepsExecutorResolver();
        var unitOfWork = new RejectingUnitOfWork();
        var service = CompilerService(store, resolver, unitOfWork);
        var first = await service.LaunchAsync(CompilerRequest());
        var second = await service.LaunchAsync(CompilerRequest());
        Assert.Equal(ProcessLaunchStage.Failed, first.Stage);
        Assert.Equal(ProcessLaunchStage.Failed, second.Stage);
        Assert.NotEqual(first.Observation!.AdmissionId, second.Observation!.AdmissionId);
        Assert.Equal(2, resolver.Calls);
        await using var context = new ProcessPersistenceDbContext(options);
        var prepared = await context.PreparedLaunches.ToArrayAsync();
        Assert.Equal(2, prepared.Length);
        Assert.All(prepared, item => Assert.Null(item.CallerIntentId));
        Assert.Equal(2, prepared.Select(item => item.RunId).Distinct().Count());
        Assert.Empty(await context.RuntimeStates.ToArrayAsync());
    }

    [Fact]
    public async Task Launch_uses_the_exact_reviewed_plan_and_overrides_without_compiling_again() {
        var options = new DbContextOptionsBuilder<ProcessPersistenceDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options;
        var store = new EfProcessPreparedLaunchStore(new Factory(options), options);
        var resolver = new AllStepsExecutorResolver();
        var unitOfWork = new RejectingUnitOfWork();
        var service = CompilerService(store, resolver, unitOfWork);
        var request = CompilerRequest() with {
            CallerIntentId = new ProcessLaunchIntentId(Guid.NewGuid()),
            Authority = ProcessPreparedLaunchFixture.Local(Guid.NewGuid())
        };
        var preview = await service.PreviewAsync(request);
        Assert.Equal(ProcessLaunchStage.Planned, preview.Stage);
        var result = await service.LaunchAsync(request with { PreparedAdmissionId = preview.Observation!.AdmissionId, Execute = true });
        Assert.Equal(ProcessLaunchStage.Failed, result.Stage);
        Assert.Equal(preview.LaunchPlanId, result.LaunchPlanId);
        Assert.Equal(preview.LaunchPlan.PlanHash, result.LaunchPlan.PlanHash);
        Assert.Equal(1, resolver.Calls);
        var committed = Assert.IsType<ProcessRuntimeCommitRequest>(unitOfWork.Request);
        Assert.Equal(preview.Observation.AdmissionId, committed.InitialLaunchAdmission!.AdmissionId);
        Assert.True(committed.InitialLaunchAdmission.Execute);
        Assert.Equal(preview.LaunchPlan.PlanHash, committed.InitialPlan!.PlanHash);
        await Assert.ThrowsAsync<ProcessLaunchIntentConflictException>(() => service.LaunchAsync(request with {
            PreparedAdmissionId = preview.Observation.AdmissionId,
            Variables = new Dictionary<string, string> { ["Topic"] = "Changed review" }
        }));
        Assert.Equal(1, resolver.Calls);
    }

    public enum PrincipalKind {
        Local,
        Authenticated,
        Agent
    }

    [Theory]
    [InlineData(PrincipalKind.Local)]
    [InlineData(PrincipalKind.Authenticated)]
    [InlineData(PrincipalKind.Agent)]
    public async Task Saved_authority_preserves_explicit_principal_kind_and_every_original_ceiling(PrincipalKind kind) {
        var profile = Guid.NewGuid();
        ProcessLaunchPrincipal principal = kind switch {
            PrincipalKind.Local => new ProcessLaunchPrincipal.LocalOperator(ProcessLaunchOperatorSurface.Api),
            PrincipalKind.Authenticated => new ProcessLaunchPrincipal.AuthenticatedOperator("operator-id", ProcessProjectAdmissionFixture.Now.AddHours(1)),
            PrincipalKind.Agent => new ProcessLaunchPrincipal.AgentExecution(new(Guid.NewGuid(), Guid.NewGuid(), 3,
                ProcessLaunchSourceScopeKind.Organization, profile.ToString("N"), true, true, "fixture-v1", "sha256:fixture",
                ["execute-process"], ["fixture-capability"], ["write-alias"], ["read-alias"], ["artifact://fixture"])),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        var authority = new ProcessLaunchAuthority(principal, profile, null, true, true, "fixture-policy");
        var options = new DbContextOptionsBuilder<ProcessPersistenceDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options;
        var store = new EfProcessPreparedLaunchStore(new Factory(options), options);
        var saved = await store.PrepareAsync(ProcessPreparedLaunchFixture.Create(authority, new(Guid.NewGuid())));
        var reloaded = (await store.GetAsync(saved.Preparation.AdmissionId))!;
        Assert.Equal(principal.GetType(), reloaded.Preparation.Authority!.Principal.GetType());
        Assert.Equal(JsonSerializer.Serialize(authority), JsonSerializer.Serialize(reloaded.Preparation.Authority));
    }

    [Fact]
    public async Task Empty_default_intent_is_rejected_instead_of_becoming_a_shared_compatibility_key() {
        var options = new DbContextOptionsBuilder<ProcessPersistenceDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options;
        var store = new EfProcessPreparedLaunchStore(new Factory(options), options);
        var prepared = ProcessPreparedLaunchFixture.Create(ProcessPreparedLaunchFixture.Local(Guid.NewGuid()), default(ProcessLaunchIntentId));
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.PrepareAsync(prepared));
        await using var context = new ProcessPersistenceDbContext(options);
        Assert.Empty(await context.PreparedLaunches.ToArrayAsync());
    }

    private static ProcessLaunchRequest CompilerRequest()
        => new("dotnet-runtime-command-writeback", null, null, null, null, "prepared-launch-test",
            new Dictionary<string, string>(), false, false);

    private static ProcessLaunchApplicationService CompilerService(IProcessPreparedLaunchStore store,
        AllStepsExecutorResolver resolver, IProcessRuntimeUnitOfWork unitOfWork)
        => new(new ProcessTemplatePackLoader(), new TestClock(), new TestDriverCatalogProvider(), resolver,
            null!, unitOfWork, null!, null!, null!, new GenericProcessStepBriefBuilder(), null!, null!,
            new LaunchVariableTemplateResolver(), TestExternalTargetPathRegistry.Create(), store);

    private sealed class Factory(DbContextOptions<ProcessPersistenceDbContext> options) : IDbContextFactory<ProcessPersistenceDbContext> {
        public ProcessPersistenceDbContext CreateDbContext() => new(options);
    }    private sealed class TestClock : IProcessProjectionClock {
        public DateTimeOffset GetUtcNow() => ProcessProjectAdmissionFixture.Now;
    }

    private sealed class TestDriverCatalogProvider : IProcessLaunchDriverCatalogProvider {
        private static readonly StrategyId ExecutionStrategyId =
            new("strategy.atomic-launch.execute");
        private static readonly IReadOnlySet<CapabilityTag> Capabilities =
            new HashSet<CapabilityTag> {
                new("capability.atomic-launch.execution")
            };

        public ValueTask<ProcessLaunchDriverCatalog> LoadAsync(
            CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();
            var descriptor = new ProcessDriverDescriptor(
                new DriverId("driver.atomic-launch"),
                "Atomic launch test driver",
                "1.0.0",
                "runtime/1.0",
                "runtime/1.0",
                ProcessDriverLayer.Framework,
                Capabilities,
                [],
                [],
                [],
                [
                    new ProcessStrategyDescriptor(
                        ExecutionStrategyId,
                        "1.0.0",
                        ProcessStrategyKind.StepExecution,
                        Capabilities)
                ]);
            var catalog = new ProcessDriverCatalog(
                [new ProcessDriverPackage(descriptor, [], [], [], [], [], [])]);
            return ValueTask.FromResult(new ProcessLaunchDriverCatalog(
                catalog,
                ExecutionStrategyId,
                Capabilities));
        }
    }

    private sealed class AllStepsExecutorResolver(
        ProcessHostCapabilityId? effectiveHostCapability = null,
        ProcessHostCapabilitySnapshot? hostCapabilities = null) : IProcessLaunchExecutorResolver {
        public int Calls { get; private set; }

        public ValueTask<ProcessLaunchExecutorResolution> ResolveAsync(
            ProcessLaunchExecutorResolutionRequest request,
            CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();
            Calls++;
            var templateSteps = request.Definition.Steps.ToDictionary(
                step => step.Key,
                StringComparer.OrdinalIgnoreCase);
            var bindings = request.Plan.Steps
                .Where(step => step.IsExecutable)
                .Select(step => {
                    templateSteps.TryGetValue(step.StepKey, out var templateStep);
                    var roleKey = templateStep?.RoleAssignments
                        .OrderBy(assignment => assignment.FallbackOrder)
                        .Select(assignment => assignment.RoleKey)
                        .FirstOrDefault(role => !string.IsNullOrWhiteSpace(role))
                        ?? "unit-test-role";
                    return new ProcessLaunchExecutorBinding(
                        step.StepKey,
                        roleKey,
                        ProcessLaunchExecutorKinds.Agent,
                        "unit-test-executor",
                        "Unit test executor",
                        "sha256:unit-test-readiness",
                        "Resolved by the focused launch test.");
                })
                .ToArray();
            var effectiveHostCapabilitiesByStep = effectiveHostCapability is { } capability
                ? request.Plan.Steps
                    .Where(step => step.IsExecutable)
                    .ToDictionary(
                        step => step.StepKey,
                        _ => (IReadOnlySet<ProcessHostCapabilityId>)new HashSet<ProcessHostCapabilityId> { capability },
                        StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, IReadOnlySet<ProcessHostCapabilityId>>(StringComparer.OrdinalIgnoreCase);
            return ValueTask.FromResult(new ProcessLaunchExecutorResolution(bindings, []) {
                EffectiveHostCapabilitiesByStep = effectiveHostCapabilitiesByStep,
                HostCapabilities = hostCapabilities
            });
        }
    }

    private sealed class RejectingUnitOfWork : IProcessRuntimeUnitOfWork {
        public ProcessRuntimeCommitRequest? Request { get; private set; }

        public Task<ProcessRuntimeCommitResult> CommitAsync(
            ProcessRuntimeCommitRequest request,
            CancellationToken cancellationToken = default) {
            Request = request;
            return Task.FromResult(ProcessRuntimeCommitResult.FromMutation(
                ProcessRuntimeMutation.Rejected(
                    request.Mutation.State,
                    "Runtime.TestRejected",
                    "The focused test stops after the initial commit request.")));
        }
    }


}
