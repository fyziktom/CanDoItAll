using System.Text.Json;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Modules.Workbench;

internal sealed record DotNetFeatureImplementationLaunchPolicy(
    string RequiredToolReceiptMap,
    string CompletionIssueRouteMap,
    string ProductSourceInspectionRequiredStepKeys,
    string ProductSourceInspectionRequiredBranchOutcomeKeyMap,
    string ProductSourceInspectionExcludedPathFragmentsByStep,
    string ProductMutationRequiredBranchOutcomeKeyMap,
    string ProductMutationBeforeManagedOutputRequiredStepKeys,
    string ProductMutationToolNames,
    string RuntimeRoutedBranchOutcomeKeyMap);

internal static class DotNetFeatureImplementationLaunchPolicyBuilder
{
    public static DotNetFeatureImplementationLaunchPolicy Build()
        => new(
            BuildRequiredToolReceiptMap(),
            BuildCompletionIssueRouteMap(),
            JsonSerializer.Serialize(new[]
            {
                "code-change",
                "targeted-validation",
                "feature-repair",
                "targeted-recheck"
            }),
            JsonSerializer.Serialize(new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["targeted-validation"] = ["feature-accepted"],
                ["feature-repair"] = ["feature-repair-applied"],
                ["targeted-recheck"] = ["feature-accepted"]
            }),
            BuildProductSourceInspectionExcludedPathFragmentsByStep(),
            JsonSerializer.Serialize(new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["feature-repair"] = ["feature-repair-applied"]
            }),
            JsonSerializer.Serialize(new[]
            {
                "code-change",
                "feature-repair"
            }),
            JsonSerializer.Serialize(new[]
            {
                "workspace_write_file",
                "workspace_append_file",
                "workspace_copy_path",
                "workspace_move_path",
                "workspace_delete_path",
                "workspace_pwsh_run_script",
                "workspace_dotnet_new"
            }),
            JsonSerializer.Serialize(new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["code-change"] = ["implementation-attempt-incomplete"],
                ["feature-repair"] = ["repair-attempt-incomplete"]
            }));

    private static string BuildProductSourceInspectionExcludedPathFragmentsByStep()
    {
        string[] nonOwningValidationFragments =
        [
            "/Layout/",
            "/wwwroot/",
            "/Program.cs",
            "/App.razor",
            "/_Imports.razor",
            ".csproj"
        ];
        return JsonSerializer.Serialize(new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["code-change"] = nonOwningValidationFragments,
            ["targeted-validation"] = nonOwningValidationFragments,
            ["feature-repair"] = nonOwningValidationFragments,
            ["targeted-recheck"] = nonOwningValidationFragments
        });
    }

    private static string BuildRequiredToolReceiptMap()
    {
        var validationReceipts = DotNetValidationReceiptPolicy.CreateRequiredReceiptNames();
        return JsonSerializer.Serialize(new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["targeted-validation"] = validationReceipts,
            ["feature-repair"] = validationReceipts,
            ["targeted-recheck"] = validationReceipts
        });
    }

    private static string BuildCompletionIssueRouteMap()
        => JsonSerializer.Serialize(new Dictionary<string, object[]>(StringComparer.Ordinal)
        {
            ["code-change"] = [BuildIncompleteMutationRoute("implementation-attempt-incomplete", "Implementation attempt incomplete")],
            ["feature-repair"] = BuildIncompleteRepairRoutes("repair-attempt-incomplete", "Repair attempt incomplete")
        });

    private static object[] BuildIncompleteRepairRoutes(string branchOutcomeKey, string branchOutcomeTitle)
        =>
        [
            BuildIncompleteRoute(ProcessCompletionDiagnosticCodes.ProductMutationReceiptMissing, branchOutcomeKey, branchOutcomeTitle),
            BuildIncompleteRoute(
                ProcessCompletionDiagnosticCodes.ManagedArtifactWriteReceiptMissing,
                branchOutcomeKey,
                branchOutcomeTitle,
                onlyAfterAutomaticRetry: true),
            BuildIncompleteRoute(ProcessCompletionDiagnosticCodes.ProductRequiredToolReceiptMissing, branchOutcomeKey, branchOutcomeTitle),
            BuildIncompleteRoute(ProcessCompletionDiagnosticCodes.ProductSourceInspectionEvidenceMissing, branchOutcomeKey, branchOutcomeTitle)
        ];

    private static object BuildIncompleteMutationRoute(string branchOutcomeKey, string branchOutcomeTitle)
        => BuildIncompleteRoute(
            ProcessCompletionDiagnosticCodes.ProductMutationReceiptMissing,
            branchOutcomeKey,
            branchOutcomeTitle);

    private static object BuildIncompleteRoute(
        string issueCode,
        string branchOutcomeKey,
        string branchOutcomeTitle,
        bool onlyAfterAutomaticRetry = false)
        => new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["issueCode"] = issueCode,
            ["sourceBranchOutcomeKeys"] = Array.Empty<string>(),
            ["targetBranchOutcomeKey"] = branchOutcomeKey,
            ["targetBranchOutcomeTitle"] = branchOutcomeTitle,
            ["requiresDefectEvidence"] = false,
            ["onlyAfterAutomaticRetry"] = onlyAfterAutomaticRetry
        };
}
