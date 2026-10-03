using System.Collections.Immutable;
using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Mcp.Abstractions;
using CanDoItAll.AgentFramework.Models;
using CapabilityKind = CanDoItAll.AgentFramework.Models.CapabilityKind;

namespace CanDoItAll.AgentFramework.CapabilityAuthoring.UI;

public sealed record CapabilityAuthoringSubmission(
    Guid? Id, string? ExpectedFingerprint, CapabilityKind Kind, string Key, string Name,
    string Description, string EndpointOrPath, string ConfigurationJson, bool IsBuiltIn,
    ImmutableArray<string> Tags) {
    public static CapabilityAuthoringSubmission Capture(CapabilityEditorModel model) => new(
        model.Id, model.ExpectedFingerprint, model.Kind, model.Key, model.Name, model.Description,
        model.EndpointOrPath, model.ConfigurationJson, model.IsBuiltIn, [.. model.Tags]);

    public CapabilityEditorModel ToEditorModel() => new() {
        Id = Id, ExpectedFingerprint = ExpectedFingerprint, Kind = Kind, Key = Key, Name = Name,
        Description = Description, EndpointOrPath = EndpointOrPath, ConfigurationJson = ConfigurationJson,
        IsBuiltIn = IsBuiltIn, Tags = [.. Tags]
    };
}

public abstract record CapabilityAuthoringSaveOutcome {
    public sealed record Accepted(CapabilityAuthoringSubmission Definition) : CapabilityAuthoringSaveOutcome;
    public sealed record Rejected(bool IsConflict) : CapabilityAuthoringSaveOutcome;
    public sealed record Unknown : CapabilityAuthoringSaveOutcome;
}

public sealed record CapabilityAuthoringOperations(
    Func<Guid, CancellationToken, Task<CapabilityEditorModel>> Load,
    Func<CapabilityAuthoringSubmission, CancellationToken, Task<CapabilityAuthoringSaveOutcome>> Save,
    Func<CapabilityAuthoringSubmission, string, CancellationToken, Task<CapabilitySetupTestResult>> TestTool,
    Func<CapabilityAuthoringSubmission, CancellationToken, Task<McpSetupTestResult>> TestMcp,
    StringComparer EnvironmentNameComparer);

public enum CapabilityAuthoringMode { Wizard, Details }
public enum CapabilitySkillInputMode { FilePath, Inline, Upload, Registered }
