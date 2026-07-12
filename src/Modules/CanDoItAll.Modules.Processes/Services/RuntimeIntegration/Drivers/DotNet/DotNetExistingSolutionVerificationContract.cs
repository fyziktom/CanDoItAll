namespace CanDoItAll.Modules.Processes;

internal sealed record DotNetExistingSolutionVerificationContract(
    string ProductRoot,
    string SolutionFile,
    string SolutionFileAlias,
    IReadOnlyList<string> SolutionCandidatePaths,
    IReadOnlyList<string> RequiredProjectFiles,
    IReadOnlyList<string> TestProjectFiles,
    string WorkspaceAlias);

internal sealed class DotNetExistingSolutionVerificationContractFactory(
    DotNetSolutionContextPathResolver pathResolver)
{
    public bool TryCreate(
        DotNetSolutionContext context,
        IDictionary<string, string> variables,
        out DotNetExistingSolutionVerificationContract contract,
        out string issue)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(variables);

        contract = null!;
        if (context.ProvisioningMode != DotNetSolutionProvisioningMode.VerifyExisting)
        {
            issue = "The existing-solution verification contract requires provisioningMode 'verify-existing'.";
            return false;
        }

        if (!pathResolver.TryResolve(context, variables, out var resolved, out issue))
        {
            return false;
        }

        contract = new DotNetExistingSolutionVerificationContract(
            resolved.ProductRoot,
            resolved.SolutionFile,
            resolved.SolutionFileAlias,
            resolved.SolutionCandidatePaths,
            resolved.RequiredProjectFiles,
            resolved.TestProjectFiles,
            resolved.WorkspaceAlias);
        issue = string.Empty;
        return true;
    }
}
