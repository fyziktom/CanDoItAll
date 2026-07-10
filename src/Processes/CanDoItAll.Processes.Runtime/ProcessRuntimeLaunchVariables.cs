using System.Text.Json;
using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Abstractions;

namespace CanDoItAll.Processes.Runtime;

public static class ProcessRuntimeLaunchVariables
{
    public const string ParentProcessRunId = "ParentProcessRunId";
    public const string ParentProcessStepId = "ParentProcessStepId";
    public const string ProductCompletionRequiredPaths = "ProductCompletionRequiredPaths";
    public const string ProductCompletionRequiredPathsByStep = "ProductCompletionRequiredPathsByStep";
    public const string ProductCompletionRequiredFileContentChecks = "ProductCompletionRequiredFileContentChecks";
    public const string ProductCompletionRequiredFileContentChecksByStep = "ProductCompletionRequiredFileContentChecksByStep";
    public const string ProductCompletionRequiredToolReceipts = "ProductCompletionRequiredToolReceipts";
    public const string ProductCompletionRequiredToolReceiptsByStep = "ProductCompletionRequiredToolReceiptsByStep";
    public const string CompletionIssueRoutes = "CompletionIssueRoutes";
    public const string CompletionIssueRoutesByStep = "CompletionIssueRoutesByStep";
    public const string AcceptanceCriteriaMatrix = "AcceptanceCriteriaMatrix";
    public const string AcceptanceCriteriaAcceptedBranchOutcomeKeys = "AcceptanceCriteriaAcceptedBranchOutcomeKeys";
    public const string ProcessStepScopedLaunchVariablePrefixesByStep = "ProcessStepScopedLaunchVariablePrefixesByStep";
    public const string ProcessDefinitionKey = "ProcessDefinitionKey";
    public const string ProcessDefinitionName = "ProcessDefinitionName";
    public const string ProcessStepKind = "ProcessStepKind";
    public const string ProcessStepSubprocessContractJson = "ProcessStepSubprocessContractJson";
    public const string ProcessStepSubprocessDefinitionKey = "ProcessStepSubprocessDefinitionKey";
    public const string ProjectId = "ProjectId";
    public const string ProjectName = "ProjectName";
    public const string ProductRoot = "ProductRoot";
    public const string OutputRoot = "OutputRoot";
    public const string ExternalTargetRoot = "ExternalTargetRoot";
    public const string ProductRootAlias = "ProductRootAlias";
    public const string OutputRootAlias = "OutputRootAlias";
    public const string WorkspaceAlias = "WorkspaceAlias";

    public static IReadOnlyDictionary<string, string> CreateParentRunLookup(ProcessRunId parentRunId)
    {
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [ParentProcessRunId] = parentRunId.ToString()
        };
    }

    public static IReadOnlyDictionary<string, string> CreateParentStepLookup(
        ProcessRunId parentRunId,
        ProcessStepInstanceId parentStepId)
    {
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [ParentProcessRunId] = parentRunId.ToString(),
            [ParentProcessStepId] = parentStepId.ToString()
        };
    }

    public static bool TryReadParentRunId(
        IReadOnlyDictionary<string, string> launchVariables,
        out ProcessRunId parentRunId)
    {
        parentRunId = default;
        if (!TryReadGuid(launchVariables, ParentProcessRunId, out var value))
        {
            return false;
        }

        parentRunId = new ProcessRunId(value);
        return true;
    }

    public static bool TryReadParentStepId(
        IReadOnlyDictionary<string, string> launchVariables,
        out ProcessStepInstanceId parentStepId)
    {
        parentStepId = default;
        if (!TryReadGuid(launchVariables, ParentProcessStepId, out var value))
        {
            return false;
        }

        parentStepId = new ProcessStepInstanceId(value);
        return true;
    }

    public static bool TryReadParentStep(
        IReadOnlyDictionary<string, string> launchVariables,
        out ProcessRuntimeParentStepReference parentStep)
    {
        parentStep = default;
        if (!TryReadParentRunId(launchVariables, out var parentRunId) ||
            !TryReadParentStepId(launchVariables, out var parentStepId))
        {
            return false;
        }

        parentStep = new ProcessRuntimeParentStepReference(parentRunId, parentStepId);
        return true;
    }

    public static bool TryReadProcessDefinitionName(
        IReadOnlyDictionary<string, string> launchVariables,
        out string definitionName)
    {
        definitionName = string.Empty;
        if (!TryReadNonEmptyString(launchVariables, ProcessDefinitionName, out var value))
        {
            return false;
        }

        definitionName = value;
        return true;
    }

    public static bool TryReadProcessStepSubprocessDefinitionKey(
        IReadOnlyDictionary<string, string> launchVariables,
        out string definitionKey)
    {
        definitionKey = string.Empty;
        if (!TryReadNonEmptyString(launchVariables, ProcessStepSubprocessDefinitionKey, out var value))
        {
            return false;
        }

        definitionKey = value;
        return true;
    }

    public static string SerializeProcessStepSubprocessContract(ProcessSubprocessContract contract)
    {
        ArgumentNullException.ThrowIfNull(contract);
        return JsonSerializer.Serialize(contract, ProcessRuntimeLaunchVariableJson.Options);
    }

    public static bool TryReadProcessStepSubprocessContract(
        IReadOnlyDictionary<string, string> launchVariables,
        out ProcessSubprocessContract contract)
    {
        contract = new ProcessSubprocessContract();
        if (!TryReadNonEmptyString(launchVariables, ProcessStepSubprocessContractJson, out var value))
        {
            return false;
        }

        try
        {
            var deserialized = JsonSerializer.Deserialize<ProcessSubprocessContract>(
                value,
                ProcessRuntimeLaunchVariableJson.Options);
            if (deserialized is null)
            {
                return false;
            }

            contract = deserialized;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public static bool TryReadProjectId(
        IReadOnlyDictionary<string, string> launchVariables,
        out Guid projectId)
    {
        projectId = Guid.Empty;
        return TryReadGuid(launchVariables, ProjectId, out projectId);
    }

    public static bool TryReadProjectName(
        IReadOnlyDictionary<string, string> launchVariables,
        out string projectName)
    {
        projectName = string.Empty;
        if (!TryReadNonEmptyString(launchVariables, ProjectName, out var value))
        {
            return false;
        }

        projectName = value;
        return true;
    }

    public static bool TryReadParentRunId(
        string launchVariablesJson,
        out ProcessRunId parentRunId)
    {
        parentRunId = default;
        return TryDeserializeLaunchVariables(launchVariablesJson, out var launchVariables) &&
               TryReadParentRunId(launchVariables, out parentRunId);
    }

    public static bool TryReadParentStep(
        string launchVariablesJson,
        out ProcessRuntimeParentStepReference parentStep)
    {
        parentStep = default;
        return TryDeserializeLaunchVariables(launchVariablesJson, out var launchVariables) &&
               TryReadParentStep(launchVariables, out parentStep);
    }

    private static bool TryReadGuid(
        IReadOnlyDictionary<string, string> launchVariables,
        string key,
        out Guid value)
    {
        value = Guid.Empty;
        return launchVariables.TryGetValue(key, out var rawValue) &&
               Guid.TryParse(rawValue, out value) &&
               value != Guid.Empty;
    }

    private static bool TryReadNonEmptyString(
        IReadOnlyDictionary<string, string> launchVariables,
        string key,
        out string value)
    {
        value = string.Empty;
        if (!launchVariables.TryGetValue(key, out var rawValue) ||
            string.IsNullOrWhiteSpace(rawValue))
        {
            return false;
        }

        value = rawValue.Trim();
        return true;
    }

    private static bool TryDeserializeLaunchVariables(
        string launchVariablesJson,
        out IReadOnlyDictionary<string, string> launchVariables)
    {
        launchVariables = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(launchVariablesJson))
        {
            return false;
        }

        try
        {
            var deserialized = JsonSerializer.Deserialize<Dictionary<string, string>>(launchVariablesJson);
            if (deserialized is null)
            {
                return false;
            }

            launchVariables = new Dictionary<string, string>(deserialized, StringComparer.Ordinal);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}

internal static class ProcessRuntimeLaunchVariableJson
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web);
}

public readonly record struct ProcessRuntimeParentStepReference(
    ProcessRunId RunId,
    ProcessStepInstanceId StepInstanceId);
