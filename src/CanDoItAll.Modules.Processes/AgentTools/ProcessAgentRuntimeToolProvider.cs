using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.AI;

namespace CanDoItAll.Modules.Processes;

public sealed partial class ProcessAgentRuntimeToolProvider(
    ProcessesService processesService,
    ProcessTemplateCatalogService templateCatalogService,
    ProcessTemplatePackLoader templatePackLoader,
    ProcessTemplateProjectionService templateProjectionService,
    ProcessTemplateMermaidExporter templateMermaidExporter) : IAgentRuntimeToolProvider
{
    private const int DefaultOrder = 1000;

    private readonly ProcessesService processesService = processesService;
    private readonly ProcessTemplateCatalogService templateCatalogService = templateCatalogService;
    private readonly ProcessTemplatePackLoader templatePackLoader = templatePackLoader;
    private readonly ProcessTemplateProjectionService templateProjectionService = templateProjectionService;
    private readonly ProcessTemplateMermaidExporter templateMermaidExporter = templateMermaidExporter;

    public int Order => DefaultOrder;

    public AgentRuntimeToolProviderDescriptor Descriptor { get; } = new(
        "processes.runtime-tools",
        "Processes runtime tools",
        "Provides process definition, run, assignment, artifact, analytics, and template tools.",
        ["processes", "runtime"],
        [
            AgentRuntimeToolProviderPurpose.InteractiveChat,
            AgentRuntimeToolProviderPurpose.GovernedProcessAutomation,
            AgentRuntimeToolProviderPurpose.AutoApprovedNonInteractive,
            AgentRuntimeToolProviderPurpose.A2AEndpoint
        ]);

    public ValueTask<IReadOnlyList<AITool>> CreateToolsAsync(
        AgentRuntimeToolProviderContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(CreateTools(context));
    }

    private IReadOnlyList<AITool> CreateTools(AgentRuntimeToolProviderContext context)
    {
        var accessState = new ProcessAccessState(AgentProcessAccessMetadata.Read(context.Agent.ConfigurationJson));
        var purposePolicy = ResolvePurposePolicy(context.Purpose);
        if (!purposePolicy.AllowReadTools &&
            !purposePolicy.AllowMutationTools)
        {
            return [];
        }

        AITool[] tools =
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

        return tools
            .Where(tool => ShouldExposeTool(accessState, purposePolicy, tool.Name))
            .ToList();
    }
}
