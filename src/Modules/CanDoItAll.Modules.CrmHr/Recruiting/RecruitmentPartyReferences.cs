using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.CrmHr;

internal static class RecruitmentPartyReferences {
    internal static async Task<bool> WouldInvalidateAsync(CrmHrDbContext context, Guid partyId, PartyType type, CancellationToken cancellationToken) {
        if (type is not (PartyType.Person or PartyType.AiAgent) && await context.Set<RecruitmentApplication>().AsNoTracking()
            .AnyAsync(row => row.PartyId == partyId, cancellationToken)) {
            return true;
        }
        if (type is not (PartyType.Organization or PartyType.OrganizationUnit) && await context.Set<RecruitmentApplication>().AsNoTracking()
            .AnyAsync(row => row.TargetUnitPartyId == partyId, cancellationToken)) {
            return true;
        }
        return type != PartyType.Person && (
            await context.Set<RecruitmentApplication>().AsNoTracking().AnyAsync(row => row.RecruiterPartyId == partyId || row.HiringManagerPartyId == partyId, cancellationToken) ||
            await context.Set<RecruitmentInterview>().AsNoTracking().AnyAsync(row => row.InterviewerPartyId == partyId, cancellationToken) ||
            await context.Set<OnboardingTask>().AsNoTracking().AnyAsync(row => row.OwnerPartyId == partyId, cancellationToken));
    }

    internal static async Task<Error?> ValidateApplicationPartiesAsync(
        CrmHrDbContext dbContext,
        RecruitmentApplicationEditorModel model,
        Guid candidatePartyId,
        CancellationToken cancellationToken) {
        if (model.RecruiterPartyId.HasValue) {
            var recruiterError = await ValidatePeopleOnlyPartyAsync(
                dbContext,
                model.RecruiterPartyId.Value,
                "Recruiter must reference an existing person.",
                "crmhr.recruiting.recruiter-invalid",
                cancellationToken);
            if (recruiterError is not null) {
                return recruiterError;
            }
        }

        if (model.HiringManagerPartyId.HasValue) {
            if (model.HiringManagerPartyId.Value == candidatePartyId) {
                return Error.Validation("Candidate cannot be the hiring manager.", "crmhr.recruiting.hiring-manager-self");
            }

            var hiringManagerError = await ValidatePeopleOnlyPartyAsync(
                dbContext,
                model.HiringManagerPartyId.Value,
                "Hiring manager must reference an existing person.",
                "crmhr.recruiting.hiring-manager-invalid",
                cancellationToken);
            if (hiringManagerError is not null) {
                return hiringManagerError;
            }
        }

        if (model.TargetUnitPartyId.HasValue) {
            var targetUnit = await dbContext.Set<Party>()
                .Select(item => new {
                    item.Id,
                    item.PartyType
                })
                .SingleOrDefaultAsync(item => item.Id == model.TargetUnitPartyId.Value, cancellationToken);
            if (targetUnit is null ||
                (targetUnit.PartyType != PartyType.Organization && targetUnit.PartyType != PartyType.OrganizationUnit)) {
                return Error.Validation("Target unit must reference an existing organization or organization unit.", "crmhr.recruiting.target-unit-invalid");
            }
        }

        return null;
    }


    internal static async Task<Error?> ValidateRecruitmentCandidatePartyAsync(
        CrmHrDbContext dbContext,
        Guid partyId,
        CancellationToken cancellationToken) {
        var party = await dbContext.Set<Party>()
            .AsNoTracking()
            .Where(item => item.Id == partyId)
            .Select(item => new {
                item.PartyType
            })
            .SingleOrDefaultAsync(cancellationToken);
        return party is null || party.PartyType is not PartyType.Person and not PartyType.AiAgent
            ? Error.Validation(
                "Candidate must reference an existing person or AI agent.",
                "crmhr.recruiting.candidate-invalid")
            : null;
    }


    internal static async Task<Error?> ValidatePeopleOnlyPartyAsync(
        CrmHrDbContext dbContext,
        Guid partyId,
        string message,
        string code,
        CancellationToken cancellationToken) {
        var party = await dbContext.Set<Party>()
            .AsNoTracking()
            .Select(item => new {
                item.Id,
                item.PartyType
            })
            .SingleOrDefaultAsync(item => item.Id == partyId, cancellationToken);
        return party is null || party.PartyType != PartyType.Person
            ? Error.Validation(message, code)
            : null;
    }

}
