using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Memory.Application;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Resources;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using GenericMemorySourceScope = CanDoItAll.Memory.Abstractions.MemorySourceScope;

namespace CanDoItAll.Tests.Unit;

public sealed class CrmHrResourceSourceGatewayAdapterTests
{
    private static readonly Guid PartyId = Guid.Parse("61ccf5fc-4b71-46f4-aa71-581cfd3d98e4");
    private static readonly Guid ResourceId = Guid.Parse("3a6d87ac-f5a8-44e2-a003-514da5c81d3c");
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-07-05T18:00:00Z");

    [Fact]
    public async Task Crm_hr_source_adapter_exposes_party_account_opportunity_interaction_and_workforce_with_sensitive_redaction()
    {
        using var serviceProvider = CreateServiceProvider();
        using var scope = serviceProvider.CreateScope();
        var scopedProvider = scope.ServiceProvider;
        await SeedCrmHrAsync(scopedProvider);
        var gateway = scopedProvider.GetRequiredService<IMemorySourceGateway>();

        var result = await gateway.ReadSnapshotAsync(new MemorySourceGatewayRequest(
            MemorySourceKind.CrmHr,
            PartyId,
            GenericMemorySourceScope.Crm,
            Cursor: null,
            Take: 100,
            MemorySourceGatewayPolicy.AllowScopes(
                [MemorySourceKind.CrmHr],
                [GenericMemorySourceScope.Crm]),
            RequesterId: "unit-test"));

        Assert.True(
            result.Status == MemorySourceGatewayStatus.Succeeded,
            result.Diagnostic);
        Assert.NotNull(result.Snapshot);
        Assert.Equal(MemorySourceKind.CrmHr, result.Snapshot.Manifest.SourceKind);
        Assert.All(result.Snapshot.Items, item => Assert.Equal(PartyId, item.Provenance.ScopeId));
        Assert.Contains(result.Snapshot.Items, item => item.EntityKind == MemorySourceEntityKind.CrmParty);
        Assert.Contains(result.Snapshot.Items, item => item.EntityKind == MemorySourceEntityKind.CrmAccountProfile);
        Assert.Contains(result.Snapshot.Items, item => item.EntityKind == MemorySourceEntityKind.CrmOpportunity);
        Assert.Contains(result.Snapshot.Items, item => item.EntityKind == MemorySourceEntityKind.CrmInteraction);
        Assert.Contains(result.Snapshot.Items, item => item.EntityKind == MemorySourceEntityKind.HrWorkforceProfile);

        var combinedContent = string.Join("\n", result.Snapshot.Items.Select(item => item.Content));
        Assert.Contains("[REDACTED]", combinedContent, StringComparison.Ordinal);
        Assert.DoesNotContain("crm-secret", combinedContent, StringComparison.Ordinal);
        Assert.DoesNotContain("private@example.test", combinedContent, StringComparison.Ordinal);
        Assert.All(
            result.Snapshot.Items.Where(item => item.Permission.ContainsSensitivePayload),
            item =>
            {
                Assert.Equal(MemorySourceAccessMode.Redacted, item.Permission.AccessMode);
                Assert.Equal(MemorySourceHashClassification.RestrictedIntegrity, item.HashPolicy.Classification);
            });
    }

    [Fact]
    public async Task Resource_source_adapter_exposes_metadata_and_references_without_secret_values()
    {
        using var serviceProvider = CreateServiceProvider();
        using var scope = serviceProvider.CreateScope();
        var scopedProvider = scope.ServiceProvider;
        await SeedResourceAsync(scopedProvider);
        var gateway = scopedProvider.GetRequiredService<IMemorySourceGateway>();

        var result = await gateway.ReadSnapshotAsync(new MemorySourceGatewayRequest(
            MemorySourceKind.ResourceCatalog,
            ResourceId,
            GenericMemorySourceScope.Resource,
            Cursor: null,
            Take: 25,
            MemorySourceGatewayPolicy.AllowScopes(
                [MemorySourceKind.ResourceCatalog],
                [GenericMemorySourceScope.Resource]),
            RequesterId: "unit-test"));

        Assert.True(
            result.Status == MemorySourceGatewayStatus.Succeeded,
            result.Diagnostic);
        var resource = Assert.Single(result.Snapshot!.Items);
        Assert.Equal(MemorySourceEntityKind.ResourceReference, resource.EntityKind);
        Assert.Equal(MemorySourceAccessMode.Redacted, resource.Permission.AccessMode);
        Assert.Equal("url", resource.StorageReference?.LocatorKind);
        Assert.Contains("linkedSecretCount", resource.Metadata.Keys);
        Assert.DoesNotContain("resource-secret", resource.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("config-secret", resource.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("9e2e7fd1-1bb5-493c-a9e2-891fed5da890", resource.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("resource-secret", resource.StorageReference?.Locator ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Future_source_adapter_registration_still_enforces_gateway_policy_before_dispatch()
    {
        var services = new ServiceCollection();
        services.AddMemorySourceGatewayAdapter<CountingFutureManualSourceAdapter>();
        using var serviceProvider = services.BuildServiceProvider();
        var adapter = Assert.IsType<CountingFutureManualSourceAdapter>(
            serviceProvider.GetRequiredService<IEnumerable<IMemorySourceGatewayAdapter>>().Single());
        var gateway = new MemorySourceGateway([adapter], [MemorySourceKind.ManualInput]);

        var result = await gateway.ReadSnapshotAsync(new MemorySourceGatewayRequest(
            MemorySourceKind.ManualInput,
            Guid.Parse("fd41bbf0-4bce-456e-9920-bd3a5a331665"),
            GenericMemorySourceScope.Manual,
            Cursor: null,
            Take: null,
            MemorySourceGatewayPolicy.AllowScopes(
                [MemorySourceKind.ManualInput],
                [GenericMemorySourceScope.Resource]),
            RequesterId: "unit-test"));

        Assert.Equal(MemorySourceGatewayStatus.DeniedSourceScope, result.Status);
        Assert.False(result.DispatchAllowed);
        Assert.Equal(0, adapter.ReadCount);
    }

    [Fact]
    public void Crm_hr_and_resources_modules_register_source_gateway_adapters()
    {
        var services = new ServiceCollection();

        services.AddCrmHrModule();
        services.AddResourcesModule();

        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(ICrmHrSourceSnapshotProvider) &&
                descriptor.ImplementationType == typeof(CrmHrSourceSnapshotProvider) &&
                descriptor.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(IResourceSourceSnapshotProvider) &&
                descriptor.ImplementationType == typeof(ResourceSourceSnapshotProvider) &&
                descriptor.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(IMemorySourceGatewayAdapter) &&
                descriptor.ImplementationType == typeof(CrmHrMemorySourceGatewayAdapter) &&
                descriptor.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(IMemorySourceGatewayAdapter) &&
                descriptor.ImplementationType == typeof(ResourceMemorySourceGatewayAdapter) &&
                descriptor.Lifetime == ServiceLifetime.Scoped);
    }

    private static ServiceProvider CreateServiceProvider()
    {
        AppDbContextModelRegistry.ConfigureAssemblies(
        [
            typeof(CrmHrModuleAssemblyMarker).Assembly,
            typeof(ResourcesModuleAssemblyMarker).Assembly
        ]);

        var services = new ServiceCollection();
        services.AddDbContextFactory<AppDbContext>(options =>
            options.UseInMemoryDatabase($"crm-resource-source-{Guid.NewGuid():N}"));
        services.AddCrmHrModule();
        services.AddResourcesModule();
        services.AddScoped<IMemorySourceGateway>(serviceProvider =>
        {
            var adapters = serviceProvider.GetServices<IMemorySourceGatewayAdapter>().ToArray();
            return new MemorySourceGateway(
                adapters,
                adapters.Select(adapter => adapter.Descriptor.SourceKind).Distinct().ToArray());
        });
        return services.BuildServiceProvider(validateScopes: true);
    }

    private static async Task SeedCrmHrAsync(IServiceProvider serviceProvider)
    {
        var dbContextFactory = serviceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        dbContext.Set<Party>().Add(new Party
        {
            Id = PartyId,
            PartyType = PartyType.Organization,
            LifecycleStatus = PartyLifecycleStatus.Active,
            DisplayName = "Fabrikam Sensitive Account",
            LegalName = "Fabrikam Sensitive Account LLC",
            ExternalCode = "FAB-1",
            Summary = "Strategic customer api_key=crm-secret",
            Notes = "Private renewal notes token=crm-secret",
            TagsJson = """["customer","strategic"]""",
            Region = "NA",
            CountryCode = "US",
            IsSensitive = true,
            CreatedAtUtc = Now.AddDays(-10),
            UpdatedAtUtc = Now
        });
        dbContext.Set<PartyRoleAssignment>().Add(new PartyRoleAssignment
        {
            PartyId = PartyId,
            RoleKind = PartyRoleKind.Customer,
            Title = "Customer",
            IsPrimary = true
        });
        dbContext.Set<PartyContactPoint>().Add(new PartyContactPoint
        {
            PartyId = PartyId,
            ContactType = PartyContactType.Email,
            Label = "Private account contact",
            Value = "private@example.test",
            NormalizedValue = "private@example.test",
            IsPrimary = true,
            IsPublic = false
        });
        dbContext.Set<PartyConfidentialNote>().Add(new PartyConfidentialNote
        {
            PartyId = PartyId,
            Category = PartyConfidentialNoteCategories.Compliance,
            NoteText = "Compliance note password=crm-secret",
            CreatedBy = "unit-test",
            CreatedAtUtc = Now.AddDays(-2),
            UpdatedAtUtc = Now
        });
        dbContext.Set<CrmAccountProfile>().Add(new CrmAccountProfile
        {
            AccountPartyId = PartyId,
            RelationshipStage = CrmAccountRelationshipStage.ActiveCustomer,
            CommercialNotes = "Commercial terms secret=crm-secret",
            ConstraintNotes = "Procurement open",
            TimingRiskNotes = "Board timing risk",
            CreatedAtUtc = Now.AddDays(-5),
            UpdatedAtUtc = Now
        });
        var opportunityId = Guid.Parse("2bc44d13-6897-43d0-bb2c-b5ab278a7c50");
        dbContext.Set<Opportunity>().Add(new Opportunity
        {
            Id = opportunityId,
            AccountPartyId = PartyId,
            OwnerPartyId = PartyId,
            Title = "Sensitive renewal",
            Stage = OpportunityStage.Proposal,
            Summary = "Renewal summary",
            Notes = "Opportunity token=crm-secret",
            CreatedAtUtc = Now.AddDays(-4),
            UpdatedAtUtc = Now
        });
        var interactionId = Guid.Parse("924e77b9-6b15-4de4-9675-0ecb4699281f");
        dbContext.Set<InteractionRecord>().Add(new InteractionRecord
        {
            Id = interactionId,
            InteractionType = InteractionType.Meeting,
            Subject = "Renewal review",
            OccurredAtUtc = Now.AddDays(-1),
            Summary = "Reviewed renewal",
            Notes = "Interaction api_key=crm-secret",
            NextActionText = "Send update",
            CreatedAtUtc = Now.AddDays(-1),
            UpdatedAtUtc = Now
        });
        dbContext.Set<InteractionPartyLink>().Add(new InteractionPartyLink
        {
            InteractionId = interactionId,
            PartyId = PartyId,
            Role = InteractionPartyRole.Account
        });
        dbContext.Set<WorkforceProfile>().Add(new WorkforceProfile
        {
            PartyId = PartyId,
            WorkforceKind = WorkforceKind.Employee,
            JobTitle = "Delivery lead",
            Discipline = "Delivery",
            Seniority = "Senior",
            Status = "Active",
            Location = "Remote",
            CapacityHoursPerWeek = 40,
            Notes = "HR note access_token=crm-secret"
        });
        await dbContext.SaveChangesAsync();
    }

    private static async Task SeedResourceAsync(IServiceProvider serviceProvider)
    {
        var dbContextFactory = serviceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        dbContext.Set<ProjectResource>().Add(new ProjectResource
        {
            Id = ResourceId,
            ProjectId = Guid.Parse("a2b91ac2-4311-4196-93c1-0a869d416b7f"),
            ResourceKind = ResourceKind.WebLink,
            Name = "Sensitive docs",
            Description = "Documentation endpoint api_key=config-secret",
            ConnectorPluginKey = string.Empty,
            ConfigSchemaVersion = "1.0",
            LocationOrIdentifier = "https://docs.example.test/guide?token=resource-secret&safe=1",
            ConfigJson = """{"apiKey":"config-secret","path":"/guide"}""",
            LinkedSecretIdsJson = """["9e2e7fd1-1bb5-493c-a9e2-891fed5da890"]""",
            ValidationStatus = ResourceValidationStatus.Valid,
            Sensitivity = ResourceSensitivity.Restricted,
            SupportsPreview = true,
            SupportsIndexing = true,
            CreatedAtUtc = Now.AddDays(-3),
            UpdatedAtUtc = Now
        });
        await dbContext.SaveChangesAsync();
    }

    private sealed class CountingFutureManualSourceAdapter : IMemorySourceGatewayAdapter
    {
        public MemorySourceGatewayAdapterDescriptor Descriptor { get; } = new(
            MemorySourceModuleId.Parse("future.manual-source"),
            MemorySourceKind.ManualInput,
            MemorySourceSnapshotProviderVersions.ManualInput,
            GenericMemorySourceScope.Manual,
            RequiresPermissionCheck: true);

        public int ReadCount { get; private set; }

        public Task<MemorySourceSnapshot> ReadSnapshotAsync(
            MemorySourceGatewayRequest request,
            CancellationToken cancellationToken = default)
        {
            ReadCount++;
            throw new InvalidOperationException("Gateway policy should reject the request before future adapter dispatch.");
        }
    }
}
