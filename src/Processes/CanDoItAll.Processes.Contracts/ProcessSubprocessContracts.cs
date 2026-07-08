using System.Text.Json.Serialization;

namespace CanDoItAll.Processes.Contracts;

public static class ProcessSubprocessContractModes
{
    public const string RuntimeOwned = nameof(ProcessSubprocessLaunchMode.RuntimeOwned);
    public const string RuntimeSynthesizedParentHandoff = nameof(ProcessSubprocessMaterializationMode.RuntimeSynthesizedParentHandoff);
}

[JsonConverter(typeof(JsonStringEnumConverter<ProcessSubprocessLaunchMode>))]
public enum ProcessSubprocessLaunchMode
{
    RuntimeOwned
}

[JsonConverter(typeof(JsonStringEnumConverter<ProcessSubprocessMaterializationMode>))]
public enum ProcessSubprocessMaterializationMode
{
    RuntimeSynthesizedParentHandoff
}

public sealed class ProcessSubprocessContract
{
    public string DefinitionKey { get; set; } = string.Empty;

    public ProcessSubprocessLaunchMode LaunchMode { get; set; } = ProcessSubprocessLaunchMode.RuntimeOwned;

    public string ParentProducedArtifactExpectationKey { get; set; } = string.Empty;

    public List<ProcessSubprocessChildOutputContract> AcceptedChildOutputs { get; set; } = [];

    public List<ProcessSubprocessChildOutputContract> NoGoChildOutputs { get; set; } = [];

    public List<ProcessSubprocessRequiredReceiptContract> RequiredChildReceipts { get; set; } = [];

    public ProcessSubprocessChildOutputContract? AlreadySatisfiedOutput { get; set; }

    public ProcessSubprocessMaterializationMode MaterializationMode { get; set; } =
        ProcessSubprocessMaterializationMode.RuntimeSynthesizedParentHandoff;
}

public sealed class ProcessSubprocessChildOutputContract
{
    public string StepKey { get; set; } = string.Empty;

    public string ArtifactExpectationKey { get; set; } = string.Empty;

    public string ArtifactTitle { get; set; } = string.Empty;

    public string BranchOutcomeKey { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;
}

public sealed class ProcessSubprocessRequiredReceiptContract
{
    public string ToolName { get; set; } = string.Empty;

    public string RuntimeToolProviderKey { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;
}
