using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.ScenarioSeeder;

internal sealed partial class UnitsConverterDeliveryProvisioningSeeder
{
    private const string ScopePhaseTitle = "Scope and architecture";
    private const string BuildPhaseTitle = "Implementation and review";
    private const string ReleasePhaseTitle = "Quality, release, and learning";
    private const string ScopeBlockTitle = "Units converter delivery plan";
    private const string BuildBlockTitle = "Basic units converter application";
    private const string ReleaseBlockTitle = "Release evidence and follow-up";
    private const string DeliveryFeatureTitle = "Deliver the Blazor SSR units converter";
    private const string DomainFeatureTitle = "Typed conversion domain and unit catalog";
    private const string UiFeatureTitle = "SSR conversion workspace";
    private const string ReleaseFeatureTitle = "QA, UI review, and release evidence";

    private async Task<UnitsConverterProjectGraph> EnsureProjectStructureAsync(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var surface = await projectWorkbenchService.GetStructureAsync(projectId, cancellationToken);
        var scopePhase = FindNode(surface, ProjectObjectType.Phase, ScopePhaseTitle)
            ?? await CreateNodeAsync(
                projectId,
                ProjectObjectType.Phase,
                ScopePhaseTitle,
                "Phase / scope, architecture, and acceptance framing",
                $"{Marker}{Environment.NewLine}Confirms the delivery boundary, conversion categories, and maintainable app shape before implementation starts.",
                null,
                "delivery-scope-phase",
                cancellationToken);
        var buildPhase = FindNode(surface, ProjectObjectType.Phase, BuildPhaseTitle)
            ?? await CreateNodeAsync(
                projectId,
                ProjectObjectType.Phase,
                BuildPhaseTitle,
                "Phase / implementation, code review, and UI review",
                $"{Marker}{Environment.NewLine}Owns the actual code change, test assets, code review, screenshot-backed UI review, and QA progression.",
                null,
                "delivery-build-phase",
                cancellationToken);
        var releasePhase = FindNode(surface, ProjectObjectType.Phase, ReleasePhaseTitle)
            ?? await CreateNodeAsync(
                projectId,
                ProjectObjectType.Phase,
                ReleasePhaseTitle,
                "Phase / release approval, rollout, and learning",
                $"{Marker}{Environment.NewLine}Owns release readiness, controlled rollout, durable output visibility, and corrective learning.",
                null,
                "delivery-release-phase",
                cancellationToken);

        var scopeBlock = FindNode(surface, ProjectObjectType.ProjectBlock, ScopeBlockTitle)
            ?? await CreateNodeAsync(
                projectId,
                ProjectObjectType.ProjectBlock,
                ScopeBlockTitle,
                "Block / brief, acceptance, and architecture intent",
                $"{Marker}{Environment.NewLine}Covers the brief at {BriefRelativePath} and the intended layered solution shape.",
                scopePhase.Id,
                "delivery-scope-block",
                cancellationToken);
        var buildBlock = FindNode(surface, ProjectObjectType.ProjectBlock, BuildBlockTitle)
            ?? await CreateNodeAsync(
                projectId,
                ProjectObjectType.ProjectBlock,
                BuildBlockTitle,
                "Block / real application implementation",
                $"{Marker}{Environment.NewLine}Builds the solution at {SolutionRelativePath} with Core, Web, and test projects.",
                buildPhase.Id,
                "delivery-build-block",
                cancellationToken);
        var releaseBlock = FindNode(surface, ProjectObjectType.ProjectBlock, ReleaseBlockTitle)
            ?? await CreateNodeAsync(
                projectId,
                ProjectObjectType.ProjectBlock,
                ReleaseBlockTitle,
                "Block / governed proof, rollout, and learning",
                $"{Marker}{Environment.NewLine}Tracks QA, UI review, release approval, rollout evidence, and project-structure-visible outputs.",
                releasePhase.Id,
                "delivery-release-block",
                cancellationToken);

        var deliveryFeature = FindNode(surface, ProjectObjectType.WorkItem, DeliveryFeatureTitle)
            ?? await CreateNodeAsync(
                projectId,
                ProjectObjectType.WorkItem,
                DeliveryFeatureTitle,
                "Feature / process-bound delivery item",
                $"{Marker}{Environment.NewLine}Run the serious delivery process for the units-converter app rooted at {DeliveryRootRelativePath}.",
                buildBlock.Id,
                "delivery-main-feature",
                cancellationToken);
        _ = FindNode(surface, ProjectObjectType.WorkItem, DomainFeatureTitle)
            ?? await CreateNodeAsync(
                projectId,
                ProjectObjectType.WorkItem,
                DomainFeatureTitle,
                "Feature / conversion categories and typed rules",
                $"{Marker}{Environment.NewLine}Cover at minimum length, mass, temperature, and volume with explicit unit options and predictable validation behavior.",
                buildBlock.Id,
                "delivery-domain-feature",
                cancellationToken);
        _ = FindNode(surface, ProjectObjectType.WorkItem, UiFeatureTitle)
            ?? await CreateNodeAsync(
                projectId,
                ProjectObjectType.WorkItem,
                UiFeatureTitle,
                "Feature / clean SSR experience",
                $"{Marker}{Environment.NewLine}Focus on intentional layout, readable form flow, clear conversion result presentation, and mobile-friendly screenshot proof.",
                buildBlock.Id,
                "delivery-ui-feature",
                cancellationToken);
        _ = FindNode(surface, ProjectObjectType.WorkItem, ReleaseFeatureTitle)
            ?? await CreateNodeAsync(
                projectId,
                ProjectObjectType.WorkItem,
                ReleaseFeatureTitle,
                "Feature / governed evidence and release confidence",
                $"{Marker}{Environment.NewLine}Capture QA, UI review, security, release approval, rollout, and learning as durable evidence under {DeliveryArtifactRootRelativePath}.",
                releaseBlock.Id,
                "delivery-release-feature",
                cancellationToken);

        await UpdateGraphProgressAsync(
            projectId,
            scopePhase.Id,
            buildPhase.Id,
            releasePhase.Id,
            deliveryFeature.Id,
            "Planned",
            0,
            cancellationToken);

        return new UnitsConverterProjectGraph(
            scopePhase.Id,
            buildPhase.Id,
            releasePhase.Id,
            deliveryFeature.Id);
    }

    private async Task<ProjectStructureNode> CreateNodeAsync(
        Guid projectId,
        ProjectObjectType objectType,
        string title,
        string subtitle,
        string notes,
        string? parentNodeKey,
        string objectSubtype,
        CancellationToken cancellationToken)
    {
        return await projectWorkbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                objectType,
                title,
                subtitle,
                notes,
                parentNodeKey,
                ObjectSubtype: objectSubtype),
            cancellationToken);
    }

    private async Task<Guid> EnsureProcessDefinitionAsync(
        Guid projectId,
        UnitsConverterWorkspacePlan workspacePlan,
        UnitsConverterSkillCatalog skillCatalog,
        CancellationToken cancellationToken)
    {
        var existingDefinition = (await processesService.ListDefinitionsAsync(projectId, cancellationToken))
            .FirstOrDefault(item => string.Equals(item.Name, DefinitionName, StringComparison.Ordinal));
        var definitionId = existingDefinition?.Id
            ?? EnsureSuccess(await processesService.ImportAsync(
                projectionService.GetProjectedEnvelope(ProcessTemplateKey, projectId, DefinitionName),
                cancellationToken));

        var editor = await processesService.GetEditorAsync(definitionId, projectId, cancellationToken);
        TailorDefinition(editor, workspacePlan, skillCatalog);
        definitionId = EnsureSuccess(await processesService.SaveAsync(editor, cancellationToken));
        EnsureSuccess(await processesService.PublishAsync(definitionId, cancellationToken));
        return definitionId;
    }

    private void TailorDefinition(ProcessDefinitionEditorModel editor, UnitsConverterWorkspacePlan workspacePlan, UnitsConverterSkillCatalog skillCatalog)
    {
        editor.Name = DefinitionName;
        editor.Summary = "Serious governed delivery of a Blazor SSR units-converter application with explicit human scope control, code review, UI review, QA, release, and learning evidence.";
        editor.ValueStatement = "Proves that AgentFramework-owned delivery agents and explicit human governance can deliver a maintainable units-converter app without hiding process gaps behind a second registry or hard-coded final app generation.";
        editor.CustomerName = "Internal product delivery";
        editor.OwnerName = "CanDoItAll agent integration wave";
        editor.InterfaceContractSummary = $"Deliver the solution at {SolutionRelativePath} and run the web application at {AppUrl}.";
        editor.GovernanceNotes = $"Marker: {Marker}. Human lanes remain explicitly human; all AI work is fulfilled through AgentFramework-owned agents projected via CRM-HR.";
        editor.ChangeSummary = "Tailored from the shared software-delivery template for the serious Blazor SSR units-converter scenario.";
        editor.GovernancePolicySummary = "No step may claim completion without durable evidence under the managed delivery artifact roots, and the final app must come from agent-authored code rather than a prewritten final-code script.";
        editor.ConstitutionRuleSummary = "Human scope control and release approval stay explicit, while technical implementation, review, QA, and rollout remain accountable to named AgentFramework agents.";
        editor.OperatingModeSummary = "Governed live execution with exact launch-plan candidate selection, human-controlled approvals, and Playwright-backed QA plus UI review.";
        editor.SimulationReadinessSummary = "This scenario is intended to expose real orchestration gaps across human and AI lanes while still producing a credible small application.";
        editor.Criticality = ProcessCriticality.High;
        editor.AutonomyLevel = ProcessAutonomyLevel.Guarded;

        EnsureRole(editor, BuildReviewLeadRole(skillCatalog));
        EnsureRole(editor, BuildUiReviewLeadRole(skillCatalog));
        ConfigureAiRole(editor, "solution-architect", skillCatalog.GetRequiredSkillIds("solution-architecture", "csharp-dotnet-delivery", "blazor-ssr-delivery"));
        ConfigureAiRole(editor, "lead-engineer", skillCatalog.GetRequiredSkillIds("csharp-dotnet-delivery", "blazor-ssr-delivery", "component-library-delivery"));
        ConfigureAiRole(editor, "review-lead", skillCatalog.GetRequiredSkillIds("code-review", "csharp-dotnet-delivery", "blazor-ssr-delivery"));
        ConfigureAiRole(editor, "qa-lead", skillCatalog.GetRequiredSkillIds("playwright-ui-qa", "blazor-ssr-delivery", "component-library-delivery"));
        ConfigureAiRole(editor, "ui-review-lead", skillCatalog.GetRequiredSkillIds("ui-composition-review", "playwright-ui-qa", "blazor-ssr-delivery", "component-library-delivery"));
        ConfigureAiRole(editor, "security-reviewer", skillCatalog.GetRequiredSkillIds("security-review", "csharp-dotnet-delivery"));
        ConfigureAiRole(editor, "release-manager", skillCatalog.GetRequiredSkillIds("release-governance", "playwright-ui-qa"));

        var peerReviewStep = editor.Steps.First(item => string.Equals(item.Key, "peer-review", StringComparison.OrdinalIgnoreCase));
        peerReviewStep.Title = "Complete governed code review";
        peerReviewStep.Subtitle = "Implementation challenge and maintainability review";
        peerReviewStep.RoleAssignments = BuildRoleAssignments(editor,
        [
            ("review-lead", ProcessResponsibilityKind.Responsible, true, 0, "Code-review ownership stays explicit and independent from implementation authorship."),
            ("lead-engineer", ProcessResponsibilityKind.Reviewer, true, 0, "Implementation owner responds to concrete review findings without silently redefining scope."),
            ("solution-architect", ProcessResponsibilityKind.Reviewer, false, 1, "Architecture review remains available when design or boundary drift appears during code review.")
        ]);

        var uiReviewTemplate = BuildUiReviewStep(editor);
        var existingUiReviewStep = editor.Steps.FirstOrDefault(item => string.Equals(item.Key, "ui-review", StringComparison.OrdinalIgnoreCase));
        if (existingUiReviewStep is null)
        {
            var releaseApprovalIndex = editor.Steps.FindIndex(item => string.Equals(item.Key, "release-approval", StringComparison.OrdinalIgnoreCase));
            editor.Steps.Insert(releaseApprovalIndex, uiReviewTemplate);
        }
        else
        {
            existingUiReviewStep.Title = uiReviewTemplate.Title;
            existingUiReviewStep.Subtitle = uiReviewTemplate.Subtitle;
            existingUiReviewStep.Notes = uiReviewTemplate.Notes;
            existingUiReviewStep.StepKind = uiReviewTemplate.StepKind;
            existingUiReviewStep.AllowsManualSkip = uiReviewTemplate.AllowsManualSkip;
            existingUiReviewStep.AllowsSafeRefusal = uiReviewTemplate.AllowsSafeRefusal;
            existingUiReviewStep.RequiresApproval = uiReviewTemplate.RequiresApproval;
            existingUiReviewStep.RequiresDecisionRecord = uiReviewTemplate.RequiresDecisionRecord;
            existingUiReviewStep.InputContractSummary = uiReviewTemplate.InputContractSummary;
            existingUiReviewStep.OutputContractSummary = uiReviewTemplate.OutputContractSummary;
            existingUiReviewStep.EvidenceContractSummary = uiReviewTemplate.EvidenceContractSummary;
            existingUiReviewStep.DecisionRightsSummary = uiReviewTemplate.DecisionRightsSummary;
            existingUiReviewStep.ExceptionPolicySummary = uiReviewTemplate.ExceptionPolicySummary;
            existingUiReviewStep.TargetLeadHours = uiReviewTemplate.TargetLeadHours;
            existingUiReviewStep.CanvasX = uiReviewTemplate.CanvasX;
            existingUiReviewStep.CanvasY = uiReviewTemplate.CanvasY;
            existingUiReviewStep.RoleAssignments = uiReviewTemplate.RoleAssignments;
            existingUiReviewStep.ArtifactExpectations = uiReviewTemplate.ArtifactExpectations;
        }

        foreach (var step in editor.Steps)
        {
            step.Notes = UpsertScenarioParagraph(
                NormalizeLegacyUnitsConverterText(step.Notes),
                step.Key,
                BuildStepNote(step.Key, workspacePlan));
            step.InputContractSummary = MergeSingleLine(
                StripSummaryClause(NormalizeLegacyUnitsConverterText(step.InputContractSummary), "Primary brief:"),
                $"Primary brief: {BriefRelativePath}.");
            step.OutputContractSummary = MergeSingleLine(
                StripSummaryClause(NormalizeLegacyUnitsConverterText(step.OutputContractSummary), "Workspace root:"),
                $"Workspace root: {DeliveryRootRelativePath}.");

            foreach (var artifact in step.ArtifactExpectations.Where(item => item.IsRequired))
            {
                artifact.ValidationRequirementSummary = NormalizeLegacyUnitsConverterText(artifact.ValidationRequirementSummary);
                var artifactRelativePath = BuildArtifactRelativePath(step.Key, artifact.Title);
                artifact.ValidationRequirementSummary = MergeSingleLine(
                    StripSummaryClause(artifact.ValidationRequirementSummary, "Create this artifact at "),
                    $"Create this artifact at {artifactRelativePath}.");
                artifact.ValidationRequirementSummary = MergeSingleLine(
                    artifact.ValidationRequirementSummary,
                    BuildArtifactValidationSummary(step.Key, artifact.Title));
            }
        }

        editor.Steps.First(item => string.Equals(item.Key, "qa-validation", StringComparison.OrdinalIgnoreCase)).Dependencies =
            BuildDependencies(editor, "implementation", "peer-review");
        editor.Steps.First(item => string.Equals(item.Key, "security-review", StringComparison.OrdinalIgnoreCase)).Dependencies =
            BuildDependencies(editor, "implementation", "peer-review");
        editor.Steps.First(item => string.Equals(item.Key, "ui-review", StringComparison.OrdinalIgnoreCase)).Dependencies =
            BuildDependencies(editor, "implementation", "peer-review", "qa-validation");
        editor.Steps.First(item => string.Equals(item.Key, "release-approval", StringComparison.OrdinalIgnoreCase)).Dependencies =
            BuildDependencies(editor, "implementation", "qa-validation", "ui-review", "security-review");
        editor.Steps.First(item => string.Equals(item.Key, "execute-release-rollout", StringComparison.OrdinalIgnoreCase)).Dependencies =
            BuildDependencies(editor, "release-approval");
        editor.Steps.First(item => string.Equals(item.Key, "post-release-learning", StringComparison.OrdinalIgnoreCase)).Dependencies =
            BuildDependencies(editor, "execute-release-rollout");

        EnsureArtifactTrustRequirement(editor, "architecture-review", "Architecture decision record", ProcessArtifactTrustRequirement.ReviewRequired);
        EnsureArtifactTrustRequirement(editor, "qa-validation", "Regression evidence pack", ProcessArtifactTrustRequirement.ReviewRequired);
        EnsureArtifactTrustRequirement(editor, "security-review", "Security exception assessment", ProcessArtifactTrustRequirement.ReviewRequired);
        EnsureArtifactTrustRequirement(editor, "release-approval", "Release approval record", ProcessArtifactTrustRequirement.HumanApproved);
    }

    private static void ConfigureAiRole(ProcessDefinitionEditorModel editor, string roleKey, IReadOnlyList<Guid> requiredSkillIds)
    {
        var role = editor.Roles.First(item => string.Equals(item.Key, roleKey, StringComparison.OrdinalIgnoreCase));
        role.PreferredExecutorKind = "AI agent";
        role.PreferredProjectAssignmentRole = ProjectPartyAssignmentRole.AiAgent;
        role.RequiredSkillIds = requiredSkillIds.Distinct().ToList();
    }

    private ProcessRoleEditorModel BuildReviewLeadRole(UnitsConverterSkillCatalog skillCatalog)
    {
        return new ProcessRoleEditorModel
        {
            Key = "review-lead",
            DisplayName = "Review lead",
            Purpose = "Own code-review findings, maintainability challenge, and explicit residual-risk framing.",
            StaffingIntent = "A senior code-review authority who can challenge implementation quality without collapsing responsibility into the author lane.",
            PreferredExecutorKind = "AI agent",
            PreferredProjectAssignmentRole = ProjectPartyAssignmentRole.AiAgent,
            IsRequired = true,
            AllowsFallback = true,
            RequiresExplicitApproval = true,
            DefaultAllocationPercent = 35,
            RoleTemplateSourceKey = "scenario-role/review-lead",
            RoleTemplateSnapshotName = "Review lead / units-converter scenario",
            SnapshotSummary = "Code-review authority for maintainability, correctness, and reviewable delivery proof.",
            RequiredSkillIds = skillCatalog.GetRequiredSkillIds("code-review", "csharp-dotnet-delivery", "blazor-ssr-delivery"),
            CanvasX = 1120,
            CanvasY = 40
        };
    }

    private ProcessRoleEditorModel BuildUiReviewLeadRole(UnitsConverterSkillCatalog skillCatalog)
    {
        return new ProcessRoleEditorModel
        {
            Key = "ui-review-lead",
            DisplayName = "UI review lead",
            Purpose = "Review visual quality, mobile friendliness, and screenshot-backed UX clarity before release approval.",
            StaffingIntent = "A design-sensitive reviewer able to challenge bland, confusing, or fragile UI outcomes using real browser evidence.",
            PreferredExecutorKind = "AI agent",
            PreferredProjectAssignmentRole = ProjectPartyAssignmentRole.AiAgent,
            IsRequired = true,
            AllowsFallback = true,
            RequiresExplicitApproval = false,
            DefaultAllocationPercent = 25,
            RoleTemplateSourceKey = "scenario-role/ui-review-lead",
            RoleTemplateSnapshotName = "UI review lead / units-converter scenario",
            SnapshotSummary = "Visual-review authority for screenshot-backed clarity, hierarchy, and layout quality.",
            RequiredSkillIds = skillCatalog.GetRequiredSkillIds("ui-composition-review", "playwright-ui-qa", "blazor-ssr-delivery", "component-library-delivery"),
            CanvasX = 1280,
            CanvasY = 40
        };
    }

    private ProcessStepEditorModel BuildUiReviewStep(ProcessDefinitionEditorModel editor)
    {
        return new ProcessStepEditorModel
        {
            Key = "ui-review",
            Title = "Review UI quality and screenshot evidence",
            Subtitle = "Visual clarity, mobile fit, and interaction polish",
            Notes = "Use the running application and Playwright evidence to challenge layout quality, readability, responsiveness, and whether the UI feels intentional rather than merely functional.",
            StepKind = ProcessStepKind.Review,
            AllowsManualSkip = false,
            AllowsSafeRefusal = false,
            RequiresApproval = false,
            RequiresDecisionRecord = true,
            InputContractSummary = "QA browser proof, running application path, and the implementation change set.",
            OutputContractSummary = "UI review note with explicit visual findings, screenshot references, and required fixes or acceptance rationale.",
            EvidenceContractSummary = "Desktop and mobile screenshots, screenshot analysis, and explicit residual UX risk.",
            DecisionRightsSummary = "UI review may block release readiness when the interface is confusing, visually weak, or not credibly mobile-friendly.",
            ExceptionPolicySummary = "Do not approve a visually weak result just because the functional flow works.",
            TargetLeadHours = 6,
            CanvasX = 1640,
            CanvasY = 120,
            RoleAssignments = BuildRoleAssignments(editor,
            [
                ("ui-review-lead", ProcessResponsibilityKind.Responsible, true, 0, "UI review ownership stays explicit and separate from feature implementation."),
                ("qa-lead", ProcessResponsibilityKind.Reviewer, true, 0, "QA remains present so functional proof and visual review stay aligned."),
                ("lead-engineer", ProcessResponsibilityKind.Reviewer, false, 1, "Implementation owner reviews concrete UI concerns before release approval.")
            ]),
            ArtifactExpectations =
            [
                new ProcessArtifactExpectationEditorModel
                {
                    ArtifactKind = ProcessArtifactKind.Evidence,
                    Title = "UI review note",
                    IsRequired = true,
                    TrustRequirement = ProcessArtifactTrustRequirement.ReviewRequired,
                    SensitivityLevel = ProcessSensitivityLevel.Internal,
                    RetentionDays = 365,
                    AllowedFutureUsageSummary = "Reusable for release approval, later UX cleanup, and future design baselines.",
                    ValidationRequirementSummary = "Must name screenshot-backed visual findings, mobile fit, and whether the UI reads as intentional."
                }
            ]
        };
    }

    private static void EnsureRole(ProcessDefinitionEditorModel editor, ProcessRoleEditorModel role)
    {
        var existingRole = editor.Roles.FirstOrDefault(item => string.Equals(item.Key, role.Key, StringComparison.OrdinalIgnoreCase));
        if (existingRole is not null)
        {
            existingRole.Id ??= role.Id ?? Guid.NewGuid();
            existingRole.DisplayName = role.DisplayName;
            existingRole.Purpose = role.Purpose;
            existingRole.StaffingIntent = role.StaffingIntent;
            existingRole.PreferredExecutorKind = role.PreferredExecutorKind;
            existingRole.PreferredProjectAssignmentRole = role.PreferredProjectAssignmentRole;
            existingRole.IsRequired = role.IsRequired;
            existingRole.AllowsFallback = role.AllowsFallback;
            existingRole.RequiresExplicitApproval = role.RequiresExplicitApproval;
            existingRole.DefaultAllocationPercent = role.DefaultAllocationPercent;
            existingRole.RoleTemplateSourceKey = role.RoleTemplateSourceKey;
            existingRole.RoleTemplateSnapshotName = role.RoleTemplateSnapshotName;
            existingRole.SnapshotSummary = role.SnapshotSummary;
            existingRole.RequiredSkillIds = role.RequiredSkillIds.Distinct().ToList();
            existingRole.CanvasX = role.CanvasX;
            existingRole.CanvasY = role.CanvasY;
            return;
        }

        role.Id ??= Guid.NewGuid();
        editor.Roles.Add(role);
    }

    private List<ProcessStepRoleRequirementEditorModel> BuildRoleAssignments(
        ProcessDefinitionEditorModel editor,
        IReadOnlyList<(string RoleKey, ProcessResponsibilityKind ResponsibilityKind, bool IsRequired, int FallbackOrder, string RebindPolicySummary)> assignments)
    {
        var roleIdsByKey = editor.Roles
            .ToDictionary(
                item => item.Key,
                item => item.Id.HasValue && item.Id.Value != Guid.Empty
                    ? item.Id.Value
                    : throw new InvalidOperationException($"Process role '{item.Key}' is missing a stable identifier."),
                StringComparer.OrdinalIgnoreCase);
        return assignments
            .Select(item =>
            {
                if (!roleIdsByKey.TryGetValue(item.RoleKey, out var roleId))
                {
                    throw new InvalidOperationException($"Process role '{item.RoleKey}' is not available for step assignment.");
                }

                return new ProcessStepRoleRequirementEditorModel
                {
                    RoleRequirementId = roleId,
                    ResponsibilityKind = item.ResponsibilityKind,
                    IsRequired = item.IsRequired,
                    FallbackOrder = item.FallbackOrder,
                    RebindPolicySummary = item.RebindPolicySummary
                };
            })
            .ToList();
    }

    private List<ProcessStepDependencyEditorModel> BuildDependencies(
        ProcessDefinitionEditorModel editor,
        params string[] dependencyStepKeys)
    {
        var stepIdByKey = editor.Steps
            .Where(item => item.Id.HasValue)
            .ToDictionary(item => item.Key, item => item.Id!.Value, StringComparer.OrdinalIgnoreCase);
        return dependencyStepKeys
            .Where(stepIdByKey.ContainsKey)
            .Select(key => new ProcessStepDependencyEditorModel
            {
                DependsOnStepId = stepIdByKey[key]
            })
            .ToList();
    }

    private static void EnsureArtifactTrustRequirement(
        ProcessDefinitionEditorModel editor,
        string stepKey,
        string artifactTitle,
        ProcessArtifactTrustRequirement trustRequirement)
    {
        var step = editor.Steps.FirstOrDefault(item => string.Equals(item.Key, stepKey, StringComparison.OrdinalIgnoreCase));
        if (step is null)
        {
            return;
        }

        var artifact = step.ArtifactExpectations.FirstOrDefault(item => string.Equals(item.Title, artifactTitle, StringComparison.OrdinalIgnoreCase));
        if (artifact is null)
        {
            return;
        }

        artifact.TrustRequirement = trustRequirement;
    }

    private string BuildStepNote(string stepKey, UnitsConverterWorkspacePlan workspacePlan)
    {
        return stepKey switch
        {
            "feature-intake" => $"Human-controlled scope step. Read {BriefRelativePath}, confirm the conversion categories, the maintainable solution split (Core + Web + Tests), and the explicit exclusions. Record the scope packet at {BuildArtifactRelativePath(stepKey, "Scope boundary packet")} before completing the step. Do not run {BootstrapScriptRelativePath}, {LaunchScriptRelativePath}, {ImportPlaywrightEvidenceScriptRelativePath}, or browser tools in this step.",
            "architecture-review" => $"Treat {SolutionRelativePath}, {CoreProjectRelativePath}, {WebProjectRelativePath}, and {TestsProjectRelativePath} as planned implementation targets, not as already-existing deliverables for this step. Use the scope packet and {BriefRelativePath} to choose the layered solution shape, keep the typed conversion domain in Core, keep the SSR interaction flow in Web, and reject unnecessary abstractions or a duplicate agent registry. Before concluding, create the architecture decision record at {BuildArtifactRelativePath(stepKey, "Architecture decision record")} and make it explicit about the selected option, rejected options, source-of-truth ownership, and migration ownership. Do not run {BootstrapScriptRelativePath}, {LaunchScriptRelativePath}, {ImportPlaywrightEvidenceScriptRelativePath}, or browser tools in this step.",
            "implementation" => $"Use workspace_pwsh_run_script to run {BootstrapScriptRelativePath} only as a blank-solution bootstrap. Do not use any helper that writes the final application for you. If {SolutionRelativePath}, {CoreProjectRelativePath}, {WebProjectRelativePath}, or {TestsProjectRelativePath} does not exist yet, that is expected pre-bootstrap state for this step, not a blocker. Run the bootstrap script first, then inspect the scaffolded files and continue. After bootstrap, read the real scaffolded files at {SolutionRelativePath}, {CoreProjectRelativePath}, {WebProjectRelativePath}, {WebProgramRelativePath}, {WebHomeRelativePath}, {WebMainLayoutRelativePath}, {WebNavMenuRelativePath}, {WebCssRelativePath}, and {TestsProjectRelativePath} before substantial edits. Preserve the generated .NET 10 Blazor SSR and MSTest structure unless the approved scope explicitly requires another target, remove leftover template files instead of mixing frameworks, and keep on-disk solution, project, and folder names short enough that build and test paths stay reliable inside the managed workspace root. Respect the actual MSTest APIs exposed by the generated test project. If the scaffold references the modern `MSTest` package, use assertions such as `Assert.Throws<T>` or `Assert.ThrowsExactly<T>` and do not introduce legacy `[ExpectedException]` or `Assert.ThrowsException(...)` patterns. Do not create Razor component names that collide with existing domain converter types or enums; if the domain already defines names such as `LengthConverter` or `LengthUnit`, rename the UI surface or qualify the domain type explicitly so the final code stays unambiguous and buildable. Do not use `object`, `dynamic`, or other weakly typed bind targets for Blazor form state; use concrete view-model properties or enums so the final route cannot fail with TypeConverter or ambiguous binding errors at runtime. Then use workspace_write_file to implement the real solution across {CoreProjectRelativePath}, {WebProjectRelativePath}, and {TestsProjectRelativePath}; it can create the required child paths, so do not stop just because a source folder does not exist yet after bootstrap. The final app must support length, mass, temperature, and volume conversions, use maintainable typed conversion logic, provide a clear SSR experience, replace the stock Counter and Weather scaffold, replace the default `MainLayout`, `NavMenu`, and docs-oriented scaffold shell, and ensure the primary `/` route, navigation, and page composition read like the real units-converter product instead of placeholder link lists or default Bootstrap-looking stacked sections. A mostly unchanged scaffold `app.css` is not acceptable. Before build validation, run {StopScriptRelativePath} through workspace_pwsh_run_script to clear any prior units-converter runtime host from earlier attempts; if it reports that the app is not running, continue. Validate with workspace_dotnet_build on {SolutionRelativePath} and workspace_dotnet_test on {TestsProjectRelativePath}. If either validation fails, inspect the real diagnostics, fix the code, and rerun the same build and test until both pass or you have an explicit blocker that you can name concretely. Then run {LaunchScriptRelativePath} through workspace_pwsh_run_script and prove the app becomes reachable at {AppUrl}; if startup or render fails, fix the implementation and rerun the same runtime smoke before QA ever starts. After launch succeeds, use browser_resize, browser_navigate, browser_take_screenshot, browser_snapshot, and browser_console_messages on the live app at {AppUrl}. Capture the implementation proof screenshot at '{BuildPlaywrightScratchCaptureRelativePath(stepKey, "desktop-home.png")}', capture the page snapshot at '{BuildPlaywrightScratchCaptureRelativePath(stepKey, "page.yml")}', and capture console output at '{BuildPlaywrightScratchCaptureRelativePath(stepKey, "console.log")}'. These paths are workspace-relative and already point into the prepared Playwright scratch root; do not shorten them to bare '{stepKey}/...' filenames. Treat any primary route that still renders mostly default scaffold content, placeholder navigation, stock docs-oriented chrome, or a bare heading-plus-links shell as implementation failure even if build and test pass. Review the screenshot yourself before concluding. Then run {ImportPlaywrightEvidenceScriptRelativePath} with step key '{stepKey}' through workspace_pwsh_run_script and pass outputPaths [{BuildUiImportOutputPathArgument(stepKey)}] so the implementation screenshot, page snapshot, console log, and import summary are registered as durable artifacts under {BuildScopedUiEvidenceRelativePath()}/{stepKey}. After the import succeeds, run {StopScriptRelativePath} through workspace_pwsh_run_script so QA and rerun attempts start from a clean runtime state. Before concluding, write the implementation change summary to {BuildArtifactRelativePath(stepKey, "Implementation change set")} and the migration and rollout preparation checklist to {BuildArtifactRelativePath(stepKey, "Migration and rollout preparation checklist")}.",
            "peer-review" => $"Act as a real code-review lane. Challenge maintainability, boundary clarity, test credibility, template residue, and overcomplication. Review the actual solution at {SolutionRelativePath}, the implementation evidence under {ProcessEvidenceRelativePath}/implementation, and the architecture decision record. Treat leftover Counter or Weather scaffold routes, stock Blazor navigation, or placeholder home-page copy on the primary `/` route as material findings, not as acceptable MVP residue. In this generated delivery workspace, do not assume git metadata exists and do not fail the review just because workspace_git_diff or workspace_git_status is unavailable; read the real files and durable evidence instead. Before concluding, write the peer review note to {BuildArtifactRelativePath(stepKey, "Peer review note")}.",
            "qa-validation" => $"Use workspace_pwsh_run_script to run {StopScriptRelativePath} first so stale runtime state from earlier steps or reruns does not contaminate QA. Then use workspace_pwsh_run_script to launch {LaunchScriptRelativePath}. Then use browser_resize, browser_navigate, browser_click, browser_fill_form or browser_type, browser_select_option when applicable, browser_take_screenshot, browser_snapshot, and browser_console_messages to verify desktop behavior at {AppUrl}. Start on the primary `/` route, inspect the actual surfaced navigation and conversion controls, and derive the tested flow from the live application instead of assuming legacy scenario routes. You must exercise the live UI, not just inspect it: interact with the real controls until you prove at least two representative conversion categories from the approved brief and record the exact routes, selected categories, and entered values in the QA note. Treat a primary route that is only a heading, a short sentence, and bare navigation links, or a screen that still looks like untouched default scaffold styling, as a QA failure even if conversions technically work. Capture the initial product surface at '{BuildPlaywrightScratchCaptureRelativePath(stepKey, "desktop-home.png")}' and capture one successful representative conversion state at '{BuildPlaywrightScratchCaptureRelativePath(stepKey, "desktop-representative-conversion.png")}', capture the page snapshot at '{BuildPlaywrightScratchCaptureRelativePath(stepKey, "page.yml")}', and capture console output at '{BuildPlaywrightScratchCaptureRelativePath(stepKey, "console.log")}'. These paths are workspace-relative and already point into the prepared Playwright scratch root; do not shorten them to bare '{stepKey}/...' filenames. The representative conversion screenshot must visibly show the entered value, the selected source and target units, and the rendered result, not only an empty or partially filled form. Review the screenshots for intentional desktop layout, credible product hierarchy, and clear results instead of treating capture alone as proof. If launch, browser checks, import, or screenshot review fails, name the concrete defect and keep the step failed instead of waving it through with chat-only confidence. Then run {ImportPlaywrightEvidenceScriptRelativePath} with step key '{stepKey}' through workspace_pwsh_run_script and pass outputPaths [{BuildUiImportOutputPathArgument(stepKey)}] so the screenshots, snapshot, console log, and import summary are registered as durable artifacts under {BuildScopedUiEvidenceRelativePath()}/{stepKey}. After the import succeeds, run {StopScriptRelativePath} through workspace_pwsh_run_script so later review and rollout steps restart the app from a clean state. Before concluding, call workspace_stat_path on '{BuildScopedUiEvidenceRelativePath()}/{stepKey}/desktop-home.png', '{BuildScopedUiEvidenceRelativePath()}/{stepKey}/desktop-representative-conversion.png', '{BuildScopedUiEvidenceRelativePath()}/{stepKey}/page.yml', '{BuildScopedUiEvidenceRelativePath()}/{stepKey}/console.log', '{BuildScopedUiEvidenceRelativePath()}/{stepKey}/import-summary.json', and '{BuildArtifactRelativePath(stepKey, "Regression evidence pack")}'. Also call workspace_read_file on '{BuildScopedUiEvidenceRelativePath()}/{stepKey}/page.yml', '{BuildScopedUiEvidenceRelativePath()}/{stepKey}/console.log', '{BuildScopedUiEvidenceRelativePath()}/{stepKey}/import-summary.json', and '{BuildArtifactRelativePath(stepKey, "Regression evidence pack")}' before you conclude. Before concluding, write the regression evidence pack to {BuildArtifactRelativePath(stepKey, "Regression evidence pack")} and reference the exact browser proof you captured.",
            "ui-review" => $"Use workspace_pwsh_run_script to run {StopScriptRelativePath} first, then launch {LaunchScriptRelativePath}, so the visual review always starts from a clean runtime state at {AppUrl}. Then use browser_resize, browser_navigate, browser_click, browser_fill_form or browser_type, browser_select_option when applicable, browser_take_screenshot, browser_snapshot, and browser_console_messages to review both desktop and mobile states on the actual current product surface. Do not assume route names from an older run when the live app exposes a different navigation shape. Interact with the live conversion surface before judging it, and fail the review if the primary `/` route is still just a heading plus simple links, if the page still reads like default scaffold output, if default sidebar or documentation chrome is still visible, or if the visual hierarchy feels unfinished even when the functional flow works. Capture screenshots at '{BuildPlaywrightScratchCaptureRelativePath(stepKey, "desktop-home.png")}' and '{BuildPlaywrightScratchCaptureRelativePath(stepKey, "mobile-home.png")}', capture the page snapshot at '{BuildPlaywrightScratchCaptureRelativePath(stepKey, "page.yml")}', and capture console output at '{BuildPlaywrightScratchCaptureRelativePath(stepKey, "console.log")}'. These paths are workspace-relative and already point into the prepared Playwright scratch root; do not shorten them to bare '{stepKey}/...' filenames. At least one reviewed screenshot must visibly show the app in a meaningful interaction state rather than an untouched landing surface. Then run {ImportPlaywrightEvidenceScriptRelativePath} with step key '{stepKey}' through workspace_pwsh_run_script and pass outputPaths [{BuildUiImportOutputPathArgument(stepKey)}] so the visual-review evidence is registered as durable artifacts under {BuildScopedUiEvidenceRelativePath()}/{stepKey}. After the import succeeds, run {StopScriptRelativePath} through workspace_pwsh_run_script so release readiness and reruns do not inherit this live session. Judge hierarchy, spacing, copy clarity, whether the app looks intentionally designed, and whether the captured screenshots would be acceptable to ship. Before concluding, call workspace_stat_path on '{BuildScopedUiEvidenceRelativePath()}/{stepKey}/desktop-home.png', '{BuildScopedUiEvidenceRelativePath()}/{stepKey}/mobile-home.png', '{BuildScopedUiEvidenceRelativePath()}/{stepKey}/page.yml', '{BuildScopedUiEvidenceRelativePath()}/{stepKey}/console.log', '{BuildScopedUiEvidenceRelativePath()}/{stepKey}/import-summary.json', and '{BuildArtifactRelativePath(stepKey, "UI review note")}'. Also call workspace_read_file on '{BuildScopedUiEvidenceRelativePath()}/{stepKey}/page.yml', '{BuildScopedUiEvidenceRelativePath()}/{stepKey}/console.log', '{BuildScopedUiEvidenceRelativePath()}/{stepKey}/import-summary.json', and '{BuildArtifactRelativePath(stepKey, "UI review note")}' before you conclude. Before concluding, write the UI review note to {BuildArtifactRelativePath(stepKey, "UI review note")}.",
            "security-review" => $"Review predictable parsing, invalid-input handling, numeric edge cases, and whether the solution exposes secrets, unsafe scripts, or fragile assumptions. Use the real files, not only prior summaries. Before concluding, write the security exception assessment to {BuildArtifactRelativePath(stepKey, "Security exception assessment")}.",
            "release-approval" => $"Human-controlled release decision. Review the code-review, QA, UI-review, and security evidence before approving. Do not approve if the app is only functionally correct but visually weak or if durable artifact handoff is incomplete. Record the release approval record at {BuildArtifactRelativePath(stepKey, "Release approval record")} before completing the step.",
            "execute-release-rollout" => $"Use workspace_pwsh_run_script to run {StopScriptRelativePath} first, then launch {LaunchScriptRelativePath}, so the final rollout smoke starts from a clean runtime state. Then use browser_resize, browser_navigate, browser_take_screenshot, browser_snapshot, and browser_console_messages for a final release smoke check at {AppUrl}. Smoke the actual primary product surface instead of assuming legacy category routes, and confirm the surface that will be shipped is reachable and credible. Capture the final release screenshot at '{BuildPlaywrightScratchCaptureRelativePath(stepKey, "release-home.png")}', capture the page snapshot at '{BuildPlaywrightScratchCaptureRelativePath(stepKey, "page.yml")}', and capture console output at '{BuildPlaywrightScratchCaptureRelativePath(stepKey, "console.log")}'. These paths are workspace-relative and already point into the prepared Playwright scratch root; do not shorten them to bare '{stepKey}/...' filenames. Then run {ImportPlaywrightEvidenceScriptRelativePath} with step key '{stepKey}' through workspace_pwsh_run_script and pass outputPaths [{BuildUiImportOutputPathArgument(stepKey)}] so the rollout evidence is registered as durable artifacts under {BuildScopedUiEvidenceRelativePath()}/{stepKey}. After the import succeeds, run {StopScriptRelativePath} through workspace_pwsh_run_script so post-release learning and future reruns do not inherit the live rollout session. Before concluding, call workspace_stat_path on '{BuildScopedUiEvidenceRelativePath()}/{stepKey}/release-home.png', '{BuildScopedUiEvidenceRelativePath()}/{stepKey}/page.yml', '{BuildScopedUiEvidenceRelativePath()}/{stepKey}/console.log', '{BuildScopedUiEvidenceRelativePath()}/{stepKey}/import-summary.json', and '{BuildArtifactRelativePath(stepKey, "Deployment and telemetry watch log")}'. Also call workspace_read_file on '{BuildScopedUiEvidenceRelativePath()}/{stepKey}/page.yml', '{BuildScopedUiEvidenceRelativePath()}/{stepKey}/console.log', '{BuildScopedUiEvidenceRelativePath()}/{stepKey}/import-summary.json', and '{BuildArtifactRelativePath(stepKey, "Deployment and telemetry watch log")}' before you conclude. Before concluding, write the deployment and telemetry watch log to {BuildArtifactRelativePath(stepKey, "Deployment and telemetry watch log")} with the real rollout outcome.",
            "post-release-learning" => $"Human-controlled learning step. Record architecture, process, artifact-flow, and UX observations exposed by the real run. The review must name weak spots concretely enough to drive follow-up repairs. Record the post-release learning review at {BuildArtifactRelativePath(stepKey, "Post-release learning review")} before completing the step.",
            _ => $"Operate only inside {DeliveryRootRelativePath} and keep durable evidence under {DeliveryArtifactRootRelativePath}."
        };
    }

    private string BuildArtifactValidationSummary(string stepKey, string artifactTitle)
    {
        return stepKey switch
        {
            "feature-intake" => "Must capture the approved scope, included conversion categories, exclusions, and the human acceptance boundary.",
            "architecture-review" => "Must justify the layered solution shape, typed conversion boundary, and why the app remains static SSR.",
            "implementation" => "Must reference the real files changed, build/test results, and any remaining implementation risk.",
            "peer-review" => "Must name accepted issues, rejected concerns, and explicit residual code risk.",
            "qa-validation" => "Must reference the real browser proof, functional checks, screenshots, and unresolved QA concerns.",
            "ui-review" => "Must reference screenshot-backed visual findings and whether the UI is credible on desktop and mobile.",
            "security-review" => "Must name the reviewed trust boundaries, edge cases, and residual security posture.",
            "release-approval" => "Must name the approver, release condition, residual risk owner, and the rationale for go or no-go.",
            "execute-release-rollout" => "Must capture runtime outcome, smoke-check evidence, and any halt or follow-up decision.",
            "post-release-learning" => "Must capture process and architecture lessons, not only application bugs.",
            _ => $"Must be explicit enough to support replay of '{artifactTitle}'."
        };
    }

    private string BuildArtifactRelativePath(string stepKey, string artifactTitle)
    {
        return $"{ProcessEvidenceRelativePath}/{stepKey}/{FileSafeSlugBuilder.Build(artifactTitle)}.md";
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

    private async Task<UnitsConverterLaunchResult> CreateLaunchAndRunAsync(
        Guid projectId,
        Guid definitionId,
        IReadOnlyDictionary<string, DeliveryRoleBinding> bindingsByRoleKey,
        UnitsConverterProjectGraph graph,
        UnitsConverterWorkspacePlan workspacePlan,
        CancellationToken cancellationToken)
    {
        await SupersedeActiveScenarioRunsAsync(projectId, definitionId, cancellationToken);

        var launchPlanId = EnsureSuccess(await processesService.CreateLaunchPlanAsync(
            new ProcessLaunchCreateRequest
            {
                ProcessDefinitionId = definitionId,
                ProjectId = projectId,
                LaunchName = $"{ProjectName} / {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss}",
                OperatingMode = ProcessOperatingMode.GovernedLive,
                TriggerReason = BuildLaunchTriggerReason(workspacePlan),
                RequestedBy = "units-converter-delivery-seeder"
            },
            cancellationToken));
        var launchPlan = await processesService.GetLaunchPlanAsync(launchPlanId, cancellationToken)
            ?? throw new InvalidOperationException($"Launch plan '{launchPlanId}' was not found after creation.");

        foreach (var role in launchPlan.Roles)
        {
            if (!bindingsByRoleKey.TryGetValue(role.RoleKey, out var binding))
            {
                throw new InvalidOperationException($"No delivery binding exists for role '{role.RoleKey}'.");
            }

            var candidate = role.Candidates.FirstOrDefault(item =>
                item.PartyId == binding.PartyId ||
                (binding.TechnicalAgentId.HasValue && item.TechnicalAgentId == binding.TechnicalAgentId.Value));
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
            "units-converter-delivery-seeder",
            cancellationToken));
        EnsureSuccess(await processesService.DecideLaunchPlanApprovalAsync(
            new ProcessLaunchApprovalDecisionRequest
            {
                LaunchPlanId = launchPlanId,
                Status = ProcessLaunchApprovalStatus.Approved,
                ResolutionSummary = "Scenario seeder approved the serious units-converter launch after resolving exact human and AgentFramework-owned delivery candidates.",
                DecidedBy = "units-converter-delivery-seeder"
            },
            cancellationToken));

        launchPlan = await processesService.GetLaunchPlanAsync(launchPlanId, cancellationToken)
            ?? throw new InvalidOperationException($"Launch plan '{launchPlanId}' disappeared after approval.");
        if (launchPlan.Status == ProcessLaunchPlanStatus.Provisioning)
        {
            EnsureSuccess(await processesService.ProvisionLaunchPlanAsync(
                launchPlanId,
                "units-converter-delivery-seeder",
                cancellationToken));
            launchPlan = await processesService.GetLaunchPlanAsync(launchPlanId, cancellationToken)
                ?? throw new InvalidOperationException($"Launch plan '{launchPlanId}' disappeared after provisioning.");
        }

        if (launchPlan.Status is not ProcessLaunchPlanStatus.Ready and not ProcessLaunchPlanStatus.Approved)
        {
            throw new InvalidOperationException($"Launch plan '{launchPlanId}' is not executable. Status={launchPlan.Status}.");
        }

        var runId = EnsureSuccess(await processesService.ExecuteLaunchPlanAsync(
            new ProcessLaunchExecutionRequest
            {
                LaunchPlanId = launchPlanId,
                RequestedBy = "units-converter-delivery-seeder"
            },
            cancellationToken));
        await UpsertProcessBindingAsync(projectId, graph.DeliveryFeatureNodeId, definitionId, runId, cancellationToken);
        await UpdateGraphProgressAsync(
            projectId,
            graph.ScopePhaseNodeId,
            graph.BuildPhaseNodeId,
            graph.ReleasePhaseNodeId,
            graph.DeliveryFeatureNodeId,
            ResolveNodeStatus(ProcessRunStatus.Active),
            10,
            cancellationToken);
        return new UnitsConverterLaunchResult(launchPlanId, runId);
    }

    private async Task SupersedeActiveScenarioRunsAsync(
        Guid projectId,
        Guid definitionId,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var staleRuns = await dbContext.Set<ProcessRun>()
            .Where(item =>
                item.ProjectId == projectId &&
                item.ProcessDefinitionId == definitionId &&
                item.Status == ProcessRunStatus.Active)
            .ToListAsync(cancellationToken);
        if (staleRuns.Count == 0)
        {
            return;
        }

        var staleRunIds = staleRuns
            .Select(item => item.Id)
            .ToList();
        var staleStepRuns = await dbContext.Set<ProcessStepRun>()
            .Where(item => staleRunIds.Contains(item.ProcessRunId))
            .ToListAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;

        foreach (var stepRun in staleStepRuns.Where(item => item.Status is ProcessStepRunStatus.Pending or ProcessStepRunStatus.Ready or ProcessStepRunStatus.InProgress or ProcessStepRunStatus.WaitingApproval or ProcessStepRunStatus.Blocked))
        {
            stepRun.Status = ProcessStepRunStatus.Skipped;
            stepRun.DecisionSummary = "Superseded by a newer units-converter scenario rerun.";
            stepRun.BlockedReason = string.Empty;
            stepRun.RefusalReason = string.Empty;
            stepRun.ExceptionSummary = string.Empty;
            stepRun.CompletedAtUtc ??= now;
            stepRun.ConcurrencyToken = Guid.NewGuid();
        }

        foreach (var run in staleRuns)
        {
            run.Status = ProcessRunStatus.Cancelled;
            run.CompletedAtUtc ??= now;
            run.UpdatedAtUtc = now;
            run.ConcurrencyToken = Guid.NewGuid();

            await dbContext.Set<ProcessJournalEntry>().AddAsync(
                new ProcessJournalEntry
                {
                    ProcessRunId = run.Id,
                    EventType = "scenario-superseded",
                    Title = "Scenario run superseded",
                    Description = "A newer serious units-converter scenario rerun superseded this still-active attempt before launch.",
                    CorrelationId = $"units-converter-rerun:{run.Id:D}",
                    OperatingMode = run.OperatingMode,
                    PolicyVersion = $"definition-version:{run.ProcessDefinitionVersionId:D}",
                    EnvironmentMode = "scenario-seeder",
                    ReplayContextJson = "{}",
                    OccurredAtUtc = now
                },
                cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private string BuildLaunchTriggerReason(UnitsConverterWorkspacePlan workspacePlan)
    {
        return string.Join(
            Environment.NewLine,
            [
                $"Deliver the serious Blazor SSR units-converter project '{ProjectName}' end to end.",
                $"Workspace brief: {BriefRelativePath}",
                $"Solution path: {SolutionRelativePath}",
                $"Primary web project: {WebProjectRelativePath}",
                $"Primary core project: {CoreProjectRelativePath}",
                $"Primary tests project: {TestsProjectRelativePath}",
                $"Bootstrap script: {BootstrapScriptRelativePath}",
                $"Launch script: {LaunchScriptRelativePath}",
                $"Managed process evidence root: {ProcessEvidenceRelativePath}",
                $"Managed UI evidence root: {BuildScopedUiEvidenceRelativePath()}",
                $"UI evidence filesystem root: {workspacePlan.UiEvidenceFullPath}",
                "Human lanes: feature-intake, release-approval, and post-release-learning remain explicitly human-controlled.",
                "AI lanes must be fulfilled only by AgentFramework-owned agents projected through CRM-HR, with no duplicate agent registry.",
                "Acceptance: deliver a maintainable Blazor SSR units-converter application that covers length, mass, temperature, and volume, includes tests, survives code review and security review, passes Playwright-backed QA plus UI review, and records durable output folders in project structure."
            ]);
    }

    private static ProjectStructureNode? FindNode(
        ProjectStructureSurface surface,
        ProjectObjectType objectType,
        string title)
    {
        return surface.Nodes.FirstOrDefault(item =>
                   item.ObjectType == objectType &&
                   !string.IsNullOrWhiteSpace(item.Notes) &&
                   ContainsScenarioMarker(item.Notes) &&
                   string.Equals(item.Title, title, StringComparison.Ordinal))
               ?? surface.Nodes.FirstOrDefault(item =>
                   item.ObjectType == objectType &&
                   string.Equals(item.Title, title, StringComparison.Ordinal));
    }

    private async Task UpdateGraphProgressAsync(
        Guid projectId,
        string scopePhaseNodeId,
        string buildPhaseNodeId,
        string releasePhaseNodeId,
        string deliveryFeatureNodeId,
        string status,
        int progressPercent,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var nodeKeys = new HashSet<string>(StringComparer.Ordinal)
        {
            scopePhaseNodeId,
            buildPhaseNodeId,
            releasePhaseNodeId,
            deliveryFeatureNodeId
        };
        var nodes = await dbContext.Set<ProjectObjectRecord>()
            .Where(item => item.ProjectId == projectId && nodeKeys.Contains(item.NodeKey))
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

    private static string StripSummaryClause(string text, string clausePrefix)
    {
        if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(clausePrefix))
        {
            return text?.Trim() ?? string.Empty;
        }

        var index = text.IndexOf(clausePrefix, StringComparison.OrdinalIgnoreCase);
        return index < 0
            ? text.Trim()
            : text[..index].Trim();
    }

    private static string NormalizeLegacyUnitsConverterText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return text?.Trim() ?? string.Empty;
        }

        var normalized = text;
        foreach (var replacement in BuildLegacyUnitsConverterReplacements())
        {
            normalized = normalized.Replace(
                replacement.LegacyValue,
                replacement.CurrentValue,
                StringComparison.OrdinalIgnoreCase);
        }

        return normalized.Trim();
    }

    private static IReadOnlyList<(string LegacyValue, string CurrentValue)> BuildLegacyUnitsConverterReplacements()
    {
        return
        [
            (LegacyMarker, Marker),
            (LegacySolutionRelativePath, SolutionRelativePath),
            (LegacyPreSlnxSolutionRelativePath, SolutionRelativePath),
            (LegacyCoreProjectRelativePath, CoreProjectRelativePath),
            (LegacyWebProjectRelativePath, WebProjectRelativePath),
            (LegacyTestsProjectRelativePath, TestsProjectRelativePath),
            (LegacyWebProgramRelativePath, WebProgramRelativePath),
            (LegacyWebHomeRelativePath, WebHomeRelativePath),
            (LegacyBootstrapScriptRelativePath, BootstrapScriptRelativePath),
            (LegacyLaunchScriptRelativePath, LaunchScriptRelativePath),
            (LegacyStopScriptRelativePath, StopScriptRelativePath),
            (LegacyImportPlaywrightEvidenceScriptRelativePath, ImportPlaywrightEvidenceScriptRelativePath),
            (LegacyPlaywrightScratchRelativePath, PlaywrightScratchRelativePath),
            (LegacyDeliveryArtifactRootRelativePath, DeliveryArtifactRootRelativePath),
            (LegacyDeliveryRootRelativePath, DeliveryRootRelativePath)
        ];
    }

    private static string UpsertScenarioParagraph(string existing, string stepKey, string addition)
    {
        if (string.IsNullOrWhiteSpace(addition))
        {
            return existing?.Trim() ?? string.Empty;
        }

        var paragraphs = SplitParagraphs(existing)
            .Where(paragraph => !IsScenarioParagraph(paragraph, stepKey))
            .ToList();
        paragraphs.Add(addition.Trim());
        return string.Join($"{Environment.NewLine}{Environment.NewLine}", paragraphs);
    }

    private static bool IsScenarioParagraph(string paragraph, string stepKey)
    {
        var trimmedParagraph = paragraph.Trim();
        if (ContainsScenarioMarker(trimmedParagraph))
        {
            return true;
        }

        return GetScenarioParagraphPrefixes(stepKey)
            .Any(prefix =>
                !string.IsNullOrWhiteSpace(prefix) &&
                trimmedParagraph.StartsWith(prefix, StringComparison.Ordinal));
    }

    private static bool ContainsScenarioMarker(string text)
    {
        return text.Contains(Marker, StringComparison.Ordinal) ||
               text.Contains(LegacyMarker, StringComparison.Ordinal);
    }

    private static IReadOnlyList<string> GetScenarioParagraphPrefixes(string stepKey)
    {
        return stepKey switch
        {
            "feature-intake" =>
            [
                "Human-controlled scope step."
            ],
            "architecture-review" =>
            [
                "Treat "
            ],
            "implementation" =>
            [
                "Use workspace_pwsh_run_script to run ",
                "Treat missing solution or project files as expected pre-bootstrap state for this step, not a blocker.",
                "The final app must support length, mass, temperature, and volume conversions",
                "Do not use `object`, `dynamic`, or other weakly typed bind targets for Blazor form state.",
                "Then run ",
                "If startup or render fails, fix the implementation and rerun the same runtime smoke before QA begins."
            ],
            "peer-review" =>
            [
                "Act as a real code-review lane."
            ],
            "qa-validation" =>
            [
                "Use workspace_pwsh_run_script to launch ",
                "Then use browser_resize, browser_navigate, browser_click, browser_fill_form or browser_type, browser_select_option when applicable, browser_take_screenshot, browser_snapshot, and browser_console_messages",
                "Treat a primary route that is only a heading, a short sentence, and bare navigation links, or a screen that still looks like untouched default scaffold styling, as a QA failure"
            ],
            "ui-review" =>
            [
                $"Use workspace_pwsh_run_script to ensure the app is available at {AppUrl}.",
                "Then use browser_resize, browser_navigate, browser_click, browser_fill_form or browser_type, browser_select_option when applicable, browser_take_screenshot, browser_snapshot, and browser_console_messages",
                "Fail the review if the primary `/` route is still just a heading plus simple links, if the page still reads like default scaffold output, or if the visual hierarchy feels unfinished"
            ],
            "security-review" =>
            [
                "Review predictable parsing"
            ],
            "release-approval" =>
            [
                "Human-controlled release decision."
            ],
            "execute-release-rollout" =>
            [
                $"Use workspace_pwsh_run_script to launch {LaunchScriptRelativePath}. Then use browser_resize, browser_navigate, browser_take_screenshot"
            ],
            "post-release-learning" =>
            [
                "Human-controlled learning step."
            ],
            _ =>
            [
                $"Operate only inside {DeliveryRootRelativePath}"
            ]
        };
    }

    private static IReadOnlyList<string> SplitParagraphs(string text)
    {
        return string.IsNullOrWhiteSpace(text)
            ? []
            : text
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();
    }

    private sealed record UnitsConverterProjectGraph(
        string ScopePhaseNodeId,
        string BuildPhaseNodeId,
        string ReleasePhaseNodeId,
        string DeliveryFeatureNodeId);

    private sealed record UnitsConverterLaunchResult(
        Guid LaunchPlanId,
        Guid RunId);
}
