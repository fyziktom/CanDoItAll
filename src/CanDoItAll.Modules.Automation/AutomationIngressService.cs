using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Automation;

public sealed class PluginIngressInbox(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IEnumerable<IPluginIngressMaterializer> materializers,
    IAutomationTelemetryPublisher telemetryPublisher,
    IClock clock) : IPluginIngressInbox
{
    public async Task<PluginIngressAcceptResult> AcceptAsync(
        PluginIngressEnvelopeRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var dedupeKey = BuildDedupeKey(request);
        var existing = await dbContext.Set<PluginIngressEnvelopeRecord>()
            .FirstOrDefaultAsync(item =>
                    item.SourceKind == request.SourceKind &&
                    item.SourceKey == request.SourceKey &&
                    item.DedupeKey == dedupeKey,
                cancellationToken);
        if (existing is not null)
        {
            return new PluginIngressAcceptResult(existing.Id, true, existing.State);
        }

        var now = clock.GetUtcNow();
        var record = new PluginIngressEnvelopeRecord
        {
            SourceKind = request.SourceKind.Trim(),
            SourceKey = request.SourceKey.Trim(),
            ExternalMessageId = request.ExternalMessageId.Trim(),
            CursorValue = request.CursorValue?.Trim() ?? string.Empty,
            DedupeKey = dedupeKey,
            PayloadJson = string.IsNullOrWhiteSpace(request.PayloadJson)
                ? "{}"
                : request.PayloadJson.Trim(),
            CorrelationId = request.CorrelationId,
            ReceivedAtUtc = now,
            UpdatedAtUtc = now
        };

        await dbContext.Set<PluginIngressEnvelopeRecord>().AddAsync(record, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        await telemetryPublisher.PublishAsync(new AutomationTelemetryEvent(
            AutomationExecutionLogKind.IngressAccepted,
            "plugin-ingress-envelope",
            record.Id.ToString("N"),
            record.CorrelationId,
            null,
            $"Accepted ingress envelope for {record.SourceKind}/{record.SourceKey}.",
            $$"""
              {
                "externalMessageId":"{{EscapeJson(record.ExternalMessageId)}}",
                "cursorValue":"{{EscapeJson(record.CursorValue)}}"
              }
              """), cancellationToken);

        return new PluginIngressAcceptResult(record.Id, false, record.State);
    }

    public async Task<string?> GetCursorAsync(
        string sourceKind,
        string sourceKey,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.Set<PluginIngressCursorRecord>()
            .Where(item => item.SourceKind == sourceKind && item.SourceKey == sourceKey)
            .Select(item => item.CursorValue)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task SaveCursorAsync(
        string sourceKind,
        string sourceKey,
        string cursorValue,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceKind);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(cursorValue);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var record = await dbContext.Set<PluginIngressCursorRecord>()
            .FirstOrDefaultAsync(item => item.SourceKind == sourceKind && item.SourceKey == sourceKey, cancellationToken);

        if (record is null)
        {
            record = new PluginIngressCursorRecord
            {
                SourceKind = sourceKind.Trim(),
                SourceKey = sourceKey.Trim()
            };
            await dbContext.Set<PluginIngressCursorRecord>().AddAsync(record, cancellationToken);
        }

        record.CursorValue = cursorValue.Trim();
        record.UpdatedAtUtc = clock.GetUtcNow();
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<PluginIngressEnvelopeSnapshot?> GetAsync(
        Guid envelopeId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var record = await dbContext.Set<PluginIngressEnvelopeRecord>()
            .FirstOrDefaultAsync(item => item.Id == envelopeId, cancellationToken);
        return record is null
            ? null
            : Map(record);
    }

    public async Task<PluginIngressEnvelopeSnapshot> MaterializeAsync(
        Guid envelopeId,
        string materializerKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(materializerKey);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var record = await dbContext.Set<PluginIngressEnvelopeRecord>()
            .FirstOrDefaultAsync(item => item.Id == envelopeId, cancellationToken)
            ?? throw new InvalidOperationException($"Plugin ingress envelope '{envelopeId}' was not found.");

        var materializer = materializers.FirstOrDefault(candidate =>
            string.Equals(candidate.MaterializerKey, materializerKey, StringComparison.Ordinal))
            ?? throw new InvalidOperationException(
                $"Plugin ingress materializer '{materializerKey}' is not registered.");

        var snapshot = Map(record);
        var result = await materializer.MaterializeAsync(snapshot, cancellationToken);
        var now = clock.GetUtcNow();

        record.MaterializerKey = materializerKey.Trim();
        record.UpdatedAtUtc = now;
        if (result.IsSuccess)
        {
            record.State = PluginIngressState.Materialized;
            record.MaterializationSummary = result.Summary.Trim();
            record.LastError = string.Empty;
            record.MaterializedAtUtc = now;
        }
        else
        {
            record.State = PluginIngressState.Failed;
            record.MaterializationSummary = string.Empty;
            record.LastError = result.ErrorMessage.Trim();
            record.MaterializedAtUtc = null;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        await telemetryPublisher.PublishAsync(new AutomationTelemetryEvent(
            AutomationExecutionLogKind.IngressMaterialized,
            "plugin-ingress-envelope",
            record.Id.ToString("N"),
            record.CorrelationId,
            null,
            result.IsSuccess
                ? $"Materialized ingress envelope {record.Id:N} with '{materializerKey}'."
                : $"Materialization for ingress envelope {record.Id:N} failed in '{materializerKey}'.",
            $$"""
              {
                "materializerKey":"{{EscapeJson(record.MaterializerKey)}}",
                "state":"{{record.State}}",
                "summary":"{{EscapeJson(record.MaterializationSummary)}}",
                "error":"{{EscapeJson(record.LastError)}}"
              }
              """), cancellationToken);

        return Map(record);
    }

    private static void ValidateRequest(PluginIngressEnvelopeRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SourceKind);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SourceKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ExternalMessageId);
    }

    private static string BuildDedupeKey(PluginIngressEnvelopeRequest request)
    {
        var cursorPart = string.IsNullOrWhiteSpace(request.CursorValue)
            ? "-"
            : request.CursorValue.Trim();

        return $"{request.SourceKind.Trim()}::{request.SourceKey.Trim()}::{request.ExternalMessageId.Trim()}::{cursorPart}";
    }

    private static PluginIngressEnvelopeSnapshot Map(PluginIngressEnvelopeRecord record)
    {
        return new PluginIngressEnvelopeSnapshot(
            record.Id,
            record.SourceKind,
            record.SourceKey,
            record.ExternalMessageId,
            record.CursorValue,
            record.DedupeKey,
            record.State,
            record.PayloadJson,
            record.MaterializerKey,
            record.MaterializationSummary,
            record.LastError,
            record.CorrelationId,
            record.ReceivedAtUtc,
            record.UpdatedAtUtc,
            record.MaterializedAtUtc);
    }

    private static string EscapeJson(string? value)
    {
        return (value ?? string.Empty)
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal);
    }
}
