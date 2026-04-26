using System.Text;
using CanDoItAll.Modules.Projects;

namespace CanDoItAll.Modules.Processes;

public sealed partial class ProcessesService
{
    private static string BuildLaunchApprovalRequestMessage(
        ProcessLaunchPlan plan,
        IReadOnlyList<ProcessLaunchPlanRole> roles,
        IReadOnlyDictionary<Guid, ProcessLaunchCandidate> candidateLookup,
        LaunchApprovalAuthority approvalAuthority,
        string requestedBy)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Launch plan '{plan.Name}' is waiting for approval.")
            .Append("Operating mode: ")
            .AppendLine(plan.OperatingMode.ToString())
            .Append("Requested by: ")
            .AppendLine(string.IsNullOrWhiteSpace(requestedBy) ? plan.RequestedBy : requestedBy.Trim());

        if (!string.IsNullOrWhiteSpace(plan.TriggerReason))
        {
            builder.Append("Trigger reason: ")
                .AppendLine(plan.TriggerReason);
        }

        builder.Append("Approver: ")
            .Append(approvalAuthority.ApproverDisplayName)
            .Append(" / ")
            .AppendLine(approvalAuthority.ApproverKind);
        if (!string.IsNullOrWhiteSpace(approvalAuthority.HumanSubstituteName))
        {
            builder.Append("Human substitute: ")
                .AppendLine(approvalAuthority.HumanSubstituteName);
        }

        builder.AppendLine("Selected candidates:");
        foreach (var role in roles)
        {
            var line = new StringBuilder()
                .Append("- ")
                .Append(role.DisplayName)
                .Append(": ");
            if (role.SelectedCandidateId.HasValue &&
                candidateLookup.TryGetValue(role.SelectedCandidateId.Value, out var selectedCandidate))
            {
                line.Append(selectedCandidate.DisplayName)
                    .Append(" / ")
                    .Append(selectedCandidate.CandidateKind);
                if (selectedCandidate.RequiresProvisioning)
                {
                    line.Append(" / provisioning required");
                }
            }
            else
            {
                line.Append("unresolved");
            }

            builder.AppendLine(line.ToString());
        }

        return builder.ToString().Trim();
    }

    private static string BuildLaunchApprovalDecisionMessage(
        ProcessLaunchPlan plan,
        ProcessLaunchApprovalRecord approvalRecord)
    {
        var builder = new StringBuilder()
            .Append("Launch plan '")
            .Append(plan.Name)
            .Append("' was resolved as ")
            .Append(approvalRecord.Status)
            .Append(" by ")
            .Append(approvalRecord.DecidedBy);
        if (!string.IsNullOrWhiteSpace(approvalRecord.ResolutionSummary))
        {
            builder.Append(". ")
                .Append(approvalRecord.ResolutionSummary.Trim());
        }

        return builder.ToString();
    }

    private static LaunchApprovalAuthority ResolveLaunchApprovalAuthority(
        IReadOnlyList<ProjectPartyAssignmentDetail> assignments)
    {
        var manager = assignments
            .Where(item => item.Role == ProjectPartyAssignmentRole.Manager && IsHumanAssignment(item))
            .OrderByDescending(item => item.IsPrimary)
            .ThenBy(item => item.PartyDisplayName, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
        if (manager is not null)
        {
            return new LaunchApprovalAuthority(
                manager.PartyId,
                manager.PartyDisplayName,
                manager.PartyTypeLabel,
                null,
                string.Empty);
        }

        var substitute = assignments
            .Where(item =>
                IsHumanAssignment(item) &&
                item.Role is ProjectPartyAssignmentRole.Reviewer or
                    ProjectPartyAssignmentRole.TeamMember or
                    ProjectPartyAssignmentRole.TechnicalContact or
                    ProjectPartyAssignmentRole.Stakeholder)
            .OrderByDescending(item => item.Role == ProjectPartyAssignmentRole.Reviewer)
            .ThenByDescending(item => item.IsPrimary)
            .ThenBy(item => item.PartyDisplayName, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
        if (substitute is not null)
        {
            return new LaunchApprovalAuthority(
                substitute.PartyId,
                substitute.PartyDisplayName,
                "Human substitute",
                substitute.PartyId,
                substitute.PartyDisplayName);
        }

        return new LaunchApprovalAuthority(
            null,
            "Main Manager fallback",
            "System fallback",
            null,
            string.Empty);
    }

    private static bool IsHumanAssignment(ProjectPartyAssignmentDetail assignment)
    {
        return !assignment.PartyTypeLabel.Contains("ai", StringComparison.OrdinalIgnoreCase);
    }
}
