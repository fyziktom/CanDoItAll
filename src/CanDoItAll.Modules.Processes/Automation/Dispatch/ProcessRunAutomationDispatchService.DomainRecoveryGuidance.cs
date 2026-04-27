using CanDoItAll.AgentFramework.Models;
using System.Text;

namespace CanDoItAll.Modules.Processes;

internal sealed partial class ProcessRunAutomationDispatchService
{
    private static string BuildDomainRecoveryFocusGuidance(
        DispatchCandidate candidate,
        ExecutionRunDetail detail,
        string? responseText,
        string missingConcreteImplementationProofSummary,
        IReadOnlyList<string> missingRequiredTools,
        IReadOnlyList<ToolExecutionReceiptRecord> unresolvedCriticalToolFailures)
    {
        var context = CreateRecoveryGuidanceContext(
            candidate,
            detail,
            responseText,
            missingConcreteImplementationProofSummary,
            missingRequiredTools,
            unresolvedCriticalToolFailures,
            isImplementationRetry: false,
            isBrowserProofRetry: false);

        return ProcessAutomationRecoveryGuidanceProviders.BuildFocusGuidance(context);
    }

    private static void AppendDomainImplementationRecoveryGuidance(
        StringBuilder builder,
        DispatchCandidate candidate,
        ExecutionRunDetail detail,
        string? responseText,
        string missingConcreteImplementationProofSummary,
        IReadOnlyList<string> missingRequiredTools,
        IReadOnlyList<ToolExecutionReceiptRecord> unresolvedCriticalToolFailures)
    {
        var context = CreateRecoveryGuidanceContext(
            candidate,
            detail,
            responseText,
            missingConcreteImplementationProofSummary,
            missingRequiredTools,
            unresolvedCriticalToolFailures,
            isImplementationRetry: true,
            isBrowserProofRetry: false);

        ProcessAutomationRecoveryGuidanceProviders.AppendImplementationGuidance(builder, context);
    }

    private static void AppendDomainBrowserRecoveryGuidance(
        StringBuilder builder,
        DispatchCandidate candidate,
        ExecutionRunDetail detail,
        string? responseText,
        string missingConcreteImplementationProofSummary,
        IReadOnlyList<string> missingRequiredTools,
        IReadOnlyList<ToolExecutionReceiptRecord> unresolvedCriticalToolFailures)
    {
        var context = CreateRecoveryGuidanceContext(
            candidate,
            detail,
            responseText,
            missingConcreteImplementationProofSummary,
            missingRequiredTools,
            unresolvedCriticalToolFailures,
            isImplementationRetry: false,
            isBrowserProofRetry: true);

        ProcessAutomationRecoveryGuidanceProviders.AppendBrowserGuidance(builder, context);
    }

    private static ProcessRecoveryGuidanceContext CreateRecoveryGuidanceContext(
        DispatchCandidate candidate,
        ExecutionRunDetail detail,
        string? responseText,
        string missingConcreteImplementationProofSummary,
        IReadOnlyList<string> missingRequiredTools,
        IReadOnlyList<ToolExecutionReceiptRecord> unresolvedCriticalToolFailures,
        bool isImplementationRetry,
        bool isBrowserProofRetry)
    {
        return new ProcessRecoveryGuidanceContext(
            candidate,
            detail,
            responseText,
            missingConcreteImplementationProofSummary,
            missingRequiredTools,
            unresolvedCriticalToolFailures,
            isImplementationRetry,
            isBrowserProofRetry);
    }

    private sealed record ProcessRecoveryGuidanceContext(
        DispatchCandidate Candidate,
        ExecutionRunDetail Detail,
        string? ResponseText,
        string MissingConcreteImplementationProofSummary,
        IReadOnlyList<string> MissingRequiredTools,
        IReadOnlyList<ToolExecutionReceiptRecord> UnresolvedCriticalToolFailures,
        bool IsImplementationRetry,
        bool IsBrowserProofRetry);

    private sealed record ProcessRecoveryGuidance(
        IReadOnlyList<string> FocusLines,
        IReadOnlyList<string> ImplementationLines,
        IReadOnlyList<string> BrowserLines)
    {
        public static readonly ProcessRecoveryGuidance Empty = new([], [], []);

        public static ProcessRecoveryGuidance FromText(
            string focusText = "",
            string implementationText = "",
            string browserText = "")
        {
            return new ProcessRecoveryGuidance(
                SplitLines(focusText),
                SplitLines(implementationText),
                SplitLines(browserText));
        }

        private static IReadOnlyList<string> SplitLines(string text)
        {
            return string.IsNullOrWhiteSpace(text)
                ? []
                : text.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }
    }

    private interface IProcessAutomationRecoveryGuidanceProvider
    {
        bool CanHandle(ProcessRecoveryGuidanceContext context);

        ProcessRecoveryGuidance BuildGuidance(ProcessRecoveryGuidanceContext context);
    }

    private static class ProcessAutomationRecoveryGuidanceProviders
    {
        private static readonly IReadOnlyList<IProcessAutomationRecoveryGuidanceProvider> Providers =
        [
            new DotnetProjectProcessAutomationRecoveryGuidanceProvider(),
            new BlazorProcessAutomationRecoveryGuidanceProvider(),
            new CalculatorProcessAutomationRecoveryGuidanceProvider()
        ];

        public static string BuildFocusGuidance(ProcessRecoveryGuidanceContext context)
        {
            var lines = BuildGuidance(context)
                .SelectMany(guidance => guidance.FocusLines)
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToList();

            return lines.Count == 0
                ? string.Empty
                : string.Join(Environment.NewLine, lines);
        }

        public static void AppendImplementationGuidance(
            StringBuilder builder,
            ProcessRecoveryGuidanceContext context)
        {
            AppendLines(
                builder,
                BuildGuidance(context).SelectMany(guidance => guidance.ImplementationLines));
        }

        public static void AppendBrowserGuidance(
            StringBuilder builder,
            ProcessRecoveryGuidanceContext context)
        {
            AppendLines(
                builder,
                BuildGuidance(context).SelectMany(guidance => guidance.BrowserLines));
        }

        private static IEnumerable<ProcessRecoveryGuidance> BuildGuidance(ProcessRecoveryGuidanceContext context)
        {
            return Providers
                .Where(provider => provider.CanHandle(context))
                .Select(provider => provider.BuildGuidance(context));
        }

        private static void AppendLines(StringBuilder builder, IEnumerable<string> lines)
        {
            foreach (var line in lines.Where(line => !string.IsNullOrWhiteSpace(line)))
            {
                builder.AppendLine(line.Trim());
            }
        }
    }

    private sealed class DotnetProjectProcessAutomationRecoveryGuidanceProvider : IProcessAutomationRecoveryGuidanceProvider
    {
        public bool CanHandle(ProcessRecoveryGuidanceContext context)
        {
            return context.IsImplementationRetry ||
                   context.UnresolvedCriticalToolFailures.Count > 0;
        }

        public ProcessRecoveryGuidance BuildGuidance(ProcessRecoveryGuidanceContext context)
        {
            var builder = new StringBuilder();
            if (context.IsImplementationRetry)
            {
                builder.AppendLine("If the scaffold is greenfield, create the actual solution and project files now with workspace_dotnet_new or a controlled helper path instead of writing only a source file set.");
                builder.AppendLine("If the host or sibling test project already exists from an earlier attempt, inspect and repair the existing scaffold in place instead of recreating it with --force.");
                builder.AppendLine("If a prior workspace_dotnet_new attempt failed because files already existed or the template wanted to overwrite content, inspect the target directory immediately. When the scaffold is already present at the required path, continue by repairing, reading, and building that existing project in place instead of declaring the retry blocked.");
                builder.AppendLine("If you retry a greenfield .NET bootstrap with workspace_dotnet_new, explicitly request a supported target framework such as net10.0 instead of accepting an older template default.");
                builder.AppendLine("If this implementation produces browser-facing UI files such as .razor, .cshtml, or wwwroot assets, leave a runnable web host and startup entrypoint in place for downstream QA. Do not stop at a plain class library.");
                builder.AppendLine("Do not stop at a starter template or say the app is merely ready for later feature implementation. Replace default template output with the requested product behavior before you conclude.");
                builder.AppendLine("On this retry, repair placeholder or incomplete product files before validating. A validation-only retry is acceptable only when read-back proves the current concrete source already satisfies the full implementation contract, then build and tests pass without any later mutation.");
            }

            var misplacedTestProjectRecoveryGuidance = BuildMisplacedTestProjectRecoveryGuidance(context.UnresolvedCriticalToolFailures);
            if (!string.IsNullOrWhiteSpace(misplacedTestProjectRecoveryGuidance))
            {
                builder.AppendLine(misplacedTestProjectRecoveryGuidance);
            }

            var frameworkRecoveryGuidance = BuildDotnetFrameworkRecoveryGuidance(
                context.Candidate,
                context.UnresolvedCriticalToolFailures,
                context.ResponseText ?? string.Empty);
            if (!string.IsNullOrWhiteSpace(frameworkRecoveryGuidance))
            {
                builder.AppendLine(frameworkRecoveryGuidance);
            }

            return ProcessRecoveryGuidance.FromText(implementationText: builder.ToString());
        }
    }

    private sealed class BlazorProcessAutomationRecoveryGuidanceProvider : IProcessAutomationRecoveryGuidanceProvider
    {
        public bool CanHandle(ProcessRecoveryGuidanceContext context)
        {
            return (context.IsImplementationRetry || context.IsBrowserProofRetry) &&
                   ContainsBlazorContext(context);
        }

        public ProcessRecoveryGuidance BuildGuidance(ProcessRecoveryGuidanceContext context)
        {
            var implementationBuilder = new StringBuilder();
            if (context.IsImplementationRetry)
            {
                implementationBuilder.AppendLine("If the project structure names Blazor SSR, repair toward a runnable Blazor SSR app instead of MVC, Razor Pages, or controller/view placeholders.");
                implementationBuilder.AppendLine("Keep test projects outside the Blazor host folder. If a previous attempt left nested test files under the host project, remove that stale nested test folder before rerunning the host build.");
                implementationBuilder.AppendLine("If the build error mentions missing test attribute namespaces in the host project, treat that as misplaced test code under the host and fix the file layout, not the production host dependencies.");
                implementationBuilder.AppendLine("For Blazor Web App scaffolds, keep routed pages under Components/Pages, keep Components/Routes.razor as the Router host, and repair layout references before rebuilding.");

                var blazorBuildRecoveryGuidance = BuildBlazorBuildRecoveryGuidance(
                    context.Candidate,
                    context.UnresolvedCriticalToolFailures,
                    context.ResponseText ?? string.Empty);
                if (!string.IsNullOrWhiteSpace(blazorBuildRecoveryGuidance))
                {
                    implementationBuilder.AppendLine(blazorBuildRecoveryGuidance);
                }
            }

            var browserBuilder = new StringBuilder();
            if (context.IsBrowserProofRetry)
            {
                browserBuilder.AppendLine("If the launched Blazor app returns HTTP 500, inspect the captured logs and route files before claiming browser proof.");
                browserBuilder.AppendLine("For button-driven Blazor apps, click a representative sequence and assert that the visible display or history changes to the expected result.");
            }

            return ProcessRecoveryGuidance.FromText(
                implementationText: implementationBuilder.ToString(),
                browserText: browserBuilder.ToString());
        }

        private static bool ContainsBlazorContext(ProcessRecoveryGuidanceContext context)
        {
            var text = string.Join(
                Environment.NewLine,
                context.Candidate.Definition.Name,
                context.Candidate.Definition.Summary,
                context.Candidate.Definition.ValueStatement,
                context.Candidate.Run.Name,
                context.Candidate.Run.TriggerReason,
                context.Candidate.StepRun.Title,
                context.Candidate.WorkBrief?.Title,
                context.Candidate.WorkBrief?.WorkBriefText,
                context.Candidate.WorkBrief?.ExpectedOutcome,
                context.Candidate.WorkBrief?.EvidenceExpectationSummary,
                context.ResponseText,
                string.Join(Environment.NewLine, context.UnresolvedCriticalToolFailures.Select(item => item.ExitSummary)));

            return text.Contains("Blazor", StringComparison.OrdinalIgnoreCase) ||
                   text.Contains(".razor", StringComparison.OrdinalIgnoreCase) ||
                   text.Contains("Components/Pages", StringComparison.OrdinalIgnoreCase) ||
                   text.Contains("wwwroot", StringComparison.OrdinalIgnoreCase);
        }
    }

    private sealed class CalculatorProcessAutomationRecoveryGuidanceProvider : IProcessAutomationRecoveryGuidanceProvider
    {
        public bool CanHandle(ProcessRecoveryGuidanceContext context)
        {
            return ContainsCalculatorContext(context.Candidate) ||
                   RequiresCalculatorLikeImplementationProof(context.Candidate, context.Detail);
        }

        public ProcessRecoveryGuidance BuildGuidance(ProcessRecoveryGuidanceContext context)
        {
            var focusText = BuildCalculatorRecoveryFocusGuidance(
                context.Candidate,
                context.ResponseText,
                context.MissingConcreteImplementationProofSummary,
                context.MissingRequiredTools,
                context.UnresolvedCriticalToolFailures);

            var implementationText = context.IsImplementationRetry &&
                                     RequiresCalculatorLikeImplementationProof(context.Candidate, context.Detail)
                ? BuildCalculatorRecoveryChecklist(context.MissingConcreteImplementationProofSummary)
                : string.Empty;

            var browserText = context.IsBrowserProofRetry
                ? "For button-driven Blazor apps such as calculators, click a representative sequence and assert that the visible display or history changes to the expected result. If `@onclick` buttons do not mutate state in the browser, block with a Blazor render-mode or static-SSR implementation defect."
                : string.Empty;

            return ProcessRecoveryGuidance.FromText(focusText, implementationText, browserText);
        }
    }
}
