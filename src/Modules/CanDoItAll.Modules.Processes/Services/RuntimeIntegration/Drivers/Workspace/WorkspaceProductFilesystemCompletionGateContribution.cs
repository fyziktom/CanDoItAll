using CanDoItAll.Processes.Drivers.Abstractions;

using static CanDoItAll.Modules.Processes.ProcessProductCompletionPathGate;

namespace CanDoItAll.Modules.Processes;

internal sealed class WorkspaceProductFilesystemCompletionGateContribution : IProcessCompletionGateContribution
{
    public string ContributionKey => "workspace-product-filesystem-completion";

    public int Order => 50;

    public ProcessCompletionIssue? Validate(ProcessCompletionGateContext context)
    {
        if (ValidateRequiredProductFilesystemState(context.Assignment, context.Output) is { } requiredFilesystemIssue)
        {
            return requiredFilesystemIssue;
        }

        return ProcessProductMutationEvidenceGate.IsProductMutationEvidenceMissing(
            context.Assignment,
            context.Output)
            ? null
            : ValidateProductMutationFilesystemState(context.Assignment, context.Output);
    }
}
