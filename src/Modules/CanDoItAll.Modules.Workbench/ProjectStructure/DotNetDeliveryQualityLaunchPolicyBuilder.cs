using System.Text;
using System.Text.Json;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Modules.Workbench;

internal enum DotNetDeliveryQualityWorkflow
{
    SoftwareDelivery,
    BlazorDelivery,
    QualityRepair
}

internal sealed record DotNetDeliveryQualityLaunchPolicy(
    string RequiredToolReceiptMap,
    string RequiredFileContentCheckMap,
    string CompletionIssueRouteMap,
    string ProductMutationRequiredBranchOutcomeKeyMap,
    string RuntimeRoutedBranchOutcomeKeyMap,
    string ProductSourceInspectionRequiredStepKeys,
    string ProductSourceInspectionRequiredBranchOutcomeKeyMap,
    string ProductSourceInspectionExcludedPathFragmentsByStep,
    string ScaffoldRepairScriptRef,
    string ScaffoldRepairScript,
    string ScaffoldRepairSideEffectManifest,
    string ScaffoldRepairExecutionPlan);

internal static class DotNetDeliveryQualityLaunchPolicyBuilder
{
    private const string BrowserInteractionProofRequirement = "browser interaction proof";
    private const string ProductRequiredFileContentMissingCode = "process.adapter.product_required_file_content_missing";

    public static DotNetDeliveryQualityLaunchPolicy Build(
        DotNetDeliveryQualityWorkflow workflow,
        string appTemplate,
        bool requiresVisualTargetComparison,
        string appProjectName,
        string appProjectDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appTemplate);
        ArgumentException.ThrowIfNullOrWhiteSpace(appProjectName);
        ArgumentException.ThrowIfNullOrWhiteSpace(appProjectDirectory);

        var isBrowserVisible = IsBrowserVisibleAppTemplate(appTemplate);
        return new DotNetDeliveryQualityLaunchPolicy(
            BuildRequiredToolReceiptMap(workflow, isBrowserVisible, requiresVisualTargetComparison),
            BuildRequiredFileContentCheckMap(workflow, isBrowserVisible, appProjectName, appProjectDirectory),
            BuildCompletionIssueRouteMap(workflow),
            BuildProductMutationRequiredBranchOutcomeKeyMap(workflow),
            BuildRuntimeRoutedBranchOutcomeKeyMap(workflow),
            BuildProductSourceInspectionRequiredStepKeys(workflow),
            BuildProductSourceInspectionRequiredBranchOutcomeKeyMap(workflow),
            BuildProductSourceInspectionExcludedPathFragmentsByStep(workflow),
            BuildScaffoldRepairScriptRef(workflow, isBrowserVisible),
            BuildScaffoldRepairScript(workflow, isBrowserVisible, appProjectName, appProjectDirectory),
            BuildScaffoldRepairSideEffectManifest(workflow, isBrowserVisible, appProjectDirectory),
            BuildScaffoldRepairExecutionPlan(workflow, isBrowserVisible));
    }

    private static string BuildScaffoldRepairScriptRef(
        DotNetDeliveryQualityWorkflow workflow,
        bool isBrowserVisible)
        => workflow == DotNetDeliveryQualityWorkflow.QualityRepair && isBrowserVisible
            ? "artifacts/process-runs/{CurrentProcessRunId}/scripts/remove-default-blazor-scaffold.ps1"
            : string.Empty;

    private static string BuildScaffoldRepairScript(
        DotNetDeliveryQualityWorkflow workflow,
        bool isBrowserVisible,
        string appProjectName,
        string appProjectDirectory)
    {
        if (workflow != DotNetDeliveryQualityWorkflow.QualityRepair || !isBrowserVisible)
        {
            return string.Empty;
        }

        var escapedProjectName = appProjectName.Replace("'", "''", StringComparison.Ordinal);
        var normalizedDirectory = appProjectDirectory.Replace('\\', '/');
        return $$"""
        $ErrorActionPreference = 'Stop'
        Set-StrictMode -Version Latest

        $appDirectory = '{{normalizedDirectory}}'
        $navMenu = Join-Path $appDirectory 'Layout/NavMenu.razor'
        $mainLayout = Join-Path $appDirectory 'Layout/MainLayout.razor'
        $counterPage = Join-Path $appDirectory 'Pages/Counter.razor'
        $weatherPage = Join-Path $appDirectory 'Pages/Weather.razor'
        $weatherData = Join-Path $appDirectory 'wwwroot/sample-data/weather.json'
        $appCss = Join-Path $appDirectory 'wwwroot/css/app.css'

        function Test-GeneratedStarterPlaceholder {
            param(
                [string] $Content,
                [string] $RoutePattern
            )

            if ($Content.Length -eq 0 -or $Content.Length -gt 800 -or $Content -notmatch $RoutePattern) {
                return $false
            }

            if ($Content -match '(?i)@code|@inject|@onclick|<button|<EditForm|NavigationManager|<svg') {
                return $false
            }

            return $Content -match '(?i)\bstarter\b|\bsample\b|\bscaffold\b|redirect\s+to|removed\s+from|without\s+product\s+content'
        }

        if (Test-Path -LiteralPath $navMenu) {
            $content = Get-Content -LiteralPath $navMenu -Raw
            if ($content -match 'href\s*=\s*"counter"' -and $content -match 'href\s*=\s*"weather"') {
                $replacement = @'
        <nav aria-label="Primary navigation">
            <NavLink href="" Match="NavLinkMatch.All">{{escapedProjectName}}</NavLink>
        </nav>
        '@
                Set-Content -LiteralPath $navMenu -Value $replacement -Encoding utf8
            }
        }

        if (Test-Path -LiteralPath $mainLayout) {
            $content = Get-Content -LiteralPath $mainLayout -Raw
            $content = [regex]::Replace($content, '(?s)<a\s+href="https://learn\.microsoft\.com/aspnet/core/"[^>]*>.*?</a>', '')
            Set-Content -LiteralPath $mainLayout -Value $content -Encoding utf8
        }

        if (Test-Path -LiteralPath $counterPage) {
            $content = Get-Content -LiteralPath $counterPage -Raw
            if (($content -match 'currentCount' -and $content -match 'Click me') -or
                (Test-GeneratedStarterPlaceholder -Content $content -RoutePattern '@page\s+"/counter"')) {
                Remove-Item -LiteralPath $counterPage -Force
            }
        }

        if (Test-Path -LiteralPath $weatherPage) {
            $content = Get-Content -LiteralPath $weatherPage -Raw
            if (($content -match 'WeatherForecast' -and $content -match 'sample-data/weather\.json') -or
                (Test-GeneratedStarterPlaceholder -Content $content -RoutePattern '@page\s+"/weather"')) {
                Remove-Item -LiteralPath $weatherPage -Force
                if (Test-Path -LiteralPath $weatherData) {
                    Remove-Item -LiteralPath $weatherData -Force
                }
            }
        }

        if (Test-Path -LiteralPath $appCss) {
            $content = Get-Content -LiteralPath $appCss -Raw
            if ($content -notmatch '#blazor-error-ui') {
                Add-Content -LiteralPath $appCss -Encoding utf8 -Value @'

        #blazor-error-ui {
            display: none;
        }

        #blazor-error-ui .dismiss {
            cursor: pointer;
            position: absolute;
            right: 0.75rem;
            top: 0.5rem;
        }
        '@
            }
        }
        """;
    }

    private static string BuildScaffoldRepairSideEffectManifest(
        DotNetDeliveryQualityWorkflow workflow,
        bool isBrowserVisible,
        string appProjectDirectory)
    {
        if (workflow != DotNetDeliveryQualityWorkflow.QualityRepair || !isBrowserVisible)
        {
            return string.Empty;
        }

        var paths = new[]
        {
            CombinePath(appProjectDirectory, "Layout", "NavMenu.razor"),
            CombinePath(appProjectDirectory, "Layout", "MainLayout.razor"),
            CombinePath(appProjectDirectory, "Pages", "Counter.razor"),
            CombinePath(appProjectDirectory, "Pages", "Weather.razor"),
            CombinePath(appProjectDirectory, "wwwroot", "sample-data", "weather.json"),
            CombinePath(appProjectDirectory, "wwwroot", "css", "app.css")
        };
        return JsonSerializer.Serialize(new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["version"] = 1,
            ["mode"] = "ProductMutation",
            ["declaredReadPaths"] = paths,
            ["declaredWritePaths"] = paths,
            ["allowShellDelegation"] = false
        });
    }

    private static string BuildScaffoldRepairExecutionPlan(
        DotNetDeliveryQualityWorkflow workflow,
        bool isBrowserVisible)
    {
        if (workflow != DotNetDeliveryQualityWorkflow.QualityRepair || !isBrowserVisible)
        {
            return string.Empty;
        }

        return string.Join(
            Environment.NewLine,
            "Default Blazor scaffold repair execution plan (use only when current diagnosis/readback proves the stock template fingerprints remain):",
            "1. Write DotNetScaffoldRepairScript verbatim to DotNetScaffoldRepairScriptRef with workspace_write_file.",
            "2. Verify that .ps1 artifact with workspace_stat_path or workspace_read_file.",
            "3. Invoke workspace_pwsh_run_script with that script ref, ProductRootAlias as workingDirectory, and DotNetScaffoldRepairSideEffectManifest.",
            "4. Read back NavMenu.razor, MainLayout.razor, app.css, and stat the default Counter/Weather pages before writing the primary repair artifact.",
            "5. Rerun restore/build/test after the successful product mutation receipt.",
            "The helper removes only files matching stock Counter/Weather fingerprints or short non-functional starter/redirect placeholders on those stock routes, removes the stock ASP.NET Core link, and restores the hidden Blazor error UI rule; it must not replace functional product-specific pages that do not match those fingerprints.");
    }

    private static string BuildProductSourceInspectionRequiredStepKeys(
        DotNetDeliveryQualityWorkflow workflow)
    {
        string[] stepKeys = workflow switch
        {
            DotNetDeliveryQualityWorkflow.SoftwareDelivery =>
            [
                "peer-review",
                "qa-validation",
                "qa-recheck"
            ],
            DotNetDeliveryQualityWorkflow.BlazorDelivery =>
            [
                "validate-blazor-runtime",
                "revalidate-blazor-repair"
            ],
            DotNetDeliveryQualityWorkflow.QualityRepair =>
            [
                "diagnose-quality-failure",
                "implement-quality-repair",
                "diagnose-persistent-failure",
                "implement-bughunt-repair"
            ],
            _ => throw new ArgumentOutOfRangeException(nameof(workflow), workflow, "Unsupported .NET delivery quality workflow.")
        };
        return JsonSerializer.Serialize(stepKeys);
    }

    private static string BuildProductSourceInspectionExcludedPathFragmentsByStep(
        DotNetDeliveryQualityWorkflow workflow)
    {
        string[] nonOwningShellFragments =
        [
            "/Layout/",
            "/wwwroot/",
            "/Pages/Counter.razor",
            "/Pages/Weather.razor",
            "/Program.cs",
            "/App.razor",
            "/_Imports.razor",
            ".csproj"
        ];
        var exclusions = workflow switch
        {
            DotNetDeliveryQualityWorkflow.SoftwareDelivery => new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["peer-review"] = nonOwningShellFragments,
                ["qa-validation"] = nonOwningShellFragments,
                ["qa-recheck"] = nonOwningShellFragments
            },
            DotNetDeliveryQualityWorkflow.BlazorDelivery => new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["validate-blazor-runtime"] = nonOwningShellFragments,
                ["revalidate-blazor-repair"] = nonOwningShellFragments
            },
            DotNetDeliveryQualityWorkflow.QualityRepair => new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["diagnose-quality-failure"] = nonOwningShellFragments,
                ["diagnose-persistent-failure"] = nonOwningShellFragments
            },
            _ => throw new ArgumentOutOfRangeException(nameof(workflow), workflow, "Unsupported .NET delivery quality workflow.")
        };
        return JsonSerializer.Serialize(exclusions);
    }

    private static string BuildProductSourceInspectionRequiredBranchOutcomeKeyMap(
        DotNetDeliveryQualityWorkflow workflow)
    {
        var rules = workflow switch
        {
            DotNetDeliveryQualityWorkflow.SoftwareDelivery => new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["qa-validation"] = ["quality-accepted"],
                ["qa-recheck"] = ["quality-accepted"]
            },
            DotNetDeliveryQualityWorkflow.BlazorDelivery => new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["validate-blazor-runtime"] = ["quality-accepted"],
                ["revalidate-blazor-repair"] = ["quality-accepted"]
            },
            DotNetDeliveryQualityWorkflow.QualityRepair => new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["implement-quality-repair"] = ["product-repair-applied", "proof-only-revalidation-prepared"],
                ["implement-bughunt-repair"] = ["product-repair-applied", "proof-only-revalidation-prepared"]
            },
            _ => throw new ArgumentOutOfRangeException(nameof(workflow), workflow, "Unsupported .NET delivery quality workflow.")
        };
        return JsonSerializer.Serialize(rules);
    }

    private static string BuildProductMutationRequiredBranchOutcomeKeyMap(
        DotNetDeliveryQualityWorkflow workflow)
    {
        if (workflow != DotNetDeliveryQualityWorkflow.QualityRepair)
        {
            return string.Empty;
        }

        return JsonSerializer.Serialize(new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["implement-quality-repair"] = ["product-repair-applied"],
            ["implement-bughunt-repair"] = ["product-repair-applied"]
        });
    }

    private static string BuildRuntimeRoutedBranchOutcomeKeyMap(
        DotNetDeliveryQualityWorkflow workflow)
    {
        if (workflow != DotNetDeliveryQualityWorkflow.QualityRepair)
        {
            return string.Empty;
        }

        return JsonSerializer.Serialize(new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["implement-quality-repair"] = ["repair-attempt-incomplete"],
            ["implement-bughunt-repair"] = ["repair-attempt-incomplete"]
        });
    }

    private static string BuildRequiredToolReceiptMap(
        DotNetDeliveryQualityWorkflow workflow,
        bool isBrowserVisible,
        bool requiresVisualTargetComparison)
    {
        var validationReceipts = DotNetValidationReceiptPolicy.CreateRequiredReceiptNames();
        var qaReceipts = isBrowserVisible
            ? validationReceipts.Concat(BuildBrowserRuntimeProofReceiptNames(requiresVisualTargetComparison)).ToArray()
            : validationReceipts;
        var map = workflow switch
        {
            DotNetDeliveryQualityWorkflow.SoftwareDelivery => new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["qa-validation"] = BuildBranchAwareReceiptRules(
                    qaReceipts,
                    ["quality-accepted"],
                    "AcceptanceProof"),
                ["qa-recheck"] = BuildBranchAwareReceiptRules(
                    qaReceipts,
                    ["quality-accepted"],
                    "AcceptanceProof")
            },
            DotNetDeliveryQualityWorkflow.BlazorDelivery => new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["validate-blazor-runtime"] = BuildBranchAwareReceiptRules(
                    qaReceipts,
                    ["quality-accepted"],
                    "AcceptanceProof"),
                ["repair-blazor-findings"] = qaReceipts,
                ["revalidate-blazor-repair"] = BuildBranchAwareReceiptRules(
                    qaReceipts,
                    ["quality-accepted"],
                    "AcceptanceProof")
            },
            DotNetDeliveryQualityWorkflow.QualityRepair => new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["implement-quality-repair"] = validationReceipts,
                ["validate-quality-repair"] = BuildBranchAwareReceiptRules(
                    qaReceipts,
                    ["quality-repair-accepted"],
                    "AcceptanceProof"),
                ["implement-bughunt-repair"] = validationReceipts,
                ["revalidate-bughunt-repair"] = BuildBranchAwareReceiptRules(
                    qaReceipts,
                    ["quality-repair-accepted"],
                    "AcceptanceProof")
            },
            _ => throw new ArgumentOutOfRangeException(nameof(workflow), workflow, "Unsupported .NET delivery quality workflow.")
        };

        return JsonSerializer.Serialize(map);
    }

    private static string BuildRequiredFileContentCheckMap(
        DotNetDeliveryQualityWorkflow workflow,
        bool isBrowserVisible,
        string appProjectName,
        string appProjectDirectory)
    {
        if (!isBrowserVisible)
        {
            return string.Empty;
        }

        var map = workflow switch
        {
            DotNetDeliveryQualityWorkflow.SoftwareDelivery => new Dictionary<string, object[]>(StringComparer.Ordinal)
            {
                ["qa-validation"] = BuildVisibleUiScaffoldRemovalChecks(
                    appProjectName,
                    appProjectDirectory,
                    enforceBranchOutcomeKeys: ["quality-accepted"],
                    evidenceBranchOutcomeKeys: ["repair-required"]),
                ["qa-recheck"] = BuildVisibleUiScaffoldRemovalChecks(
                    appProjectName,
                    appProjectDirectory,
                    enforceBranchOutcomeKeys: ["quality-accepted"],
                    evidenceBranchOutcomeKeys: ["repair-escalation"])
            },
            DotNetDeliveryQualityWorkflow.BlazorDelivery => new Dictionary<string, object[]>(StringComparer.Ordinal)
            {
                ["validate-blazor-runtime"] = BuildVisibleUiScaffoldRemovalChecks(
                    appProjectName,
                    appProjectDirectory,
                    enforceBranchOutcomeKeys: ["quality-accepted"],
                    evidenceBranchOutcomeKeys: ["repair-required"]),
                ["revalidate-blazor-repair"] = BuildVisibleUiScaffoldRemovalChecks(
                    appProjectName,
                    appProjectDirectory,
                    enforceBranchOutcomeKeys: ["quality-accepted"],
                    evidenceBranchOutcomeKeys: ["repair-escalation"])
            },
            DotNetDeliveryQualityWorkflow.QualityRepair => new Dictionary<string, object[]>(StringComparer.Ordinal)
            {
                ["validate-quality-repair"] = BuildVisibleUiScaffoldRemovalChecks(
                    appProjectName,
                    appProjectDirectory,
                    enforceBranchOutcomeKeys: ["quality-repair-accepted"],
                    evidenceBranchOutcomeKeys: ["bughunt-required"]),
                ["revalidate-bughunt-repair"] = BuildVisibleUiScaffoldRemovalChecks(
                    appProjectName,
                    appProjectDirectory,
                    enforceBranchOutcomeKeys: ["quality-repair-accepted"],
                    evidenceBranchOutcomeKeys: ["quality-repair-no-go"])
            },
            _ => throw new ArgumentOutOfRangeException(nameof(workflow), workflow, "Unsupported .NET delivery quality workflow.")
        };

        return JsonSerializer.Serialize(map);
    }

    private static string BuildCompletionIssueRouteMap(DotNetDeliveryQualityWorkflow workflow)
    {
        var map = workflow switch
        {
            DotNetDeliveryQualityWorkflow.SoftwareDelivery => BuildIssueRouteMap(
                "qa-validation",
                "qa-recheck",
                "quality-accepted",
                "repair-required",
                "Repair required",
                "repair-escalation",
                "Repair escalation"),
            DotNetDeliveryQualityWorkflow.BlazorDelivery => BuildIssueRouteMap(
                "validate-blazor-runtime",
                "revalidate-blazor-repair",
                "quality-accepted",
                "repair-required",
                "Repair required",
                "repair-escalation",
                "Repair escalation"),
            DotNetDeliveryQualityWorkflow.QualityRepair => BuildQualityRepairIssueRouteMap(),
            _ => throw new ArgumentOutOfRangeException(nameof(workflow), workflow, "Unsupported .NET delivery quality workflow.")
        };

        return JsonSerializer.Serialize(map);
    }

    private static Dictionary<string, object[]> BuildQualityRepairIssueRouteMap()
    {
        var map = BuildIssueRouteMap(
            "validate-quality-repair",
            "revalidate-bughunt-repair",
            "quality-repair-accepted",
            "bughunt-required",
            "Bughunt required",
            "quality-repair-no-go",
            "Quality repair no-go");
        map["implement-quality-repair"] = BuildIncompleteRepairRoutes();
        map["implement-bughunt-repair"] = BuildIncompleteRepairRoutes();
        return map;
    }

    private static object[] BuildIncompleteRepairRoutes()
        =>
        [
            BuildIncompleteRepairRoute(ProcessCompletionDiagnosticCodes.ProductRequiredToolReceiptMissing),
            BuildIncompleteRepairRoute(ProcessCompletionDiagnosticCodes.ProductMutationReceiptMissing),
            BuildIncompleteRepairRoute(ProcessCompletionDiagnosticCodes.ProductSourceInspectionEvidenceMissing)
        ];

    private static object BuildIncompleteRepairRoute(string issueCode)
        => BuildBranchRoute(
            issueCode,
            "product-repair-applied",
            "repair-attempt-incomplete",
            "Repair attempt incomplete",
            requiresDefectEvidence: false);

    private static Dictionary<string, object[]> BuildIssueRouteMap(
        string validationStepKey,
        string revalidationStepKey,
        string acceptedBranchOutcomeKey,
        string firstFailureBranchOutcomeKey,
        string firstFailureBranchTitle,
        string finalFailureBranchOutcomeKey,
        string finalFailureBranchTitle)
        => new(StringComparer.Ordinal)
        {
            [validationStepKey] = BuildIssueRoutes(
                acceptedBranchOutcomeKey,
                firstFailureBranchOutcomeKey,
                firstFailureBranchTitle),
            [revalidationStepKey] = BuildIssueRoutes(
                acceptedBranchOutcomeKey,
                finalFailureBranchOutcomeKey,
                finalFailureBranchTitle)
        };

    private static object[] BuildIssueRoutes(
        string sourceBranchOutcomeKey,
        string targetBranchOutcomeKey,
        string targetBranchOutcomeTitle)
        =>
        [
            BuildBranchRoute(
                ProcessCompletionDiagnosticCodes.ProductRequiredToolReceiptMissing,
                sourceBranchOutcomeKey,
                targetBranchOutcomeKey,
                targetBranchOutcomeTitle,
                requiresDefectEvidence: false,
                onlyAfterAutomaticRetry: true),
            BuildBranchRoute(
                ProductRequiredFileContentMissingCode,
                sourceBranchOutcomeKey,
                targetBranchOutcomeKey,
                targetBranchOutcomeTitle),
            BuildBranchRoute(
                ProcessCompletionDiagnosticCodes.ToolReceiptEvidenceContentRejected,
                sourceBranchOutcomeKey,
                targetBranchOutcomeKey,
                targetBranchOutcomeTitle),
            BuildBranchRoute(
                ProcessCompletionDiagnosticCodes.ProductSourceInspectionEvidenceMissing,
                sourceBranchOutcomeKey,
                targetBranchOutcomeKey,
                targetBranchOutcomeTitle),
            BuildBranchRoute(
                ProcessCompletionDiagnosticCodes.UiInteractionEvidenceMissing,
                sourceBranchOutcomeKey,
                targetBranchOutcomeKey,
                targetBranchOutcomeTitle),
            BuildBranchRoute(
                ProcessCompletionDiagnosticCodes.UiPostInteractionStateEvidenceMissing,
                sourceBranchOutcomeKey,
                targetBranchOutcomeKey,
                targetBranchOutcomeTitle)
        ];

    private static object BuildBranchRoute(
        string issueCode,
        string sourceBranchOutcomeKey,
        string targetBranchOutcomeKey,
        string targetBranchOutcomeTitle,
        bool requiresDefectEvidence = true,
        bool onlyAfterAutomaticRetry = false)
        => new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["issueCode"] = issueCode,
            ["sourceBranchOutcomeKeys"] = new[] { sourceBranchOutcomeKey },
            ["targetBranchOutcomeKey"] = targetBranchOutcomeKey,
            ["targetBranchOutcomeTitle"] = targetBranchOutcomeTitle,
            ["requiresDefectEvidence"] = requiresDefectEvidence,
            ["onlyAfterAutomaticRetry"] = onlyAfterAutomaticRetry
        };

    private static object[] BuildBranchAwareReceiptRules(
        IReadOnlyList<string> receipts,
        IReadOnlyList<string> enforceBranchOutcomeKeys,
        string purpose)
        => receipts
            .Select(receipt => new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["toolName"] = receipt,
                ["purpose"] = purpose,
                ["enforceBranchOutcomeKeys"] = enforceBranchOutcomeKeys,
                ["reason"] = "Current-run proof required for the selected branch outcome."
            })
            .ToArray();

    private static object[] BuildVisibleUiScaffoldRemovalChecks(
        string appProjectName,
        string appProjectDirectory,
        IReadOnlyCollection<string> enforceBranchOutcomeKeys,
        IReadOnlyCollection<string> evidenceBranchOutcomeKeys)
    {
        var paths = new[]
        {
            CombinePath(appProjectDirectory, "Layout", "NavMenu.razor"),
            CombinePath(appProjectDirectory, "Layout", "MainLayout.razor"),
            CombinePath(appProjectDirectory, "Pages", "Home.razor"),
            CombinePath(appProjectDirectory, "Pages", "Counter.razor"),
            CombinePath(appProjectDirectory, "Pages", "Weather.razor"),
            CombinePath(appProjectDirectory, "wwwroot", "sample-data", "weather.json"),
            CombinePath(appProjectDirectory, "Components", "Layout", "NavMenu.razor"),
            CombinePath(appProjectDirectory, "Components", "Layout", "MainLayout.razor"),
            CombinePath(appProjectDirectory, "Components", "Pages", "Home.razor"),
            CombinePath(appProjectDirectory, "Components", "Pages", "Counter.razor"),
            CombinePath(appProjectDirectory, "Components", "Pages", "Weather.razor")
        };
        var forbiddenText = new[]
        {
            "href=\"counter\"",
            "href=\"weather\"",
            "@page \"/counter\"",
            "@page \"/weather\"",
            "currentCount",
            "WeatherForecast",
            "sample-data/weather.json",
            "Welcome to your new app.",
            "Hello, world!",
            "learn.microsoft.com/aspnet/core/"
        };

        return paths.Select(path => new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["pathCandidates"] = new[] { path },
                ["mustExist"] = false,
                ["forbiddenTextAny"] = forbiddenText,
                ["description"] = $"{appProjectName} visible UI must not ship default template scaffold content."
            })
            .Select(check =>
            {
                if (enforceBranchOutcomeKeys.Count > 0)
                {
                    check["enforceBranchOutcomeKeys"] = enforceBranchOutcomeKeys;
                }

                if (evidenceBranchOutcomeKeys.Count > 0)
                {
                    check["evidenceBranchOutcomeKeys"] = evidenceBranchOutcomeKeys;
                }

                return check;
            })
            .Cast<object>()
            .ToArray();
    }

    private static string[] BuildBrowserRuntimeProofReceiptNames(bool requiresVisualTargetComparison)
    {
        var receipts = new List<string>
        {
            "workspace_dotnet_run",
            "browser_navigate",
            BrowserInteractionProofRequirement,
            "browser_evaluate",
            "browser_snapshot",
            "browser_take_screenshot",
            "browser_console_messages",
            "workspace_dotnet_stop"
        };
        if (requiresVisualTargetComparison)
        {
            receipts.Add("workspace_inspect_image");
            receipts.Add("workspace_analyze_image");
            receipts.Add("workspace_analyze_images");
        }

        return receipts.ToArray();
    }

    private static bool IsBrowserVisibleAppTemplate(string appTemplate)
        => string.Equals(appTemplate, "blazor", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(appTemplate, "blazorwasm", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(appTemplate, "mvc", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(appTemplate, "razor", StringComparison.OrdinalIgnoreCase);

    private static string CombinePath(string root, params string[] segments)
    {
        var separator = root.Contains('/') && !root.Contains('\\')
            ? "/"
            : "\\";
        var builder = new StringBuilder(root.TrimEnd('\\', '/'));
        foreach (var segment in segments.Where(segment => !string.IsNullOrWhiteSpace(segment)))
        {
            builder.Append(separator);
            builder.Append(segment.Trim('\\', '/'));
        }

        return builder.ToString();
    }
}

internal static class DotNetValidationReceiptPolicy
{
    public static string[] CreateRequiredReceiptNames()
        =>
        [
            "workspace_dotnet_restore",
            "workspace_dotnet_build",
            "workspace_dotnet_test"
        ];
}
