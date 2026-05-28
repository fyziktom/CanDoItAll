using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.AI;

namespace CanDoItAll.AgentFramework.Maf;

public sealed partial class MafAgentRuntime
{
    private ProcessToolBuilder? CreateProcessToolBuilder()
    {
        var processesService = services.GetService(typeof(ProcessesService)) as ProcessesService;
        var templateCatalogService = services.GetService(typeof(ProcessTemplateCatalogService)) as ProcessTemplateCatalogService;
        var templatePackLoader = services.GetService(typeof(ProcessTemplatePackLoader)) as ProcessTemplatePackLoader;
        var templateProjectionService = services.GetService(typeof(ProcessTemplateProjectionService)) as ProcessTemplateProjectionService;
        var templateMermaidExporter = services.GetService(typeof(ProcessTemplateMermaidExporter)) as ProcessTemplateMermaidExporter;

        if (processesService is null ||
            templateCatalogService is null ||
            templatePackLoader is null ||
            templateProjectionService is null ||
            templateMermaidExporter is null)
        {
            return null;
        }

        return new ProcessToolBuilder(
            processesService,
            templateCatalogService,
            templatePackLoader,
            templateProjectionService,
            templateMermaidExporter);
    }

    private sealed class ProcessToolBuilder(
        ProcessesService processesService,
        ProcessTemplateCatalogService templateCatalogService,
        ProcessTemplatePackLoader templatePackLoader,
        ProcessTemplateProjectionService templateProjectionService,
        ProcessTemplateMermaidExporter templateMermaidExporter)
    {
        private readonly ProcessesService processesService = processesService;
        private readonly ProcessTemplateCatalogService templateCatalogService = templateCatalogService;
        private readonly ProcessTemplatePackLoader templatePackLoader = templatePackLoader;
        private readonly ProcessTemplateProjectionService templateProjectionService = templateProjectionService;
        private readonly ProcessTemplateMermaidExporter templateMermaidExporter = templateMermaidExporter;

        public IReadOnlyList<AITool> CreateTools(AgentDefinition agent)
        {
            var accessState = new ProcessAccessState(AgentProcessAccessMetadata.Read(agent.ConfigurationJson));

            return
            [
                AIFunctionFactory.Create(
                    (Guid? projectId = null, CancellationToken cancellationToken = default) => ProcessesDefinitionsListAsync(accessState, projectId, cancellationToken),
                    "processes_definitions_list",
                    "Lists process definitions. Use projectId to scope the list to one project."),
                AIFunctionFactory.Create(
                    (Guid? definitionId = null, Guid? projectId = null, CancellationToken cancellationToken = default) => ProcessesDefinitionEditorGetAsync(accessState, definitionId, projectId, cancellationToken),
                    "processes_definition_editor_get",
                    "Loads the full process-definition editor model. Omit definitionId to get a blank editor template optionally scoped to projectId."),
                AIFunctionFactory.Create(
                    (ProcessDefinitionEditorModel model, CancellationToken cancellationToken = default) => ProcessesDefinitionSaveAsync(accessState, model, cancellationToken),
                    "processes_definition_save",
                    "Creates or updates a process definition from the editor model and returns the definition id."),
                AIFunctionFactory.Create(
                    (ProcessDefinitionRoleAddRequest request, CancellationToken cancellationToken = default) => ProcessesDefinitionRoleAddAsync(accessState, request, cancellationToken),
                    AgentToolInvocationPolicyMetadata.ProcessesDefinitionRoleAdd,
                    "Adds one role requirement to an existing process definition without loading or rewriting the full editor model. Optionally attempts publish and returns publish errors explicitly."),
                AIFunctionFactory.Create(
                    (Guid definitionId, CancellationToken cancellationToken = default) => ProcessesDefinitionPublishAsync(accessState, definitionId, cancellationToken),
                    "processes_definition_publish",
                    "Publishes the current draft version of a process definition and makes it runtime-usable."),
                AIFunctionFactory.Create(
                    (Guid definitionId, CancellationToken cancellationToken = default) => ProcessesDefinitionDeleteAsync(accessState, definitionId, cancellationToken),
                    "processes_definition_delete",
                    "Deletes a process definition and its related runtime records."),
                AIFunctionFactory.Create(
                    (Guid definitionId, CancellationToken cancellationToken = default) => ProcessesDefinitionExportAsync(accessState, definitionId, cancellationToken),
                    "processes_definition_export",
                    "Exports a process definition into the CanDoItAll process import-export envelope."),
                AIFunctionFactory.Create(
                    (ProcessImportExportEnvelope envelope, CancellationToken cancellationToken = default) => ProcessesDefinitionImportAsync(accessState, envelope, cancellationToken),
                    "processes_definition_import",
                    "Imports a process definition from a CanDoItAll process import-export envelope and returns the imported definition id."),
                AIFunctionFactory.Create(
                    (Guid? definitionId = null, Guid? projectId = null, CancellationToken cancellationToken = default) => ProcessesRunsListAsync(accessState, definitionId, projectId, cancellationToken),
                    "processes_runs_list",
                    "Lists process runs. Use definitionId or projectId to narrow the scope."),
                AIFunctionFactory.Create(
                    (Guid runId, CancellationToken cancellationToken = default) => ProcessesRunDetailGetAsync(accessState, runId, cancellationToken),
                    "processes_run_detail_get",
                    "Loads a process run with health summary, step runs, decisions, artifacts, assignments, work briefs, conformance observations, and improvements."),
                AIFunctionFactory.Create(
                    (Guid? definitionId = null, Guid? projectId = null, CancellationToken cancellationToken = default) => ProcessesAnalyticsGetAsync(accessState, definitionId, projectId, cancellationToken),
                    "processes_analytics_get",
                    "Returns summary analytics for process definitions or project-scoped process execution."),
                AIFunctionFactory.Create(
                    (ProcessRunStartRequest request, CancellationToken cancellationToken = default) => ProcessesRunStartAsync(accessState, request, cancellationToken),
                    "processes_run_start",
                    "Starts a new process run from a published definition and returns the run id."),
                AIFunctionFactory.Create(
                    (ProcessStepTransitionRequest request, CancellationToken cancellationToken = default) => ProcessesStepTransitionAsync(accessState, request, cancellationToken),
                    "processes_step_transition",
                    "Transitions a process step run to the requested status and records the decision evidence."),
                AIFunctionFactory.Create(
                    (ProcessAssignmentResolutionRequest request, CancellationToken cancellationToken = default) => ProcessesAssignmentResolveAsync(accessState, request, cancellationToken),
                    "processes_assignment_resolve",
                    "Resolves or updates a runtime role assignment for a process run."),
                AIFunctionFactory.Create(
                    (ProcessArtifactRecordRequest request, CancellationToken cancellationToken = default) => ProcessesArtifactRecordAsync(accessState, request, cancellationToken),
                    "processes_artifact_record",
                    "Records artifact metadata against a process run or step and returns the artifact id."),
                AIFunctionFactory.Create(
                    (Guid projectId, CancellationToken cancellationToken = default) => ProcessesPartyOptionsListAsync(accessState, projectId, cancellationToken),
                    "processes_party_options_list",
                    "Lists project party options that can be used for process assignment decisions."),
                AIFunctionFactory.Create(
                    (CancellationToken cancellationToken = default) => ProcessesExecutorOptionsListAsync(accessState, cancellationToken),
                    "processes_executor_options_list",
                    "Lists executor registry options available to process runtime assignment flows."),
                AIFunctionFactory.Create(
                    (CancellationToken cancellationToken = default) => ProcessesTemplatesListAsync(accessState, cancellationToken),
                    "processes_templates_list",
                    "Lists the folder-based process template pack entries that can be inspected or imported without hardcoding templates in code."),
                AIFunctionFactory.Create(
                    (string processKey, CancellationToken cancellationToken = default) => ProcessesTemplateGetAsync(accessState, processKey, cancellationToken),
                    "processes_template_get",
                    "Loads a detailed template definition from the process template pack, including sidecar metadata and compatibility notes."),
                AIFunctionFactory.Create(
                    (string processKey, CancellationToken cancellationToken = default) => ProcessesTemplateMermaidGetAsync(accessState, processKey, cancellationToken),
                    "processes_template_mermaid_get",
                    "Exports Mermaid flowchart and sequence content for a process template together with the supporting sidecar files."),
                AIFunctionFactory.Create(
                    (InternalProcessTemplateImportRequest request, CancellationToken cancellationToken = default) => ProcessesTemplateImportAsync(accessState, request, cancellationToken),
                    "processes_template_import",
                    "Projects a folder-based process template into the current module import envelope, imports it, and optionally publishes it."),
                AIFunctionFactory.Create(
                    (CancellationToken cancellationToken = default) => ProcessesTemplateBaselineScenariosListAsync(accessState, cancellationToken),
                    AgentToolInvocationPolicyMetadata.ProcessesTemplateBaselineScenariosList,
                    "Lists baseline runtime scenarios stored in the process template pack for seeded regression coverage."),
                AIFunctionFactory.Create(
                    (CancellationToken cancellationToken = default) => ProcessesTemplateLiveRunProfilesListAsync(accessState, cancellationToken),
                    AgentToolInvocationPolicyMetadata.ProcessesTemplateLiveRunProfilesList,
                    "Lists fresh live-run profiles stored in the process template pack, including the typed fresh-run policy that forbids seeded transitions and artifacts.")
            ];
        }

        private async Task<IReadOnlyList<ProcessDefinitionListItem>> ProcessesDefinitionsListAsync(
            ProcessAccessState accessState,
            Guid? projectId,
            CancellationToken cancellationToken)
        {
            EnsureReadAllowed(accessState);
            var definitions = await processesService.ListDefinitionsAsync(projectId, cancellationToken);
            return accessState.AllowAllDefinitions
                ? definitions
                : definitions
                    .Where(item => accessState.AllowedDefinitionIds.Contains(item.Id))
                    .ToList();
        }

        private async Task<ProcessDefinitionEditorModel> ProcessesDefinitionEditorGetAsync(
            ProcessAccessState accessState,
            Guid? definitionId,
            Guid? projectId,
            CancellationToken cancellationToken)
        {
            EnsureReadAllowed(accessState);
            if (!definitionId.HasValue)
            {
                return await processesService.GetEditorAsync(null, projectId, cancellationToken);
            }

            EnsureDefinitionReadAllowed(accessState, definitionId.Value);
            await EnsureDefinitionExistsAsync(accessState, definitionId.Value, cancellationToken);

            var editor = await processesService.GetEditorAsync(definitionId, projectId, cancellationToken);
            if (!editor.Id.HasValue)
            {
                throw new ProcessToolException(
                    "ProcessDefinitionNotFound",
                    $"Process definition '{definitionId.Value:D}' was not found.");
            }

            return editor;
        }

        private async Task<Guid> ProcessesDefinitionSaveAsync(
            ProcessAccessState accessState,
            ProcessDefinitionEditorModel model,
            CancellationToken cancellationToken)
        {
            if (model.Id.HasValue)
            {
                EnsureDefinitionWriteAllowed(accessState, model.Id.Value);
                await EnsureDefinitionExistsAsync(accessState, model.Id.Value, cancellationToken);
            }
            else
            {
                EnsureWriteAllowed(accessState);
            }

            var definitionId = EnsureSuccess(await processesService.SaveAsync(model, cancellationToken));
            GrantDefinitionAccess(accessState, definitionId);
            return definitionId;
        }

        private async Task<ProcessDefinitionRoleAddResult> ProcessesDefinitionRoleAddAsync(
            ProcessAccessState accessState,
            ProcessDefinitionRoleAddRequest request,
            CancellationToken cancellationToken) {
            ArgumentNullException.ThrowIfNull(request);
            if (request.DefinitionId == Guid.Empty) {
                throw new ProcessToolException(
                    "ProcessDefinitionIdRequired",
                    "A process definition id is required to add a role.");
            }

            EnsureDefinitionWriteAllowed(accessState, request.DefinitionId);
            await EnsureDefinitionExistsAsync(accessState, request.DefinitionId, cancellationToken);

            var roleName = ResolveRequiredRoleName(request);
            var editor = await processesService.GetEditorAsync(request.DefinitionId, projectId: null, cancellationToken);
            if (!editor.Id.HasValue) {
                throw new ProcessToolException(
                    "ProcessDefinitionNotFound",
                    $"Process definition '{request.DefinitionId:D}' was not found.");
            }

            if (editor.Roles.Any(role => string.Equals(role.DisplayName.Trim(), roleName, StringComparison.OrdinalIgnoreCase))) {
                throw new ProcessToolException(
                    "ProcessRoleDuplicate",
                    $"Process definition '{request.DefinitionId:D}' already contains a role named '{roleName}'.");
            }

            var roleId = Guid.NewGuid();
            editor.Roles.Add(new ProcessRoleEditorModel {
                Id = roleId,
                DisplayName = roleName,
                Purpose = ResolveRolePurpose(request, roleName),
                StaffingIntent = ResolveRoleStaffingIntent(request, roleName),
                PreferredExecutorKind = request.PreferredExecutorKind.Trim(),
                PreferredProjectAssignmentRole = request.PreferredProjectAssignmentRole,
                IsRequired = request.IsRequired,
                AllowsFallback = request.AllowsFallback,
                RequiresExplicitApproval = request.RequiresExplicitApproval,
                DefaultAllocationPercent = request.DefaultAllocationPercent,
                SnapshotSummary = ResolveFirstNonBlank(request.SnapshotSummary, request.Responsibilities),
                CanvasX = request.CanvasX ?? ResolveNextRoleCanvasX(editor),
                CanvasY = request.CanvasY ?? ResolveNextRoleCanvasY(editor)
            });

            var definitionId = EnsureSuccess(await processesService.SaveAsync(editor, cancellationToken));
            var publishAttempted = request.PublishIfValid;
            var published = false;
            var publishErrorCode = string.Empty;
            var publishErrorMessage = string.Empty;

            if (request.PublishIfValid) {
                var publishResult = await processesService.PublishAsync(definitionId, cancellationToken);
                if (publishResult.IsSuccess) {
                    published = true;
                }
                else {
                    var publishError = publishResult.Errors.FirstOrDefault();
                    publishErrorCode = publishError?.Code ?? "processes.publish-failed";
                    publishErrorMessage = publishError?.Message ?? "The process definition could not be published.";
                }
            }

            return new ProcessDefinitionRoleAddResult(
                definitionId,
                roleId,
                roleName,
                publishAttempted,
                published,
                publishErrorCode,
                publishErrorMessage);
        }

        private async Task<Guid> ProcessesDefinitionPublishAsync(
            ProcessAccessState accessState,
            Guid definitionId,
            CancellationToken cancellationToken)
        {
            EnsureDefinitionWriteAllowed(accessState, definitionId);
            EnsureSuccess(await processesService.PublishAsync(definitionId, cancellationToken));
            return definitionId;
        }

        private async Task<Guid> ProcessesDefinitionDeleteAsync(
            ProcessAccessState accessState,
            Guid definitionId,
            CancellationToken cancellationToken)
        {
            EnsureDefinitionWriteAllowed(accessState, definitionId);
            await EnsureDefinitionExistsAsync(accessState, definitionId, cancellationToken);
            await processesService.DeleteAsync(definitionId, cancellationToken);
            RevokeDefinitionAccess(accessState, definitionId);
            return definitionId;
        }

        private async Task<ProcessImportExportEnvelope> ProcessesDefinitionExportAsync(
            ProcessAccessState accessState,
            Guid definitionId,
            CancellationToken cancellationToken)
        {
            EnsureDefinitionReadAllowed(accessState, definitionId);
            await EnsureDefinitionExistsAsync(accessState, definitionId, cancellationToken);
            return await processesService.ExportAsync(definitionId, cancellationToken);
        }

        private async Task<Guid> ProcessesDefinitionImportAsync(
            ProcessAccessState accessState,
            ProcessImportExportEnvelope envelope,
            CancellationToken cancellationToken)
        {
            EnsureWriteAllowed(accessState);
            var definitionId = EnsureSuccess(await processesService.ImportAsync(envelope, cancellationToken));
            GrantDefinitionAccess(accessState, definitionId);
            return definitionId;
        }

        private async Task<IReadOnlyList<ProcessRunListItem>> ProcessesRunsListAsync(
            ProcessAccessState accessState,
            Guid? definitionId,
            Guid? projectId,
            CancellationToken cancellationToken)
        {
            EnsureReadAllowed(accessState);
            if (definitionId.HasValue)
            {
                EnsureDefinitionReadAllowed(accessState, definitionId.Value);
            }

            var runs = await processesService.ListRunsAsync(definitionId, projectId, cancellationToken);
            return accessState.AllowAllDefinitions
                ? runs
                : runs
                    .Where(item => accessState.AllowedDefinitionIds.Contains(item.ProcessDefinitionId))
                    .ToList();
        }

        private async Task<InternalProcessRunDetailToolData> ProcessesRunDetailGetAsync(
            ProcessAccessState accessState,
            Guid runId,
            CancellationToken cancellationToken)
        {
            EnsureReadAllowed(accessState);
            var run = await GetRunAsync(runId, cancellationToken);
            EnsureDefinitionReadAllowed(accessState, run.ProcessDefinitionId);

            var details = await processesService.GetRunDetailsAsync(runId, cancellationToken);
            var improvements = await processesService.ListRunImprovementsAsync(runId, cancellationToken);

            return new InternalProcessRunDetailToolData(
                run,
                details.Health,
                details.StepRuns,
                details.Decisions,
                details.Artifacts,
                details.Assignments,
                details.WorkBriefs,
                details.ConformanceObservations,
                improvements);
        }

        private async Task<ProcessAnalyticsSummary> ProcessesAnalyticsGetAsync(
            ProcessAccessState accessState,
            Guid? definitionId,
            Guid? projectId,
            CancellationToken cancellationToken)
        {
            EnsureReadAllowed(accessState);
            if (definitionId.HasValue)
            {
                EnsureDefinitionReadAllowed(accessState, definitionId.Value);
                return await processesService.GetAnalyticsAsync(definitionId, projectId, cancellationToken);
            }

            if (projectId.HasValue)
            {
                await EnsureProjectReadAllowedAsync(accessState, projectId.Value, cancellationToken);
            }

            var allowedDefinitionIds = accessState.AllowAllDefinitions
                ? (await GetAllowedDefinitionsByIdAsync(accessState, cancellationToken)).Keys.ToList()
                : accessState.AllowedDefinitionIds.ToList();
            return await processesService.GetAnalyticsForDefinitionsAsync(
                allowedDefinitionIds,
                projectId,
                cancellationToken);
        }

        private async Task<Guid> ProcessesRunStartAsync(
            ProcessAccessState accessState,
            ProcessRunStartRequest request,
            CancellationToken cancellationToken)
        {
            EnsureWriteAllowed(accessState);
            if (request.LaunchPlanId.HasValue)
            {
                var launchPlan = await processesService.GetLaunchPlanAccessSummaryAsync(request.LaunchPlanId.Value, cancellationToken);
                if (launchPlan is null)
                {
                    throw new ProcessToolException(
                        "ProcessLaunchPlanNotFound",
                        $"Process launch plan '{request.LaunchPlanId.Value:D}' was not found.");
                }

                EnsureDefinitionWriteAllowed(accessState, launchPlan.ProcessDefinitionId);
            }
            else if (request.ProcessDefinitionId != Guid.Empty)
            {
                EnsureDefinitionWriteAllowed(accessState, request.ProcessDefinitionId);
            }

            return EnsureSuccess(await processesService.StartRunAsync(request, cancellationToken));
        }

        private async Task<Guid> ProcessesStepTransitionAsync(
            ProcessAccessState accessState,
            ProcessStepTransitionRequest request,
            CancellationToken cancellationToken)
        {
            EnsureWriteAllowed(accessState);
            var stepRun = await processesService.GetStepRunAccessSummaryAsync(request.StepRunId, cancellationToken);
            if (stepRun is null)
            {
                throw new ProcessToolException(
                    "ProcessStepRunNotFound",
                    $"Process step run '{request.StepRunId:D}' was not found.");
            }

            EnsureDefinitionWriteAllowed(accessState, stepRun.ProcessDefinitionId);
            EnsureSuccess(await processesService.TransitionStepAsync(request, cancellationToken));
            return request.StepRunId;
        }

        private async Task<Guid> ProcessesAssignmentResolveAsync(
            ProcessAccessState accessState,
            ProcessAssignmentResolutionRequest request,
            CancellationToken cancellationToken)
        {
            EnsureWriteAllowed(accessState);
            var run = await GetRunAsync(request.ProcessRunId, cancellationToken);
            EnsureDefinitionWriteAllowed(accessState, run.ProcessDefinitionId);
            EnsureSuccess(await processesService.ResolveAssignmentAsync(request, cancellationToken));
            return request.ProcessRunId;
        }

        private async Task<Guid> ProcessesArtifactRecordAsync(
            ProcessAccessState accessState,
            ProcessArtifactRecordRequest request,
            CancellationToken cancellationToken)
        {
            EnsureWriteAllowed(accessState);
            var run = await GetRunAsync(request.ProcessRunId, cancellationToken);
            EnsureDefinitionWriteAllowed(accessState, run.ProcessDefinitionId);
            return EnsureSuccess(await processesService.RecordArtifactAsync(request, cancellationToken));
        }

        private async Task<IReadOnlyList<ProjectPartyOption>> ProcessesPartyOptionsListAsync(
            ProcessAccessState accessState,
            Guid projectId,
            CancellationToken cancellationToken)
        {
            EnsureReadAllowed(accessState);
            await EnsureProjectReadAllowedAsync(accessState, projectId, cancellationToken);
            return await processesService.ListPartyOptionsAsync(projectId, cancellationToken);
        }

        private async Task<IReadOnlyList<ProcessExecutorRegistryOption>> ProcessesExecutorOptionsListAsync(
            ProcessAccessState accessState,
            CancellationToken cancellationToken)
        {
            EnsureReadAllowed(accessState);
            return await processesService.ListExecutorOptionsAsync(cancellationToken);
        }

        private Task<IReadOnlyList<ProcessTemplateCatalogItem>> ProcessesTemplatesListAsync(
            ProcessAccessState accessState,
            CancellationToken cancellationToken)
        {
            EnsureReadAllowed(accessState);
            return Task.FromResult(templateCatalogService.ListProcessTemplates());
        }

        private Task<InternalProcessTemplateDetailToolData> ProcessesTemplateGetAsync(
            ProcessAccessState accessState,
            string processKey,
            CancellationToken cancellationToken)
        {
            EnsureReadAllowed(accessState);
            var pack = templatePackLoader.Load();
            if (!pack.Processes.TryGetValue(processKey, out var process))
            {
                throw new ProcessToolException(
                    "ProcessTemplateNotFound",
                    $"Process template '{processKey}' was not found.");
            }

            var summary = templateCatalogService.ListProcessTemplates()
                .Single(item => string.Equals(item.Key, processKey, StringComparison.OrdinalIgnoreCase));
            var supportingFiles = templateMermaidExporter.Export(processKey).SupportingFiles;

            return Task.FromResult(
                new InternalProcessTemplateDetailToolData(
                    summary,
                    process,
                    templateProjectionService.GetCompatibilityReportMarkdown(processKey),
                    supportingFiles));
        }

        private Task<ProcessTemplateMermaidDocument> ProcessesTemplateMermaidGetAsync(
            ProcessAccessState accessState,
            string processKey,
            CancellationToken cancellationToken)
        {
            EnsureReadAllowed(accessState);
            try
            {
                return Task.FromResult(templateMermaidExporter.Export(processKey));
            }
            catch (InvalidOperationException)
            {
                throw new ProcessToolException(
                    "ProcessTemplateNotFound",
                    $"Process template '{processKey}' was not found.");
            }
        }

        private async Task<ProcessTemplateImportResult> ProcessesTemplateImportAsync(
            ProcessAccessState accessState,
            InternalProcessTemplateImportRequest request,
            CancellationToken cancellationToken)
        {
            EnsureWriteAllowed(accessState);

            ProcessImportExportEnvelope envelope;
            try
            {
                envelope = templateProjectionService.GetProjectedEnvelope(
                    request.ProcessKey,
                    request.ProjectId,
                    request.DefinitionName);
            }
            catch (InvalidOperationException)
            {
                throw new ProcessToolException(
                    "ProcessTemplateNotFound",
                    $"Process template '{request.ProcessKey}' was not found.");
            }

            var definitionId = EnsureSuccess(await processesService.ImportAsync(envelope, cancellationToken));
            if (request.AutoPublish)
            {
                EnsureSuccess(await processesService.PublishAsync(definitionId, cancellationToken));
            }

            GrantDefinitionAccess(accessState, definitionId);
            return new ProcessTemplateImportResult(request.ProcessKey, definitionId, envelope.Warnings);
        }

        private Task<IReadOnlyList<ProcessTemplateBaselineScenarioSummary>> ProcessesTemplateBaselineScenariosListAsync(
            ProcessAccessState accessState,
            CancellationToken cancellationToken)
        {
            EnsureReadAllowed(accessState);
            var pack = templatePackLoader.Load();
            return Task.FromResult<IReadOnlyList<ProcessTemplateBaselineScenarioSummary>>(
                pack.BaselineScenarios
                    .Select(item => new ProcessTemplateBaselineScenarioSummary(
                        item.Key,
                        item.ProcessTemplateKey,
                        item.RunName,
                        item.OperatingMode,
                        item.Assignments.Count,
                        item.Transitions.Count,
                        item.Artifacts.Count,
                        item.Transitions.Count(transition => !string.IsNullOrWhiteSpace(transition.SelectedBranchOutcomeKey)),
                        item.Transitions.Count(transition => string.Equals(transition.TargetStatus, ProcessStepRunStatus.Blocked.ToString(), StringComparison.OrdinalIgnoreCase)),
                        item.ContractExercises.Count,
                        item.RecoveryExercises.Count))
                    .ToList());
        }

        private Task<IReadOnlyList<ProcessTemplateLiveRunProfileSummary>> ProcessesTemplateLiveRunProfilesListAsync(
            ProcessAccessState accessState,
            CancellationToken cancellationToken)
        {
            EnsureReadAllowed(accessState);
            var pack = templatePackLoader.Load();
            return Task.FromResult<IReadOnlyList<ProcessTemplateLiveRunProfileSummary>>(
                pack.LiveRunProfiles
                    .Select(item => new ProcessTemplateLiveRunProfileSummary(
                        item.Key,
                        item.ProcessTemplateKey,
                        item.RunNameTemplate,
                        item.Summary,
                        item.OperatingMode,
                        item.TriggerReasonTemplate,
                        item.FreshRunPolicy,
                        item.Assignments.Count,
                        item.AcceptanceCriteria.Count,
                        item.RequiredProofKinds.Count))
                    .ToList());
        }

        private async Task<ProcessRunListItem> GetRunAsync(Guid runId, CancellationToken cancellationToken)
        {
            var run = await processesService.GetRunAsync(runId, cancellationToken);
            if (run is not null)
            {
                return run;
            }

            throw new ProcessToolException(
                "ProcessRunNotFound",
                $"Process run '{runId:D}' was not found.");
        }

        private async Task<IReadOnlyDictionary<Guid, ProcessDefinitionListItem>> GetAllowedDefinitionsByIdAsync(
            ProcessAccessState accessState,
            CancellationToken cancellationToken)
        {
            if (accessState.AllowedDefinitionsByIdTask is not null)
            {
                return await accessState.AllowedDefinitionsByIdTask;
            }

            accessState.AllowedDefinitionsByIdTask = LoadAllowedDefinitionsByIdAsync(accessState, cancellationToken);
            return await accessState.AllowedDefinitionsByIdTask;
        }

        private async Task<IReadOnlyDictionary<Guid, ProcessDefinitionListItem>> LoadAllowedDefinitionsByIdAsync(
            ProcessAccessState accessState,
            CancellationToken cancellationToken)
        {
            var definitions = await processesService.ListDefinitionsAsync(cancellationToken: cancellationToken);
            return accessState.AllowAllDefinitions
                ? definitions.ToDictionary(item => item.Id)
                : definitions
                    .Where(item => accessState.AllowedDefinitionIds.Contains(item.Id))
                    .ToDictionary(item => item.Id);
        }

        private async Task EnsureDefinitionExistsAsync(
            ProcessAccessState accessState,
            Guid definitionId,
            CancellationToken cancellationToken)
        {
            var allowedDefinitions = await GetAllowedDefinitionsByIdAsync(accessState, cancellationToken);
            if (allowedDefinitions.ContainsKey(definitionId))
            {
                return;
            }

            throw new ProcessToolException(
                "ProcessDefinitionNotFound",
                $"Process definition '{definitionId:D}' was not found.");
        }

        private async Task EnsureProjectReadAllowedAsync(
            ProcessAccessState accessState,
            Guid projectId,
            CancellationToken cancellationToken)
        {
            EnsureReadAllowed(accessState);
            var allowedDefinitions = await GetAllowedDefinitionsByIdAsync(accessState, cancellationToken);
            if (allowedDefinitions.Values.Any(item => item.ProjectId == projectId))
            {
                return;
            }

            throw new ProcessToolException(
                "ProcessProjectDenied",
                $"Project '{projectId:D}' is outside the agent's allowed process scope.");
        }

        private static Guid EnsureSuccess(Result<Guid> result)
        {
            if (result.IsSuccess)
            {
                return result.Value;
            }

            throw CreateProcessToolException(result.Errors);
        }

        private static void EnsureSuccess(Result result)
        {
            if (result.IsFailure)
            {
                throw CreateProcessToolException(result.Errors);
            }
        }

        private static ProcessToolException CreateProcessToolException(IReadOnlyList<Error> errors)
        {
            var firstError = errors.FirstOrDefault() ?? Error.Failure("The process operation failed.", "processes.failure");
            return new ProcessToolException(firstError.Code, firstError.Message);
        }

        private static string ResolveRequiredRoleName(ProcessDefinitionRoleAddRequest request) {
            var roleName = request.RoleName.Trim();
            if (!string.IsNullOrWhiteSpace(roleName)) {
                return roleName;
            }

            throw new ProcessToolException(
                "ProcessRoleNameRequired",
                "A non-empty roleName is required to add a process role.");
        }

        private static string ResolveRolePurpose(ProcessDefinitionRoleAddRequest request, string roleName) {
            var value = ResolveFirstNonBlank(request.Purpose, request.Responsibilities);
            return string.IsNullOrWhiteSpace(value)
                ? $"Owns {roleName} responsibilities for the process."
                : value;
        }

        private static string ResolveRoleStaffingIntent(ProcessDefinitionRoleAddRequest request, string roleName) {
            var value = ResolveFirstNonBlank(request.StaffingIntent, request.Responsibilities);
            return string.IsNullOrWhiteSpace(value)
                ? $"Staff a contributor accountable for {roleName}."
                : value;
        }

        private static string ResolveFirstNonBlank(params string?[] values) {
            return values
                .Select(value => value?.Trim() ?? string.Empty)
                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
        }

        private static double ResolveNextRoleCanvasX(ProcessDefinitionEditorModel editor) {
            return editor.Roles.Count == 0
                ? 80
                : editor.Roles.Max(role => role.CanvasX) + 220;
        }

        private static double ResolveNextRoleCanvasY(ProcessDefinitionEditorModel editor) {
            return editor.Roles.Count == 0
                ? 80
                : editor.Roles
                    .OrderBy(role => role.CanvasX)
                    .Last()
                    .CanvasY;
        }

        private static void EnsureReadAllowed(ProcessAccessState accessState)
        {
            if (accessState.CanRead)
            {
                return;
            }

            throw new ProcessToolException(
                "ProcessReadDenied",
                "This agent is not allowed to read process modules. Enable read access in the agent settings.");
        }

        private static void EnsureWriteAllowed(ProcessAccessState accessState)
        {
            if (accessState.CanWrite)
            {
                return;
            }

            throw new ProcessToolException(
                "ProcessWriteDenied",
                "This agent is not allowed to write process modules. Enable write access in the agent settings.");
        }

        private static void EnsureDefinitionReadAllowed(ProcessAccessState accessState, Guid definitionId)
        {
            EnsureReadAllowed(accessState);
            EnsureDefinitionAllowed(accessState, definitionId);
        }

        private static void EnsureDefinitionWriteAllowed(ProcessAccessState accessState, Guid definitionId)
        {
            EnsureWriteAllowed(accessState);
            EnsureDefinitionAllowed(accessState, definitionId);
        }

        private static void EnsureDefinitionAllowed(ProcessAccessState accessState, Guid definitionId)
        {
            if (accessState.AllowAllDefinitions ||
                accessState.AllowedDefinitionIds.Contains(definitionId))
            {
                return;
            }

            throw new ProcessToolException(
                "ProcessDefinitionDenied",
                $"Process definition '{definitionId:D}' is outside the agent's allowed process scope.");
        }

        private static void GrantDefinitionAccess(ProcessAccessState accessState, Guid definitionId)
        {
            if (definitionId == Guid.Empty)
            {
                return;
            }

            accessState.AllowedDefinitionIds.Add(definitionId);
            accessState.AllowedDefinitionsByIdTask = null;
        }

        private static void RevokeDefinitionAccess(ProcessAccessState accessState, Guid definitionId)
        {
            accessState.AllowedDefinitionIds.Remove(definitionId);
            accessState.AllowedDefinitionsByIdTask = null;
        }
    }

    private sealed class ProcessAccessState
    {
        public ProcessAccessState(AgentProcessAccessSettings settings)
        {
            var normalized = AgentProcessAccessMetadata.Normalize(settings);
            CanRead = normalized.CanRead;
            CanWrite = normalized.CanWrite;
            AllowAllDefinitions = normalized.AllowAllDefinitions;
            AllowedDefinitionIds = normalized.AllowedDefinitionIds.ToHashSet();
        }

        public bool CanRead { get; }

        public bool CanWrite { get; }

        public bool AllowAllDefinitions { get; }

        public HashSet<Guid> AllowedDefinitionIds { get; }

        public Task<IReadOnlyDictionary<Guid, ProcessDefinitionListItem>>? AllowedDefinitionsByIdTask { get; set; }
    }

    private sealed class ProcessToolException(
        string errorCode,
        string message) : InvalidOperationException(message)
    {
        public string ErrorCode { get; } = errorCode;
    }
}

public sealed class InternalProcessTemplateImportRequest
{
    public string ProcessKey { get; set; } = string.Empty;

    public Guid? ProjectId { get; set; }

    public string DefinitionName { get; set; } = string.Empty;

    public bool AutoPublish { get; set; } = true;
}

public sealed class ProcessDefinitionRoleAddRequest
{
    public Guid DefinitionId { get; set; }

    public string RoleName { get; set; } = string.Empty;

    public string Purpose { get; set; } = string.Empty;

    public string Responsibilities { get; set; } = string.Empty;

    public string StaffingIntent { get; set; } = string.Empty;

    public string PreferredExecutorKind { get; set; } = string.Empty;

    public ProjectPartyAssignmentRole? PreferredProjectAssignmentRole { get; set; }

    public bool IsRequired { get; set; } = true;

    public bool AllowsFallback { get; set; } = true;

    public bool RequiresExplicitApproval { get; set; }

    public int DefaultAllocationPercent { get; set; } = 100;

    public string SnapshotSummary { get; set; } = string.Empty;

    public double? CanvasX { get; set; }

    public double? CanvasY { get; set; }

    public bool PublishIfValid { get; set; }
}

public sealed record ProcessDefinitionRoleAddResult(
    Guid DefinitionId,
    Guid RoleRequirementId,
    string RoleName,
    bool PublishAttempted,
    bool Published,
    string PublishErrorCode,
    string PublishErrorMessage);

public sealed record InternalProcessRunDetailToolData(
    ProcessRunListItem Run,
    ProcessRunHealthSummaryViewModel Health,
    IReadOnlyList<ProcessStepRunViewModel> StepRuns,
    IReadOnlyList<ProcessDecisionViewModel> DecisionRecords,
    IReadOnlyList<ProcessArtifactViewModel> Artifacts,
    IReadOnlyList<ProcessRunAssignmentViewModel> Assignments,
    IReadOnlyList<ProcessWorkBriefViewModel> WorkBriefs,
    IReadOnlyList<ProcessConformanceObservationViewModel> ConformanceObservations,
    IReadOnlyList<ProcessImprovementViewModel> Improvements);

public sealed record InternalProcessTemplateDetailToolData(
    ProcessTemplateCatalogItem Summary,
    ProcessTemplateDefinition Template,
    string CompatibilityReportMarkdown,
    IReadOnlyList<string> SupportingFiles);
