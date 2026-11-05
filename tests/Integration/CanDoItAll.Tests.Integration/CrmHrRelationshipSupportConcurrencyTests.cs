using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class CrmHrRelationshipSupportConcurrencyTests {
    [Fact]
    public async Task Stale_support_assignment_draft_cannot_replace_a_peer_manager_and_buddy() {
        await using var application = await TestApplication.CreateAsync();
        await using var first = application.Services.CreateAsyncScope();
        await using var second = application.Services.CreateAsyncScope();
        var directory = first.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var candidate = await CreatePartyAsync(directory, "Support candidate");
        var firstManager = await CreatePartyAsync(directory, "Original manager");
        var secondManager = await CreatePartyAsync(directory, "Peer manager");
        var buddy = await CreatePartyAsync(directory, "Peer buddy");
        var loser = first.ServiceProvider.GetRequiredService<RecruitingService>();
        var winner = second.ServiceProvider.GetRequiredService<RecruitingService>();
        var initial = new RecruitmentSupportAssignmentsEditorModel { PartyId = candidate, ManagerPartyId = firstManager };
        initial.SetOriginal(new([]));
        Assert.True((await loser.SaveSupportAssignmentsAsync(initial)).IsSuccess);
        var original = await first.ServiceProvider.GetRequiredService<PartyDirectoryManagementService>().ListRelationshipsAsync(candidate);
        var stale = new RecruitmentSupportAssignmentsEditorModel { PartyId = candidate, ManagerPartyId = firstManager };
        stale.SetOriginal(PartyRelationshipObservation.FromRows(original));
        var peer = new RecruitmentSupportAssignmentsEditorModel {
            PartyId = candidate, ManagerPartyId = secondManager, BuddyPartyId = buddy
        };
        peer.SetOriginal(PartyRelationshipObservation.FromRows(original));
        Assert.True((await winner.SaveSupportAssignmentsAsync(peer)).IsSuccess);

        var result = await loser.SaveSupportAssignmentsAsync(stale);

        Assert.True(result.IsFailure);
        Assert.Contains(result.Errors, error => error.Code == CrmHrEditingErrorCodes.RelationshipConflict);
        var stored = await first.ServiceProvider.GetRequiredService<PartyDirectoryManagementService>().ListRelationshipsAsync(candidate);
        Assert.Contains(stored, row => row.RelationshipKind == PartyRelationshipKind.ManagedBy && row.RelatedPartyId == secondManager);
        Assert.Contains(stored, row => row.RelationshipKind == PartyRelationshipKind.Supports && row.RelatedPartyId == buddy);
        Assert.DoesNotContain(stored, row => row.Id == original[0].Id && row.RelatedPartyId == firstManager);
    }

    [Fact]
    public async Task Support_replacement_requires_its_original_subset_observation() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var directory = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var candidate = await CreatePartyAsync(directory, "Unobserved support candidate");
        var recruiting = scope.ServiceProvider.GetRequiredService<RecruitingService>();

        var result = await recruiting.SaveSupportAssignmentsAsync(new() { PartyId = candidate });

        Assert.True(result.IsFailure);
        Assert.Contains(result.Errors, error => error.Code == CrmHrEditingErrorCodes.RelationshipOriginalRequired);
    }

    [Fact]
    public async Task A_support_subset_save_preserves_unrelated_links_added_since_its_observation() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var directory = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var candidate = await CreatePartyAsync(directory, "Subset candidate");
        var originalManager = await CreatePartyAsync(directory, "Subset original manager");
        var newManager = await CreatePartyAsync(directory, "Subset new manager");
        var peer = await CreatePartyAsync(directory, "Unrelated partner");
        var recruiting = scope.ServiceProvider.GetRequiredService<RecruitingService>();
        var management = scope.ServiceProvider.GetRequiredService<PartyDirectoryManagementService>();
        var initial = Model(candidate, new([]), manager: originalManager);
        var created = await recruiting.SaveSupportAssignmentsAsync(initial);
        Assert.True(created.IsSuccess);
        var whole = await management.ListRelationshipsAsync(candidate);
        var withPartner = whole.Select(Editor).Append(new() { RelatedPartyId = peer, RelationshipKind = PartyRelationshipKind.PartnerOf }).ToList();
        Assert.True((await management.SaveRelationshipsAsync(candidate, withPartner, "support-test", original: PartyRelationshipObservation.FromRows(whole))).IsSuccess);

        var result = await recruiting.SaveSupportAssignmentsAsync(Model(candidate, PartyRelationshipObservation.FromRows(created.Value!.Accepted), manager: newManager));

        Assert.True(result.IsSuccess);
        var stored = await management.ListRelationshipsAsync(candidate);
        Assert.Equal(2, stored.Count);
        Assert.Contains(stored, row => row.RelatedPartyId == peer && row.RelationshipKind == PartyRelationshipKind.PartnerOf);
        Assert.Contains(stored, row => row.RelatedPartyId == newManager && row.RelationshipKind == PartyRelationshipKind.ManagedBy);
    }

    [Fact]
    public async Task Buddy_and_mentor_roles_for_the_same_person_survive_generic_replacement() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var directory = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var candidate = await CreatePartyAsync(directory, "Two support roles candidate");
        var support = await CreatePartyAsync(directory, "Two support roles person");
        var recruiting = scope.ServiceProvider.GetRequiredService<RecruitingService>();
        var created = await recruiting.SaveSupportAssignmentsAsync(Model(candidate, new([]), buddy: support, mentor: support));
        Assert.True(created.IsSuccess);
        var original = created.Value!.Accepted;
        Assert.Equal(2, original.Count);
        var management = scope.ServiceProvider.GetRequiredService<PartyDirectoryManagementService>();

        var result = await management.SaveRelationshipsAsync(candidate, original.Select(Editor).ToList(), "support-test", original: PartyRelationshipObservation.FromRows(original));

        Assert.True(result.IsSuccess);
        Assert.Equal(original.Select(row => row.Id).Order(), result.Value!.Accepted.Select(row => row.Id).Order());
        Assert.Equal(new[] { "Buddy", "Mentor" }, result.Value.Accepted.Select(row => row.Notes).Order());
    }

    [Fact]
    public async Task An_opposite_endpoint_directory_edit_invalidates_an_older_support_submission() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var directory = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var candidate = await CreatePartyAsync(directory, "Opposite support candidate");
        var manager = await CreatePartyAsync(directory, "Opposite support manager");
        var recruiting = scope.ServiceProvider.GetRequiredService<RecruitingService>();
        var management = scope.ServiceProvider.GetRequiredService<PartyDirectoryManagementService>();
        var created = await recruiting.SaveSupportAssignmentsAsync(Model(candidate, new([]), manager: manager));
        Assert.True(created.IsSuccess);
        var original = created.Value!.Accepted;
        var peer = await management.ListRelationshipsAsync(manager);
        var edited = peer.Select(Editor).ToList();
        edited[0].Notes = "Directory manager note retained";
        Assert.True((await management.SaveRelationshipsAsync(manager, edited, "support-test", original: PartyRelationshipObservation.FromRows(peer))).IsSuccess);

        var result = await recruiting.SaveSupportAssignmentsAsync(Model(candidate, PartyRelationshipObservation.FromRows(original)));

        Assert.True(result.IsFailure);
        Assert.Contains(result.Errors, error => error.Code == CrmHrEditingErrorCodes.RelationshipConflict);
        Assert.Equal("Directory manager note retained", Assert.Single(await management.ListRelationshipsAsync(candidate)).Notes);
    }

    [Theory]
    [InlineData(SupportRole.Manager, false)]
    [InlineData(SupportRole.Buddy, false)]
    [InlineData(SupportRole.Mentor, false)]
    [InlineData(SupportRole.Manager, true)]
    [InlineData(SupportRole.Buddy, true)]
    [InlineData(SupportRole.Mentor, true)]
    public async Task Canonical_support_people_cannot_change_to_an_incompatible_type(SupportRole role, bool import) {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var directory = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var candidate = await CreatePartyAsync(directory, "Support type candidate");
        var person = await CreatePartyAsync(directory, "Support type person");
        var original = (await directory.GetPartyAsync(person))!;
        var model = Model(candidate, new([]),
            manager: role == SupportRole.Manager ? person : null,
            buddy: role == SupportRole.Buddy ? person : null,
            mentor: role == SupportRole.Mentor ? person : null);
        Assert.True((await scope.ServiceProvider.GetRequiredService<RecruitingService>().SaveSupportAssignmentsAsync(model)).IsSuccess);
        var edited = original.Snapshot();
        edited.PartyType = PartyType.Organization;

        var errors = import ? (await directory.ImportPartiesAtomicallyAsync([edited], "support-test")).Errors : (await directory.SavePartyAsync(edited)).Errors;

        Assert.Contains(errors, error => error.Code == CrmHrEditingErrorCodes.RelationshipSupportTypeConflict);
        Assert.Equal(PartyType.Person, (await directory.GetPartyAsync(person))!.PartyType);
        Assert.Single(await scope.ServiceProvider.GetRequiredService<PartyDirectoryManagementService>().ListRelationshipsAsync(candidate));
    }

    [Theory]
    [InlineData(SupportRole.Manager, false)]
    [InlineData(SupportRole.Manager, true)]
    [InlineData(SupportRole.Buddy, false)]
    [InlineData(SupportRole.Buddy, true)]
    [InlineData(SupportRole.Mentor, false)]
    [InlineData(SupportRole.Mentor, true)]
    public async Task Canonical_support_markers_cannot_bypass_person_validation_with_padding(SupportRole role, bool invalidRoot) {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var directory = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var root = await directory.SavePartyAsync(new() { DisplayName = "Padded support root", PartyType = invalidRoot ? PartyType.Organization : PartyType.Person });
        var related = await directory.SavePartyAsync(new() { DisplayName = "Padded support peer", PartyType = invalidRoot ? PartyType.Person : PartyType.Organization });
        Assert.True(root.IsSuccess);
        Assert.True(related.IsSuccess);
        var marker = role switch {
            SupportRole.Manager => RecruitmentSupportRelationshipSemantics.Manager,
            SupportRole.Buddy => RecruitmentSupportRelationshipSemantics.Buddy,
            SupportRole.Mentor => RecruitmentSupportRelationshipSemantics.Mentor,
            _ => throw new ArgumentOutOfRangeException(nameof(role))
        };
        var owner = scope.ServiceProvider.GetRequiredService<IPartyRelationshipService>();

        var result = await owner.ReplaceAsync(root.Value, [new() {
            RelatedPartyId = related.Value,
            IsOutgoing = role == SupportRole.Manager ? !invalidRoot : invalidRoot,
            RelationshipKind = role == SupportRole.Manager ? PartyRelationshipKind.ManagedBy : PartyRelationshipKind.Supports,
            Notes = $"  {marker}  "
        }], new([]), "support-test");

        Assert.True(result.IsFailure);
        Assert.Equal($"{role} must reference an existing person.", Assert.Single(result.Errors).Message);
        Assert.Empty(await owner.ListAsync(root.Value));
        Assert.Empty(await owner.ListAsync(related.Value));
    }

    public enum SupportRole { Manager, Buddy, Mentor }

    private static RecruitmentSupportAssignmentsEditorModel Model(Guid partyId, PartyRelationshipObservation original,
        Guid? manager = null, Guid? buddy = null, Guid? mentor = null) {
        var model = new RecruitmentSupportAssignmentsEditorModel { PartyId = partyId, ManagerPartyId = manager, BuddyPartyId = buddy, MentorPartyId = mentor };
        model.SetOriginal(original);
        return model;
    }

    private static PartyRelationshipEditorModel Editor(PartyRelationshipListItemModel row) => new() {
        Id = row.Id, RelatedPartyId = row.RelatedPartyId, RelationshipKind = row.RelationshipKind,
        IsOutgoing = row.IsOutgoing, IsPrimary = row.IsPrimary, StartDateUtc = row.StartDateUtc, EndDateUtc = row.EndDateUtc, Notes = row.Notes
    };

    private static async Task<Guid> CreatePartyAsync(PartyDirectoryService directory, string name) {
        var result = await directory.SavePartyAsync(new() { DisplayName = name });
        Assert.True(result.IsSuccess);
        return result.Value;
    }
}
