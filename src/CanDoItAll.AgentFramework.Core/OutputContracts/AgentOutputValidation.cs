using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public interface IAgentOutputValidator<in TOutput>
{
    AgentOutputValidationResult Validate(TOutput output);
}

public interface IAgentOutputRepairService<TOutput>
{
    Task<AgentOutputRepairResult<TOutput>> TryRepairAsync(
        AgentOutputRepairRequest repairRequest,
        CancellationToken cancellationToken);
}

public static class AgentOutputJson
{
    public static JsonSerializerOptions SerializerOptions { get; } = CreateSerializerOptions();

    public static AgentOutputPipelineResult<TOutput> DeserializeAndValidate<TOutput>(
        string? rawOutput,
        IAgentOutputValidator<TOutput> validator)
    {
        ArgumentNullException.ThrowIfNull(validator);

        if (string.IsNullOrWhiteSpace(rawOutput))
        {
            return AgentOutputPipelineResult<TOutput>.Failure(
                rawOutput ?? string.Empty,
                AgentOutputValidationResult.Failure(new AgentOutputValidationError
                {
                    Code = "agent.output.empty",
                    Message = "Agent output was empty.",
                    Path = "$"
                }));
        }

        TOutput? output;
        try
        {
            output = JsonSerializer.Deserialize<TOutput>(rawOutput, SerializerOptions);
        }
        catch (JsonException exception)
        {
            return AgentOutputPipelineResult<TOutput>.Failure(
                rawOutput,
                AgentOutputValidationResult.Failure(new AgentOutputValidationError
                {
                    Code = "agent.output.malformed_json",
                    Message = exception.Message,
                    Path = exception.Path
                }));
        }

        if (output is null)
        {
            return AgentOutputPipelineResult<TOutput>.Failure(
                rawOutput,
                AgentOutputValidationResult.Failure(new AgentOutputValidationError
                {
                    Code = "agent.output.null",
                    Message = "Agent output deserialized to null.",
                    Path = "$"
                }));
        }

        var validation = validator.Validate(output);
        return validation.IsValid
            ? AgentOutputPipelineResult<TOutput>.Success(rawOutput, output)
            : AgentOutputPipelineResult<TOutput>.Failure(rawOutput, validation, output);
    }

    public static string ComputeRawOutputHash(string? rawOutput)
    {
        var bytes = Encoding.UTF8.GetBytes(rawOutput ?? string.Empty);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Disallow,
            AllowTrailingCommas = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        options.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
        return options;
    }
}

public sealed class AgentOutputPipelineResult<TOutput>
{
    private AgentOutputPipelineResult(
        bool succeeded,
        string rawOutput,
        AgentOutputValidationResult validation,
        TOutput? output)
    {
        Succeeded = succeeded;
        RawOutput = rawOutput;
        Validation = validation;
        Output = output;
        RawOutputHash = AgentOutputJson.ComputeRawOutputHash(rawOutput);
    }

    public bool Succeeded { get; }
    public string RawOutput { get; }
    public string RawOutputHash { get; }
    public AgentOutputValidationResult Validation { get; }
    public TOutput? Output { get; }

    public static AgentOutputPipelineResult<TOutput> Success(string rawOutput, TOutput output)
        => new(true, rawOutput, AgentOutputValidationResult.Success(), output);

    public static AgentOutputPipelineResult<TOutput> Failure(
        string rawOutput,
        AgentOutputValidationResult validation,
        TOutput? output = default)
        => new(false, rawOutput, validation, output);
}

public sealed class ProcessStatePatchValidator(
    IReadOnlySet<string> allowedRootPaths,
    IReadOnlySet<string> protectedPaths) : IAgentOutputValidator<ProcessStatePatch>
{
    public AgentOutputValidationResult Validate(ProcessStatePatch output)
    {
        ArgumentNullException.ThrowIfNull(output);

        var errors = new List<AgentOutputValidationError>();
        if (output.Operations.Count == 0)
        {
            errors.Add(new AgentOutputValidationError
            {
                Code = "process.patch.operations_required",
                Message = "Process state patch must contain at least one operation.",
                Path = "$.operations"
            });
        }

        for (var index = 0; index < output.Operations.Count; index++)
        {
            ValidateOperation(output.Operations[index], index, errors);
        }

        return errors.Count == 0
            ? AgentOutputValidationResult.Success()
            : AgentOutputValidationResult.Failure([.. errors]);
    }

    private void ValidateOperation(
        ProcessPatchOperation operation,
        int index,
        List<AgentOutputValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(operation.Path) || !operation.Path.StartsWith("/", StringComparison.Ordinal))
        {
            errors.Add(new AgentOutputValidationError
            {
                Code = "process.patch.path_invalid",
                Message = "Patch path must be a non-empty JSON pointer path.",
                Path = $"$.operations[{index}].path"
            });
        }
        else if (!IsAllowedPath(operation.Path))
        {
            errors.Add(new AgentOutputValidationError
            {
                Code = "process.patch.path_not_allowed",
                Message = $"Patch path '{operation.Path}' is not allowed for this agent role.",
                Path = $"$.operations[{index}].path"
            });
        }
        else if (IsProtectedPath(operation.Path))
        {
            errors.Add(new AgentOutputValidationError
            {
                Code = "process.patch.path_protected",
                Message = $"Patch path '{operation.Path}' targets protected process state.",
                Path = $"$.operations[{index}].path",
                Severity = AgentOutputValidationSeverity.Critical
            });
        }

        if (operation.Op != ProcessPatchOperationKind.Remove && operation.Value is null)
        {
            errors.Add(new AgentOutputValidationError
            {
                Code = "process.patch.value_required",
                Message = "Add and replace patch operations must include a value.",
                Path = $"$.operations[{index}].value"
            });
        }

        if (string.IsNullOrWhiteSpace(operation.Reason))
        {
            errors.Add(new AgentOutputValidationError
            {
                Code = "process.patch.reason_required",
                Message = "Patch operation reason is required.",
                Path = $"$.operations[{index}].reason"
            });
        }
    }

    private bool IsAllowedPath(string path)
    {
        if (allowedRootPaths.Count == 0)
        {
            return false;
        }

        return allowedRootPaths.Any(root =>
            path.Equals(root, StringComparison.Ordinal) ||
            path.StartsWith(root.TrimEnd('/') + "/", StringComparison.Ordinal));
    }

    private bool IsProtectedPath(string path)
    {
        return protectedPaths.Any(protectedPath =>
            path.Equals(protectedPath, StringComparison.Ordinal) ||
            path.StartsWith(protectedPath.TrimEnd('/') + "/", StringComparison.Ordinal));
    }
}
