using System.Text.Json;

namespace CanDoItAll.Manager;

public sealed record TuningExecutionContext(
    Guid RequestId,
    string WorkspaceRoot,
    string RequestDirectory,
    string RequestJsonPath,
    string StdOutPath,
    string StdErrPath,
    string EventsPath);

public sealed record TuningExecutionResult(
    string AdapterJobId,
    int ExitCode,
    string Summary);

public interface ITuningExecutionAdapter
{
    Task<TuningExecutionResult> ExecuteAsync(TuningExecutionContext context, CancellationToken cancellationToken = default);
}

public sealed class LocalProcessTuningExecutionAdapter(
    IConfiguration configuration,
    IManagerProcessCoordinator processCoordinator) : ITuningExecutionAdapter
{
    private readonly ManagerOptions _options = configuration.GetSection("Manager").Get<ManagerOptions>() ?? new();

    public async Task<TuningExecutionResult> ExecuteAsync(TuningExecutionContext context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.TuningCommand))
        {
            throw new InvalidOperationException("No local tuning adapter command is configured.");
        }

        await using var process = await processCoordinator.StartAsync(
            new ManagerProcessLaunchRequest(
                ManagerProcessPurpose.Tuning,
                "manager_tuning_adapter",
                "manager.tuning.v1",
                _options.TuningCommand,
                BuildArguments(context),
                ResolveWorkingDirectory(context),
                new Dictionary<string, string?>
                {
                    ["CANDOITALL_TUNING_REQUEST_ID"] = context.RequestId.ToString("N"),
                    ["CANDOITALL_TUNING_REQUEST_PATH"] = context.RequestJsonPath,
                    ["CANDOITALL_TUNING_EVENTS_PATH"] = context.EventsPath
                },
                context.WorkspaceRoot,
                $"Tuning:{context.RequestId:N}"),
            cancellationToken);
        var execution = await process.WaitForExitAsync(cancellationToken);
        await File.WriteAllTextAsync(context.StdOutPath, execution.Stdout, cancellationToken);
        await File.WriteAllTextAsync(context.StdErrPath, execution.Stderr, cancellationToken);

        return new TuningExecutionResult(
            Guid.NewGuid().ToString("N"),
            execution.ExitCode,
            execution.ExitCode == 0
                ? "Local tuning adapter completed successfully."
                : $"Local tuning adapter exited with code {execution.ExitCode}.");
    }

    private string ResolveWorkingDirectory(TuningExecutionContext context)
        => string.IsNullOrWhiteSpace(_options.TuningWorkingDirectory)
            ? context.WorkspaceRoot
            : Path.GetFullPath(_options.TuningWorkingDirectory, context.WorkspaceRoot);

    private IReadOnlyList<string> BuildArguments(TuningExecutionContext context)
        => ManagerTuningArgumentBuilder.Build(_options.TuningArguments, context);
}

internal static class ManagerTuningArgumentBuilder
{
    public static IReadOnlyList<string> Build(
        string? template,
        TuningExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return ManagerCommandLineTokenizer.Tokenize(template ?? string.Empty)
            .Select(argument => argument
                .Replace("{requestPath}", context.RequestJsonPath, StringComparison.Ordinal)
                .Replace("{requestDirectory}", context.RequestDirectory, StringComparison.Ordinal)
                .Replace("{workspaceRoot}", context.WorkspaceRoot, StringComparison.Ordinal))
            .ToArray();
    }
}

internal static class ManagerCommandLineTokenizer
{
    public static IReadOnlyList<string> Tokenize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        var arguments = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (character == '\\' &&
                index + 1 < value.Length &&
                value[index + 1] is '"' or '\\')
            {
                current.Append(value[++index]);
                continue;
            }

            if (character == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (char.IsWhiteSpace(character) && !inQuotes)
            {
                if (current.Length > 0)
                {
                    arguments.Add(current.ToString());
                    current.Clear();
                }

                continue;
            }

            current.Append(character);
        }

        if (inQuotes)
        {
            throw new InvalidOperationException("The configured tuning arguments contain an unterminated quote.");
        }

        if (current.Length > 0)
        {
            arguments.Add(current.ToString());
        }

        return arguments;
    }
}

internal sealed record TuningRequestPacket(
    Guid RequestId,
    string CorrelationId,
    string CapsuleKey,
    string ComponentName,
    string Route,
    Guid? ProjectId,
    string? TabId,
    string? SelectionId,
    string? ContextSummary,
    string Instruction,
    IReadOnlyList<TuningAttachmentPacket> Attachments,
    string CapsuleSummary,
    DateTimeOffset CreatedAtUtc);

internal sealed record TuningAttachmentPacket(
    string FileName,
    string ContentType,
    string Source,
    string RelativePath);

internal sealed record TuningEventLogEntry(
    DateTimeOffset TimestampUtc,
    string Status,
    string Summary,
    string? AdapterJobId = null,
    int? ExitCode = null);
