namespace CanDoItAll.AgentFramework.Capabilities.Abstractions;

/// <summary>
/// Kind of a capability identity, used by setup tests and access previews, as a JSON integer: 0 Skill, 1 Tool,
/// 2 McpServer, 3 McpTool (one tool of an MCP server), 4 Plugin, 5 Rag, 6 AiContext, 7 Memory. The schema name
/// <c>CapabilityKind</c> is shared with the catalog kind of catalog capabilities and capability forms, which numbers
/// kinds differently (0 McpServer, 1 Skill, 2 Tool, 3 Plugin, 4 Rag, 5 AiContext, 6 Memory); each member's
/// description states the numbering it uses.
/// </summary>
public enum CapabilityKind
{
    Skill,
    Tool,
    McpServer,
    McpTool,
    Plugin,
    Rag,
    AiContext,
    Memory
}

/// <summary>
/// Effect of a capability access rule, as a JSON integer: 0 Inherit, 1 Allow, 2 Deny (wins over any allow),
/// 3 Require (allow, and report a diagnostic when nothing matches).
/// </summary>
public enum CapabilityAccessEffect
{
    Inherit,
    Allow,
    Deny,
    Require
}

/// <summary>
/// What an access policy does with capabilities no rule matches, as a JSON integer: 0 Inherit, 1 AllowAssigned,
/// 2 DenyAll.
/// </summary>
public enum CapabilityAccessDefaultEffect
{
    Inherit,
    AllowAssigned,
    DenyAll
}

/// <summary>
/// What a capability access rule matches on, as a JSON integer: 0 All, 1 Kind, 2 CapabilityKey, 3 Tag,
/// 4 OperationClassification, 5 RuntimeToolName, 6 McpServerKey, 7 McpToolName, 8 ImplementationKey.
/// </summary>
public enum CapabilitySelectorKind
{
    All,
    Kind,
    CapabilityKey,
    Tag,
    OperationClassification,
    RuntimeToolName,
    McpServerKey,
    McpToolName,
    ImplementationKey
}

/// <summary>
/// Level at which a capability access rule is declared, as a JSON integer: 0 System, 1 AgentDefault,
/// 2 WorkflowDefinition, 3 WorkflowNode, 4 ProcessDefinition, 5 ProcessStep, 6 RuntimeOverride, 7 UiPreview.
/// </summary>
public enum CapabilityAccessScope
{
    System,
    AgentDefault,
    WorkflowDefinition,
    WorkflowNode,
    ProcessDefinition,
    ProcessStep,
    RuntimeOverride,
    UiPreview
}

/// <summary>
/// Kind of operation a capability performs, as a JSON integer: 0 Read, 1 Write, 2 Mutation, 3 Validation,
/// 4 ScriptExecution, 5 BrowserAccess, 6 ProjectStructure, 7 DocumentProcessing, 8 ProviderNative, 9 ExternalAction,
/// 10 McpTool, 11 RuntimeLaunch, 12 ResourceCleanup.
/// </summary>
public enum CapabilityOperationClassification
{
    Read,
    Write,
    Mutation,
    Validation,
    ScriptExecution,
    BrowserAccess,
    ProjectStructure,
    DocumentProcessing,
    ProviderNative,
    ExternalAction,
    McpTool,
    RuntimeLaunch,
    ResourceCleanup
}

/// <summary>
/// Main side effect of a capability, as a JSON integer: 0 None, 1 WorkspaceRead, 2 WorkspaceWrite,
/// 3 LocalProcessExecution, 4 RuntimeLaunch, 5 RuntimeProofCapture, 6 ProcessMutation, 7 ProjectStructureMutation,
/// 8 ExternalAction, 9 MediaGeneration, 10 DocumentConversion, 11 ProviderNative, 12 McpTool, 13 InternalDataRead,
/// 14 InternalStateMutation.
/// </summary>
public enum CapabilitySideEffectKind
{
    None,
    WorkspaceRead,
    WorkspaceWrite,
    LocalProcessExecution,
    RuntimeLaunch,
    RuntimeProofCapture,
    ProcessMutation,
    ProjectStructureMutation,
    ExternalAction,
    MediaGeneration,
    DocumentConversion,
    ProviderNative,
    McpTool,
    InternalDataRead,
    InternalStateMutation
}

/// <summary>
/// How a capability is reached, as a JSON integer: 0 InternalHosted, 1 InternalImplementation, 2 ExternalProcess,
/// 3 ExternalHttp, 4 LocalStdio (a local MCP server process), 5 RemoteHttp (a remote MCP server), 6 FileSkill,
/// 7 InlineSkill, 8 RegisteredSkill.
/// </summary>
public enum CapabilityTransportKind
{
    InternalHosted,
    InternalImplementation,
    ExternalProcess,
    ExternalHttp,
    LocalStdio,
    RemoteHttp,
    FileSkill,
    InlineSkill,
    RegisteredSkill
}

/// <summary>
/// Category of a capability diagnostic, as a JSON integer: 0 TemplateValidation, 1 SecretBinding, 2 CommandPolicy,
/// 3 ProcessStart, 4 ProcessExit, 5 Timeout, 6 McpHandshake, 7 McpListTools, 8 SchemaValidation, 9 JsonParse,
/// 10 HttpStatus, 11 Cancellation, 12 ImplementationMissing, 13 RuntimeAdapter, 14 ResourceCleanup, 15 AccessPolicy,
/// 16 CapabilityUnavailable, 17 RequiredCapabilityDenied, 18 RuntimeDependency, 19 PackageSetup, 20 WorkingDirectory,
/// 21 UnsupportedPlatform, 22 PermissionDenied.
/// </summary>
public enum CapabilityDiagnosticCategory
{
    TemplateValidation,
    SecretBinding,
    CommandPolicy,
    ProcessStart,
    ProcessExit,
    Timeout,
    McpHandshake,
    McpListTools,
    SchemaValidation,
    JsonParse,
    HttpStatus,
    Cancellation,
    ImplementationMissing,
    RuntimeAdapter,
    ResourceCleanup,
    AccessPolicy,
    CapabilityUnavailable,
    RequiredCapabilityDenied,
    RuntimeDependency,
    PackageSetup,
    WorkingDirectory,
    UnsupportedPlatform,
    PermissionDenied
}

/// <summary>
/// Severity of a capability diagnostic or validation issue, as a JSON integer: 0 Info, 1 Warning, 2 Error; only Error
/// makes a result invalid.
/// </summary>
public enum CapabilityValidationSeverity
{
    Info,
    Warning,
    Error
}

/// <summary>
/// Whether a capability can be used, as a JSON integer: 0 Available, 1 Retired, 2 Unavailable, 3 FailedSetup (its
/// definition did not pass validation).
/// </summary>
public enum CapabilityAvailabilityState
{
    Available,
    Retired,
    Unavailable,
    FailedSetup
}
