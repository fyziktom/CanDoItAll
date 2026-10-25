using CanDoItAll.Modules.Workbench;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Unit.Processes;

public sealed class ProcessLaunchDecisionDiscoveryTests {
    [Fact]
    public void Discovery_snapshots_provider_metadata_in_identity_order_without_preparing() {
        var descriptor = ProjectTaskPlanProcessLaunchPolicyProvider.SupportedContract;
        var policies = descriptor.SupportedPolicies.ToArray();
        var first = new Provider(descriptor with { Execution = descriptor.Execution with { ContractId = new("00000000-0000-0000-0000-000000000001") }, SupportedPolicies = policies });
        var later = new Provider(descriptor);
        var coordinator = new ProcessDecisionLaunchAdmissionCoordinator([later, first]);
        Assert.Equal(new[] { first.Descriptor.Execution, later.Descriptor.Execution }, coordinator.Contracts.Select(item => item.Execution));
        var captured = coordinator.Contracts[0];
        policies[0] = policies[1];
        first.Descriptor = descriptor;
        Assert.Equal(ReadBasisMode.RunSnapshot, captured.SupportedPolicies[0].ReadBasisMode);
        Assert.NotEqual(first.Descriptor.Execution, captured.Execution);
        Assert.Throws<NotSupportedException>(() => ((IList<DecisionDataPolicy>)captured.SupportedPolicies)[0] = policies[1]);
        Assert.Throws<NotSupportedException>(() => ((IList<ProcessLaunchDecisionDescriptor>)coordinator.Contracts).Clear());
    }

    [Fact]
    public void Empty_catalog_is_explicit_and_duplicate_execution_versions_are_refused() {
        Assert.Empty(new ProcessDecisionLaunchAdmissionCoordinator([]).Contracts);
        var descriptor = ProjectTaskPlanProcessLaunchPolicyProvider.SupportedContract;
        Assert.Throws<ArgumentException>(() => new ProcessDecisionLaunchAdmissionCoordinator([new Provider(descriptor), new Provider(descriptor with { DisplayName = "Other owner" })]));
        var next = descriptor with { Execution = descriptor.Execution with { Version = descriptor.Execution.Version + 1 } };
        Assert.Equal(new[] { descriptor.Execution.Version, next.Execution.Version },
            new ProcessDecisionLaunchAdmissionCoordinator([new Provider(next), new Provider(descriptor)]).Contracts.Select(item => item.Execution.Version));
    }

    [Theory]
    [InlineData(InvalidDescriptor.MissingExecution)]
    [InlineData(InvalidDescriptor.EmptyIdentity)]
    [InlineData(InvalidDescriptor.UnknownVersion)]
    [InlineData(InvalidDescriptor.UnknownExecutor)]
    [InlineData(InvalidDescriptor.MissingName)]
    [InlineData(InvalidDescriptor.MissingDescription)]
    [InlineData(InvalidDescriptor.NoPolicies)]
    [InlineData(InvalidDescriptor.MissingPolicy)]
    [InlineData(InvalidDescriptor.DuplicatePolicy)]
    public void Invalid_registration_fails_before_advertising_or_preparing(InvalidDescriptor invalid) {
        var original = ProjectTaskPlanProcessLaunchPolicyProvider.SupportedContract;
        var descriptor = invalid switch {
            InvalidDescriptor.MissingExecution => original with { Execution = null! },
            InvalidDescriptor.EmptyIdentity => original with { Execution = original.Execution with { ContractId = Guid.Empty } },
            InvalidDescriptor.UnknownVersion => original with { Execution = original.Execution with { Version = 0 } },
            InvalidDescriptor.UnknownExecutor => original with { Execution = original.Execution with { Executor = (ProcessDecisionChildOwner)99 } },
            InvalidDescriptor.MissingName => original with { DisplayName = " " },
            InvalidDescriptor.MissingDescription => original with { Description = " " },
            InvalidDescriptor.NoPolicies => original with { SupportedPolicies = [] },
            InvalidDescriptor.MissingPolicy => original with { SupportedPolicies = [null!] },
            InvalidDescriptor.DuplicatePolicy => original with { SupportedPolicies = [original.SupportedPolicies[0], original.SupportedPolicies[0]] },
            _ => throw new ArgumentOutOfRangeException(nameof(invalid))
        };
        Assert.Throws<InvalidDataException>(() => new ProcessDecisionLaunchAdmissionCoordinator([new Provider(descriptor)]));
    }

    [Theory]
    [InlineData(ReadBasisMode.RunSnapshot, OnBasisChange.PauseForReview)]
    [InlineData(ReadBasisMode.StepSnapshot, OnBasisChange.PauseForReview)]
    [InlineData(ReadBasisMode.RunSnapshot, OnBasisChange.Fail)]
    [InlineData(ReadBasisMode.StepSnapshot, OnBasisChange.Fail)]
    public void Every_advertised_choice_is_admitted_and_its_ceiling_is_enforced(ReadBasisMode mode, OnBasisChange response) {
        var descriptor = ProjectTaskPlanProcessLaunchPolicyProvider.SupportedContract;
        var advertised = Assert.Single(descriptor.SupportedPolicies.Where(item => item.ReadBasisMode == mode && item.OnBasisChange == response));
        Assert.Equal(advertised, ProjectTaskPlanProcessLaunchPolicyProvider.ResolvePolicy(advertised, null));
        Assert.True(descriptor.Supports(new(mode, PublicationFreshness.RequireCurrent, response,
            new(0, TimeSpan.FromTicks(1), TimeSpan.FromTicks(1)))));
        Assert.False(descriptor.Supports(new(mode, PublicationFreshness.RequireCurrent, response,
            new(0, advertised.Budget.MaximumWorkDuration + TimeSpan.FromTicks(1), advertised.Budget.MaximumBasisAge))));
        Assert.False(descriptor.Supports(new(mode, PublicationFreshness.RequireCurrent, response,
            new(0, advertised.Budget.MaximumWorkDuration, advertised.Budget.MaximumBasisAge + TimeSpan.FromTicks(1)))));
        Assert.False(descriptor.Supports(new(mode, PublicationFreshness.PinnedObservation, response, advertised.Budget)));
        Assert.False(descriptor.Supports(new(mode, PublicationFreshness.RequireCurrent, OnBasisChange.Replan,
            new(1, advertised.Budget.MaximumWorkDuration, advertised.Budget.MaximumBasisAge))));
    }

    public enum InvalidDescriptor { MissingExecution, EmptyIdentity, UnknownVersion, UnknownExecutor, MissingName, MissingDescription, NoPolicies, MissingPolicy, DuplicatePolicy }

    private sealed class Provider(ProcessLaunchDecisionDescriptor descriptor) : IProcessDecisionLaunchPolicyProvider {
        public ProcessLaunchDecisionDescriptor Descriptor { get; set; } = descriptor;

        public Task<ProcessDecisionLaunchAdmissions> PrepareAsync(ProcessLaunchRequest request, ProcessRuntimeCommitRequest initial, CancellationToken cancellationToken)
            => throw new InvalidOperationException("Discovery must never prepare a launch.");
    }
}
