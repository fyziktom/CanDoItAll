using System.Text.Json;

namespace CanDoItAll.Modules.Processes;

internal sealed class DotNetSolutionSetupLaunchPlanBuilder
{
    private const string CreateProjectScriptRef = "artifacts/process-runs/{CurrentProcessRunId}/scripts/create-dotnet-project.wire-solution.ps1";
    private const string AddTestProjectScriptRef = "artifacts/process-runs/{CurrentProcessRunId}/scripts/add-test-project.wire-solution.ps1";

    public void Apply(
        DotNetProcessLaunchContract contract,
        DotNetSolutionSetupTemplatePolicyBindings setupPolicyBindings,
        IDictionary<string, string> variables)
    {
        ArgumentNullException.ThrowIfNull(contract);
        ArgumentNullException.ThrowIfNull(setupPolicyBindings);
        ArgumentNullException.ThrowIfNull(variables);

        setupPolicyBindings.ApplyTo(variables);
        DotNetProcessLaunchVariableWriter.SetAuthoritative(
            variables,
            "DotNetCreateProjectScriptRef",
            CreateProjectScriptRef);
        DotNetProcessLaunchVariableWriter.SetAuthoritative(
            variables,
            "DotNetCreateProjectScript",
            DotNetSolutionSetupScriptFactory.BuildCreateProjectScript(contract));
        DotNetProcessLaunchVariableWriter.SetAuthoritative(
            variables,
            "DotNetCreateProjectSideEffectManifest",
            DotNetSolutionSetupScriptFactory.BuildCreateProjectSideEffectManifest(contract));
        DotNetProcessLaunchVariableWriter.SetAuthoritative(
            variables,
            "DotNetCreateProjectExecutionPlan",
            BuildExecutionPlan(
                "dotnet.create-project",
                CreateProjectScriptRef,
                contract.WorkspaceAlias,
                requiresScaffold: true));
        DotNetProcessLaunchVariableWriter.SetAuthoritative(
            variables,
            "DotNetAddTestProjectScriptRef",
            AddTestProjectScriptRef);
        DotNetProcessLaunchVariableWriter.SetAuthoritative(
            variables,
            "DotNetAddTestProjectScript",
            DotNetSolutionSetupScriptFactory.BuildAddTestProjectScript(contract));
        DotNetProcessLaunchVariableWriter.SetAuthoritative(
            variables,
            "DotNetAddTestProjectSideEffectManifest",
            DotNetSolutionSetupScriptFactory.BuildAddTestProjectSideEffectManifest(contract));
        DotNetProcessLaunchVariableWriter.SetAuthoritative(
            variables,
            "DotNetAddTestProjectExecutionPlan",
            BuildExecutionPlan(
                "dotnet.add-test-project",
                AddTestProjectScriptRef,
                contract.WorkspaceAlias,
                requiresScaffold: false));
        DotNetProcessLaunchVariableWriter.SetAuthoritative(
            variables,
            "DotNetRepairSolutionSetupExecutionPlan",
            BuildExecutionPlan(
                "dotnet.repair-solution-setup",
                AddTestProjectScriptRef,
                contract.WorkspaceAlias,
                requiresScaffold: false));
    }

    private static string BuildExecutionPlan(
        string planKey,
        string scriptRef,
        string workspaceAlias,
        bool requiresScaffold)
        => JsonSerializer.Serialize(
            new DotNetSolutionSetupExecutionPlan(
                planKey,
                scriptRef,
                workspaceAlias,
                requiresScaffold));
}
