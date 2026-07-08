using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessSubprocessContractResolver
{
    public static bool TryResolve(
        ProcessRuntimeStepAssignment assignment,
        out ProcessSubprocessContract contract)
    {
        ArgumentNullException.ThrowIfNull(assignment);

        if (ProcessRuntimeLaunchVariables.TryReadProcessStepSubprocessContract(
                assignment.LaunchVariables,
                out contract))
        {
            return true;
        }

        var processDefinitionKey = ResolveLaunchVariable(
            assignment.LaunchVariables,
            ProcessRuntimeLaunchVariables.ProcessDefinitionKey);
        var subprocessDefinitionKey = ResolveLaunchVariable(
            assignment.LaunchVariables,
            ProcessRuntimeLaunchVariables.ProcessStepSubprocessDefinitionKey);
        if (string.IsNullOrWhiteSpace(processDefinitionKey) ||
            string.IsNullOrWhiteSpace(subprocessDefinitionKey))
        {
            return TryResolveKnownContractByStepAndChild(
                assignment.StepKey,
                subprocessDefinitionKey,
                out contract);
        }

        return TryResolveKnownContract(
            processDefinitionKey,
            assignment.StepKey,
            subprocessDefinitionKey,
            out contract);
    }

    public static bool TryResolve(
        IReadOnlyDictionary<string, string> launchVariables,
        string stepKey,
        out ProcessSubprocessContract contract)
    {
        if (ProcessRuntimeLaunchVariables.TryReadProcessStepSubprocessContract(
                launchVariables,
                out contract))
        {
            return true;
        }

        var processDefinitionKey = ResolveLaunchVariable(
            launchVariables,
            ProcessRuntimeLaunchVariables.ProcessDefinitionKey);
        var subprocessDefinitionKey = ResolveLaunchVariable(
            launchVariables,
            ProcessRuntimeLaunchVariables.ProcessStepSubprocessDefinitionKey);
        if (string.IsNullOrWhiteSpace(processDefinitionKey) ||
            string.IsNullOrWhiteSpace(subprocessDefinitionKey))
        {
            return TryResolveKnownContractByStepAndChild(
                stepKey,
                subprocessDefinitionKey,
                out contract);
        }

        return TryResolveKnownContract(
            processDefinitionKey,
            stepKey,
            subprocessDefinitionKey,
            out contract);
    }

    private static bool TryResolveKnownContract(
        string parentProcessKey,
        string parentStepKey,
        string childProcessKey,
        out ProcessSubprocessContract contract)
    {
        contract = (NormalizeKey(parentProcessKey), NormalizeKey(parentStepKey)) switch
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
            _ => new ProcessSubprocessContract()
        };

        return !string.IsNullOrWhiteSpace(contract.DefinitionKey) &&
               string.Equals(contract.DefinitionKey, childProcessKey, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryResolveKnownContractByStepAndChild(
        string parentStepKey,
        string childProcessKey,
        out ProcessSubprocessContract contract)
    {
        contract = (NormalizeKey(parentStepKey), NormalizeKey(childProcessKey)) switch
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
            _ => new ProcessSubprocessContract()
        };

        return !string.IsNullOrWhiteSpace(contract.DefinitionKey);
    }

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

    private static string ResolveLaunchVariable(
        IReadOnlyDictionary<string, string> launchVariables,
        string key)
        => launchVariables.TryGetValue(key, out var value) ? value.Trim() : string.Empty;

    private static string NormalizeKey(string value)
        => value.Trim().ToLowerInvariant();
}
