using System.Text;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Plugins.Abstractions;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Plugins;

public sealed class PluginLogStore(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IClock clock)
{
    private const int MaxMessageLength = 1200;
    private const int MaxDetailsLength = 8000;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<PluginLogItem> WriteAsync(
        PluginLogWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var record = new PluginLogRecord
        {
            PluginId = request.PluginId?.Value ?? string.Empty,
            PackageId = request.PackageId?.Value ?? string.Empty,
            WorkflowExecutorId = request.WorkflowExecutorId?.Value ?? string.Empty,
            StreamKind = request.StreamKind.ToString(),
            OperationKind = request.OperationKind.ToString(),
            Severity = request.Severity.ToString(),
            Status = NormalizeStatus(request.Status),
            Message = Truncate(WorkflowExecutorRedaction.RedactText(request.Message), MaxMessageLength),
            DetailsJson = NormalizeDetailsJson(request.DetailsJson),
            CorrelationId = string.IsNullOrWhiteSpace(request.CorrelationId) ? Guid.NewGuid().ToString("N") : request.CorrelationId.Trim(),
            CreatedAtUtc = clock.GetUtcNow()
        };

        dbContext.Set<PluginLogRecord>().Add(record);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToItem(record);
    }

    public async Task<IReadOnlyList<PluginLogItem>> ListAsync(
        PluginLogQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var take = Math.Clamp(query.Take, 1, 500);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var sql = new StringBuilder("SELECT * FROM \"Plugins_Logs\"");
        var filters = new List<string>();
        var parameters = new List<object>();

        if (query.StreamKind is { } streamKind)
        {
            var stream = streamKind.ToString();
            filters.Add($@"""StreamKind"" = {{{parameters.Count}}}");
            parameters.Add(stream);
        }

        if (query.PluginId is { } pluginId)
        {
            filters.Add($@"""PluginId"" = {{{parameters.Count}}}");
            parameters.Add(pluginId.Value);
        }

        if (query.PackageId is { } packageId)
        {
            filters.Add($@"""PackageId"" = {{{parameters.Count}}}");
            parameters.Add(packageId.Value);
        }

        if (query.MinimumSeverity is { } severity)
        {
            var severities = ResolveSeverityFilter(severity);
            var placeholders = severities
                .Select(severityValue =>
                {
                    var placeholder = $"{{{parameters.Count}}}";
                    parameters.Add(severityValue);
                    return placeholder;
                });
            filters.Add($@"""Severity"" IN ({string.Join(", ", placeholders)})");
        }

        if (filters.Count > 0)
        {
            sql.Append(" WHERE ");
            sql.Append(string.Join(" AND ", filters));
        }

        sql.Append($@" ORDER BY ""CreatedAtUtc"" DESC, ""Id"" DESC LIMIT {{{parameters.Count}}}");
        parameters.Add(take);

        var records = await dbContext.Set<PluginLogRecord>()
            .FromSqlRaw(sql.ToString(), parameters.ToArray())
            .AsNoTracking()
            .ToArrayAsync(cancellationToken);

        return records.Select(ToItem).ToArray();
    }

    public static string SerializeDetails(object details)
        => JsonSerializer.Serialize(details, JsonOptions);

    private static string NormalizeStatus(string status)
        => string.IsNullOrWhiteSpace(status) ? "Recorded" : Truncate(status.Trim(), 80);

    private static string NormalizeDetailsJson(string detailsJson)
    {
        if (string.IsNullOrWhiteSpace(detailsJson))
        {
            return "{}";
        }

        var redacted = WorkflowExecutorRedaction.RedactSettingsJson(detailsJson);
        return Truncate(redacted, MaxDetailsLength);
    }

    private static string[] ResolveSeverityFilter(PluginLogSeverity severity)
        => severity switch
        {
            PluginLogSeverity.Error => [PluginLogSeverity.Error.ToString()],
            PluginLogSeverity.Warning => [PluginLogSeverity.Warning.ToString(), PluginLogSeverity.Error.ToString()],
            _ => Enum.GetValues<PluginLogSeverity>().Select(item => item.ToString()).ToArray()
        };

    private static PluginLogItem ToItem(PluginLogRecord record)
        => new(
            record.Id,
            Enum.TryParse<PluginLogStreamKind>(record.StreamKind, out var streamKind) ? streamKind : PluginLogStreamKind.Installation,
            Enum.TryParse<PluginLogOperationKind>(record.OperationKind, out var operationKind) ? operationKind : PluginLogOperationKind.PluginEvent,
            Enum.TryParse<PluginLogSeverity>(record.Severity, out var severity) ? severity : PluginLogSeverity.Warning,
            record.Status,
            record.Message,
            record.DetailsJson,
            string.IsNullOrWhiteSpace(record.PluginId) ? null : new PluginId(record.PluginId),
            string.IsNullOrWhiteSpace(record.PackageId) ? null : new PluginPackageId(record.PackageId),
            string.IsNullOrWhiteSpace(record.WorkflowExecutorId) ? null : new WorkflowExecutorId(record.WorkflowExecutorId),
            record.CorrelationId,
            record.CreatedAtUtc);

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength];
}

public sealed class PluginWorkflowExecutorExecutionObserver(
    PluginLogStore logStore) : IWorkflowExecutorExecutionObserver
{
    public async ValueTask RecordAsync(
        WorkflowExecutorExecutionAuditRecord auditRecord,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(auditRecord);
        if (auditRecord.SourceKind == WorkflowExecutorSourceKind.BuiltIn ||
            string.IsNullOrWhiteSpace(auditRecord.PluginId))
        {
            return;
        }

        await logStore.WriteAsync(new PluginLogWriteRequest(
            PluginLogStreamKind.Runtime,
            ResolveOperationKind(auditRecord.Status),
            auditRecord.Status == WorkflowExecutorExecutionAuditStatus.Failed ? PluginLogSeverity.Error : PluginLogSeverity.Information,
            auditRecord.Status.ToString(),
            auditRecord.RedactedMessage,
            PluginLogStore.SerializeDetails(new
            {
                auditRecord.WorkflowId,
                auditRecord.VersionId,
                auditRecord.RunId,
                auditRecord.NodeId,
                auditRecord.AttemptNumber,
                auditRecord.MaxAttempts,
                auditRecord.TimeoutSeconds,
                auditRecord.CaptureOutputArtifact,
                auditRecord.RedactedSettingsSummary,
                auditRecord.PayloadCharacters,
                auditRecord.OccurredAtUtc
            }),
            new PluginId(auditRecord.PluginId),
            string.IsNullOrWhiteSpace(auditRecord.PackageId) ? null : new PluginPackageId(auditRecord.PackageId),
            auditRecord.ExecutorId),
            cancellationToken);
    }

    private static PluginLogOperationKind ResolveOperationKind(WorkflowExecutorExecutionAuditStatus status)
        => status switch
        {
            WorkflowExecutorExecutionAuditStatus.Started => PluginLogOperationKind.ExecutorStarted,
            WorkflowExecutorExecutionAuditStatus.Completed => PluginLogOperationKind.ExecutorCompleted,
            _ => PluginLogOperationKind.ExecutorFailed
        };
}

public sealed class DurablePluginExecutionEvents(
    PluginLogStore logStore) : IPluginExecutionEvents
{
    public async ValueTask RecordAsync(
        PluginExecutionEvent pluginEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pluginEvent);
        await logStore.WriteAsync(new PluginLogWriteRequest(
            PluginLogStreamKind.Runtime,
            PluginLogOperationKind.PluginEvent,
            PluginLogSeverity.Information,
            pluginEvent.EventName,
            pluginEvent.RedactedMessage,
            PluginLogStore.SerializeDetails(new
            {
                pluginEvent.WorkflowId,
                pluginEvent.VersionId,
                pluginEvent.RunId,
                pluginEvent.NodeId,
                pluginEvent.ConnectionId
            }),
            pluginEvent.PluginId,
            WorkflowExecutorId: pluginEvent.ExecutorId),
            cancellationToken);
    }
}
