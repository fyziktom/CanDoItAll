using System.Text.Json.Serialization;

namespace CanDoItAll.AgentFramework.Models;

/// <summary>
/// Catalog entry of a workflow: the metadata of one version, normally the current one, without its graph. Returned by
/// <c>GET /api/workflows/definitions</c> and inside stable-identity resolutions; read the full definition with
/// <c>GET /api/workflows/definitions/{workflowId}</c>.
/// </summary>
/// <param name="Id">Identifier of the workflow.</param>
/// <param name="VersionId">Identifier of the version the entry describes.</param>
/// <param name="Name">Display name.</param>
/// <param name="Description">Description; may be empty.</param>
/// <param name="Status">
/// Lifecycle status of the version, as a JSON integer: 0 Draft, 1 Active, 2 Suspended, 3 Archived.
/// </param>
/// <param name="PreferredBackend">
/// Backend from the version's runtime policy, as a JSON integer: 0 InProcess, 1 DurableTask, 2 AzureFunctions.
/// </param>
/// <param name="UpdatedAtUtc">Time the version was stored, as an instant with offset.</param>
public sealed record WorkflowCatalogItem(
    WorkflowId Id,
    WorkflowVersionId VersionId,
    string Name,
    string Description,
    WorkflowLifecycleStatus Status,
    WorkflowRuntimeBackendKind PreferredBackend,
    DateTimeOffset UpdatedAtUtc)
{
    /// <summary>
    /// Template key when the workflow was installed from a workflow template; empty otherwise. Not unique.
    /// </summary>
    public string TemplateKey { get; init; } = string.Empty;

    /// <summary>Key of the template pack the workflow came from; empty when not installed from a template.</summary>
    public string TemplatePackKey { get; init; } = string.Empty;

    /// <summary>Version of that template pack; empty when not installed from a template.</summary>
    public string TemplatePackVersion { get; init; } = string.Empty;

    /// <summary>
    /// SHA-256 hash of the template source as lower-case hexadecimal; empty when not installed from a template.
    /// </summary>
    public string SourceHash { get; init; } = string.Empty;

    /// <summary>Namespace of the workflow's external identity, lower-case; empty when it has none.</summary>
    public string ExternalNamespace { get; init; } = string.Empty;

    /// <summary>Key of the workflow's external identity, lower-case; empty when it has none.</summary>
    public string ExternalKey { get; init; } = string.Empty;
}

/// <summary>
/// Body of <c>POST /api/workflows/definitions</c>: the complete content of a new workflow or of a new version of an
/// existing one, with an optional concurrency precondition. Send every member; the content replaces the definition.
/// </summary>
/// <param name="Id">
/// Identifier of the workflow to update; null creates a new workflow with a server-generated identifier. An unknown
/// identifier creates a workflow with that identifier.
/// </param>
/// <param name="ExpectedVersionId">
/// <c>versionId</c> of the current version as last read. When another save happened in between, the save is rejected
/// with HTTP 400; null saves without this check. It must be null when creating a workflow.
/// </param>
/// <param name="Name">Display name; required and trimmed.</param>
/// <param name="Description">Description; send an empty string for none. It is trimmed.</param>
/// <param name="Status">
/// Lifecycle status of the new version, as a JSON integer: 0 Draft, 1 Active, 2 Suspended, 3 Archived. It is stored
/// as sent, so Active publishes the new version.
/// </param>
/// <param name="Graph">The complete node graph.</param>
/// <param name="RuntimePolicy">Backend and run-kind policy of the workflow.</param>
public sealed record WorkflowDefinitionSaveRequest(
    WorkflowId? Id,
    WorkflowVersionId? ExpectedVersionId,
    string Name,
    string Description,
    WorkflowLifecycleStatus Status,
    WorkflowGraph Graph,
    WorkflowRuntimePolicy RuntimePolicy)
{
    /// <summary>
    /// Input parameters the workflow expects; omitted or empty stores none. The list replaces the stored parameters.
    /// </summary>
    public IReadOnlyList<WorkflowInputParameterDescriptor> InputParameters { get; init; } = [];

    /// <summary>
    /// Namespace of the workflow's external identity, for example <c>partner.system</c>; send it together with
    /// <c>externalKey</c>. It is trimmed and lower-cased; at most 100 characters of ASCII letters, digits, hyphen,
    /// underscore, period and colon. Empty keeps the identity of the current version.
    /// </summary>
    public string ExternalNamespace { get; init; } = string.Empty;

    /// <summary>
    /// Key of the workflow's external identity within the namespace, for example <c>invoice:review</c>; send it
    /// together with <c>externalNamespace</c>. It is trimmed and lower-cased; at most 200 characters of the same
    /// characters. The pair must not be used by another workflow. Empty keeps the identity of the current version.
    /// </summary>
    public string ExternalKey { get; init; } = string.Empty;

    [JsonIgnore]
    public WorkflowTemplateProvenance? TemplateProvenance { get; init; }
}

public sealed record WorkflowTemplateProvenance(
    string TemplateKey,
    string TemplatePackKey,
    string TemplatePackVersion,
    string SourceHash);

/// <summary>
/// Kind of stable workflow identity, written as the string token <c>Template</c> (the template key a workflow was
/// installed from) or <c>External</c> (an external namespace and key assigned by a client).
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<WorkflowStableIdentityKind>))]
public enum WorkflowStableIdentityKind
{
    Template,
    External
}

/// <summary>
/// Outcome of a stable-identity resolution, written as a string token: <c>Resolved</c> (exactly one workflow matches
/// and it has a runnable Active version), <c>NotFound</c> (no workflow matches), <c>Ambiguous</c> (several workflows
/// match; none is selected) or <c>Stale</c> (one workflow matches but it has no runnable Active version).
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<WorkflowStableIdentityResolutionStatus>))]
public enum WorkflowStableIdentityResolutionStatus
{
    Resolved,
    NotFound,
    Ambiguous,
    Stale
}

/// <summary>
/// Result of resolving a stable workflow identity (a template key or an external namespace and key) to a workflow and
/// the version that a latest-Active start would run. Every outcome is returned with HTTP 200; check <c>status</c>.
/// </summary>
/// <param name="IdentityKind">
/// Which identity was resolved, as a string token: <c>Template</c> or <c>External</c>.
/// </param>
/// <param name="Namespace">
/// Normalized (lower-case) namespace that was resolved; empty for template keys.
/// </param>
/// <param name="Key">Normalized (lower-case) template key or external key that was resolved.</param>
/// <param name="Status">
/// Outcome, as a string token: <c>Resolved</c>, <c>NotFound</c>, <c>Ambiguous</c> or <c>Stale</c>.
/// </param>
/// <param name="WorkflowId">
/// The matching workflow for <c>Resolved</c> and <c>Stale</c>; null for <c>NotFound</c> and <c>Ambiguous</c>.
/// </param>
/// <param name="RunnableVersionId">
/// Version a latest-Active start would run; only for <c>Resolved</c>, null otherwise.
/// </param>
/// <param name="Materializations">
/// Catalog entries of every matching workflow, ordered by workflow identifier; empty for <c>NotFound</c>.
/// </param>
/// <param name="Message">Human-readable explanation of the outcome.</param>
public sealed record WorkflowStableIdentityResolution(
    WorkflowStableIdentityKind IdentityKind,
    string Namespace,
    string Key,
    WorkflowStableIdentityResolutionStatus Status,
    WorkflowId? WorkflowId,
    WorkflowVersionId? RunnableVersionId,
    IReadOnlyList<WorkflowCatalogItem> Materializations,
    string Message);

public static class WorkflowDefinitionExchangeFormats
{
    public const string Current = "CanDoItAll.WorkflowDefinition/v1";
}

public sealed record WorkflowDefinitionStatusChangeRequest(
    WorkflowId WorkflowId,
    WorkflowVersionId? ExpectedVersionId,
    WorkflowLifecycleStatus Status);

/// <summary>
/// Portable export of one workflow definition version, produced by
/// <c>GET /api/workflows/definitions/{workflowId}/export</c> and accepted in <c>envelope</c> of
/// <c>POST /api/workflows/definitions/import</c>. Send it back unchanged.
/// </summary>
/// <param name="SourceFormat">
/// Exchange format of the envelope; the import accepts exactly <c>CanDoItAll.WorkflowDefinition/v1</c>.
/// </param>
/// <param name="Definition">The exported definition version.</param>
/// <param name="Validation">Validation result at export time; informational and ignored by the import.</param>
/// <param name="ExportedAtUtc">Time of the export, as an instant with offset; ignored by the import.</param>
public sealed record WorkflowDefinitionExportEnvelope(
    string SourceFormat,
    WorkflowDefinition Definition,
    WorkflowValidationResult Validation,
    DateTimeOffset ExportedAtUtc);

/// <summary>
/// Body of <c>POST /api/workflows/definitions/import</c>: an export envelope and how to import it.
/// </summary>
/// <param name="Envelope">The export envelope, as produced by the export operation.</param>
/// <param name="Name">Name of the imported workflow; null or blank keeps the name from the envelope.</param>
/// <param name="Status">
/// Lifecycle status of the imported version, as a JSON integer: 0 Draft, 1 Active, 2 Suspended, 3 Archived; null
/// imports it as Draft.
/// </param>
/// <param name="PreserveWorkflowId">
/// True to keep the source's workflow identifier, external identity and template provenance, which makes the import a
/// new version of that workflow when it exists here; false to create a new workflow with a new identifier and no
/// external identity or template provenance.
/// </param>
public sealed record WorkflowDefinitionImportRequest(
    WorkflowDefinitionExportEnvelope Envelope,
    string? Name,
    WorkflowLifecycleStatus? Status,
    bool PreserveWorkflowId);

/// <summary>
/// A stored workflow definition version with its validation result, computed when it was read.
/// </summary>
/// <param name="Definition">The definition version.</param>
/// <param name="Validation">
/// Validation against the current components, providers, backends and settings; an invalid version cannot be
/// published or started.
/// </param>
public sealed record WorkflowDefinitionDetail(
    WorkflowDefinition Definition,
    WorkflowValidationResult Validation);

/// <summary>
/// Payload and artifact capture policy of the workflow settings: how much of each payload is kept inline in run
/// records and when a payload is stored as an artifact.
/// </summary>
/// <param name="CaptureNodeOutputs">
/// True to store every node output as an artifact when its kind is allowed, even when it fits inline; false stores
/// only outputs and other payloads that were cut to the inline limit.
/// </param>
/// <param name="MaxInlinePayloadCharacters">
/// Maximum number of characters of a payload kept inline, after credential-like values are redacted; longer payloads
/// are cut and end with <c>...[TRUNCATED]</c>. Must be greater than zero; values above 1,000,000 are treated as
/// 1,000,000.
/// </param>
/// <param name="AllowedArtifactKinds">
/// Artifact kinds that may be stored, as JSON integers: 0 Text, 1 Json, 2 File, 3 Image, 4 Binary, 5 ToolReceipt,
/// 6 PreviewSimulation.
/// </param>
public sealed record WorkflowArtifactPolicy(
    bool CaptureNodeOutputs,
    int MaxInlinePayloadCharacters,
    IReadOnlyList<WorkflowArtifactKind> AllowedArtifactKinds);

/// <summary>
/// Human-in-the-loop policy of the workflow settings.
/// </summary>
/// <param name="AllowHumanInputNodes">
/// True to allow HumanInput nodes. When false, every definition containing one fails validation and can no longer be
/// saved, published or started.
/// </param>
/// <param name="RequireApprovalForToolUse">
/// Recorded preference for approving tool use. Executors take their approval requirement from their own permission
/// policy; the runtime does not read this flag.
/// </param>
/// <param name="DefaultRequestTimeoutMinutes">
/// Default waiting time for external requests, in minutes; must be greater than zero. It is recorded and displayed;
/// the runtime does not expire requests after it.
/// </param>
public sealed record WorkflowHumanInLoopPolicy(
    bool AllowHumanInputNodes,
    bool RequireApprovalForToolUse,
    int DefaultRequestTimeoutMinutes);

/// <summary>
/// The workflow settings document of this host, read with <c>GET /api/workflows/settings</c> and replaced as a whole
/// with <c>POST /api/workflows/settings</c>.
/// </summary>
/// <param name="DefaultRuntimePolicy">
/// Default runtime policy recorded for the host and shown by the workflow workspace; its preferred backend must be
/// runnable. Definitions and runs use their own <c>runtimePolicy</c>, not this value.
/// </param>
/// <param name="ArtifactPolicy">Payload and artifact capture policy for all runs.</param>
/// <param name="HumanInLoopPolicy">Human-in-the-loop policy for all definitions.</param>
/// <param name="VoiceSettings">
/// Agent voice settings (speech-to-text and text-to-speech), stored exactly as sent; <c>normalizedVoiceSettings</c>
/// shows the values in effect. Null or omitted stores none, which means the defaults: voice input and output
/// disabled.
/// </param>
public sealed record WorkflowSettings(
    WorkflowRuntimePolicy DefaultRuntimePolicy,
    WorkflowArtifactPolicy ArtifactPolicy,
    WorkflowHumanInLoopPolicy HumanInLoopPolicy,
    AgentVoiceSettings? VoiceSettings = null)
{
    public static WorkflowSettings Default { get; } = new(
        new WorkflowRuntimePolicy(
            WorkflowRuntimeBackendKind.InProcess,
            AllowInProcessPreviewRuns: true,
            RequireDurableProductionRuns: false,
            ExposeAzureFunctionsStatusEndpoint: false,
            ExposeAzureFunctionsMcpTool: false),
        new WorkflowArtifactPolicy(
            CaptureNodeOutputs: true,
            MaxInlinePayloadCharacters: 64_000,
            AllowedArtifactKinds:
            [
                WorkflowArtifactKind.Text,
                WorkflowArtifactKind.Json,
                WorkflowArtifactKind.File,
                WorkflowArtifactKind.ToolReceipt,
                WorkflowArtifactKind.PreviewSimulation
            ]),
        new WorkflowHumanInLoopPolicy(
            AllowHumanInputNodes: true,
            RequireApprovalForToolUse: true,
            DefaultRequestTimeoutMinutes: 240),
        AgentVoiceSettings.Default);

    /// <summary>
    /// The voice settings with defaults applied to missing or blank values; this is what agent voice mode uses.
    /// Computed; it is ignored when sent.
    /// </summary>
    public AgentVoiceSettings NormalizedVoiceSettings => AgentVoiceSettingsNormalizer.Normalize(VoiceSettings);
}

/// <summary>
/// Body of <c>POST /api/workflows/components</c>: the complete LLM call component to create or replace.
/// </summary>
/// <param name="Id">Identifier of the component to replace; null creates a new component.</param>
/// <param name="Name">Display name; required and trimmed.</param>
/// <param name="ProviderProfileId">
/// Provider profile to call, from <c>providerProfileId</c> of <c>GET /api/workflows/provider-options</c>; it must
/// exist, be enabled and be a chat provider. Null leaves the provider unset.
/// </param>
/// <param name="Model">
/// Model name within the provider, for example one of its <c>modelOptions</c>; required and trimmed. The provider must
/// have a price row for it.
/// </param>
/// <param name="Modality">
/// Input modality, as a JSON integer: 0 Text, 1 Vision, 2 Audio, 3 Image, 4 Multimodal. Audio and Image are rejected;
/// Vision and Multimodal need a provider with vision support.
/// </param>
/// <param name="ModelSettings">Temperature, output limit and JSON output settings.</param>
/// <param name="Instructions">
/// System instructions; required and trimmed. They are stored as a new Prompt Gallery prompt or prompt version, unless
/// <c>promptVersionId</c> is set, whose content is used instead.
/// </param>
/// <param name="InputShape">Shape of the value the component receives.</param>
/// <param name="ResultShape">Shape of the value the component produces.</param>
/// <param name="Permissions">Permissions granted to the model call, such as tools and secrets it may use.</param>
public sealed record LlmCallComponentSaveRequest(
    WorkflowComponentId? Id,
    string Name,
    Guid? ProviderProfileId,
    string Model,
    WorkflowModality Modality,
    WorkflowModelSettings ModelSettings,
    string Instructions,
    WorkflowValueShape InputShape,
    WorkflowValueShape ResultShape,
    AgentPermissionsPolicy Permissions)
{
    /// <summary>
    /// Prompt Gallery item to bind the instructions to; null keeps the existing binding, or creates a new prompt for a
    /// component that has none.
    /// </summary>
    public Guid? PromptArtifactId { get; init; }

    /// <summary>
    /// Prompt Gallery version whose content becomes the instructions; it must belong to <c>promptArtifactId</c> when
    /// both are set. Null records the sent instructions instead.
    /// </summary>
    public Guid? PromptVersionId { get; init; }
}

/// <summary>
/// A chat provider profile that LLM call components and nodes can use, returned by
/// <c>GET /api/workflows/provider-options</c>. It contains no credentials.
/// </summary>
/// <param name="ProviderProfileId">Identifier of the provider profile; use it as <c>providerProfileId</c>.</param>
/// <param name="Name">Display name of the provider profile.</param>
/// <param name="Kind">
/// Provider kind, as a JSON integer: 0 OpenAi, 1 AzureOpenAi, 2 Ollama, 3 ComfyUi.
/// </param>
/// <param name="Transport">
/// Protocol used to call the provider, as a JSON integer: 0 Responses, 1 ChatCompletions.
/// </param>
/// <param name="Purpose">
/// Purpose of the profile, as a JSON integer: 0 Chat, 1 ImageGeneration. Only Chat profiles are listed.
/// </param>
/// <param name="DefaultModel">Model used when a component names none.</param>
/// <param name="ModelOptions">The default model followed by the suggested models, without duplicates.</param>
/// <param name="IsEnabled">False when the profile is disabled; a component referencing it fails validation.</param>
/// <param name="SupportsStreaming">True when the provider can stream responses.</param>
/// <param name="SupportsTools">True when the provider can call tools.</param>
/// <param name="SupportsStructuredOutput">
/// True when the provider supports structured JSON output; true also when this is not known.
/// </param>
/// <param name="SupportsVision">True when the provider accepts images; true also when this is not known.</param>
/// <param name="SupportsBackgroundResponses">True when the provider supports background responses.</param>
public sealed record WorkflowProviderOption(
    Guid ProviderProfileId,
    string Name,
    ProviderKind Kind,
    ProviderTransportKind Transport,
    ProviderProfilePurpose Purpose,
    string DefaultModel,
    IReadOnlyList<string> ModelOptions,
    bool IsEnabled,
    bool SupportsStreaming,
    bool SupportsTools,
    bool SupportsStructuredOutput,
    bool SupportsVision,
    bool SupportsBackgroundResponses);

/// <summary>
/// Body of <c>POST /api/workflows/test-runs</c>: what to test and how. Identify the definition either with
/// <c>draftDefinition</c> or with both <c>workflowId</c> and <c>versionId</c>; the draft wins when both are sent.
/// </summary>
/// <param name="WorkflowId">Workflow of a stored version to test; used with <c>versionId</c>.</param>
/// <param name="VersionId">
/// Exact stored version to test; used with <c>workflowId</c>. It need not be Active, but the workflow's current version
/// must be Draft or Active.
/// </param>
/// <param name="DraftDefinition">
/// Unsaved definition to test; its <c>status</c> must be Draft (0) to be run. Null tests the stored version.
/// </param>
/// <param name="InputJson">
/// Run input as JSON text in a string, containing a JSON object; empty means <c>{}</c>. Ignored when
/// <c>validateOnly</c> is true.
/// </param>
/// <param name="RequestedBackend">
/// Backend to run on, as a JSON integer: 0 InProcess, 1 DurableTask, 2 AzureFunctions; null uses the definition's
/// preferred backend.
/// </param>
/// <param name="ValidateOnly">True to validate only; no run is created.</param>
public sealed record WorkflowTestRunRequest(
    WorkflowId? WorkflowId,
    WorkflowVersionId? VersionId,
    WorkflowDefinition? DraftDefinition,
    string InputJson,
    WorkflowRuntimeBackendKind? RequestedBackend,
    bool ValidateOnly)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public WorkflowStructureAuthority? StructureAuthority { get; init; }

    /// <summary>
    /// Nodes whose execution the preview run replaces with an output template; omitted or empty simulates nothing.
    /// </summary>
    public WorkflowPreviewSimulationPlan PreviewSimulationPlan { get; init; } = WorkflowPreviewSimulationPlan.Empty;
}

/// <summary>
/// Result of <c>POST /api/workflows/test-runs</c>, returned with HTTP 200 when <c>succeeded</c> is true and HTTP 400
/// otherwise. The run records are the stored records without the public safe projection.
/// </summary>
/// <param name="Succeeded">
/// True when validation passed (validate-only) or the preview run ended Completed, WaitingForInput or Idle.
/// </param>
/// <param name="Validation">
/// Validation issues of the definition, or the reason a launch was rejected reported as a single issue; empty when a
/// run was admitted.
/// </param>
/// <param name="Run">The preview run; null when no run was created.</param>
/// <param name="Events">Recorded events of the run, oldest first; empty without a run.</param>
/// <param name="Artifacts">Artifacts of the run; empty without a run.</param>
/// <param name="PendingExternalRequests">External requests of the run without a recorded response.</param>
/// <param name="ErrorMessage">Why the test did not succeed; empty on success.</param>
public sealed record WorkflowTestRunResult(
    bool Succeeded,
    WorkflowValidationResult Validation,
    WorkflowRunSnapshot? Run,
    IReadOnlyList<WorkflowEventRecord> Events,
    IReadOnlyList<WorkflowArtifactRecord> Artifacts,
    IReadOnlyList<WorkflowExternalRequestRecord> PendingExternalRequests,
    string ErrorMessage)
{
    /// <summary>Checkpoints of the run; empty without a run.</summary>
    public IReadOnlyList<WorkflowCheckpointRecord> Checkpoints { get; init; } = [];

    /// <summary>
    /// False when the run was admitted but its events, artifacts and requests could not be read; read the run instead.
    /// </summary>
    public bool DetailsComplete { get; init; } = true;

    /// <summary>
    /// How reliably the start was observed, as a JSON integer: 0 Confirmed, 1 RecoveredAfterObserverFailure (an
    /// error was reported after the run had been admitted; the result was rebuilt from the stored run), 2
    /// AdmissionReceiptPending.
    /// </summary>
    public WorkflowLaunchObservation Observation { get; init; }

    [System.Text.Json.Serialization.JsonIgnore]
    public Exception? ObservationException { get; init; }
}
