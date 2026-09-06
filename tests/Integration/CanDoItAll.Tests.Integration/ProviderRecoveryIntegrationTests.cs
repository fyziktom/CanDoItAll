using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework.ProviderManagement;
using CanDoItAll.Modules.Security;
using CanDoItAll.Web.Api;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using CanDoItAll.SharedProviders.Abstractions;
using Editor = CanDoItAll.AgentFramework.Models.ProviderProfileEditorModel;
using Entity = CanDoItAll.Modules.AgentFramework.ProviderManagement.ProviderProfile;
using Kind = CanDoItAll.AgentFramework.Models.ProviderKind;

namespace CanDoItAll.Tests.Integration.SharedProviders;

public sealed class ProviderRecoveryIntegrationTests(ProviderRecoveryFixture fixture) : IClassFixture<ProviderRecoveryFixture> {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Canonical_candidate_verification_distinguishes_committed_and_absent(bool committed) {
        await using var scope = fixture.Host.App.Services.CreateAsyncScope();
        var registry = scope.ServiceProvider.GetRequiredService<IProviderProfileRegistry>();
        var model = NewEditor();
        fixture.Fault.ArmProvider(model.Name, committed);
        var error = await Assert.ThrowsAsync<ProviderMutationUnconfirmedException>(() => registry.SaveProviderAsync(model));
        Assert.NotEqual(Guid.Empty, error.Attempt.ProviderId);
        var verification = scope.ServiceProvider.GetRequiredService<IProviderMutationVerification>();
        var result = await verification.VerifyAsync(error.Attempt);
        Assert.Equal(committed ? ProviderVerificationDisposition.Committed : ProviderVerificationDisposition.DefinitelyNotCommitted, result.Disposition);
        Assert.Equal(error.Attempt.ProviderId, result.ProviderId);
        await using var db = await fixture.Factory.CreateDbContextAsync();
        Assert.Equal(committed ? 1 : 0, await db.Set<Entity>().CountAsync(row => row.Id == result.ProviderId));
    }

    [Fact]
    public async Task Repeated_controlled_retry_cannot_create_second_provider() {
        await using var scope = fixture.Host.App.Services.CreateAsyncScope();
        var registry = scope.ServiceProvider.GetRequiredService<IProviderProfileRegistry>();
        var model = NewEditor();
        model.Id = Guid.NewGuid();
        model.ExpectedConcurrencyToken = Guid.Empty;
        fixture.Fault.ArmProvider(model.Name, afterCommit: false);
        var unknown = await Assert.ThrowsAsync<ProviderMutationUnconfirmedException>(() => registry.SaveProviderAsync(model));
        var result = await scope.ServiceProvider.GetRequiredService<IProviderMutationVerification>().VerifyAsync(unknown.Attempt);
        Assert.Equal(ProviderVerificationDisposition.DefinitelyNotCommitted, result.Disposition);
        var id = await registry.SaveProviderAsync(model);
        Assert.Equal(model.Id, id);
        await Assert.ThrowsAsync<ProviderProfileConcurrencyException>(() => registry.SaveProviderAsync(model));
        await using var db = await fixture.Factory.CreateDbContextAsync();
        Assert.Single(await db.Set<Entity>().Where(row => row.Name == model.Name).ToArrayAsync());
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Update_and_delete_verify_identity_and_revision_without_replay(bool delete, bool committed) {
        await using var scope = fixture.Host.App.Services.CreateAsyncScope();
        var registry = scope.ServiceProvider.GetRequiredService<IProviderProfileRegistry>();
        var id = await registry.SaveProviderAsync(NewEditor());
        var update = await registry.GetProviderEditorAsync(id);
        var before = update.ExpectedConcurrencyToken;
        update.Name += " changed";
        fixture.Fault.ArmProvider(delete ? null : update.Name, committed, delete ? id : null);
        var unknown = await Assert.ThrowsAsync<ProviderMutationUnconfirmedException>(async () => {
            if (delete) {
                await registry.DeleteProviderAsync(id);
            } else {
                await registry.SaveProviderAsync(update);
            }
        });
        Assert.Equal(id, unknown.Attempt.ProviderId);
        Assert.Equal(before, unknown.Attempt.ExpectedConcurrencyToken);
        var verification = scope.ServiceProvider.GetRequiredService<IProviderMutationVerification>();
        var outcome = await verification.VerifyAsync(unknown.Attempt);
        Assert.Equal(committed ? ProviderVerificationDisposition.Committed : ProviderVerificationDisposition.DefinitelyNotCommitted, outcome.Disposition);
        var repeated = await verification.VerifyAsync(unknown.Attempt);
        Assert.Equal(outcome, repeated);
        await using var db = await fixture.Factory.CreateDbContextAsync();
        var row = await db.Set<Entity>().AsNoTracking().SingleOrDefaultAsync(provider => provider.Id == id);
        Assert.Equal(delete && committed, row is null);
        if (!delete && committed) {
            Assert.Equal(unknown.Attempt.IntendedConcurrencyToken, row!.ConcurrencyToken);
            row.Name += " intervening writer";
            db.Update(row);
            await db.SaveChangesAsync();
            Assert.Equal(ProviderVerificationDisposition.StillUnconfirmed, (await verification.VerifyAsync(unknown.Attempt)).Disposition);
        }
    }

    [Fact]
    public async Task Api_unconfirmed_write_returns_identity_and_non_retry_contract() {
        var model = NewEditor();
        fixture.Fault.ArmProvider(model.Name, afterCommit: true);
        var response = await fixture.Host.Client.PostAsJsonAsync("/api/agents/providers", model);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("unconfirmed-verification-required", Assert.Single(response.Headers.GetValues("CDA-Provider-Outcome")));
        Assert.Null(response.Headers.RetryAfter);
        var receipt = (await response.Content.ReadFromJsonAsync<ProviderUnconfirmedApiResponse>())!;
        Assert.False(receipt.AutomaticReplaySafe);
        Assert.NotEqual(Guid.Empty, receipt.ProviderId);
        Assert.Equal(receipt.ProviderId, receipt.Attempt.ProviderId);
        Assert.Equal("agents.provider-write-unconfirmed", receipt.Code);
        var check = await fixture.Host.Client.PostAsJsonAsync(receipt.VerificationPath, receipt.Attempt);
        Assert.Equal(HttpStatusCode.OK, check.StatusCode);
        var verified = (await check.Content.ReadFromJsonAsync<ProviderVerificationApiResponse>())!;
        Assert.Equal(ProviderVerificationDisposition.Committed, verified.Outcome);
        using var wire = System.Text.Json.JsonDocument.Parse(await check.Content.ReadAsStringAsync());
        Assert.Equal("Committed", wire.RootElement.GetProperty("outcome").GetString());
        Assert.Equal(receipt.ProviderId, verified.ProviderId);
        Assert.False(verified.AutomaticReplaySafe);
        await using var db = await fixture.Factory.CreateDbContextAsync();
        Assert.Single(await db.Set<Entity>().Where(row => row.Name == model.Name).ToArrayAsync());
    }

    [Fact]
    public async Task Api_unconfirmed_body_is_sanitized() {
        var model = NewEditor();
        fixture.Fault.ArmProvider(model.Name, afterCommit: false);
        var response = await fixture.Host.Client.PostAsJsonAsync("/api/agents/providers", model);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        foreach (var forbidden in new[] { "fixture-private-fault", "upstream.example.test", "credential-value", "secret-id" }) {
            Assert.DoesNotContain(forbidden, body, StringComparison.Ordinal);
        }
        Assert.DoesNotContain(model.BaseUrl, body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Source_create_has_stable_proposed_identity() {
        await using var scope = fixture.Host.App.Services.CreateAsyncScope();
        var management = scope.ServiceProvider.GetRequiredService<ISharedProviderManagementService>();
        var request = await SourceRequestAsync();
        fixture.Fault.SourceId = request.Id;
        var attempt = new SharedProviderSourceMutationAttempt(request.Id!.Value, SharedProviderSourceMutationKind.Create, request: request);
        await Assert.ThrowsAnyAsync<Exception>(() => management.SaveSourceAsync(request));
        var verification = await management.VerifySourceAsync(attempt);
        Assert.Equal(ProviderVerificationDisposition.Committed, verification.Disposition);
        Assert.Equal(request.Id, verification.SourceId);
        var repeated = await management.SaveSourceAsync(request);
        Assert.Equal(request.Id, repeated.Id);
        await Assert.ThrowsAsync<SharedProviderConcurrencyException>(() => management.SaveSourceAsync(request with { Name = "Different immutable request" }));
        await using var db = await fixture.Factory.CreateDbContextAsync();
        Assert.Single(await db.Set<SharedProviderSource>().Where(row => row.Id == request.Id).ToArrayAsync());
    }

    [Theory]
    [InlineData(SharedProviderSourceMutationKind.Update)]
    [InlineData(SharedProviderSourceMutationKind.Enablement)]
    [InlineData(SharedProviderSourceMutationKind.Delete)]
    public async Task Source_existing_operation_verifies_exact_canonical_postcondition(SharedProviderSourceMutationKind kind) {
        await using var scope = fixture.Host.App.Services.CreateAsyncScope();
        var management = scope.ServiceProvider.GetRequiredService<ISharedProviderManagementService>();
        var request = await SourceRequestAsync();
        var first = await management.SaveSourceAsync(request);
        var before = (await management.ListSourcesAsync()).Single(source => source.Source.Id == first.Id);
        var update = request with { ExpectedConcurrencyToken = first.ConcurrencyToken, Name = request.Name + " changed" };
        var attempt = new SharedProviderSourceMutationAttempt(first.Id, kind, before,
            kind == SharedProviderSourceMutationKind.Update ? update : null, intendedEnabled: false);
        fixture.Fault.SourceId = first.Id;
        await Assert.ThrowsAnyAsync<Exception>(async () => {
            if (kind == SharedProviderSourceMutationKind.Update) {
                await management.SaveSourceAsync(update);
            } else if (kind == SharedProviderSourceMutationKind.Enablement) {
                await management.SetSourceEnabledAsync(first.Id, first.ConcurrencyToken, false);
            } else {
                await management.DeleteSourceAsync(first.Id, first.ConcurrencyToken);
            }
        });
        var result = await management.VerifySourceAsync(attempt);
        Assert.Equal(ProviderVerificationDisposition.Committed, result.Disposition);
        Assert.NotNull(result.Change);
        var second = await management.VerifySourceAsync(attempt);
        Assert.Equal(result.Disposition, second.Disposition);
        Assert.Equal(result.Sources.SingleOrDefault(source => source.Source.Id == first.Id)?.Source.ConcurrencyToken,
            second.Sources.SingleOrDefault(source => source.Source.Id == first.Id)?.Source.ConcurrencyToken);
    }

    [Fact]
    public async Task Publication_conflict_explains_permanent_identity() {
        await using var scope = fixture.Host.App.Services.CreateAsyncScope();
        var registry = scope.ServiceProvider.GetRequiredService<IProviderProfileRegistry>();
        var sharing = scope.ServiceProvider.GetRequiredService<ISharedProviderManagementService>();
        var id = await registry.SaveProviderAsync(NewEditor());
        var published = await sharing.SetPublicationAsync(id, SharedProviderPublicationAction.Publish, null);
        await sharing.SetPublicationAsync(id, SharedProviderPublicationAction.Unpublish, published.Publication!.ConcurrencyToken);
        var response = await fixture.Host.Client.DeleteAsync($"/api/agents/providers/{id:D}");
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Unpublish only stops sharing", body, StringComparison.Ordinal);
        Assert.Contains("permanent publication identity", body, StringComparison.Ordinal);
        Assert.DoesNotContain("Unpublish or retire", body, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Import_and_source_conflicts_have_specific_remediation(bool hasPublication) {
        await using var scope = fixture.Host.App.Services.CreateAsyncScope();
        var registry = scope.ServiceProvider.GetRequiredService<IProviderProfileRegistry>();
        var management = scope.ServiceProvider.GetRequiredService<ISharedProviderManagementService>();
        var providerId = await registry.SaveProviderAsync(NewEditor());
        if (hasPublication) {
            await management.SetPublicationAsync(providerId, SharedProviderPublicationAction.Publish, null);
        }
        var request = await SourceRequestAsync();
        var source = await management.SaveSourceAsync(request);
        var publicationId = new SharedProviderPublicationId(Guid.NewGuid());
        await using (var db = await fixture.Factory.CreateDbContextAsync()) {
            db.Add(new SharedProviderImport {
                SourceId = source.Id, ProviderProfileId = providerId,
                RemotePublicationId = publicationId, RemoteDisplayName = "Recovery import",
                RemoteRevision = new SharedProviderPublicRevision($"sha256:{new string('a', 64)}"),
                RemotePurpose = SharedProviderPurpose.Chat, RemoteTransport = SharedProviderTransport.OpenAiCompatible,
                RemoteDefaultModelId = SharedProviderRoutingModelIdCodec.Create(publicationId, "model"),
                RemoteCatalogSnapshotJson = "{}", SelectionState = SharedProviderSelectionState.Retired,
                AvailabilityState = SharedProviderAvailabilityState.Available,
                CreatedAtUtc = DateTimeOffset.UtcNow, UpdatedAtUtc = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
        }
        var blocked = await fixture.Host.Client.DeleteAsync($"/api/agents/providers/{providerId:D}");
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        var body = await blocked.Content.ReadAsStringAsync();
        Assert.Contains("audit identity remains", body, StringComparison.Ordinal);
        Assert.Contains(hasPublication ? "Retire the import" : "Retire the imported provider", body, StringComparison.Ordinal);
        Assert.Equal(hasPublication, body.Contains("Unpublish only stops sharing", StringComparison.Ordinal));
        var sourceBlocked = await fixture.Host.Client.DeleteAsync($"/fixture/recovery/sources/{source.Id:D}/{source.ConcurrencyToken:D}");
        Assert.Equal(HttpStatusCode.Conflict, sourceBlocked.StatusCode);
        var sourceBody = await sourceBlocked.Content.ReadAsStringAsync();
        Assert.Contains("Retire or migrate", sourceBody, StringComparison.Ordinal);
        Assert.DoesNotContain("Unpublish", sourceBody, StringComparison.Ordinal);
        Assert.DoesNotContain(request.BaseUri.Host, sourceBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Canonical_read_failure_is_not_evidence_of_absence() {
        await using var scope = fixture.Host.App.Services.CreateAsyncScope();
        fixture.Fault.FailRead = true;
        var attempt = new ProviderMutationAttempt(Guid.NewGuid(), Guid.NewGuid(), ProviderMutationKind.Create, Guid.Empty);
        var result = await scope.ServiceProvider.GetRequiredService<IProviderMutationVerification>().VerifyAsync(attempt);
        Assert.Equal(ProviderVerificationDisposition.StillUnconfirmed, result.Disposition);
        Assert.Equal(attempt.ProviderId, result.ProviderId);
    }

    private async Task<SharedProviderSourceEditorRequest> SourceRequestAsync() {
        var secret = new SecretRecord {
            Name = "Recovery source credential metadata", Kind = SecretKind.Token, Scope = "workspace",
            MetadataJson = "{}", CreatedAtUtc = DateTimeOffset.UtcNow, UpdatedAtUtc = DateTimeOffset.UtcNow
        };
        await using var db = await fixture.Factory.CreateDbContextAsync();
        db.Add(secret);
        await db.SaveChangesAsync();
        return new(Guid.NewGuid(), null, "Recovery source " + Guid.NewGuid().ToString("N"),
            new Uri("https://source.example.test/"), secret.Id, true, false);
    }

    private static Editor NewEditor() => new() {
        Name = "Recovery provider " + Guid.NewGuid().ToString("N"), Kind = Kind.Ollama,
        Transport = CanDoItAll.AgentFramework.Models.ProviderTransportKind.ChatCompletions,
        BaseUrl = "http://127.0.0.1:11434", DefaultModel = "recovery-model", SuggestedModels = ["recovery-model"], IsEnabled = true
    };
}

public sealed class ProviderRecoveryFixture : IAsyncLifetime {
    internal ApiTestHost Host { get; private set; } = null!;
    internal RecoveryFault Fault { get; } = new();
    internal IDbContextFactory<AppDbContext> Factory => Host.App.Services.GetRequiredService<IDbContextFactory<AppDbContext>>();

    public async Task InitializeAsync() {
        Host = await ApiTestHost.CreateAsync(jwtEnabled: false, configureServices: services => {
            services.AddSingleton<IDbContextFactory<AppDbContext>>(provider => new RecoveryFactory(
                new DbContextOptionsBuilder<AppDbContext>(provider.GetRequiredService<DbContextOptions<AppDbContext>>())
                    .AddInterceptors(new RecoverySaveInterceptor(Fault), new RecoveryTransactionInterceptor(Fault)).Options, Fault));
        }, configureApplication: app => app.MapDelete("/fixture/recovery/sources/{id:guid}/{token:guid}",
            (HttpContext context, Guid id, Guid token, ISharedProviderManagementService management) =>
                ProviderApiResults.ExecuteAsync(context, async () => {
                    await management.DeleteSourceAsync(id, token, context.RequestAborted);
                    return Results.NoContent();
                })));
    }

    public Task DisposeAsync() => Host.DisposeAsync().AsTask();

    private sealed class RecoveryFactory(DbContextOptions<AppDbContext> options, RecoveryFault fault) : IDbContextFactory<AppDbContext> {
        public AppDbContext CreateDbContext() => new(options);
        public Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) {
            if (fault.FailRead) {
                fault.FailRead = false;
                throw RecoveryFault.Failure();
            }
            return Task.FromResult(new AppDbContext(options));
        }
    }

    internal sealed class RecoveryFault {
        public bool FailRead { get; set; }
        public string? ProviderName { get; private set; }
        public Guid? ProviderId { get; private set; }
        public Guid? SourceId { get; set; }
        public Guid? SourceContext { get; set; }
        public bool AfterCommit { get; private set; }
        public bool Armed { get; set; }
        public void ArmProvider(string? name, bool afterCommit, Guid? providerId = null) {
            ProviderName = name;
            ProviderId = providerId;
            AfterCommit = afterCommit;
            Armed = true;
        }
        public static IOException Failure() => new("fixture-private-fault https://upstream.example.test/ credential-value secret-id");
    }

    private sealed class RecoverySaveInterceptor(RecoveryFault fault) : SaveChangesInterceptor {
        private readonly HashSet<Guid> pending = [];
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default) {
            var db = eventData.Context!;
            if (fault.SourceId is { } sourceId && db.ChangeTracker.Entries<SharedProviderSource>().Any(entry => entry.Entity.Id == sourceId)) {
                fault.SourceContext = db.ContextId.InstanceId;
            }
            if (fault.Armed && db.ChangeTracker.Entries<Entity>().Any(entry =>
                    entry.Entity.Name == fault.ProviderName || entry.Entity.Id == fault.ProviderId)) {
                fault.Armed = false;
                if (!fault.AfterCommit) {
                    throw RecoveryFault.Failure();
                }
                pending.Add(db.ContextId.InstanceId);
            }
            return ValueTask.FromResult(result);
        }
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
            CancellationToken cancellationToken = default) {
            if (pending.Remove(eventData.Context!.ContextId.InstanceId)) {
                throw RecoveryFault.Failure();
            }
            return ValueTask.FromResult(result);
        }
    }

    private sealed class RecoveryTransactionInterceptor(RecoveryFault fault) : DbTransactionInterceptor {
        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData,
            CancellationToken cancellationToken = default) {
            if (eventData.Context is { } db && fault.SourceContext == db.ContextId.InstanceId) {
                fault.SourceId = null;
                fault.SourceContext = null;
                throw RecoveryFault.Failure();
            }
            return Task.CompletedTask;
        }
    }
}
