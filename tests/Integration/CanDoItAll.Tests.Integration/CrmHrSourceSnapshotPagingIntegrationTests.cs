using System.Collections.Concurrent;
using System.Data.Common;
using CanDoItAll.Composition;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Memory.SourceGateway;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CanDoItAll.Tests.Integration.CrmHr;

public sealed class CrmHrSourceSnapshotPagingIntegrationTests
{
    [Fact]
    public async Task Second_party_page_bounds_primary_and_related_queries_to_returned_items()
    {
        AppDbContextModelRegistry.ConfigureAssemblies(ModuleAssemblies.All);
        await using var database = PostgresTestDatabaseLease.Create("crmhrsnapshotpaging");
        var interceptor = new QueryCommandInterceptor();
        var options = new DbContextOptionsBuilder<AppDbContext>(database.CreateAppDbContextOptions())
            .AddInterceptors(interceptor)
            .Options;
        var factory = new TestDbContextFactory(options);
        var partyIds = Enumerable.Range(0, 6)
            .Select(index => DeterministicGuid(20_000 + index))
            .ToArray();

        await using (var dbContext = factory.CreateDbContext())
        {
            await dbContext.Database.EnsureCreatedAsync();
            for (var index = 0; index < partyIds.Length; index++)
            {
                var partyId = partyIds[index];
                dbContext.Set<Party>().Add(new Party
                {
                    Id = partyId,
                    PartyType = PartyType.Person,
                    LifecycleStatus = PartyLifecycleStatus.Active,
                    DisplayName = $"Paged party {index:D2}",
                    Summary = $"Public summary {index:D2}",
                    CreatedAtUtc = DateTimeOffset.UnixEpoch,
                    UpdatedAtUtc = DateTimeOffset.UnixEpoch.AddDays(index)
                });
                dbContext.Set<PartyRoleAssignment>().Add(new PartyRoleAssignment
                {
                    PartyId = partyId,
                    RoleKind = PartyRoleKind.Employee,
                    Title = $"Role {index:D2}",
                    IsPrimary = true
                });
                dbContext.Set<PartyContactPoint>().Add(new PartyContactPoint
                {
                    PartyId = partyId,
                    ContactType = PartyContactType.Email,
                    Label = "Private",
                    Value = $"private-{index:D2}@secret.test",
                    NormalizedValue = $"private-{index:D2}@secret.test",
                    IsPrimary = true,
                    IsPublic = false
                });
                dbContext.Set<PartyConfidentialNote>().Add(new PartyConfidentialNote
                {
                    PartyId = partyId,
                    Category = PartyConfidentialNoteCategories.Compliance,
                    NoteText = $"confidential-{index:D2}-secret",
                    CreatedBy = "integration-test",
                    CreatedAtUtc = DateTimeOffset.UnixEpoch,
                    UpdatedAtUtc = DateTimeOffset.UnixEpoch
                });
            }

            await dbContext.SaveChangesAsync();
        }

        var provider = new CrmHrSourceSnapshotProvider(factory);
        var first = await provider.ReadSnapshotAsync(new CrmHrSourceSnapshotRequest(Take: 2));
        interceptor.Clear();

        var second = await provider.ReadSnapshotAsync(new CrmHrSourceSnapshotRequest(
            Cursor: first.Manifest.NextCursor,
            Take: 2));
        var repeatedSecond = await provider.ReadSnapshotAsync(new CrmHrSourceSnapshotRequest(
            Cursor: first.Manifest.NextCursor,
            Take: 2));

        var expectedIds = partyIds
            .OrderBy(id => id)
            .Skip(2)
            .Take(2)
            .ToArray();
        Assert.Equal(6, second.Manifest.TotalItemCount);
        Assert.Equal(MemorySourceSnapshotHashScope.PageScoped, second.Manifest.SnapshotHashScope);
        Assert.Equal(MemorySourceSnapshotProviderVersions.CrmHr, second.Manifest.ProviderVersion);
        Assert.Equal(
            expectedIds.Select(id => $"party:{id:D}"),
            second.Items.Select(item => item.Provenance.SourceEntityId));
        Assert.Equal(second.Manifest.SnapshotId, repeatedSecond.Manifest.SnapshotId);
        Assert.Equal(
            second.Items.Select(item => item.ContentHash),
            repeatedSecond.Items.Select(item => item.ContentHash));

        var combinedContent = string.Join(Environment.NewLine, second.Items.Select(item => item.Content));
        Assert.Contains(MemorySourceSnapshotSecurity.RedactedValue, combinedContent, StringComparison.Ordinal);
        Assert.DoesNotContain("secret.test", combinedContent, StringComparison.Ordinal);
        Assert.DoesNotContain("confidential-", combinedContent, StringComparison.Ordinal);

        var pageCommands = interceptor.Commands
            .Where(command =>
                command.CommandText.Contains("CrmHr_Parties", StringComparison.Ordinal) &&
                command.CommandText.Contains("LIMIT", StringComparison.OrdinalIgnoreCase) &&
                command.CommandText.Contains("OFFSET", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        Assert.NotEmpty(pageCommands);
        var relatedCommands = interceptor.Commands
            .Where(command =>
                command.CommandText.Contains("CrmHr_PartyRoles", StringComparison.Ordinal) ||
                command.CommandText.Contains("CrmHr_PartyContactPoints", StringComparison.Ordinal) ||
                command.CommandText.Contains("CrmHr_ConfidentialNotes", StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(6, relatedCommands.Length);
        Assert.All(relatedCommands, command =>
        {
            Assert.Equal(
                expectedIds.OrderBy(id => id),
                command.GuidValues.OrderBy(id => id));
            Assert.DoesNotContain(partyIds[0], command.GuidValues);
            Assert.DoesNotContain(partyIds[1], command.GuidValues);
            Assert.DoesNotContain(partyIds[4], command.GuidValues);
            Assert.DoesNotContain(partyIds[5], command.GuidValues);
        });

        var staleCursor = MemorySourceSnapshotCursor.Create(
            MemorySourceKind.CrmHr,
            Guid.Empty,
            MemorySourceSnapshotProviderVersions.CrmHr,
            2,
            second.Items[^1].Id);
        var exception = await Assert.ThrowsAsync<MemorySourceSnapshotCursorException>(
            async () => await provider.ReadSnapshotAsync(new CrmHrSourceSnapshotRequest(
                Cursor: staleCursor,
                Take: 2)));
        Assert.Equal(MemorySourceSnapshotCursorFailureReason.StaleAnchor, exception.Reason);
    }

    [Fact]
    public async Task Second_interaction_page_loads_links_only_for_returned_items()
    {
        AppDbContextModelRegistry.ConfigureAssemblies(ModuleAssemblies.All);
        await using var database = PostgresTestDatabaseLease.Create("crmhrinteractionpaging");
        var interceptor = new QueryCommandInterceptor();
        var options = new DbContextOptionsBuilder<AppDbContext>(database.CreateAppDbContextOptions())
            .AddInterceptors(interceptor)
            .Options;
        var factory = new TestDbContextFactory(options);
        var interactionIds = Enumerable.Range(0, 6)
            .Select(index => DeterministicGuid(30_000 + index))
            .ToArray();
        var partyIds = Enumerable.Range(0, interactionIds.Length)
            .Select(index => DeterministicGuid(40_000 + index))
            .ToArray();

        await using (var dbContext = factory.CreateDbContext())
        {
            await dbContext.Database.EnsureCreatedAsync();
            for (var index = 0; index < interactionIds.Length; index++)
            {
                dbContext.Set<Party>().Add(new Party
                {
                    Id = partyIds[index],
                    PartyType = PartyType.Organization,
                    LifecycleStatus = PartyLifecycleStatus.Active,
                    DisplayName = $"Interaction account {index:D2}",
                    CreatedAtUtc = DateTimeOffset.UnixEpoch,
                    UpdatedAtUtc = DateTimeOffset.UnixEpoch
                });
                dbContext.Set<InteractionRecord>().Add(new InteractionRecord
                {
                    Id = interactionIds[index],
                    InteractionType = InteractionType.Email,
                    Subject = $"Interaction {index:D2}",
                    OccurredAtUtc = DateTimeOffset.UnixEpoch.AddDays(index),
                    Summary = $"secret: private-{index:D2}@secret.test",
                    CreatedAtUtc = DateTimeOffset.UnixEpoch,
                    UpdatedAtUtc = DateTimeOffset.UnixEpoch
                });
                dbContext.Set<InteractionPartyLink>().Add(new InteractionPartyLink
                {
                    Id = DeterministicGuid(50_000 + index),
                    InteractionId = interactionIds[index],
                    PartyId = partyIds[index],
                    Role = InteractionPartyRole.Account
                });
            }

            await dbContext.SaveChangesAsync();
        }

        var provider = new CrmHrSourceSnapshotProvider(factory);
        var first = await provider.ReadSnapshotAsync(new CrmHrSourceSnapshotRequest(Take: 2));
        interceptor.Clear();

        var second = await provider.ReadSnapshotAsync(new CrmHrSourceSnapshotRequest(
            Cursor: first.Manifest.NextCursor,
            Take: 2));
        var repeatedSecond = await provider.ReadSnapshotAsync(new CrmHrSourceSnapshotRequest(
            Cursor: first.Manifest.NextCursor,
            Take: 2));

        var expectedIds = interactionIds
            .OrderBy(id => id)
            .Skip(2)
            .Take(2)
            .ToArray();
        Assert.Equal(12, second.Manifest.TotalItemCount);
        Assert.Equal(
            expectedIds.Select(id => $"interaction:{id:D}"),
            second.Items.Select(item => item.Provenance.SourceEntityId));
        Assert.Equal(second.Manifest.SnapshotId, repeatedSecond.Manifest.SnapshotId);
        var combinedContent = string.Join(
            Environment.NewLine,
            second.Items.Select(item => item.Content));
        Assert.Contains(MemorySourceSnapshotSecurity.RedactedValue, combinedContent, StringComparison.Ordinal);
        Assert.DoesNotContain("secret.test", combinedContent, StringComparison.Ordinal);

        var pageCommands = interceptor.Commands
            .Where(command =>
                command.CommandText.Contains("CrmHr_Interactions", StringComparison.Ordinal) &&
                command.CommandText.Contains("LIMIT", StringComparison.OrdinalIgnoreCase) &&
                command.CommandText.Contains("OFFSET", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        Assert.NotEmpty(pageCommands);
        var linkCommands = interceptor.Commands
            .Where(command => command.CommandText.Contains(
                "CrmHr_InteractionParties",
                StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(2, linkCommands.Length);
        Assert.All(linkCommands, command =>
        {
            Assert.Equal(
                expectedIds.OrderBy(id => id),
                command.GuidValues.OrderBy(id => id));
            Assert.DoesNotContain(interactionIds[0], command.GuidValues);
            Assert.DoesNotContain(interactionIds[1], command.GuidValues);
            Assert.DoesNotContain(interactionIds[4], command.GuidValues);
            Assert.DoesNotContain(interactionIds[5], command.GuidValues);
        });
    }

    private static Guid DeterministicGuid(int value)
    {
        Span<byte> bytes = stackalloc byte[16];
        BitConverter.TryWriteBytes(bytes, value);
        return new Guid(bytes);
    }

    private sealed record CapturedCommand(
        string CommandText,
        IReadOnlyList<Guid> GuidValues);

    private sealed class QueryCommandInterceptor : DbCommandInterceptor
    {
        private readonly ConcurrentQueue<CapturedCommand> commands = new();

        public IReadOnlyList<CapturedCommand> Commands => commands.ToArray();

        public void Clear()
        {
            while (commands.TryDequeue(out _))
            {
            }
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            var guidValues = command.Parameters
                .Cast<DbParameter>()
                .SelectMany(parameter => parameter.Value switch
                {
                    Guid value => [value],
                    Guid[] values => values,
                    IEnumerable<Guid> values => values,
                    _ => []
                })
                .ToArray();
            commands.Enqueue(new CapturedCommand(command.CommandText, guidValues));
            return ValueTask.FromResult(result);
        }
    }

    private sealed class TestDbContextFactory(
        DbContextOptions<AppDbContext> options) : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext()
        {
            return new AppDbContext(options);
        }
    }
}
