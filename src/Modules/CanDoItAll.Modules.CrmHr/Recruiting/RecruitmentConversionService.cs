using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using static CanDoItAll.Modules.CrmHr.RecruitmentEditValues;

namespace CanDoItAll.Modules.CrmHr;

public sealed class RecruitmentConversionService(IDbContextFactory<CrmHrDbContext> dbContextFactory,
    WorkforceProfileEditingService workforce, IAgentRecruitingEvidenceService evidence,
    RecruitmentSearchProjection search, IActivityStream activityStream, IClock clock, CrmHrChanges changes) {
    public async Task<Result<Guid>> ConvertCandidateAsync(RecruitmentConversionEditorModel model,
        CancellationToken cancellationToken = default, Action<RecruitmentConversionReceipt>? onCommitted = null) {
        ArgumentNullException.ThrowIfNull(model);
        model = model.Snapshot();
        if (model.Original is not { } original) {
            return Result<Guid>.Failure(OriginalRequired());
        }
        if (model.ApplicationId != original.Application.Id || original.Application.PartyId != original.Workforce.PartyId) {
            return Result<Guid>.Failure(IdentityInvalid());
        }
        var committed = false;
        try {
            await changes.DemandCommitAsync(cancellationToken);
            await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            var partyId = original.Application.PartyId;
            var references = new Guid?[] { partyId, model.HomeUnitPartyId, model.ManagerPartyId }.OfType<Guid>();
            await using var transaction = await SerializableMutationScope.BeginAsync(context,
                references.Select(OpportunityPartyReferences.ScopeKey).Append(ApplicationScope(model.ApplicationId))
                    .Append(WorkforceEditValues.ScopeKey(partyId)).ToArray(), cancellationToken);
            var application = await context.Set<RecruitmentApplication>().SingleOrDefaultAsync(row => row.Id == model.ApplicationId, cancellationToken);
            var party = await context.Set<Party>().SingleOrDefaultAsync(row => row.Id == partyId, cancellationToken);
            var profile = await context.Set<WorkforceProfile>().SingleOrDefaultAsync(row => row.PartyId == partyId, cancellationToken);
            if (application is null || party is null || Application(application) != original.Application ||
                party.LifecycleStatus != original.PartyLifecycle || party.UpdatedAtUtc != original.PartyUpdatedAtUtc ||
                WorkforceEditValues.Profile(partyId, party.PartyType, profile) != original.Workforce) {
                return Result<Guid>.Failure(Conflict(RecruitmentEditingErrors.ConversionConflict));
            }
            var eligibility = RecruitmentConversionPolicy.Evaluate(application.Stage, application.Decision);
            if (eligibility is not null) {
                return Result<Guid>.Failure(eligibility);
            }
            var assessment = await ValidateAssessmentAsync(context, party, application, cancellationToken);
            if (assessment is not null) {
                return Result<Guid>.Failure(assessment);
            }
            var draft = original.Workforce.Values?.ToEditor(partyId, original.Workforce.ProfileId, Actor(model.LastChangedBy))
                ?? new WorkforceProfileEditorModel { PartyId = partyId, LastChangedBy = Actor(model.LastChangedBy) };
            draft.Original = original.Workforce;
            draft.WorkforceKind = model.WorkforceKind;
            draft.JobTitle = string.IsNullOrWhiteSpace(model.JobTitle) ? original.Application.DesiredRole : model.JobTitle;
            draft.Discipline = model.Discipline;
            draft.Seniority = model.Seniority;
            draft.HomeUnitPartyId = model.HomeUnitPartyId;
            draft.ManagerPartyId = model.ManagerPartyId;
            draft.StartDate = model.StartDate;
            draft.Location = model.Location;
            draft.TimeZone = model.TimeZone;
            draft.CapacityHoursPerWeek = model.CapacityHoursPerWeek;
            draft.Status = string.IsNullOrWhiteSpace(model.Status) ? "Active" : model.Status;
            draft.Notes = model.Notes;
            var prepared = await workforce.PrepareAsync(context, draft, cancellationToken);
            if (prepared.IsFailure) {
                return Result<Guid>.Failure(prepared.Errors);
            }
            var previousStage = application.Stage;
            application.Stage = RecruitmentStage.Hired;
            application.Decision = RecruitmentDecision.Approved;
            application.UpdatedAtUtc = CrmHrEditVersions.Next(application.UpdatedAtUtc, clock.GetUtcNow());
            party.LifecycleStatus = PartyLifecycleStatus.Active;
            party.LastChangedBy = Actor(model.LastChangedBy);
            party.UpdatedAtUtc = CrmHrEditVersions.Next(party.UpdatedAtUtc, clock.GetUtcNow());
            if (previousStage != RecruitmentStage.Hired) {
                AddAudit(context, application.Id, "RecruitmentStageChanged", $"Moved {party.DisplayName} to Hired",
                    StageDetail(previousStage, RecruitmentStage.Hired, "Converted to workforce profile."), party.LastChangedBy, clock.GetUtcNow());
            }
            AddAudit(context, application.Id, "RecruitmentConverted", $"Converted {party.DisplayName} to workforce",
                StageDetail(previousStage, RecruitmentStage.Hired, draft.JobTitle), party.LastChangedBy, clock.GetUtcNow());
            await changes.DemandCommitAsync(cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
            assessment = await ValidateAssessmentAsync(context, party, application, cancellationToken);
            if (assessment is not null) {
                return Result<Guid>.Failure(assessment);
            }
            await changes.DemandCommitAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            committed = true;
            var acceptedApplication = Accepted(application);
            acceptedApplication.CandidateName = party.DisplayName;
            var acceptedProfile = WorkforceEditValues.AcceptedProfile(party, prepared.Value.Profile);
            var acceptedOriginal = new RecruitmentConversionObservation(acceptedApplication.Original!, acceptedProfile.Original!,
                party.LifecycleStatus, party.UpdatedAtUtc);
            onCommitted?.Invoke(new(application.Id, partyId, acceptedApplication, acceptedProfile, acceptedOriginal));
            changes.PublishRecruitment(application.Id);
            await workforce.PublishSavedAsync(party, prepared.Value.Profile, cancellationToken);
            await search.UpsertAsync(application.Id, cancellationToken);
            await activityStream.RecordAsync(new ActivityWriteRequest("CRM / HR", "RecruitmentConverted",
                $"Converted {party.DisplayName} to workforce", draft.WorkforceKind.ToString(),
                ArtifactKind: nameof(RecruitmentApplication), ArtifactId: application.Id,
                Route: Route(application.Id), Actor: party.LastChangedBy), cancellationToken);
            return Result<Guid>.Success(partyId);
        } catch (Exception exception) when (!committed && WorkforceEditValues.IsConflict(exception)) {
            return Result<Guid>.Failure(Conflict(RecruitmentEditingErrors.ConversionConflict));
        }
    }

    private async Task<Error?> ValidateAssessmentAsync(CrmHrDbContext context, Party party,
        RecruitmentApplication application, CancellationToken cancellationToken) {
        if (party.PartyType != PartyType.AiAgent) {
            return null;
        }
        var agentId = await context.Set<AiResourceBinding>().AsNoTracking()
            .Where(binding => binding.PartyId == party.Id && binding.BindingStatus == AiResourceBindingStatus.Bound)
            .Select(binding => binding.TechnicalAgentId).SingleOrDefaultAsync(cancellationToken);
        AgentRecruitingCandidateReadiness? readiness = null;
        if (agentId.HasValue) {
            try {
                readiness = await evidence.GetCandidateReadinessAsync(agentId.Value, application.Id, cancellationToken);
            } catch (AgentRecruitingEvidenceException exception) {
                return Error.Validation($"Technical assessment readiness could not be verified: {exception.Message}", RecruitmentConversionPolicy.AssessmentNotReadyErrorCode);
            }
        }
        return RecruitmentConversionPolicy.Evaluate(application.Stage, application.Decision,
            assessmentRequired: true, assessmentReady: readiness?.ReadyForProduction == true);
    }
}
