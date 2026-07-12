using CanDoItAll.Processes.Application;

namespace CanDoItAll.Modules.Processes;

internal sealed record DotNetProcessLaunchActivation(
    ProcessLaunchDriverArtifactBinding SolutionContextBinding,
    DotNetSolutionSetupTemplatePolicyBindings SetupPolicyBindings)
{
    internal const string DriverKey = "dotnet.launch-contract";

    private const string ModeSettingKey = "Mode";
    private const string SolutionSetupMode = "solution-setup";

    internal static bool TryResolve(
        ProcessLaunchPreparationContext context,
        out DotNetProcessLaunchActivation activation)
    {
        ArgumentNullException.ThrowIfNull(context);

        var configuredActivation = context.DriverActivations.SingleOrDefault(candidate =>
            string.Equals(candidate.DriverKey, DriverKey, StringComparison.OrdinalIgnoreCase));
        if (configuredActivation is null)
        {
            activation = null!;
            return false;
        }

        if (!configuredActivation.TryGetSetting(ModeSettingKey, out var mode))
        {
            throw new InvalidOperationException(
                $"Launch driver '{DriverKey}' for process definition '{context.DefinitionKey}' must declare setting '{ModeSettingKey}'.");
        }

        if (!string.Equals(mode, SolutionSetupMode, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Launch driver '{DriverKey}' for process definition '{context.DefinitionKey}' supports only mode '{SolutionSetupMode}'.");
        }

        var bindings = configuredActivation.InputArtifactBindings
            .Where(binding => string.Equals(
                binding.PayloadSchema,
                DotNetSolutionContextParser.Schema,
                StringComparison.Ordinal))
            .ToArray();
        if (bindings.Length != 1)
        {
            throw new InvalidOperationException(
                $"Launch driver '{DriverKey}' for process definition '{context.DefinitionKey}' must declare exactly one input artifact binding with schema '{DotNetSolutionContextParser.Schema}'.");
        }

        if (!DotNetSolutionSetupTemplatePolicyBindings.TryParse(
                configuredActivation,
                out var setupPolicyBindings,
                out var policyIssue))
        {
            throw new InvalidOperationException(
                $"Launch driver '{DriverKey}' for process definition '{context.DefinitionKey}' has invalid template-owned setup policy bindings: {policyIssue}");
        }

        activation = new DotNetProcessLaunchActivation(bindings[0], setupPolicyBindings);
        return true;
    }
}
