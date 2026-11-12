using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Runtime;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Processes.Persistence;

public sealed class EfProcessDecisionRootQuery(ProcessPersistenceDbContext context, ICanonicalRuntimeDatabase database)
    : IProcessDecisionRootQuery {
    public async Task<ProcessDecisionRootSelection?> ReadAsync(ProcessRuntimeStateSnapshot state, CancellationToken cancellationToken = default) {
        var row = await (from runtime in context.RuntimeStates.AsNoTracking()
            where runtime.RunId == state.RootRunId.Value
            join budget in context.DecisionBudgets.AsNoTracking().Where(item => item.StepInstanceId == null)
                on runtime.RunId equals budget.RunId into budgets
            from budget in budgets.DefaultIfEmpty()
            select new {
                Runtime = new ProcessRuntimeStateEntity {
                    RunId = runtime.RunId, RootRunId = runtime.RootRunId, PlanId = runtime.PlanId, PlanHash = runtime.PlanHash,
                    Status = runtime.Status, LaunchAdmissionId = runtime.LaunchAdmissionId,
                    ProjectAdmissionDatabaseProfileId = runtime.ProjectAdmissionDatabaseProfileId,
                    ProjectAdmissionProjectId = runtime.ProjectAdmissionProjectId, ProjectAdmissionLifetimeId = runtime.ProjectAdmissionLifetimeId
                },
                BudgetId = (Guid?)budget.BudgetId, budget.RunAdmissionJson
            }).SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidDataException("The original Process root runtime is missing.");
        if (row.BudgetId is null) {
            return null;
        }
        var root = ProcessDecisionRunAdmissionCodec.Deserialize(row.RunAdmissionJson
            ?? throw new InvalidDataException("The root budget has no original policy admission."));
        if (root.RunId != state.RootRunId || root.BudgetId.Value != row.BudgetId || row.Runtime.RootRunId != row.Runtime.RunId ||
                root.DatabaseProfileId != database.Profile.Profile.Id) {
            throw new InvalidDataException("The original root runtime, budget and profile disagree.");
        }
        if (root.MaterialScope is not { } scope) {
            return null;
        }
        var loaded = await new EfProcessDecisionRunScopeStore(context, database).ReadPreparationAsync(scope.ScopeId, cancellationToken).ConfigureAwait(false);
        if (ProcessDecisionRunAdmissionCodec.Serialize(loaded.Root) != row.RunAdmissionJson) {
            throw new InvalidDataException("The shared root admission changed while selecting capture.");
        }
        var header = await ReadHeaderAsync(row.Runtime, cancellationToken).ConfigureAwait(false);
        if (header.DefinitionVersionId != root.DefinitionVersionId || header.Authority is not { } authority ||
                authority.DatabaseProfileId != root.DatabaseProfileId || authority.ProjectAdmission is null) {
            throw new InvalidDataException("Root capture requires its original accepted definition and Project source authority.");
        }
        if (state.RunId == state.RootRunId && (header.PlanId != state.PlanId || header.PlanHash != state.PlanHash ||
                header.ProjectAdmission != state.ProjectAdmission || header.AdmissionId != state.LaunchAdmissionId)) {
            throw new InvalidDataException("The selected original source differs from the requesting root runtime.");
        }
        return new(root, loaded.Budget, loaded.Scope, row.Runtime.Status, authority);
    }

    private async Task<ProcessAcceptedSourceHeader> ReadHeaderAsync(ProcessRuntimeStateEntity runtime, CancellationToken cancellationToken) {
        var admissionId = runtime.LaunchAdmissionId ?? throw new InvalidDataException("Root capture requires its original accepted launch.");
        var row = await context.PreparedLaunches.AsNoTracking().Where(item => item.Id == admissionId)
            .Select(item => new { item.RunId, item.PlanId, item.AcceptedAtUtc, item.AcceptedSourceVersion, item.PreparationFingerprint,
                item.AcceptedSourceHeaderJson, item.AcceptedSourceHeaderFingerprint })
            .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidDataException("The original accepted root launch is missing.");
        if (row.RunId != runtime.RunId || row.PlanId != runtime.PlanId || row.AcceptedAtUtc is null) {
            throw new InvalidDataException("The selected root launch was not accepted for this runtime.");
        }
        if (row.AcceptedSourceVersion == ProcessAcceptedSourceVersion.LegacyPreparedLaunch &&
                row.AcceptedSourceHeaderJson is null && row.AcceptedSourceHeaderFingerprint is null) {
            throw new ProcessDecisionSourceProjectionRequiredException();
        }
        var header = ProcessAcceptedSourceCodec.ReadHeader(row.AcceptedSourceVersion, new(admissionId), row.PreparationFingerprint,
            new(row.AcceptedSourceHeaderJson ?? throw new InvalidDataException("The accepted source header is missing."),
                row.AcceptedSourceHeaderFingerprint ?? throw new InvalidDataException("The accepted source digest is missing.")));
        if (header.RunId.Value != runtime.RunId || header.RootRunId.Value != runtime.RootRunId || header.PlanId.Value != runtime.PlanId ||
                header.PlanHash != runtime.PlanHash || header.ProjectAdmission != ProcessPersistenceMappers.ReadProjectAdmission(runtime)) {
            throw new InvalidDataException("The accepted source header differs from its original runtime and Project lifetime.");
        }
        return header;
    }
}
