using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.AgentFramework.ProviderManagement;
using CanDoItAll.SharedKernel;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Tests.Integration.CrmHr;

public sealed class AiStewardNativeBoundaryTests {
    [Fact]
    public async Task Canonical_technical_creation_rejects_opaque_CRM_JSON_instead_of_silently_discarding_it() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var created = await services.GetRequiredService<PartyDirectoryService>().SavePartyAsync(new() {
            PartyType = PartyType.AiAgent, DisplayName = "Explicit technical configuration owner"
        });
        Assert.True(created.IsSuccess);

        var result = await services.GetRequiredService<IAiTechnicalAgentBridge>().SaveAsync(new() {
            PartyId = created.Value, ExtendedDataJson = """{"configuration":"owned-elsewhere"}"""
        });

        Assert.True(result.IsFailure);
        Assert.Contains(result.Errors, error => error.Code == AiStewardEditingErrors.TechnicalReadOnly);
        await using var context = await services.GetRequiredService<IDbContextFactory<CrmHrDbContext>>().CreateDbContextAsync();
        Assert.False(await context.Set<AiResourceBinding>().AnyAsync(binding => binding.PartyId == created.Value));
        Assert.False(await context.Set<AiAgentProfile>().AnyAsync(profile => profile.PartyId == created.Value));
    }

    [Fact]
    public async Task The_business_owner_freezes_the_submission_before_awaiting_a_database_context() {
        var gate = new ContextGate();
        await using var application = await TestApplication.CreateAsync(new() {
            ConfigureServices = services => {
                services.RemoveAll<IDbContextFactory<CrmHrDbContext>>();
                services.AddSingleton<IDbContextFactory<CrmHrDbContext>>(provider => new GatedContexts(provider.GetRequiredService<DbContextOptions<CrmHrDbContext>>(), gate));
            }
        });
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var created = await services.GetRequiredService<AiAgentService>().CreateAgentAsync("Frozen stewardship");
        Assert.True(created.IsSuccess);
        var owner = services.GetRequiredService<IAiStewardProfileEditing>();
        var draft = (await owner.ReadAsync(created.Value))!;
        draft.Notes = "Submitted stewardship";
        gate.Hold = true;
        var write = owner.SaveAsync(draft);
        try {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            draft.PartyId = Guid.NewGuid();
            draft.Notes = "Later local stewardship";
            draft.Original = null;
        } finally {
            gate.Release.TrySetResult();
        }

        Assert.True((await write).IsSuccess);
        Assert.Equal("Submitted stewardship", (await owner.ReadAsync(created.Value))!.Notes);
    }

    [Fact]
    public async Task The_standalone_legacy_producer_creates_once_and_returns_its_exact_business_observation() {
        await using var application = await TestApplication.CreateAsync(new() {
            ConfigureServices = services => {
                var legacy = new ServiceCollection().AddCrmHrModule().Single(descriptor => descriptor.ServiceType == typeof(IAiTechnicalAgentBridge));
                services.RemoveAll<IAiTechnicalAgentBridge>();
                services.Add(legacy);
                services.RemoveAll<IProviderAdministrationService>();
                services.AddSingleton(DispatchProxy.Create<IProviderAdministrationService, StandaloneProviders>());
            }
        });
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var created = await services.GetRequiredService<PartyDirectoryService>().SavePartyAsync(new() {
            PartyType = PartyType.AiAgent, DisplayName = "Standalone legacy steward"
        });
        Assert.True(created.IsSuccess);
        var bridge = services.GetRequiredService<IAiTechnicalAgentBridge>();
        const string legacyConfiguration = """{"legacyConfiguration":"preserved"}""";
        var technical = await bridge.SaveAsync(new() {
            PartyId = created.Value, DefaultModel = "Created legacy model", ExtendedDataJson = legacyConfiguration
        });
        Assert.True(technical.IsSuccess);
        var owner = services.GetRequiredService<IAiStewardProfileEditing>();
        var business = (await owner.ReadAsync(created.Value))!;
        Assert.Equal(technical.Value!.CreatedBusinessProfile, business.Original);
        Assert.NotNull(technical.Value.CreatedBusinessProfile!.ProfileId);
        business.Notes = "Accepted standalone business notes";
        Assert.True((await owner.SaveAsync(business)).IsSuccess);

        var replaced = await bridge.SaveAsync(new() { PartyId = created.Value, DefaultModel = "Forbidden legacy replacement" });

        Assert.True(replaced.IsFailure);
        Assert.Equal("Created legacy model", (await bridge.GetWorkspaceAsync(created.Value)).DefaultModel);
        Assert.Equal("Accepted standalone business notes", (await owner.ReadAsync(created.Value))!.Notes);
        await using var context = await services.GetRequiredService<IDbContextFactory<CrmHrDbContext>>().CreateDbContextAsync();
        Assert.Equal(legacyConfiguration, (await context.Set<AiAgentProfile>().SingleAsync(profile => profile.PartyId == created.Value)).ExtendedDataJson);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Creation_continuations_preserve_a_peers_business_work_and_report_the_committed_identity(bool technicalAccepted) {
        var handoff = new Handoff(technicalAccepted);
        await using var application = await TestApplication.CreateAsync(new() {
            ConfigureServices = services => {
                var registration = services.Last(descriptor => descriptor.ServiceType == typeof(IAiTechnicalAgentBridge));
                var implementation = registration.ImplementationType ?? throw new InvalidOperationException("Expected the native bridge registration.");
                services.RemoveAll<IAiTechnicalAgentBridge>();
                services.AddScoped<IAiTechnicalAgentBridge>(provider => new ObservedBridge(
                    (IAiTechnicalAgentBridge)ActivatorUtilities.CreateInstance(provider, implementation), handoff));
            }
        });
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        handoff.Peer = async draft => {
            if (technicalAccepted) {
                var profiles = services.GetRequiredService<IAiStewardProfileEditing>();
                var peer = (await profiles.ReadAsync(draft.PartyId))!;
                peer.Notes = "Accepted peer stewardship before creation continuation";
                Assert.True((await profiles.SaveAsync(peer)).IsSuccess);
            } else {
                var parties = services.GetRequiredService<PartyDirectoryService>();
                var peer = (await parties.GetPartyAsync(draft.PartyId))!;
                peer.Summary = "Accepted peer party update before rejection";
                Assert.True((await parties.SavePartyAsync(peer)).IsSuccess);
            }
        };

        var error = await Record.ExceptionAsync(() => services.GetRequiredService<AiAgentService>().CreateAgentAsync("Retained peer work"));

        Guid partyId;
        if (technicalAccepted) {
            var partial = Assert.IsType<AiTechnicalAgentCommittedSaveException>(error);
            Assert.Equal(handoff.Accepted!.TechnicalAgentId, partial.TechnicalAgentId);
            partyId = partial.PartyId!.Value;
            Assert.Equal("Accepted peer stewardship before creation continuation", (await services.GetRequiredService<IAiStewardProfileEditing>().ReadAsync(partyId))!.Notes);
        } else {
            var partial = Assert.IsType<AiAgentCreationIncompleteException>(error);
            partyId = partial.PartyId;
            Assert.Contains(partial.Errors, failure => failure.Code == "test.creation-rejected");
            Assert.Equal("Accepted peer party update before rejection", (await services.GetRequiredService<PartyDirectoryService>().GetPartyAsync(partyId))!.Summary);
        }
        Assert.Equal(handoff.PartyId, partyId);
        await using var context = await services.GetRequiredService<IDbContextFactory<CrmHrDbContext>>().CreateDbContextAsync();
        Assert.Equal(1, await context.Set<Party>().CountAsync(party => party.Id == partyId));
        Assert.Equal(technicalAccepted ? 1 : 0, await context.Set<AiResourceBinding>().CountAsync(binding => binding.PartyId == partyId));
    }

    private sealed class ContextGate {
        public bool Hold { get; set; }
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    public class StandaloneProviders : DispatchProxy {
        protected override object? Invoke(MethodInfo? method, object?[]? arguments) {
            if (method?.Name != nameof(IProviderAdministrationService.ListProviderProfilesAsync)) {
                throw new NotSupportedException("The standalone writer fixture only supplies provider catalog reads.");
            }
            return Task.FromResult<IReadOnlyList<ProviderProfileSummary>>([]);
        }
    }

    private sealed class GatedContexts(DbContextOptions<CrmHrDbContext> options, ContextGate gate) : IDbContextFactory<CrmHrDbContext> {
        public CrmHrDbContext CreateDbContext() => new(options);
        public async Task<CrmHrDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) {
            if (gate.Hold) {
                gate.Hold = false;
                gate.Entered.TrySetResult();
                await gate.Release.Task.WaitAsync(cancellationToken);
            }
            return CreateDbContext();
        }
    }

    private sealed class Handoff(bool accept) {
        public bool Accept { get; } = accept;
        public Guid PartyId { get; set; }
        public AiTechnicalAgentSaveResult? Accepted { get; set; }
        public Func<AiAgentProfileEditorModel, Task> Peer { get; set; } = _ => throw new InvalidOperationException("The peer handoff was not configured.");
    }

    private sealed class ObservedBridge(IAiTechnicalAgentBridge native, Handoff handoff) : IAiTechnicalAgentBridge {
        public Task SynchronizeDirectoryProjectionAsync(CancellationToken cancellationToken = default) => native.SynchronizeDirectoryProjectionAsync(cancellationToken);
        public Task<IReadOnlyDictionary<Guid, AiTechnicalAgentDirectorySummary>> GetDirectorySummariesAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken = default) => native.GetDirectorySummariesAsync(ids, cancellationToken);
        public Task<AiTechnicalAgentWorkspaceModel> GetWorkspaceAsync(Guid id, CancellationToken cancellationToken = default) => native.GetWorkspaceAsync(id, cancellationToken);
        public Task<IReadOnlyDictionary<Guid, AiAgentStaffingFactModel>> GetStaffingFactsAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken = default) => native.GetStaffingFactsAsync(ids, cancellationToken);
        public async Task<Result<AiTechnicalAgentSaveResult>> SaveAsync(AiAgentProfileEditorModel model, CancellationToken cancellationToken = default) {
            handoff.PartyId = model.PartyId;
            var accepted = handoff.Accept ? await native.SaveAsync(model, cancellationToken) : null;
            if (accepted?.IsFailure == true) {
                return accepted;
            }
            handoff.Accepted = accepted?.Value;
            await handoff.Peer(model);
            return accepted ?? Result<AiTechnicalAgentSaveResult>.Failure(Error.Validation("Known native precreation rejection.", "test.creation-rejected"));
        }
    }
}
