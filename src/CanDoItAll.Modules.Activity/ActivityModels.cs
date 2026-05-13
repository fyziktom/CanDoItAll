using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Search;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Activity;

public sealed class ActivityEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Category { get; set; } = string.Empty;

    public string Action { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public Guid? ProjectId { get; set; }

    public string? ArtifactKind { get; set; }

    public Guid? ArtifactId { get; set; }

    public string? Route { get; set; }

    public string Actor { get; set; } = "local-user";

    public string? IdempotencyKey { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}

internal sealed class ActivityEntryConfiguration : IEntityTypeConfiguration<ActivityEntry>
{
    public void Configure(EntityTypeBuilder<ActivityEntry> builder)
    {
        builder.ToTable("Activity_Entries");
        builder.HasKey(entry => entry.Id);
        builder.Property(entry => entry.Category).HasMaxLength(80).IsRequired();
        builder.Property(entry => entry.Action).HasMaxLength(80).IsRequired();
        builder.Property(entry => entry.Title).HasMaxLength(200).IsRequired();
        builder.Property(entry => entry.Description).HasColumnType("TEXT");
        builder.Property(entry => entry.ArtifactKind).HasMaxLength(120);
        builder.Property(entry => entry.Route).HasMaxLength(500);
        builder.Property(entry => entry.Actor).HasMaxLength(120).IsRequired();
        builder.Property(entry => entry.IdempotencyKey).HasMaxLength(200);
        builder.HasIndex(entry => entry.CreatedAtUtc);
        builder.HasIndex(entry => entry.IdempotencyKey).IsUnique();
    }
}

public sealed record ActivityTimelineItem(
    Guid Id,
    string Category,
    string Action,
    string Title,
    string Description,
    string? Route,
    DateTimeOffset CreatedAtUtc,
    string Actor);

/* codex-capsule
kind: service
name: ActivityService
summary: Persists user-visible activity entries and exposes timeline queries for the shell and activity page.
owns: activity-entry writes, timeline queries, lightweight search bridge
deps: AppDbContext, IClock, ISearchIndexService
risks: noisy-activity, missing-route
tests: unit:ActivityServiceTests, integration:ActivityPersistenceTests
inputs: ActivityWriteRequest, search text
outputs: ActivityTimelineItem list, SearchResult list
*/
public sealed class ActivityService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IClock clock,
    ISearchIndexService searchIndexService,
    ILogger<ActivityService> logger) : IActivityStream
{
    public async Task RecordAsync(ActivityWriteRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Category) ||
            string.IsNullOrWhiteSpace(request.Action) ||
            string.IsNullOrWhiteSpace(request.Title))
        {
            return;
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var normalizedIdempotencyKey = string.IsNullOrWhiteSpace(request.IdempotencyKey)
            ? null
            : request.IdempotencyKey.Trim();
        logger.LogInformation(
            "Recording activity entry Category={Category} Action={Action} Title={Title} IdempotencyKey={IdempotencyKey}.",
            request.Category,
            request.Action,
            request.Title,
            normalizedIdempotencyKey ?? "<none>");
        if (normalizedIdempotencyKey is not null)
        {
            var alreadyRecorded = await dbContext.Set<ActivityEntry>()
                .AnyAsync(entry => entry.IdempotencyKey == normalizedIdempotencyKey, cancellationToken);
            if (alreadyRecorded)
            {
                return;
            }
        }

        await dbContext.Set<ActivityEntry>().AddAsync(
            new ActivityEntry
            {
                Category = request.Category.Trim(),
                Action = request.Action.Trim(),
                Title = request.Title.Trim(),
                Description = request.Description?.Trim() ?? string.Empty,
                ProjectId = request.ProjectId,
                ArtifactKind = request.ArtifactKind?.Trim(),
                ArtifactId = request.ArtifactId,
                Route = request.Route?.Trim(),
                Actor = string.IsNullOrWhiteSpace(request.Actor) ? "local-user" : request.Actor.Trim(),
                IdempotencyKey = normalizedIdempotencyKey,
                CreatedAtUtc = clock.GetUtcNow()
            },
            cancellationToken);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation(
                "Recorded activity entry Category={Category} Action={Action} Title={Title}.",
                request.Category,
                request.Action,
                request.Title);
        }
        catch (DbUpdateException exception) when (normalizedIdempotencyKey is not null && DbUpdateExceptionClassifier.IsUniqueConstraintViolation(exception))
        {
            return;
        }
    }

    public async Task<IReadOnlyList<ActivityTimelineItem>> ListRecentAsync(int take = 40, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var boundedTake = Math.Clamp(take, 1, 100);
        var query = dbContext.Set<ActivityEntry>()
            .AsNoTracking()
            .Select(entry => new ActivityTimelineItem(
                entry.Id,
                entry.Category,
                entry.Action,
                entry.Title,
                entry.Description,
                entry.Route,
                entry.CreatedAtUtc,
                entry.Actor));

        if (dbContext.Database.IsSqlite())
        {
            return (await query.ToListAsync(cancellationToken))
                .OrderByDescending(entry => entry.CreatedAtUtc)
                .Take(boundedTake)
                .ToList();
        }

        return await query
            .OrderByDescending(entry => entry.CreatedAtUtc)
            .Take(boundedTake)
            .ToListAsync(cancellationToken);
    }

    public Task<IReadOnlyList<SearchResult>> SearchAsync(string query, int take = 12, CancellationToken cancellationToken = default)
        => searchIndexService.SearchAsync(query, take, cancellationToken);
}


