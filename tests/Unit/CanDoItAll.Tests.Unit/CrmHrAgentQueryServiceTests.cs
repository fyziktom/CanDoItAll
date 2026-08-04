using System.Text.Json;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Memory.SourceGateway;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Unit;

[Collection(AppDbContextModelRegistryTestCollectionNames.Name)]
public sealed class CrmHrAgentQueryServiceTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-07-14T12:00:00Z");
    private static readonly Guid GenericPartyId = Guid.Parse("1d93eeeb-028b-4fdb-a7b2-9c7662e50287");
    private static readonly Guid WorkforcePartyId = Guid.Parse("38384b27-6844-45c2-bdd0-591722cc6d9d");
    private static readonly Guid AccountPartyId = Guid.Parse("cd9669f3-49aa-4233-b1e0-73868da7290a");
    private static readonly Guid UnprofiledOrganizationId = Guid.Parse("9203c7e3-6e74-4c39-b8c1-2fac6217dff9");
    private static readonly Guid OpportunityId = Guid.Parse("7915371e-f239-429c-9497-0ad30c735677");
    private static readonly Guid AiAgentPartyId = Guid.Parse("d1237f99-aa38-4f66-9ae2-cf64f186ad0d");
    private static readonly Guid SensitiveWorkforcePartyId = Guid.Parse("970ad84d-2980-47c6-9c34-b25bb9d829b9");
    private static readonly Guid SensitiveAccountPartyId = Guid.Parse("a20f40aa-c9c2-4e4b-8c06-dc3df4eeabdd");
    private static readonly Guid SensitiveOpportunityId = Guid.Parse("a221dd24-8b35-4291-8337-e459c67e21ed");
    private static readonly Guid InlineRedactionPartyId = Guid.Parse("2e79657a-f838-439d-99db-a6c3d5f4c729");

    [Fact]
    public async Task Search_returns_only_typed_safe_fields_and_uses_the_injected_clock_for_availability()
    {
        using var serviceProvider = CreateServiceProvider();
        using var scope = serviceProvider.CreateScope();
        await SeedSafeRecordsAsync(scope.ServiceProvider);
        var service = scope.ServiceProvider.GetRequiredService<ICrmHrAgentQueryService>();

        var party = Assert.Single((await service.SearchAsync(new CrmHrAgentSearchQuery(
            "General party",
            CrmHrAgentRecordKind.Party))).Value!);
        Assert.Equal(GenericPartyId, party.Id);
        Assert.Equal(PartyLifecycleStatus.Active, party.Status.LifecycleStatus);
        Assert.Equal(new[] { "delivery", "mentor" }, party.SafeTags);

        var workforce = Assert.Single((await service.SearchAsync(new CrmHrAgentSearchQuery(
            "Platform Engineer",
            CrmHrAgentRecordKind.Workforce))).Value!);
        Assert.Equal(WorkforcePartyId, workforce.Id);
        Assert.Equal("Active", workforce.Status.WorkforceStatus);
        Assert.Contains("Platform Engineer", workforce.SafeSummary, StringComparison.Ordinal);
        Assert.Contains("Engineering", workforce.SafeSummary, StringComparison.Ordinal);
        Assert.NotNull(workforce.Availability);
        Assert.Equal(WorkforceAvailabilityState.NearAvailable, workforce.Availability.State);
        Assert.Equal(40m, workforce.Availability.AvailablePercent);
        Assert.Equal(DateOnly.FromDateTime(Now.AddDays(20).UtcDateTime), workforce.Availability.NextAvailabilityOn);

        var account = Assert.Single((await service.SearchAsync(new CrmHrAgentSearchQuery(
            "Fabrikam",
            CrmHrAgentRecordKind.CrmAccount))).Value!);
        Assert.Equal(AccountPartyId, account.Id);
        Assert.Equal(CrmAccountRelationshipStage.ActiveCustomer, account.Status.AccountRelationshipStage);
        Assert.Empty((await service.SearchAsync(new CrmHrAgentSearchQuery(
            "Unprofiled Organization",
            CrmHrAgentRecordKind.CrmAccount))).Value!);

        var aiAgent = Assert.Single((await service.SearchAsync(new CrmHrAgentSearchQuery(
            "Atlas",
            CrmHrAgentRecordKind.AiAgent))).Value!);
        Assert.Equal(AiAgentPartyId, aiAgent.Id);
        Assert.Equal(AiValidationStatus.Approved, aiAgent.Status.AiValidationStatus);

        Assert.All(
            new[] { party, workforce, account, aiAgent },
            item =>
            {
                Assert.Equal(CrmHrAgentBusinessTextTrust.UntrustedBusinessData, item.BusinessTextTrust);
                Assert.Equal(CrmHrAgentRedactionState.None, item.RedactionState);
            });
    }

    [Fact]
    public async Task Search_is_bounded_and_rejects_invalid_inputs_with_stable_error_codes()
    {
        using var serviceProvider = CreateServiceProvider();
        using var scope = serviceProvider.CreateScope();
        await SeedSafeRecordsAsync(scope.ServiceProvider);
        var service = scope.ServiceProvider.GetRequiredService<ICrmHrAgentQueryService>();

        var bounded = await service.SearchAsync(new CrmHrAgentSearchQuery(
            "party",
            CrmHrAgentRecordKind.Party,
            Take: 1));
        Assert.True(bounded.IsSuccess);
        Assert.Single(bounded.Value!);

        AssertFailureCode(
            await service.SearchAsync(new CrmHrAgentSearchQuery("party", Take: 0)),
            CrmHrAgentQueryErrorCodes.TakeOutOfRange);
        AssertFailureCode(
            await service.SearchAsync(new CrmHrAgentSearchQuery(
                "party",
                Take: CrmHrAgentQueryLimits.MaxTake + 1)),
            CrmHrAgentQueryErrorCodes.TakeOutOfRange);
        AssertFailureCode(
            await service.SearchAsync(new CrmHrAgentSearchQuery(" ")),
            CrmHrAgentQueryErrorCodes.SearchRequired);
        AssertFailureCode(
            await service.SearchAsync(new CrmHrAgentSearchQuery(
                new string('x', CrmHrAgentQueryLimits.MaxQueryLength + 1))),
            CrmHrAgentQueryErrorCodes.SearchTooLong);
        AssertFailureCode(
            await service.SearchAsync(new CrmHrAgentSearchQuery(
                "party",
                (CrmHrAgentRecordKind)999)),
            CrmHrAgentQueryErrorCodes.RecordKindInvalid);
    }

    [Fact]
    public async Task Search_hides_sensitive_records_and_opportunities_before_matching()
    {
        using var serviceProvider = CreateServiceProvider();
        using var scope = serviceProvider.CreateScope();
        await SeedSafeRecordsAsync(scope.ServiceProvider);
        await SeedSensitiveRecordsAsync(scope.ServiceProvider);
        var service = scope.ServiceProvider.GetRequiredService<ICrmHrAgentQueryService>();

        Assert.Empty((await service.SearchAsync(new CrmHrAgentSearchQuery(
            "Restricted Worker",
            CrmHrAgentRecordKind.Party))).Value!);
        Assert.Empty((await service.SearchAsync(new CrmHrAgentSearchQuery(
            "Restricted Worker",
            CrmHrAgentRecordKind.Workforce))).Value!);
        Assert.Empty((await service.SearchAsync(new CrmHrAgentSearchQuery(
            "Restricted Account",
            CrmHrAgentRecordKind.CrmAccount))).Value!);
        Assert.Empty((await service.SearchAsync(new CrmHrAgentSearchQuery(
            "Renewal",
            CrmHrAgentRecordKind.Opportunity))).Value!);
        Assert.Empty((await service.SearchAsync(new CrmHrAgentSearchQuery("Restricted"))).Value!);
        Assert.Empty((await service.SearchAsync(new CrmHrAgentSearchQuery("payroll-secret"))).Value!);
        Assert.Empty((await service.SearchAsync(new CrmHrAgentSearchQuery(
            "inline-secret",
            CrmHrAgentRecordKind.Party))).Value!);
        Assert.Empty((await service.SearchAsync(new CrmHrAgentSearchQuery(
            "tag-secret",
            CrmHrAgentRecordKind.Party))).Value!);

        var redactedWorkforce = (await service.GetSummaryAsync(new CrmHrAgentItemReference(
            CrmHrAgentRecordKind.Workforce,
            SensitiveWorkforcePartyId))).Value!;
        Assert.Equal(MemorySourceSnapshotSecurity.RedactedValue, redactedWorkforce.DisplayLabel);
        Assert.Equal(MemorySourceSnapshotSecurity.RedactedValue, redactedWorkforce.SafeSummary);
        Assert.Equal(CrmHrAgentRedactionState.SensitiveRecordRedacted, redactedWorkforce.RedactionState);
        Assert.Empty(redactedWorkforce.SafeTags);
        Assert.Null(redactedWorkforce.Availability);
        Assert.Null(redactedWorkforce.Status.LifecycleStatus);
        Assert.Equal(string.Empty, redactedWorkforce.Status.WorkforceStatus);

        var redactedOpportunity = (await service.GetSummaryAsync(new CrmHrAgentItemReference(
            CrmHrAgentRecordKind.Opportunity,
            SensitiveOpportunityId))).Value!;
        Assert.Equal(CrmHrAgentRedactionState.SensitiveRecordRedacted, redactedOpportunity.RedactionState);

        var serialized = JsonSerializer.Serialize(new[] { redactedWorkforce, redactedOpportunity });
        Assert.DoesNotContain("Restricted Worker", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("private@example.test", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("payroll-secret", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("sensitive-extended-data", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("901.25", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("Restricted renewal", serialized, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Summary_redacts_inline_secret_assignments_and_rejects_invalid_references()
    {
        using var serviceProvider = CreateServiceProvider();
        using var scope = serviceProvider.CreateScope();
        await SeedSafeRecordsAsync(scope.ServiceProvider);
        var service = scope.ServiceProvider.GetRequiredService<ICrmHrAgentQueryService>();

        var item = (await service.GetSummaryAsync(new CrmHrAgentItemReference(
            CrmHrAgentRecordKind.Party,
            InlineRedactionPartyId))).Value!;
        Assert.Equal(CrmHrAgentRedactionState.InlineSensitiveValueRedacted, item.RedactionState);
        Assert.Contains("api_key=[REDACTED]", item.SafeSummary, StringComparison.Ordinal);
        Assert.Contains("token=[REDACTED]", item.SafeTags);
        var serialized = JsonSerializer.Serialize(item);
        Assert.DoesNotContain("inline-secret", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("tag-secret", serialized, StringComparison.Ordinal);

        AssertFailureCode(
            await service.GetSummaryAsync(new CrmHrAgentItemReference(
                CrmHrAgentRecordKind.Party,
                Guid.Empty)),
            CrmHrAgentQueryErrorCodes.RecordIdRequired);
        AssertFailureCode(
            await service.GetSummaryAsync(new CrmHrAgentItemReference(
                (CrmHrAgentRecordKind)999,
                GenericPartyId)),
            CrmHrAgentQueryErrorCodes.RecordKindInvalid);
        AssertFailureCode(
            await service.GetSummaryAsync(new CrmHrAgentItemReference(
                CrmHrAgentRecordKind.Workforce,
                GenericPartyId)),
            CrmHrAgentQueryErrorCodes.RecordNotFound);
        AssertFailureCode(
            await service.GetSummaryAsync(new CrmHrAgentItemReference(
                CrmHrAgentRecordKind.CrmAccount,
                UnprofiledOrganizationId)),
            CrmHrAgentQueryErrorCodes.RecordNotFound);
    }

    [Fact]
    public void Add_crm_hr_module_registers_the_agent_query_boundary_as_scoped()
    {
        var services = new ServiceCollection();

        services.AddCrmHrModule();

        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(ICrmHrAgentQueryService) &&
                descriptor.ImplementationType == typeof(CrmHrAgentQueryService) &&
                descriptor.Lifetime == ServiceLifetime.Scoped);
    }

    private static ServiceProvider CreateServiceProvider()
    {
        AppDbContextModelRegistry.ConfigureAssemblies(
        [
            typeof(CrmHrModuleAssemblyMarker).Assembly
        ]);

        var services = new ServiceCollection();
        services.AddSingleton<IClock>(new FixedClock(Now));
        services.AddDbContextFactory<AppDbContext>(options =>
        {
            AppDbContextTestOptionsBuilder.ConfigureModelCacheKey(options);
            options.UseInMemoryDatabase($"crm-hr-agent-query-{Guid.NewGuid():N}");
        });
        services.AddCrmHrModule();
        return services.BuildServiceProvider(validateScopes: true);
    }

    private static async Task SeedSafeRecordsAsync(IServiceProvider serviceProvider)
    {
        var dbContextFactory = serviceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        dbContext.Set<Party>().AddRange(
            CreateParty(GenericPartyId, PartyType.Person, "General Party", "Delivery coordinator", "[\"delivery\",\"mentor\"]"),
            CreateParty(WorkforcePartyId, PartyType.Person, "Jordan Lee", "Platform delivery specialist", "[\"platform\"]"),
            CreateParty(AccountPartyId, PartyType.Organization, "Fabrikam", "Strategic customer", "[\"customer\"]"),
            CreateParty(UnprofiledOrganizationId, PartyType.Organization, "Unprofiled Organization", "Directory-only organization", "[]"),
            CreateParty(AiAgentPartyId, PartyType.AiAgent, "Atlas", "Delivery assistant", "[\"assistant\"]"),
            CreateParty(
                InlineRedactionPartyId,
                PartyType.Person,
                "Public Operations",
                "Operational summary api_key=inline-secret",
                "[\"public\",\"token=tag-secret\"]"));
        dbContext.Set<WorkforceProfile>().Add(new WorkforceProfile
        {
            PartyId = WorkforcePartyId,
            WorkforceKind = WorkforceKind.Employee,
            JobTitle = "Platform Engineer",
            Discipline = "Engineering",
            Status = "Active",
            CapacityHoursPerWeek = 40m
        });
        dbContext.Set<ProjectPartyAssignment>().Add(new ProjectPartyAssignment
        {
            ProjectId = Guid.Parse("616d148c-ec16-47db-8787-b45712a20d44"),
            PartyId = WorkforcePartyId,
            AssignmentKind = ProjectPartyAssignmentKind.TeamMember,
            AllocationPercent = 60m,
            StartsAtUtc = Now.AddDays(-10),
            EndsAtUtc = Now.AddDays(20)
        });
        dbContext.Set<CrmAccountProfile>().Add(new CrmAccountProfile
        {
            AccountPartyId = AccountPartyId,
            RelationshipStage = CrmAccountRelationshipStage.ActiveCustomer,
            CreatedAtUtc = Now.AddDays(-30),
            UpdatedAtUtc = Now
        });
        dbContext.Set<Opportunity>().Add(new Opportunity
        {
            Id = OpportunityId,
            Title = "Fabrikam renewal",
            Stage = OpportunityStage.Proposal,
            AccountPartyId = AccountPartyId,
            OwnerPartyId = GenericPartyId,
            Summary = "Renewal planning",
            CreatedAtUtc = Now.AddDays(-10),
            UpdatedAtUtc = Now
        });
        dbContext.Set<AiAgentProfile>().Add(new AiAgentProfile
        {
            PartyId = AiAgentPartyId,
            ExecutionMode = AiExecutionMode.Remote,
            ValidationStatus = AiValidationStatus.Approved
        });
        await dbContext.SaveChangesAsync();
    }

    private static async Task SeedSensitiveRecordsAsync(IServiceProvider serviceProvider)
    {
        var dbContextFactory = serviceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        dbContext.Set<Party>().AddRange(
            CreateParty(
                SensitiveWorkforcePartyId,
                PartyType.Person,
                "Restricted Worker",
                "Private workforce summary api_key=payroll-secret",
                "[\"private\"]",
                isSensitive: true,
                extendedDataJson: "{\"private\":\"sensitive-extended-data\"}"),
            CreateParty(
                SensitiveAccountPartyId,
                PartyType.Organization,
                "Restricted Account",
                "Private account summary",
                "[\"restricted\"]",
                isSensitive: true));
        dbContext.Set<WorkforceProfile>().Add(new WorkforceProfile
        {
            PartyId = SensitiveWorkforcePartyId,
            WorkforceKind = WorkforceKind.Employee,
            JobTitle = "Finance lead",
            Status = "Active",
            InternalCostRate = 901.25m,
            ExternalBillingRate = 1_501.75m,
            Notes = "Compensation payroll-secret"
        });
        dbContext.Set<PartyContactPoint>().Add(new PartyContactPoint
        {
            PartyId = SensitiveWorkforcePartyId,
            ContactType = PartyContactType.Email,
            Label = "Private",
            Value = "private@example.test",
            NormalizedValue = "private@example.test",
            IsPrimary = true,
            IsPublic = false
        });
        dbContext.Set<PartyConfidentialNote>().Add(new PartyConfidentialNote
        {
            PartyId = SensitiveWorkforcePartyId,
            Category = PartyConfidentialNoteCategories.Compensation,
            NoteText = "Confidential payroll-secret",
            CreatedBy = "unit-test",
            CreatedAtUtc = Now.AddDays(-2),
            UpdatedAtUtc = Now
        });
        dbContext.Set<CrmAccountProfile>().Add(new CrmAccountProfile
        {
            AccountPartyId = SensitiveAccountPartyId,
            RelationshipStage = CrmAccountRelationshipStage.ActiveCustomer,
            CommercialNotes = "Private contract payroll-secret",
            CreatedAtUtc = Now.AddDays(-20),
            UpdatedAtUtc = Now
        });
        dbContext.Set<Opportunity>().Add(new Opportunity
        {
            Id = SensitiveOpportunityId,
            Title = "Restricted renewal",
            Stage = OpportunityStage.Negotiation,
            AccountPartyId = SensitiveAccountPartyId,
            OwnerPartyId = SensitiveWorkforcePartyId,
            Summary = "Private renewal payroll-secret",
            Notes = "Confidential",
            CreatedAtUtc = Now.AddDays(-5),
            UpdatedAtUtc = Now
        });
        await dbContext.SaveChangesAsync();
    }

    private static Party CreateParty(
        Guid id,
        PartyType partyType,
        string displayName,
        string summary,
        string tagsJson,
        bool isSensitive = false,
        string extendedDataJson = "{}")
    {
        return new Party
        {
            Id = id,
            PartyType = partyType,
            LifecycleStatus = PartyLifecycleStatus.Active,
            DisplayName = displayName,
            Summary = summary,
            TagsJson = tagsJson,
            IsSensitive = isSensitive,
            ExtendedDataJson = extendedDataJson,
            CreatedAtUtc = Now.AddDays(-60),
            UpdatedAtUtc = Now
        };
    }

    private static void AssertFailureCode(Result result, string expectedCode)
    {
        Assert.True(result.IsFailure);
        Assert.Contains(result.Errors, error => error.Code == expectedCode);
    }

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset GetUtcNow() => now;
    }
}
