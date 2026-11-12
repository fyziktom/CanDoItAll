using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Editing;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.CrmHr;

public sealed class PartyMergePreflightIntegrationTests {
    [Fact]
    public async Task Merge_requires_the_original_preflight_before_mutating_either_party() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var parties = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var manager = scope.ServiceProvider.GetRequiredService<PartyDirectoryManagementService>();
        var retainedId = await CreatePartyAsync(parties, "Retained preflight party");
        var mergedId = await CreatePartyAsync(parties, "Merged preflight party");

        var result = await manager.MergePartyAsync(retainedId, mergedId, "preflight-tests", "Missing original");

        Assert.True(result.IsFailure);
        Assert.Contains(result.Errors, error => error.Code == CrmHrEditingErrorCodes.PartyMergeOriginalRequired);
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var context = await factory.CreateDbContextAsync();
        Assert.Equal(2, await context.Set<Party>().CountAsync(item => item.Id == retainedId || item.Id == mergedId));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_peer_change_to_either_party_rejects_the_entire_observed_merge(bool changeRetained) {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var parties = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var manager = scope.ServiceProvider.GetRequiredService<PartyDirectoryManagementService>();
        var retained = await CreatePartyAsync(parties, "Retained observed party");
        var merged = await CreatePartyAsync(parties, "Merged observed party");
        var preview = await manager.PreviewMergeAsync(retained, merged);
        Assert.True(preview.IsSuccess);
        await using var peer = application.Services.CreateAsyncScope();
        var peerParties = peer.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var changedId = changeRetained ? retained : merged;
        var changed = (await peerParties.GetPartyAsync(changedId))!;
        changed.Summary = "Independent peer content after the merge preview";
        Assert.True((await peerParties.SavePartyAsync(changed)).IsSuccess);
        var accepted = false;

        var result = await manager.MergePartyAsync(retained, merged, "preflight-tests", "Stale preview",
            original: preview.Value!.Original, onCommitted: _ => accepted = true);

        Assert.Contains(result.Errors, error => error.Code == CrmHrEditingErrorCodes.PartyMergeConflict);
        Assert.False(accepted);
        Assert.NotNull(await parties.GetPartyAsync(retained));
        Assert.NotNull(await parties.GetPartyAsync(merged));
        Assert.Equal(changed.Summary, (await parties.GetPartyAsync(changedId))!.Summary);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Native_relationship_addition_or_removal_invalidates_the_observed_affected_set(bool remove) {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var parties = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var manager = scope.ServiceProvider.GetRequiredService<PartyDirectoryManagementService>();
        var retained = await CreatePartyAsync(parties, "Retained membership party");
        var merged = await CreatePartyAsync(parties, "Merged membership party");
        var related = await CreatePartyAsync(parties, "Related membership party");
        PartyRelationshipEditorModel relationship = new() { RelatedPartyId = related, RelationshipKind = PartyRelationshipKind.Supports };
        if (remove) {
            Assert.True((await manager.SaveRelationshipsAsync(merged, [relationship], "preflight-tests", original: new([]))).IsSuccess);
        }
        var shownParty = (await parties.GetPartyAsync(merged))!;
        var preview = await manager.PreviewMergeAsync(retained, merged);
        Assert.True(preview.IsSuccess);
        await using var peer = application.Services.CreateAsyncScope();
        var peerRelations = peer.ServiceProvider.GetRequiredService<IPartyRelationshipService>();
        var before = await peerRelations.ListAsync(merged);
        Assert.True((await peerRelations.ReplaceAsync(merged, remove ? [] : [relationship],
            PartyRelationshipObservation.FromRows(before), "peer-membership")).IsSuccess);
        Assert.Equal(shownParty.UpdatedAtUtc, (await parties.GetPartyAsync(merged))!.UpdatedAtUtc);

        var result = await manager.MergePartyAsync(retained, merged, "preflight-tests", "Changed child membership",
            original: preview.Value!.Original);

        Assert.Contains(result.Errors, error => error.Code == CrmHrEditingErrorCodes.PartyMergeConflict);
        Assert.NotNull(await parties.GetPartyAsync(merged));
        Assert.Equal(remove ? 0 : 1, (await peerRelations.ListAsync(merged)).Count);
    }

    [Fact]
    public async Task An_unchanged_round_tripped_preflight_accepts_one_merge_and_reports_its_receipt() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var parties = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var manager = scope.ServiceProvider.GetRequiredService<PartyDirectoryManagementService>();
        var retained = await CreatePartyAsync(parties, "Retained accepted party");
        var merged = await CreatePartyAsync(parties, "Merged accepted party");
        var preview = await manager.PreviewMergeAsync(retained, merged);
        Assert.True(preview.IsSuccess);
        var original = JsonSerializer.Deserialize<PartyMergeObservation>(JsonSerializer.Serialize(preview.Value!.Original))!;
        Assert.True(original.Matches(preview.Value.Original));
        using var subscription = scope.ServiceProvider.GetRequiredService<CrmHrChanges>().Subscribe(EditingOwner.CrmHrParty);
        PartyMergeSummaryModel? receipt = null;

        var result = await manager.MergePartyAsync(retained, merged, "preflight-tests", "Accepted observed merge",
            original: original, onCommitted: accepted => receipt = accepted);

        Assert.True(result.IsSuccess);
        Assert.Same(result.Value, receipt);
        Assert.NotNull(await parties.GetPartyAsync(retained));
        Assert.Null(await parties.GetPartyAsync(merged));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var batch = await subscription.ReadAsync(timeout.Token);
        Assert.Contains(batch.Changes, change => change.Resource.ResourceId == merged && change.Kind == EditingChangeKind.Deleted);
    }

    [Fact]
    public async Task Apply_import_rechecks_duplicates_created_after_its_dry_run() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<PartyDirectoryManagementService>();
        var name = $"Import dry run {Guid.NewGuid():N}";
        var preview = await manager.PreviewImportAsync($"DisplayName,PartyType,LifecycleStatus\n{name},Person,Active");
        Assert.True(preview.IsSuccess);
        Assert.True(Assert.Single(preview.Value!.Rows).CanImport);
        await using var peer = application.Services.CreateAsyncScope();
        await CreatePartyAsync(peer.ServiceProvider.GetRequiredService<PartyDirectoryService>(), name);

        var result = await manager.ApplyImportAsync(preview.Value.Rows, "preflight-tests");

        Assert.True(result.IsFailure);
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var context = await factory.CreateDbContextAsync();
        Assert.Single(await context.Set<Party>().Where(item => item.DisplayName == name).ToArrayAsync());
    }

    [Fact]
    public async Task Merge_preserves_account_notes_and_consolidates_connected_records() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var parties = services.GetRequiredService<PartyDirectoryService>();
        var manager = services.GetRequiredService<PartyDirectoryManagementService>();
        var crm = services.GetRequiredService<CrmService>();
        var retained = await CreatePartyAsync(parties, "Retained account", PartyType.Organization);
        var source = await CreatePartyAsync(parties, "Duplicate account", PartyType.Organization);
        var contact = await CreatePartyAsync(parties, "Account contact");
        var sharedProject = new Project { Name = "Shared account link", Slug = Guid.NewGuid().ToString("N") };
        var sourceProject = new Project { Name = "Source account link", Slug = Guid.NewGuid().ToString("N") };
        await using (var setup = await services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync()) {
            setup.AddRange(sharedProject, sourceProject);
            await setup.SaveChangesAsync();
        }
        var dataset = services.GetRequiredService<CrmHrChanges>().Scope.Id;
        foreach (var id in new[] { retained, source }) {
            var profile = (await crm.GetAccountProfileAsync(id))!;
            profile.CommercialNotes = id == retained ? "Retained commercial note" : "Source commercial note";
            profile.ConstraintNotes = id == source ? "Source constraint" : string.Empty;
            profile.TimingRiskNotes = id == source ? "Source timing risk" : string.Empty;
            Assert.True((await crm.SaveAccountProfileAsync(profile)).IsSuccess);
            var workspace = (await crm.GetAccountWorkspaceAsync(id))!;
            Assert.True((await crm.SaveConnectedRecordsAsync(id, [new() {
                Id = Guid.NewGuid(), RelatedPartyId = contact,
                Role = CrmAccountConnectionRole.PrimaryContact, IsPrimary = true,
                Notes = id == retained ? "Retained connection note" : "Source connection note",
                ProjectIds = id == retained ? [sharedProject.Id] : [sharedProject.Id, sourceProject.Id],
                ProjectAdmissions = new() {
                    [sharedProject.Id] = new(dataset, sharedProject.Id, sharedProject.LifetimeId),
                    [sourceProject.Id] = new(dataset, sourceProject.Id, sourceProject.LifetimeId)
                }
            }], "preflight-tests", original: workspace.ConnectionsOriginal)).IsSuccess);
        }
        var preview = (await manager.PreviewMergeAsync(retained, source)).Value!;
        using var accountHints = services.GetRequiredService<CrmHrChanges>().Subscribe(EditingOwner.CrmAccountRecords);
        var receipts = 0;

        var result = await manager.MergePartyAsync(retained, source, "preflight-tests", "Consolidate account", original: preview.Original,
            onCommitted: _ => {
                using var visible = services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext();
                Assert.False(visible.Set<Party>().Any(row => row.Id == source));
                receipts++;
            });

        Assert.True(result.IsSuccess);
        Assert.Equal(1, receipts);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var hints = await accountHints.ReadAsync(timeout.Token);
        Assert.Equal(2, hints.Changes.Count);
        Assert.Contains(hints.Changes, hint => hint.Resource.ResourceId == retained && hint.Kind == EditingChangeKind.EditableContentChanged);
        Assert.Contains(hints.Changes, hint => hint.Resource.ResourceId == source && hint.Kind == EditingChangeKind.Deleted);
        var stored = (await crm.GetAccountWorkspaceAsync(retained))!;
        Assert.Contains("Source commercial note", stored.Profile.CommercialNotes, StringComparison.Ordinal);
        Assert.Contains("Retained commercial note", stored.Profile.CommercialNotes, StringComparison.Ordinal);
        Assert.Equal("Source constraint", stored.Profile.ConstraintNotes);
        Assert.Equal("Source timing risk", stored.Profile.TimingRiskNotes);
        var factory = services.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var context = await factory.CreateDbContextAsync();
        Assert.False(await context.Set<CrmAccountProfile>().AnyAsync(row => row.AccountPartyId == source));
        Assert.False(await context.Set<CrmAccountConnection>().AnyAsync(row => row.AccountPartyId == source || row.RelatedPartyId == source));
        var connection = Assert.Single(await context.Set<CrmAccountConnection>().Where(row => row.AccountPartyId == retained).ToArrayAsync());
        Assert.Contains("Source connection note", connection.Notes, StringComparison.Ordinal);
        Assert.Contains("Retained connection note", connection.Notes, StringComparison.Ordinal);
        var links = await context.Set<CrmAccountConnectionProjectLink>().ToArrayAsync();
        Assert.Equal(2, links.Length);
        Assert.All(links, link => Assert.Equal(connection.Id, link.AccountConnectionId));
        Assert.Equal(new[] { sharedProject.Id, sourceProject.Id }.Order(), links.Select(link => link.ProjectId).Order());
    }

    [Fact]
    public async Task Merge_cannot_remove_the_party_that_owns_a_bound_technical_agent() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var parties = services.GetRequiredService<PartyDirectoryService>();
        var manager = services.GetRequiredService<PartyDirectoryManagementService>();
        var retained = await CreatePartyAsync(parties, "Unbound survivor", PartyType.AiAgent);
        var source = await CreatePartyAsync(parties, "Bound source", PartyType.AiAgent);
        var factory = services.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var technicalId = Guid.NewGuid();
        await using (var context = await factory.CreateDbContextAsync()) {
            context.Set<AiResourceBinding>().Add(new() { PartyId = source, TechnicalAgentId = technicalId });
            await context.SaveChangesAsync();
        }
        var preview = (await manager.PreviewMergeAsync(retained, source)).Value!;
        var accepted = false;

        var result = await manager.MergePartyAsync(retained, source, "preflight-tests", "Must preserve technical ownership",
            original: preview.Original, onCommitted: _ => accepted = true);

        Assert.True(result.IsFailure);
        Assert.Contains(result.Errors, error => error.Code == CrmHrEditingErrorCodes.PartyMergeTechnicalOwner);
        Assert.False(accepted);
        Assert.NotNull(await parties.GetPartyAsync(source));
        await using var verification = await factory.CreateDbContextAsync();
        Assert.Equal(technicalId, (await verification.Set<AiResourceBinding>().SingleAsync(row => row.PartyId == source)).TechnicalAgentId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_throwing_receipt_receiver_cannot_suppress_the_committed_stewardship_hints(bool merge) {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var parties = services.GetRequiredService<PartyDirectoryService>();
        var manager = services.GetRequiredService<PartyDirectoryManagementService>();
        var retained = await CreatePartyAsync(parties, "Receipt survivor");
        var source = await CreatePartyAsync(parties, "Receipt duplicate");
        var original = (await manager.PreviewMergeAsync(retained, source)).Value!.Original;
        var csv = (await manager.PreviewImportAsync("DisplayName,PartyType,LifecycleStatus\nReceipt import one,Person,Active\nReceipt import two,Person,Active")).Value!;
        using var hints = services.GetRequiredService<CrmHrChanges>().Subscribe(EditingOwner.CrmHrParty);
        var receipts = 0;

        await Assert.ThrowsAsync<ReceiptReceiverFailure>(async () => {
            if (merge) {
                await manager.MergePartyAsync(retained, source, "receipt-test", "Committed merge", original: original,
                    onCommitted: _ => {
                        receipts++;
                        throw new ReceiptReceiverFailure();
                    });
            } else {
                await manager.ApplyImportAsync(csv.Rows, "receipt-test",
                    onCommitted: count => {
                        receipts += count;
                        throw new ReceiptReceiverFailure();
                    });
            }
        });

        Assert.Equal(merge ? 1 : 2, receipts);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var batch = await hints.ReadAsync(timeout.Token);
        Assert.Equal(merge ? 6 : 2, batch.Changes.Count);
        if (merge) {
            Assert.Null(await parties.GetPartyAsync(source));
            Assert.Equal(4, batch.Changes.Count(hint => hint.Kind == EditingChangeKind.MetadataChanged));
            Assert.Equal(EditingChangeKind.Deleted, batch.Changes.Last(hint => hint.Resource.ResourceId == source).Kind);
            Assert.Equal(EditingChangeKind.EditableContentChanged, batch.Changes.Last(hint => hint.Resource.ResourceId == retained).Kind);
        } else {
            Assert.All(batch.Changes, hint => Assert.Equal(EditingChangeKind.Created, hint.Kind));
            await using var context = await services.GetRequiredService<IDbContextFactory<CrmHrDbContext>>().CreateDbContextAsync();
            Assert.Equal(2, await context.Set<Party>().CountAsync(row => row.DisplayName == "Receipt import one" || row.DisplayName == "Receipt import two"));
        }
    }

    private sealed class ReceiptReceiverFailure : Exception;

    private static async Task<Guid> CreatePartyAsync(PartyDirectoryService parties, string name, PartyType type = PartyType.Person) {
        var result = await parties.SavePartyAsync(new PartyEditorModel {
            PartyType = type,
            LifecycleStatus = PartyLifecycleStatus.Active,
            DisplayName = name,
            LastChangedBy = "preflight-tests"
        });
        Assert.True(result.IsSuccess);
        return result.Value;
    }
}
