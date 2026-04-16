using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Processes;
using System.Reflection;
using System.Text.Json;

namespace CanDoItAll.Tests.Integration;

public sealed class ProcessRunAutomationDispatchServiceTests
{
    [Fact]
    public void BuildWorkBrief_includes_step_notes_in_runtime_brief()
    {
        var buildWorkBrief = typeof(ProcessesService).GetMethod("BuildWorkBrief", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildWorkBrief method was not found.");

        var definition = new ProcessDefinition
        {
            Name = "Software delivery",
            ValueStatement = "Deliver the requested feature.",
            OwnerName = "Showcase owner"
        };
        var step = new ProcessStepDefinition
        {
            Title = "Implement feature",
            InputContractSummary = "Approved scope and architecture inputs.",
            OutputContractSummary = "A buildable implementation.",
            EvidenceContractSummary = "Build and change evidence.",
            Notes = "Call workspace_pwsh_run_script first, then call workspace_dotnet_build before writing summary evidence."
        };

        var workBrief = buildWorkBrief.Invoke(null, [definition, step, "Showcase Lead Engineer"]) as string;

        Assert.NotNull(workBrief);
        Assert.Contains("Instructions:", workBrief, StringComparison.Ordinal);
        Assert.Contains("workspace_pwsh_run_script", workBrief, StringComparison.Ordinal);
        Assert.Contains("workspace_dotnet_build", workBrief, StringComparison.Ordinal);
    }

    [Fact]
    public void MatchExpectedArtifactId_matches_brief_markdown_by_slug_when_kind_guess_differs()
    {
        var expectedArtifactId = Guid.NewGuid();
        var expectedArtifacts = new[]
        {
            new ProcessRunAutomationDispatchService.DispatchArtifactExpectation(
                expectedArtifactId,
                ProcessArtifactKind.Brief,
                "Scope boundary packet",
                true,
                ProcessArtifactTrustRequirement.ReviewRequired,
                ProcessSensitivityLevel.Internal,
                "Must capture the clarified release boundary.")
        };
        var artifact = new ExecutionArtifactRecord(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "generated-output",
            string.Empty,
            "artifacts/scopes/organization/demo/showcases/blazor-ssr-calculator/evidence/process/feature-intake/scope-boundary-packet.md",
            "text/markdown",
            "workspace",
            "Durable scope packet",
            DateTimeOffset.UtcNow);

        var matchedExpectationId = ProcessRunAutomationDispatchService.MatchExpectedArtifactId(expectedArtifacts, artifact);

        Assert.Equal(expectedArtifactId, matchedExpectationId);
    }

    [Fact]
    public void MatchExpectedArtifactId_does_not_bind_unrelated_artifact_when_only_expectation_is_present()
    {
        var expectedArtifacts = new[]
        {
            new ProcessRunAutomationDispatchService.DispatchArtifactExpectation(
                Guid.NewGuid(),
                ProcessArtifactKind.Brief,
                "Scope boundary packet",
                true,
                ProcessArtifactTrustRequirement.ReviewRequired,
                ProcessSensitivityLevel.Internal,
                "Must capture the clarified release boundary.")
        };
        var artifact = new ExecutionArtifactRecord(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "tool-log",
            string.Empty,
            "artifacts/scopes/organization/demo/showcases/blazor-ssr-calculator/logs/stdout.log",
            "text/plain",
            "workspace",
            "Command output",
            DateTimeOffset.UtcNow);

        var matchedExpectationId = ProcessRunAutomationDispatchService.MatchExpectedArtifactId(expectedArtifacts, artifact);

        Assert.Null(matchedExpectationId);
    }

    [Fact]
    public void MatchExpectedArtifactId_ignores_playwright_scratch_artifact_even_when_name_matches()
    {
        var expectedArtifactId = Guid.NewGuid();
        var expectedArtifacts = new[]
        {
            new ProcessRunAutomationDispatchService.DispatchArtifactExpectation(
                expectedArtifactId,
                ProcessArtifactKind.Evidence,
                "Calculator proof",
                true,
                ProcessArtifactTrustRequirement.ReviewRequired,
                ProcessSensitivityLevel.Internal,
                "Imported browser screenshot is required.")
        };
        var artifact = new ExecutionArtifactRecord(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "generated-output",
            string.Empty,
            "showcases/blazor-ssr-calculator/.playwright-mcp/qa-validation/calculator-proof.png",
            "image/png",
            "workspace",
            "calculator-proof.png",
            DateTimeOffset.UtcNow);

        var matchedExpectationId = ProcessRunAutomationDispatchService.MatchExpectedArtifactId(expectedArtifacts, artifact);

        Assert.Null(matchedExpectationId);
    }

    [Fact]
    public void MatchExpectedArtifactId_matches_imported_ui_screenshot_by_slug()
    {
        var expectedArtifactId = Guid.NewGuid();
        var expectedArtifacts = new[]
        {
            new ProcessRunAutomationDispatchService.DispatchArtifactExpectation(
                expectedArtifactId,
                ProcessArtifactKind.Evidence,
                "Calculator proof",
                true,
                ProcessArtifactTrustRequirement.ReviewRequired,
                ProcessSensitivityLevel.Internal,
                "Imported browser screenshot is required.")
        };
        var artifact = new ExecutionArtifactRecord(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "generated-output",
            string.Empty,
            "artifacts/scopes/organization/demo/showcases/blazor-ssr-calculator/evidence/ui/qa-validation/calculator-proof.png",
            "image/png",
            "workspace",
            "calculator-proof.png",
            DateTimeOffset.UtcNow);

        var matchedExpectationId = ProcessRunAutomationDispatchService.MatchExpectedArtifactId(expectedArtifacts, artifact);

        Assert.Equal(expectedArtifactId, matchedExpectationId);
    }

    [Fact]
    public void MatchExpectedArtifactId_requires_exact_path_when_validation_summary_declares_one()
    {
        var expectedArtifacts = new[]
        {
            new ProcessRunAutomationDispatchService.DispatchArtifactExpectation(
                Guid.NewGuid(),
                ProcessArtifactKind.Deliverable,
                "Implementation change set",
                true,
                ProcessArtifactTrustRequirement.ReviewRequired,
                ProcessSensitivityLevel.Internal,
                "Create this artifact at artifacts/showcases/blazor-ssr-calculator/evidence/process/implementation/implementation-change-set.md using workspace create/write file tools.")
        };
        var artifact = new ExecutionArtifactRecord(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "generated-output",
            string.Empty,
            "artifacts/showcases/blazor-ssr-calculator/evidence/process/implementation/implementation-change-set/implementation-change-set.md",
            "text/markdown",
            "workspace",
            "Implementation change set",
            DateTimeOffset.UtcNow);

        var matchedExpectationId = ProcessRunAutomationDispatchService.MatchExpectedArtifactId(expectedArtifacts, artifact);

        Assert.Null(matchedExpectationId);
    }

    [Fact]
    public void MatchExpectedArtifactId_matches_exact_path_even_when_title_differs()
    {
        var expectedArtifactId = Guid.NewGuid();
        var expectedArtifacts = new[]
        {
            new ProcessRunAutomationDispatchService.DispatchArtifactExpectation(
                expectedArtifactId,
                ProcessArtifactKind.Deliverable,
                "Calculator app project",
                true,
                ProcessArtifactTrustRequirement.ReviewRequired,
                ProcessSensitivityLevel.Internal,
                "Create this artifact at showcases/blazor-ssr-calculator/app/SimpleCalculatorApp/SimpleCalculatorApp.csproj using workspace create/write file tools.")
        };
        var artifact = new ExecutionArtifactRecord(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "generated-output",
            string.Empty,
            "showcases/blazor-ssr-calculator/app/SimpleCalculatorApp/SimpleCalculatorApp.csproj",
            "text/xml",
            "workspace",
            "SimpleCalculatorApp.csproj",
            DateTimeOffset.UtcNow);

        var matchedExpectationId = ProcessRunAutomationDispatchService.MatchExpectedArtifactId(expectedArtifacts, artifact);

        Assert.Equal(expectedArtifactId, matchedExpectationId);
    }

    [Fact]
    public void MatchExpectedArtifactId_matches_scoped_managed_path_when_validation_summary_uses_unscoped_root()
    {
        var expectedArtifactId = Guid.NewGuid();
        var expectedArtifacts = new[]
        {
            new ProcessRunAutomationDispatchService.DispatchArtifactExpectation(
                expectedArtifactId,
                ProcessArtifactKind.Evidence,
                "Calculator proof",
                true,
                ProcessArtifactTrustRequirement.ReviewRequired,
                ProcessSensitivityLevel.Internal,
                "The durable screenshot must exist at artifacts/showcases/blazor-ssr-calculator/evidence/ui/qa-validation/calculator-proof.png.")
        };
        var artifact = new ExecutionArtifactRecord(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "generated-output",
            string.Empty,
            "artifacts/scopes/organization/demo/showcases/blazor-ssr-calculator/evidence/ui/qa-validation/calculator-proof.png",
            "image/png",
            "workspace",
            "calculator-proof.png",
            DateTimeOffset.UtcNow);

        var matchedExpectationId = ProcessRunAutomationDispatchService.MatchExpectedArtifactId(expectedArtifacts, artifact);

        Assert.Equal(expectedArtifactId, matchedExpectationId);
    }

    [Fact]
    public void BuildExecutionPrompt_warns_against_unrelated_side_actions()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var candidateType = serviceType.GetNestedType("DispatchCandidate", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("DispatchCandidate type was not found.");
        var artifactExpectationType = serviceType.GetNestedType("DispatchArtifactExpectation", BindingFlags.NonPublic | BindingFlags.Public)
            ?? throw new InvalidOperationException("DispatchArtifactExpectation type was not found.");
        var artifactInputType = serviceType.GetNestedType("DispatchArtifactInput", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("DispatchArtifactInput type was not found.");
        var buildExecutionPrompt = serviceType.GetMethod("BuildExecutionPrompt", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildExecutionPrompt method was not found.");

        var expectedArtifacts = Array.CreateInstance(artifactExpectationType, 0);
        var artifactInputs = Array.CreateInstance(artifactInputType, 0);
        var candidate = Activator.CreateInstance(
            candidateType,
            new object?[]
            {
                new ProcessRun
                {
                    Name = "Showcase run",
                    TriggerReason = "Deliver the calculator showcase."
                },
                new ProcessDefinition
                {
                    Name = "Software delivery"
                },
                new ProcessStepRun
                {
                    Title = "Complete peer review and integration readiness",
                    CurrentExecutorName = "Showcase Lead Engineer"
                },
                new ProcessWorkBrief
                {
                    WorkBriefText = "Review the delivered change set and confirm whether it is ready for QA.",
                    HandoffSummary = "Implementation package and architecture decision record.",
                    ExpectedOutcome = "Peer-reviewed change set with explicit residual risk.",
                    EvidenceExpectationSummary = "Peer review note"
                },
                Guid.NewGuid(),
                expectedArtifacts,
                artifactInputs,
                new HashSet<string>(StringComparer.Ordinal)
            })
            ?? throw new InvalidOperationException("DispatchCandidate could not be constructed.");

        var prompt = buildExecutionPrompt.Invoke(null, [candidate]) as string;

        Assert.NotNull(prompt);
        Assert.Contains(
            "Do not execute helper scripts, app launches, browser proof, release rollout, or other side actions unless the current step contract or required artifacts explicitly call for them.",
            prompt,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveCompletionStatus_fails_when_required_step_tools_were_not_executed()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolveCompletionStatus = serviceType.GetMethod("ResolveCompletionStatus", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveCompletionStatus method was not found.");
        var buildCompletionReason = serviceType.GetMethod("BuildCompletionReason", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildCompletionReason method was not found.");

        var candidate = CreateDispatchCandidate(
            "Implement the calculator.\nInstructions: Call workspace_pwsh_run_script first and then call workspace_dotnet_build before you conclude.");
        var now = DateTimeOffset.UtcNow;
        var detail = new ExecutionRunDetail(
            new ExecutionRunRecord(
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                "Implementation run",
                "process-step",
                "step-1",
                "corr-1",
                "run-start",
                "process-automation-dispatch",
                "system",
                "{}",
                "Prompt",
                "Response",
                "OpenAI chat completions",
                "gpt-4o-mini",
                ExecutionState.Completed,
                RunOutcome.Succeeded,
                now,
                now,
                now,
                now,
                string.Empty,
                null,
                []),
            null,
            [],
            [])
        {
            ToolReceipts =
            [
                new ToolExecutionReceiptRecord(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    "workspace-file",
                    "workspace_write_file",
                    "MutatingWorkspace",
                    "NotRequired",
                    "Workspace-root-only file service.",
                    "showcases/blazor-ssr-calculator/app/SimpleCalculatorApp/Program.cs",
                    ".",
                    "Succeeded: Created file.",
                    now,
                    now)
            ]
        };

        var status = (ProcessStepRunStatus?)resolveCompletionStatus.Invoke(null, [candidate, detail]);
        var reason = buildCompletionReason.Invoke(null, [candidate, detail, "Implement feature"]) as string;

        Assert.Equal(ProcessStepRunStatus.Failed, status);
        Assert.NotNull(reason);
        Assert.Contains("workspace_pwsh_run_script", reason, StringComparison.Ordinal);
        Assert.Contains("workspace_dotnet_build", reason, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveCompletionStatus_allows_completion_when_required_step_tools_succeed()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolveCompletionStatus = serviceType.GetMethod("ResolveCompletionStatus", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveCompletionStatus method was not found.");

        var candidate = CreateDispatchCandidate(
            "Implement the calculator.\nInstructions: Call workspace_pwsh_run_script first and then call workspace_dotnet_build before you conclude.");
        var now = DateTimeOffset.UtcNow;
        var detail = new ExecutionRunDetail(
            new ExecutionRunRecord(
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                "Implementation run",
                "process-step",
                "step-1",
                "corr-1",
                "run-start",
                "process-automation-dispatch",
                "system",
                "{}",
                "Prompt",
                "Response",
                "OpenAI chat completions",
                "gpt-4o-mini",
                ExecutionState.Completed,
                RunOutcome.Succeeded,
                now,
                now,
                now,
                now,
                string.Empty,
                null,
                []),
            null,
            [],
            [])
        {
            ToolReceipts =
            [
                new ToolExecutionReceiptRecord(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    "workspace-process",
                    "workspace_pwsh_run_script",
                    "LocalExecution",
                    "Required",
                    "PolicyOnlyLocal",
                    "Apply-CalculatorShowcaseApp.ps1",
                    "showcases/blazor-ssr-calculator",
                    "Succeeded",
                    now,
                    now),
                new ToolExecutionReceiptRecord(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    "workspace-process",
                    "workspace_dotnet_build",
                    "LocalExecution",
                    "Required",
                    "PolicyOnlyLocal",
                    "SimpleCalculatorApp.csproj",
                    "showcases/blazor-ssr-calculator/app/SimpleCalculatorApp",
                    "Succeeded",
                    now,
                    now)
            ]
        };

        var status = (ProcessStepRunStatus?)resolveCompletionStatus.Invoke(null, [candidate, detail]);

        Assert.Equal(ProcessStepRunStatus.Completed, status);
    }

    [Fact]
    public void ResolveRequiredToolNames_ignores_negated_tool_references_and_keeps_affirmative_requirements()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolveRequiredToolNames = serviceType.GetMethod("ResolveRequiredToolNames", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveRequiredToolNames method was not found.");

        var candidate = CreateDispatchCandidate(
            "Implement the calculator.\nInstructions: Call workspace_pwsh_run_script first and then call workspace_dotnet_build before you conclude. Do not use workspace_append_file for canonical deliverables.");

        var requiredToolNames = resolveRequiredToolNames.Invoke(null, [candidate]) as IReadOnlyList<string>;

        Assert.NotNull(requiredToolNames);
        Assert.Contains("workspace_pwsh_run_script", requiredToolNames, StringComparer.Ordinal);
        Assert.Contains("workspace_dotnet_build", requiredToolNames, StringComparer.Ordinal);
        Assert.DoesNotContain("workspace_append_file", requiredToolNames, StringComparer.Ordinal);
    }

    [Fact]
    public void ResolveCompletionStatus_ignores_negated_tool_references_when_required_step_tools_succeed()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolveCompletionStatus = serviceType.GetMethod("ResolveCompletionStatus", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveCompletionStatus method was not found.");

        var candidate = CreateDispatchCandidate(
            "Implement the calculator.\nInstructions: Call workspace_pwsh_run_script first and then call workspace_dotnet_build before you conclude. Do not use workspace_append_file for canonical deliverables.");
        var now = DateTimeOffset.UtcNow;
        var detail = new ExecutionRunDetail(
            new ExecutionRunRecord(
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                "Implementation run",
                "process-step",
                "step-1",
                "corr-1",
                "run-start",
                "process-automation-dispatch",
                "system",
                "{}",
                "Prompt",
                "Response",
                "OpenAI chat completions",
                "gpt-4o-mini",
                ExecutionState.Completed,
                RunOutcome.Succeeded,
                now,
                now,
                now,
                now,
                string.Empty,
                string.Empty,
                []),
            null,
            [],
            [])
        {
            ToolReceipts =
            [
                new ToolExecutionReceiptRecord(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    "workspace-process",
                    "workspace_pwsh_run_script",
                    "LocalExecution",
                    "Required",
                    "PolicyOnlyLocal",
                    "Apply-CalculatorShowcaseApp.ps1",
                    "showcases/blazor-ssr-calculator",
                    "Succeeded",
                    now,
                    now),
                new ToolExecutionReceiptRecord(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    "workspace-process",
                    "workspace_dotnet_build",
                    "LocalExecution",
                    "Required",
                    "PolicyOnlyLocal",
                    "SimpleCalculatorApp.csproj",
                    "showcases/blazor-ssr-calculator/app/SimpleCalculatorApp",
                    "Succeeded",
                    now,
                    now)
            ]
        };

        var status = (ProcessStepRunStatus?)resolveCompletionStatus.Invoke(null, [candidate, detail]);

        Assert.Equal(ProcessStepRunStatus.Completed, status);
    }

    [Fact]
    public void ResolveCompletionStatus_allows_completion_when_required_browser_tools_are_confirmed_by_session_state()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolveCompletionStatus = serviceType.GetMethod("ResolveCompletionStatus", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveCompletionStatus method was not found.");

        var candidate = CreateDispatchCandidate(
            "Validate the UI.\nInstructions: Call browser_take_screenshot, browser_snapshot, and browser_console_messages before you conclude.");
        var now = DateTimeOffset.UtcNow;
        var detail = new ExecutionRunDetail(
            new ExecutionRunRecord(
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                "QA run",
                "process-step",
                "step-1",
                "corr-1",
                "run-start",
                "process-automation-dispatch",
                "system",
                "{}",
                "Prompt",
                "Response",
                "OpenAI chat completions",
                "gpt-4o-mini",
                ExecutionState.Completed,
                RunOutcome.Succeeded,
                now,
                now,
                now,
                now,
                string.Empty,
                BuildSerializedSessionState(
                    ("browser_take_screenshot", CreateProviderNativeTextResult("Screenshot saved.")),
                    ("browser_snapshot", CreateProviderNativeTextResult("Snapshot saved.")),
                    ("browser_console_messages", CreateProviderNativeTextResult("Console log saved."))),
                []),
            null,
            [],
            [])
        {
            ToolReceipts =
            [
                new ToolExecutionReceiptRecord(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    "workspace-process",
                    "workspace_pwsh_run_script",
                    "LocalExecution",
                    "Required",
                    "PolicyOnlyLocal",
                    "Import-PlaywrightEvidence.ps1",
                    "showcases/blazor-ssr-calculator",
                    "Succeeded",
                    now,
                    now)
            ]
        };

        var status = (ProcessStepRunStatus?)resolveCompletionStatus.Invoke(null, [candidate, detail]);

        Assert.Equal(ProcessStepRunStatus.Completed, status);
    }

    [Fact]
    public void ResolveSuccessfulSessionToolOutputFiles_returns_browser_filenames_for_successful_calls()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolveSuccessfulSessionToolOutputFiles = serviceType.GetMethod("ResolveSuccessfulSessionToolOutputFiles", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveSuccessfulSessionToolOutputFiles method was not found.");

        var serializedSessionState = BuildSerializedSessionState(
            ("browser_take_screenshot", new Dictionary<string, object?> { ["filename"] = "execute-release-rollout/calculator-proof.png" }, CreateProviderNativeTextResult("Screenshot saved.")),
            ("browser_snapshot", new Dictionary<string, object?> { ["filename"] = "execute-release-rollout/calculator-page.yml" }, CreateProviderNativeTextResult("Snapshot saved.")),
            ("browser_console_messages", new Dictionary<string, object?> { ["filename"] = "execute-release-rollout/calculator-console.log" }, CreateProviderNativeTextResult("Console log saved.")));

        var outputFilesByToolName = resolveSuccessfulSessionToolOutputFiles.Invoke(null, [serializedSessionState]) as IReadOnlyDictionary<string, IReadOnlyList<string>>;

        Assert.NotNull(outputFilesByToolName);
        Assert.Contains(outputFilesByToolName.Keys, item => string.Equals(item, "browser_take_screenshot", StringComparison.Ordinal));
        Assert.Contains(outputFilesByToolName.Keys, item => string.Equals(item, "browser_snapshot", StringComparison.Ordinal));
        Assert.Contains(outputFilesByToolName.Keys, item => string.Equals(item, "browser_console_messages", StringComparison.Ordinal));
        Assert.Contains("execute-release-rollout/calculator-proof.png", outputFilesByToolName["browser_take_screenshot"], StringComparer.OrdinalIgnoreCase);
        Assert.Contains("execute-release-rollout/calculator-page.yml", outputFilesByToolName["browser_snapshot"], StringComparer.OrdinalIgnoreCase);
        Assert.Contains("execute-release-rollout/calculator-console.log", outputFilesByToolName["browser_console_messages"], StringComparer.OrdinalIgnoreCase);
    }

    private static object CreateDispatchCandidate(string workBriefText)
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var candidateType = serviceType.GetNestedType("DispatchCandidate", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("DispatchCandidate type was not found.");
        var artifactExpectationType = serviceType.GetNestedType("DispatchArtifactExpectation", BindingFlags.NonPublic | BindingFlags.Public)
            ?? throw new InvalidOperationException("DispatchArtifactExpectation type was not found.");
        var artifactInputType = serviceType.GetNestedType("DispatchArtifactInput", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("DispatchArtifactInput type was not found.");
        var expectedArtifacts = Array.CreateInstance(artifactExpectationType, 0);
        var artifactInputs = Array.CreateInstance(artifactInputType, 0);

        return Activator.CreateInstance(
                   candidateType,
                   new object?[]
                   {
                       new ProcessRun
                       {
                           Name = "Showcase run",
                           TriggerReason = "Deliver the calculator showcase."
                       },
                       new ProcessDefinition
                       {
                           Name = "Software delivery"
                       },
                       new ProcessStepRun
                       {
                           Title = "Implement feature, tests, and migration notes",
                           CurrentExecutorName = "Showcase Lead Engineer"
                       },
                       new ProcessWorkBrief
                       {
                           WorkBriefText = workBriefText,
                           HandoffSummary = "Architecture decision record.",
                           ExpectedOutcome = "Buildable calculator implementation.",
                           EvidenceExpectationSummary = "Implementation change set"
                       },
                       Guid.NewGuid(),
                       expectedArtifacts,
                       artifactInputs,
                       new HashSet<string>(StringComparer.Ordinal)
                   })
                ?? throw new InvalidOperationException("DispatchCandidate could not be constructed.");
    }

    private static string BuildSerializedSessionState(params (string ToolName, object Result)[] toolCalls)
    {
        return BuildSerializedSessionState(toolCalls.Select(toolCall =>
            (toolCall.ToolName, (IReadOnlyDictionary<string, object?>)new Dictionary<string, object?>(), toolCall.Result)).ToArray());
    }

    private static string BuildSerializedSessionState(params (string ToolName, IReadOnlyDictionary<string, object?> Arguments, object Result)[] toolCalls)
    {
        var callContents = toolCalls
            .Select((toolCall, index) => new
            {
                Content = new Dictionary<string, object?>
                {
                    ["$type"] = "functionCall",
                    ["callId"] = $"call-{index + 1}",
                    ["name"] = toolCall.ToolName,
                    ["arguments"] = toolCall.Arguments
                }
            }.Content)
            .ToArray();
        var resultContents = toolCalls
            .Select((toolCall, index) => new
            {
                Content = new Dictionary<string, object?>
                {
                    ["$type"] = "functionResult",
                    ["callId"] = $"call-{index + 1}",
                    ["result"] = toolCall.Result
                }
            }.Content)
            .ToArray();

        return JsonSerializer.Serialize(
            new
            {
                stateBag = new
                {
                    InMemoryChatHistoryProvider = new
                    {
                        messages = new object[]
                        {
                            new
                            {
                                role = "assistant",
                                contents = callContents
                            },
                            new
                            {
                                role = "tool",
                                contents = resultContents
                            }
                        }
                    }
                }
            });
    }

    private static object CreateProviderNativeTextResult(string text)
    {
        return new Dictionary<string, string>
        {
            ["$type"] = "text",
            ["text"] = text
        };
    }
}
