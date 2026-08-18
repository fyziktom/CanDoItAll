using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Tests.Unit.Infrastructure;

public sealed class PartyOrganizationAffiliationPolicyTests
{
    private static readonly DateOnly Today = new(2026, 7, 29);

    [Fact]
    public void Replacement_validation_rejects_invalid_endpoints_dates_duplicates_and_primaries()
    {
        var personId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var duplicate = CreateEditor(personId, organizationId);
        var errors = PartyOrganizationAffiliationPolicy.ValidateReplacement(
            personId,
            [
                duplicate,
                CreateEditor(personId, organizationId),
                new PartyOrganizationAffiliationEditorModel
                {
                    PersonPartyId = personId,
                    OrganizationPartyId = personId,
                    AffiliationKind = PartyOrganizationAffiliationKind.Employee,
                    IsPrimary = true,
                    ManagerPartyId = personId,
                    ValidFrom = Today.AddDays(1),
                    ValidTo = Today
                },
                new PartyOrganizationAffiliationEditorModel
                {
                    PersonPartyId = personId,
                    OrganizationPartyId = Guid.NewGuid(),
                    AffiliationKind = PartyOrganizationAffiliationKind.Contractor,
                    IsPrimary = true
                }
            ],
            "unit-tests",
            Today);

        Assert.Contains(errors, error => error.Code == "crmhr.affiliation.duplicate");
        Assert.Contains(errors, error => error.Code == "crmhr.affiliation.self-reference");
        Assert.Contains(errors, error => error.Code == "crmhr.affiliation.date-range-invalid");
        Assert.Contains(errors, error => error.Code == "crmhr.affiliation.primary-not-current");
        Assert.Contains(errors, error => error.Code == "crmhr.affiliation.primary-duplicate");
    }

    [Theory]
    [InlineData(
        PartyOrganizationAffiliationKind.ExternalContact,
        WorkforceKind.Employee,
        PartyType.Person,
        false,
        WorkforceRecordClassification.ExternalContact)]
    [InlineData(
        PartyOrganizationAffiliationKind.Contractor,
        WorkforceKind.Employee,
        PartyType.Person,
        false,
        WorkforceRecordClassification.Contractor)]
    [InlineData(
        null,
        WorkforceKind.Freelancer,
        PartyType.Person,
        false,
        WorkforceRecordClassification.Freelancer)]
    [InlineData(
        null,
        null,
        PartyType.OrganizationUnit,
        false,
        WorkforceRecordClassification.DeliveryUnit)]
    [InlineData(
        null,
        null,
        PartyType.Organization,
        true,
        WorkforceRecordClassification.DeliveryUnit)]
    [InlineData(
        null,
        null,
        PartyType.Person,
        false,
        WorkforceRecordClassification.ExternalContact)]
    public void Workforce_classification_applies_affiliation_profile_delivery_and_fallback_precedence(
        PartyOrganizationAffiliationKind? affiliationKind,
        WorkforceKind? workforceKind,
        PartyType partyType,
        bool hasDeliveryUnitRole,
        WorkforceRecordClassification expected)
    {
        var actual = WorkforceRecordClassificationPolicy.Resolve(
            affiliationKind,
            workforceKind,
            partyType,
            hasDeliveryUnitRole);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task Workforce_query_rejects_unbounded_inputs_before_opening_a_database()
    {
        var service = new WorkforceRecordQueryService(
            new ThrowingDbContextFactory(),
            new FixedClock(new DateTimeOffset(2026, 7, 29, 12, 0, 0, TimeSpan.Zero)));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.SearchAsync(new WorkforceRecordQuery(
                PageSize: WorkforceRecordQueryLimits.MaximumPageSize + 1)));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.SearchAsync(new WorkforceRecordQuery(
                SearchText: new string('x', WorkforceRecordQueryLimits.MaximumSearchLength + 1))));
    }

    private static PartyOrganizationAffiliationEditorModel CreateEditor(
        Guid personId,
        Guid organizationId)
        => new()
        {
            PersonPartyId = personId,
            OrganizationPartyId = organizationId,
            AffiliationKind = PartyOrganizationAffiliationKind.ExternalContact
        };

    private sealed class ThrowingDbContextFactory : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext()
            => throw new InvalidOperationException("Validation opened a database context.");
    }

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset GetUtcNow() => now;
    }
}
