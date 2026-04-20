using System.Diagnostics;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.ScenarioSeeder;

internal sealed partial class AgentShowcaseCalculatorSeeder
{
    private static readonly string[] UiValidationStepKeys =
    [
        "qa-validation",
        "execute-release-rollout"
    ];

    private const string PhaseTitle = "Agent showcase execution";
    private const string DeliveryBlockTitle = "Blazor SSR calculator delivery";
    private const string FeatureTitle = "Simple calculator feature";

    private async Task<ShowcaseGraph> EnsureProjectStructureAsync(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var surface = await projectWorkbenchService.GetStructureAsync(projectId, cancellationToken);
        var phaseNode = FindShowcaseNode(surface, ProjectObjectType.Phase, PhaseTitle)
            ?? await projectWorkbenchService.CreateObjectAsync(
                projectId,
                new ProjectObjectCreateRequest(
                    ProjectObjectType.Phase,
                    PhaseTitle,
                    "Phase / agent integration verification",
                    $"{Marker}{Environment.NewLine}Owns the end-to-end validation of the calculator showcase.",
                    ParentNodeKey: null,
                    ObjectSubtype: "showcase-phase"),
                cancellationToken);
        var deliveryBlockNode = FindShowcaseNode(surface, ProjectObjectType.ProjectBlock, DeliveryBlockTitle)
            ?? await projectWorkbenchService.CreateObjectAsync(
                projectId,
                new ProjectObjectCreateRequest(
                    ProjectObjectType.ProjectBlock,
                    DeliveryBlockTitle,
                    "Delivery block / template-driven execution",
                    $"{Marker}{Environment.NewLine}Runs the imported software-delivery template with AI role assignments.",
                    phaseNode.Id,
                    ObjectSubtype: "showcase-delivery-block"),
                cancellationToken);
        var featureNode = FindShowcaseNode(surface, ProjectObjectType.WorkItem, FeatureTitle)
            ?? await projectWorkbenchService.CreateObjectAsync(
                projectId,
                new ProjectObjectCreateRequest(
                    ProjectObjectType.WorkItem,
                    FeatureTitle,
                    "Feature / Blazor SSR calculator",
                    $"{Marker}{Environment.NewLine}Deliver the calculator app at {AppProjectRelativePath}.",
                    deliveryBlockNode.Id,
                    ObjectSubtype: "showcase-feature"),
                cancellationToken);

        await UpdateGraphProgressAsync(
            projectId,
            new ShowcaseGraph(phaseNode.Id, deliveryBlockNode.Id, featureNode.Id),
            "Planned",
            0,
            cancellationToken);

        return new ShowcaseGraph(phaseNode.Id, deliveryBlockNode.Id, featureNode.Id);
    }

    private async Task EnsureProjectAssignmentsAsync(
        Guid projectId,
        IReadOnlyDictionary<string, ShowcaseAgentBinding> bindingsByRoleKey,
        CancellationToken cancellationToken)
    {
        var existingAssignments = await projectPartyIntegrationBridge.ListAssignmentsDetailedAsync(projectId, cancellationToken);
        foreach (var binding in bindingsByRoleKey.Values)
        {
            var exists = existingAssignments.Any(item =>
                item.PartyId == binding.PartyId &&
                item.Role == ProjectPartyAssignmentRole.AiAgent);
            if (exists)
            {
                continue;
            }

            EnsureSuccess(await projectPartyIntegrationBridge.SaveAssignmentAsync(
                new ProjectPartyAssignmentUpsertRequest
                {
                    ProjectId = projectId,
                    PartyId = binding.PartyId,
                    Role = ProjectPartyAssignmentRole.AiAgent,
                    IsPrimary = false,
                    AllocationPercent = 100m,
                    Source = "agent-showcase-scenario",
                    Notes = $"Showcase AI assignment for role '{binding.RoleKey}'."
                },
                cancellationToken));
        }
    }

    private async Task<Guid> EnsureProcessDefinitionAsync(
        Guid projectId,
        ShowcaseWorkspacePlan workspacePlan,
        CancellationToken cancellationToken)
    {
        var existingDefinition = (await processesService.ListDefinitionsAsync(projectId, cancellationToken))
            .FirstOrDefault(item => string.Equals(item.Name, DefinitionName, StringComparison.Ordinal));
        var definitionId = existingDefinition?.Id
            ?? EnsureSuccess(await processesService.ImportAsync(
                projectionService.GetProjectedEnvelope(ProcessTemplateKey, projectId, DefinitionName),
                cancellationToken));

        var editor = await processesService.GetEditorAsync(definitionId, projectId, cancellationToken);
        TailorDefinition(editor, workspacePlan);
        definitionId = EnsureSuccess(await processesService.SaveAsync(editor, cancellationToken));
        EnsureSuccess(await processesService.PublishAsync(definitionId, cancellationToken));
        return definitionId;
    }

    private void TailorDefinition(
        ProcessDefinitionEditorModel editor,
        ShowcaseWorkspacePlan workspacePlan)
    {
        editor.Name = DefinitionName;
        editor.Summary = "Template-driven AI showcase for delivering a Blazor SSR calculator from project structure through release evidence.";
        editor.ValueStatement = "Proves that shared organization agents can execute a delivery process without creating a second source of truth.";
        editor.CustomerName = "CanDoItAll showcase validation";
        editor.OwnerName = "Agent integration wave";
        editor.InterfaceContractSummary = $"The delivered application must exist at {AppProjectRelativePath} and run at {AppUrl}.";
        editor.GovernanceNotes = $"Marker: {Marker}. All roles are fulfilled by AgentFramework agents projected through CRM-HR.";
        editor.ChangeSummary = "Tailored from the shared software-delivery template for the Blazor SSR calculator showcase.";
        editor.GovernancePolicySummary = "No step may claim completion without durable evidence in the showcase workspace.";
        editor.ConstitutionRuleSummary = "Agent roles remain explicit and must hand off through process artifacts, not implicit chat state.";
        editor.OperatingModeSummary = "Governed live execution with auto-dispatched AI workers and screenshot-backed UI validation.";
        editor.SimulationReadinessSummary = "The scenario is designed to expose process-launch, artifact-handoff, and execution-monitoring gaps.";
        editor.Criticality = ProcessCriticality.High;
        editor.AutonomyLevel = ProcessAutonomyLevel.Guarded;

        foreach (var role in editor.Roles)
        {
            role.PreferredExecutorKind = "ai-agent";
            role.PreferredProjectAssignmentRole = ProjectPartyAssignmentRole.AiAgent;
            role.StaffingIntent = MergeSingleLine(
                role.StaffingIntent,
                "This showcase role is fulfilled by a bound AgentFramework AI worker from the shared organization directory.");
            role.SnapshotSummary = MergeSingleLine(
                role.SnapshotSummary,
                "Scenario tailored for the calculator showcase.");
        }

        foreach (var step in editor.Steps)
        {
            step.Notes = MergeParagraphs(step.Notes, BuildStepNote(step.Key, workspacePlan));
            step.InputContractSummary = MergeSingleLine(step.InputContractSummary, $"Primary brief: {BriefRelativePath}.");
            step.OutputContractSummary = MergeSingleLine(step.OutputContractSummary, $"Showcase app path: {AppProjectRelativePath}.");
            if (string.Equals(step.Key, "implementation", StringComparison.OrdinalIgnoreCase))
            {
                EnsureImplementationArtifacts(step);
            }

            if (IsUiValidationStep(step.Key))
            {
                EnsureUiValidationArtifacts(step);
                step.EvidenceContractSummary = BuildUiValidationEvidenceContractSummary(step);

                foreach (var artifact in step.ArtifactExpectations)
                {
                    artifact.TrustRequirement = ProcessArtifactTrustRequirement.ReviewRequired;
                    artifact.ValidationRequirementSummary = BuildUiValidationArtifactSummary(step.Key, artifact);
                }
            }
            else
            {
                step.EvidenceContractSummary = MergeSingleLine(
                    step.EvidenceContractSummary,
                    $"Write required process artifacts under {ProcessEvidenceRelativePath} and use {BuildScopedUiEvidenceRelativePath()} for screenshots, launch logs, and browser proof.");
                step.EvidenceContractSummary = MergeSingleLine(
                    step.EvidenceContractSummary,
                    BuildStepArtifactContractSummary(step));

                foreach (var artifact in step.ArtifactExpectations)
                {
                    var artifactRelativePath = BuildArtifactRelativePath(step.Key, artifact);
                    artifact.TrustRequirement = ProcessArtifactTrustRequirement.ReviewRequired;
                    artifact.ValidationRequirementSummary = artifactRelativePath.StartsWith("artifacts/", StringComparison.OrdinalIgnoreCase)
                        ? MergeSingleLine(
                            artifact.ValidationRequirementSummary,
                            $"Evidence must remain inside the managed showcase artifact roots. Create this artifact at {artifactRelativePath} using workspace create/write file tools.")
                        : MergeSingleLine(
                            artifact.ValidationRequirementSummary,
                            $"Create this artifact at {artifactRelativePath}. This is a real workspace deliverable, not a summary-only note.");
                    artifact.ValidationRequirementSummary = MergeSingleLine(
                        artifact.ValidationRequirementSummary,
                        BuildImplementationArtifactValidationSummary(step.Key, artifact.Title, artifactRelativePath));
                    artifact.ValidationRequirementSummary = MergeSingleLine(
                        artifact.ValidationRequirementSummary,
                        "For this governed AI showcase, the assigned agent must leave explicit review-required evidence; do not rely on an implicit human fallback.");
                }
            }
        }

        EnsureShowcaseDependencies(editor);
        EnsureShowcaseArtifactInputs(editor);

        var securityReviewStep = editor.Steps.FirstOrDefault(item =>
            string.Equals(item.Key, "security-review", StringComparison.OrdinalIgnoreCase));
        if (securityReviewStep is not null)
        {
            securityReviewStep.StepKind = ProcessStepKind.Review;
            securityReviewStep.RequiresApproval = false;
            securityReviewStep.RequiresDecisionRecord = true;
            securityReviewStep.DecisionRightsSummary = MergeSingleLine(
                securityReviewStep.DecisionRightsSummary,
                "This step produces the security assessment; final go/no-go authority stays on release approval.");

            foreach (var assignment in securityReviewStep.RoleAssignments)
            {
                if (assignment.ResponsibilityKind == ProcessResponsibilityKind.Approver)
                {
                    assignment.ResponsibilityKind = ProcessResponsibilityKind.Responsible;
                }
            }
        }

        var releaseApprovalStep = editor.Steps.FirstOrDefault(item =>
            string.Equals(item.Key, "release-approval", StringComparison.OrdinalIgnoreCase));
        if (releaseApprovalStep is not null)
        {
            releaseApprovalStep.StepKind = ProcessStepKind.Approval;
            releaseApprovalStep.RequiresApproval = true;
        }
    }

    private string BuildStepNote(string stepKey, ShowcaseWorkspacePlan workspacePlan)
    {
        return stepKey switch
        {
            "feature-intake" => $"Read {BriefRelativePath}, lock scope to a simple SSR calculator, and state explicit acceptance criteria. Use the exact required artifact path listed in the artifact contract. Do not run {LaunchScriptRelativePath}, {ImportPlaywrightEvidenceScriptRelativePath}, or browser tools in this step.",
            "architecture-review" => $"Keep the implementation intentionally small, reuse standard Blazor SSR patterns, and avoid speculative abstractions. Use the exact required artifact path listed in the artifact contract. Do not run {LaunchScriptRelativePath}, {ImportPlaywrightEvidenceScriptRelativePath}, or browser tools in this step.",
            "implementation" => $"Create the app under {AppProjectRelativePath}. Call workspace_pwsh_run_script for {ApplyAppScriptRelativePath} first as the mandatory deterministic baseline for this step and pass outputPaths [{BuildImplementationOutputPathArgument()}] so the canonical app files are recorded as durable deliverables. Then call workspace_dotnet_build for {AppProjectRelativePath}, inspect {AppProjectRelativePath}, Program.cs, and Home.razor, and document the real files you changed. After the repair script succeeds, treat {AppProjectRelativePath}, {AppProgramRelativePath}, and {AppHomeRelativePath} as canonical deliverables and do not overwrite them with workspace_write_file, workspace_append_file, ad-hoc templates, or a legacy Blazor Server rewrite. Only the required markdown artifacts under {ProcessEvidenceRelativePath}/implementation may be written directly in this step unless the repair script itself left a concrete defect that you explicitly fix. Do not write implementation evidence until the repair script and build both succeed and the app project exists at {AppProjectRelativePath}. Do not run {LaunchScriptRelativePath} or {ImportPlaywrightEvidenceScriptRelativePath} in this step; browser proof belongs to QA and rollout only. The markdown evidence is not the deliverable; it must describe the real app, files, and validation you produced. Use the exact required artifact paths listed in the artifact contract.",
            "peer-review" => $"Review for maintainability, correctness, and unnecessary complexity. Reject unclear or weak evidence. Do not run {LaunchScriptRelativePath}, {ImportPlaywrightEvidenceScriptRelativePath}, or browser tools in this step; peer review consumes durable evidence instead of recreating it.",
            "qa-validation" => $"Launch the app with {LaunchScriptRelativePath}, validate it at {AppUrl}, use Playwright without absolute managed filenames so evidence lands under {PlaywrightScratchRelativePath}, call browser_take_screenshot with the relative filename '{stepKey}/calculator-proof.png', call browser_snapshot with the relative filename '{stepKey}/calculator-page.yml', call browser_console_messages with the relative filename '{stepKey}/calculator-console.log', then run {ImportPlaywrightEvidenceScriptRelativePath} with step key '{stepKey}' through workspace_pwsh_run_script and pass outputPaths [{BuildUiImportOutputPathArgument(stepKey)}] so the imported screenshot, snapshot, console log, and summary are registered as durable execution artifacts under {BuildUiEvidenceStepFullPath(stepKey, workspacePlan)}. Write the required evidence note at the exact artifact path listed in the contract, and reference the imported files from the managed UI evidence root. If the app does not launch, browser proof fails, or the import script does not produce all required UI evidence files, stop and record the failure instead of a success note.",
            "security-review" => $"Confirm predictable error handling, input validation, and absence of secret or environment leakage. Use the exact required artifact path listed in the artifact contract. Do not run {LaunchScriptRelativePath}, {ImportPlaywrightEvidenceScriptRelativePath}, or browser tools in this step unless the process is explicitly reopened for fresh validation.",
            "release-approval" => $"Approve only when scope, implementation, QA, and security evidence are all explicit and durable. Use the exact required artifact path listed in the artifact contract. Do not rerun {LaunchScriptRelativePath}, {ImportPlaywrightEvidenceScriptRelativePath}, or browser tools in this step; approve or reject based on the recorded evidence set.",
            "execute-release-rollout" => $"Run the release smoke path against {AppUrl}, confirm the final route is usable, use Playwright without absolute managed filenames so evidence lands under {PlaywrightScratchRelativePath}, call browser_take_screenshot with the relative filename '{stepKey}/calculator-proof.png', call browser_snapshot with the relative filename '{stepKey}/calculator-page.yml', call browser_console_messages with the relative filename '{stepKey}/calculator-console.log', then run {ImportPlaywrightEvidenceScriptRelativePath} with step key '{stepKey}' through workspace_pwsh_run_script and pass outputPaths [{BuildUiImportOutputPathArgument(stepKey)}] so the imported screenshot, snapshot, console log, and summary are registered as durable execution artifacts under {BuildUiEvidenceStepFullPath(stepKey, workspacePlan)}. Write the required artifact at the exact contract path, and make the rollout note reflect the real runtime result, not a planned path.",
            "post-release-learning" => $"Capture what the current agent/process implementation still misses so the next integration wave has concrete follow-up work. Do not rerun {LaunchScriptRelativePath}, {ImportPlaywrightEvidenceScriptRelativePath}, or browser tools in this step unless an earlier validation step was explicitly reopened.",
            _ => $"Operate within the showcase root {ShowcaseRootRelativePath} and keep evidence explicit."
        };
    }

    private static void EnsureShowcaseArtifactInputs(ProcessDefinitionEditorModel editor)
    {
        var stepsByKey = editor.Steps.ToDictionary(item => item.Key, StringComparer.OrdinalIgnoreCase);
        SetArtifactInputs(
            stepsByKey,
            "architecture-review",
            ("feature-intake", "Scope boundary packet"));
        SetArtifactInputs(
            stepsByKey,
            "implementation",
            ("feature-intake", "Scope boundary packet"),
            ("architecture-review", "Architecture decision record"));
        SetArtifactInputs(
            stepsByKey,
            "peer-review",
            ("implementation", "Implementation change set"),
            ("architecture-review", "Architecture decision record"));
        SetArtifactInputs(
            stepsByKey,
            "qa-validation",
            ("implementation", "Implementation change set"),
            ("peer-review", "Peer review note"));
        SetArtifactInputs(
            stepsByKey,
            "security-review",
            ("implementation", "Implementation change set"),
            ("peer-review", "Peer review note"));
    }

    private static void SetArtifactInputs(
        IReadOnlyDictionary<string, ProcessStepEditorModel> stepsByKey,
        string targetStepKey,
        params (string SourceStepKey, string ArtifactTitle)[] inputs)
    {
        if (!stepsByKey.TryGetValue(targetStepKey, out var targetStep))
        {
            return;
        }

        targetStep.ArtifactInputs = inputs
            .Select(input => ResolveArtifactInput(stepsByKey, input.SourceStepKey, input.ArtifactTitle))
            .Where(item => item is not null)
            .Select(item => item!)
            .ToList();
    }

    private static ProcessStepArtifactInputEditorModel? ResolveArtifactInput(
        IReadOnlyDictionary<string, ProcessStepEditorModel> stepsByKey,
        string sourceStepKey,
        string artifactTitle)
    {
        if (!stepsByKey.TryGetValue(sourceStepKey, out var sourceStep))
        {
            return null;
        }

        var artifactExpectation = sourceStep.ArtifactExpectations.FirstOrDefault(item =>
            string.Equals(item.Title, artifactTitle, StringComparison.OrdinalIgnoreCase));
        if (artifactExpectation?.Id is null)
        {
            return null;
        }

        return new ProcessStepArtifactInputEditorModel
        {
            ArtifactExpectationId = artifactExpectation.Id
        };
    }

    private static string BuildStepArtifactContractSummary(ProcessStepEditorModel step)
    {
        if (step.ArtifactExpectations.Count == 0)
        {
            return "No explicit step artifact files are required.";
        }

        return string.Join(
            " ",
            step.ArtifactExpectations.Select(artifact =>
                $"Required artifact '{artifact.Title}' must be written at {BuildArtifactRelativePath(step.Key, artifact)}."));
    }

    private static string BuildArtifactRelativePath(
        string stepKey,
        ProcessArtifactExpectationEditorModel artifact)
    {
        if (TryResolveImplementationArtifactRelativePath(stepKey, artifact.Title, out var implementationRelativePath))
        {
            return implementationRelativePath;
        }

        if (TryResolveUiValidationArtifactRelativePath(stepKey, artifact.Title, out var relativePath))
        {
            return relativePath;
        }

        return $"{ProcessEvidenceRelativePath}/{stepKey}/{FileSafeSlugBuilder.Build(artifact.Title)}.md";
    }

    private static bool TryResolveImplementationArtifactRelativePath(
        string stepKey,
        string artifactTitle,
        out string relativePath)
    {
        if (!string.Equals(stepKey, "implementation", StringComparison.OrdinalIgnoreCase))
        {
            relativePath = string.Empty;
            return false;
        }

        if (string.Equals(artifactTitle, "Calculator app project", StringComparison.OrdinalIgnoreCase))
        {
            relativePath = AppProjectRelativePath;
            return true;
        }

        if (string.Equals(artifactTitle, "Calculator host program", StringComparison.OrdinalIgnoreCase))
        {
            relativePath = AppProgramRelativePath;
            return true;
        }

        if (string.Equals(artifactTitle, "Calculator home page", StringComparison.OrdinalIgnoreCase))
        {
            relativePath = AppHomeRelativePath;
            return true;
        }

        relativePath = string.Empty;
        return false;
    }

    private static string BuildImplementationArtifactValidationSummary(
        string stepKey,
        string artifactTitle,
        string artifactRelativePath)
    {
        if (!string.Equals(stepKey, "implementation", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        if (!TryResolveImplementationArtifactRelativePath(stepKey, artifactTitle, out _))
        {
            return string.Empty;
        }

        return string.Join(
            " ",
            [
                $"This deliverable must remain the canonical output of {ApplyAppScriptRelativePath} after a successful dotnet build and must exist at {artifactRelativePath}.",
                "Do not replace it with a legacy Blazor Server rewrite.",
                "net6.0, explicit Microsoft.AspNetCore.Components.* package references, Startup.cs, UseStartup<Startup>(), or button-only event-handler calculator logic fail this requirement."
            ]);
    }

    private void EnsureUiValidationArtifacts(ProcessStepEditorModel step)
    {
        EnsureRequiredArtifactExpectation(
            step,
            "Calculator proof",
            ProcessArtifactKind.Evidence,
            365,
            "Reusable for release review, defect triage, and later proof that the validated UI really rendered.");
        EnsureRequiredArtifactExpectation(
            step,
            "Calculator page",
            ProcessArtifactKind.Evidence,
            365,
            "Reusable for later review of the exact SSR page state captured during governed validation.");
        EnsureRequiredArtifactExpectation(
            step,
            "Calculator console",
            ProcessArtifactKind.Transcript,
            365,
            "Reusable for debugging browser-visible issues and validating that the UI proof was collected from a real run.");
    }

    private void EnsureImplementationArtifacts(ProcessStepEditorModel step)
    {
        EnsureRequiredArtifactExpectation(
            step,
            "Calculator app project",
            ProcessArtifactKind.Deliverable,
            365,
            "Proves the canonical showcase app exists at the expected project path for downstream execution.");
        EnsureRequiredArtifactExpectation(
            step,
            "Calculator host program",
            ProcessArtifactKind.Deliverable,
            365,
            "Proves the SSR host configuration that peer review, QA, and release validation depend on.");
        EnsureRequiredArtifactExpectation(
            step,
            "Calculator home page",
            ProcessArtifactKind.Deliverable,
            365,
            "Proves the calculator UI source that QA and review must validate.");
    }

    private static void EnsureRequiredArtifactExpectation(
        ProcessStepEditorModel step,
        string title,
        ProcessArtifactKind artifactKind,
        int retentionDays,
        string allowedFutureUsageSummary)
    {
        var artifact = step.ArtifactExpectations.FirstOrDefault(item =>
            string.Equals(item.Title, title, StringComparison.OrdinalIgnoreCase));
        if (artifact is null)
        {
            artifact = new ProcessArtifactExpectationEditorModel
            {
                Id = Guid.NewGuid()
            };
            step.ArtifactExpectations.Add(artifact);
        }

        artifact.Title = title;
        artifact.ArtifactKind = artifactKind;
        artifact.IsRequired = true;
        artifact.TrustRequirement = ProcessArtifactTrustRequirement.ReviewRequired;
        artifact.SensitivityLevel = ProcessSensitivityLevel.Internal;
        artifact.RetentionDays = retentionDays;
        artifact.AllowedFutureUsageSummary = allowedFutureUsageSummary;
    }

    private string BuildUiValidationEvidenceContractSummary(ProcessStepEditorModel step)
    {
        return string.Join(
            " ",
            [
                $"Write required process artifacts under {ProcessEvidenceRelativePath}.",
                $"For browser proof, use Playwright scratch output under {PlaywrightScratchRelativePath} with relative filenames only; do not pass managed artifact paths or other absolute workspace paths to browser tools.",
                $"Create three real UI proof files in scratch: '{step.Key}/calculator-proof.png' via browser_take_screenshot, '{step.Key}/calculator-page.yml' via browser_snapshot, and '{step.Key}/calculator-console.log' via browser_console_messages. Then run {ImportPlaywrightEvidenceScriptRelativePath} with step key '{step.Key}' so durable proof is imported into {BuildUiEvidenceStepRelativePath(step.Key)}.",
                $"Artifacts left only under {PlaywrightScratchRelativePath} are scratch files and do not satisfy the step contract.",
                BuildStepArtifactContractSummary(step)
            ]);
    }

    private string BuildUiValidationArtifactSummary(
        string stepKey,
        ProcessArtifactExpectationEditorModel artifact)
    {
        if (IsUiProofScreenshotArtifact(stepKey, artifact.Title))
        {
            return string.Join(
                " ",
                [
                    $"Must be the imported .png screenshot proving the calculator UI for step '{stepKey}'.",
                    $"Create it via browser_take_screenshot with the relative filename '{stepKey}/calculator-proof.png' under {PlaywrightScratchRelativePath}, then run {ImportPlaywrightEvidenceScriptRelativePath} with step key '{stepKey}' through workspace_pwsh_run_script with outputPaths [{BuildUiImportOutputPathArgument(stepKey)}].",
                    $"The durable screenshot must exist at {BuildArtifactRelativePath(stepKey, artifact)}.",
                    $"Scratch files left only under {PlaywrightScratchRelativePath} do not satisfy this requirement."
                ]);
        }

        if (IsUiValidationPageArtifact(stepKey, artifact.Title))
        {
            return string.Join(
                " ",
                [
                    $"Must be the imported Playwright page snapshot for step '{stepKey}'.",
                    $"Create it via browser_snapshot with the relative filename '{stepKey}/calculator-page.yml' under {PlaywrightScratchRelativePath}, then run {ImportPlaywrightEvidenceScriptRelativePath} with step key '{stepKey}' through workspace_pwsh_run_script with outputPaths [{BuildUiImportOutputPathArgument(stepKey)}].",
                    $"The durable snapshot must exist at {BuildArtifactRelativePath(stepKey, artifact)}.",
                    $"Scratch files left only under {PlaywrightScratchRelativePath} do not satisfy this requirement."
                ]);
        }

        if (IsUiValidationConsoleArtifact(stepKey, artifact.Title))
        {
            return string.Join(
                " ",
                [
                    $"Must be the imported Playwright browser console log for step '{stepKey}'.",
                    $"Create it via browser_console_messages with the relative filename '{stepKey}/calculator-console.log' under {PlaywrightScratchRelativePath}, then run {ImportPlaywrightEvidenceScriptRelativePath} with step key '{stepKey}' through workspace_pwsh_run_script with outputPaths [{BuildUiImportOutputPathArgument(stepKey)}].",
                    $"The durable console log must exist at {BuildArtifactRelativePath(stepKey, artifact)}.",
                    $"Scratch files left only under {PlaywrightScratchRelativePath} do not satisfy this requirement."
                ]);
        }

        return stepKey switch
        {
            "qa-validation" => string.Join(
                " ",
                [
                    "Must name changed flows, assertion depth, imported UI proof, and unresolved risks.",
                    $"Create this artifact at {BuildArtifactRelativePath(stepKey, artifact)} using workspace create/write file tools.",
                    $"Reference the imported screenshot at {UiEvidenceRelativePath}/{stepKey}/calculator-proof.png.",
                    "For this governed AI showcase, the assigned agent must leave explicit review-required evidence; do not rely on an implicit human fallback."
                ]),
            "execute-release-rollout" => string.Join(
                " ",
                [
                    "Must capture release timing, telemetry checkpoints, and any halt or rollback decision.",
                    $"Create this artifact at {BuildArtifactRelativePath(stepKey, artifact)} using workspace create/write file tools.",
                    $"Reference the imported screenshot at {UiEvidenceRelativePath}/{stepKey}/calculator-proof.png.",
                    "For this governed AI showcase, the assigned agent must leave explicit review-required evidence; do not rely on an implicit human fallback."
                ]),
            _ => string.Join(
                " ",
                [
                    $"Create this artifact at {BuildArtifactRelativePath(stepKey, artifact)} using workspace create/write file tools.",
                    "For this governed AI showcase, the assigned agent must leave explicit review-required evidence; do not rely on an implicit human fallback."
                ])
        };
    }

    private string BuildUiEvidenceStepRelativePath(string stepKey)
    {
        return $"{BuildScopedUiEvidenceRelativePath()}/{stepKey}";
    }

    private static string BuildUiEvidenceStepFullPath(string stepKey, ShowcaseWorkspacePlan workspacePlan)
    {
        return Path.Combine(workspacePlan.UiEvidenceFullPath, stepKey);
    }

    private static bool IsUiValidationStep(string stepKey)
    {
        foreach (var uiValidationStepKey in UiValidationStepKeys)
        {
            if (string.Equals(uiValidationStepKey, stepKey, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsUiProofScreenshotArtifact(string stepKey, string artifactTitle)
    {
        return IsUiValidationStep(stepKey) &&
               string.Equals(artifactTitle, "Calculator proof", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsUiValidationPageArtifact(string stepKey, string artifactTitle)
    {
        return IsUiValidationStep(stepKey) &&
               string.Equals(artifactTitle, "Calculator page", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsUiValidationConsoleArtifact(string stepKey, string artifactTitle)
    {
        return IsUiValidationStep(stepKey) &&
               string.Equals(artifactTitle, "Calculator console", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryResolveUiValidationArtifactRelativePath(
        string stepKey,
        string artifactTitle,
        out string relativePath)
    {
        if (IsUiProofScreenshotArtifact(stepKey, artifactTitle))
        {
            relativePath = $"{UiEvidenceRelativePath}/{stepKey}/calculator-proof.png";
            return true;
        }

        if (IsUiValidationPageArtifact(stepKey, artifactTitle))
        {
            relativePath = $"{UiEvidenceRelativePath}/{stepKey}/calculator-page.yml";
            return true;
        }

        if (IsUiValidationConsoleArtifact(stepKey, artifactTitle))
        {
            relativePath = $"{UiEvidenceRelativePath}/{stepKey}/calculator-console.log";
            return true;
        }

        relativePath = string.Empty;
        return false;
    }

    private static string BuildUiImportOutputPathArgument(string stepKey)
    {
        return string.Join(
            ", ",
            [
                $"'{UiEvidenceRelativePath}/{stepKey}/calculator-proof.png'",
                $"'{UiEvidenceRelativePath}/{stepKey}/calculator-page.yml'",
                $"'{UiEvidenceRelativePath}/{stepKey}/calculator-console.log'",
                $"'{UiEvidenceRelativePath}/{stepKey}/import-summary.json'"
            ]);
    }

    private static string BuildImplementationOutputPathArgument()
    {
        return string.Join(
            ", ",
            [
                $"'{AppProjectRelativePath}'",
                $"'{AppProgramRelativePath}'",
                $"'{AppHomeRelativePath}'"
            ]);
    }

    private async Task UpsertProcessBindingAsync(
        Guid projectId,
        string featureNodeId,
        Guid definitionId,
        Guid? runId,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var node = await dbContext.Set<ProjectObjectRecord>()
            .FirstOrDefaultAsync(item => item.ProjectId == projectId && item.NodeKey == featureNodeId, cancellationToken)
            ?? throw new InvalidOperationException($"Feature node '{featureNodeId}' was not found.");
        var binding = await dbContext.Set<ProjectNodeBindingRecord>()
            .FirstOrDefaultAsync(item => item.ProjectObjectId == node.Id, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var route = runId.HasValue
            ? $"/projects/{projectId:D}/processes?processId={definitionId:D}&runId={runId.Value:D}"
            : $"/projects/{projectId:D}/processes?processId={definitionId:D}";

        if (binding is null)
        {
            binding = new ProjectNodeBindingRecord
            {
                ProjectObjectId = node.Id,
                CreatedAtUtc = now
            };
            await dbContext.Set<ProjectNodeBindingRecord>().AddAsync(binding, cancellationToken);
        }

        binding.Route = route;
        binding.ExternalArtifactKind = runId.HasValue
            ? ProjectObjectType.ProcessRun.ToString()
            : ProjectObjectType.ProcessDefinition.ToString();
        binding.ExternalArtifactId = runId ?? definitionId;
        binding.MediaRelativePath = string.Empty;
        binding.MediaContentType = string.Empty;
        binding.MediaOriginalFileName = string.Empty;
        binding.StorageObjectReferenceJson = string.Empty;
        binding.UpdatedAtUtc = now;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<ShowcaseLaunchResult> CreateLaunchAndRunAsync(
        Guid projectId,
        Guid definitionId,
        IReadOnlyDictionary<string, ShowcaseAgentBinding> bindingsByRoleKey,
        ShowcaseGraph graph,
        ShowcaseWorkspacePlan workspacePlan,
        CancellationToken cancellationToken)
    {
        var launchPlanId = EnsureSuccess(await processesService.CreateLaunchPlanAsync(
            new ProcessLaunchCreateRequest
            {
                ProcessDefinitionId = definitionId,
                ProjectId = projectId,
                LaunchName = $"Showcase / {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss}",
                OperatingMode = ProcessOperatingMode.GovernedLive,
                TriggerReason = BuildLaunchTriggerReason(workspacePlan),
                RequestedBy = "agent-showcase-seeder"
            },
            cancellationToken));
        var launchPlan = await processesService.GetLaunchPlanAsync(launchPlanId, cancellationToken)
            ?? throw new InvalidOperationException($"Launch plan '{launchPlanId}' was not found after creation.");

        foreach (var role in launchPlan.Roles)
        {
            if (!bindingsByRoleKey.TryGetValue(role.RoleKey, out var binding))
            {
                throw new InvalidOperationException($"No bound showcase agent exists for role '{role.RoleKey}'.");
            }

            var candidate = role.Candidates.FirstOrDefault(item =>
                item.PartyId == binding.PartyId ||
                item.TechnicalAgentId == binding.TechnicalAgentId);
            if (candidate is null)
            {
                throw new InvalidOperationException(
                    $"Launch plan role '{role.RoleKey}' did not expose the expected candidate for party '{binding.PartyId:D}'.");
            }

            EnsureSuccess(await processesService.SelectLaunchCandidateAsync(
                new ProcessLaunchCandidateSelectionRequest
                {
                    LaunchPlanId = launchPlanId,
                    LaunchPlanRoleId = role.Id,
                    CandidateId = candidate.Id
                },
                cancellationToken));
        }

        EnsureSuccess(await processesService.SubmitLaunchPlanForApprovalAsync(
            launchPlanId,
            "agent-showcase-seeder",
            cancellationToken));
        EnsureSuccess(await processesService.DecideLaunchPlanApprovalAsync(
            new ProcessLaunchApprovalDecisionRequest
            {
                LaunchPlanId = launchPlanId,
                Status = ProcessLaunchApprovalStatus.Approved,
                ResolutionSummary = "Scenario seeder auto-approved the launch after resolving exact showcase agents.",
                DecidedBy = "agent-showcase-seeder"
            },
            cancellationToken));

        launchPlan = await processesService.GetLaunchPlanAsync(launchPlanId, cancellationToken)
            ?? throw new InvalidOperationException($"Launch plan '{launchPlanId}' disappeared after approval.");
        if (launchPlan.Status == ProcessLaunchPlanStatus.Provisioning)
        {
            EnsureSuccess(await processesService.ProvisionLaunchPlanAsync(
                launchPlanId,
                "agent-showcase-seeder",
                cancellationToken));
            launchPlan = await processesService.GetLaunchPlanAsync(launchPlanId, cancellationToken)
                ?? throw new InvalidOperationException($"Launch plan '{launchPlanId}' disappeared after provisioning.");
        }

        if (launchPlan.Status is not ProcessLaunchPlanStatus.Ready and not ProcessLaunchPlanStatus.Approved)
        {
            throw new InvalidOperationException(
                $"Launch plan '{launchPlanId}' is not executable. Status={launchPlan.Status}.");
        }

        var runId = EnsureSuccess(await processesService.ExecuteLaunchPlanAsync(
            new ProcessLaunchExecutionRequest
            {
                LaunchPlanId = launchPlanId,
                RequestedBy = "agent-showcase-seeder"
            },
            cancellationToken));
        await UpsertProcessBindingAsync(projectId, graph.FeatureNodeId, definitionId, runId, cancellationToken);
        return new ShowcaseLaunchResult(launchPlanId, runId);
    }

    private string BuildLaunchTriggerReason(ShowcaseWorkspacePlan workspacePlan)
    {
        return string.Join(
            Environment.NewLine,
            [
                "Deliver the Blazor SSR simple calculator showcase end to end.",
                $"Workspace brief: {BriefRelativePath}",
                $"Application project: {AppProjectRelativePath}",
                $"Process evidence root: {ProcessEvidenceRelativePath}",
                $"Managed UI evidence root: {BuildScopedUiEvidenceRelativePath()}",
                $"Browser evidence filesystem root: {workspacePlan.UiEvidenceFullPath}",
                $"Use {ApplyAppScriptRelativePath}, {LaunchScriptRelativePath}, and {ImportPlaywrightEvidenceScriptRelativePath} only when the current step note, evidence contract, or required artifacts explicitly require them.",
                "Only qa-validation and execute-release-rollout should create Playwright screenshot evidence or import browser proof.",
                "Acceptance: four arithmetic operations, clear/reset path, divide-by-zero handling, buildable proof, screenshot-backed QA, and explicit post-release learning notes."
            ]);
    }

    private async Task<ShowcaseMonitoringResult> MonitorRunAsync(
        Guid projectId,
        ShowcaseGraph graph,
        Guid definitionId,
        Guid launchPlanId,
        Guid runId,
        IReadOnlyDictionary<string, AgentDefinition> agentsByRoleKey,
        ShowcaseWorkspacePlan workspacePlan,
        IAgentFrameworkWorkspaceService workspaceService,
        CancellationToken cancellationToken)
    {
        var timeoutAtUtc = DateTimeOffset.UtcNow.AddMinutes(25);
        ProcessRunListItem? run = null;
        ProcessWorkspaceRunDetails? runDetails = null;

        while (DateTimeOffset.UtcNow < timeoutAtUtc)
        {
            run = (await processesService.ListRunsAsync(definitionId, projectId, cancellationToken))
                .FirstOrDefault(item => item.Id == runId)
                ?? throw new InvalidOperationException($"Process run '{runId}' is no longer visible.");
            runDetails = await LoadRunDetailsAsync(workspaceService, runId, cancellationToken);
            var completedSteps = runDetails.StepRuns.Count(item =>
                item.Status is ProcessStepRunStatus.Completed or ProcessStepRunStatus.Skipped);
            var percent = runDetails.StepRuns.Count == 0
                ? 0
                : (int)Math.Round(completedSteps * 100d / runDetails.StepRuns.Count, MidpointRounding.AwayFromZero);
            await UpdateGraphProgressAsync(
                projectId,
                graph,
                ResolveNodeStatus(run.Status),
                run.Status == ProcessRunStatus.Completed ? 100 : percent,
                cancellationToken);

            if (run.Status is ProcessRunStatus.Completed or ProcessRunStatus.Blocked or ProcessRunStatus.Failed or ProcessRunStatus.Cancelled)
            {
                break;
            }

            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
        }

        if (run is null || runDetails is null)
        {
            throw new InvalidOperationException($"Showcase monitoring for launch plan '{launchPlanId}' did not load a process run snapshot.");
        }

        var stepResults = runDetails.StepRuns
            .OrderBy(item => item.Sequence)
            .Select(item => new AgentShowcaseStepResult(
                item.Sequence,
                item.Title,
                item.Status.ToString(),
                item.CurrentExecutorName,
                item.DecisionSummary,
                item.BlockedReason,
                item.RefusalReason))
            .ToList();
        var executionResults = runDetails.ExecutionRuns
            .OrderBy(item => item.CreatedAtUtc)
            .Select(item => new AgentShowcaseExecutionResult(
                item.Id,
                item.StepTitle,
                item.AgentName,
                item.State.ToString(),
                item.Outcome?.ToString() ?? string.Empty,
                item.Approvals.Count,
                item.Artifacts.Count,
                item.ResultSummary))
            .ToList();

        if (run.Status != ProcessRunStatus.Completed)
        {
            throw new InvalidOperationException(
                $"Showcase process run '{runId}' ended in status {run.Status}.{Environment.NewLine}{BuildRunFailureSummary(stepResults)}");
        }

        try
        {
            await ValidateCompletedShowcaseRunAsync(
                projectId,
                definitionId,
                workspacePlan,
                runDetails,
                cancellationToken);
        }
        catch
        {
            await UpdateGraphProgressAsync(projectId, graph, "Blocked", 95, cancellationToken);
            throw;
        }

        return new ShowcaseMonitoringResult(run, stepResults, executionResults);
    }

    private async Task ValidateCompletedShowcaseRunAsync(
        Guid projectId,
        Guid definitionId,
        ShowcaseWorkspacePlan workspacePlan,
        ProcessWorkspaceRunDetails runDetails,
        CancellationToken cancellationToken)
    {
        var failures = new List<string>();
        var pidFile = Path.Combine(workspacePlan.UiEvidenceFullPath, "calculator-app.pid");
        var appProjectFullPath = Path.Combine(
            workspacePlan.AppParentFullPath,
            "SimpleCalculatorApp",
            "SimpleCalculatorApp.csproj");
        if (!File.Exists(appProjectFullPath))
        {
            failures.Add($"Generated app project is missing at '{appProjectFullPath}'.");
        }

        var definition = await processesService.GetEditorAsync(definitionId, projectId, cancellationToken);
        foreach (var step in definition.Steps)
        {
            foreach (var artifact in step.ArtifactExpectations.Where(item => item.IsRequired))
            {
                var artifactFullPath = ResolveWorkspaceFullPath(
                    BuildArtifactRelativePath(step.Key, artifact),
                    workspaceFactory.GetOrganizationScope());
                if (!File.Exists(artifactFullPath))
                {
                    failures.Add($"Required artifact '{artifact.Title}' is missing at '{artifactFullPath}'.");
                }

                var isRegistered = runDetails.Artifacts.Any(item =>
                    string.Equals(item.Title, artifact.Title, StringComparison.OrdinalIgnoreCase));
                if (!isRegistered)
                {
                    failures.Add($"Required process artifact '{artifact.Title}' was not registered in the process run.");
                }
            }

            if (IsUiValidationStep(step.Key))
            {
                var uiEvidenceStepPath = Path.Combine(workspacePlan.UiEvidenceFullPath, step.Key);
                if (!Directory.Exists(uiEvidenceStepPath))
                {
                    failures.Add($"UI evidence directory '{uiEvidenceStepPath}' was not created.");
                    continue;
                }

                var screenshotPaths = Directory.GetFiles(uiEvidenceStepPath, "*.*", SearchOption.AllDirectories)
                    .Where(IsScreenshotFile)
                    .ToList();
                if (screenshotPaths.Count == 0)
                {
                    failures.Add($"UI validation step '{step.Title}' did not produce any screenshots under '{uiEvidenceStepPath}'.");
                }

                var snapshotPaths = Directory.GetFiles(uiEvidenceStepPath, "*.*", SearchOption.AllDirectories)
                    .Where(path =>
                        string.Equals(Path.GetExtension(path), ".md", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(Path.GetExtension(path), ".yml", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(Path.GetExtension(path), ".yaml", StringComparison.OrdinalIgnoreCase))
                    .ToList();
                if (snapshotPaths.Count == 0)
                {
                    failures.Add($"UI validation step '{step.Title}' did not import a Playwright snapshot or page dump under '{uiEvidenceStepPath}'.");
                }

                var logPaths = Directory.GetFiles(uiEvidenceStepPath, "*.log", SearchOption.AllDirectories)
                    .ToList();
                if (logPaths.Count == 0)
                {
                    failures.Add($"UI validation step '{step.Title}' did not import any Playwright console logs under '{uiEvidenceStepPath}'.");
                }
            }
        }

        ValidateStepToolReceipts(
            failures,
            runDetails.ExecutionRuns,
            "Implement feature, tests, and migration notes",
            "pwsh_run_script",
            "dotnet_build");
        ValidateStepToolReceipts(
            failures,
            runDetails.ExecutionRuns,
            "Run QA validation and browser proof",
            "pwsh_run_script");
        ValidateStepToolReceipts(
            failures,
            runDetails.ExecutionRuns,
            "Execute controlled release rollout",
            "pwsh_run_script");
        ValidateBrowserEvidence(
            failures,
            runDetails.ExecutionRuns,
            "Run QA validation and browser proof");
        ValidateBrowserEvidence(
            failures,
            runDetails.ExecutionRuns,
            "Execute controlled release rollout");

        ValidateGeneratedAppSource(failures, appProjectFullPath);

        TryStopShowcaseProcess(pidFile);

        if (File.Exists(appProjectFullPath))
        {
            var buildFailure = await TryBuildGeneratedAppAsync(appProjectFullPath, cancellationToken);
            if (!string.IsNullOrWhiteSpace(buildFailure))
            {
                failures.Add(buildFailure);
            }
        }

        if (failures.Count > 0)
        {
            throw new InvalidOperationException(
                "Showcase run completed but failed post-run validation:" +
                Environment.NewLine +
                string.Join(Environment.NewLine, failures.Select(item => $"- {item}")));
        }
    }

    private static void ValidateGeneratedAppSource(List<string> failures, string appProjectFullPath)
    {
        if (!File.Exists(appProjectFullPath))
        {
            return;
        }

        var appDirectory = Path.GetDirectoryName(appProjectFullPath);
        if (string.IsNullOrWhiteSpace(appDirectory))
        {
            failures.Add($"Generated app directory could not be resolved from '{appProjectFullPath}'.");
            return;
        }

        var programPath = Path.Combine(appDirectory, "Program.cs");
        var homePath = Path.Combine(appDirectory, "Components", "Pages", "Home.razor");
        var projectText = File.ReadAllText(appProjectFullPath);

        if (projectText.Contains("Microsoft.AspNetCore.Components.WebAssembly", StringComparison.OrdinalIgnoreCase) ||
            projectText.Contains("PackageReference Include=\"Microsoft.AspNetCore.Components\"", StringComparison.OrdinalIgnoreCase))
        {
            failures.Add($"Generated app project '{appProjectFullPath}' still contains legacy Blazor package references instead of the .NET 10 scaffold defaults.");
        }

        if (!File.Exists(programPath))
        {
            failures.Add($"Generated app is missing Program.cs at '{programPath}'.");
        }
        else
        {
            var programText = File.ReadAllText(programPath);
            if (!programText.Contains("AddRazorComponents", StringComparison.Ordinal))
            {
                failures.Add($"Generated Program.cs at '{programPath}' does not register AddRazorComponents().");
            }

            if (!programText.Contains("MapRazorComponents<App>()", StringComparison.Ordinal))
            {
                failures.Add($"Generated Program.cs at '{programPath}' does not map the Razor component app.");
            }

            if (programText.Contains("AddInteractiveServerComponents", StringComparison.Ordinal) ||
                programText.Contains("AddInteractiveServerRenderMode", StringComparison.Ordinal))
            {
                failures.Add($"Generated Program.cs at '{programPath}' still enables interactive server components instead of static SSR only.");
            }
        }

        if (!File.Exists(homePath))
        {
            failures.Add($"Generated app is missing Home.razor at '{homePath}'.");
            return;
        }

        var homeText = File.ReadAllText(homePath);
        ValidateHomePageText(failures, homePath, homeText, "method=\"get\"", "GET form submission");
        ValidateHomePageText(failures, homePath, homeText, "name=\"operation\"", "operation selector naming");
        ValidateHomePageText(failures, homePath, homeText, "[SupplyParameterFromQuery(Name = \"left\")]", "left query binding");
        ValidateHomePageText(failures, homePath, homeText, "[SupplyParameterFromQuery(Name = \"right\")]", "right query binding");
        ValidateHomePageText(failures, homePath, homeText, "[SupplyParameterFromQuery(Name = \"operation\")]", "operation query binding");
        ValidateHomePageText(failures, homePath, homeText, "Enum.TryParse<CalculatorOperation>", "safe operation parsing");
        ValidateHomePageText(failures, homePath, homeText, "Division by zero is not allowed.", "divide-by-zero handling");

        if (!homeText.Contains("Clear", StringComparison.OrdinalIgnoreCase) &&
            !homeText.Contains("Reset", StringComparison.OrdinalIgnoreCase))
        {
            failures.Add($"Generated Home.razor at '{homePath}' does not expose a clear or reset path.");
        }

        if (!homeText.Contains("Result:", StringComparison.Ordinal))
        {
            failures.Add($"Generated Home.razor at '{homePath}' does not render explicit arithmetic result text.");
        }
    }

    private static void ValidateHomePageText(
        List<string> failures,
        string homePath,
        string homeText,
        string requiredText,
        string description)
    {
        if (!homeText.Contains(requiredText, StringComparison.Ordinal))
        {
            failures.Add($"Generated Home.razor at '{homePath}' is missing the required {description} marker '{requiredText}'.");
        }
    }

    private static void ValidateStepToolReceipts(
        List<string> failures,
        IReadOnlyList<ProcessExecutionRunViewModel> executionRuns,
        string stepTitle,
        params string[] requiredToolHints)
    {
        var stepExecutionRuns = executionRuns
            .Where(item => string.Equals(item.StepTitle, stepTitle, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (stepExecutionRuns.Count == 0)
        {
            failures.Add($"No execution run was recorded for step '{stepTitle}'.");
            return;
        }

        foreach (var requiredToolHint in requiredToolHints)
        {
            var matched = stepExecutionRuns
                .SelectMany(item => item.ToolReceipts)
                .Any(item => ToolNameMatches(item.ToolName, requiredToolHint));
            if (!matched)
            {
                failures.Add($"Step '{stepTitle}' did not record required tool usage matching '{requiredToolHint}'.");
            }
        }
    }

    private static void ValidateBrowserEvidence(
        List<string> failures,
        IReadOnlyList<ProcessExecutionRunViewModel> executionRuns,
        string stepTitle)
    {
        var stepExecutionRuns = executionRuns
            .Where(item => string.Equals(item.StepTitle, stepTitle, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (stepExecutionRuns.Count == 0)
        {
            failures.Add($"No execution run was recorded for step '{stepTitle}'.");
            return;
        }

        if (!stepExecutionRuns.Any(item => item.HasBrowserEvidenceToolInvocation))
        {
            failures.Add($"Step '{stepTitle}' did not invoke browser navigation, snapshot, or screenshot tools.");
        }
    }

    private static bool ToolNameMatches(string toolName, string requiredToolHint)
    {
        var normalizedToolName = NormalizeToolToken(toolName);
        var normalizedHint = NormalizeToolToken(requiredToolHint);
        return normalizedToolName.Contains(normalizedHint, StringComparison.Ordinal);
    }

    private static string NormalizeToolToken(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Replace('-', '_').Trim().ToLowerInvariant();
    }

    private static bool IsScreenshotFile(string path)
    {
        return string.Equals(Path.GetExtension(path), ".png", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(Path.GetExtension(path), ".jpg", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(Path.GetExtension(path), ".jpeg", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<string> TryBuildGeneratedAppAsync(string appProjectFullPath, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(appProjectFullPath) ?? string.Empty
        };
        startInfo.ArgumentList.Add("build");
        startInfo.ArgumentList.Add(appProjectFullPath);
        startInfo.ArgumentList.Add("--nologo");

        using var process = new Process
        {
            StartInfo = startInfo
        };
        process.Start();
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync(cancellationToken);
        var stdout = await stdoutTask;
        var stderr = await stderrTask;
        if (process.ExitCode == 0)
        {
            return string.Empty;
        }

        var combinedOutput = string.Join(
            Environment.NewLine,
            new[]
            {
                stdout.Trim(),
                stderr.Trim()
            }.Where(item => !string.IsNullOrWhiteSpace(item)));
        if (combinedOutput.Length > 1600)
        {
            combinedOutput = combinedOutput[..1600].TrimEnd() + "...";
        }

        return $"Generated app failed 'dotnet build' for '{appProjectFullPath}'. Output: {combinedOutput}";
    }

    private async Task<ProcessWorkspaceRunDetails> LoadRunDetailsAsync(
        IAgentFrameworkWorkspaceService workspaceService,
        Guid runId,
        CancellationToken cancellationToken)
    {
        var runDetails = await processesService.GetRunDetailsAsync(runId, cancellationToken);
        var executionRuns = await LoadExecutionRunsAsync(workspaceService, runId, runDetails.StepRuns, cancellationToken);
        return runDetails with
        {
            ExecutionRuns = executionRuns
        };
    }

    private async Task<IReadOnlyList<ProcessExecutionRunViewModel>> LoadExecutionRunsAsync(
        IAgentFrameworkWorkspaceService workspaceService,
        Guid runId,
        IReadOnlyList<ProcessStepRunViewModel> stepRuns,
        CancellationToken cancellationToken)
    {
        var executionRuns = await workspaceService.ListExecutionRunsAsync(
            new ExecutionRunQuery(
                ProcessRunId: runId.ToString("D"),
                Take: 200),
            cancellationToken);
        if (executionRuns.Count == 0)
        {
            return [];
        }

        var stepTitlesById = stepRuns.ToDictionary(item => item.Id, item => item.Title);
        var agentsById = (await workspaceService.ListAgentsAsync(includeTemplates: false, cancellationToken))
            .ToDictionary(item => item.Id);
        var mappedRuns = new List<ProcessExecutionRunViewModel>(executionRuns.Count);

        foreach (var executionRun in executionRuns.OrderByDescending(item => item.CreatedAtUtc))
        {
            var detail = await workspaceService.GetExecutionRunDetailAsync(executionRun.Id, cancellationToken);
            mappedRuns.Add(MapExecutionRun(detail, stepTitlesById, agentsById));
        }

        return mappedRuns;
    }

    private async Task UpdateGraphProgressAsync(
        Guid projectId,
        ShowcaseGraph graph,
        string status,
        int progressPercent,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var nodes = await dbContext.Set<ProjectObjectRecord>()
            .Where(item =>
                item.ProjectId == projectId &&
                (item.NodeKey == graph.PhaseNodeId ||
                    item.NodeKey == graph.DeliveryBlockNodeId ||
                    item.NodeKey == graph.FeatureNodeId))
            .ToListAsync(cancellationToken);

        foreach (var node in nodes)
        {
            node.Status = status;
            node.ProgressMode = "progress";
            node.ProgressPercent = progressPercent;
            node.UpdatedAtUtc = DateTimeOffset.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string ResolveNodeStatus(ProcessRunStatus status)
    {
        return status switch
        {
            ProcessRunStatus.Draft => "Planned",
            ProcessRunStatus.Active => "In Progress",
            ProcessRunStatus.Blocked => "Blocked",
            ProcessRunStatus.Completed => "Completed",
            ProcessRunStatus.Cancelled => "Cancelled",
            ProcessRunStatus.Failed => "Failed",
            _ => status.ToString()
        };
    }

    private static ProjectStructureNode? FindShowcaseNode(
        ProjectStructureSurface surface,
        ProjectObjectType objectType,
        string title)
    {
        return surface.Nodes.FirstOrDefault(item =>
                   item.ObjectType == objectType &&
                   item.Notes.Contains(Marker, StringComparison.Ordinal) &&
                   string.Equals(item.Title, title, StringComparison.Ordinal))
               ?? surface.Nodes.FirstOrDefault(item =>
                   item.ObjectType == objectType &&
                   string.Equals(item.Title, title, StringComparison.Ordinal));
    }

    private static string MergeSingleLine(string existing, string addition)
    {
        if (string.IsNullOrWhiteSpace(addition))
        {
            return existing?.Trim() ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(existing))
        {
            return addition.Trim();
        }

        return existing.Contains(addition, StringComparison.Ordinal)
            ? existing.Trim()
            : $"{existing.Trim()} {addition.Trim()}";
    }

    private static string MergeParagraphs(string existing, string addition)
    {
        if (string.IsNullOrWhiteSpace(existing))
        {
            return addition.Trim();
        }

        if (existing.Contains(addition, StringComparison.Ordinal))
        {
            return existing.Trim();
        }

        return $"{existing.Trim()}{Environment.NewLine}{Environment.NewLine}{addition.Trim()}";
    }

    private static string BuildRunFailureSummary(IReadOnlyList<AgentShowcaseStepResult> stepResults)
    {
        var relevant = stepResults
            .Where(item =>
                !string.Equals(item.Status, ProcessStepRunStatus.Completed.ToString(), StringComparison.OrdinalIgnoreCase) &&
                (!string.IsNullOrWhiteSpace(item.BlockedReason) ||
                    !string.IsNullOrWhiteSpace(item.RefusalReason) ||
                    !string.IsNullOrWhiteSpace(item.DecisionSummary)))
            .ToList();
        if (relevant.Count == 0)
        {
            return "No detailed blocked-step summary was available.";
        }

        return string.Join(
            Environment.NewLine,
            relevant.Select(item =>
            {
                var reason = !string.IsNullOrWhiteSpace(item.BlockedReason)
                    ? item.BlockedReason
                    : !string.IsNullOrWhiteSpace(item.RefusalReason)
                        ? item.RefusalReason
                        : item.DecisionSummary;
                return $"- {item.Sequence}. {item.Title} [{item.Status}] {reason}";
            }));
    }

    private static ProcessExecutionRunViewModel MapExecutionRun(
        ExecutionRunDetail detail,
        IReadOnlyDictionary<Guid, string> stepTitlesById,
        IReadOnlyDictionary<Guid, AgentDefinition> agentsById)
    {
        var stepRunId = Guid.TryParse(detail.Run.ProcessStepId, out var parsedStepRunId)
            ? parsedStepRunId
            : (Guid?)null;
        var stepTitle = stepRunId.HasValue && stepTitlesById.TryGetValue(stepRunId.Value, out var resolvedStepTitle)
            ? resolvedStepTitle
            : string.Empty;
        agentsById.TryGetValue(detail.Run.AgentId, out var agent);

        return new ProcessExecutionRunViewModel(
            detail.Run.Id,
            detail.Run.AgentId,
            stepRunId,
            stepTitle,
            agent?.Name ?? detail.Run.AgentId.ToString("D"),
            agent?.RoleTitle ?? string.Empty,
            string.IsNullOrWhiteSpace(detail.Run.Title)
                ? string.IsNullOrWhiteSpace(stepTitle)
                    ? "Technical execution"
                    : stepTitle
                : detail.Run.Title,
            detail.Run.ProviderName,
            detail.Run.Model,
            detail.Run.State,
            detail.Run.Outcome,
            detail.Run.InputSummary,
            detail.Run.ResultSummary,
            detail.Run.CreatedAtUtc,
            detail.Run.UpdatedAtUtc,
            detail.Run.StartedAtUtc,
            detail.Run.CompletedAtUtc,
            detail.ExecutionLog.Count)
        {
            HasBrowserEvidenceToolInvocation = HasBrowserEvidenceToolInvocation(detail.ExecutionLog),
            Approvals = detail.Approvals
                .OrderByDescending(item => item.RequestedAtUtc)
                .Select(item => new ProcessExecutionApprovalViewModel(
                    item.ApprovalId,
                    item.ToolName,
                    item.ToolKind,
                    item.Status,
                    item.Details,
                    item.RequestedAtUtc,
                    item.DecidedAtUtc,
                    item.DecisionNotes))
                .ToList(),
            Artifacts = detail.Artifacts
                .OrderByDescending(item => item.CreatedAtUtc)
                .Select(item => new ProcessExecutionArtifactViewModel(
                    item.Id,
                    item.ArtifactKind,
                    item.DisplayName,
                    item.RelativePath,
                    item.ContentType,
                    item.ProducedBy,
                    item.Summary,
                    item.CreatedAtUtc))
                .ToList(),
            Checkpoints = detail.Checkpoints
                .OrderByDescending(item => item.CapturedAtUtc)
                .Select(item => new ProcessExecutionCheckpointViewModel(
                    item.Id,
                    item.CheckpointKind,
                    item.RunState,
                    item.PendingApprovalIds.Count,
                    item.CapturedAtUtc,
                    item.ResumedAtUtc))
                .ToList(),
            ToolReceipts = detail.ToolReceipts
                .OrderByDescending(item => item.StartedAtUtc)
                .Select(item => new ProcessExecutionToolReceiptViewModel(
                    item.Id,
                    item.ToolFamily,
                    item.ToolName,
                    item.RiskClass,
                    item.ApprovalMode,
                    item.IsolationGuarantee,
                    item.RequestSummary,
                    item.WorkingDirectory,
                    item.ExitSummary,
                    item.StartedAtUtc,
                    item.CompletedAtUtc))
                .ToList()
        };
    }

    private static bool HasBrowserEvidenceToolInvocation(IReadOnlyList<ExecutionLogEntry> executionLog)
    {
        return executionLog.Any(item =>
            item.Message.Contains("Invoking tool 'browser_navigate'", StringComparison.OrdinalIgnoreCase) ||
            item.Message.Contains("Invoking tool 'browser_snapshot'", StringComparison.OrdinalIgnoreCase) ||
            item.Message.Contains("Invoking tool 'browser_take_screenshot'", StringComparison.OrdinalIgnoreCase));
    }
    private static void EnsureShowcaseDependencies(ProcessDefinitionEditorModel editor)
    {
        var stepsByKey = editor.Steps
            .Where(item => !string.IsNullOrWhiteSpace(item.Key) && item.Id.HasValue)
            .ToDictionary(item => item.Key, StringComparer.OrdinalIgnoreCase);
        SetDependencies(
            stepsByKey,
            "implementation",
            "feature-intake",
            "architecture-review");
        SetDependencies(
            stepsByKey,
            "peer-review",
            "architecture-review",
            "implementation");
        SetDependencies(
            stepsByKey,
            "qa-validation",
            "implementation",
            "peer-review");
        SetDependencies(
            stepsByKey,
            "security-review",
            "implementation",
            "peer-review");
    }

    private static void SetDependencies(
        IReadOnlyDictionary<string, ProcessStepEditorModel> stepsByKey,
        string targetStepKey,
        params string[] dependencyStepKeys)
    {
        if (!stepsByKey.TryGetValue(targetStepKey, out var targetStep))
        {
            return;
        }

        targetStep.Dependencies = dependencyStepKeys
            .Select(stepKey => ResolveDependency(stepsByKey, stepKey))
            .Where(item => item is not null)
            .Select(item => item!)
            .ToList();
    }

    private static ProcessStepDependencyEditorModel? ResolveDependency(
        IReadOnlyDictionary<string, ProcessStepEditorModel> stepsByKey,
        string dependencyStepKey)
    {
        if (!stepsByKey.TryGetValue(dependencyStepKey, out var dependencyStep) || !dependencyStep.Id.HasValue)
        {
            return null;
        }

        return new ProcessStepDependencyEditorModel
        {
            DependsOnStepId = dependencyStep.Id
        };
    }
}
