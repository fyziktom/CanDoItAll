using System.Collections.Immutable;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Tests.Integration.Processes;
using Microsoft.Extensions.DependencyInjection;
using static CanDoItAll.Tests.Integration.Processes.ProcessDecisionPersistenceFixture;

namespace CanDoItAll.Tests.Integration;

public sealed class ProjectTaskPlanProcessFenceTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Source_expiry_before_staging_or_after_flush_leaves_no_task_or_receipt(bool afterFlush) {
        var clock = new ProcessDecisionPublicationTestClock();
        await using var fixture = await ProjectTaskPlanOperationFixture.CreateAsync(configureServices: services => services.AddSingleton<TimeProvider>(clock));
        var authority = await SourceAuthority.CreateAsync(fixture);
        var command = await fixture.CommandAsync();
        var flushed = false;
        fixture.Faults.Save.AfterFlush = _ => {
            flushed = true;
            authority.Expire(clock);
            return Task.CompletedTask;
        };
        if (!afterFlush) {
            authority.Expire(clock);
        }
        var outcome = await fixture.Publisher().PublishAsync(command, new(fixture.Caller, authority, fixture.Authority));
        Assert.Equal(ProjectTaskPlanRefusal.SourceChanged, Assert.IsType<ProjectTaskPlanOutcome.Rejected>(outcome).Reason);
        Assert.Equal(afterFlush, flushed);
        Assert.Equal(afterFlush ? 2 : 1, authority.EnlistedChecks);
        await fixture.VerifyAsync(0, 0);
        var child = (await fixture.Services.GetRequiredService<IProcessDecisionChildReader>().ReadAsync(authority.Binding.Child.Address))!;
        Assert.Null(child.Closure);
        Assert.Null(child.Settlement);
    }

    [Fact]
    public async Task One_effect_transaction_fences_both_checks_and_recovers_its_receipt_after_source_expiry() {
        var clock = new ProcessDecisionPublicationTestClock();
        await using var fixture = await ProjectTaskPlanOperationFixture.CreateAsync(configureServices: services => services.AddSingleton<TimeProvider>(clock));
        var authority = await SourceAuthority.CreateAsync(fixture);
        var command = await fixture.CommandAsync();
        var invocation = new ProjectTaskPlanInvocation(fixture.Caller, authority, fixture.Authority);
        var accepted = Assert.IsType<ProjectTaskPlanOutcome.Accepted>(await fixture.Publisher().PublishAsync(command, invocation));
        Assert.False(accepted.Replayed);
        Assert.Equal(2, authority.EnlistedChecks);
        await fixture.VerifyAsync(2, 1, accepted.Acknowledgement);
        var acquisitions = fixture.Authority.Acquisitions;
        authority.Expire(clock);
        var recovered = Assert.IsType<ProjectTaskPlanOutcome.Accepted>(await fixture.Publisher().PublishAsync(command, invocation));
        Assert.True(recovered.Replayed);
        Assert.Equal(ProjectTaskPlanOperationCodec.EncodeAccepted(accepted.Acknowledgement), ProjectTaskPlanOperationCodec.EncodeAccepted(recovered.Acknowledgement));
        Assert.Equal(acquisitions, fixture.Authority.Acquisitions);
        Assert.Equal(2, authority.EnlistedChecks);
        await fixture.VerifyAsync(2, 1, recovered.Acknowledgement);
    }

    private sealed class SourceAuthority(ProjectTaskPlanOperationFixture fixture, ProcessDecisionDispatchAuthorityFixture original,
        ProcessExecutionDispatchAuthority source) : IProjectTaskPlanPublicationAuthority {
        internal ProcessDecisionChildMaterialBinding Binding => original.Original.Binding;
        internal int EnlistedChecks { get; private set; }

        internal static async Task<SourceAuthority> CreateAsync(ProjectTaskPlanOperationFixture fixture) {
            var services = fixture.Services;
            var launch = await services.GetRequiredService<IProcessLaunchOperatorAuthoritySource>()
                .CaptureLocalAsync(fixture.Project.ProjectId, ProcessLaunchOperatorSurface.UserInterface);
            await using var context = Context(services);
            var original = await ProcessDecisionDispatchAuthorityFixture.CreateAsync(context, services, authority: launch);
            var result = await services.GetRequiredService<IProcessDecisionDispatchAuthorityQuery>()
                .ReadDecisionDispatchAsync(original.Evidence, original.Original.Binding, original.Original.Binding.ExecutionContract,
                    original.Original.Material.ContentContract);
            var source = Assert.IsType<ProcessExecutionDispatchAuthority>(result.Snapshot);
            Assert.True(source.ObservedCurrentDispatch);
            Assert.Equal(new ProcessProjectAdmission(fixture.Project.DatabaseProfileId, fixture.Project.ProjectId, fixture.Project.LifetimeId),
                source.SourceAuthority!.ProjectAdmission);
            return new(fixture, original, source);
        }

        internal void Expire(ProcessDecisionPublicationTestClock clock)
            => clock.SetUtcNow(original.State.Claims.Single(item => item.ClaimToken.Value == source.Evidence.DispatchClaimToken).ExpiresAtUtc);

        public async Task<IProjectTaskPlanHeldAuthority> AcquireAsync(ProjectTaskPlanSource expected, ProjectWriteAdmission target,
            CancellationToken cancellationToken) {
            Assert.Equal(fixture.Project, target);
            return new Held(this, await fixture.Authority.AcquireAsync(expected, target, cancellationToken));
        }

        private async Task RequireEnlistedAsync(CancellationToken cancellationToken) {
            EnlistedChecks++;
            try {
                await fixture.Services.GetRequiredService<IProcessDecisionPublicationFence>().RequireSelectedAsync(Binding,
                    source, Binding.ExecutionContract, original.Original.Material.ContentContract,
                    ProcessDecisionPublicationKind.Dispatch, cancellationToken);
            } catch (ProcessExecutionAuthorityMismatchException) {
                throw new ProjectTaskPlanAuthorityException(ProjectTaskPlanRefusal.SourceChanged);
            }
        }

        private sealed class Held(SourceAuthority owner, IProjectTaskPlanHeldAuthority current) : IProjectTaskPlanHeldAuthority {
            public ImmutableArray<ProjectWriteAdmission> AdditionalProjects => current.AdditionalProjects;
            public Task RequireCurrentAsync(CancellationToken cancellationToken) => current.RequireCurrentAsync(cancellationToken);
            public async Task RequireEnlistedAsync(CancellationToken cancellationToken) {
                await RequireCurrentAsync(cancellationToken);
                await owner.RequireEnlistedAsync(cancellationToken);
            }
            public ValueTask DisposeAsync() => current.DisposeAsync();
        }
    }
}
