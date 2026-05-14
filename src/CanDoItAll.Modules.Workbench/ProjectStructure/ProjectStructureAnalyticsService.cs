using System.Text.Json;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Workbench;

public sealed record ProjectStructureAnalyticsWriteRequest(
    string OperationName,
    Guid? ProjectId,
    string? NodeKey,
    ProjectStructureLeaseScopeKind? ScopeKind,
    string? ScopeKey,
    ProjectStructureAgentContext Agent,
    bool Succeeded,
    long DurationMs,
    IReadOnlyList<string> Warnings,
    string? ErrorCode,
    string? ErrorMessage,
    string RequestSummaryJson,
    string ResponseSummaryJson);

public sealed class ProjectStructureAnalyticsService(
    IDbContextFactory<AppDbContext> dbContextFactory)
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task RecordAsync(ProjectStructureAnalyticsWriteRequest request, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await ProjectStructureAgentSchemaInitializer.EnsureAsync(dbContext, cancellationToken);

        await dbContext.Set<ProjectStructureOperationAnalyticsRecord>().AddAsync(
            new ProjectStructureOperationAnalyticsRecord
            {
                OperationName = NormalizeText(request.OperationName, "unknown"),
                ProjectId = request.ProjectId,
                NodeKey = NormalizeNullableText(request.NodeKey),
                ScopeKind = request.ScopeKind,
                ScopeKey = NormalizeNullableText(request.ScopeKey),
                AgentId = NormalizeText(request.Agent.AgentId, "anonymous-agent"),
                AgentName = NormalizeText(request.Agent.AgentName, "Anonymous agent"),
                MachineName = NormalizeText(request.Agent.MachineName, "unknown-machine"),
                RepositoryRoot = NormalizeText(request.Agent.RepositoryRoot, string.Empty),
                BranchName = NormalizeText(request.Agent.BranchName, string.Empty),
                Succeeded = request.Succeeded,
                DurationMs = Math.Max(0, request.DurationMs),
                WarningCount = request.Warnings.Count,
                ErrorCode = NormalizeNullableText(request.ErrorCode),
                ErrorMessage = NormalizeNullableText(request.ErrorMessage),
                RequestSummaryJson = NormalizeJson(request.RequestSummaryJson, "{}"),
                ResponseSummaryJson = NormalizeJson(request.ResponseSummaryJson, "{}"),
                WarningsJson = NormalizeWarnings(request.Warnings),
                OccurredAtUtc = DateTimeOffset.UtcNow
            },
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<ProjectStructureAnalyticsResponse> QueryAsync(
        ProjectStructureAnalyticsQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await ProjectStructureAgentSchemaInitializer.EnsureAsync(dbContext, cancellationToken);

        var take = Math.Clamp(request.Take, 1, 200);
        var query = dbContext.Set<ProjectStructureOperationAnalyticsRecord>()
            .AsNoTracking()
            .AsQueryable();

        if (request.ProjectId.HasValue)
        {
            query = query.Where(item => item.ProjectId == request.ProjectId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.OperationName))
        {
            var operationName = request.OperationName.Trim();
            query = query.Where(item => item.OperationName == operationName);
        }

        if (!string.IsNullOrWhiteSpace(request.AgentId))
        {
            var agentId = request.AgentId.Trim();
            query = query.Where(item => item.AgentId == agentId);
        }

        if (request.Succeeded.HasValue)
        {
            query = query.Where(item => item.Succeeded == request.Succeeded.Value);
        }

        if (dbContext.Database.IsSqlite())
        {
            var records = await query.ToListAsync(cancellationToken);
            var sqliteEntries = records
                .OrderByDescending(item => item.OccurredAtUtc)
                .Take(take)
                .Select(item => new ProjectStructureAnalyticsEntry(
                    item.Id,
                    item.OperationName,
                    item.ProjectId,
                    item.NodeKey,
                    item.ScopeKind,
                    item.ScopeKey,
                    item.AgentId,
                    item.AgentName,
                    item.MachineName,
                    item.RepositoryRoot,
                    item.BranchName,
                    item.Succeeded,
                    item.DurationMs,
                    item.WarningCount,
                    item.ErrorCode,
                    item.ErrorMessage,
                    item.RequestSummaryJson,
                    item.ResponseSummaryJson,
                    item.WarningsJson,
                    item.OccurredAtUtc))
                .ToList();

            return new ProjectStructureAnalyticsResponse(sqliteEntries);
        }

        var entries = await query
            .OrderByDescending(item => item.OccurredAtUtc)
            .Take(take)
            .Select(item => new ProjectStructureAnalyticsEntry(
                item.Id,
                item.OperationName,
                item.ProjectId,
                item.NodeKey,
                item.ScopeKind,
                item.ScopeKey,
                item.AgentId,
                item.AgentName,
                item.MachineName,
                item.RepositoryRoot,
                item.BranchName,
                item.Succeeded,
                item.DurationMs,
                item.WarningCount,
                item.ErrorCode,
                item.ErrorMessage,
                item.RequestSummaryJson,
                item.ResponseSummaryJson,
                item.WarningsJson,
                item.OccurredAtUtc))
            .ToListAsync(cancellationToken);

        return new ProjectStructureAnalyticsResponse(entries);
    }

    public static string SerializeSummary(object? value)
    {
        try
        {
            return JsonSerializer.Serialize(value ?? new { }, SerializerOptions);
        }
        catch
        {
            return "{}";
        }
    }

    private static string NormalizeWarnings(IReadOnlyList<string> warnings)
    {
        try
        {
            var normalized = warnings
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(item => item.Trim())
                .ToList();
            return JsonSerializer.Serialize(normalized, SerializerOptions);
        }
        catch
        {
            return "[]";
        }
    }

    private static string NormalizeJson(string? json, string fallback)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return fallback;
        }

        return json.Trim();
    }

    private static string NormalizeText(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        return value.Trim();
    }

    private static string? NormalizeNullableText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }
}
