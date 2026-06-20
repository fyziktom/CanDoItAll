using System.Text.Json;
using CanDoItAll.Processes.Abstractions;

namespace CanDoItAll.Processes.Runtime;

public static class ProcessRuntimeLaunchVariables
{
    public const string ParentProcessRunId = "ParentProcessRunId";
    public const string ParentProcessStepId = "ParentProcessStepId";

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

public readonly record struct ProcessRuntimeParentStepReference(
    ProcessRunId RunId,
    ProcessStepInstanceId StepInstanceId);
