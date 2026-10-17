using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Search;
using CanDoItAll.Modules.Resources;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed partial class ResourceMetadataOwnerTests {
    [Fact]
    public async Task Oversized_unbound_metadata_and_Search_body_can_be_deleted_without_payload_projection() {
        var probe = new Probe();
        await using var fixture = await Fixture.CreateAsync(probe);
        var original = await fixture.CreateAsync();
        var payload = new string('x', ResourceMetadataLimits.ConfigJsonBytes + 1);
        await using (var owner = await fixture.OwnerAsync()) {
            var row = await owner.Set<ProjectResource>().SingleAsync();
            row.ConfigJson = payload;
            row.ProjectId = Guid.Empty;
            row.ProjectLifetimeId = null;
            row.Revision = Guid.NewGuid();
            await owner.SaveChangesAsync();
        }
        await using (var search = await fixture.SearchAsync()) {
            var row = await search.Set<SearchDocument>().SingleAsync(document => document.SourceType == SearchDocument.ResourceSourceType);
            row.Body = new string('s', 2 * 1024 * 1024);
            await search.SaveChangesAsync();
        }
        probe.Armed = true;
        var read = Assert.IsType<ResourceMetadataReadResult.MetadataOnly>(await fixture.Service.ReadAsync(original.Expected.ResourceId));
        Assert.IsType<ResourceProjectBinding.RetainedUnbound>(read.Expected.Project);
        Assert.Equal(ResourceMetadataReadOnlyReason.TooLarge, read.Reason);
        Assert.Equal(payload.Length, read.Lengths.ConfigJson);
        var boundedRead = Assert.Single(probe.Reads.Where(sql => sql.Contains("WITH candidate AS", StringComparison.Ordinal)));
        Assert.Contains("CASE WHEN \"FitsRaw\" THEN \"ConfigJson\" END", boundedRead, StringComparison.Ordinal);
        probe.Reads.Clear();
        var deleted = Assert.IsType<ResourceMetadataDeleteOutcome.Accepted>(await fixture.Service.DeleteAsync(new(read.Expected)));
        Assert.Equal(read.Expected, deleted.Deleted);
        Assert.DoesNotContain(probe.Reads, sql => sql.Contains("\"ConfigJson\"", StringComparison.Ordinal));
        await using var verification = await fixture.OwnerAsync();
        Assert.Empty(await verification.Set<ProjectResource>().ToArrayAsync());
        await using var searchVerification = await fixture.SearchAsync();
        Assert.False(await searchVerification.Set<SearchDocument>().AnyAsync(document => document.SourceType == SearchDocument.ResourceSourceType));
    }

    [Fact]
    public async Task Stored_unsupported_payloads_refuse_replacement_even_with_forged_valid_header_but_allow_metadata_deletion() {
        await using var fixture = await Fixture.CreateAsync();
        Action<ProjectResource>[] corruptions = [
            row => row.LinkedSecretIdsJson = $"[\"{Guid.NewGuid():D}\",\"{Guid.NewGuid():D}\"]",
            row => row.LinkedSecretIdsJson = "null",
            row => row.ConfigJson = "{\"endpointUrl\":\"first\",\"ENDPOINTURL\":\"second\"}",
            row => row.ConnectorPluginKey = "retained-unavailable-connector",
            row => row.Sensitivity = (ResourceSensitivity)int.MaxValue
        ];
        foreach (var corrupt in corruptions) {
            var original = await fixture.CreateAsync();
            await using (var owner = await fixture.OwnerAsync()) {
                var row = await owner.Set<ProjectResource>().SingleAsync(resource => resource.Id == original.Expected.ResourceId);
                corrupt(row);
                row.Revision = Guid.NewGuid();
                await owner.SaveChangesAsync();
            }
            var read = await fixture.Service.ReadAsync(original.Expected.ResourceId);
            var header = read switch {
                ResourceMetadataReadResult.MetadataOnly metadata => metadata.Expected,
                ResourceMetadataReadResult.Found { ReadOnlyReason: ResourceMetadataReadOnlyReason.UnavailableConnector } found => found.Snapshot.Expected,
                _ => throw new InvalidOperationException("The retained payload must be explicitly read-only.")
            };
            var replace = new ReplaceResourceMetadataCommand(header, fixture.Admission, ResourceMetadataValues.Capture(fixture.Editor("Must not overwrite")));
            Assert.Equal(ResourceMetadataRejectionReason.UnsupportedMetadata,
                Assert.IsType<ResourceMetadataSaveOutcome.Rejected>(await fixture.Service.SaveAsync(replace)).Reason);
            Assert.IsType<ResourceMetadataDeleteOutcome.Accepted>(await fixture.Service.DeleteAsync(new(header)));
        }
    }

    [Fact]
    public async Task Encoded_snapshot_limit_refuses_escape_expansion_without_changing_retained_row_or_Search() {
        await using var fixture = await Fixture.CreateAsync();
        var original = await fixture.CreateAsync();
        var escaped = new string('<', 60 * 1024);
        var config = "{" + string.Join(',', Enumerable.Range(0, 4).Select(index => $"\"field{index}\":\"{escaped}\"")) + "}";
        Assert.True(System.Text.Encoding.UTF8.GetByteCount(config) < ResourceMetadataLimits.ConfigJsonBytes);
        await using (var owner = await fixture.OwnerAsync()) {
            var row = await owner.Set<ProjectResource>().SingleAsync();
            row.ConfigJson = config;
            row.Revision = Guid.NewGuid();
            await owner.SaveChangesAsync();
        }
        var read = Assert.IsType<ResourceMetadataReadResult.MetadataOnly>(await fixture.Service.ReadAsync(original.Expected.ResourceId));
        Assert.Equal(ResourceMetadataReadOnlyReason.TooLarge, read.Reason);
        var command = new ReplaceResourceMetadataCommand(read.Expected, fixture.Admission, ResourceMetadataValues.Capture(fixture.Editor()));
        Assert.Equal(ResourceMetadataRejectionReason.UnsupportedMetadata,
            Assert.IsType<ResourceMetadataSaveOutcome.Rejected>(await fixture.Service.SaveAsync(command)).Reason);
        await using var verify = await fixture.OwnerAsync();
        Assert.Equal(config, await verify.Set<ProjectResource>().Select(row => row.ConfigJson).SingleAsync());
        await fixture.AssertSearchAsync(original);
    }

    [Fact]
    public async Task Profile_generation_changed_after_enlisted_Search_rolls_back_before_commit() {
        var probe = new OwnerPostcommitTestProbe();
        await using var fixture = await Fixture.CreateAsync(configure: probe.ConfigureServices);
        var original = await fixture.CreateAsync();
        var canonical = new MutableCanonical(fixture.Services.GetRequiredService<ICanonicalRuntimeDatabase>().Profile);
        var writer = ActivatorUtilities.CreateInstance<ResourceMetadataWriter>(fixture.Services, canonical);
        probe.AfterSearch = (_, _, _, _) => {
            canonical.Generation++;
            return Task.CompletedTask;
        };
        var outcome = await writer.ReplaceAsync(fixture.Replace(original, "Retired owner"));
        Assert.Equal(ResourceMetadataRejectionReason.OwnerRetired, Assert.IsType<ResourceMetadataSaveOutcome.Rejected>(outcome).Reason);
        Assert.Equal(original, await ResourceMetadataTestCommands.FoundAsync(fixture.Service, original.Expected.ResourceId));
        await fixture.AssertSearchAsync(original);
    }

    [Fact]
    public async Task Foreign_original_profile_and_nonrelational_factory_cannot_publish_any_Resource_effect() {
        await using var fixture = await Fixture.CreateAsync();
        var command = await fixture.NewAsync();
        var profileId = Guid.NewGuid();
        var foreign = new ReplaceResourceMetadataCommand(new ResourceMetadataExpectation.Absent(
            new(profileId, command.Expected.Context.Namespace), command.Expected.ResourceId),
            new(profileId, fixture.Admission.ProjectId, fixture.Admission.LifetimeId), command.Values);
        Assert.Equal(ResourceMetadataRejectionReason.OwnerRetired,
            Assert.IsType<ResourceMetadataSaveOutcome.Rejected>(await fixture.Service.SaveAsync(foreign)).Reason);
        var options = new DbContextOptionsBuilder<ResourcesDbContext>().UseInMemoryDatabase($"explicit-resource-refusal-{Guid.NewGuid():N}").Options;
        var writer = ActivatorUtilities.CreateInstance<ResourceMetadataWriter>(fixture.Services, new NonrelationalFactory(options));
        Assert.Equal(ResourceMetadataRejectionReason.PersistenceUnavailable,
            Assert.IsType<ResourceMetadataSaveOutcome.Rejected>(await writer.ReplaceAsync(command)).Reason);
        await using var inMemory = new ResourcesDbContext(options);
        Assert.Empty(await inMemory.Set<ProjectResource>().ToArrayAsync());
        Assert.IsType<ResourceMetadataReadResult.Missing>(await fixture.Service.ReadAsync(command.Expected.ResourceId));
        await using var search = await fixture.SearchAsync();
        Assert.False(await search.Set<SearchDocument>().AnyAsync(document => document.SourceType == SearchDocument.ResourceSourceType));
    }

    [Fact]
    public async Task Early_cancellation_and_prepared_activation_cannot_open_or_mutate_the_owner() {
        var probe = new Probe();
        await using var fixture = await Fixture.CreateAsync(probe);
        var original = await fixture.CreateAsync();
        var command = await fixture.NewAsync();
        probe.PauseCreation = true;
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var failure = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Service.SaveAsync(command, cancellation.Token));
        Assert.Equal(cancellation.Token, failure.CancellationToken);
        Assert.False(probe.CreationPaused.Task.IsCompleted);
        var activation = fixture.Services.GetRequiredService<DatasetActivationService>();
        var binding = await activation.ReadActiveBindingAsync(command.Expected.Context.DatabaseProfileId);
        var prepared = new DatasetActivationPlan(Guid.NewGuid(), DatasetActivationKind.Attach, binding, binding);
        using (var lease = activation.Acquire(command.Expected.Context.DatabaseProfileId)) {
            await lease.WritePreparedAsync(prepared);
        }
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try {
            var save = Assert.IsType<ResourceMetadataSaveOutcome.Rejected>(await fixture.Service.SaveAsync(command, deadline.Token));
            Assert.Equal(ResourceMetadataRejectionReason.PersistenceUnavailable, save.Reason);
            Assert.Null(save.Message);
            var delete = Assert.IsType<ResourceMetadataDeleteOutcome.Rejected>(await fixture.Service.DeleteAsync(new(original.Expected), deadline.Token));
            Assert.Equal(ResourceMetadataRejectionReason.PersistenceUnavailable, delete.Reason);
            Assert.False(probe.CreationPaused.Task.IsCompleted);
            Assert.Equal(0, probe.Commits);
            using var lease = activation.Acquire(command.Expected.Context.DatabaseProfileId);
            Assert.Equal(DatasetActivationFileState.Prepared, (await lease.ReadAsync())!.State);
        } finally {
            probe.PauseCreation = false;
            probe.ResumeCreation.TrySetResult();
            using var lease = activation.Acquire(command.Expected.Context.DatabaseProfileId);
            await lease.MarkActiveAsync(prepared);
        }
        Assert.IsType<ResourceMetadataReadResult.Missing>(await fixture.Service.ReadAsync(command.Expected.ResourceId));
        Assert.Equal(original, await ResourceMetadataTestCommands.FoundAsync(fixture.Service, original.Expected.ResourceId));
        await fixture.AssertSearchAsync(original);
    }

    private sealed class MutableCanonical(ResolvedDatabaseProfile profile) : ICanonicalRuntimeDatabase {
        public ResolvedDatabaseProfile Profile { get; } = profile;
        public long Generation { get; set; }
    }

    private sealed class NonrelationalFactory(DbContextOptions<ResourcesDbContext> options) : IDbContextFactory<ResourcesDbContext> {
        public ResourcesDbContext CreateDbContext() => new(options);
        public Task<ResourcesDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) => Task.FromResult(new ResourcesDbContext(options));
    }
}
