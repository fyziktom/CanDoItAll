using System.Text.RegularExpressions;
using CanDoItAll.Mcp.DotNetWatch.Logging;
using CanDoItAll.Mcp.DotNetWatch.Processes;

namespace CanDoItAll.Mcp.DotNetWatch.Operations;

public sealed partial class OperationRecord
{
    private readonly object _gate = new();

    public OperationRecord(
        string operationId,
        OperationType operationType,
        string correlationId,
        string targetPath,
        string? framework,
        string configuration,
        WhenAppRunningPolicy whenAppRunningPolicy,
        string? affectedSessionId,
        string? runner,
        RingLogBuffer logBuffer,
        TimeSpan timeout)
    {
        OperationId = operationId;
        OperationType = operationType;
        CorrelationId = correlationId;
        TargetPath = targetPath;
        Framework = framework;
        Configuration = configuration;
        WhenAppRunningPolicy = whenAppRunningPolicy;
        AffectedSessionId = affectedSessionId;
        Runner = runner;
        LogBuffer = logBuffer;
        Timeout = timeout;
        StartedUtc = DateTimeOffset.UtcNow;
        Summary = $"{operationType} queued.";
    }

    public string OperationId { get; }

    public OperationType OperationType { get; }

    public string CorrelationId { get; }

    public string TargetPath { get; }

    public string? Framework { get; }

    public string Configuration { get; }

    public WhenAppRunningPolicy WhenAppRunningPolicy { get; }

    public string? AffectedSessionId { get; }

    public string? Runner { get; }

    public RingLogBuffer LogBuffer { get; }

    public TimeSpan Timeout { get; }

    public ManagedProcess? Process { get; private set; }

    public OperationState State { get; private set; } = OperationState.Queued;

    public DateTimeOffset StartedUtc { get; }

    public DateTimeOffset? FinishedUtc { get; private set; }

    public int? ExitCode { get; private set; }

    public string Summary { get; private set; }

    public bool ResumeAttempted { get; private set; }

    public bool ResumeSucceeded { get; private set; }

    public string? ResumedSessionId { get; private set; }

    public int? TotalTests { get; private set; }

    public int? PassedTests { get; private set; }

    public int? FailedTests { get; private set; }

    public int? SkippedTests { get; private set; }

    public void AttachProcess(ManagedProcess process)
    {
        lock (_gate)
        {
            Process = process;
            State = OperationState.Running;
            Summary = $"{OperationType} running.";
        }
    }

    public void NoteLog(LogEntry entry)
    {
        if (OperationType == OperationType.Test)
        {
            if (TestSummaryRegex().Match(entry.Text) is { Success: true } match)
            {
                lock (_gate)
                {
                    PassedTests = ParseNullableInt(match.Groups["passed"].Value);
                    FailedTests = ParseNullableInt(match.Groups["failed"].Value);
                    SkippedTests = ParseNullableInt(match.Groups["skipped"].Value);
                    TotalTests = ParseNullableInt(match.Groups["total"].Value);
                    Summary = $"Tests passed={PassedTests ?? 0}, failed={FailedTests ?? 0}, skipped={SkippedTests ?? 0}.";
                }
            }
        }

        if (entry.Text.Contains("Build succeeded", StringComparison.OrdinalIgnoreCase))
        {
            lock (_gate)
            {
                Summary = "Build succeeded.";
            }
        }

        if (entry.Text.Contains("Build FAILED", StringComparison.OrdinalIgnoreCase) ||
            entry.Text.Contains("Test Run Failed", StringComparison.OrdinalIgnoreCase))
        {
            lock (_gate)
            {
                Summary = $"{OperationType} failed.";
            }
        }
    }

    public void MarkCompleted(int? exitCode, string summary)
    {
        lock (_gate)
        {
            State = exitCode == 0 ? OperationState.Completed : OperationState.Failed;
            ExitCode = exitCode;
            Summary = summary;
            FinishedUtc = DateTimeOffset.UtcNow;
            Process = null;
        }
    }

    public void MarkTimedOut()
    {
        lock (_gate)
        {
            State = OperationState.TimedOut;
            Summary = $"{OperationType} timed out.";
            FinishedUtc = DateTimeOffset.UtcNow;
            Process = null;
        }
    }

    public void SetResumeOutcome(bool attempted, bool success, string? sessionId)
    {
        lock (_gate)
        {
            ResumeAttempted = attempted;
            ResumeSucceeded = success;
            ResumedSessionId = sessionId;
        }
    }

    public OperationStatusData ToStatusData()
    {
        lock (_gate)
        {
            var finishedUtc = FinishedUtc;
            var elapsed = (long)((finishedUtc ?? DateTimeOffset.UtcNow) - StartedUtc).TotalMilliseconds;

            return new OperationStatusData(
                OperationId,
                OperationType,
                State,
                StartedUtc,
                finishedUtc,
                elapsed,
                ExitCode,
                Summary,
                Runner,
                new ResumeOutcomeData(ResumeAttempted, ResumeSucceeded, ResumedSessionId),
                LogBuffer.CurrentSequence,
                OperationType == OperationType.Test
                    ? new TestSummaryData(TotalTests, PassedTests, FailedTests, SkippedTests)
                    : null);
        }
    }

    private static int? ParseNullableInt(string value)
    {
        return int.TryParse(value, out var parsed) ? parsed : null;
    }

    [GeneratedRegex(@"Failed:\s*(?<failed>\d+),\s*Passed:\s*(?<passed>\d+),\s*Skipped:\s*(?<skipped>\d+),\s*Total:\s*(?<total>\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex TestSummaryRegex();
}

public sealed class OperationRegistry
{
    private readonly object _gate = new();
    private readonly Dictionary<string, OperationRecord> _operations = new(StringComparer.OrdinalIgnoreCase);

    public void Add(OperationRecord operation)
    {
        lock (_gate)
        {
            _operations[operation.OperationId] = operation;
        }
    }

    public OperationRecord? GetById(string? operationId)
    {
        lock (_gate)
        {
            if (string.IsNullOrWhiteSpace(operationId))
            {
                return _operations.Values.OrderByDescending(static operation => operation.StartedUtc).FirstOrDefault();
            }

            _operations.TryGetValue(operationId, out var operation);
            return operation;
        }
    }

    public IReadOnlyList<OperationRecord> GetActiveOperations()
    {
        lock (_gate)
        {
            return _operations.Values
                .Where(operation => operation.State is OperationState.Queued or OperationState.Running)
                .OrderByDescending(static operation => operation.StartedUtc)
                .ToList();
        }
    }

    public OperationRecord? GetLastFailed()
    {
        lock (_gate)
        {
            return _operations.Values
                .Where(operation => operation.State is OperationState.Failed or OperationState.TimedOut)
                .OrderByDescending(static operation => operation.StartedUtc)
                .FirstOrDefault();
        }
    }
}
