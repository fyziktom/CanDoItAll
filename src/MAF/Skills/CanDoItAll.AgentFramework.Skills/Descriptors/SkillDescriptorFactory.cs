using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Skills.Abstractions;

namespace CanDoItAll.AgentFramework.Skills;

public static class SkillDescriptorFactory
{
    public static FileSkillDescriptor File(
        CapabilityKey key,
        string displayName,
        string description,
        string skillRoot,
        IEnumerable<string> allowedExternalRoots,
        SkillScriptExecutionPolicy scriptExecutionPolicy,
        IEnumerable<CapabilityTag>? tags = null,
        IEnumerable<CapabilityOperationClassification>? operationClassifications = null,
        CapabilityAvailabilityState availabilityState = CapabilityAvailabilityState.Available)
    {
        var classifications = NormalizeClassifications(operationClassifications ?? []);
        if (scriptExecutionPolicy.ApprovalRequired)
        {
            classifications.Add(CapabilityOperationClassification.ScriptExecution);
        }

        return new FileSkillDescriptor(
            Identity(key),
            RequireText(displayName, nameof(displayName)),
            RequireText(description, nameof(description)),
            NormalizeTags([CapabilityTag.Create("skill"), CapabilityTag.Create("file")], tags),
            classifications,
            new CapabilitySideEffectProfile(
                CapabilitySideEffectKind.LocalProcessExecution,
                scriptExecutionPolicy.ApprovalRequired,
                false),
            availabilityState,
            RequireText(skillRoot, nameof(skillRoot)),
            NormalizeStringSet(allowedExternalRoots),
            scriptExecutionPolicy);
    }

    public static InlineSkillDescriptor Inline(
        CapabilityKey key,
        string displayName,
        string description,
        string skillName,
        string instructions,
        IReadOnlyList<InlineSkillResource> resources,
        IEnumerable<CapabilityTag>? tags = null,
        IEnumerable<CapabilityOperationClassification>? operationClassifications = null,
        CapabilityAvailabilityState availabilityState = CapabilityAvailabilityState.Available)
    {
        return new InlineSkillDescriptor(
            Identity(key),
            RequireText(displayName, nameof(displayName)),
            RequireText(description, nameof(description)),
            NormalizeTags([CapabilityTag.Create("skill"), CapabilityTag.Create("inline")], tags),
            NormalizeClassifications(operationClassifications ?? []),
            new CapabilitySideEffectProfile(CapabilitySideEffectKind.None, false, false),
            availabilityState,
            SkillName.Create(skillName).Value,
            instructions,
            resources);
    }

    public static RegisteredSkillDescriptor Registered(
        CapabilityKey key,
        string displayName,
        string description,
        ImplementationKey registeredSkillKey,
        IEnumerable<CapabilityTag>? tags = null,
        IEnumerable<CapabilityOperationClassification>? operationClassifications = null,
        CapabilityAvailabilityState availabilityState = CapabilityAvailabilityState.Available)
    {
        return new RegisteredSkillDescriptor(
            Identity(key),
            RequireText(displayName, nameof(displayName)),
            RequireText(description, nameof(description)),
            NormalizeTags([CapabilityTag.Create("skill"), CapabilityTag.Create("registered")], tags),
            NormalizeClassifications(operationClassifications ?? []),
            new CapabilitySideEffectProfile(CapabilitySideEffectKind.None, false, false),
            availabilityState,
            registeredSkillKey);
    }

    private static CapabilityIdentity Identity(CapabilityKey key)
        => new(CapabilityKind.Skill, key);

    private static IReadOnlySet<CapabilityTag> NormalizeTags(
        IEnumerable<CapabilityTag> requiredTags,
        IEnumerable<CapabilityTag>? providedTags)
    {
        var tags = requiredTags.ToHashSet();
        foreach (var tag in providedTags ?? [])
        {
            tags.Add(tag);
        }

        return tags;
    }

    private static HashSet<CapabilityOperationClassification> NormalizeClassifications(
        IEnumerable<CapabilityOperationClassification> classifications)
        => classifications.ToHashSet();

    private static IReadOnlySet<string> NormalizeStringSet(IEnumerable<string> values)
        => values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static string RequireText(string value, string parameterName)
        => string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Value is required.", parameterName)
            : value.Trim();
}
