using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections;
using System.Collections.Concurrent;
using System.Data;
using System.Data.Common;
using System.Text.Json;
using System.Text;
using System.Text.RegularExpressions;

namespace CanDoItAll.Modules.Processes;

internal sealed partial class ProcessRunAutomationDispatchService
{
    private static IReadOnlyList<ProjectStructureGroundingNodeData> ResolveProjectStructureAncestorPath(
        string? nodeId,
        IReadOnlyDictionary<string, ProjectStructureGroundingNodeData> nodesById)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            return [];
        }

        var path = new List<ProjectStructureGroundingNodeData>();
        var cursor = NormalizeProjectStructureNodeId(nodeId);
        var visited = new HashSet<string>(StringComparer.Ordinal);

        while (!string.IsNullOrWhiteSpace(cursor) &&
               visited.Add(cursor) &&
               nodesById.TryGetValue(cursor, out var node))
        {
            path.Add(node);
            cursor = NormalizeProjectStructureNodeId(node.ParentId);
        }

        path.Reverse();
        return path;
    }

    private static IReadOnlyList<ProjectStructureGroundingNodeData> ResolveProjectStructureDescendants(
        string? nodeId,
        IReadOnlyDictionary<string, IReadOnlyList<ProjectStructureGroundingNodeData>> nodesByParentId,
        int maxDepth)
    {
        if (string.IsNullOrWhiteSpace(nodeId) || maxDepth <= 0)
        {
            return [];
        }

        var descendants = new List<ProjectStructureGroundingNodeData>();
        var queue = new Queue<(string NodeId, int Depth)>();
        var visited = new HashSet<string>(StringComparer.Ordinal);
        queue.Enqueue((NormalizeProjectStructureNodeId(nodeId), 0));

        while (queue.Count > 0)
        {
            var (currentNodeId, depth) = queue.Dequeue();
            if (depth >= maxDepth ||
                !nodesByParentId.TryGetValue(currentNodeId, out var children))
            {
                continue;
            }

            foreach (var child in children)
            {
                if (!visited.Add(child.Id))
                {
                    continue;
                }

                descendants.Add(child);
                queue.Enqueue((child.Id, depth + 1));
            }
        }

        return descendants;
    }

    private static bool TryResolveExternalTargetHintFromProjectStructureGrounding(
        string? groundingSummary,
        out string absolutePath,
        out string mappedAlias)
    {
        absolutePath = string.Empty;
        mappedAlias = string.Empty;

        if (string.IsNullOrWhiteSpace(groundingSummary))
        {
            return false;
        }

        var match = Regex.Match(
            groundingSummary,
            @"\b(?<path>[A-Za-z]:\\[A-Za-z0-9 _.\-\\]+)",
            RegexOptions.CultureInvariant);
        if (!match.Success)
        {
            return false;
        }

        var candidatePath = match.Groups["path"].Value.Trim().TrimEnd('\\');
        if (candidatePath.Length < 3 || candidatePath[1] != ':' || candidatePath[2] != '\\')
        {
            return false;
        }

        var driveLetter = char.ToUpperInvariant(candidatePath[0]);
        var remainder = candidatePath.Length == 3
            ? string.Empty
            : candidatePath[3..].Replace('\\', '/');
        absolutePath = candidatePath;
        mappedAlias = string.IsNullOrWhiteSpace(remainder)
            ? $"external-target/{driveLetter}"
            : $"external-target/{driveLetter}/{remainder}";
        return true;
    }

    private static void AppendProjectStructureGroundingNodes(
        StringBuilder builder,
        IReadOnlyList<ProjectStructureGroundingNodeData> nodes)
    {
        foreach (var node in nodes)
        {
            builder.AppendLine($"- {BuildProjectStructureGroundingNodeSummary(node)}");
        }
    }

    private static string BuildProjectStructureGroundingNodeSummary(ProjectStructureGroundingNodeData node)
    {
        ArgumentNullException.ThrowIfNull(node);

        var segments = new List<string>
        {
            $"{node.Title} ({node.Id})",
            $"type: {node.ObjectType}/{NormalizeProjectStructureNodeSubtype(node.ObjectSubtype)}"
        };

        if (!string.IsNullOrWhiteSpace(node.Status))
        {
            segments.Add($"status: {CollapsePromptWhitespace(node.Status)}");
        }

        if (!string.IsNullOrWhiteSpace(node.Subtitle))
        {
            segments.Add($"subtitle: {TrimProjectStructureGroundingText(node.Subtitle, 140)}");
        }

        if (!string.IsNullOrWhiteSpace(node.Notes))
        {
            segments.Add($"notes: {TrimProjectStructureGroundingText(node.Notes, 320)}");
        }

        var metadataSummary = NormalizeProjectStructureMetadataSummary(node.MetadataJson);
        if (!string.IsNullOrWhiteSpace(metadataSummary))
        {
            segments.Add($"metadata: {metadataSummary}");
        }

        return string.Join("; ", segments);
    }

    private static bool HasProjectStructureGroundingSignal(ProjectStructureGroundingNodeData node)
    {
        ArgumentNullException.ThrowIfNull(node);

        return GetProjectStructureGroundingSignalScore(node) > 0;
    }

    private static int GetProjectStructureGroundingSignalScore(ProjectStructureGroundingNodeData node)
    {
        ArgumentNullException.ThrowIfNull(node);

        var score = 0;
        if (!string.IsNullOrWhiteSpace(node.Notes))
        {
            score += 4;
        }

        if (!string.IsNullOrWhiteSpace(node.Subtitle))
        {
            score += 3;
        }

        if (!string.IsNullOrWhiteSpace(NormalizeProjectStructureMetadataSummary(node.MetadataJson)))
        {
            score += 2;
        }

        if (LooksLikeProjectStructureConstraintTitle(node.Title))
        {
            score += 5;
        }

        if (LooksLikeProjectStructureFeatureTitle(node.Title))
        {
            score += 3;
        }

        return score;
    }

    private static bool LooksLikeProjectStructureConstraintTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return false;
        }

        var normalizedTitle = CollapsePromptWhitespace(title);
        if (string.IsNullOrWhiteSpace(normalizedTitle))
        {
            return false;
        }

        return normalizedTitle.Contains("output", StringComparison.OrdinalIgnoreCase) ||
               normalizedTitle.Contains("must", StringComparison.OrdinalIgnoreCase) ||
               normalizedTitle.Contains("required", StringComparison.OrdinalIgnoreCase) ||
               normalizedTitle.Contains("directory", StringComparison.OrdinalIgnoreCase) ||
               normalizedTitle.Contains("path", StringComparison.OrdinalIgnoreCase) ||
               normalizedTitle.Contains("place", StringComparison.OrdinalIgnoreCase) ||
               Regex.IsMatch(
                   normalizedTitle,
                   @"\b[a-zA-Z]:\\",
                RegexOptions.CultureInvariant) ||
               normalizedTitle.Contains("external-target/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeProjectStructureFeatureTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return false;
        }

        var normalizedTitle = CollapsePromptWhitespace(title);
        if (string.IsNullOrWhiteSpace(normalizedTitle))
        {
            return false;
        }

        return normalizedTitle.Contains("blazor", StringComparison.OrdinalIgnoreCase) ||
               normalizedTitle.Contains("calculator", StringComparison.OrdinalIgnoreCase) ||
               normalizedTitle.Contains("button", StringComparison.OrdinalIgnoreCase) ||
               normalizedTitle.Contains("history", StringComparison.OrdinalIgnoreCase) ||
               normalizedTitle.Contains("keypad", StringComparison.OrdinalIgnoreCase) ||
               normalizedTitle.Contains("keyboard", StringComparison.OrdinalIgnoreCase) ||
               normalizedTitle.Contains("screen", StringComparison.OrdinalIgnoreCase) ||
               normalizedTitle.Contains("page", StringComparison.OrdinalIgnoreCase) ||
               normalizedTitle.Contains("form", StringComparison.OrdinalIgnoreCase) ||
               normalizedTitle.Contains("ui", StringComparison.OrdinalIgnoreCase) ||
               normalizedTitle.Contains("route", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsProjectStructureGroundingNoiseNode(ProjectStructureGroundingNodeData node)
    {
        ArgumentNullException.ThrowIfNull(node);

        return string.Equals(node.ObjectType, "ProcessRun", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(node.ObjectType, "File", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeProjectStructureNodeId(string? nodeId)
        => string.IsNullOrWhiteSpace(nodeId) ? string.Empty : nodeId.Trim();

    private static string NormalizeProjectStructureNodeSubtype(string? objectSubtype)
        => string.IsNullOrWhiteSpace(objectSubtype) ? "default" : CollapsePromptWhitespace(objectSubtype);

    private static string TrimProjectStructureGroundingText(string? value, int maxLength)
    {
        var collapsed = CollapsePromptWhitespace(value);
        if (collapsed.Length <= maxLength)
        {
            return collapsed;
        }

        return $"{collapsed[..Math.Max(0, maxLength - 3)].TrimEnd()}...";
    }

    private static string CollapsePromptWhitespace(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return Regex.Replace(
                value,
                @"\s+",
                " ",
                RegexOptions.CultureInvariant)
            .Trim();
    }

    private static string NormalizeProjectStructureMetadataSummary(string? metadataJson)
    {
        if (string.IsNullOrWhiteSpace(metadataJson))
        {
            return string.Empty;
        }

        try
        {
            using var document = JsonDocument.Parse(metadataJson);
            var root = document.RootElement;
            if (root.ValueKind == JsonValueKind.Object && !root.EnumerateObject().MoveNext())
            {
                return string.Empty;
            }

            if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() == 0)
            {
                return string.Empty;
            }

            return TrimProjectStructureGroundingText(JsonSerializer.Serialize(root), 320);
        }
        catch (JsonException)
        {
            return TrimProjectStructureGroundingText(metadataJson, 320);
        }
    }

    private static IReadOnlyList<ProjectStructureGroundingNodeData> ExtractProjectStructureGroundingNodes(object surface)
    {
        var nodesValue = surface.GetType().GetProperty("Nodes")?.GetValue(surface) as IEnumerable;
        if (nodesValue is null)
        {
            return [];
        }

        var nodes = new List<ProjectStructureGroundingNodeData>();
        foreach (var node in nodesValue.Cast<object>())
        {
            var id = GetProjectStructureGroundingString(node, "Id");
            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            nodes.Add(new ProjectStructureGroundingNodeData(
                id,
                GetProjectStructureGroundingString(node, "ParentId"),
                GetProjectStructureGroundingString(node, "ObjectType"),
                GetProjectStructureGroundingString(node, "ObjectSubtype"),
                GetProjectStructureGroundingString(node, "Title"),
                GetProjectStructureGroundingString(node, "Subtitle"),
                GetProjectStructureGroundingString(node, "Status"),
                GetProjectStructureGroundingString(node, "Notes"),
                GetProjectStructureGroundingString(node, "MetadataJson")));
        }

        return nodes;
    }

    private static string GetProjectStructureGroundingString(object source, string propertyName)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        var value = source.GetType().GetProperty(propertyName)?.GetValue(source);
        return value?.ToString()?.Trim() ?? string.Empty;
    }

    private static string BuildCalculatorRecoveryFocusGuidance(
        DispatchCandidate candidate,
        string? responseText,
        string missingConcreteImplementationProofSummary,
        IReadOnlyList<string> missingRequiredTools,
        IReadOnlyList<ToolExecutionReceiptRecord> unresolvedCriticalToolFailures)
    {
        if (!ContainsCalculatorContext(candidate))
        {
            return string.Empty;
        }

        var unresolvedFailureText = string.Join(
            Environment.NewLine,
            unresolvedCriticalToolFailures.Select(item => $"{item.ToolName} {item.RequestSummary} {item.ExitSummary}"));
        var recoveryDiagnosticText = string.Join(
            Environment.NewLine,
            responseText,
            missingConcreteImplementationProofSummary,
            unresolvedFailureText);
        var repeatedTestProjectWrite = MentionsRepeatedToolInvocation(responseText) &&
            responseText?.Contains("Calculator.Tests/Calculator.Tests.csproj", StringComparison.OrdinalIgnoreCase) == true;
        var repeatedHomeRazorWrite = MentionsRepeatedToolInvocation(responseText) &&
            responseText?.Contains("Calculator/Components/Pages/Home.razor", StringComparison.OrdinalIgnoreCase) == true;
        var homeRazorCharStringCompilerFailure = MentionsHomeRazorCharStringCompilerFailure(recoveryDiagnosticText);
        var homeRazorRouteTemplateFailure = MentionsHomeRazorRouteTemplateFailure(recoveryDiagnosticText);
        var calculatorEngineDuplicateCompilerFailure = MentionsCalculatorEngineDuplicateCompilerFailure(recoveryDiagnosticText);
        var testProjectReferenceFailure =
            MentionsCalculatorTestProjectReferenceFailure(responseText) ||
            MentionsCalculatorTestProjectReferenceFailure(missingConcreteImplementationProofSummary) ||
            MentionsCalculatorTestProjectReferenceFailure(unresolvedFailureText);
        var missingTestValidation = missingRequiredTools.Contains("workspace_dotnet_test", StringComparer.Ordinal);
        var routedUiProofMissing =
            missingConcreteImplementationProofSummary.Contains("routed UI", StringComparison.OrdinalIgnoreCase) ||
            missingConcreteImplementationProofSummary.Contains("Home.razor", StringComparison.OrdinalIgnoreCase) ||
            missingConcreteImplementationProofSummary.Contains("keypad", StringComparison.OrdinalIgnoreCase) ||
            missingConcreteImplementationProofSummary.Contains("history", StringComparison.OrdinalIgnoreCase);
        if (!repeatedTestProjectWrite &&
            !repeatedHomeRazorWrite &&
            !homeRazorCharStringCompilerFailure &&
            !homeRazorRouteTemplateFailure &&
            !calculatorEngineDuplicateCompilerFailure &&
            !testProjectReferenceFailure &&
            !missingTestValidation &&
            !routedUiProofMissing)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        builder.AppendLine("Immediate calculator recovery focus:");
        if (repeatedTestProjectWrite)
        {
            builder.AppendLine("- The previous attempt looped rewriting `Calculator.Tests/Calculator.Tests.csproj`. If that file already has the host ProjectReference, it is not the active blocker. Do not write it again until after the routed UI proof passes.");
        }

        if (routedUiProofMissing)
        {
            builder.AppendLine("- The next concrete mutation must repair `external-target/C/programovani/csharp/calculator/Calculator/Components/Pages/Home.razor`. Read it, then overwrite the placeholder/free-form textbox route with a `CalculatorEngine`-backed keypad/operator/equal/history UI before touching artifacts or rerunning tests.");
        }

        if (repeatedHomeRazorWrite)
        {
            builder.AppendLine("- The previous attempt looped rewriting `Calculator/Components/Pages/Home.razor`. Do not write the same page again unchanged; first inspect the latest build diagnostic and change the event handler signatures, button literal types, or calculation logic that directly addresses it.");
        }

        if (homeRazorCharStringCompilerFailure)
        {
            builder.AppendLine("- The host build is failing in `Calculator/Components/Pages/Home.razor` with `CS1503` char-to-string errors. Use one type-consistent Razor callback pattern: either handlers accept `char` and callbacks use `@onclick=\"() => AppendDigit('1')\"`, or handlers accept `string` and callbacks use single-quoted Razor attributes such as `@onclick='() => AppendDigit(\"1\")'`.");
            builder.AppendLine("- Do not leave `AppendToResult('1')` or `SetOperation('+')` calling methods that still accept `string`; that is the exact prior compiler failure. Also never write malformed double-quoted callbacks such as `@onclick=\"() => AppendDigit(\"1\")\"`.");
            builder.AppendLine("- If `Calculator.Tests/Calculator.Tests.csproj` already has the host ProjectReference, do not rewrite the test project again while the compiler error points at `Calculator/Components/Pages/Home.razor`; repair the routed UI first.");
            builder.AppendLine("- After the `Home.razor` compile fix, remove placeholder `CalculateResult` behavior and connect equals/evaluate, operators, display/result state, divide-by-zero feedback, and history to `CalculatorEngine`; then rerun `workspace_dotnet_build` on `Calculator/Calculator.csproj` before `workspace_dotnet_test`.");
        }

        if (homeRazorRouteTemplateFailure)
        {
            builder.AppendLine("- The host build is failing in `Calculator/Components/Pages/Home.razor` with `RZ9988` because the page route is empty. Change `@page \"\"` to `@page \"/\"` before any test-project repair or test rerun.");
        }

        if (calculatorEngineDuplicateCompilerFailure)
        {
            builder.AppendLine("- The host build is failing with duplicate `CalculatorEngine` definitions (`CS0101`/`CS0111`). Read both `Calculator/CalculatorEngine.cs` and `Calculator/Domain/CalculatorEngine.cs`; delete the stale top-level `Calculator/CalculatorEngine.cs` if both define the engine, then rebuild. Do not delete and recreate only `Domain/CalculatorEngine.cs` because that leaves the duplicate in place.");
        }

        if (testProjectReferenceFailure)
        {
            builder.AppendLine("- The previous test failure was a host visibility failure, not a package or assertion failure. Read `Calculator.Tests/Calculator.Tests.csproj` and `Calculator/Domain/CalculatorEngine.cs`, then repair the test project so it contains `<ProjectReference Include=\"..\\Calculator\\Calculator.csproj\" />` and the engine source is in namespace `Calculator.Domain`.");
        }

        if (missingTestValidation)
        {
            builder.AppendLine("- `workspace_dotnet_test` is still required. Do not rerun it until the host ProjectReference, `CalculatorEngine`, `Program.cs` DI registration, and routed UI have been read back after the latest mutations.");
        }

        builder.AppendLine("- Required repair order: fix `Calculator/Program.cs`, `Calculator/Components/Pages/Home.razor`, `Calculator/Domain/CalculatorEngine.cs`, `Calculator.Tests/Calculator.Tests.csproj`, and meaningful sibling tests; read those files back; then build the host and run the sibling test project.");
        return builder.ToString().Trim();
    }

    private static bool MentionsHomeRazorCharStringCompilerFailure(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return text.Contains("Home.razor", StringComparison.OrdinalIgnoreCase) &&
               text.Contains("CS1503", StringComparison.OrdinalIgnoreCase) &&
               text.Contains("char", StringComparison.OrdinalIgnoreCase) &&
               text.Contains("string", StringComparison.OrdinalIgnoreCase);
    }

    private static bool MentionsHomeRazorRouteTemplateFailure(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return text.Contains("Home.razor", StringComparison.OrdinalIgnoreCase) &&
               (text.Contains("RZ9988", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("@page directive must specify a route template", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("@page \"\"", StringComparison.OrdinalIgnoreCase));
    }

    private static bool MentionsCalculatorEngineDuplicateCompilerFailure(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return text.Contains("CalculatorEngine", StringComparison.OrdinalIgnoreCase) &&
               (text.Contains("CS0101", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("CS0111", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("already contains a definition", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("already defines a member", StringComparison.OrdinalIgnoreCase));
    }

    private static bool MentionsCalculatorTestProjectReferenceFailure(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var mentionsCalculatorTestOrValidation =
            text.Contains("Calculator.Tests", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("workspace_dotnet_test", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("ProjectReference", StringComparison.OrdinalIgnoreCase);
        var mentionsHostTypeVisibility =
            text.Contains("Calculator.Domain", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("CalculatorEngine", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("CS0234", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("CS0246", StringComparison.OrdinalIgnoreCase);

        return mentionsCalculatorTestOrValidation && mentionsHostTypeVisibility;
    }

    private static string BuildMisplacedTestProjectRecoveryGuidance(
        IReadOnlyList<ToolExecutionReceiptRecord> unresolvedCriticalToolFailures)
    {
        var cleanupTargets = ResolveMisplacedTestProjectCleanupTargets(unresolvedCriticalToolFailures);
        if (cleanupTargets.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        builder.AppendLine("A previous host build failed while a sibling test project build succeeded or was attempted. Treat stale nested test folders under the host as the first repair target before more scaffolding.");
        foreach (var target in cleanupTargets)
        {
            builder.AppendLine($"For failed host build `{target.HostProjectPath}`, remove the stale nested test directory `{target.NestedTestDirectoryPath}` with `workspace_delete_path` using `recursive: true`, then rerun `workspace_dotnet_build` against `{target.HostProjectPath}`.");
            builder.AppendLine($"Do not recreate test files under `{target.NestedTestDirectoryPath}`. If tests are still required, create or repair a sibling test project outside the host folder.");
        }

        builder.AppendLine("Do not add xUnit, MSTest, or test SDK packages to the production host to satisfy misplaced test files.");
        return builder.ToString().Trim();
    }

    private static string BuildBlazorBuildRecoveryGuidance(
        DispatchCandidate candidate,
        IReadOnlyList<ToolExecutionReceiptRecord> unresolvedCriticalToolFailures,
        string responseText)
    {
        if (!RequiresConcreteImplementationProof(candidate) ||
            !unresolvedCriticalToolFailures.Any(IsFrameworkRecoverableDotnetToolFailure) &&
            !MentionsRepeatedToolInvocation(responseText))
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        builder.AppendLine("Do not rerun the identical `workspace_dotnet_build` request until you have changed or deleted files that directly address the current compiler errors.");
        builder.AppendLine("Do not rerun the identical `workspace_dotnet_test` request after a denied or missing-path result until you have created or repaired the sibling test project and ProjectReference that the command targets.");
        builder.AppendLine("Do not recover from scaffold conflicts by recursively deleting the runnable host, sibling test project, or target root. If a directory contains a .NET project or solution file, repair it in place.");
        builder.AppendLine("Do not delete scaffold core files one by one to make re-scaffolding succeed. Preserve and edit `.csproj`, `Program.cs`, `Components/App.razor`, `Components/Routes.razor`, `_Imports.razor`, `Components/Pages/Home.razor`, layout files, `appsettings*.json`, and `wwwroot/app.css`.");
        builder.AppendLine("If the previous attempt only scaffolded or wrote markdown artifacts, the next recovery attempt must mutate concrete source/project files before writing any artifacts or running validations.");
        builder.AppendLine("If a `.csproj` exists anywhere under the target root, do not call `workspace_dotnet_new` for that same host again. Read the project shape and repair the existing scaffold.");
        builder.AppendLine("For Blazor host builds that mention nested `*.Tests` files, delete the nested host test folder and do not recreate it; use a sibling test project outside the host folder if tests are required.");
        builder.AppendLine("For test-project failures with duplicate test classes or methods (`CS0101`, `CS0111`), inspect the sibling test project files and remove stale template sources such as `UnitTest1.cs`, `<Project>.Tests.cs`, old `.bak` sources that are still compiled, or duplicate `CalculatorTests` files before rerunning `workspace_dotnet_test`.");
        builder.AppendLine("If a test retry keeps failing after rewriting the same test file, stop rewriting that file. Inspect the whole test project shape, add the missing `ProjectReference` or domain class, and remove the conflicting stale source files first.");
        builder.AppendLine("If `Calculator.Tests/Calculator.Tests.csproj` already has a host `ProjectReference` and the compiler error points at `Calculator/Components/Pages/Home.razor`, do not rewrite the test project again. Repair `Home.razor` first, especially `CS1503` char/string callback mismatches.");
        builder.AppendLine("For test failures such as `CS0118` or `'Calculator' is a namespace but is used like a type`, create a distinct concrete domain type such as `<RootNamespace>.Domain.CalculatorEngine`, update the sibling tests to instantiate that type, and add a ProjectReference to the host before rerunning validation.");
        builder.AppendLine("For Blazor Web App scaffolds, the primary route belongs under `Components/Pages`. Move any calculator UI from legacy root `Pages/*.razor` into `Components/Pages/Home.razor` and delete the stale root route before rerunning build/test/launch validation.");
        builder.AppendLine("For `Home.razor` build errors such as `CS1503` converting `char` to `string`, fix the Razor callback argument mismatch before rerunning tests. Either change the handler signatures to `char` (`AppendDigit(char digit)`, `ChooseOperator(char op)`) and keep callbacks such as `@onclick=\"() => AppendDigit('1')\"`, or keep `string` handlers and use single-quoted Razor attributes such as `@onclick='() => AppendDigit(\"1\")'`. Do not leave `AppendToResult('1')` or `SetOperation('+')` calling methods that still accept `string`.");
        builder.AppendLine("For `Home.razor` `RZ9988` or `@page \"\"` build errors, set the route directive to `@page \"/\"` before touching tests; do not rerun `workspace_dotnet_test` while the host build is red.");
        builder.AppendLine("For host build errors `CS0101` or `CS0111` involving `CalculatorEngine`, inspect for duplicate source files such as `Calculator/CalculatorEngine.cs` plus `Calculator/Domain/CalculatorEngine.cs`. Delete the stale top-level engine file and keep one concrete engine under `Calculator/Domain` before rerunning build/test.");
        builder.AppendLine("If the host build mentions `Pages/_Host.cshtml`, `typeof(App)`, `Startup.cs`, `UseStartup<Startup>()`, `blazor.server.js`, or ASP.NET Core 7.x component package warnings, a repair attempt polluted the Blazor Web App with old Blazor Server hosting. Delete `Pages/_Host.cshtml`, `Startup.cs`, legacy root `Pages/*.cshtml`, and stale root `Pages/*.razor` routes, remove obsolete `Microsoft.AspNetCore.Components*` package references, restore the generated minimal `Program.cs`/`Components/App.razor`/`Components/Routes.razor` shape, and put the UI in `Components/Pages/Home.razor` before rebuilding.");
        builder.AppendLine("For Blazor builds that mention `_Imports.razor` with `CS0138` or a type being used as a namespace, remove the bad root namespace import or rename the conflicting domain type to a distinct name such as `CalculatorEngine` under a concrete namespace such as `<RootNamespace>.Domain`.");
        builder.AppendLine("Remember that `Components/Calculator.razor` in a root namespace named `Calculator` generates a `Calculator` type too. Rename that component to `CalculatorPage.razor` or move the route into `Components/Pages/Home.razor` before rebuilding.");
        builder.AppendLine("If `MainLayout` was renamed, restore it or update all `MainLayout` references in the same repair before rerunning the build.");
        return builder.ToString().Trim();
    }

    private static IReadOnlyList<MisplacedTestProjectCleanupTarget> ResolveMisplacedTestProjectCleanupTargets(
        IReadOnlyList<ToolExecutionReceiptRecord> unresolvedCriticalToolFailures)
    {
        var targets = new Dictionary<string, MisplacedTestProjectCleanupTarget>(StringComparer.OrdinalIgnoreCase);
        foreach (var receipt in unresolvedCriticalToolFailures)
        {
            if (!string.Equals(NormalizeToolToken(receipt.ToolName), "workspace_dotnet_build", StringComparison.Ordinal))
            {
                continue;
            }

            foreach (var projectPath in ResolveProjectPathsFromToolRequest(receipt.RequestSummary))
            {
                var projectName = Path.GetFileNameWithoutExtension(projectPath);
                if (string.IsNullOrWhiteSpace(projectName) || IsTestProjectName(projectName))
                {
                    continue;
                }

                var projectDirectory = ResolvePromptDirectory(projectPath);
                if (string.IsNullOrWhiteSpace(projectDirectory))
                {
                    continue;
                }

                var nestedTestDirectoryPath = $"{projectDirectory}/{projectName}.Tests";
                targets.TryAdd(
                    nestedTestDirectoryPath,
                    new MisplacedTestProjectCleanupTarget(projectPath, nestedTestDirectoryPath));
            }
        }

        return targets.Values.ToList();
    }

    private static IReadOnlyList<string> ResolveProjectPathsFromToolRequest(string requestSummary)
    {
        if (string.IsNullOrWhiteSpace(requestSummary))
        {
            return [];
        }

        var paths = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in ProjectPathInToolRequestRegex.Matches(requestSummary))
        {
            var candidatePath = match.Groups["path"].Value;
            if (TryMapProjectPathForPrompt(candidatePath, out var promptPath))
            {
                paths.Add(promptPath);
            }
        }

        return paths.ToList();
    }

    private static bool TryMapProjectPathForPrompt(string projectPath, out string promptPath)
    {
        promptPath = string.Empty;
        var normalized = projectPath.Trim().TrimEnd(',', ';', '.', ')', ']').Replace('\\', '/');
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        if (normalized.StartsWith($"{ExternalTargetAliasRoot}/", StringComparison.OrdinalIgnoreCase))
        {
            promptPath = normalized;
            return true;
        }

        if (normalized.Length < 3 || !char.IsLetter(normalized[0]) || normalized[1] != ':' || normalized[2] != '/')
        {
            return false;
        }

        var driveLetter = char.ToUpperInvariant(normalized[0]);
        var remainder = normalized.Length == 3
            ? string.Empty
            : normalized[3..].Trim('/');
        promptPath = string.IsNullOrWhiteSpace(remainder)
            ? $"{ExternalTargetAliasRoot}/{driveLetter}"
            : $"{ExternalTargetAliasRoot}/{driveLetter}/{remainder}";
        return true;
    }

    private static string ResolvePromptDirectory(string promptPath)
    {
        var normalized = promptPath.Replace('\\', '/').TrimEnd('/');
        var lastSlash = normalized.LastIndexOf('/');
        return lastSlash <= 0
            ? string.Empty
            : normalized[..lastSlash];
    }

    private static bool IsTestProjectName(string projectName)
    {
        return projectName.EndsWith(".Tests", StringComparison.OrdinalIgnoreCase) ||
               projectName.EndsWith("Tests", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildDotnetFrameworkRecoveryGuidance(
        DispatchCandidate candidate,
        IReadOnlyList<ToolExecutionReceiptRecord> unresolvedCriticalToolFailures,
        string responseText)
    {
        var dotnetFailureSummary = string.Join(
            Environment.NewLine,
            unresolvedCriticalToolFailures
                .Where(IsFrameworkRecoverableDotnetToolFailure)
                .Select(item => item.ExitSummary));
        if (string.IsNullOrWhiteSpace(dotnetFailureSummary))
        {
            return string.Empty;
        }

        var combinedFailureText = string.Join(
            Environment.NewLine,
            new[] { dotnetFailureSummary, responseText }.Where(item => !string.IsNullOrWhiteSpace(item)));
        if (!MentionsMissingDotnetFramework(combinedFailureText))
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        builder.AppendLine("A previous dotnet validation failed because the generated project targeted a framework/runtime that is not available in this workspace.");
        builder.AppendLine("Inspect the generated `.csproj` files now and replace unsupported target frameworks such as `net7.0` with a supported target before rerunning the failed dotnet validation.");
        if (RequiresConcreteImplementationProof(candidate))
        {
            builder.AppendLine("For new greenfield .NET projects in this repository, prefer `workspace_dotnet_new`; if you must author a project file manually, prefer `net10.0` unless the project structure or existing solution explicitly requires another target.");
        }
        else
        {
            builder.AppendLine("This retry must repair the concrete solution or project configuration, not just report the mismatch.");
            builder.AppendLine("Update the affected `.csproj` or solution files to a supported target, then rerun the originally required dotnet validation successfully before you conclude.");
            builder.AppendLine("If the project was bootstrapped during this process and no stricter runtime is required, prefer `net10.0` for the repaired target.");
        }
        return builder.ToString().Trim();
    }

    private static bool IsFrameworkRecoverableDotnetToolFailure(ToolExecutionReceiptRecord receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);

        return string.Equals(NormalizeToolToken(receipt.ToolName), "workspace_dotnet_build", StringComparison.Ordinal) ||
               string.Equals(NormalizeToolToken(receipt.ToolName), "workspace_dotnet_test", StringComparison.Ordinal) ||
               string.Equals(NormalizeToolToken(receipt.ToolName), "workspace_dotnet_run", StringComparison.Ordinal) ||
               string.Equals(NormalizeToolToken(receipt.ToolName), "workspace_dotnet_publish", StringComparison.Ordinal);
    }

    private static bool IsSuccessfulUpstreamValidationReceipt(ToolExecutionReceiptRecord receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);

        if (IsFailedToolReceipt(receipt))
        {
            return false;
        }

        return string.Equals(NormalizeToolToken(receipt.ToolName), "workspace_dotnet_build", StringComparison.Ordinal) ||
               string.Equals(NormalizeToolToken(receipt.ToolName), "workspace_dotnet_test", StringComparison.Ordinal);
    }

    private static bool MentionsMissingDotnetFramework(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return text.Contains("You must install or update .NET", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("Microsoft.NETCore.App", StringComparison.OrdinalIgnoreCase) &&
               text.Contains("was not found", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("NETSDK1045", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("is not supported by this SDK", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("TargetFramework", StringComparison.OrdinalIgnoreCase) &&
               text.Contains("net7.0", StringComparison.OrdinalIgnoreCase);
    }

    private static bool MentionsRepeatedToolInvocation(string? text)
    {
        return !string.IsNullOrWhiteSpace(text) &&
               text.Contains("repeated identical tool invocation", StringComparison.OrdinalIgnoreCase);
    }

}
