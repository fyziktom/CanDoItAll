using CanDoItAll.Processes.Drivers.Abstractions;

namespace CanDoItAll.Modules.Processes;

internal sealed class WorkspaceProductFilesystemCompletionGateContribution(
    ProcessProductCompletionPathGate productCompletionPathGate) : IProcessCompletionGateContribution
{
    public string ContributionKey => "workspace-product-filesystem-completion";

    public int Order => 50;

    public ProcessCompletionIssue? Validate(ProcessCompletionGateContext context)
    {
        if (productCompletionPathGate.ValidateRequiredProductFilesystemState(
                context.Assignment,
                context.Output) is { } requiredFilesystemIssue)
        {
            return requiredFilesystemIssue;
        }

        return ProcessProductMutationEvidenceGate.IsProductMutationEvidenceMissing(
            context.Assignment,
            context.Output)
            ? null
            : productCompletionPathGate.ValidateProductMutationFilesystemState(
                context.Assignment,
                context.Output);
    }
}
