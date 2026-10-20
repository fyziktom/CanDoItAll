using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using static CanDoItAll.Tests.Integration.Processes.ProcessDecisionPersistenceFixture;
using static CanDoItAll.Tests.Integration.Processes.ProcessDecisionRunScopeFixture;

namespace CanDoItAll.Tests.Integration.Processes;

internal sealed record ProcessDecisionDispatchAuthorityFixture(ProcessDecisionChildMaterialFixture Original,
    ProcessRuntimeStateSnapshot State, ProcessExecutionClaimEvidence Evidence) {
    internal static async Task<ProcessDecisionDispatchAuthorityFixture> CreateAsync(ProcessPersistenceDbContext context,
        IServiceProvider services, ReadBasisMode mode = ReadBasisMode.RunSnapshot, bool descendant = false, bool prepared = false) {
        var original = await ProcessDecisionChildMaterialFixture.CreateAsync(context, services, mode, descendant, prepared: prepared);
        var state = (await Owner(context, services).CommitAsync(Claim(original.Enrolled.State))).State;
        var assignment = (await new EfProcessRuntimeStepAssignmentStore(context).LoadAsync(state.RunId, Step(state)))!;
        var claim = state.Claims.Single(item => item.ClaimToken == state.Steps.Single().ActiveClaimToken);
        return new(original, state, new(original.Binding.Child.Address.ChildId.Value, Guid.Parse(assignment.ExecutorId), state.RunId,
            Step(state), claim.ClaimToken.Value, assignment.StepKey, claim.CreatedAtUtc.AddTicks(1), true));
    }

    internal Task<ProcessExecutionDispatchAuthorityResult> ReadAsync(EfProcessExecutionAuthorityQuery query)
        => query.ReadDecisionDispatchAsync(Evidence, Original.Binding, Original.Binding.ExecutionContract, Original.Material.ContentContract);

    internal static EfProcessExecutionAuthorityQuery Query(IServiceProvider services, TimeProvider clock, params IInterceptor[] interceptors)
        => new(new ContextFactory(Options(services, interceptors)), clock);

    private sealed class ContextFactory(DbContextOptions<ProcessPersistenceDbContext> options) : IDbContextFactory<ProcessPersistenceDbContext> {
        public ProcessPersistenceDbContext CreateDbContext() => new(options);
    }
}
