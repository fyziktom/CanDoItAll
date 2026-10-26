using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Tests.Unit;

public sealed class FiniteOperationContractTests {
    private static readonly DateTimeOffset Now = new(2026, 10, 16, 12, 0, 0, TimeSpan.Zero);
    private static readonly DatasetActivationBinding Binding = new(new(Guid.NewGuid()), Guid.NewGuid(),
        DatasetLifecycleRecord.CurrentSchemaVersion, DatasetGuardProtocol.SharedRows);
    private static readonly FiniteOperationPoolRegistration Projects = ProjectFiniteOperationPool.Create(new());

    [Fact]
    public void Stable_pool_and_family_identities_are_typed_nonempty_and_registration_captures_the_family_set() {
        Assert.Equal(new Guid("e6eaf1c1-5dbc-46bf-8641-1ae9fe3ae376"), Projects.Pool.Value);
        Assert.Equal(new[] { new Guid("574607ce-cb66-4783-9675-8a59d4383ab4"), new Guid("10547738-2635-4baf-9ef0-f0fdd49c4b8a") },
            Projects.Families.Select(family => family.Family.Value));
        Assert.Throws<ArgumentOutOfRangeException>(() => new FiniteOperationPoolId(Guid.Empty));
        Assert.Throws<ArgumentOutOfRangeException>(() => new FiniteReceiptFamilyId(Guid.Empty));
        var original = Projects.Families.Single(family => family.Family == ProjectFiniteOperationPool.DefinitionFamily);
        var families = new[] { original };
        var captured = new FiniteOperationPoolRegistration(Projects.Pool, 1, Projects.Timing, families);
        families[0] = new(new(Guid.NewGuid()), 1);
        Assert.Equal(original, captured.Families.Single());
        Assert.Throws<FiniteOperationProtocolException>(() => new FiniteOperationPoolRegistration(Projects.Pool, 1, Projects.Timing, []));
        Assert.Throws<FiniteOperationProtocolException>(() => new FiniteOperationPoolRegistration(Projects.Pool, 1, Projects.Timing, [original, original]));
        Assert.Throws<ArgumentOutOfRangeException>(() => new FiniteOperationPoolRegistration(Projects.Pool, 0, Projects.Timing, [original]));
    }

    [Fact]
    public void Timing_and_batch_limits_preserve_finite_promises_and_bound_each_family_transaction() {
        var timing = Projects.Timing;
        Assert.Equal(TimeSpan.FromDays(7), timing.ReplayRetention);
        Assert.Equal(TimeSpan.FromSeconds(60), timing.MaintenanceInterval);
        Assert.Throws<ArgumentOutOfRangeException>(() => new FiniteOperationTiming(timing.AdmissionWindow, timing.ReplayRetention,
            timing.MutationDeadline, timing.StatementDeadline, timing.LockDeadline, TimeSpan.FromSeconds(4), timing.WarningAfter));
        Assert.Throws<ArgumentOutOfRangeException>(() => new FiniteOperationTiming(timing.AdmissionWindow, timing.ReplayRetention,
            timing.MutationDeadline, timing.StatementDeadline, timing.MutationDeadline + TimeSpan.FromSeconds(1),
            timing.MaintenanceInterval, timing.WarningAfter));
        Assert.Throws<ArgumentOutOfRangeException>(() => new FiniteReceiptPurgeRequest(Binding, Projects,
            ProjectFiniteOperationPool.DefinitionFamily, maximumRows: 129));
        Assert.Throws<ArgumentOutOfRangeException>(() => new FiniteReceiptPurgeRequest(Binding, Projects,
            ProjectFiniteOperationPool.DefinitionFamily, maximumBytes: 1));
        Assert.Equal(128, new FiniteReceiptPurgeRequest(Binding, Projects, ProjectFiniteOperationPool.DefinitionFamily).MaximumRows);
        Assert.Equal(16 * 1024 * 1024, FiniteOperationBounds.BatchBytes);
    }

    [Fact]
    public async Task Installed_receipt_families_require_exactly_one_participant_before_any_database_work() {
        var participant = new NeverExecuteParticipant(Projects.Pool, ProjectFiniteOperationPool.DefinitionFamily);
        var lifecycle = new NeverExecuteParticipant(Projects.Pool, ProjectFiniteOperationPool.LifecycleFamily);
        _ = Maintenance([Projects], [participant, lifecycle]);
        Assert.Throws<FiniteOperationProtocolException>(() => Maintenance([Projects], []));
        Assert.Throws<FiniteOperationProtocolException>(() => Maintenance([Projects], [participant]));
        Assert.Throws<FiniteOperationProtocolException>(() => Maintenance([Projects], [lifecycle]));
        Assert.Throws<FiniteOperationProtocolException>(() => Maintenance([Projects], [participant, lifecycle, participant]));
        Assert.Throws<FiniteOperationProtocolException>(() => Maintenance([Projects], [participant, lifecycle, lifecycle]));
        Assert.Throws<InvalidOperationException>(() => Maintenance([Projects, Projects], [participant]));
        Assert.Throws<InvalidOperationException>(() => Maintenance([Projects], [participant, lifecycle,
            new NeverExecuteParticipant(new(Guid.NewGuid()), new(Guid.NewGuid()))]));
        Assert.Throws<FiniteOperationProtocolException>(() => Projects.RequireFamily(new(Guid.NewGuid())));
        var services = new ServiceCollection();
        services.AddProjectsModule();
        var registrations = services.Where(descriptor => descriptor.ServiceType == typeof(IFiniteOperationReceiptRetentionParticipant)).ToArray();
        Assert.Equal(new[] { typeof(ProjectDefinitionReceiptRetention), typeof(ProjectLifecycleReceiptRetention) },
            registrations.Select(registration => registration.ImplementationType));
        Assert.All(registrations, registration => Assert.Equal(ServiceLifetime.Singleton, registration.Lifetime));
        await using var environment = CanDoItAllTestEnvironment.Create("finite-bootstrap-registration");
        var profile = environment.CreateInMemoryProfile("registration");
        foreach (var participantCount in new[] { 0, 1, 2 }) {
            var composed = new ServiceCollection();
            TestApplicationBootstrap.ConfigureDefaultServices(composed, TestApplicationBootstrap.BuildConfiguration(profile),
                environment.CreateHostEnvironment("CanDoItAll.Tests.Unit"));
            if (participantCount == 0) {
                composed.RemoveAll<IFiniteOperationReceiptRetentionParticipant>();
            } else if (participantCount == 2) {
                composed.AddSingleton<IFiniteOperationReceiptRetentionParticipant>(participant);
            }
            await using var provider = composed.BuildServiceProvider(new ServiceProviderOptions {
                ValidateOnBuild = true,
                ValidateScopes = true
            });
            if (participantCount == 1) {
                Assert.NotNull(provider.GetRequiredService<IAppDatabaseBootstrapper>());
            } else {
                var failure = Assert.Throws<FiniteOperationProtocolException>(() => provider.GetRequiredService<IAppDatabaseBootstrapper>());
                Assert.Equal(FiniteOperationProtocolFailure.InvalidRegistration, failure.Failure);
            }
        }
    }

    [Fact]
    public void Participant_counts_and_eligibility_cannot_create_false_confirmed_progress() {
        var request = new FiniteReceiptPurgeRequest(Binding, Projects, ProjectFiniteOperationPool.DefinitionFamily);
        var valid = new FiniteReceiptPurgeObservation(1, 3, FiniteOperationEligibleWork.Present, Now, Now.AddSeconds(-1));
        FiniteOperationReceiptMaintenance.ValidateObservation(request, valid);
        foreach (var invalid in new[] {
            valid with { DeletedReceipts = 129 }, valid with { DeletedReceipts = -1 }, valid with { DeletedBytes = 0 },
            valid with { DeletedBytes = FiniteOperationBounds.BatchBytes + 1L }, valid with { DeletedReceipts = 0 },
            valid with { ObservedAtUtc = default }, valid with { EligibleAtUtc = null },
            valid with { EligibleAtUtc = Now.AddSeconds(1) }, valid with { EligibleWork = FiniteOperationEligibleWork.NoneObserved }
        }) {
            Assert.Equal(FiniteOperationProtocolFailure.InvalidParticipantResponse,
                Assert.Throws<FiniteOperationProtocolException>(() => FiniteOperationReceiptMaintenance.ValidateObservation(request, invalid)).Failure);
        }
    }

    [Fact]
    public void Family_inspection_captures_only_unique_requested_epoch_addresses() {
        var address = new FiniteOperationEpochAddress(Binding.Namespace, Projects.Pool, 1);
        var mutable = new[] { address };
        var request = new FiniteEpochInspectionRequest(Binding, Projects, ProjectFiniteOperationPool.DefinitionFamily, mutable);
        mutable[0] = new(Binding.Namespace, Projects.Pool, 2);
        Assert.Equal(address, request.Candidates.Single());
        var response = new[] { address };
        var captured = FiniteOperationReceiptMaintenance.ValidateInspection(request, response);
        response[0] = mutable[0];
        Assert.Equal(address, captured.Single());
        Assert.Throws<FiniteOperationProtocolException>(() => FiniteOperationReceiptMaintenance.ValidateInspection(request, response));
        Assert.Throws<FiniteOperationProtocolException>(() => FiniteOperationReceiptMaintenance.ValidateInspection(request, [address, address]));
        Assert.Throws<FiniteOperationProtocolException>(() => new FiniteEpochInspectionRequest(Binding, Projects,
            ProjectFiniteOperationPool.DefinitionFamily, [address, address]));
    }

    [Fact]
    public void Scheduling_advances_pool_and_family_cursors_before_work_and_resets_only_for_binding_change() {
        var secondFamily = new FiniteReceiptFamilyRegistration(new(Guid.NewGuid()), 1);
        var first = new FiniteOperationPoolRegistration(Projects.Pool, 1, Projects.Timing, [Projects.Families[0], secondFamily]);
        var second = new FiniteOperationPoolRegistration(new(Guid.NewGuid()), 1, Projects.Timing, [secondFamily]);
        var schedule = new FiniteOperationMaintenanceState([first, second]);
        var profile = Guid.NewGuid();
        schedule.ObserveBinding(profile, Binding);
        Assert.Equal(first, schedule.TakeDue(Now)!.Pool);
        Assert.Equal(second, schedule.TakeDue(Now)!.Pool);
        Assert.Null(schedule.TakeDue(Now));
        Assert.Equal(0, schedule.TakeFamily(first, [false, false]));
        var frequentTiming = new FiniteOperationTiming(Projects.Timing.AdmissionWindow, Projects.Timing.ReplayRetention,
            Projects.Timing.MutationDeadline, Projects.Timing.StatementDeadline, Projects.Timing.LockDeadline,
            TimeSpan.FromSeconds(5), Projects.Timing.WarningAfter);
        var frequentPools = Enumerable.Range(0, 3).Select(_ => new FiniteOperationPoolRegistration(new(Guid.NewGuid()),
            1, frequentTiming, [secondFamily])).ToArray();
        var frequent = new FiniteOperationMaintenanceState(frequentPools);
        frequent.ObserveBinding(profile, Binding);
        Assert.Equal(frequentPools[0], frequent.TakeDue(Now)!.Pool);
        Assert.Equal(frequentPools[1], frequent.TakeDue(Now.AddSeconds(2))!.Pool);
        Assert.Equal(frequentPools[2], frequent.TakeDue(Now.AddSeconds(4))!.Pool);
        Assert.Null(frequent.TakeDue(Now.AddSeconds(6)));
        frequent.ObserveBinding(profile, Binding);
        var resumed = frequent.TakeDue(Now.AddSeconds(6))!;
        Assert.Equal(frequentPools[0], resumed.Pool);
        Assert.Equal(TimeSpan.FromSeconds(1), resumed.ScheduledLateness);
        frequent.ObserveBinding(profile, new(Binding.Namespace, Guid.NewGuid(), Binding.SchemaVersion, Binding.GuardProtocol));
        Assert.Equal(TimeSpan.Zero, frequent.TakeDue(Now.AddSeconds(6))!.ScheduledLateness);
        Assert.Equal(1, schedule.TakeFamily(first, [false, false]));
        Assert.Equal(1, schedule.TakeFamily(first, [true, false]));
        Assert.Equal(-1, schedule.TakeFamily(first, [true, true]));
        schedule.ObserveBinding(profile, Binding);
        Assert.Null(schedule.TakeDue(Now));
        Assert.Equal(TimeSpan.FromSeconds(10), schedule.TakeDue(Now.AddSeconds(70))!.ScheduledLateness);
        Assert.Equal(second, schedule.TakeDue(Now.AddSeconds(70))!.Pool);
        schedule.ObserveBinding(Guid.NewGuid(), Binding);
        Assert.Equal(first, schedule.TakeDue(Now)!.Pool);
        Assert.Equal(0, schedule.TakeFamily(first, [false, false]));
    }

    [Fact]
    public void Neutral_model_contains_only_composite_epoch_metadata_without_owner_foreign_keys_or_cascades() {
        using var context = new FiniteOperationDbContext(new DbContextOptionsBuilder<FiniteOperationDbContext>()
            .UseInMemoryDatabase($"finite-model-{Guid.NewGuid():N}").Options);
        Assert.Equal(new[] { typeof(FiniteOperationEpochRecord), typeof(FiniteOperationStateRecord) },
            context.Model.GetEntityTypes().Select(entity => entity.ClrType).OrderBy(type => type.Name));
        Assert.All(context.Model.GetEntityTypes(), entity => {
            Assert.Empty(entity.GetForeignKeys());
            Assert.Empty(entity.GetReferencingForeignKeys());
            Assert.Empty(entity.GetNavigations());
        });
        Assert.Equal(new[] { "NamespaceId", "PoolId", "Epoch" }, context.Model.FindEntityType(typeof(FiniteOperationEpochRecord))!
            .FindPrimaryKey()!.Properties.Select(property => property.Name));
        Assert.Equal(new[] { "NamespaceId", "PoolId" }, context.Model.FindEntityType(typeof(FiniteOperationStateRecord))!
            .FindPrimaryKey()!.Properties.Select(property => property.Name));
        Assert.Null(context.Model.FindEntityType(typeof(ProjectDefinitionReceiptRecord)));
    }

    [Fact]
    public void Issued_snapshot_preserves_its_original_horizon_and_rejects_inconsistent_metadata() {
        var snapshot = new FiniteOperationEpochSnapshot(Binding, Projects.Pool, 1, 1, FiniteOperationEpochState.Open,
            Now.AddHours(-1), Now.AddHours(1), null, 30000, 604800, Now);
        Assert.Equal(Now.AddHours(1).AddSeconds(30).AddDays(7), snapshot.ReplayUntilUtc);
        Assert.Throws<FiniteOperationProtocolException>(() => new FiniteOperationEpochSnapshot(Binding, Projects.Pool, 1, 1,
            FiniteOperationEpochState.Open, Now.AddHours(-1), Now.AddHours(1), Now, 30000, 604800, Now));
        Assert.Throws<FiniteOperationProtocolException>(() => new FiniteOperationEpochSnapshot(Binding, Projects.Pool, 1, 1,
            FiniteOperationEpochState.Closed, Now.AddHours(-1), Now.AddHours(1), null, 30000, 604800, Now));
        Assert.Throws<FiniteOperationProtocolException>(() => new FiniteOperationEpochSnapshot(Binding, Projects.Pool, 1, 1,
            FiniteOperationEpochState.Open, Now.AddHours(-1), Now.AddHours(1), null, 30000, 604800, default));
    }

    private static FiniteOperationReceiptMaintenance Maintenance(FiniteOperationPoolRegistration[] pools,
        IFiniteOperationReceiptRetentionParticipant[] participants) => new(new DbContextOptionsBuilder<FiniteOperationDbContext>().Options,
            CoordinatedDatabaseTransaction.ForProfile(new(new DatabaseProfileRecord { ProviderKind = DatabaseProviderKind.InMemory },
                DatabaseProfileResolutionSource.ExplicitOverride, "finite-contract-registration")), pools, participants);

    private sealed class NeverExecuteParticipant(FiniteOperationPoolId pool, FiniteReceiptFamilyId family)
        : IFiniteOperationReceiptRetentionParticipant {
        public FiniteOperationPoolId Pool => pool;
        public FiniteReceiptFamilyId Family => family;
        public Task<FiniteReceiptPurgeObservation> PurgeRetiredBatchEnlistedAsync(FiniteReceiptPurgeRequest request, CancellationToken cancellationToken)
            => throw new InvalidOperationException("Pure registration checks must not invoke a participant.");
        public Task<IReadOnlyList<FiniteOperationEpochAddress>> FindNonemptyEpochsEnlistedAsync(FiniteEpochInspectionRequest request,
            CancellationToken cancellationToken) => throw new InvalidOperationException("Pure registration checks must not invoke a participant.");
    }
}
