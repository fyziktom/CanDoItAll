using System.Text.Json.Serialization;

namespace CanDoItAll.Processes.Contracts;

public sealed class ProcessCapabilityScope
{
    public static ProcessCapabilityScope Empty => new();

    public List<ProcessCapabilityScopeDirective> Directives { get; set; } = [];

    public List<ProcessScopedInstructionFragment> InstructionFragments { get; set; } = [];

    public bool IsEmpty => Directives.Count == 0 && InstructionFragments.Count == 0;

    public static ProcessCapabilityScope Normalize(ProcessCapabilityScope? scope)
    {
        if (scope is null)
        {
            return Empty;
        }

        return new ProcessCapabilityScope
        {
            Directives = scope.Directives
                .Where(directive => directive.Target.Kind != ProcessCapabilityScopeTargetKind.Unspecified)
                .Select(NormalizeDirective)
                .ToList(),
            InstructionFragments = scope.InstructionFragments
                .Select(NormalizeInstructionFragment)
                .Where(fragment => !string.IsNullOrWhiteSpace(fragment.Content))
                .ToList()
        };
    }

    private static ProcessCapabilityScopeDirective NormalizeDirective(ProcessCapabilityScopeDirective directive)
    {
        return new ProcessCapabilityScopeDirective
        {
            Kind = directive.Kind,
            Target = new ProcessCapabilityScopeTarget
            {
                Kind = directive.Target.Kind,
                Value = directive.Target.Value.Trim(),
                SecondaryValue = directive.Target.SecondaryValue.Trim()
            },
            Reason = directive.Reason.Trim()
        };
    }

    private static ProcessScopedInstructionFragment NormalizeInstructionFragment(ProcessScopedInstructionFragment fragment)
    {
        return new ProcessScopedInstructionFragment
        {
            Key = fragment.Key.Trim(),
            Title = fragment.Title.Trim(),
            Content = fragment.Content.Trim(),
            Placement = fragment.Placement
        };
    }
}

public sealed class ProcessCapabilityScopeDirective
{
    public ProcessCapabilityScopeDirectiveKind Kind { get; set; } = ProcessCapabilityScopeDirectiveKind.Deny;

    public ProcessCapabilityScopeTarget Target { get; set; } = new();

    public string Reason { get; set; } = string.Empty;
}

public sealed class ProcessCapabilityScopeTarget
{
    public ProcessCapabilityScopeTargetKind Kind { get; set; } = ProcessCapabilityScopeTargetKind.Unspecified;

    public string Value { get; set; } = string.Empty;

    public string SecondaryValue { get; set; } = string.Empty;
}

public sealed class ProcessScopedInstructionFragment
{
    public string Key { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public ProcessScopedInstructionPlacement Placement { get; set; } = ProcessScopedInstructionPlacement.AppendToStepBrief;
}

[JsonConverter(typeof(JsonStringEnumConverter<ProcessCapabilityScopeDirectiveKind>))]
public enum ProcessCapabilityScopeDirectiveKind
{
    Allow,
    AllowOnly,
    Deny,
    Require
}

[JsonConverter(typeof(JsonStringEnumConverter<ProcessCapabilityScopeTargetKind>))]
public enum ProcessCapabilityScopeTargetKind
{
    Unspecified,
    All,
    CapabilityKind,
    CapabilityKey,
    CapabilityIdentity,
    CapabilityTag,
    RuntimeToolName,
    RuntimeToolProviderKey,
    McpServerKey,
    McpToolName,
    ImplementationKey,
    OperationClassification
}

[JsonConverter(typeof(JsonStringEnumConverter<ProcessScopedInstructionPlacement>))]
public enum ProcessScopedInstructionPlacement
{
    AppendToStepBrief
}
