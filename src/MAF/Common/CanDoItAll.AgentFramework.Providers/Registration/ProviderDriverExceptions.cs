using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Providers;

public sealed class UnsupportedProviderCapabilityException : InvalidOperationException
{
    public UnsupportedProviderCapabilityException(
        ProviderKind providerKind,
        AgentProviderCapabilityKind capability)
        : base($"Provider kind '{providerKind}' does not have a registered '{capability}' driver.")
    {
        ProviderKind = providerKind;
        Capability = capability;
    }

    public ProviderKind ProviderKind { get; }

    public AgentProviderCapabilityKind Capability { get; }
}

public sealed class DuplicateProviderCapabilityRegistrationException : InvalidOperationException
{
    public DuplicateProviderCapabilityRegistrationException(
        ProviderKind providerKind,
        AgentProviderCapabilityKind capability,
        Type existingDriverType,
        Type duplicateDriverType)
        : base($"Provider kind '{providerKind}' already has a registered '{capability}' driver: '{existingDriverType.Name}'. Duplicate: '{duplicateDriverType.Name}'.")
    {
        ProviderKind = providerKind;
        Capability = capability;
        ExistingDriverType = existingDriverType;
        DuplicateDriverType = duplicateDriverType;
    }

    public ProviderKind ProviderKind { get; }

    public AgentProviderCapabilityKind Capability { get; }

    public Type ExistingDriverType { get; }

    public Type DuplicateDriverType { get; }
}
