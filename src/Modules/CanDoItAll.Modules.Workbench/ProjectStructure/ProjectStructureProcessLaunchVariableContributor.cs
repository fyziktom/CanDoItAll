using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Workbench;

public interface IProjectStructureProcessLaunchVariableContributor
{
    void Enrich(ProjectStructureProcessLaunchVariableContext context, IDictionary<string, string> variables);
}

public sealed record ProjectStructureProcessLaunchVariableContext(
    Guid ProjectId,
    ProjectStructureSurface Surface,
    ProjectStructureNode ProjectNode,
    string? DefinitionKey,
    Guid? ProcessDefinitionId,
    ProcessRunId? ParentRunId,
    ProcessStepInstanceId? ParentStepId,
    ProcessRuntimeStepAssignment? ParentAssignment,
    bool IsSubprocess);

internal sealed partial class DotNetProcessLaunchVariableContributor : IProjectStructureProcessLaunchVariableContributor
{
    private const string DefaultTargetFramework = "net10.0";
    private const string DefaultTestTemplate = "xunit";
    private const string DefaultTestFramework = "xUnit";
    private const string SoftwareDeliveryDefinitionKey = "software-delivery";
    private const string RuntimeCommandWritebackDefinitionKey = "dotnet-runtime-command-writeback";
    private const string UiScreenshotWritebackDefinitionKey = "dotnet-ui-screenshot-writeback";
    private static readonly HashSet<string> SupportedRootDefinitionKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        SoftwareDeliveryDefinitionKey,
        "blazor-app-delivery",
        "blazor-app-repair-fix",
        "blazor-backend-feature",
        "blazor-frontend-feature",
        "blazor-fullstack-feature"
    };
    private static readonly HashSet<string> SupportedSubprocessDefinitionKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "dotnet-architecture-design-review",
        "dotnet-development-slice",
        "dotnet-feature-function-implementation",
        RuntimeCommandWritebackDefinitionKey,
        "dotnet-solution-setup",
        UiScreenshotWritebackDefinitionKey
    };

    public void Enrich(ProjectStructureProcessLaunchVariableContext context, IDictionary<string, string> variables)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(variables);

        if (string.IsNullOrWhiteSpace(context.DefinitionKey) ||
            !IsSupportedDefinition(context))
        {
            return;
        }

        if (string.Equals(context.DefinitionKey, RuntimeCommandWritebackDefinitionKey, StringComparison.OrdinalIgnoreCase))
        {
            SetIfNotEmpty(
                variables,
                ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceiptsByStep,
                BuildRuntimeCommandWritebackCompletionRequiredToolReceiptMap());
        }

        if (!TryResolveProductRoot(variables, out var productRoot))
        {
            return;
        }

        var contextText = BuildContextText(context, variables);
        var hasVisualTargetAssets = HasVisualTargetAssets(context, contextText);
        var solutionName = ResolveProjectIdentifier(context, variables);
        var appArchetype = ResolveAppArchetype(contextText);
        if (appArchetype is null)
        {
            return;
        }

        var appProjectName = ResolveVariable(variables, "DotNetAppProjectName");
        if (string.IsNullOrWhiteSpace(appProjectName))
        {
            appProjectName = solutionName;
        }

        var testProjectName = ResolveVariable(variables, "DotNetTestProjectName");
        if (string.IsNullOrWhiteSpace(testProjectName))
        {
            testProjectName = $"{appProjectName}.Tests";
        }

        var appProjectDirectory = CombinePath(productRoot, "src", appProjectName);
        var appProjectFile = CombinePath(appProjectDirectory, $"{appProjectName}.csproj");
        var appProjectFileAlias = AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias(appProjectFile) ?? string.Empty;
        var testProjectDirectory = CombinePath(productRoot, "tests", testProjectName);
        var testProjectFile = CombinePath(testProjectDirectory, $"{testProjectName}.csproj");
        var testProjectFileAlias = AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias(testProjectFile) ?? string.Empty;
        var targetFramework = ResolveTargetFramework(contextText);
        var solutionCandidatePaths = new[]
        {
            CombinePath(productRoot, $"{solutionName}.slnx"),
            CombinePath(productRoot, $"{solutionName}.sln")
        };
        var solutionFile = ResolveVariable(variables, "DotNetSolutionFile");
        if (string.IsNullOrWhiteSpace(solutionFile))
        {
            solutionFile = ResolveCanonicalSolutionFile(solutionCandidatePaths);
        }

        var solutionFileAlias = ResolveVariable(variables, "DotNetSolutionFileAlias");
        if (string.IsNullOrWhiteSpace(solutionFileAlias))
        {
            solutionFileAlias = AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias(solutionFile) ?? string.Empty;
        }

        var solutionCandidates = string.Join(
            "; ",
            solutionCandidatePaths);
        var workspaceAlias = AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias(productRoot) ?? string.Empty;

        AddIfMissing(variables, "DotNetSolutionName", solutionName);
        AddIfMissing(variables, "DotNetSolutionFile", solutionFile);
        AddIfMissing(variables, "DotNetSolutionFileAlias", solutionFileAlias);
        AddIfMissing(variables, "DotNetSolutionFileCandidates", solutionCandidates);
        AddIfMissing(variables, "DotNetAppProjectName", appProjectName);
        AddIfMissing(variables, "DotNetAppProjectDirectory", appProjectDirectory);
        AddIfMissing(variables, "DotNetAppProjectFile", appProjectFile);
        AddIfMissing(variables, "DotNetAppProjectFileAlias", appProjectFileAlias);
        AddIfMissing(variables, "DotNetAppArchetype", appArchetype.Archetype);
        AddIfMissing(variables, "DotNetAppTemplate", appArchetype.Template);
        AddIfMissing(variables, "DotNetAppTemplateOptions", appArchetype.TemplateOptions);
        AddIfMissing(variables, "DotNetAllowedTemplateSwitches", appArchetype.AllowedTemplateSwitches);
        AddIfMissing(variables, "DotNetTestProjectName", testProjectName);
        AddIfMissing(variables, "DotNetTestProjectDirectory", testProjectDirectory);
        AddIfMissing(variables, "DotNetTestProjectFile", testProjectFile);
        AddIfMissing(variables, "DotNetTestProjectFileAlias", testProjectFileAlias);
        AddIfMissing(variables, "DotNetTestTemplate", DefaultTestTemplate);
        AddIfMissing(variables, "DotNetTestFrameworkPreference", DefaultTestFramework);
        AddIfMissing(variables, "DotNetTargetFramework", targetFramework);
        if (string.Equals(context.DefinitionKey, "dotnet-solution-setup", StringComparison.OrdinalIgnoreCase))
        {
            SetIfNotEmpty(
                variables,
                ProcessRuntimeLaunchVariables.ProductCompletionRequiredPathsByStep,
                BuildSolutionSetupCompletionRequiredPathMap(
                    solutionName,
                    productRoot,
                    appProjectName,
                    appProjectDirectory,
                    testProjectName,
                    testProjectDirectory));
            SetIfNotEmpty(
                variables,
                ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceiptsByStep,
                BuildSolutionSetupCompletionRequiredToolReceiptMap(appArchetype.Template));
            SetIfNotEmpty(
                variables,
                ProcessRuntimeLaunchVariables.ProductCompletionRequiredFileContentChecksByStep,
                BuildSolutionSetupCompletionRequiredFileContentCheckMap(
                    solutionName,
                    productRoot,
                    appProjectName,
                    appProjectDirectory,
                    testProjectName,
                    testProjectDirectory));
            SetIfNotEmpty(
                variables,
                ProcessRuntimeLaunchVariables.ProcessStepScopedLaunchVariablePrefixesByStep,
                BuildSolutionSetupStepScopedLaunchVariablePrefixMap());
            var createScriptRef = "artifacts/process-runs/{CurrentProcessRunId}/scripts/create-dotnet-project.wire-solution.ps1";
            AddIfMissing(variables, "DotNetCreateProjectScriptRef", createScriptRef);
            AddIfMissing(
                variables,
                "DotNetCreateProjectScript",
                BuildCreateProjectScript(
                    solutionCandidatePaths,
                    productRoot,
                    appProjectName,
                    appProjectDirectory));
            AddIfMissing(
                variables,
                "DotNetCreateProjectSideEffectManifest",
                BuildCreateProjectSideEffectManifest(
                    solutionCandidatePaths,
                    appProjectName,
                    appProjectDirectory));
            AddIfMissing(
                variables,
                "DotNetCreateProjectExecutionPlan",
                BuildCreateProjectExecutionPlan(
                    workspaceAlias,
                    appProjectName,
                    appArchetype.Template,
                    createScriptRef));
            var addTestScriptRef = "artifacts/process-runs/{CurrentProcessRunId}/scripts/add-test-project.wire-solution.ps1";
            AddIfMissing(variables, "DotNetAddTestProjectScriptRef", addTestScriptRef);
            AddIfMissing(
                variables,
                "DotNetAddTestProjectScript",
                BuildAddTestProjectScript(
                    solutionCandidatePaths,
                    productRoot,
                    appProjectName,
                    appProjectDirectory,
                    testProjectName,
                    testProjectDirectory,
                    DefaultTestTemplate,
                    targetFramework));
            AddIfMissing(
                variables,
                "DotNetAddTestProjectSideEffectManifest",
                BuildAddTestProjectSideEffectManifest(
                    solutionCandidatePaths,
                    appProjectName,
                    appProjectDirectory,
                    testProjectName,
                    testProjectDirectory));
            AddIfMissing(
                variables,
                "DotNetAddTestProjectExecutionPlan",
                BuildAddTestProjectExecutionPlan(
                    workspaceAlias,
                    testProjectName,
                    DefaultTestTemplate,
                    addTestScriptRef));
        }
        else if (string.Equals(context.DefinitionKey, "dotnet-feature-function-implementation", StringComparison.OrdinalIgnoreCase))
        {
            SetIfNotEmpty(
                variables,
                ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceiptsByStep,
                BuildFeatureImplementationCompletionRequiredToolReceiptMap());
        }
        else if (string.Equals(context.DefinitionKey, SoftwareDeliveryDefinitionKey, StringComparison.OrdinalIgnoreCase))
        {
            SetIfNotEmpty(
                variables,
                ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceiptsByStep,
                BuildSoftwareDeliveryCompletionRequiredToolReceiptMap(appArchetype, hasVisualTargetAssets));
            SetIfNotEmpty(
                variables,
                ProcessRuntimeLaunchVariables.ProductCompletionRequiredFileContentChecksByStep,
                BuildSoftwareDeliveryCompletionRequiredFileContentCheckMap(
                    appArchetype,
                    appProjectName,
                    appProjectDirectory));
            SetIfNotEmpty(
                variables,
                ProcessRuntimeLaunchVariables.CompletionIssueRoutesByStep,
                BuildSoftwareDeliveryCompletionIssueRouteMap());
            if (TryBuildAcceptanceCriteriaMatrix(context, out var acceptanceCriteriaMatrix, out var acceptanceCriteriaContract))
            {
                AddAcceptanceCriteriaLaunchVariables(
                    variables,
                    acceptanceCriteriaMatrix,
                    acceptanceCriteriaContract,
                    acceptedBranchOutcomeKey: "quality-accepted");
            }
        }
        else if (IsBlazorDeliveryDefinitionKey(context.DefinitionKey))
        {
            SetIfNotEmpty(
                variables,
                ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceiptsByStep,
                BuildBlazorDeliveryCompletionRequiredToolReceiptMap(appArchetype, hasVisualTargetAssets));
            SetIfNotEmpty(
                variables,
                ProcessRuntimeLaunchVariables.ProductCompletionRequiredFileContentChecksByStep,
                BuildBlazorDeliveryCompletionRequiredFileContentCheckMap(
                    appArchetype,
                    appProjectName,
                    appProjectDirectory));
            SetIfNotEmpty(
                variables,
                ProcessRuntimeLaunchVariables.CompletionIssueRoutesByStep,
                BuildBlazorDeliveryCompletionIssueRouteMap());
            if (TryBuildAcceptanceCriteriaMatrix(context, out var acceptanceCriteriaMatrix, out var acceptanceCriteriaContract))
            {
                AddAcceptanceCriteriaLaunchVariables(
                    variables,
                    acceptanceCriteriaMatrix,
                    acceptanceCriteriaContract,
                    acceptedBranchOutcomeKey: "quality-accepted");
            }
        }
        else if (string.Equals(context.DefinitionKey, RuntimeCommandWritebackDefinitionKey, StringComparison.OrdinalIgnoreCase))
        {
            SetIfNotEmpty(
                variables,
                ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceiptsByStep,
                BuildRuntimeCommandWritebackCompletionRequiredToolReceiptMap());
        }
        else if (string.Equals(context.DefinitionKey, UiScreenshotWritebackDefinitionKey, StringComparison.OrdinalIgnoreCase) &&
                 IsBrowserVisibleAppArchetype(appArchetype))
        {
            SetIfNotEmpty(
                variables,
                ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceiptsByStep,
                BuildUiScreenshotWritebackCompletionRequiredToolReceiptMap(hasVisualTargetAssets));
        }

        AddIfMissing(variables, "DotNetWorkspaceAlias", workspaceAlias);
        AddIfMissing(
            variables,
            "DotNetScaffoldContractSource",
            "Inferred from current project-structure .NET target, product root, and CanDoItAll repository test-framework convention.");
        AddIfMissing(
            variables,
            "DotNetScaffoldContract",
            BuildContract(
                solutionName,
                solutionFile,
                solutionFileAlias,
                solutionCandidates,
                appProjectName,
                appProjectDirectory,
                appArchetype,
                testProjectName,
                testProjectDirectory,
                targetFramework,
                productRoot,
                workspaceAlias));
    }

    private static bool IsSupportedDefinition(ProjectStructureProcessLaunchVariableContext context)
        => context.IsSubprocess
            ? SupportedSubprocessDefinitionKeys.Contains(context.DefinitionKey!)
            : SupportedRootDefinitionKeys.Contains(context.DefinitionKey!);

    private static bool IsBlazorDeliveryDefinitionKey(string? definitionKey)
        => !string.IsNullOrWhiteSpace(definitionKey) &&
           !string.Equals(definitionKey, SoftwareDeliveryDefinitionKey, StringComparison.OrdinalIgnoreCase) &&
           SupportedRootDefinitionKeys.Contains(definitionKey);

    private static bool TryResolveProductRoot(IDictionary<string, string> variables, out string productRoot)
    {
        productRoot = FirstNonEmpty(
            ResolveVariable(variables, "ProductRoot"),
            ResolveVariable(variables, "OutputRoot"));

        return !string.IsNullOrWhiteSpace(productRoot);
    }

    private static string ResolveProjectIdentifier(
        ProjectStructureProcessLaunchVariableContext context,
        IDictionary<string, string> variables)
    {
        var candidates = new[]
        {
            ResolveVariable(variables, "DotNetSolutionName"),
            ResolveVariable(variables, "ProjectName"),
            context.Surface.ProjectName,
            context.ProjectNode.Title,
            ResolveProductRootLeaf(ResolveVariable(variables, "ProductRoot")),
            ResolveProductRootLeaf(ResolveVariable(variables, "OutputRoot"))
        };

        foreach (var candidate in candidates)
        {
            var identifier = ToIdentifier(candidate);
            if (!string.IsNullOrWhiteSpace(identifier))
            {
                return identifier;
            }
        }

        return "GeneratedApp";
    }

    private static DotNetScaffoldArchetype? ResolveAppArchetype(string contextText)
    {
        if (ContainsAny(contextText, "Blazor WebAssembly", "Blazor WASM"))
        {
            var isPwa = ContainsAny(contextText, "PWA", "Progressive Web App", "offline-friendly", "static-host");
            return new DotNetScaffoldArchetype(
                isPwa ? "Blazor WebAssembly PWA" : "Blazor WebAssembly",
                "blazorwasm",
                isPwa ? "--pwa" : string.Empty,
                isPwa ? "--pwa" : string.Empty);
        }

        if (ContainsAny(contextText, "Blazor SSR", "Blazor Server", "Blazor Web App"))
        {
            return new DotNetScaffoldArchetype("Blazor Web App", "blazor", string.Empty, string.Empty);
        }

        if (ContainsAny(contextText, "web api", "webapi", "http api", "backend api"))
        {
            return new DotNetScaffoldArchetype("ASP.NET Core Web API", "webapi", string.Empty, string.Empty);
        }

        if (ContainsAny(contextText, "worker service", "background worker"))
        {
            return new DotNetScaffoldArchetype(".NET worker service", "worker", string.Empty, string.Empty);
        }

        if (ContainsAny(contextText, "console app", "command-line", "cli"))
        {
            return new DotNetScaffoldArchetype(".NET console app", "console", string.Empty, string.Empty);
        }

        if (ContainsAny(contextText, "class library", "library"))
        {
            return new DotNetScaffoldArchetype(".NET class library", "classlib", string.Empty, string.Empty);
        }

        return null;
    }

    private static string ResolveTargetFramework(string contextText)
    {
        var match = TargetFrameworkRegex().Match(contextText);
        return match.Success
            ? match.Value.ToLowerInvariant()
            : DefaultTargetFramework;
    }

    private static string BuildContextText(
        ProjectStructureProcessLaunchVariableContext context,
        IDictionary<string, string> variables)
    {
        var builder = new StringBuilder();
        AppendLine(builder, ResolveVariable(variables, "ProjectStructureContextSummary"));
        AppendLine(builder, context.Surface.ProjectName);
        AppendLine(builder, context.ProjectNode.Title);
        AppendLine(builder, context.ProjectNode.Subtitle);
        AppendLine(builder, context.ProjectNode.Notes);

        foreach (var node in context.Surface.Nodes.Where(ProjectStructureProcessContextNodeFilter.ShouldIncludeInProcessContext))
        {
            AppendLine(builder, node.Title);
            AppendLine(builder, node.Subtitle);
            AppendLine(builder, node.Notes);
            AppendLine(builder, node.ObjectSubtype);
        }

        return builder.ToString();
    }

    private static string BuildContract(
        string solutionName,
        string solutionFile,
        string solutionFileAlias,
        string solutionCandidates,
        string appProjectName,
        string appProjectDirectory,
        DotNetScaffoldArchetype appArchetype,
        string testProjectName,
        string testProjectDirectory,
        string targetFramework,
        string productRoot,
        string workspaceAlias)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"SolutionName: {solutionName}");
        builder.AppendLine($"SolutionFile: {solutionFile}");
        if (!string.IsNullOrWhiteSpace(solutionFileAlias))
        {
            builder.AppendLine($"SolutionFileAlias: {solutionFileAlias}");
        }

        builder.AppendLine($"SolutionFileCandidates: {solutionCandidates}");
        builder.AppendLine($"AppProjectName: {appProjectName}");
        builder.AppendLine($"AppProjectDirectory: {appProjectDirectory}");
        builder.AppendLine($"AppProjectFile: {CombinePath(appProjectDirectory, $"{appProjectName}.csproj")}");
        var appProjectFileAlias = AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias(CombinePath(appProjectDirectory, $"{appProjectName}.csproj"));
        if (!string.IsNullOrWhiteSpace(appProjectFileAlias))
        {
            builder.AppendLine($"AppProjectFileAlias: {appProjectFileAlias}");
        }

        builder.AppendLine($"AppArchetype: {appArchetype.Archetype}");
        builder.AppendLine($"AppTemplate: {appArchetype.Template}");
        builder.AppendLine($"AppTemplateOptions: {appArchetype.TemplateOptions}");
        builder.AppendLine($"AllowedTemplateSwitches: {appArchetype.AllowedTemplateSwitches}");
        builder.AppendLine($"SolutionScaffoldToolContract: use workspace_dotnet_new with template 'sln', parentDirectory set to WorkspaceAlias, and name '{solutionName}'. Never use the product root parent folder as the solution scaffold parent.");
        builder.AppendLine($"ScaffoldToolContract: use workspace_dotnet_new with template '{BuildTemplateSpec(appArchetype)}' for the app project; do not hand-author SDK/package scaffolding unless repairing an existing project in place.");
        builder.AppendLine("SolutionValidationTargetRule: restore and build validation must target DotNetSolutionFile or DotNetSolutionFileAlias when present. Test validation must target DotNetTestProjectFileAlias or DotNetTestProjectFile when present, preferably with noBuild=true after a successful solution build; fall back to the solution target only when no test project target exists. Do not infer <SolutionName>.sln from SolutionName, and do not report a missing .sln when the canonical .slnx validation target exists or has successful current-run restore/build receipts. If the canonical target is absent, list SolutionFileCandidates and use the existing candidate, preferring .slnx when both exist.");
        builder.AppendLine("ExternalTargetScriptRule: external-target/... aliases are only for structured workspace tool path arguments. Do not put external-target aliases inside PowerShell, Python, or shell script content; scripts must use the native absolute ProductRoot or DotNet* launch variable paths.");
        builder.AppendLine("EvidenceSourceRule: cite project-media file paths only when they are present in current launch variables, current prompt context, inherited upstream artifacts, or current-run tool receipts. Do not introduce source document paths from unrelated projects or prior runs.");
        if (string.Equals(appArchetype.Template, "blazorwasm", StringComparison.OrdinalIgnoreCase))
        {
            builder.AppendLine($"BlazorWasmNamespaceRule: Program.cs, App.razor, _Imports.razor, Pages/NotFound.razor, and Layout/MainLayout.razor must retain resolvable root component and layout namespace wiring. If Program.cs uses RootComponents.Add<App>, it must import the app root namespace with `using {appProjectName};` or use a fully qualified `{appProjectName}.App`. _Imports.razor must include `@using {appProjectName}` and, when MainLayout is referenced by short name, `@using {appProjectName}.Layout`. Do not return Completed while CS0246 remains for App, MainLayout, Routes, HeadOutlet, or Router.");
            builder.AppendLine("BlazorWasmTemplateIntegrityRule: Program.cs, App.razor, and _Imports.razor must retain the current blazorwasm template wiring, including Microsoft.AspNetCore.Components.Routing imports and a resolvable App root component.");
            if (ContainsAny(appArchetype.AllowedTemplateSwitches, "--pwa"))
            {
                builder.AppendLine("PackageRule: do not add PackageReference Include=\"Microsoft.AspNetCore.Components.WebAssembly.PWA\"; Blazor WebAssembly PWA support comes from the template-generated assets, not a NuGet package.");
                builder.AppendLine("BlazorWasmPwaTemplateIntegrityRule: template-generated PWA assets must be retained when the scaffold was created with --pwa.");
            }
        }

        builder.AppendLine("ExistingScaffoldRule: existing files are not enough; if the app project already exists, compare template-critical files to the current template baseline and repair stale or hand-authored scaffold drift before first build validation.");
        builder.AppendLine($"TestProjectName: {testProjectName}");
        builder.AppendLine($"TestProjectDirectory: {testProjectDirectory}");
        var testProjectFile = CombinePath(testProjectDirectory, $"{testProjectName}.csproj");
        builder.AppendLine($"TestProjectFile: {testProjectFile}");
        var testProjectFileAlias = AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias(testProjectFile);
        if (!string.IsNullOrWhiteSpace(testProjectFileAlias))
        {
            builder.AppendLine($"TestProjectFileAlias: {testProjectFileAlias}");
        }

        builder.AppendLine($"TestTemplate: {DefaultTestTemplate}");
        builder.AppendLine($"TestFrameworkPreference: {DefaultTestFramework}");
        builder.AppendLine($"TargetFramework: {targetFramework}");
        builder.AppendLine($"ProductRoot: {productRoot}");
        if (!string.IsNullOrWhiteSpace(workspaceAlias))
        {
            builder.AppendLine($"WorkspaceAlias: {workspaceAlias}");
        }

        builder.AppendLine("StructuredWorkspacePathRule: use WorkspaceAlias or external-target/... aliases in workspace_* tool path arguments, including workspace_pwsh_run_script workingDirectory; use ProductRoot only inside approved ProductMutation scripts, sideEffectManifest read/write declarations, and native dotnet command arguments.");
        builder.AppendLine("DotNetRunProjectTargetRule: workspace_dotnet_run targetPath must be DotNetAppProjectFileAlias when present, otherwise DotNetAppProjectFile. Never call workspace_dotnet_run with DotNetSolutionFile, DotNetSolutionFileAlias, ProductRoot, WorkspaceAlias, DotNetAppProjectDirectory, or a directory path; the tool accepts only .csproj, .fsproj, or .vbproj project file targets.");
        builder.AppendLine("WorkspaceScriptPathRule: when invoking workspace_pwsh_run_script, path must be the reviewed current-run .ps1 helper path, for example CurrentManagedArtifactRoot/scripts/<step-key>.ps1. Never pass the primary step markdown artifact under steps/*.md as the script path.");
        builder.AppendLine($"SolutionMembershipScript: create-dotnet-project must leave the solution containing the app project before it completes. Use the provided DotNetCreateProjectScript helper after scaffold tool calls; it chooses an existing solution from {solutionCandidates}, executes dotnet sln <solution-file> add \"{CombinePath(appProjectDirectory, $"{appProjectName}.csproj")}\" idempotently, executes dotnet sln <solution-file> list, converts the list output to one scalar string before checks, computes the product-root-relative app project path, normalizes both '\\' and '/' separators, and fails only if neither the relative nor normalized project path is listed. add-test-project still owns test-project creation and ProjectReference wiring. Do not compare only the native absolute project path because dotnet sln list normally emits solution-relative paths. In PowerShell, do not test an output-line array with -notmatch directly; use a joined string such as ($listOutput -join [Environment]::NewLine). If workspace_pwsh_run_script uses arguments for product paths, those argument values are consumed by PowerShell and must be native absolute ProductRoot/DotNet* paths, not external-target aliases. If workspace_pwsh_run_script is denied because the helper path did not exist or could not be inspected, write or verify the helper .ps1 path and retry workspace_pwsh_run_script before returning Blocked.");
        builder.AppendLine($"MandatoryTestWiringScript: create a reviewed current-run artifact PowerShell helper with workspace_write_file, verify that helper .ps1 path with workspace_stat_path or workspace_read_file, then run it with workspace_pwsh_run_script using the same helper .ps1 path. The helper must create the missing test project with dotnet new {DefaultTestTemplate} before wiring, choose the existing contracted solution, add the app project \"{CombinePath(appProjectDirectory, $"{appProjectName}.csproj")}\" first when solution membership is missing, add \"{CombinePath(testProjectDirectory, $"{testProjectName}.csproj")}\" to the same solution, execute dotnet add \"{CombinePath(testProjectDirectory, $"{testProjectName}.csproj")}\" reference \"{CombinePath(appProjectDirectory, $"{appProjectName}.csproj")}\", execute dotnet sln <solution-file> list, convert command output to scalar strings before membership or ProjectReference regex checks, compute product-root-relative app/test project paths and the test-project-relative ProjectReference path, normalize both '\\' and '/' separators, and fail only after the create/repair commands have run and either project or the ProjectReference is still missing. Do not preflight-fail only because the app project is absent from the solution; repair that membership in this step when the app project file exists. If workspace_pwsh_run_script uses arguments for product paths, those argument values are consumed by PowerShell and must be native absolute ProductRoot/DotNet* paths, not external-target aliases. In PowerShell, do not test output-line arrays with -notmatch directly; join them before matching. If workspace_pwsh_run_script is denied because the helper path did not exist or could not be inspected, write or verify the helper .ps1 path and retry workspace_pwsh_run_script before returning Blocked.");
        builder.AppendLine("ProductMutationScriptManifest: pass sideEffectManifest as an object or JSON with version=1, mode=ProductMutation, declaredReadPaths/declaredWritePaths containing native absolute solution and project paths, and allowShellDelegation=true when invoking dotnet. Do not use external-target aliases inside script content.");
        builder.AppendLine("ProductCompletionRequiredFileContentChecks: create-dotnet-project, add-test-project, and repair setup steps have hard readback gates for solution membership and project-reference file content. Do not report create-dotnet-project Completed until the solution contains the app project. Do not report add-test-project or repair-solution-setup Completed until their required file content checks are satisfied.");
        builder.Append("Layout: solution file at ProductRoot, app under ProductRoot/src, tests under ProductRoot/tests.");
        return builder.ToString();
    }

    private static string BuildTemplateSpec(DotNetScaffoldArchetype appArchetype)
        => string.IsNullOrWhiteSpace(appArchetype.TemplateOptions)
            ? appArchetype.Template
            : $"{appArchetype.Template} {appArchetype.TemplateOptions}";

    private static string ResolveCanonicalSolutionFile(IReadOnlyList<string> solutionCandidatePaths)
    {
        foreach (var candidate in solutionCandidatePaths)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return solutionCandidatePaths.First();
    }

    private static string BuildSolutionSetupCompletionRequiredPathMap(
        string solutionName,
        string productRoot,
        string appProjectName,
        string appProjectDirectory,
        string testProjectName,
        string testProjectDirectory)
    {
        var solutionFile = CombinePath(productRoot, $"{solutionName}.slnx");
        var appProjectFile = CombinePath(appProjectDirectory, $"{appProjectName}.csproj");
        var testProjectFile = CombinePath(testProjectDirectory, $"{testProjectName}.csproj");
        var map = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["create-dotnet-project"] = [solutionFile, appProjectFile],
            ["add-test-project"] = [solutionFile, appProjectFile, testProjectFile],
            ["repair-solution-setup"] = [solutionFile, appProjectFile, testProjectFile]
        };

        return JsonSerializer.Serialize(map);
    }

    private static string BuildSolutionSetupCompletionRequiredToolReceiptMap(string appTemplate)
    {
        var appTemplateReceipt = string.IsNullOrWhiteSpace(appTemplate)
            ? "workspace_dotnet_new"
            : $"template={appTemplate.Trim()}";
        var map = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["create-dotnet-project"] = ["template=sln", appTemplateReceipt, "workspace_pwsh_run_script"],
            ["add-test-project"] = ["workspace_pwsh_run_script"],
            ["repair-solution-setup"] = ["workspace_pwsh_run_script"],
            ["validate-first-build"] = ["workspace_dotnet_restore", "workspace_dotnet_build", "workspace_dotnet_test"],
            ["validate-first-build-after-repair"] = ["workspace_dotnet_restore", "workspace_dotnet_build", "workspace_dotnet_test"]
        };

        return JsonSerializer.Serialize(map);
    }

    private static string BuildFeatureImplementationCompletionRequiredToolReceiptMap()
    {
        var validationReceipts = BuildDotNetValidationReceiptNames();
        var map = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["targeted-validation"] = validationReceipts,
            ["feature-repair"] = validationReceipts,
            ["targeted-recheck"] = validationReceipts
        };

        return JsonSerializer.Serialize(map);
    }

    private static string BuildSoftwareDeliveryCompletionRequiredToolReceiptMap(
        DotNetScaffoldArchetype appArchetype,
        bool requiresVisualTargetComparison)
    {
        var validationReceipts = BuildDotNetValidationReceiptNames();
        var qaReceipts = IsBrowserVisibleAppArchetype(appArchetype)
            ? validationReceipts.Concat(BuildBrowserRuntimeProofReceiptNames(requiresVisualTargetComparison)).ToArray()
            : validationReceipts;
        var map = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["qa-validation"] = BuildBranchAwareReceiptRules(
                qaReceipts,
                ["quality-accepted"],
                "AcceptanceProof"),
            ["quality-repair"] = qaReceipts,
            ["qa-recheck"] = BuildBranchAwareReceiptRules(
                qaReceipts,
                ["quality-accepted"],
                "AcceptanceProof")
        };

        return JsonSerializer.Serialize(map);
    }

    private static string BuildSoftwareDeliveryCompletionIssueRouteMap()
    {
        var map = new Dictionary<string, object[]>(StringComparer.Ordinal)
        {
            ["qa-validation"] =
            [
                BuildBranchRoute(
                    "process.adapter.product_required_file_content_missing",
                    ["quality-accepted"],
                    "repair-required",
                    "Repair required",
                    requiresDefectEvidence: true),
                BuildBranchRoute(
                    ProcessCompletionDiagnosticCodes.ToolReceiptEvidenceContentRejected,
                    ["quality-accepted"],
                    "repair-required",
                    "Repair required",
                    requiresDefectEvidence: true)
            ],
            ["qa-recheck"] =
            [
                BuildBranchRoute(
                    "process.adapter.product_required_file_content_missing",
                    ["quality-accepted"],
                    "repair-escalation",
                    "Repair escalation",
                    requiresDefectEvidence: true),
                BuildBranchRoute(
                    ProcessCompletionDiagnosticCodes.ToolReceiptEvidenceContentRejected,
                    ["quality-accepted"],
                    "repair-escalation",
                    "Repair escalation",
                    requiresDefectEvidence: true)
            ]
        };

        return JsonSerializer.Serialize(map);
    }

    private static string BuildBlazorDeliveryCompletionRequiredToolReceiptMap(
        DotNetScaffoldArchetype appArchetype,
        bool requiresVisualTargetComparison)
    {
        var validationReceipts = BuildDotNetValidationReceiptNames();
        var qaReceipts = IsBrowserVisibleAppArchetype(appArchetype)
            ? validationReceipts.Concat(BuildBrowserRuntimeProofReceiptNames(requiresVisualTargetComparison)).ToArray()
            : validationReceipts;
        var map = new Dictionary<string, object>(StringComparer.Ordinal)
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
        };

        return JsonSerializer.Serialize(map);
    }

    private static string BuildBlazorDeliveryCompletionIssueRouteMap()
    {
        var map = new Dictionary<string, object[]>(StringComparer.Ordinal)
        {
            ["validate-blazor-runtime"] =
            [
                BuildBranchRoute(
                    "process.adapter.product_required_file_content_missing",
                    ["quality-accepted"],
                    "repair-required",
                    "Repair required",
                    requiresDefectEvidence: true),
                BuildBranchRoute(
                    ProcessCompletionDiagnosticCodes.ToolReceiptEvidenceContentRejected,
                    ["quality-accepted"],
                    "repair-required",
                    "Repair required",
                    requiresDefectEvidence: true)
            ],
            ["revalidate-blazor-repair"] =
            [
                BuildBranchRoute(
                    "process.adapter.product_required_file_content_missing",
                    ["quality-accepted"],
                    "repair-escalation",
                    "Repair escalation",
                    requiresDefectEvidence: true),
                BuildBranchRoute(
                    ProcessCompletionDiagnosticCodes.ToolReceiptEvidenceContentRejected,
                    ["quality-accepted"],
                    "repair-escalation",
                    "Repair escalation",
                    requiresDefectEvidence: true)
            ]
        };

        return JsonSerializer.Serialize(map);
    }

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

    private static object BuildBranchRoute(
        string issueCode,
        IReadOnlyList<string> sourceBranchOutcomeKeys,
        string targetBranchOutcomeKey,
        string targetBranchOutcomeTitle,
        bool requiresDefectEvidence)
        => new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["issueCode"] = issueCode,
            ["sourceBranchOutcomeKeys"] = sourceBranchOutcomeKeys,
            ["targetBranchOutcomeKey"] = targetBranchOutcomeKey,
            ["targetBranchOutcomeTitle"] = targetBranchOutcomeTitle,
            ["requiresDefectEvidence"] = requiresDefectEvidence
        };

    private static bool TryBuildAcceptanceCriteriaMatrix(
        ProjectStructureProcessLaunchVariableContext context,
        out ProcessAcceptanceCriteriaMatrix matrix,
        out string contract)
    {
        matrix = new ProcessAcceptanceCriteriaMatrix();
        contract = string.Empty;

        var criteria = EnumerateAcceptanceCriteriaCandidates(context)
            .Select((candidate, index) => new ProcessAcceptanceCriterion
            {
                Id = ProcessAcceptanceCriteriaIds.FromIndex(index),
                SourceNodeId = candidate.NodeId,
                Summary = candidate.Summary,
                VerificationMethods = BuildAcceptanceVerificationMethods(candidate.Summary).ToList(),
                RequiredForAcceptance = true
            })
            .ToArray();
        if (criteria.Length == 0)
        {
            return false;
        }

        matrix.Criteria.AddRange(criteria);
        contract = BuildAcceptanceCriteriaContract(criteria);
        return true;
    }

    private static void AddAcceptanceCriteriaLaunchVariables(
        IDictionary<string, string> variables,
        ProcessAcceptanceCriteriaMatrix acceptanceCriteriaMatrix,
        string acceptanceCriteriaContract,
        string acceptedBranchOutcomeKey)
    {
        AddIfMissing(
            variables,
            ProcessRuntimeLaunchVariables.AcceptanceCriteriaMatrix,
            ProcessAcceptanceCriteriaMatrixJson.Serialize(acceptanceCriteriaMatrix));
        AddIfMissing(
            variables,
            ProcessRuntimeLaunchVariables.AcceptanceCriteriaAcceptedBranchOutcomeKeys,
            acceptedBranchOutcomeKey);
        AddIfMissing(
            variables,
            "ProductAcceptanceCriteriaContract",
            acceptanceCriteriaContract);
    }

    private static IReadOnlyList<AcceptanceCriteriaCandidate> EnumerateAcceptanceCriteriaCandidates(
        ProjectStructureProcessLaunchVariableContext context)
    {
        var candidates = new List<AcceptanceCriteriaCandidate>();
        foreach (var node in context.Surface.Nodes.Where(ProjectStructureProcessContextNodeFilter.ShouldIncludeInProcessContext))
        {
            AddAcceptanceCriteriaCandidates(node, candidates);
        }

        if (candidates.Count == 0 &&
            ProjectStructureProcessContextNodeFilter.ShouldIncludeInProcessContext(context.ProjectNode))
        {
            AddAcceptanceCriteriaCandidates(context.ProjectNode, candidates);
        }

        return candidates
            .DistinctBy(candidate => NormalizeCriteriaSummary(candidate.Summary))
            .Take(20)
            .ToArray();
    }

    private static void AddAcceptanceCriteriaCandidates(
        ProjectStructureNode node,
        List<AcceptanceCriteriaCandidate> candidates)
    {
        if (!HasExplicitAcceptanceCriteriaSignal(node))
        {
            return;
        }

        foreach (var summary in ExtractAcceptanceCriteriaSummaries(node))
        {
            candidates.Add(new AcceptanceCriteriaCandidate(node.Id, summary));
        }
    }

    private static bool HasExplicitAcceptanceCriteriaSignal(ProjectStructureNode node)
    {
        var text = string.Join(" ", node.Title, node.Subtitle, node.Notes, node.ObjectSubtype, node.Badges);
        return ContainsAny(
            text,
            "acceptance criteria",
            "acceptance requirement",
            "acceptance requirements",
            "must support",
            "must include",
            "must provide",
            "must allow",
            "must reject",
            "negative case",
            "quality gate",
            "proof required",
            "definition of done");
    }

    private static IReadOnlyList<string> ExtractAcceptanceCriteriaSummaries(ProjectStructureNode node)
    {
        var sourceText = string.Join(
            Environment.NewLine,
            node.Title,
            node.Subtitle,
            node.Notes);
        var explicitSectionLines = ExtractExplicitAcceptanceCriteriaSection(sourceText);
        var segments = explicitSectionLines.Count > 0
            ? explicitSectionLines
            : SplitCriteriaText(sourceText);

        return segments
            .Select(CleanAcceptanceCriterion)
            .Where(IsSubstantiveAcceptanceCriterion)
            .Take(12)
            .ToArray();
    }

    private static IReadOnlyList<string> ExtractExplicitAcceptanceCriteriaSection(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var lines = text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.TrimEntries)
            .ToArray();
        var selected = new List<string>();
        var inSection = false;
        foreach (var line in lines)
        {
            if (line.Contains("acceptance criteria", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("definition of done", StringComparison.OrdinalIgnoreCase))
            {
                inSection = true;
                var tail = Regex.Replace(
                    line,
                    ".*?(acceptance criteria|definition of done)\\s*[:\\-]\\s*",
                    string.Empty,
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                if (!string.IsNullOrWhiteSpace(tail) &&
                    !string.Equals(tail, line, StringComparison.Ordinal))
                {
                    selected.Add(tail);
                }

                continue;
            }

            if (!inSection)
            {
                continue;
            }

            if (LooksLikeSectionHeader(line) &&
                selected.Count > 0)
            {
                break;
            }

            selected.Add(line);
        }

        return selected.Count == 0
            ? []
            : SplitCriteriaText(string.Join(Environment.NewLine, selected));
    }

    private static IReadOnlyList<string> SplitCriteriaText(string text)
        => string.IsNullOrWhiteSpace(text)
            ? []
            : Regex.Split(text, @"(?:\r?\n)+|(?:^|\s)[-*]\s+|;\s+|\.\s+(?=[A-Z0-9])", RegexOptions.CultureInvariant)
                .Select(value => value.Trim())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToArray();

    private static string CleanAcceptanceCriterion(string value)
    {
        var cleaned = Regex.Replace(
            value.Trim(),
            @"^\s*(?:[-*]|\d+[\).\:]|AC[-\s]?\d+[\).\:]?)\s*",
            string.Empty,
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(
            cleaned,
            @"^(?:must|should|shall)\s+",
            string.Empty,
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        return cleaned.Trim();
    }

    private static bool IsSubstantiveAcceptanceCriterion(string value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            value.Length < 18 ||
            value.Length > 260)
        {
            return false;
        }

        if (!ContainsAny(
                value,
                "must",
                "support",
                "allow",
                "provide",
                "render",
                "show",
                "display",
                "validate",
                "reject",
                "persist",
                "score",
                "clear",
                "pause",
                "resume",
                "keyboard",
                "mouse",
                "touch",
                "gameplay",
                "negative",
                "error",
                "proof"))
        {
            return false;
        }

        return !ContainsAny(
            value,
            "Blazor WebAssembly",
            "xUnit tests",
            "ProductRoot",
            "ProjectStructureContextSummary");
    }

    private static bool LooksLikeSectionHeader(string line)
        => !string.IsNullOrWhiteSpace(line) &&
           line.Length <= 80 &&
           line.EndsWith(':') &&
           !line.Contains("must", StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<string> BuildAcceptanceVerificationMethods(string summary)
    {
        var methods = new List<string>();
        if (ContainsAny(summary, "click", "keyboard", "mouse", "touch", "screen", "render", "display", "show", "ui", "gameplay"))
        {
            methods.Add("browser-proof");
        }

        if (ContainsAny(summary, "test", "validate", "score", "clear", "reject", "negative", "error"))
        {
            methods.Add("unit-test");
        }

        if (methods.Count == 0)
        {
            methods.Add("source-inspection");
        }

        return methods;
    }

    private static string BuildAcceptanceCriteriaContract(IReadOnlyList<ProcessAcceptanceCriterion> criteria)
    {
        var builder = new StringBuilder();
        builder.AppendLine("AcceptanceCriteriaContract: completed accepted-branch QA must cite each required acceptance criterion id with concrete evidence. Screenshots or runtime receipts alone are insufficient unless tied to criterion ids.");
        foreach (var criterion in criteria)
        {
            builder.AppendLine($"{criterion.Id}: {criterion.Summary} [proof={string.Join(",", criterion.VerificationMethods)}]");
        }

        return builder.ToString().Trim();
    }

    private static string NormalizeCriteriaSummary(string value)
        => Regex.Replace(value.Trim().ToLowerInvariant(), @"\s+", " ", RegexOptions.CultureInvariant);

    private static string BuildRuntimeCommandWritebackCompletionRequiredToolReceiptMap()
    {
        var map = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["write-run-command-nodes"] = ["project_structure_node_create", "project_structure_read"]
        };

        return JsonSerializer.Serialize(map);
    }

    private static string BuildUiScreenshotWritebackCompletionRequiredToolReceiptMap(bool requiresVisualTargetComparison)
    {
        var storeScreenshotReceipts = new List<string>
        {
            "workspace_inspect_image",
            "workspace_analyze_image",
            "project_structure_node_create",
            "project_structure_asset_create"
        };
        if (requiresVisualTargetComparison)
        {
            storeScreenshotReceipts.Insert(2, "workspace_analyze_images");
        }

        var map = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["capture-ui-screenshots"] =
            [
                "workspace_dotnet_run",
                "browser_navigate",
                "browser_snapshot",
                "browser_take_screenshot",
                "browser_console_messages",
                "workspace_dotnet_stop"
            ],
            ["store-ui-screenshots"] = storeScreenshotReceipts.ToArray()
        };

        return JsonSerializer.Serialize(map);
    }

    private static string[] BuildBrowserRuntimeProofReceiptNames(bool requiresVisualTargetComparison)
    {
        var receipts = new List<string>
        {
            "workspace_dotnet_run",
            "browser_navigate",
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

    private static string BuildSoftwareDeliveryCompletionRequiredFileContentCheckMap(
        DotNetScaffoldArchetype appArchetype,
        string appProjectName,
        string appProjectDirectory)
    {
        if (!IsBrowserVisibleAppArchetype(appArchetype))
        {
            return string.Empty;
        }

        var acceptanceChecks = BuildVisibleUiScaffoldRemovalChecks(
            appProjectName,
            appProjectDirectory,
            enforceBranchOutcomeKeys: ["quality-accepted"],
            evidenceBranchOutcomeKeys: ["repair-required"]);
        var recheckAcceptanceChecks = BuildVisibleUiScaffoldRemovalChecks(
            appProjectName,
            appProjectDirectory,
            enforceBranchOutcomeKeys: ["quality-accepted"],
            evidenceBranchOutcomeKeys: ["repair-escalation"]);
        var repairCompletionChecks = BuildVisibleUiScaffoldRemovalChecks(
            appProjectName,
            appProjectDirectory,
            enforceBranchOutcomeKeys: [],
            evidenceBranchOutcomeKeys: []);
        if (acceptanceChecks.Length == 0 ||
            recheckAcceptanceChecks.Length == 0 ||
            repairCompletionChecks.Length == 0)
        {
            return string.Empty;
        }

        var map = new Dictionary<string, object[]>(StringComparer.Ordinal)
        {
            ["qa-validation"] = acceptanceChecks,
            ["quality-repair"] = repairCompletionChecks,
            ["qa-recheck"] = recheckAcceptanceChecks
        };

        return JsonSerializer.Serialize(map);
    }

    private static string BuildBlazorDeliveryCompletionRequiredFileContentCheckMap(
        DotNetScaffoldArchetype appArchetype,
        string appProjectName,
        string appProjectDirectory)
    {
        if (!IsBrowserVisibleAppArchetype(appArchetype))
        {
            return string.Empty;
        }

        var acceptanceChecks = BuildVisibleUiScaffoldRemovalChecks(
            appProjectName,
            appProjectDirectory,
            enforceBranchOutcomeKeys: ["quality-accepted"],
            evidenceBranchOutcomeKeys: ["repair-required"]);
        var recheckAcceptanceChecks = BuildVisibleUiScaffoldRemovalChecks(
            appProjectName,
            appProjectDirectory,
            enforceBranchOutcomeKeys: ["quality-accepted"],
            evidenceBranchOutcomeKeys: ["repair-escalation"]);
        var repairCompletionChecks = BuildVisibleUiScaffoldRemovalChecks(
            appProjectName,
            appProjectDirectory,
            enforceBranchOutcomeKeys: [],
            evidenceBranchOutcomeKeys: []);
        if (acceptanceChecks.Length == 0 ||
            recheckAcceptanceChecks.Length == 0 ||
            repairCompletionChecks.Length == 0)
        {
            return string.Empty;
        }

        var map = new Dictionary<string, object[]>(StringComparer.Ordinal)
        {
            ["validate-blazor-runtime"] = acceptanceChecks,
            ["repair-blazor-findings"] = repairCompletionChecks,
            ["revalidate-blazor-repair"] = recheckAcceptanceChecks
        };

        return JsonSerializer.Serialize(map);
    }

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

    private static bool HasVisualTargetAssets(ProjectStructureProcessLaunchVariableContext context, string contextText)
        => ContainsVisualTargetAssetSummary(contextText) ||
           context.Surface.Nodes.Any(IsVisualTargetAsset);

    private static bool ContainsVisualTargetAssetSummary(string text)
        => text.Contains("Visual target assets:", StringComparison.OrdinalIgnoreCase) ||
           text.Contains("Visual target rule:", StringComparison.OrdinalIgnoreCase);

    private static bool IsVisualTargetAsset(ProjectStructureNode node)
    {
        if (!ProjectStructureProcessContextNodeFilter.ShouldIncludeInProcessContext(node) ||
            node.ObjectType != ProjectObjectType.ImageAsset)
        {
            return false;
        }

        if (string.Equals(node.ObjectSubtype, "screenshot", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(node.ArtifactKind, "process-run-screenshot", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.Equals(node.ObjectSubtype, "generated", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(node.ObjectSubtype, "layout-recommendation", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var searchableText = string.Join(" ", node.Title, node.Subtitle, node.Notes, node.ObjectSubtype, node.ArtifactKind);
        return ContainsAny(searchableText, "visual", "target", "proposal", "mockup", "wireframe", "layout", "design", "look", "ui");
    }

    private static string[] BuildDotNetValidationReceiptNames()
        =>
        [
            "workspace_dotnet_restore",
            "workspace_dotnet_build",
            "workspace_dotnet_test"
        ];

    private static bool IsBrowserVisibleAppArchetype(DotNetScaffoldArchetype appArchetype)
        => string.Equals(appArchetype.Template, "blazor", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(appArchetype.Template, "blazorwasm", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(appArchetype.Template, "mvc", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(appArchetype.Template, "razor", StringComparison.OrdinalIgnoreCase);

    private static string BuildSolutionSetupCompletionRequiredFileContentCheckMap(
        string solutionName,
        string productRoot,
        string appProjectName,
        string appProjectDirectory,
        string testProjectName,
        string testProjectDirectory)
    {
        var appProjectFile = CombinePath(appProjectDirectory, $"{appProjectName}.csproj");
        var testProjectFile = CombinePath(testProjectDirectory, $"{testProjectName}.csproj");
        var solutionCandidates = new[]
        {
            CombinePath(productRoot, $"{solutionName}.slnx"),
            CombinePath(productRoot, $"{solutionName}.sln")
        };
        var appSolutionPathAlternatives = BuildPathTextAlternatives(ToRelativePath(productRoot, appProjectFile));
        var testSolutionPathAlternatives = BuildPathTextAlternatives(ToRelativePath(productRoot, testProjectFile));
        var testProjectReferenceAlternatives = BuildPathTextAlternatives(ToRelativePath(testProjectDirectory, appProjectFile));
        var map = new Dictionary<string, object[]>(StringComparer.Ordinal)
        {
            ["create-dotnet-project"] =
            [
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["pathCandidates"] = solutionCandidates,
                    ["requiredTextAnyGroups"] = new[] { appSolutionPathAlternatives }
                }
            ],
            ["add-test-project"] =
            [
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["pathCandidates"] = solutionCandidates,
                    ["requiredTextAnyGroups"] = new[] { appSolutionPathAlternatives, testSolutionPathAlternatives }
                },
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["pathCandidates"] = new[] { testProjectFile },
                    ["requiredTextAnyGroups"] = new[] { testProjectReferenceAlternatives }
                }
            ],
            ["repair-solution-setup"] =
            [
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["pathCandidates"] = solutionCandidates,
                    ["requiredTextAnyGroups"] = new[] { appSolutionPathAlternatives, testSolutionPathAlternatives }
                },
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["pathCandidates"] = new[] { testProjectFile },
                    ["requiredTextAnyGroups"] = new[] { testProjectReferenceAlternatives }
                }
            ]
        };

        return JsonSerializer.Serialize(map);
    }

    private static string BuildSolutionSetupStepScopedLaunchVariablePrefixMap()
    {
        var map = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["scaffold-contract"] = ["DotNetCreateProject", "DotNetAddTestProject"],
            ["create-dotnet-project"] = ["DotNetCreateProject"],
            ["add-test-project"] = ["DotNetAddTestProject"],
            ["repair-solution-setup"] = ["DotNetCreateProject", "DotNetAddTestProject"]
        };

        return JsonSerializer.Serialize(map);
    }

    private static string BuildCreateProjectExecutionPlan(
        string workspaceAlias,
        string appProjectName,
        string appTemplate,
        string scriptRef)
    {
        var appParentAlias = string.IsNullOrWhiteSpace(workspaceAlias)
            ? "external-target/.../src"
            : $"{workspaceAlias}/src";
        var workingDirectory = string.IsNullOrWhiteSpace(workspaceAlias)
            ? "the grounded external-target product root alias"
            : workspaceAlias;

        return string.Join(
            Environment.NewLine,
            "Create-dotnet-project deterministic execution plan:",
            "1. Create or verify the grounded product root and src directory before scaffold tool calls.",
            "2. Invoke workspace_dotnet_new for template 'sln' with parentDirectory set to WorkspaceAlias and the contracted solution name when the solution is absent.",
            $"3. Invoke workspace_dotnet_new for template '{appTemplate}' with parentDirectory '{appParentAlias}', name '{appProjectName}', and force false when the app project is absent.",
            $"4. Write launch variable DotNetCreateProjectScript verbatim to '{scriptRef}' with workspace_write_file.",
            $"5. Verify '{scriptRef}' with workspace_stat_path or workspace_read_file.",
            $"6. Invoke workspace_pwsh_run_script with path '{scriptRef}', workingDirectory '{workingDirectory}', sideEffectManifest from DotNetCreateProjectSideEffectManifest, and outputPaths under the current managed artifact root.",
            "7. Read back the solution file and app project file.",
            "8. Write the primary create-dotnet-project.md artifact only after template tool receipts, the helper receipt, required paths, and solution app-membership readback pass.",
            "Do not write the primary steps/create-dotnet-project.md artifact, progress notes, or a Completed outcome before step 6 has a successful workspace_pwsh_run_script receipt.",
            "If a retry diagnostic says workspace_pwsh_run_script is missing, do not rerun workspace_dotnet_new or use force=true; write or verify the helper script, run it, read back solution membership, then rewrite the primary artifact.",
            "Do not rerun workspace_dotnet_new against an existing project directory; use the deterministic helper only to repair app membership in the solution.");
    }

    private static string BuildCreateProjectSideEffectManifest(
        IReadOnlyList<string> solutionCandidatePaths,
        string appProjectName,
        string appProjectDirectory)
    {
        var appProjectFile = CombinePath(appProjectDirectory, $"{appProjectName}.csproj");
        var manifest = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["version"] = 1,
            ["mode"] = "ProductMutation",
            ["declaredReadPaths"] = solutionCandidatePaths.Concat([appProjectFile]).ToArray(),
            ["declaredWritePaths"] = solutionCandidatePaths,
            ["allowShellDelegation"] = true
        };

        return JsonSerializer.Serialize(manifest);
    }

    private static string BuildCreateProjectScript(
        IReadOnlyList<string> solutionCandidatePaths,
        string productRoot,
        string appProjectName,
        string appProjectDirectory)
    {
        var appProjectFile = CombinePath(appProjectDirectory, $"{appProjectName}.csproj");
        var solutionArray = string.Join(", ", solutionCandidatePaths.Select(ToPowerShellSingleQuoted));

        var builder = new StringBuilder();
        builder.AppendLine("$ErrorActionPreference = 'Stop'");
        builder.AppendLine($"$ProductRoot = {ToPowerShellSingleQuoted(productRoot)}");
        builder.AppendLine($"$SolutionCandidates = @({solutionArray})");
        builder.AppendLine($"$AppProjectFile = {ToPowerShellSingleQuoted(appProjectFile)}");
        builder.AppendLine();
        builder.AppendLine("function Normalize-PathText([string]$Value) {");
        builder.AppendLine("    if ($null -eq $Value) { return '' }");
        builder.AppendLine("    return $Value.Replace('\\', '/').ToLowerInvariant()");
        builder.AppendLine("}");
        builder.AppendLine();
        builder.AppendLine("function Invoke-Dotnet([string[]]$Arguments) {");
        builder.AppendLine("    $output = & dotnet @Arguments 2>&1");
        builder.AppendLine("    $text = $output -join [Environment]::NewLine");
        builder.AppendLine("    if ($LASTEXITCODE -ne 0) {");
        builder.AppendLine("        throw \"dotnet $($Arguments -join ' ') failed with exit code $LASTEXITCODE. $text\"");
        builder.AppendLine("    }");
        builder.AppendLine("    if (-not [string]::IsNullOrWhiteSpace($text)) { Write-Host $text }");
        builder.AppendLine("    return $text");
        builder.AppendLine("}");
        builder.AppendLine();
        builder.AppendLine("function Get-SolutionListText([string]$SolutionFile) {");
        builder.AppendLine("    return Invoke-Dotnet @('sln', $SolutionFile, 'list')");
        builder.AppendLine("}");
        builder.AppendLine();
        builder.AppendLine("function Test-SolutionContainsProject([string]$SolutionFile, [string]$ProjectFile) {");
        builder.AppendLine("    $listText = Get-SolutionListText $SolutionFile");
        builder.AppendLine("    $relativeProjectPath = [System.IO.Path]::GetRelativePath($ProductRoot, $ProjectFile)");
        builder.AppendLine("    $normalizedList = Normalize-PathText $listText");
        builder.AppendLine("    $normalizedRelative = Normalize-PathText $relativeProjectPath");
        builder.AppendLine("    return $normalizedList.Contains($normalizedRelative)");
        builder.AppendLine("}");
        builder.AppendLine();
        builder.AppendLine("$SolutionFile = $SolutionCandidates | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1");
        builder.AppendLine("if ([string]::IsNullOrWhiteSpace($SolutionFile)) {");
        builder.AppendLine("    throw \"No contracted solution file exists. Candidates: $($SolutionCandidates -join '; ')\"");
        builder.AppendLine("}");
        builder.AppendLine("if (-not (Test-Path -LiteralPath $AppProjectFile -PathType Leaf)) {");
        builder.AppendLine("    throw \"Contracted app project file is missing: $AppProjectFile\"");
        builder.AppendLine("}");
        builder.AppendLine("if (-not (Test-SolutionContainsProject $SolutionFile $AppProjectFile)) {");
        builder.AppendLine("    Invoke-Dotnet @('sln', $SolutionFile, 'add', $AppProjectFile) | Out-Null");
        builder.AppendLine("}");
        builder.AppendLine("$finalListText = Get-SolutionListText $SolutionFile");
        builder.AppendLine("$finalNormalizedList = Normalize-PathText $finalListText");
        builder.AppendLine("$appRelative = Normalize-PathText ([System.IO.Path]::GetRelativePath($ProductRoot, $AppProjectFile))");
        builder.AppendLine("if (-not $finalNormalizedList.Contains($appRelative)) {");
        builder.AppendLine("    throw \"Solution membership is missing the app project relative path: $appRelative\"");
        builder.AppendLine("}");
        builder.AppendLine("Write-Host \"Verified solution membership for $AppProjectFile.\"");

        return builder.ToString();
    }

    private static string BuildAddTestProjectExecutionPlan(
        string workspaceAlias,
        string testProjectName,
        string testTemplate,
        string scriptRef)
    {
        var testParentAlias = string.IsNullOrWhiteSpace(workspaceAlias)
            ? "external-target/.../tests"
            : $"{workspaceAlias}/tests";
        var workingDirectory = string.IsNullOrWhiteSpace(workspaceAlias)
            ? "the grounded external-target product root alias"
            : workspaceAlias;

        return string.Join(
            Environment.NewLine,
            "Add-test-project deterministic execution plan:",
            $"1. Write launch variable DotNetAddTestProjectScript verbatim to '{scriptRef}' with workspace_write_file.",
            $"2. Verify '{scriptRef}' with workspace_stat_path or workspace_read_file.",
            $"3. Invoke workspace_pwsh_run_script with path '{scriptRef}', workingDirectory '{workingDirectory}', sideEffectManifest from DotNetAddTestProjectSideEffectManifest, and outputPaths under the current managed artifact root. The helper creates the missing test project with template '{testTemplate}', parent directory '{testParentAlias}', name '{testProjectName}', and force false.",
            "4. Read back the solution file and test project file, then write the primary add-test-project.md artifact only after the tool receipt and file content gates pass.",
            "Do not write Status: InProgress, progress notes, or a Completed artifact before step 3 has a successful receipt.");
    }

    private static string BuildAddTestProjectSideEffectManifest(
        IReadOnlyList<string> solutionCandidatePaths,
        string appProjectName,
        string appProjectDirectory,
        string testProjectName,
        string testProjectDirectory)
    {
        var appProjectFile = CombinePath(appProjectDirectory, $"{appProjectName}.csproj");
        var testProjectFile = CombinePath(testProjectDirectory, $"{testProjectName}.csproj");
        var manifest = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["version"] = 1,
            ["mode"] = "ProductMutation",
            ["declaredReadPaths"] = solutionCandidatePaths.Concat([appProjectFile, testProjectFile]).ToArray(),
            ["declaredWritePaths"] = solutionCandidatePaths.Concat([testProjectDirectory, testProjectFile]).ToArray(),
            ["allowShellDelegation"] = true
        };

        return JsonSerializer.Serialize(manifest);
    }

    private static string BuildAddTestProjectScript(
        IReadOnlyList<string> solutionCandidatePaths,
        string productRoot,
        string appProjectName,
        string appProjectDirectory,
        string testProjectName,
        string testProjectDirectory,
        string testTemplate,
        string targetFramework)
    {
        var appProjectFile = CombinePath(appProjectDirectory, $"{appProjectName}.csproj");
        var testProjectFile = CombinePath(testProjectDirectory, $"{testProjectName}.csproj");
        var solutionArray = string.Join(", ", solutionCandidatePaths.Select(ToPowerShellSingleQuoted));

        var builder = new StringBuilder();
        builder.AppendLine("$ErrorActionPreference = 'Stop'");
        builder.AppendLine($"$ProductRoot = {ToPowerShellSingleQuoted(productRoot)}");
        builder.AppendLine($"$SolutionCandidates = @({solutionArray})");
        builder.AppendLine($"$AppProjectFile = {ToPowerShellSingleQuoted(appProjectFile)}");
        builder.AppendLine($"$TestProjectFile = {ToPowerShellSingleQuoted(testProjectFile)}");
        builder.AppendLine($"$TestProjectName = {ToPowerShellSingleQuoted(testProjectName)}");
        builder.AppendLine($"$TestProjectDirectory = {ToPowerShellSingleQuoted(testProjectDirectory)}");
        builder.AppendLine($"$TestTemplate = {ToPowerShellSingleQuoted(testTemplate)}");
        builder.AppendLine($"$TargetFramework = {ToPowerShellSingleQuoted(targetFramework)}");
        builder.AppendLine();
        builder.AppendLine("function Normalize-PathText([string]$Value) {");
        builder.AppendLine("    if ($null -eq $Value) { return '' }");
        builder.AppendLine("    return $Value.Replace('\\', '/').ToLowerInvariant()");
        builder.AppendLine("}");
        builder.AppendLine();
        builder.AppendLine("function Invoke-Dotnet([string[]]$Arguments) {");
        builder.AppendLine("    $output = & dotnet @Arguments 2>&1");
        builder.AppendLine("    $text = $output -join [Environment]::NewLine");
        builder.AppendLine("    if ($LASTEXITCODE -ne 0) {");
        builder.AppendLine("        throw \"dotnet $($Arguments -join ' ') failed with exit code $LASTEXITCODE. $text\"");
        builder.AppendLine("    }");
        builder.AppendLine("    if (-not [string]::IsNullOrWhiteSpace($text)) { Write-Host $text }");
        builder.AppendLine("    return $text");
        builder.AppendLine("}");
        builder.AppendLine();
        builder.AppendLine("function Get-SolutionListText([string]$SolutionFile) {");
        builder.AppendLine("    return Invoke-Dotnet @('sln', $SolutionFile, 'list')");
        builder.AppendLine("}");
        builder.AppendLine();
        builder.AppendLine("function Test-SolutionContainsProject([string]$SolutionFile, [string]$ProjectFile) {");
        builder.AppendLine("    $listText = Get-SolutionListText $SolutionFile");
        builder.AppendLine("    $relativeProjectPath = [System.IO.Path]::GetRelativePath($ProductRoot, $ProjectFile)");
        builder.AppendLine("    $normalizedList = Normalize-PathText $listText");
        builder.AppendLine("    $normalizedRelative = Normalize-PathText $relativeProjectPath");
        builder.AppendLine("    return $normalizedList.Contains($normalizedRelative)");
        builder.AppendLine("}");
        builder.AppendLine();
        builder.AppendLine("$SolutionFile = $SolutionCandidates | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1");
        builder.AppendLine("if ([string]::IsNullOrWhiteSpace($SolutionFile)) {");
        builder.AppendLine("    throw \"No contracted solution file exists. Candidates: $($SolutionCandidates -join '; ')\"");
        builder.AppendLine("}");
        builder.AppendLine("if (-not (Test-Path -LiteralPath $AppProjectFile -PathType Leaf)) {");
        builder.AppendLine("    throw \"Contracted app project file is missing: $AppProjectFile\"");
        builder.AppendLine("}");
        builder.AppendLine("if (-not (Test-Path -LiteralPath $TestProjectFile -PathType Leaf)) {");
        builder.AppendLine("    $testProjectParentDirectory = Split-Path -Parent $TestProjectDirectory");
        builder.AppendLine("    if (-not (Test-Path -LiteralPath $testProjectParentDirectory -PathType Container)) {");
        builder.AppendLine("        New-Item -ItemType Directory -Path $testProjectParentDirectory -Force | Out-Null");
        builder.AppendLine("    }");
        builder.AppendLine("    $newTestProjectArguments = @('new', $TestTemplate, '--name', $TestProjectName, '--output', $TestProjectDirectory)");
        builder.AppendLine("    if (-not [string]::IsNullOrWhiteSpace($TargetFramework)) {");
        builder.AppendLine("        $newTestProjectArguments += @('--framework', $TargetFramework)");
        builder.AppendLine("    }");
        builder.AppendLine("    Invoke-Dotnet $newTestProjectArguments | Out-Null");
        builder.AppendLine("}");
        builder.AppendLine("if (-not (Test-Path -LiteralPath $TestProjectFile -PathType Leaf)) {");
        builder.AppendLine("    throw \"Contracted test project file is missing: $TestProjectFile\"");
        builder.AppendLine("}");
        builder.AppendLine();
        builder.AppendLine("if (-not (Test-SolutionContainsProject $SolutionFile $AppProjectFile)) {");
        builder.AppendLine("    Invoke-Dotnet @('sln', $SolutionFile, 'add', $AppProjectFile) | Out-Null");
        builder.AppendLine("}");
        builder.AppendLine("if (-not (Test-SolutionContainsProject $SolutionFile $TestProjectFile)) {");
        builder.AppendLine("    Invoke-Dotnet @('sln', $SolutionFile, 'add', $TestProjectFile) | Out-Null");
        builder.AppendLine("}");
        builder.AppendLine();
        builder.AppendLine("$testProjectDirectory = Split-Path -Parent $TestProjectFile");
        builder.AppendLine("$expectedReference = [System.IO.Path]::GetRelativePath($testProjectDirectory, $AppProjectFile)");
        builder.AppendLine("$testProjectText = Get-Content -LiteralPath $TestProjectFile -Raw");
        builder.AppendLine("if (-not (Normalize-PathText $testProjectText).Contains((Normalize-PathText $expectedReference))) {");
        builder.AppendLine("    Invoke-Dotnet @('add', $TestProjectFile, 'reference', $AppProjectFile) | Out-Null");
        builder.AppendLine("}");
        builder.AppendLine();
        builder.AppendLine("$finalListText = Get-SolutionListText $SolutionFile");
        builder.AppendLine("$finalNormalizedList = Normalize-PathText $finalListText");
        builder.AppendLine("$appRelative = Normalize-PathText ([System.IO.Path]::GetRelativePath($ProductRoot, $AppProjectFile))");
        builder.AppendLine("$testRelative = Normalize-PathText ([System.IO.Path]::GetRelativePath($ProductRoot, $TestProjectFile))");
        builder.AppendLine("if (-not $finalNormalizedList.Contains($appRelative)) {");
        builder.AppendLine("    throw \"Solution membership is missing the app project relative path: $appRelative\"");
        builder.AppendLine("}");
        builder.AppendLine("if (-not $finalNormalizedList.Contains($testRelative)) {");
        builder.AppendLine("    throw \"Solution membership is missing the test project relative path: $testRelative\"");
        builder.AppendLine("}");
        builder.AppendLine("$finalTestProjectText = Get-Content -LiteralPath $TestProjectFile -Raw");
        builder.AppendLine("if (-not (Normalize-PathText $finalTestProjectText).Contains((Normalize-PathText $expectedReference))) {");
        builder.AppendLine("    throw \"Test project is missing ProjectReference relative path: $expectedReference\"");
        builder.AppendLine("}");
        builder.AppendLine("Write-Host \"Verified solution membership and ProjectReference for $TestProjectFile.\"");

        return builder.ToString();
    }

    private static string ToPowerShellSingleQuoted(string value)
        => $"'{value.Replace("'", "''")}'";

    private static string[] BuildPathTextAlternatives(string path)
    {
        var trimmed = path.Trim();
        return new[]
            {
                trimmed,
                trimmed.Replace('\\', '/'),
                trimmed.Replace('/', '\\')
            }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string ToRelativePath(string root, string path)
    {
        try
        {
            return Path.GetRelativePath(root, path);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        {
            return path;
        }
    }

    private static void AddIfMissing(IDictionary<string, string> variables, string key, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (!variables.TryGetValue(key, out var existing) || string.IsNullOrWhiteSpace(existing))
        {
            variables[key] = value;
        }
    }

    private static void SetIfNotEmpty(IDictionary<string, string> variables, string key, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            variables[key] = value;
        }
    }

    private static string ResolveVariable(IDictionary<string, string> variables, string key)
        => variables.TryGetValue(key, out var value)
            ? value?.Trim() ?? string.Empty
            : string.Empty;

    private static string ResolveProductRootLeaf(string productRoot)
    {
        if (string.IsNullOrWhiteSpace(productRoot))
        {
            return string.Empty;
        }

        var normalized = productRoot.Trim().TrimEnd('\\', '/');
        var slashIndex = normalized.LastIndexOfAny(['\\', '/']);
        return slashIndex < 0
            ? normalized
            : normalized[(slashIndex + 1)..];
    }

    private static string ToIdentifier(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var parts = IdentifierPartRegex()
            .Matches(value)
            .Select(match => match.Value)
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .ToArray();
        if (parts.Length == 0)
        {
            return string.Empty;
        }

        var identifier = string.Concat(parts.Select(ToPascalPart));
        return char.IsLetter(identifier[0])
            ? identifier
            : $"App{identifier}";
    }

    private static string ToPascalPart(string value)
    {
        if (value.Length == 0)
        {
            return string.Empty;
        }

        return char.ToUpperInvariant(value[0]) + value[1..];
    }

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

    private static bool ContainsAny(string text, params string[] values)
        => values.Any(value => text.Contains(value, StringComparison.OrdinalIgnoreCase));

    private static string FirstNonEmpty(params string[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;

    private static void AppendLine(StringBuilder builder, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            builder.AppendLine(value);
        }
    }

    private sealed record DotNetScaffoldArchetype(
        string Archetype,
        string Template,
        string TemplateOptions,
        string AllowedTemplateSwitches);

    private sealed record AcceptanceCriteriaCandidate(
        string NodeId,
        string Summary);

    private static class ProcessAcceptanceCriteriaIds
    {
        public static string FromIndex(int index)
            => $"AC-{index + 1:000}";
    }

    [GeneratedRegex(@"\bnet\d+(?:\.\d+)?\b", RegexOptions.IgnoreCase)]
    private static partial Regex TargetFrameworkRegex();

    [GeneratedRegex(@"[A-Za-z0-9]+")]
    private static partial Regex IdentifierPartRegex();
}
