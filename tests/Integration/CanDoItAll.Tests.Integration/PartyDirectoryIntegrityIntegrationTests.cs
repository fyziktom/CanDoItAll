using System.Text.Json;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.CrmHr;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.CrmHr;

public sealed class PartyDirectoryIntegrityIntegrationTests
{
    private const string PartyNotFoundErrorCode = "crmhr.party.not-found";
    private const string RelationshipDuplicateErrorCode = "crmhr.party.relationship-duplicate";
    private const string RelationshipPartyNotFoundErrorCode = "crmhr.party.relationship-party-not-found";
    private const string MultiplePrimaryContactsErrorCode = "crmhr.party.multiple-primary-contacts";

    [Fact]
    public async Task Save_relationships_rejects_missing_related_party_before_replacing_existing_rows()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var partyDirectoryService = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var managementService = scope.ServiceProvider.GetRequiredService<PartyDirectoryManagementService>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var sourcePartyId = await CreatePartyAsync(partyDirectoryService, "Relationship source");
        var targetPartyId = await CreatePartyAsync(partyDirectoryService, "Relationship target");
        var initialResult = await managementService.SaveRelationshipsAsync(
            sourcePartyId,
            [
                new PartyRelationshipEditorModel
                {
                    RelatedPartyId = targetPartyId,
                    RelationshipKind = PartyRelationshipKind.ReportsTo,
                    IsOutgoing = true,
                    Notes = "Preserve this relationship"
                }
            ],
            "integration-tests");
        Assert.True(initialResult.IsSuccess);

        var result = await managementService.SaveRelationshipsAsync(
            sourcePartyId,
            [
                new PartyRelationshipEditorModel
                {
                    RelatedPartyId = Guid.NewGuid(),
                    RelationshipKind = PartyRelationshipKind.ManagedBy,
                    IsOutgoing = true
                }
            ],
            "integration-tests");

        Assert.True(result.IsFailure);
        Assert.Contains(result.Errors, error => error.Code == RelationshipPartyNotFoundErrorCode);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var persistedRelationship = await dbContext.Set<PartyRelationship>()
            .SingleAsync(item => item.SourcePartyId == sourcePartyId || item.TargetPartyId == sourcePartyId);
        Assert.Equal(targetPartyId, persistedRelationship.TargetPartyId);
        Assert.Equal(PartyRelationshipKind.ReportsTo, persistedRelationship.RelationshipKind);
        Assert.Equal("Preserve this relationship", persistedRelationship.Notes);
    }

    [Fact]
    public async Task Save_relationships_rejects_duplicate_logical_identity_before_replacing_existing_rows()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var partyDirectoryService = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var managementService = scope.ServiceProvider.GetRequiredService<PartyDirectoryManagementService>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var sourcePartyId = await CreatePartyAsync(partyDirectoryService, "Duplicate relationship source");
        var targetPartyId = await CreatePartyAsync(partyDirectoryService, "Duplicate relationship target");
        var initialResult = await managementService.SaveRelationshipsAsync(
            sourcePartyId,
            [
                new PartyRelationshipEditorModel
                {
                    RelatedPartyId = targetPartyId,
                    RelationshipKind = PartyRelationshipKind.PartnerOf,
                    IsOutgoing = true,
                    Notes = "Existing identity"
                }
            ],
            "integration-tests");
        Assert.True(initialResult.IsSuccess);

        var result = await managementService.SaveRelationshipsAsync(
            sourcePartyId,
            [
                new PartyRelationshipEditorModel
                {
                    RelatedPartyId = targetPartyId,
                    RelationshipKind = PartyRelationshipKind.ManagedBy,
                    IsOutgoing = true,
                    Notes = "First duplicate"
                },
                new PartyRelationshipEditorModel
                {
                    RelatedPartyId = targetPartyId,
                    RelationshipKind = PartyRelationshipKind.ManagedBy,
                    IsOutgoing = true,
                    Notes = "Second duplicate"
                }
            ],
            "integration-tests");

        Assert.True(result.IsFailure);
        Assert.Contains(result.Errors, error => error.Code == RelationshipDuplicateErrorCode);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var persistedRelationship = await dbContext.Set<PartyRelationship>()
            .SingleAsync(item => item.SourcePartyId == sourcePartyId || item.TargetPartyId == sourcePartyId);
        Assert.Equal(PartyRelationshipKind.PartnerOf, persistedRelationship.RelationshipKind);
        Assert.Equal("Existing identity", persistedRelationship.Notes);
    }

    [Fact]
    public async Task List_relationships_surfaces_orphaned_party_reference()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var partyDirectoryService = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var managementService = scope.ServiceProvider.GetRequiredService<PartyDirectoryManagementService>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var sourcePartyId = await CreatePartyAsync(partyDirectoryService, "Orphan relationship source");
        var missingPartyId = Guid.NewGuid();
        var relationshipId = Guid.NewGuid();
        await using (var dbContext = await dbContextFactory.CreateDbContextAsync())
        {
            dbContext.Set<PartyRelationship>().Add(new PartyRelationship
            {
                Id = relationshipId,
                SourcePartyId = sourcePartyId,
                TargetPartyId = missingPartyId,
                RelationshipKind = PartyRelationshipKind.PartnerOf
            });
            await dbContext.SaveChangesAsync();
        }

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => managementService.ListRelationshipsAsync(sourcePartyId));

        Assert.Contains(relationshipId.ToString(), exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(missingPartyId.ToString(), exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Save_party_rejects_unknown_supplied_id_without_creating_a_replacement()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var partyDirectoryService = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var missingPartyId = Guid.NewGuid();

        var result = await partyDirectoryService.SavePartyAsync(new PartyEditorModel
        {
            Id = missingPartyId,
            PartyType = PartyType.Person,
            LifecycleStatus = PartyLifecycleStatus.Active,
            DisplayName = "Must not be created"
        });

        Assert.True(result.IsFailure);
        Assert.Contains(result.Errors, error => error.Code == PartyNotFoundErrorCode);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        Assert.False(await dbContext.Set<Party>().AnyAsync(item => item.Id == missingPartyId));
        Assert.False(await dbContext.Set<Party>().AnyAsync(item => item.DisplayName == "Must not be created"));
    }

    [Fact]
    public async Task Save_party_rejects_multiple_primary_contacts_of_the_same_type_without_mutation()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var partyDirectoryService = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var originalContactId = Guid.NewGuid();
        var partyId = await CreatePartyAsync(
            partyDirectoryService,
            "Primary contact invariant",
            [
                CreateContact(
                    originalContactId,
                    PartyContactType.Email,
                    "original@example.test",
                    isPrimary: true)
            ]);

        var result = await partyDirectoryService.SavePartyAsync(new PartyEditorModel
        {
            Id = partyId,
            PartyType = PartyType.Person,
            LifecycleStatus = PartyLifecycleStatus.Active,
            DisplayName = "Mutated display name",
            ContactPoints =
            [
                CreateContact(Guid.NewGuid(), PartyContactType.Email, "first@example.test", isPrimary: true),
                CreateContact(Guid.NewGuid(), PartyContactType.Email, "second@example.test", isPrimary: true)
            ]
        });

        Assert.True(result.IsFailure);
        Assert.Contains(result.Errors, error => error.Code == MultiplePrimaryContactsErrorCode);
        var persistedParty = await partyDirectoryService.GetPartyAsync(partyId);
        Assert.NotNull(persistedParty);
        Assert.Equal("Primary contact invariant", persistedParty.DisplayName);
        var persistedContact = Assert.Single(persistedParty.ContactPoints);
        Assert.Equal(originalContactId, persistedContact.Id);
        Assert.Equal("original@example.test", persistedContact.Value);
        Assert.True(persistedContact.IsPrimary);
    }

    [Fact]
    public async Task Get_party_returns_contacts_in_stable_type_primary_and_id_order()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var partyDirectoryService = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var firstEmailId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var secondEmailId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var primaryEmailId = Guid.Parse("00000000-0000-0000-0000-000000000003");
        var primaryPhoneId = Guid.Parse("00000000-0000-0000-0000-000000000004");
        var partyId = await CreatePartyAsync(
            partyDirectoryService,
            "Stable contact order",
            [
                CreateContact(secondEmailId, PartyContactType.Email, "second@example.test"),
                CreateContact(primaryPhoneId, PartyContactType.Phone, "+1 555 0100", isPrimary: true),
                CreateContact(primaryEmailId, PartyContactType.Email, "primary@example.test", isPrimary: true),
                CreateContact(firstEmailId, PartyContactType.Email, "first@example.test")
            ]);

        var party = await partyDirectoryService.GetPartyAsync(partyId);

        Assert.NotNull(party);
        Assert.Equal(
            [primaryEmailId, firstEmailId, secondEmailId, primaryPhoneId],
            party.ContactPoints.Select(item => item.Id).ToArray());
    }

    [Fact]
    public async Task Merge_party_prefers_retained_primary_then_stable_id_and_preserves_tags()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var partyDirectoryService = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var managementService = scope.ServiceProvider.GetRequiredService<PartyDirectoryManagementService>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var retainedPrimaryEmailId = Guid.Parse("10000000-0000-0000-0000-000000000001");
        var mergedPrimaryEmailId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var mergedDuplicateEmailId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var mergedPhoneIdA = Guid.Parse("00000000-0000-0000-0000-000000000010");
        var mergedPhoneIdB = Guid.Parse("00000000-0000-0000-0000-000000000020");
        var retainedPartyId = await CreatePartyAsync(
            partyDirectoryService,
            "Retained primary party",
            [
                CreateContact(
                    retainedPrimaryEmailId,
                    PartyContactType.Email,
                    "shared@example.test",
                    isPrimary: true,
                    isPublic: false,
                    tags: ["retained"])
            ]);
        var mergedPartyId = await CreatePartyAsync(
            partyDirectoryService,
            "Merged primary party",
            [
                CreateContact(
                    mergedPrimaryEmailId,
                    PartyContactType.Email,
                    "other@example.test",
                    isPrimary: true,
                    tags: ["merged-primary"]),
                CreateContact(
                    mergedDuplicateEmailId,
                    PartyContactType.Email,
                    "shared@example.test",
                    tags: ["merged-duplicate"]),
                CreateContact(
                    mergedPhoneIdB,
                    PartyContactType.Phone,
                    "+1 555 0200",
                    isPrimary: true,
                    tags: ["phone-b"])
            ]);
        await using (var dbContext = await dbContextFactory.CreateDbContextAsync())
        {
            dbContext.Set<PartyContactPoint>().Add(new PartyContactPoint
            {
                Id = mergedPhoneIdA,
                PartyId = mergedPartyId,
                ContactType = PartyContactType.Phone,
                Label = "Legacy duplicate primary",
                Value = "+1 555 0100",
                NormalizedValue = "+15550100",
                IsPrimary = true,
                IsPublic = true,
                TagsJson = JsonSerializer.Serialize(new[] { "phone-a" })
            });
            await dbContext.SaveChangesAsync();
        }

        var result = await managementService.MergePartyAsync(
            retainedPartyId,
            mergedPartyId,
            "integration-tests",
            "Integrity merge");

        Assert.True(result.IsSuccess);
        await using var verificationContext = await dbContextFactory.CreateDbContextAsync();
        var contacts = await verificationContext.Set<PartyContactPoint>()
            .Where(item => item.PartyId == retainedPartyId)
            .OrderBy(item => item.ContactType)
            .ThenBy(item => item.Id)
            .ToListAsync();
        Assert.All(
            contacts.GroupBy(item => item.ContactType),
            group => Assert.True(group.Count(item => item.IsPrimary) <= 1));
        var primaryEmail = Assert.Single(
            contacts,
            item => item.ContactType == PartyContactType.Email && item.IsPrimary);
        Assert.Equal(retainedPrimaryEmailId, primaryEmail.Id);
        var sharedEmail = Assert.Single(
            contacts,
            item => item.ContactType == PartyContactType.Email &&
                item.NormalizedValue == "shared@example.test");
        var sharedEmailTags = Assert.IsType<string[]>(
            JsonSerializer.Deserialize<string[]>(sharedEmail.TagsJson));
        Assert.False(sharedEmail.IsPublic);
        Assert.Equal(
            ["retained", "merged-duplicate"],
            sharedEmailTags);
        var primaryPhone = Assert.Single(
            contacts,
            item => item.ContactType == PartyContactType.Phone && item.IsPrimary);
        var expectedStablePhoneId = new[] { mergedPhoneIdA, mergedPhoneIdB }
            .OrderBy(item => item)
            .First();
        Assert.Equal(expectedStablePhoneId, primaryPhone.Id);
    }

    [Fact]
    public async Task Apply_import_rolls_back_every_row_when_a_later_row_fails_validation()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var managementService = scope.ServiceProvider.GetRequiredService<PartyDirectoryManagementService>();
        var partyDirectoryService = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();

        var result = await managementService.ApplyImportAsync(
            [
                new PartyCsvImportPreviewRowModel
                {
                    RowNumber = 2,
                    CanImport = true,
                    Party = new PartyEditorModel
                    {
                        DisplayName = "Atomic import first row",
                        PartyType = PartyType.Person,
                        LifecycleStatus = PartyLifecycleStatus.Candidate
                    }
                },
                new PartyCsvImportPreviewRowModel
                {
                    RowNumber = 3,
                    CanImport = true,
                    Party = new PartyEditorModel
                    {
                        DisplayName = "Atomic import invalid row",
                        PartyType = PartyType.Person,
                        LifecycleStatus = PartyLifecycleStatus.Candidate,
                        ConfidentialNotes =
                        [
                            new PartyConfidentialNoteEditorModel
                            {
                                Category = PartyConfidentialNoteCategories.HumanResources,
                                NoteText = "Sensitive note without sensitive classification"
                            }
                        ]
                    }
                }
            ],
            "integration-tests");

        Assert.True(result.IsFailure);
        var directory = await partyDirectoryService.ListDirectoryAsync();
        Assert.DoesNotContain(directory, item => item.DisplayName == "Atomic import first row");
        Assert.DoesNotContain(directory, item => item.DisplayName == "Atomic import invalid row");
    }

    private static async Task<Guid> CreatePartyAsync(
        PartyDirectoryService partyDirectoryService,
        string displayName,
        IReadOnlyList<PartyContactPointEditorModel>? contactPoints = null)
    {
        var result = await partyDirectoryService.SavePartyAsync(new PartyEditorModel
        {
            PartyType = PartyType.Person,
            LifecycleStatus = PartyLifecycleStatus.Active,
            DisplayName = displayName,
            LastChangedBy = "integration-tests",
            ContactPoints = contactPoints?.ToList() ?? []
        });
        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private static PartyContactPointEditorModel CreateContact(
        Guid id,
        PartyContactType contactType,
        string value,
        bool isPrimary = false,
        bool isPublic = true,
        IReadOnlyList<string>? tags = null)
    {
        return new PartyContactPointEditorModel
        {
            Id = id,
            ContactType = contactType,
            Label = contactType.ToString(),
            Value = value,
            NormalizedValue = value.Trim().ToLowerInvariant(),
            IsPrimary = isPrimary,
            IsPublic = isPublic,
            Tags = tags?.ToList() ?? []
        };
    }
}
