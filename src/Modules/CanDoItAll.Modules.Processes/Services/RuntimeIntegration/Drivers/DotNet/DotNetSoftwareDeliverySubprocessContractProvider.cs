using CanDoItAll.Processes.Contracts;

namespace CanDoItAll.Modules.Processes;

internal sealed class DotNetSoftwareDeliverySubprocessContractProvider : IProcessSubprocessContractProvider
{
    public bool TryResolve(
        ProcessSubprocessContractRequest request,
        out ProcessSubprocessContract contract)
    {
        contract = TryResolveByParentProcess(request) ??
                   TryResolveByStepAndChild(request) ??
                   new ProcessSubprocessContract();
        return !string.IsNullOrWhiteSpace(contract.DefinitionKey) &&
               (string.IsNullOrWhiteSpace(request.ChildProcessKey) ||
                string.Equals(contract.DefinitionKey, request.ChildProcessKey, StringComparison.OrdinalIgnoreCase));
    }

    private static ProcessSubprocessContract? TryResolveByParentProcess(ProcessSubprocessContractRequest request)
        => (NormalizeKey(request.ParentProcessKey), NormalizeKey(request.ParentStepKey)) switch
        {
            ("dotnet-development-slice", "prepare-solution-skeleton") => BuildSolutionSetupContract(),
            ("dotnet-development-slice", "implement-code-change") => BuildFeatureImplementationContract("slice-change-set"),
            ("dotnet-development-slice", "slice-repair-code-change") => BuildFeatureImplementationContract("slice-repair-change-set"),
            ("software-delivery", "architecture-review") => BuildArchitectureReviewContract(),
            ("software-delivery", "implementation") => BuildDevelopmentSliceContract("implementation-change-set"),
            ("software-delivery", "record-runtime-commands") => BuildRuntimeCommandWritebackContract("runtime-command-writeback"),
            ("software-delivery", "capture-ui-screenshots") => BuildScreenshotWritebackContract("ui-screenshot-writeback"),
            ("software-delivery", "record-runtime-commands-after-repair") => BuildRuntimeCommandWritebackContract("runtime-command-writeback-after-repair"),
            ("software-delivery", "capture-ui-screenshots-after-repair") => BuildScreenshotWritebackContract("ui-screenshot-writeback-after-repair"),
            _ => null
        };

    private static ProcessSubprocessContract? TryResolveByStepAndChild(ProcessSubprocessContractRequest request)
        => (NormalizeKey(request.ParentStepKey), NormalizeKey(request.ChildProcessKey)) switch
        {
            ("prepare-solution-skeleton", "dotnet-solution-setup") => BuildSolutionSetupContract(),
            ("implement-code-change", "dotnet-feature-function-implementation") => BuildFeatureImplementationContract("slice-change-set"),
            ("slice-repair-code-change", "dotnet-feature-function-implementation") => BuildFeatureImplementationContract("slice-repair-change-set"),
            ("architecture-review", "dotnet-architecture-design-review") => BuildArchitectureReviewContract(),
            ("implementation", "dotnet-development-slice") => BuildDevelopmentSliceContract("implementation-change-set"),
            ("record-runtime-commands", "dotnet-runtime-command-writeback") => BuildRuntimeCommandWritebackContract("runtime-command-writeback"),
            ("capture-ui-screenshots", "dotnet-ui-screenshot-writeback") => BuildScreenshotWritebackContract("ui-screenshot-writeback"),
            ("record-runtime-commands-after-repair", "dotnet-runtime-command-writeback") => BuildRuntimeCommandWritebackContract("runtime-command-writeback-after-repair"),
            ("capture-ui-screenshots-after-repair", "dotnet-ui-screenshot-writeback") => BuildScreenshotWritebackContract("ui-screenshot-writeback-after-repair"),
            _ => null
        };

    private static ProcessSubprocessContract BuildSolutionSetupContract()
        => Create(
            "dotnet-solution-setup",
            "solution-skeleton-evidence",
            [
                Child("setup-handoff", "setup-handoff-packet", "Setup handoff packet"),
                Child("setup-handoff-after-repair", "setup-handoff-packet-after-repair", "Setup handoff packet after repair")
            ],
            [
                Child("setup-repair-escalation", "setup-repair-escalation-packet", "Setup repair escalation packet")
            ]);

    private static ProcessSubprocessContract BuildFeatureImplementationContract(string parentExpectationKey)
        => Create(
            "dotnet-feature-function-implementation",
            parentExpectationKey,
            [
                Child("feature-handoff", "feature-handoff-packet", "Feature implementation handoff packet"),
                Child("feature-handoff-after-repair", "feature-handoff-packet-after-repair", "Feature implementation handoff packet after repair")
            ],
            [
                Child("feature-repair-escalation", "feature-repair-escalation-packet", "Feature repair escalation packet")
            ]);

    private static ProcessSubprocessContract BuildArchitectureReviewContract()
        => Create(
            "dotnet-architecture-design-review",
            "architecture-decision-record",
            [
                Child("classify-dotnet-application", "dotnet-application-classification", ".NET application classification and project context"),
                Child("architecture-handoff", "architecture-design-review-handoff", ".NET architecture design and review handoff")
            ],
            []);

    private static ProcessSubprocessContract BuildDevelopmentSliceContract(string parentExpectationKey)
        => Create(
            "dotnet-development-slice",
            parentExpectationKey,
            [
                Child("slice-handoff", "slice-handoff-packet", "Implementation slice handoff packet"),
                Child("slice-handoff-after-repair", "slice-handoff-packet-after-repair", "Implementation slice handoff packet after repair")
            ],
            [
                Child("slice-repair-escalation", "slice-repair-escalation-packet", "Implementation slice repair escalation packet")
            ]);

    private static ProcessSubprocessContract BuildRuntimeCommandWritebackContract(string parentExpectationKey)
        => Create(
            "dotnet-runtime-command-writeback",
            parentExpectationKey,
            [
                Child("runtime-command-handoff", "runtime-command-handoff", ".NET runtime command handoff")
            ],
            []);

    private static ProcessSubprocessContract BuildScreenshotWritebackContract(string parentExpectationKey)
        => Create(
            "dotnet-ui-screenshot-writeback",
            parentExpectationKey,
            [
                Child("screenshot-handoff", "ui-screenshot-writeback-handoff", ".NET UI screenshot writeback handoff")
            ],
            []);

    private static ProcessSubprocessContract Create(
        string definitionKey,
        string parentExpectationKey,
        IReadOnlyList<ProcessSubprocessChildOutputContract> acceptedOutputs,
        IReadOnlyList<ProcessSubprocessChildOutputContract> noGoOutputs)
        => new()
        {
            DefinitionKey = definitionKey,
            LaunchMode = ProcessSubprocessLaunchMode.RuntimeOwned,
            ParentProducedArtifactExpectationKey = parentExpectationKey,
            AcceptedChildOutputs = acceptedOutputs.ToList(),
            NoGoChildOutputs = noGoOutputs.ToList(),
            RequiredChildReceipts = [],
            AlreadySatisfiedOutput = null,
            MaterializationMode = ProcessSubprocessMaterializationMode.RuntimeSynthesizedParentHandoff
        };

    private static ProcessSubprocessChildOutputContract Child(
        string stepKey,
        string artifactExpectationKey,
        string artifactTitle)
        => new()
        {
            StepKey = stepKey,
            ArtifactExpectationKey = artifactExpectationKey,
            ArtifactTitle = artifactTitle
        };

    private static string NormalizeKey(string value)
        => value.Trim().ToLowerInvariant();
}
