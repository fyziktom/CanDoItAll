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
    private static readonly TimeSpan MaterializationPollInterval = TimeSpan.FromMilliseconds(50);

    public async Task<PluginIngressAcceptResult> AcceptAsync(
        PluginIngressEnvelopeRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var normalizedSourceKind = request.SourceKind.Trim();
        var normalizedSourceKey = request.SourceKey.Trim();
        var normalizedExternalMessageId = request.ExternalMessageId.Trim();
        var normalizedCursorValue = request.CursorValue?.Trim() ?? string.Empty;
        var dedupeKey = BuildDedupeKey(request);
        var existing = await dbContext.Set<PluginIngressEnvelopeRecord>()
            .FirstOrDefaultAsync(item =>
                    item.SourceKind == normalizedSourceKind &&
                    item.SourceKey == normalizedSourceKey &&
                    item.DedupeKey == dedupeKey,
                cancellationToken);
        if (existing is not null)
        {
            return new PluginIngressAcceptResult(existing.Id, true, existing.State);
        }

        var now = clock.GetUtcNow();
        var record = new PluginIngressEnvelopeRecord
        {
            SourceKind = normalizedSourceKind,
            SourceKey = normalizedSourceKey,
            ExternalMessageId = normalizedExternalMessageId,
            CursorValue = normalizedCursorValue,
            DedupeKey = dedupeKey,
            PayloadJson = string.IsNullOrWhiteSpace(request.PayloadJson)
                ? "{}"
                : request.PayloadJson.Trim(),
            CorrelationId = request.CorrelationId,
            ReceivedAtUtc = now,
            UpdatedAtUtc = now
        };

        await dbContext.Set<PluginIngressEnvelopeRecord>().AddAsync(record, cancellationToken);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            var existingId = await TryFindExistingEnvelopeIdAsync(
                normalizedSourceKind,
                normalizedSourceKey,
                dedupeKey,
                cancellationToken);
            if (existingId.HasValue)
            {
                var existingState = await GetStateAsync(existingId.Value, cancellationToken);
                return new PluginIngressAcceptResult(
                    existingId.Value,
                    true,
                    existingState ?? PluginIngressState.Accepted);
            }

            throw;
        }

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
        var normalizedSourceKind = NormalizeRequiredValue(sourceKind, nameof(sourceKind));
        var normalizedSourceKey = NormalizeRequiredValue(sourceKey, nameof(sourceKey));

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.Set<PluginIngressCursorRecord>()
            .Where(item => item.SourceKind == normalizedSourceKind && item.SourceKey == normalizedSourceKey)
            .Select(item => item.CursorValue)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task SaveCursorAsync(
        string sourceKind,
        string sourceKey,
        string cursorValue,
        CancellationToken cancellationToken = default)
    {
        var normalizedSourceKind = NormalizeRequiredValue(sourceKind, nameof(sourceKind));
        var normalizedSourceKey = NormalizeRequiredValue(sourceKey, nameof(sourceKey));
        var normalizedCursorValue = NormalizeRequiredValue(cursorValue, nameof(cursorValue));

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var record = await dbContext.Set<PluginIngressCursorRecord>()
            .FirstOrDefaultAsync(
                item => item.SourceKind == normalizedSourceKind && item.SourceKey == normalizedSourceKey,
                cancellationToken);

        if (record is null)
        {
            record = new PluginIngressCursorRecord
            {
                SourceKind = normalizedSourceKind,
                SourceKey = normalizedSourceKey
            };
            await dbContext.Set<PluginIngressCursorRecord>().AddAsync(record, cancellationToken);
        }

        record.CursorValue = normalizedCursorValue;
        record.UpdatedAtUtc = clock.GetUtcNow();
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            if (await UpdateExistingCursorAsync(
                    normalizedSourceKind,
                    normalizedSourceKey,
                    normalizedCursorValue,
                    cancellationToken))
            {
                return;
            }

            throw;
        }
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
        var normalizedMaterializerKey = NormalizeRequiredValue(materializerKey, nameof(materializerKey));
        var currentSnapshot = await GetRequiredSnapshotAsync(envelopeId, cancellationToken);

        if (currentSnapshot.State == PluginIngressState.Materialized ||
            currentSnapshot.State == PluginIngressState.Quarantined)
        {
            return currentSnapshot;
        }

        if (currentSnapshot.State == PluginIngressState.Materializing)
        {
            return await WaitForMaterializationResolutionAsync(envelopeId, cancellationToken);
        }

        var materializer = materializers.FirstOrDefault(candidate =>
            string.Equals(candidate.MaterializerKey, normalizedMaterializerKey, StringComparison.Ordinal))
            ?? throw new InvalidOperationException(
                $"Plugin ingress materializer '{normalizedMaterializerKey}' is not registered.");

        if (!await TryClaimMaterializationAsync(envelopeId, normalizedMaterializerKey, cancellationToken))
        {
            return await WaitForMaterializationResolutionAsync(envelopeId, cancellationToken);
        }

        var claimedSnapshot = await GetRequiredSnapshotAsync(envelopeId, cancellationToken);
        PluginIngressMaterializationResult result;
        try
        {
            result = await materializer.MaterializeAsync(claimedSnapshot, cancellationToken);
        }
        catch (Exception ex)
        {
            result = PluginIngressMaterializationResult.Failure(ex.Message);
        }

        return await FinalizeMaterializationAsync(
            envelopeId,
            normalizedMaterializerKey,
            result,
            cancellationToken);
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

    private static string NormalizeRequiredValue(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        return value.Trim();
    }

    private async Task<bool> UpdateExistingCursorAsync(
        string sourceKind,
        string sourceKey,
        string cursorValue,
        CancellationToken cancellationToken)
    {
        await using var verificationContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var existing = await verificationContext.Set<PluginIngressCursorRecord>()
            .FirstOrDefaultAsync(
                item => item.SourceKind == sourceKind && item.SourceKey == sourceKey,
                cancellationToken);
        if (existing is null)
        {
            return false;
        }

        existing.CursorValue = cursorValue;
        existing.UpdatedAtUtc = clock.GetUtcNow();
        await verificationContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<PluginIngressEnvelopeSnapshot> GetRequiredSnapshotAsync(
        Guid envelopeId,
        CancellationToken cancellationToken)
    {
        return await GetAsync(envelopeId, cancellationToken)
            ?? throw new InvalidOperationException($"Plugin ingress envelope '{envelopeId}' was not found.");
    }

    private async Task<bool> TryClaimMaterializationAsync(
        Guid envelopeId,
        string materializerKey,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var now = clock.GetUtcNow();
        var claimedRows = await dbContext.Set<PluginIngressEnvelopeRecord>()
            .Where(item => item.Id == envelopeId)
            .Where(item =>
                item.State == PluginIngressState.Accepted ||
                item.State == PluginIngressState.Failed)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.State, PluginIngressState.Materializing)
                .SetProperty(item => item.MaterializerKey, materializerKey)
                .SetProperty(item => item.MaterializationSummary, string.Empty)
                .SetProperty(item => item.LastError, string.Empty)
                .SetProperty(item => item.UpdatedAtUtc, now), cancellationToken);

        return claimedRows > 0;
    }

    private async Task<PluginIngressEnvelopeSnapshot> WaitForMaterializationResolutionAsync(
        Guid envelopeId,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            var snapshot = await GetRequiredSnapshotAsync(envelopeId, cancellationToken);
            if (snapshot.State != PluginIngressState.Materializing)
            {
                return snapshot;
            }

            await Task.Delay(MaterializationPollInterval, cancellationToken);
        }
    }

    private async Task<PluginIngressEnvelopeSnapshot> FinalizeMaterializationAsync(
        Guid envelopeId,
        string materializerKey,
        PluginIngressMaterializationResult result,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var record = await dbContext.Set<PluginIngressEnvelopeRecord>()
            .FirstOrDefaultAsync(item => item.Id == envelopeId, cancellationToken)
            ?? throw new InvalidOperationException($"Plugin ingress envelope '{envelopeId}' was not found.");

        var now = clock.GetUtcNow();
        record.MaterializerKey = materializerKey;
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

    private async Task<Guid?> TryFindExistingEnvelopeIdAsync(
        string sourceKind,
        string sourceKey,
        string dedupeKey,
        CancellationToken cancellationToken)
    {
        await using var verificationContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await verificationContext.Set<PluginIngressEnvelopeRecord>()
            .Where(item =>
                item.SourceKind == sourceKind &&
                item.SourceKey == sourceKey &&
                item.DedupeKey == dedupeKey)
            .Select(item => (Guid?)item.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<PluginIngressState?> GetStateAsync(Guid envelopeId, CancellationToken cancellationToken)
    {
        await using var verificationContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await verificationContext.Set<PluginIngressEnvelopeRecord>()
            .Where(item => item.Id == envelopeId)
            .Select(item => (PluginIngressState?)item.State)
            .FirstOrDefaultAsync(cancellationToken);
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
