using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Templates;

namespace CanDoItAll.Processes.Application;

public sealed record ProcessCompatibilityDecisionRequest(
    ProcessTemplateCompatibilityReport TemplateCompatibilityReport,
    LegacyProcessHistoryInventoryReport RuntimeHistoryInventory,
    bool ProductOwnerApprovedDeletion,
    bool FullMigrationRequired,
    string SignoffOwner);

public sealed record ProcessCompatibilityDecisionReport(
    ProcessRuntimeHistoryCompatibilityOption SelectedOption,
    bool AllowsRuntimeActionsOnLegacyRuns,
    IReadOnlyList<string> RequiredSignoffOwners,
    IReadOnlyList<string> BlockingIssues,
    IReadOnlyList<string> RequiredFollowUps,
    string CompatibilityReportSummary);

public sealed class ProcessCompatibilityDecisionService
{
    public ProcessCompatibilityDecisionReport Decide(ProcessCompatibilityDecisionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.TemplateCompatibilityReport);
        ArgumentNullException.ThrowIfNull(request.RuntimeHistoryInventory);

        var signoffOwners = string.IsNullOrWhiteSpace(request.SignoffOwner)
            ? Array.Empty<string>()
            : [request.SignoffOwner];
        var blockingIssues = new List<string>();
        var followUps = new List<string>();

        if (request.TemplateCompatibilityReport.RequiresManualReview)
        {
            blockingIssues.Add("Template compatibility scan requires manual review before import or publication.");
        }

        var option = SelectOption(request);
        var allowsRuntimeActions = option == ProcessRuntimeHistoryCompatibilityOption.FullMigration;

        if (option == ProcessRuntimeHistoryCompatibilityOption.ReadOnlyLegacyProjectionPlusArchive)
        {
            followUps.Add("Implement UI labels that mark legacy runs as read-only.");
            followUps.Add("Keep archive export/search available for legacy runtime evidence.");
        }

        if (option == ProcessRuntimeHistoryCompatibilityOption.FullMigration)
        {
            followUps.Add("Run explicit full history migration and validation before enabling runtime actions.");
        }

        return new ProcessCompatibilityDecisionReport(
            option,
            allowsRuntimeActions,
            signoffOwners,
            blockingIssues,
            followUps,
            CreateSummary(request, option));
    }

    private static ProcessRuntimeHistoryCompatibilityOption SelectOption(ProcessCompatibilityDecisionRequest request)
    {
        if (request.FullMigrationRequired)
        {
            return ProcessRuntimeHistoryCompatibilityOption.FullMigration;
        }

        if (request.ProductOwnerApprovedDeletion &&
            request.RuntimeHistoryInventory.TotalRecordCount > 0)
        {
            return ProcessRuntimeHistoryCompatibilityOption.DropAfterExplicitApproval;
        }

        return request.RuntimeHistoryInventory.TotalRecordCount > 0
            ? ProcessRuntimeHistoryCompatibilityOption.ReadOnlyLegacyProjectionPlusArchive
            : ProcessRuntimeHistoryCompatibilityOption.ArchiveExport;
    }

    private static string CreateSummary(
        ProcessCompatibilityDecisionRequest request,
        ProcessRuntimeHistoryCompatibilityOption option)
    {
        return
            $"Selected {option} for {request.RuntimeHistoryInventory.TotalRecordCount} legacy runtime record(s) " +
            $"and {request.TemplateCompatibilityReport.MigrationDryRun.ProcessCount} process template(s).";
    }
}
