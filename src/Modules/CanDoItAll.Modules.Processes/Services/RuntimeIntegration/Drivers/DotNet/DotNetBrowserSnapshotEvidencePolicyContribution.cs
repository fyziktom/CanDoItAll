using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Modules.Processes;

internal sealed class DotNetBrowserSnapshotEvidencePolicyContribution : IProcessToolReceiptEvidencePolicyContribution
{
    internal const string BlazorUnhandledErrorBanner = "An unhandled error has occurred.";
    private const string QualityAcceptedBranchOutcomeKey = "quality-accepted";
    private const string ScreenshotDefinitionKey = "dotnet-ui-screenshot-writeback";
    private const string BrowserSnapshotToolName = "browser_snapshot";
    private const string BrowserSnapshotFileNameArgument = "filename";
    private static readonly HashSet<string> BrowserValidationDefinitionKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "software-delivery",
        ScreenshotDefinitionKey,
        "blazor-app-delivery",
        "blazor-app-repair-fix",
        "blazor-backend-feature",
        "blazor-frontend-feature",
        "blazor-fullstack-feature"
    };
    private static readonly HashSet<string> BrowserRepairStepKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "quality-repair",
        "repair-blazor-findings"
    };

    public IEnumerable<ProcessToolReceiptTextEvidenceRule> ResolveRules(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output)
    {
        if (!IsApplicableProcess(assignment) || !RequiresCleanBrowserEvidence(assignment, output))
        {
            return [];
        }

        return
        [
            new ProcessToolReceiptTextEvidenceRule(
                "dotnet.blazor-browser-snapshot-no-fatal-error-banner",
                BrowserSnapshotToolName,
                BrowserSnapshotFileNameArgument,
                [BlazorUnhandledErrorBanner],
                "The browser snapshot contains Blazor's visible unhandled-error banner; a zero-error console receipt does not override DOM evidence of a fatal UI state.")
        ];
    }

    private static bool IsApplicableProcess(ProcessRuntimeStepAssignment assignment)
        => assignment.LaunchVariables.TryGetValue(
               ProcessRuntimeLaunchVariables.ProcessDefinitionKey,
               out var definitionKey) &&
           BrowserValidationDefinitionKeys.Contains(definitionKey);

    private static bool RequiresCleanBrowserEvidence(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output)
        => string.Equals(
               output.BranchOutcomeKey,
               QualityAcceptedBranchOutcomeKey,
               StringComparison.OrdinalIgnoreCase) ||
           BrowserRepairStepKeys.Contains(assignment.StepKey) ||
           string.Equals(
               assignment.LaunchVariables.GetValueOrDefault(ProcessRuntimeLaunchVariables.ProcessDefinitionKey),
               ScreenshotDefinitionKey,
               StringComparison.OrdinalIgnoreCase) &&
           string.Equals(assignment.StepKey, "capture-ui-screenshots", StringComparison.OrdinalIgnoreCase);
}
