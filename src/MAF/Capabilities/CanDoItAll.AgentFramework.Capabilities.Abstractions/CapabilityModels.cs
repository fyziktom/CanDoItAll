namespace CanDoItAll.AgentFramework.Capabilities.Abstractions;

/// <summary>
/// Kind and key identifying a capability in setup tests and access previews.
/// </summary>
/// <param name="Kind">
/// Identity kind, as a JSON integer: 0 Skill, 1 Tool, 2 McpServer, 3 McpTool, 4 Plugin, 5 Rag, 6 AiContext, 7 Memory
/// (the identity numbering, not the catalog numbering).
/// </param>
/// <param name="Key">The capability key.</param>
public sealed record CapabilityIdentity(
    CapabilityKind Kind,
    CapabilityKey Key);

/// <summary>
/// Declared side effect of a capability.
/// </summary>
/// <param name="Kind">
/// Main side effect, as a JSON integer: 0 None, 1 WorkspaceRead, 2 WorkspaceWrite, 3 LocalProcessExecution,
/// 4 RuntimeLaunch, 5 RuntimeProofCapture, 6 ProcessMutation, 7 ProjectStructureMutation, 8 ExternalAction,
/// 9 MediaGeneration, 10 DocumentConversion, 11 ProviderNative, 12 McpTool, 13 InternalDataRead,
/// 14 InternalStateMutation.
/// </param>
/// <param name="RequiresApprovalByDefault">True when calls need an approval unless a policy says otherwise.</param>
/// <param name="IsStateChanging">True when calls can change state.</param>
public sealed record CapabilitySideEffectProfile(
    CapabilitySideEffectKind Kind,
    bool RequiresApprovalByDefault,
    bool IsStateChanging);

/// <summary>
/// A capability as a candidate for exposure to a run, with the properties that access policies match on.
/// </summary>
/// <param name="Identity">Kind and key of the capability, in the identity numbering.</param>
/// <param name="DisplayName">Display name.</param>
/// <param name="Description">Description.</param>
/// <param name="ImplementationKey">Key of the implementation behind the capability, or null.</param>
/// <param name="RuntimeToolName">Name of the runtime tool the capability exposes, or null.</param>
/// <param name="McpServerKey">Key of the MCP server the capability belongs to, or null.</param>
/// <param name="McpToolName">Name of the MCP tool, for an MCP tool capability; otherwise null.</param>
/// <param name="Tags">Tags of the capability.</param>
/// <param name="OperationClassifications">
/// Kinds of operation the capability performs, as JSON integers: 0 Read, 1 Write, 2 Mutation, 3 Validation,
/// 4 ScriptExecution, 5 BrowserAccess, 6 ProjectStructure, 7 DocumentProcessing, 8 ProviderNative, 9 ExternalAction,
/// 10 McpTool, 11 RuntimeLaunch, 12 ResourceCleanup.
/// </param>
/// <param name="SideEffectProfile">Declared side effect of the capability.</param>
/// <param name="AvailabilityState">
/// Whether the capability can be used, as a JSON integer: 0 Available, 1 Retired, 2 Unavailable, 3 FailedSetup (its
/// definition did not pass validation).
/// </param>
/// <param name="SourcePath">Path of the template the capability comes from, or null.</param>
public sealed record CapabilityExposureDescriptor(
    CapabilityIdentity Identity,
    string DisplayName,
    string Description,
    ImplementationKey? ImplementationKey,
    RuntimeToolName? RuntimeToolName,
    McpServerKey? McpServerKey,
    McpToolName? McpToolName,
    IReadOnlySet<CapabilityTag> Tags,
    IReadOnlySet<CapabilityOperationClassification> OperationClassifications,
    CapabilitySideEffectProfile SideEffectProfile,
    CapabilityAvailabilityState AvailabilityState,
    TemplatePath? SourcePath);

/// <summary>
/// A problem found while validating or testing a capability definition. Details are masked so that secret values
/// are not returned.
/// </summary>
/// <param name="Category">
/// Category, as a JSON integer: 0 TemplateValidation, 1 SecretBinding, 2 CommandPolicy, 3 ProcessStart,
/// 4 ProcessExit, 5 Timeout, 6 McpHandshake, 7 McpListTools, 8 SchemaValidation, 9 JsonParse, 10 HttpStatus,
/// 11 Cancellation, 12 ImplementationMissing, 13 RuntimeAdapter, 14 ResourceCleanup, 15 AccessPolicy,
/// 16 CapabilityUnavailable, 17 RequiredCapabilityDenied, 18 RuntimeDependency, 19 PackageSetup, 20 WorkingDirectory,
/// 21 UnsupportedPlatform, 22 PermissionDenied.
/// </param>
/// <param name="Severity">Severity, as a JSON integer: 0 Info, 1 Warning, 2 Error.</param>
/// <param name="CapabilityKind">
/// Identity kind of the affected capability, as a JSON integer (0 Skill, 1 Tool, 2 McpServer, 3 McpTool, 4 Plugin,
/// 5 Rag, 6 AiContext, 7 Memory), or null.
/// </param>
/// <param name="CapabilityKey">Key of the affected capability, or null.</param>
/// <param name="TemplatePath">Path of the affected template, or null.</param>
/// <param name="FieldPath">JSON path of the affected setting, for example <c>$.jsonInput</c>.</param>
/// <param name="ImplementationKey">Key of the affected implementation, or null.</param>
/// <param name="Transport">
/// How the capability is reached, as a JSON integer (0 InternalHosted, 1 InternalImplementation, 2 ExternalProcess,
/// 3 ExternalHttp, 4 LocalStdio, 5 RemoteHttp, 6 FileSkill, 7 InlineSkill, 8 RegisteredSkill), or null.
/// </param>
/// <param name="ExitCode">Exit code of a tested process, or null.</param>
/// <param name="HttpStatusCode">HTTP status code returned by a tested endpoint, or null.</param>
/// <param name="Timeout">Timeout that was exceeded, as a time span string, or null.</param>
/// <param name="CorrelationId">Correlation identifier of the test or preview.</param>
/// <param name="MaskedDetail">Description of the problem with secret values masked.</param>
/// <param name="RepairHint">Suggested fix.</param>
public sealed record CapabilityDiagnostic(
    CapabilityDiagnosticCategory Category,
    CapabilityValidationSeverity Severity,
    CapabilityKind? CapabilityKind,
    CapabilityKey? CapabilityKey,
    TemplatePath? TemplatePath,
    string FieldPath,
    ImplementationKey? ImplementationKey,
    CapabilityTransportKind? Transport,
    int? ExitCode,
    int? HttpStatusCode,
    TimeSpan? Timeout,
    string CorrelationId,
    string MaskedDetail,
    string RepairHint);

/// <summary>
/// A validation problem of a capability definition or an access policy.
/// </summary>
/// <param name="Category">
/// Category, as a JSON integer: 0 TemplateValidation, 1 SecretBinding, 2 CommandPolicy, 3 ProcessStart,
/// 4 ProcessExit, 5 Timeout, 6 McpHandshake, 7 McpListTools, 8 SchemaValidation, 9 JsonParse, 10 HttpStatus,
/// 11 Cancellation, 12 ImplementationMissing, 13 RuntimeAdapter, 14 ResourceCleanup, 15 AccessPolicy,
/// 16 CapabilityUnavailable, 17 RequiredCapabilityDenied, 18 RuntimeDependency, 19 PackageSetup, 20 WorkingDirectory,
/// 21 UnsupportedPlatform, 22 PermissionDenied.
/// </param>
/// <param name="Severity">Severity, as a JSON integer: 0 Info, 1 Warning, 2 Error.</param>
/// <param name="CapabilityKind">
/// Identity kind of the affected capability, as a JSON integer (0 Skill, 1 Tool, 2 McpServer, 3 McpTool, 4 Plugin,
/// 5 Rag, 6 AiContext, 7 Memory), or null.
/// </param>
/// <param name="CapabilityKey">Key of the affected capability, or null.</param>
/// <param name="TemplatePath">Path of the affected template, or null.</param>
/// <param name="FieldPath">JSON path of the affected setting.</param>
/// <param name="Message">Description of the problem.</param>
/// <param name="RepairHint">Suggested fix.</param>
public sealed record CapabilityValidationIssue(
    CapabilityDiagnosticCategory Category,
    CapabilityValidationSeverity Severity,
    CapabilityKind? CapabilityKind,
    CapabilityKey? CapabilityKey,
    TemplatePath? TemplatePath,
    string FieldPath,
    string Message,
    string RepairHint);

/// <summary>
/// Validation result of capability definitions or an access policy.
/// </summary>
/// <param name="Issues">The problems found; empty when there are none.</param>
public sealed record CapabilityValidationResult(IReadOnlyList<CapabilityValidationIssue> Issues)
{
    /// <summary>Set by the server: true when no issue has severity 2 Error.</summary>
    public bool IsValid => Issues.All(issue => issue.Severity != CapabilityValidationSeverity.Error);

    public static CapabilityValidationResult Passed { get; } = new([]);
}

/// <summary>
/// Result of a tool capability setup test.
/// </summary>
/// <param name="IsSuccess">True when the definition was valid and the test call succeeded.</param>
/// <param name="Identity">Kind and key of the tested capability, in the identity numbering.</param>
/// <param name="CorrelationId">Correlation identifier of the test.</param>
/// <param name="Diagnostics">Problems found; empty when the test succeeded.</param>
public sealed record CapabilitySetupTestResult(
    bool IsSuccess,
    CapabilityIdentity Identity,
    string CorrelationId,
    IReadOnlyList<CapabilityDiagnostic> Diagnostics);

public sealed record CapabilitySelector(
    CapabilitySelectorKind Kind,
    CapabilityKind? CapabilityKind = null,
    CapabilityKey? CapabilityKey = null,
    CapabilityTag? Tag = null,
    CapabilityOperationClassification? OperationClassification = null,
    RuntimeToolName? RuntimeToolName = null,
    McpServerKey? McpServerKey = null,
    McpToolName? McpToolName = null,
    ImplementationKey? ImplementationKey = null)
{
    public static CapabilitySelector All { get; } = new(CapabilitySelectorKind.All);

    public static CapabilitySelector ByKind(CapabilityKind kind)
        => new(CapabilitySelectorKind.Kind, CapabilityKind: kind);

    public static CapabilitySelector ByCapabilityKey(CapabilityKey key)
        => new(CapabilitySelectorKind.CapabilityKey, CapabilityKey: key);

    public static CapabilitySelector ByTag(CapabilityTag tag)
        => new(CapabilitySelectorKind.Tag, Tag: tag);

    public static CapabilitySelector ByOperationClassification(CapabilityOperationClassification classification)
        => new(CapabilitySelectorKind.OperationClassification, OperationClassification: classification);

    public static CapabilitySelector ByRuntimeToolName(RuntimeToolName name)
        => new(CapabilitySelectorKind.RuntimeToolName, RuntimeToolName: name);

    public static CapabilitySelector ByMcpServerKey(McpServerKey key)
        => new(CapabilitySelectorKind.McpServerKey, McpServerKey: key);

    public static CapabilitySelector ByMcpToolName(McpServerKey serverKey, McpToolName toolName)
        => new(CapabilitySelectorKind.McpToolName, McpServerKey: serverKey, McpToolName: toolName);

    public static CapabilitySelector ByImplementationKey(ImplementationKey key)
        => new(CapabilitySelectorKind.ImplementationKey, ImplementationKey: key);
}

public sealed record CapabilityAccessRule(
    CapabilityRuleId Id,
    CapabilityAccessEffect Effect,
    CapabilityAccessScope Scope,
    CapabilitySelector Selector,
    string Reason);

public sealed record CapabilityAccessPolicy(
    IReadOnlyList<CapabilityAccessRule> Rules,
    CapabilityAccessDefaultEffect DefaultEffect = CapabilityAccessDefaultEffect.Inherit,
    CapabilityAccessScope? DefaultScope = null,
    string DefaultReason = "");

public sealed record CapabilityAccessEvaluationContext(
    IReadOnlyList<CapabilityExposureDescriptor> CandidateCapabilities,
    IReadOnlyList<CapabilityIdentity> RequiredCapabilities,
    IReadOnlyList<CapabilityAccessPolicy> Policies,
    string CorrelationId);

/// <summary>
/// Reason why an access policy suppressed a capability, or why a required capability is missing.
/// </summary>
/// <param name="Identity">
/// Kind and key of the capability, in the identity numbering. For a require rule that matched no allowed candidate
/// it is the placeholder kind 1 Tool with key <c>required-capability-missing</c>.
/// </param>
/// <param name="RuleId">Identifier of the rule that decided, or null when no rule matched.</param>
/// <param name="Scope">
/// Level of that rule, as a JSON integer (0 System, 1 AgentDefault, 2 WorkflowDefinition, 3 WorkflowNode,
/// 4 ProcessDefinition, 5 ProcessStep, 6 RuntimeOverride, 7 UiPreview), or null.
/// </param>
/// <param name="SelectorKind">
/// What that rule matched on, as a JSON integer (0 All, 1 Kind, 2 CapabilityKey, 3 Tag, 4 OperationClassification,
/// 5 RuntimeToolName, 6 McpServerKey, 7 McpToolName, 8 ImplementationKey), or null.
/// </param>
/// <param name="Category">
/// Category, as a JSON integer, for example 15 AccessPolicy, 16 CapabilityUnavailable or 17 RequiredCapabilityDenied;
/// the full list is given for the <c>category</c> of a capability diagnostic.
/// </param>
/// <param name="Reason">Why the capability was suppressed.</param>
/// <param name="RepairHint">Suggested fix.</param>
/// <param name="CorrelationId">Correlation identifier of the preview.</param>
public sealed record SuppressedCapabilityDiagnostic(
    CapabilityIdentity Identity,
    CapabilityRuleId? RuleId,
    CapabilityAccessScope? Scope,
    CapabilitySelectorKind? SelectorKind,
    CapabilityDiagnosticCategory Category,
    string Reason,
    string RepairHint,
    string CorrelationId);

public sealed record CapabilityAccessEvaluationResult(
    IReadOnlyList<CapabilityExposureDescriptor> AllowedCapabilities,
    IReadOnlyList<SuppressedCapabilityDiagnostic> Diagnostics)
{
    public EffectiveCapabilitySet ToEffectiveSet()
        => new(AllowedCapabilities, Diagnostics);
}

/// <summary>
/// Capabilities an access policy allows and the reasons for the ones it suppresses.
/// </summary>
/// <param name="AllowedCapabilities">The allowed capabilities.</param>
/// <param name="Diagnostics">Reasons for suppressed or missing capabilities.</param>
public sealed record EffectiveCapabilitySet(
    IReadOnlyList<CapabilityExposureDescriptor> AllowedCapabilities,
    IReadOnlyList<SuppressedCapabilityDiagnostic> Diagnostics);

public interface ICapabilityAccessPolicyEvaluator
{
    CapabilityAccessEvaluationResult Evaluate(CapabilityAccessEvaluationContext context);
}
