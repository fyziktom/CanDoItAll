using System.Data.Common;
using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Tests.Support;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class CrmDevelopmentDiagnosticQueryTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Owner_queries_preserve_original_diagnostic_population_order_fields_and_json(bool stringEnums) {
        var probe = new DiagnosticReadProbe();
        await using var application = await TestApplication.CreateAsync(new() { ConfigureServices = probe.Configure });
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var seeded = await SeedPopulationAsync(services);
        await using var complete = await services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();
        var legacyParties = await complete.Set<Party>()
            .Where(item => item.PartyType == PartyType.AiAgent)
            .OrderBy(item => item.DisplayName)
            .Select(item => new { item.Id, item.DisplayName })
            .ToListAsync();
        var legacyPartyIds = legacyParties.Select(item => item.Id).ToList();
        var legacyBindings = await complete.Set<AiResourceBinding>()
            .Where(item => legacyPartyIds.Contains(item.PartyId))
            .Select(item => new { item.PartyId, item.TechnicalAgentId, item.BindingStatus, item.BindingReason })
            .ToListAsync();
        var owner = services.GetRequiredService<AiAgentService>();
        probe.Armed = true;
        var parties = await owner.ListDiagnosticPartiesAsync();
        var bindings = await owner.ListDiagnosticBindingsAsync(parties.Select(item => item.Id).ToArray());
        probe.Armed = false;
        Assert.Equal(2, probe.Reads);
        Assert.True(parties.Count > PartyRecordQueryLimits.MaximumPageSize);
        Assert.Equal(legacyParties.Select(item => (item.Id, item.DisplayName)), parties.Select(item => (item.Id, item.DisplayName)));
        Assert.Contains(parties, item => item.Id == seeded.ArchivedSensitivePartyId);
        Assert.Contains(bindings, item => item.PartyId == seeded.MissingProjectionPartyId);
        Assert.DoesNotContain(parties, item => item.Id == seeded.PersonId);
        Assert.DoesNotContain(bindings, item => item.PartyId == seeded.PersonId || item.PartyId == seeded.OrphanPartyId);
        Assert.DoesNotContain(bindings, item => item.PartyId == seeded.PartyWithoutBindingId);
        Assert.Equal(Enum.GetValues<AiResourceBindingStatus>().Order(), bindings.Select(item => item.BindingStatus).Distinct().Order());

        using var jsonServices = new ServiceCollection().AddLogging()
            .Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options => {
                if (stringEnums) {
                    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
                }
            }).BuildServiceProvider();
        var originalResponse = await ExecuteOkAsync(jsonServices, new {
            PartyCount = legacyParties.Count,
            BindingCount = legacyBindings.Count,
            Parties = legacyParties,
            Bindings = legacyBindings.OrderBy(item => item.PartyId).ToArray()
        });
        var ownerResponse = await ExecuteOkAsync(jsonServices, new {
            PartyCount = parties.Count,
            BindingCount = bindings.Count,
            Parties = parties,
            Bindings = bindings.OrderBy(item => item.PartyId).ToArray()
        });
        Assert.Equal(originalResponse, ownerResponse);
        using var wire = JsonDocument.Parse(ownerResponse);
        Assert.Equal(["id", "displayName"], wire.RootElement.GetProperty("parties")[0].EnumerateObject().Select(property => property.Name));
        Assert.Equal(["partyId", "technicalAgentId", "bindingStatus", "bindingReason"],
            wire.RootElement.GetProperty("bindings")[0].EnumerateObject().Select(property => property.Name));
        Assert.Equal(await ExecuteOkAsync(jsonServices, new {
            Step = "parties", ElapsedMilliseconds = 17L, Count = legacyParties.Count, Parties = legacyParties
        }), await ExecuteOkAsync(jsonServices, new {
            Step = "parties", ElapsedMilliseconds = 17L, Count = parties.Count, Parties = parties
        }));
        Assert.Equal(legacyPartyIds, parties.Select(item => item.Id));
    }

    [Fact]
    public async Task Diagnostic_binding_query_preserves_empty_and_exact_requested_identity_semantics() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var seeded = await SeedPopulationAsync(services);
        var cursorBefore = await CursorSnapshotAsync(services);
        var owner = services.GetRequiredService<AiAgentService>();
        Assert.Empty(await owner.ListDiagnosticBindingsAsync([]));
        Assert.Empty(await owner.ListDiagnosticBindingsAsync([Guid.NewGuid()]));
        var selected = Assert.Single(await owner.ListDiagnosticBindingsAsync([
            seeded.MissingProjectionPartyId, seeded.MissingProjectionPartyId, seeded.PartyWithoutBindingId]));
        Assert.Equal(seeded.MissingProjectionPartyId, selected.PartyId);
        Assert.Equal("Original binding reason 001", selected.BindingReason);
        Assert.Null(selected.TechnicalAgentId);
        Assert.Equal(AiResourceBindingStatus.PendingBackfill, selected.BindingStatus);
        await using var context = await services.GetRequiredService<IDbContextFactory<CrmHrDbContext>>().CreateDbContextAsync();
        var stored = await context.Set<AiResourceBinding>().SingleAsync(item => item.PartyId == selected.PartyId);
        Assert.Equal(AiTechnicalProjectionAvailability.Missing, stored.ProjectionAvailability);
        Assert.Equal("Retained diagnostic-only error", stored.LastError);
        Assert.Equal(cursorBefore, await CursorSnapshotAsync(services));
    }

    [Fact]
    public async Task Diagnostics_use_their_immutable_owner_profile_and_observe_stored_values_after_restart() {
        await using var environment = CanDoItAllTestEnvironment.Create("crm-diagnostic-owner-profiles");
        var firstOptions = new TestHarnessOptions { TestEnvironment = environment, ActiveProfile = environment.CreatePostgreSqlProfile("first") };
        var secondOptions = new TestHarnessOptions { TestEnvironment = environment, ActiveProfile = environment.CreatePostgreSqlProfile("second") };
        var partyId = Guid.NewGuid();
        var firstTechnicalId = Guid.NewGuid();
        var secondTechnicalId = Guid.NewGuid();
        await using (var first = await TestApplication.CreateAsync(firstOptions)) {
            await SeedSingleAsync(first.Services, partyId, firstTechnicalId, "First profile", "First reason");
            await using var second = await TestApplication.CreateAsync(secondOptions);
            await SeedSingleAsync(second.Services, partyId, secondTechnicalId, "Second profile", "Second reason");
            await AssertProfileAsync(first.Services, partyId, firstTechnicalId, "First profile", "First reason");
            await AssertProfileAsync(second.Services, partyId, secondTechnicalId, "Second profile", "Second reason");
        }
        await using var restarted = await TestApplication.CreateAsync(firstOptions);
        await AssertProfileAsync(restarted.Services, partyId, firstTechnicalId, "First profile", "First reason");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Cancelled_diagnostic_query_preserves_cancellation_instead_of_returning_an_empty_result(bool bindings) {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var owner = scope.ServiceProvider.GetRequiredService<AiAgentService>();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var failure = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => {
            if (bindings) {
                await owner.ListDiagnosticBindingsAsync([Guid.NewGuid()], cancellation.Token);
            } else {
                await owner.ListDiagnosticPartiesAsync(cancellation.Token);
            }
        });
        Assert.Equal(cancellation.Token, failure.CancellationToken);
    }

    private static async Task<SeededPopulation> SeedPopulationAsync(IServiceProvider services) {
        await using var owner = await services.GetRequiredService<IDbContextFactory<CrmHrDbContext>>().CreateDbContextAsync();
        var parties = Enumerable.Range(0, 107).Select(index => new Party {
            PartyType = PartyType.AiAgent,
            DisplayName = index switch { 0 => "Žluťoučký archived", 1 => "alpha missing projection", 2 => "Beta bound", _ => $"Diagnostic {index:D3}" },
            LifecycleStatus = index == 0 ? PartyLifecycleStatus.Archived : PartyLifecycleStatus.Active,
            IsSensitive = index == 0,
            Notes = "Fixture private note excluded from the diagnostic contract",
            Summary = "Fixture summary excluded from the diagnostic contract"
        }).ToArray();
        owner.AddRange(parties);
        foreach (var (party, index) in parties.Select((party, index) => (party, index))) {
            if (index == parties.Length - 1) {
                continue;
            }
            owner.Add(new AiResourceBinding {
                PartyId = party.Id,
                TechnicalAgentId = index % 4 == 2 ? Guid.NewGuid() : null,
                BindingStatus = (AiResourceBindingStatus)(index % 4),
                BindingReason = $"Original binding reason {index:D3}",
                ProjectionAvailability = index == 1 ? AiTechnicalProjectionAvailability.Missing : AiTechnicalProjectionAvailability.Unknown,
                LastError = "Retained diagnostic-only error"
            });
        }
        var person = new Party { PartyType = PartyType.Person, DisplayName = "A person excluded from Agent diagnostics" };
        var orphanId = Guid.NewGuid();
        owner.Add(person);
        owner.AddRange(new AiResourceBinding { PartyId = person.Id, BindingReason = "Person binding excluded" },
            new AiResourceBinding { PartyId = orphanId, BindingReason = "Orphan binding excluded" });
        await owner.SaveChangesAsync();
        return new(parties[0].Id, parties[1].Id, parties[^1].Id, person.Id, orphanId);
    }

    private static async Task SeedSingleAsync(IServiceProvider services, Guid partyId, Guid technicalId, string name, string reason) {
        await using var owner = await services.GetRequiredService<IDbContextFactory<CrmHrDbContext>>().CreateDbContextAsync();
        owner.Add(new Party { Id = partyId, PartyType = PartyType.AiAgent, DisplayName = name, LifecycleStatus = PartyLifecycleStatus.Archived });
        owner.Add(new AiResourceBinding { PartyId = partyId, TechnicalAgentId = technicalId, BindingStatus = AiResourceBindingStatus.Bound, BindingReason = reason });
        await owner.SaveChangesAsync();
    }

    private static async Task AssertProfileAsync(IServiceProvider services, Guid partyId, Guid technicalId, string name, string reason) {
        await using var scope = services.CreateAsyncScope();
        var owner = scope.ServiceProvider.GetRequiredService<AiAgentService>();
        Assert.Equal(name, Assert.Single(await owner.ListDiagnosticPartiesAsync(), item => item.Id == partyId).DisplayName);
        var binding = Assert.Single(await owner.ListDiagnosticBindingsAsync([partyId]));
        Assert.Equal(technicalId, binding.TechnicalAgentId);
        Assert.Equal(AiResourceBindingStatus.Bound, binding.BindingStatus);
        Assert.Equal(reason, binding.BindingReason);
    }

    private static async Task<string> CursorSnapshotAsync(IServiceProvider services) {
        await using var context = await services.GetRequiredService<IDbContextFactory<CrmHrDbContext>>().CreateDbContextAsync();
        return JsonSerializer.Serialize(await context.Set<AiTechnicalProjectionCursor>().AsNoTracking()
            .OrderBy(item => item.DatabaseProfileId).ThenBy(item => item.SourceScopeKind).ThenBy(item => item.SourceScopeKey).ToArrayAsync());
    }

    private static async Task<string> ExecuteOkAsync(IServiceProvider services, object payload) {
        await using var response = new MemoryStream();
        var httpContext = new DefaultHttpContext { RequestServices = services };
        httpContext.Response.Body = response;
        await Results.Ok(payload).ExecuteAsync(httpContext);
        Assert.Equal(StatusCodes.Status200OK, httpContext.Response.StatusCode);
        response.Position = 0;
        using var reader = new StreamReader(response);
        return await reader.ReadToEndAsync();
    }

    private sealed record SeededPopulation(Guid ArchivedSensitivePartyId, Guid MissingProjectionPartyId,
        Guid PartyWithoutBindingId, Guid PersonId, Guid OrphanPartyId);

    private sealed class DiagnosticReadProbe : DbCommandInterceptor {
        public bool Armed { get; set; }
        public int Reads { get; private set; }
        public void Configure(IServiceCollection services) {
            services.AddSingleton<IDbContextFactory<CrmHrDbContext>>(provider =>
                new PooledDbContextFactory<CrmHrDbContext>(new DbContextOptionsBuilder<CrmHrDbContext>(
                    provider.GetRequiredService<DbContextOptions<CrmHrDbContext>>()).AddInterceptors(this).Options));
        }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) {
            if (Armed) {
                Assert.IsType<CrmHrDbContext>(eventData.Context);
                Assert.StartsWith("SELECT", command.CommandText.TrimStart(), StringComparison.OrdinalIgnoreCase);
                Reads++;
            }
            return ValueTask.FromResult(result);
        }
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default) {
            if (Armed) {
                throw new InvalidOperationException("A diagnostic read must not issue an owner mutation command.");
            }
            return ValueTask.FromResult(result);
        }
    }
}
