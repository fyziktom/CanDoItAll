using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Infrastructure.FileSystem;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Processes.Application;

namespace CanDoItAll.Modules.Processes;

internal sealed class DotNetProcessLaunchVariableContributor(
    IExternalTargetPathRegistry externalTargetPathRegistry,
    IPhysicalFileSystemPathPolicyFactory physicalPathPolicyFactory,
    IWorkspaceFileService? workspaceFiles = null) : IProcessLaunchVariableContributor
{
    private readonly DotNetSolutionSetupLaunchPlanBuilder solutionSetupLaunchPlanBuilder = new();
    private readonly IWorkspaceFileService? workspaceFiles = workspaceFiles;
    private readonly DotNetSolutionContextParser solutionContextParser = new();
    private readonly DotNetSolutionContextPathResolver contextPathResolver = new(
        externalTargetPathRegistry,
        physicalPathPolicyFactory);

    public void Enrich(
        ProcessLaunchPreparationContext context,
        IDictionary<string, string> variables)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(variables);

        if (!DotNetProcessLaunchActivation.TryResolve(context, out var activation))
        {
            return;
        }

        if (workspaceFiles is null)
        {
            throw new InvalidOperationException(
                $".NET launch contract for process definition '{context.DefinitionKey}' requires the workspace file service to read its declared bootstrap decision.");
        }

        var solutionContextResolver = new DotNetSolutionContextArtifactResolver(
            workspaceFiles,
            solutionContextParser);
        if (!solutionContextResolver.TryResolve(
                activation.SolutionContextBinding,
                variables,
                out var solutionContext,
                out var solutionContextIssue))
        {
            throw new InvalidOperationException(
                $".NET launch contract for process definition '{context.DefinitionKey}' could not resolve its declared solution context: {solutionContextIssue}");
        }

        if (solutionContext.ProvisioningMode == DotNetSolutionProvisioningMode.VerifyExisting)
        {
            var verificationFactory = new DotNetExistingSolutionVerificationContractFactory(contextPathResolver);
            if (!verificationFactory.TryCreate(solutionContext, variables, out var verificationContract, out var verificationIssue))
            {
                throw new InvalidOperationException(
                    $".NET launch contract for process definition '{context.DefinitionKey}' could not be created from its declared existing solution context: {verificationIssue}");
            }

            var productRootPolicy = physicalPathPolicyFactory.Create(verificationContract.ProductRoot);
            DotNetProcessLaunchVariableWriter.ApplyExistingSolution(
                verificationContract,
                variables,
                productRootPolicy.PathComparer);
            return;
        }

        var contractFactory = new DotNetProcessLaunchContractFactory(
            contextPathResolver,
            externalTargetPathRegistry);
        if (!contractFactory.TryCreate(solutionContext, variables, out var contract, out var contractIssue))
        {
            throw new InvalidOperationException(
                $".NET launch contract for process definition '{context.DefinitionKey}' could not be created from its declared initialization plan: {contractIssue}");
        }

        var initializationRootPolicy = physicalPathPolicyFactory.Create(contract.ProductRoot);
        DotNetProcessLaunchVariableWriter.ApplyCore(
            contract,
            variables,
            initializationRootPolicy.PathComparer);
        solutionSetupLaunchPlanBuilder.Apply(contract, activation.SetupPolicyBindings, variables);
    }
}
