using CanDoItAll.AgentFramework.Capabilities.Abstractions;

namespace CanDoItAll.AgentFramework.Skills.Abstractions;

public enum SkillDescriptorKind
{
    File,
    Inline,
    Registered
}

public enum SkillScriptTrustLevel
{
    WorkspaceSkillRoot,
    ExternalSkillRoot,
    InlineSkill,
    RegisteredSkill
}

public abstract record SkillDescriptor(
    SkillDescriptorKind DescriptorKind,
    CapabilityIdentity Identity,
    string DisplayName,
    string Description,
    IReadOnlySet<CapabilityTag> Tags,
    IReadOnlySet<CapabilityOperationClassification> OperationClassifications,
    CapabilitySideEffectProfile SideEffectProfile,
    CapabilityAvailabilityState AvailabilityState);

public sealed record FileSkillDescriptor(
    CapabilityIdentity Identity,
    string DisplayName,
    string Description,
    IReadOnlySet<CapabilityTag> Tags,
    IReadOnlySet<CapabilityOperationClassification> OperationClassifications,
    CapabilitySideEffectProfile SideEffectProfile,
    CapabilityAvailabilityState AvailabilityState,
    string SkillRoot,
    IReadOnlySet<string> AllowedExternalRoots,
    SkillScriptExecutionPolicy ScriptExecutionPolicy)
    : SkillDescriptor(
        SkillDescriptorKind.File,
        Identity,
        DisplayName,
        Description,
        Tags,
        OperationClassifications,
        SideEffectProfile,
        AvailabilityState);

public sealed record InlineSkillDescriptor(
    CapabilityIdentity Identity,
    string DisplayName,
    string Description,
    IReadOnlySet<CapabilityTag> Tags,
    IReadOnlySet<CapabilityOperationClassification> OperationClassifications,
    CapabilitySideEffectProfile SideEffectProfile,
    CapabilityAvailabilityState AvailabilityState,
    string SkillName,
    string Instructions,
    IReadOnlyList<InlineSkillResource> Resources)
    : SkillDescriptor(
        SkillDescriptorKind.Inline,
        Identity,
        DisplayName,
        Description,
        Tags,
        OperationClassifications,
        SideEffectProfile,
        AvailabilityState);

public sealed record RegisteredSkillDescriptor(
    CapabilityIdentity Identity,
    string DisplayName,
    string Description,
    IReadOnlySet<CapabilityTag> Tags,
    IReadOnlySet<CapabilityOperationClassification> OperationClassifications,
    CapabilitySideEffectProfile SideEffectProfile,
    CapabilityAvailabilityState AvailabilityState,
    ImplementationKey RegisteredSkillKey)
    : SkillDescriptor(
        SkillDescriptorKind.Registered,
        Identity,
        DisplayName,
        Description,
        Tags,
        OperationClassifications,
        SideEffectProfile,
        AvailabilityState);

public sealed record SkillScriptExecutionPolicy(
    bool ApprovalRequired,
    SkillScriptTrustLevel TrustLevel);

public sealed record InlineSkillResource(
    string Name,
    string Content,
    string? Description);

public sealed record LoadedSkill(
    CapabilityIdentity Identity,
    SkillDescriptorKind SourceKind,
    string Name,
    string Description,
    string Instructions,
    IReadOnlyList<InlineSkillResource> Resources,
    string? SourcePath,
    ImplementationKey? RegisteredSkillKey,
    SkillScriptExecutionPolicy? ScriptExecutionPolicy);

public sealed record SkillLoadResult(
    bool IsSuccess,
    LoadedSkill? Skill,
    string CorrelationId,
    IReadOnlyList<CapabilityDiagnostic> Diagnostics)
{
    public static SkillLoadResult Success(LoadedSkill skill, string correlationId)
        => new(true, skill, correlationId, []);

    public static SkillLoadResult Failure(string correlationId, IReadOnlyList<CapabilityDiagnostic> diagnostics)
        => new(false, null, correlationId, diagnostics);
}

public sealed record RegisteredSkillBinding(
    ImplementationKey RegisteredSkillKey,
    Func<RegisteredSkillDescriptor, string, SkillLoadResult> Resolve);

public interface IFileSkillLoader
{
    Task<SkillLoadResult> LoadAsync(
        FileSkillDescriptor descriptor,
        string correlationId,
        CancellationToken cancellationToken);
}

public interface IInlineSkillLoader
{
    Task<SkillLoadResult> LoadAsync(
        InlineSkillDescriptor descriptor,
        string correlationId,
        CancellationToken cancellationToken);
}

public interface IRegisteredSkillRegistry
{
    void Register(RegisteredSkillBinding binding);

    bool TryResolve(ImplementationKey registeredSkillKey, out RegisteredSkillBinding binding);

    IReadOnlyList<RegisteredSkillBinding> List();
}

public interface IRegisteredSkillResolver
{
    Task<SkillLoadResult> ResolveAsync(
        RegisteredSkillDescriptor descriptor,
        string correlationId,
        CancellationToken cancellationToken);
}
