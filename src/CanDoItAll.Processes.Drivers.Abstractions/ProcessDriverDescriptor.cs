namespace CanDoItAll.Processes.Drivers.Abstractions;

public sealed record ProcessDriverDescriptor(
    string Key,
    string DisplayName,
    IReadOnlySet<string> CapabilityTags);
