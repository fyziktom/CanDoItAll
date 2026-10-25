using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using static CanDoItAll.Tests.Integration.Processes.ProcessDecisionPersistenceFixture;

namespace CanDoItAll.Tests.Integration.Processes;

internal sealed record ProcessStepMaterialFixture(ProcessRootMaterialFixture Root, ProcessRuntimeStateSnapshot State,
    ProcessRuntimeDispatchPrecondition Claim) : IAsyncDisposable {
    internal IServiceProvider Services => Root.Services;

    internal static async Task<ProcessStepMaterialFixture> CreateAsync(ReadBasisMode mode = ReadBasisMode.StepSnapshot, int steps = 1,
        TimeSpan? claimDuration = null, ProjectTaskPlanOperationFixture? existingSql = null, Guid? executorId = null,
        bool allowTaskPublication = false, int promptCharacters = 16_384) {
        var root = await ProcessRootMaterialFixture.CreateAsync(steps, mode, existingSql, executorId, allowTaskPublication, promptCharacters);
        try {
            var state = root.State;
            if (mode == ReadBasisMode.RunSnapshot) {
                state = (await root.Services.GetRequiredService<ProcessDecisionRootPreparationCoordinator>().PrepareAsync(state)).State;
            }
            var selected = state.Steps[0];
            var claim = new DispatchClaimState(new(Guid.NewGuid()), selected.StepInstanceId, new("selected-step-fixture"),
                DispatchClaimStatus.Claimed, selected.AttemptNumber + 1, DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow.Add(claimDuration ?? TimeSpan.FromHours(1)), null, null);
            var next = state with {
                UpdatedAtUtc = state.UpdatedAtUtc.AddSeconds(1),
                Steps = state.Steps.Select(step => step.StepInstanceId == selected.StepInstanceId ? step with {
                    Status = ProcessRuntimeStepStatus.Running, AttemptNumber = claim.AttemptNumber, ActiveClaimToken = claim.ClaimToken
                } : step).ToArray(),
                Claims = [.. state.Claims, claim]
            };
            await using var context = Context(root.Services);
            state = (await ProcessRootMaterialFixture.Owner(context, root.Services).CommitAsync(new(RuntimeCommandId.New(), state,
                new(ProcessRuntimeTransitionOutcome.Applied, next, [], [], [], [])))).State;
            return new(root, state, new(selected.StepInstanceId, claim.ClaimToken, claim.OwnerId));
        } catch {
            await root.DisposeAsync();
            throw;
        }
    }

    internal EfProcessDecisionStepQuery Query(ProcessPersistenceDbContext context, TimeProvider? clock = null)
        => new(context, Services.GetRequiredService<ICanonicalRuntimeDatabase>(), clock ?? TimeProvider.System);

    internal EfProcessRuntimeUnitOfWork Owner(ProcessPersistenceDbContext context, TimeProvider? clock = null)
        => new(context, clock ?? TimeProvider.System, Services.GetRequiredService<CoordinatedDatabaseTransaction>(),
            Services.GetRequiredService<IProcessProjectAdmissionPolicy>(), Services.GetRequiredService<IProcessLaunchAuthorityPolicy>(),
            runtimeDatabase: Services.GetRequiredService<ICanonicalRuntimeDatabase>());

    internal ProcessDecisionStepPreparationCoordinator Coordinator(ProcessPersistenceDbContext context,
        IProcessDecisionMaterialCapturer? capturer = null, IProcessRuntimeUnitOfWork? commit = null, TimeProvider? clock = null) {
        var owner = Owner(context, clock);
        return new(Query(context, clock), Services.GetRequiredService<IProcessDecisionResultQuery>(),
            Services.GetRequiredService<IProcessDecisionChildReader>(), [capturer ?? Root.Capturer], owner, owner, commit ?? owner,
            clock ?? TimeProvider.System, NullLogger<ProcessDecisionStepPreparationCoordinator>.Instance);
    }

    public ValueTask DisposeAsync() => Root.DisposeAsync();
}
