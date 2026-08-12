namespace CanDoItAll.AgentFramework.Capabilities.Abstractions;

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

public enum CapabilityAccessEffect
{
    Inherit,
    Allow,
    Deny,
    Require
}

public enum CapabilityAccessDefaultEffect
{
    Inherit,
    AllowAssigned,
    DenyAll
}

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

public enum CapabilityValidationSeverity
{
    Info,
    Warning,
    Error
}

public enum CapabilityAvailabilityState
{
    Available,
    Retired,
    Unavailable,
    FailedSetup
}
