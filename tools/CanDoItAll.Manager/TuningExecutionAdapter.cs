using System.Diagnostics;
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

public sealed class LocalProcessTuningExecutionAdapter(IConfiguration configuration) : ITuningExecutionAdapter
{
    private readonly ManagerOptions _options = configuration.GetSection("Manager").Get<ManagerOptions>() ?? new();

    public async Task<TuningExecutionResult> ExecuteAsync(TuningExecutionContext context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.TuningCommand))
        {
            throw new InvalidOperationException("No local tuning adapter command is configured.");
        }

        var startInfo = new ProcessStartInfo(_options.TuningCommand, FormatArguments(context))
        {
            WorkingDirectory = ResolveWorkingDirectory(context),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        startInfo.Environment["CANDOITALL_TUNING_REQUEST_ID"] = context.RequestId.ToString("N");
        startInfo.Environment["CANDOITALL_TUNING_REQUEST_PATH"] = context.RequestJsonPath;
        startInfo.Environment["CANDOITALL_TUNING_EVENTS_PATH"] = context.EventsPath;

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start the local tuning adapter process.");
        await using var stdoutStream = new FileStream(context.StdOutPath, FileMode.Create, FileAccess.Write, FileShare.Read);
        await using var stderrStream = new FileStream(context.StdErrPath, FileMode.Create, FileAccess.Write, FileShare.Read);

        var stdoutTask = process.StandardOutput.BaseStream.CopyToAsync(stdoutStream, cancellationToken);
        var stderrTask = process.StandardError.BaseStream.CopyToAsync(stderrStream, cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        await Task.WhenAll(stdoutTask, stderrTask);

        return new TuningExecutionResult(
            Guid.NewGuid().ToString("N"),
            process.ExitCode,
            process.ExitCode == 0
                ? "Local tuning adapter completed successfully."
                : $"Local tuning adapter exited with code {process.ExitCode}.");
    }

    private string ResolveWorkingDirectory(TuningExecutionContext context)
        => string.IsNullOrWhiteSpace(_options.TuningWorkingDirectory)
            ? context.WorkspaceRoot
            : Path.GetFullPath(_options.TuningWorkingDirectory, context.WorkspaceRoot);

    private string FormatArguments(TuningExecutionContext context)
        => (_options.TuningArguments ?? string.Empty)
            .Replace("{requestPath}", context.RequestJsonPath, StringComparison.Ordinal)
            .Replace("{requestDirectory}", context.RequestDirectory, StringComparison.Ordinal)
            .Replace("{workspaceRoot}", context.WorkspaceRoot, StringComparison.Ordinal);
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
