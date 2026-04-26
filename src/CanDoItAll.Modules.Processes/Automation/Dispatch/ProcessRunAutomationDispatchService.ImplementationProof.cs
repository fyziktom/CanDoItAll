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
    private static string ResolveMissingConcreteImplementationProofSummary(
        DispatchCandidate candidate,
        ExecutionRunDetail detail)
    {
        if (!RequiresConcreteImplementationProof(candidate))
        {
            return string.Empty;
        }

        if (ResolveProcessMockArtifactProjections(detail.Run.SerializedSessionStateJson)
            .Any(projection => CanSatisfyConcreteImplementationProofWithProcessMock(candidate, projection)))
        {
            return string.Empty;
        }

        var successfulReceipts = detail.ToolReceipts
            .Where(receipt => !IsFailedToolReceipt(receipt))
            .ToList();
        var concreteReadReceipt = ResolveLatestReceipt(
            successfulReceipts,
            "workspace_read_file",
            requireConcreteProductPath: true,
            requireConcreteSourceOrProjectPath: true);
        if (concreteReadReceipt is null)
        {
            return "the current attempt did not read any concrete product source or project file";
        }

        var concreteMutationReceipts = successfulReceipts
            .Where(receipt => ConcreteProductMutationToolNames.Contains(NormalizeToolToken(receipt.ToolName)))
            .Where(IsConcreteProductMutationReceipt)
            .ToList();

        var latestMutationReceipt = concreteMutationReceipts
            .OrderByDescending(receipt => receipt.CompletedAtUtc)
            .ThenByDescending(receipt => receipt.StartedAtUtc)
            .FirstOrDefault();

        var blazorRouteProofSummary = ResolveMissingBlazorWebAppRouteProofSummary(candidate, detail, successfulReceipts);
        if (!string.IsNullOrWhiteSpace(blazorRouteProofSummary))
        {
            return blazorRouteProofSummary;
        }

        var blazorHostingShapeSummary = ResolveInvalidBlazorWebAppHostingShapeSummary(candidate, detail, successfulReceipts);
        if (!string.IsNullOrWhiteSpace(blazorHostingShapeSummary))
        {
            return blazorHostingShapeSummary;
        }

        var calculatorImplementationSummary = ResolveMissingCalculatorLikeImplementationProofSummary(candidate, detail, successfulReceipts);
        if (!string.IsNullOrWhiteSpace(calculatorImplementationSummary))
        {
            return calculatorImplementationSummary;
        }

        var successfulBuildReceipt = ResolveLatestReceipt(
            successfulReceipts,
            "workspace_dotnet_build",
            requireConcreteProductPath: false,
            requireConcreteSourceOrProjectPath: false);
        if (successfulBuildReceipt is null)
        {
            return "the current attempt did not run workspace_dotnet_build successfully";
        }

        var buildTargetPaths = ResolveWorkspacePathsFromToolRequest(successfulBuildReceipt.RequestSummary);
        if (buildTargetPaths.Count > 0 && !buildTargetPaths.Any(IsConcreteProductPath))
        {
            return "the current attempt built only managed artifact paths instead of the concrete product project";
        }

        if (latestMutationReceipt is not null)
        {
            if (IsReceiptAfter(latestMutationReceipt, successfulBuildReceipt))
            {
                return "workspace_dotnet_build ran before the latest concrete product mutation";
            }

            if (IsReceiptAfter(latestMutationReceipt, concreteReadReceipt))
            {
                return "workspace_read_file ran before the latest concrete product mutation";
            }

            var latestScaffoldReceipt = concreteMutationReceipts
                .Where(receipt => string.Equals(NormalizeToolToken(receipt.ToolName), "workspace_dotnet_new", StringComparison.Ordinal))
                .OrderByDescending(receipt => receipt.CompletedAtUtc)
                .ThenByDescending(receipt => receipt.StartedAtUtc)
                .FirstOrDefault();
            if (latestScaffoldReceipt is not null &&
                !successfulReceipts.Any(receipt =>
                    ConcreteProductSourceWriteToolNames.Contains(NormalizeToolToken(receipt.ToolName)) &&
                    IsReceiptAfter(receipt, latestScaffoldReceipt) &&
                    HasConcreteProductSourceOrProjectPath(receipt)))
            {
                return "the latest scaffold was not followed by a concrete product source or project file write";
            }
        }

        if (RequiresConcreteTestProof(candidate))
        {
            var successfulTestReceipt = ResolveLatestReceipt(
                successfulReceipts,
                "workspace_dotnet_test",
                requireConcreteProductPath: false,
                requireConcreteSourceOrProjectPath: false);
            if (successfulTestReceipt is null)
            {
                return "the current implementation attempt did not run workspace_dotnet_test successfully even though this step includes tests";
            }

            var testTargetPaths = ResolveWorkspacePathsFromToolRequest(successfulTestReceipt.RequestSummary);
            if (testTargetPaths.Count > 0 && !testTargetPaths.Any(IsConcreteProductPath))
            {
                return "the current implementation attempt tested only managed artifact paths instead of the concrete product test project";
            }

            if (latestMutationReceipt is not null &&
                IsReceiptAfter(latestMutationReceipt, successfulTestReceipt))
            {
                return "workspace_dotnet_test ran before the latest concrete product mutation";
            }
        }

        return string.Empty;
    }

    private static string ResolveMissingBlazorWebAppRouteProofSummary(
        DispatchCandidate candidate,
        ExecutionRunDetail detail,
        IReadOnlyList<ToolExecutionReceiptRecord> successfulReceipts)
    {
        if (!RequiresBlazorWebAppRouteProof(candidate, detail, successfulReceipts))
        {
            return string.Empty;
        }

        var hasComponentsPagesMutation = successfulReceipts
            .Where(IsConcreteProductSourceMutationReceipt)
            .Any(HasConcreteBlazorComponentsPagePath);
        var componentPageReads = successfulReceipts
            .Where(receipt => string.Equals(NormalizeToolToken(receipt.ToolName), "workspace_read_file", StringComparison.Ordinal))
            .Where(HasConcreteBlazorComponentsPagePath)
            .ToList();
        if (!hasComponentsPagesMutation &&
            componentPageReads.Count == 0)
        {
            var hasLegacyRootPageMutation = successfulReceipts
                .Where(IsConcreteProductSourceMutationReceipt)
                .Any(HasConcreteLegacyRootPagePath);
            return hasLegacyRootPageMutation
                ? "the current Blazor Web App attempt mutated a legacy root Pages/*.razor route instead of Components/Pages/*.razor; move that UI into Components/Pages/Home.razor and delete stale root Pages/Home.razor or Pages/Index.razor routes"
                : "the current Blazor Web App attempt did not read or mutate any routed page under Components/Pages";
        }

        var latestComponentsPagesMutation = successfulReceipts
            .Where(IsConcreteProductSourceMutationReceipt)
            .Where(HasConcreteBlazorComponentsPagePath)
            .OrderByDescending(receipt => receipt.CompletedAtUtc)
            .ThenByDescending(receipt => receipt.StartedAtUtc)
            .FirstOrDefault();
        var latestComponentsPagesRead = componentPageReads
            .OrderByDescending(receipt => receipt.CompletedAtUtc)
            .ThenByDescending(receipt => receipt.StartedAtUtc)
            .FirstOrDefault();
        if (latestComponentsPagesMutation is not null &&
            (latestComponentsPagesRead is null || IsReceiptAfter(latestComponentsPagesMutation, latestComponentsPagesRead)))
        {
            return "workspace_read_file for the Components/Pages routed page ran before the latest routed page mutation";
        }

        return string.Empty;
    }

    private static string ResolveInvalidBlazorWebAppHostingShapeSummary(
        DispatchCandidate candidate,
        ExecutionRunDetail detail,
        IReadOnlyList<ToolExecutionReceiptRecord> successfulReceipts)
    {
        if (!RequiresBlazorWebAppRouteProof(candidate, detail, successfulReceipts))
        {
            return string.Empty;
        }

        var routeFileWrites = ResolveSuccessfulSessionFileWrites(detail.Run.SerializedSessionStateJson)
            .Where(item => IsBlazorRoutesPath(item.Path))
            .ToList();
        var routeFileReads = ResolveSuccessfulSessionFileReads(detail.Run.SerializedSessionStateJson)
            .Where(item => IsBlazorRoutesPath(item.Path))
            .ToList();

        if (routeFileWrites.Concat(routeFileReads).Any(item => ContainsRazorPageDirective(item.Content)))
        {
            return "the current Blazor Web App attempt left an @page directive in Components/Routes.razor; restore Routes.razor as the Router-only host and keep route directives in Components/Pages/Home.razor";
        }

        var sessionFileContents = routeFileWrites
            .Concat(routeFileReads)
            .Concat(ResolveSuccessfulSessionFileWrites(detail.Run.SerializedSessionStateJson))
            .Concat(ResolveSuccessfulSessionFileReads(detail.Run.SerializedSessionStateJson))
            .ToList();
        if (sessionFileContents
            .Where(item => IsBlazorHostProgramPath(item.Path))
            .Any(item => ContainsBlazorWebAssemblyHostingContent(item.Content)))
        {
            return "the current Blazor Web App attempt replaced Program.cs with WebAssemblyHostBuilder hosting; restore the generated WebApplication/AddRazorComponents/MapRazorComponents<App>() server-side Blazor Web App shape";
        }

        if (sessionFileContents
            .Where(item => IsBlazorHostProjectFilePath(item.Path))
            .Any(item => ContainsLegacyBlazorComponentPackageReferences(item.Content)))
        {
            return "the current Blazor Web App attempt added obsolete ASP.NET Core 7 component package references to the net10 host project; remove those package references and rely on the shared framework";
        }

        return routeFileReads.Count == 0
            ? "the current Blazor Web App attempt did not read Components/Routes.razor to verify the generated Router hosting shape"
            : string.Empty;
    }

    private static string ResolveMissingCalculatorLikeImplementationProofSummary(
        DispatchCandidate candidate,
        ExecutionRunDetail detail,
        IReadOnlyList<ToolExecutionReceiptRecord> successfulReceipts)
    {
        if (!RequiresCalculatorLikeImplementationProof(candidate, detail))
        {
            return string.Empty;
        }

        var fileWrites = ResolveSuccessfulSessionFileWrites(detail.Run.SerializedSessionStateJson);
        var fileReads = ResolveSuccessfulSessionFileReads(detail.Run.SerializedSessionStateJson);
        var fileContents = fileWrites
            .Concat(fileReads)
            .ToList();
        var engineWrites = fileWrites
            .Where(item => IsCalculatorEngineSourcePath(item.Path))
            .ToList();
        var engineContents = fileContents
            .Where(item => IsCalculatorEngineSourcePath(item.Path))
            .ToList();
        if (engineContents.Count == 0)
        {
            return "the current calculator implementation attempt did not write or read a concrete CalculatorEngine domain/application source file";
        }

        if (!engineContents.Any(item => ContainsCalculatorEngineImplementation(item.Content)))
        {
            return "the current calculator implementation wrote CalculatorEngine without concrete Add, Subtract, Multiply, and Divide operations";
        }

        if (engineWrites.Count > 0 &&
            !successfulReceipts.Any(receipt =>
                string.Equals(NormalizeToolToken(receipt.ToolName), "workspace_read_file", StringComparison.Ordinal) &&
                ResolveWorkspacePathsFromToolRequest(receipt.RequestSummary).Any(IsCalculatorEngineSourcePath)))
        {
            return "the current calculator implementation attempt did not read CalculatorEngine after writing it";
        }

        var routedPageContents = fileContents
            .Where(item => IsBlazorComponentsPagePath(item.Path))
            .ToList();
        if (routedPageContents.Any(item => ContainsMalformedDoubleQuotedRazorStringCallback(item.Content)))
        {
            return "the current calculator routed UI wrote a string literal inside a double-quoted Razor event attribute; either change the handlers to char signatures and use @onclick=\"() => AppendDigit('1')\", or keep string handlers and use single-quoted attributes such as @onclick='() => AppendDigit(\"1\")'";
        }

        if (routedPageContents.Any(item => ContainsCalculatorStringHandlerWithCharCallback(item.Content)))
        {
            return "the current calculator routed UI passes char literals to handlers that still accept string, causing CS1503; either change those handlers to char parameters or keep string handlers and use single-quoted attributes such as @onclick='() => AppendToResult(\"1\")'";
        }

        if (!routedPageContents.Any(item => ContainsCalculatorRoutedUiContent(item.Content)))
        {
            return "the current calculator implementation attempt did not leave a non-placeholder Components/Pages routed UI with CalculatorEngine-backed arithmetic controls, equals/evaluate behavior, keypad, and history";
        }

        if (routedPageContents.Any(item => ContainsInjectedCalculatorEngine(item.Content)))
        {
            var programContents = fileContents
                .Where(item => IsBlazorHostProgramPath(item.Path))
                .ToList();
            if (!programContents.Any(item => ContainsCalculatorEngineServiceRegistration(item.Content)))
            {
                return "the current calculator implementation injects CalculatorEngine in the routed UI but did not register CalculatorEngine in Program.cs before building the app";
            }
        }

        if (!RequiresConcreteTestProof(candidate))
        {
            return string.Empty;
        }

        var testProjectWrites = fileContents
            .Where(item => IsConcreteTestProjectFilePath(item.Path))
            .ToList();
        if (!testProjectWrites.Any(item => ContainsCalculatorHostProjectReference(item.Content)))
        {
            return "the current calculator implementation attempt did not write or read a sibling test project with a ProjectReference to the Calculator host project";
        }

        var testSourceWrites = fileContents
            .Where(item => IsConcreteTestSourcePath(item.Path))
            .ToList();
        if (testSourceWrites.Count == 0)
        {
            return "the current calculator implementation attempt did not write or read meaningful sibling test source";
        }

        return testSourceWrites.Any(item => ContainsCalculatorEngineTestContent(item.Content))
            ? string.Empty
            : "the current calculator implementation attempt wrote tests that do not exercise CalculatorEngine arithmetic behavior";
    }

    private static bool RequiresCalculatorLikeImplementationProof(DispatchCandidate candidate, ExecutionRunDetail detail)
    {
        if (!RequiresConcreteImplementationProof(candidate))
        {
            return false;
        }

        var contextText = string.Join(
            Environment.NewLine,
            candidate.Definition.Name,
            candidate.Definition.Summary,
            candidate.Definition.ValueStatement,
            candidate.Run.Name,
            candidate.Run.TriggerReason,
            candidate.StepRun.Title,
            candidate.WorkBrief?.Title,
            candidate.WorkBrief?.WorkBriefText,
            candidate.WorkBrief?.ExpectedOutcome,
            candidate.WorkBrief?.EvidenceExpectationSummary,
            detail.Run.InputSummary,
            detail.Run.ResultSummary);

        return contextText.Contains("Calculator", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsRazorPageDirective(string content)
    {
        return !string.IsNullOrWhiteSpace(content) &&
               RazorPageDirectiveRegex.IsMatch(content);
    }

    private static bool ContainsMalformedDoubleQuotedRazorStringCallback(string content)
    {
        return !string.IsNullOrWhiteSpace(content) &&
               MalformedDoubleQuotedRazorStringCallbackRegex.IsMatch(content);
    }

    private static bool ContainsCalculatorStringHandlerWithCharCallback(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return false;
        }

        return RazorCharLiteralCallbackRegex
            .Matches(content)
            .Cast<Match>()
            .Select(match => match.Groups["handler"].Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Any(handlerName => ContainsStringParameterHandler(content, handlerName));
    }

    private static bool ContainsStringParameterHandler(string content, string handlerName)
    {
        return Regex.IsMatch(
            content,
            $@"\b{Regex.Escape(handlerName)}\s*\(\s*string\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static bool ContainsBlazorWebAssemblyHostingContent(string content)
    {
        return !string.IsNullOrWhiteSpace(content) &&
               (content.Contains("WebAssemblyHostBuilder", StringComparison.Ordinal) ||
                content.Contains("Microsoft.AspNetCore.Components.WebAssembly.Hosting", StringComparison.Ordinal) ||
                content.Contains("RootComponents.Add<App>", StringComparison.Ordinal));
    }

    private static bool ContainsLegacyBlazorComponentPackageReferences(string content)
    {
        return !string.IsNullOrWhiteSpace(content) &&
               (content.Contains("Microsoft.AspNetCore.Components.WebAssembly", StringComparison.OrdinalIgnoreCase) ||
                content.Contains("Microsoft.AspNetCore.Components.Web\" Version=\"7.", StringComparison.OrdinalIgnoreCase) ||
                content.Contains("Microsoft.AspNetCore.Components\" Version=\"7.", StringComparison.OrdinalIgnoreCase));
    }

    private static bool ContainsCalculatorEngineImplementation(string content)
    {
        if (string.IsNullOrWhiteSpace(content) ||
            !content.Contains("CalculatorEngine", StringComparison.Ordinal))
        {
            return false;
        }

        return ContainsCalculatorOperation(content, "Add") &&
               ContainsCalculatorOperation(content, "Subtract") &&
               ContainsCalculatorOperation(content, "Multiply") &&
               ContainsCalculatorOperation(content, "Divide");
    }

    private static bool ContainsCalculatorRoutedUiContent(string content)
    {
        if (string.IsNullOrWhiteSpace(content) ||
            !ContainsRazorPageDirective(content) ||
            !content.Contains("CalculatorEngine", StringComparison.Ordinal))
        {
            return false;
        }

        return ContainsCalculatorUiOperation(content, "Add", "+") &&
               ContainsCalculatorUiOperation(content, "Subtract", "-") &&
               ContainsCalculatorUiOperation(content, "Multiply", "*") &&
               ContainsCalculatorUiOperation(content, "Divide", "/") &&
               ContainsEqualsOrEvaluateAction(content) &&
               ContainsCalculatorHistoryUi(content) &&
               ContainsCalculatorKeypadUi(content);
    }

    private static bool ContainsCalculatorUiOperation(string content, string operationName, string operationSymbol)
    {
        return content.Contains(operationName, StringComparison.OrdinalIgnoreCase) ||
               content.Contains(operationSymbol, StringComparison.Ordinal);
    }

    private static bool ContainsEqualsOrEvaluateAction(string content)
    {
        return content.Contains("Equals", StringComparison.OrdinalIgnoreCase) ||
               content.Contains("Evaluate", StringComparison.OrdinalIgnoreCase) ||
               content.Contains("Calculate", StringComparison.OrdinalIgnoreCase) ||
               content.Contains("=", StringComparison.Ordinal);
    }

    private static bool ContainsCalculatorHistoryUi(string content)
    {
        return content.Contains("history", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsCalculatorKeypadUi(string content)
    {
        if (content.Contains("keypad", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("AppendDigit", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("InputDigit", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var digitButtonMatches = Regex.Matches(
            content,
            @"(?is)<button\b[^>]*>\s*[0-9]\s*</button>|['""]\s*[0-9]\s*['""]");
        return digitButtonMatches.Count >= 10;
    }

    private static bool ContainsInjectedCalculatorEngine(string content)
    {
        return !string.IsNullOrWhiteSpace(content) &&
               (CalculatorEngineInjectDirectiveRegex.IsMatch(content) ||
                content.Contains("[Inject]", StringComparison.Ordinal) &&
                content.Contains("CalculatorEngine", StringComparison.Ordinal));
    }

    private static bool ContainsCalculatorEngineServiceRegistration(string content)
    {
        if (string.IsNullOrWhiteSpace(content) ||
            !content.Contains("CalculatorEngine", StringComparison.Ordinal))
        {
            return false;
        }

        return CalculatorEngineServiceRegistrationRegex.IsMatch(content);
    }

    private static bool ContainsCalculatorHostProjectReference(string content)
    {
        return !string.IsNullOrWhiteSpace(content) &&
               content.Contains("<ProjectReference", StringComparison.OrdinalIgnoreCase) &&
               content.Contains("Calculator.csproj", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsCalculatorEngineTestContent(string content)
    {
        if (string.IsNullOrWhiteSpace(content) ||
            !content.Contains("CalculatorEngine", StringComparison.Ordinal) ||
            !content.Contains("Assert.", StringComparison.Ordinal))
        {
            return false;
        }

        var hasTestAttribute =
            content.Contains("[Fact]", StringComparison.Ordinal) ||
            content.Contains("[Theory]", StringComparison.Ordinal) ||
            content.Contains("[TestMethod]", StringComparison.Ordinal) ||
            content.Contains("[Test]", StringComparison.Ordinal);
        return hasTestAttribute &&
               ContainsCalculatorOperation(content, "Add") &&
               ContainsCalculatorOperation(content, "Subtract") &&
               ContainsCalculatorOperation(content, "Multiply") &&
               ContainsCalculatorOperation(content, "Divide");
    }

    private static bool ContainsCalculatorOperation(string content, string operationName)
    {
        return content.Contains(operationName, StringComparison.OrdinalIgnoreCase);
    }

    private static bool RequiresBlazorWebAppRouteProof(
        DispatchCandidate candidate,
        ExecutionRunDetail detail,
        IReadOnlyList<ToolExecutionReceiptRecord> successfulReceipts)
    {
        if (!RequiresConcreteImplementationProof(candidate))
        {
            return false;
        }

        return successfulReceipts.Any(IsBlazorWebAppScaffoldReceipt) ||
               successfulReceipts.Any(HasConcreteBlazorComponentsPagePath) ||
               ContainsStrongBlazorWebAppContext(candidate, detail);
    }

    private static bool ContainsStrongBlazorWebAppContext(DispatchCandidate candidate, ExecutionRunDetail detail)
    {
        var contextText = string.Join(
            Environment.NewLine,
            candidate.Definition.Name,
            candidate.Definition.Summary,
            candidate.Definition.ValueStatement,
            candidate.Run.Name,
            candidate.Run.TriggerReason,
            candidate.StepRun.Title,
            candidate.WorkBrief?.WorkBriefText,
            candidate.WorkBrief?.HandoffSummary,
            candidate.WorkBrief?.ExpectedOutcome,
            candidate.WorkBrief?.EvidenceExpectationSummary,
            detail.Run.InputSummary,
            detail.Run.ResultSummary,
            detail.Run.SerializedSessionStateJson);

        return contextText.Contains("notes: Blazor", StringComparison.OrdinalIgnoreCase) ||
               contextText.Contains("Blazor Server-Side Rendering", StringComparison.OrdinalIgnoreCase) ||
               contextText.Contains("Blazor SSR (", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ShouldIncludeBlazorWebAppHostingContract(
        DispatchCandidate candidate,
        string? projectStructureGroundingSummary,
        string? artifactInspectionGroundingSummary)
    {
        var contextText = string.Join(
            Environment.NewLine,
            candidate.Definition.Name,
            candidate.Definition.Summary,
            candidate.Definition.ValueStatement,
            candidate.Run.Name,
            candidate.Run.TriggerReason,
            candidate.StepRun.Title,
            candidate.WorkBrief?.Title,
            candidate.WorkBrief?.WorkBriefText,
            candidate.WorkBrief?.HandoffSummary,
            candidate.WorkBrief?.ExpectedOutcome,
            candidate.WorkBrief?.EvidenceExpectationSummary,
            projectStructureGroundingSummary,
            artifactInspectionGroundingSummary);

        return contextText.Contains("Blazor", StringComparison.OrdinalIgnoreCase) ||
               contextText.Contains("dotnet new blazor", StringComparison.OrdinalIgnoreCase) ||
               contextText.Contains("Components/Pages", StringComparison.OrdinalIgnoreCase);
    }

    private static void AppendBlazorWebAppHostingContract(StringBuilder builder)
    {
        builder.AppendLine("- On current .NET, `dotnet new blazor` creates a Blazor Web App: `Program.cs` maps Razor components, the app shell is `Components/App.razor`, routing is `Components/Routes.razor`, and routed UI belongs under `Components/Pages`.");
        builder.AppendLine("- Treat `Blazor SSR`, `Blazor Server-Side Rendering`, or `Blazor Web App` as this Blazor Web App hosting shape, not as legacy Blazor Server plus Razor Pages.");
        builder.AppendLine("- Do not recommend, create, or preserve `Pages/_Host.cshtml`, `Startup.cs`, `UseStartup<Startup>()`, root `Pages/*.razor` routes, `blazor.server.js`, or ASP.NET Core 7.x `Microsoft.AspNetCore.Components*` package references for a net10 Blazor Web App.");
        builder.AppendLine("- If an upstream artifact says `Blazor Server-Side`, `Blazor Server`, or `Razor Pages` while the live project structure or scaffold says Blazor SSR/Web App, treat that wording as stale shorthand and normalize the implementation plan back to Blazor Web App with routed pages under `Components/Pages`.");
        builder.AppendLine("- If legacy hosting files are present from a prior bad repair, delete those specific legacy files first, restore the generated minimal Blazor Web App shape, then build and test. Do not recursively delete the host project directory or build on top of both hosting models.");
    }

    private static bool IsBlazorWebAppScaffoldReceipt(ToolExecutionReceiptRecord receipt)
    {
        return string.Equals(NormalizeToolToken(receipt.ToolName), "workspace_dotnet_new", StringComparison.Ordinal) &&
               receipt.RequestSummary.Contains("blazor", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsConcreteProductSourceMutationReceipt(ToolExecutionReceiptRecord receipt)
    {
        var toolName = NormalizeToolToken(receipt.ToolName);
        return (string.Equals(toolName, "workspace_write_file", StringComparison.Ordinal) ||
                string.Equals(toolName, "workspace_append_file", StringComparison.Ordinal) ||
                string.Equals(toolName, "workspace_move_path", StringComparison.Ordinal)) &&
               HasConcreteProductSourceOrProjectPath(receipt);
    }

    private static bool HasConcreteBlazorComponentsPagePath(ToolExecutionReceiptRecord receipt)
    {
        return ResolveWorkspacePathsFromToolRequest(receipt.RequestSummary)
            .Any(path => IsConcreteProductPath(path) && IsBlazorComponentsPagePath(path));
    }

    private static bool HasConcreteLegacyRootPagePath(ToolExecutionReceiptRecord receipt)
    {
        return ResolveWorkspacePathsFromToolRequest(receipt.RequestSummary)
            .Any(path => IsConcreteProductPath(path) && IsLegacyRootRazorPagePath(path));
    }

    private static bool IsBlazorRoutesPath(string promptPath)
    {
        var normalized = WorkspaceScopeDescriptor.NormalizeRelativePath(promptPath);
        return normalized.EndsWith("/Components/Routes.razor", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsBlazorComponentsPagePath(string promptPath)
    {
        var normalized = WorkspaceScopeDescriptor.NormalizeRelativePath(promptPath);
        return string.Equals(Path.GetExtension(normalized), ".razor", StringComparison.OrdinalIgnoreCase) &&
               normalized.Contains("/Components/Pages/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLegacyRootRazorPagePath(string promptPath)
    {
        var normalized = WorkspaceScopeDescriptor.NormalizeRelativePath(promptPath);
        return string.Equals(Path.GetExtension(normalized), ".razor", StringComparison.OrdinalIgnoreCase) &&
               normalized.Contains("/Pages/", StringComparison.OrdinalIgnoreCase) &&
               !normalized.Contains("/Components/Pages/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsBlazorHostProgramPath(string promptPath)
    {
        var normalized = WorkspaceScopeDescriptor.NormalizeRelativePath(promptPath);
        return IsConcreteProductSourceOrProjectPath(normalized) &&
               !normalized.Contains(".Tests/", StringComparison.OrdinalIgnoreCase) &&
               string.Equals(Path.GetFileName(normalized), "Program.cs", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsBlazorHostProjectFilePath(string promptPath)
    {
        var normalized = WorkspaceScopeDescriptor.NormalizeRelativePath(promptPath);
        if (!IsConcreteProductSourceOrProjectPath(normalized) ||
            !string.Equals(Path.GetExtension(normalized), ".csproj", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return !IsTestProjectName(Path.GetFileNameWithoutExtension(normalized));
    }

    private static bool IsCalculatorEngineSourcePath(string promptPath)
    {
        var normalized = WorkspaceScopeDescriptor.NormalizeRelativePath(promptPath);
        return IsConcreteProductSourceOrProjectPath(normalized) &&
               !normalized.Contains(".Tests/", StringComparison.OrdinalIgnoreCase) &&
               string.Equals(Path.GetFileName(normalized), "CalculatorEngine.cs", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsConcreteTestProjectFilePath(string promptPath)
    {
        var normalized = WorkspaceScopeDescriptor.NormalizeRelativePath(promptPath);
        if (!IsConcreteProductSourceOrProjectPath(normalized) ||
            !string.Equals(Path.GetExtension(normalized), ".csproj", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var projectName = Path.GetFileNameWithoutExtension(normalized);
        return IsTestProjectName(projectName);
    }

    private static bool IsConcreteTestSourcePath(string promptPath)
    {
        var normalized = WorkspaceScopeDescriptor.NormalizeRelativePath(promptPath);
        return IsConcreteProductSourceOrProjectPath(normalized) &&
               string.Equals(Path.GetExtension(normalized), ".cs", StringComparison.OrdinalIgnoreCase) &&
               normalized.Contains(".Tests/", StringComparison.OrdinalIgnoreCase);
    }

    private static ToolExecutionReceiptRecord? ResolveLatestReceipt(
        IEnumerable<ToolExecutionReceiptRecord> receipts,
        string normalizedToolName,
        bool requireConcreteProductPath,
        bool requireConcreteSourceOrProjectPath)
    {
        return receipts
            .Where(receipt => string.Equals(NormalizeToolToken(receipt.ToolName), normalizedToolName, StringComparison.Ordinal))
            .Where(receipt => !requireConcreteProductPath || HasConcreteProductPath(receipt))
            .Where(receipt => !requireConcreteSourceOrProjectPath || HasConcreteProductSourceOrProjectPath(receipt))
            .OrderByDescending(receipt => receipt.CompletedAtUtc)
            .ThenByDescending(receipt => receipt.StartedAtUtc)
            .FirstOrDefault();
    }

    private static bool IsConcreteProductMutationReceipt(ToolExecutionReceiptRecord receipt)
    {
        var toolName = NormalizeToolToken(receipt.ToolName);
        if (string.Equals(toolName, "workspace_write_file", StringComparison.Ordinal) ||
            string.Equals(toolName, "workspace_append_file", StringComparison.Ordinal))
        {
            return HasConcreteProductSourceOrProjectPath(receipt);
        }

        return HasConcreteProductPath(receipt);
    }

    private static bool HasConcreteProductPath(ToolExecutionReceiptRecord receipt)
    {
        return ResolveWorkspacePathsFromToolRequest(receipt.RequestSummary)
            .Any(IsConcreteProductPath);
    }

    private static bool HasConcreteProductSourceOrProjectPath(ToolExecutionReceiptRecord receipt)
    {
        return ResolveWorkspacePathsFromToolRequest(receipt.RequestSummary)
            .Any(IsConcreteProductSourceOrProjectPath);
    }

    private static IReadOnlyList<string> ResolveWorkspacePathsFromToolRequest(string requestSummary)
    {
        if (string.IsNullOrWhiteSpace(requestSummary))
        {
            return [];
        }

        var paths = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in WorkspacePathInToolRequestRegex.Matches(requestSummary))
        {
            var candidatePath = match.Groups["path"].Value;
            if (TryMapWorkspacePathForPrompt(candidatePath, out var promptPath))
            {
                paths.Add(promptPath);
            }
        }

        return paths.ToList();
    }

    private static bool TryMapWorkspacePathForPrompt(string path, out string promptPath)
    {
        promptPath = string.Empty;
        var normalized = path.Trim().TrimEnd(',', ';', '.', ')', ']', '}').Replace('\\', '/');
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

    private static bool IsConcreteProductSourceOrProjectPath(string promptPath)
    {
        if (!IsConcreteProductPath(promptPath))
        {
            return false;
        }

        var extension = Path.GetExtension(promptPath);
        return IsCodeOrProjectExtension(extension);
    }

    private static bool IsConcreteProductPath(string promptPath)
    {
        var normalized = WorkspaceScopeDescriptor.NormalizeRelativePath(promptPath);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return segments.Length > 0 &&
               !IsManagedRootSegment(segments[0]) &&
               !segments.Any(IsNonProductPathSegment);
    }

    private static bool IsNonProductPathSegment(string segment)
    {
        return IsManagedRootSegment(segment) ||
               string.Equals(segment, ".playwright-mcp", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsReceiptAfter(ToolExecutionReceiptRecord candidate, ToolExecutionReceiptRecord baseline)
    {
        return candidate.CompletedAtUtc > baseline.CompletedAtUtc ||
               candidate.CompletedAtUtc == baseline.CompletedAtUtc &&
               candidate.StartedAtUtc > baseline.StartedAtUtc;
    }

}
