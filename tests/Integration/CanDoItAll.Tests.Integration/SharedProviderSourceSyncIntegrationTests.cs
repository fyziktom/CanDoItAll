using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using CanDoItAll.Composition;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework.ProviderManagement;
using CanDoItAll.Modules.Security;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.Security.Abstractions;
using CanDoItAll.SharedKernel;
using CanDoItAll.SharedProviders.Abstractions;
using CanDoItAll.SharedProviders.Http;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Tests.Integration.SharedProviders;

public sealed class SharedProviderSourceSyncIntegrationTests
{
    private const string SourceToken = "source-token_ABC-123";
    private const string AccessContextValue = "client-session-42";
    private const string PrivateUpstreamValue = "https://private-upstream.example.internal/v1";
    private static readonly DateTimeOffset Now = new(2026, 8, 25, 6, 0, 0, TimeSpan.Zero);
    private static readonly SharedProviderSourceInstanceId PrimaryInstanceId = new(
        Guid.Parse("22222222-2222-4222-8222-222222222222"));
    private static readonly SharedProviderSourceInstanceId ReplacementInstanceId = new(
        Guid.Parse("99999999-9999-4999-8999-999999999999"));
    private static readonly SharedProviderPublicationId PrimaryPublicationId = new(
        Guid.Parse("11111111-1111-4111-8111-111111111111"));
    private static readonly SharedProviderPublicationId SecondaryPublicationId = new(
        Guid.Parse("33333333-3333-4333-8333-333333333333"));

    [Fact]
    public async Task Source_crud_and_connection_test_use_real_postgresql_secret_and_http_services()
    {
        await using var harness = await SourceSyncHarness.CreateAsync("sp-sync-crud");
        var source = await harness.CreateSourceAsync(
            new Uri("https://central.example.test/tenant/client"));
        var created = await harness.Sources.GetAsync(source.Id);

        Assert.Equal(source.SecretId, created.ApiTokenSecretId);
        Assert.Equal("https://central.example.test/tenant/client/", created.BaseUri.AbsoluteUri);
        Assert.Equal(created, Assert.Single(await harness.Sources.ListAsync()));

        var disabled = await harness.Sources.SetEnabledAsync(
            source.Id,
            created.ConcurrencyToken,
            isEnabled: false);
        var disabledSync = await harness.Sync.SynchronizeAsync(
            source.Id,
            PublicationSet(PrimaryPublicationId));
        Assert.Equal(SharedProviderSourceOperationOutcome.SourceDisabled, disabledSync.Outcome);
        Assert.Empty(harness.Http.Requests);
        await harness.Sources.SetEnabledAsync(
            source.Id,
            disabled.ConcurrencyToken,
            isEnabled: true);
        var catalog = CreateCatalog();
        harness.Http.EnqueueCatalog(catalog);
        var reenabledSync = await harness.Sync.SynchronizeAsync(
            source.Id,
            PublicationSet());
        Assert.Equal(SharedProviderSourceOperationOutcome.Succeeded, reenabledSync.Outcome);
        Assert.Single(harness.Http.Requests);
        var reenabledSource = await harness.Sources.GetAsync(source.Id);

        var updated = await harness.Sources.UpdateAsync(
            source.Id,
            reenabledSource.ConcurrencyToken,
            new SharedProviderSourceWriteRequest(
                "Renamed central source",
                created.BaseUri,
                source.SecretId,
                IsEnabled: true,
                AllowInsecurePrivateNetwork: false));
        harness.Http.EnqueueCatalog(catalog);

        var tested = await harness.Sync.TestAsync(source.Id);

        Assert.Equal(SharedProviderSourceOperationOutcome.Succeeded, tested.Outcome);
        Assert.Equal(catalog.SourceInstanceId, tested.Catalog!.SourceInstanceId);
        var testedSource = await harness.Sources.GetAsync(source.Id);
        Assert.Equal("Renamed central source", testedSource.Name);
        Assert.NotEqual(updated.ConcurrencyToken, testedSource.ConcurrencyToken);

        var deleted = await harness.Sources.DeleteAsync(
            source.Id,
            testedSource.ConcurrencyToken);

        Assert.Equal(source.Id, deleted.Id);
        Assert.Empty(await harness.Sources.ListAsync());
    }

    [Fact]
    public async Task Catalog_request_preserves_canonical_reverse_proxy_base_path()
    {
        await using var harness = await SourceSyncHarness.CreateAsync("sp-sync-path");
        var source = await harness.CreateSourceAsync(
            new Uri("https://central.example.test/reverse/proxy/root"));
        harness.Http.EnqueueCatalog(CreateCatalog());

        var result = await harness.Sync.TestAsync(source.Id);

        Assert.Equal(SharedProviderSourceOperationOutcome.Succeeded, result.Outcome);
        var request = Assert.Single(harness.Http.Requests);
        Assert.Equal(
            "https://central.example.test/reverse/proxy/root/api/shared-providers/v1/catalog",
            request.RequestUri.AbsoluteUri);
        Assert.Equal(HttpMethod.Get, request.Method);
    }

    [Fact]
    public async Task Catalog_request_propagates_bearer_and_access_context_without_unrelated_metadata()
    {
        await using var harness = await SourceSyncHarness.CreateAsync("sp-sync-headers");
        var source = await harness.CreateSourceAsync(token: SourceToken);
        harness.Http.EnqueueCatalog(CreateCatalog());
        var accessContext = new AccessContextReference(AccessContextValue);
        harness.AccessContext.Current = accessContext;
        harness.AccessContext.CurrentType = AccessContextReferenceTypes.Project;

        var result = await harness.Sync.TestAsync(source.Id);

        Assert.Equal(SharedProviderSourceOperationOutcome.Succeeded, result.Outcome);
        var request = Assert.Single(harness.Http.Requests);
        Assert.Equal("Bearer", request.AuthorizationScheme);
        Assert.Equal(SourceToken, request.AuthorizationParameter);
        Assert.Equal(
            AccessContextValue,
            Assert.Single(request.GetHeaderValues(SharedProviderHeaders.AccessContextReference)));
        Assert.Equal(
            AccessContextReferenceTypes.Project.Value,
            Assert.Single(request.GetHeaderValues(SharedProviderHeaders.AccessContextReferenceType)));
        Assert.DoesNotContain(SourceToken, request.RequestUri.AbsoluteUri, StringComparison.Ordinal);
        Assert.Null(request.Content);
        Assert.DoesNotContain(
            request.Headers.Keys,
            name => name.Contains("subject", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("provider-profile", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("upstream", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("api-key", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Conditional_get_not_modified_is_a_true_persistence_and_observer_no_op()
    {
        await using var harness = await SourceSyncHarness.CreateAsync("sp-sync-not-modified");
        var source = await harness.CreateSourceAsync();
        var catalog = CreateCatalog();
        harness.Http.EnqueueCatalog(catalog);
        await harness.Sync.SynchronizeAsync(source.Id, PublicationSet(PrimaryPublicationId));
        var before = await harness.LoadStateAsync(source.Id);
        harness.Observer.Clear();
        harness.Http.EnqueueNotModified(EntityTag(catalog));

        var result = await harness.Sync.SynchronizeAsync(
            source.Id,
            PublicationSet(PrimaryPublicationId));

        var after = await harness.LoadStateAsync(source.Id);
        Assert.Equal(SharedProviderSourceOperationOutcome.NotModified, result.Outcome);
        Assert.Equal(before.Source, after.Source);
        Assert.Equal(before.Imports.ToArray(), after.Imports.ToArray());
        Assert.Equal(before.Profiles.ToArray(), after.Profiles.ToArray());
        Assert.Empty(harness.Observer.SavedProviderIds);
        var request = harness.Http.Requests[^1];
        Assert.Equal(
            EntityTag(catalog).Value,
            Assert.Single(request.GetHeaderValues("If-None-Match")));
    }

    [Fact]
    public async Task First_successful_catalog_pins_remote_source_identity()
    {
        await using var harness = await SourceSyncHarness.CreateAsync("sp-sync-pin");
        var source = await harness.CreateSourceAsync();
        Assert.Null((await harness.Sources.GetAsync(source.Id)).RemoteInstanceId);
        var catalog = CreateCatalog(sourceInstanceId: PrimaryInstanceId);
        harness.Http.EnqueueCatalog(catalog);

        var result = await harness.Sync.SynchronizeAsync(
            source.Id,
            PublicationSet(PrimaryPublicationId));

        var persisted = await harness.Sources.GetAsync(source.Id);
        Assert.Equal(SharedProviderSourceOperationOutcome.Succeeded, result.Outcome);
        Assert.Equal(PrimaryInstanceId, persisted.RemoteInstanceId);
        Assert.Equal(EntityTag(catalog), persisted.LastCatalogETag);
        Assert.Equal(SharedProviderSourceStatus.Available, persisted.Status);
    }

    [Fact]
    public async Task Remote_identity_mismatch_blocks_reconciliation_and_preserves_remote_snapshot()
    {
        await using var harness = await SourceSyncHarness.CreateAsync("sp-sync-mismatch");
        var source = await harness.CreateSourceAsync();
        var trustedCatalog = CreateCatalog(sourceInstanceId: PrimaryInstanceId);
        harness.Http.EnqueueCatalog(trustedCatalog);
        await harness.Sync.SynchronizeAsync(source.Id, PublicationSet(PrimaryPublicationId));
        var before = await harness.LoadStateAsync(source.Id);
        harness.Http.EnqueueCatalog(CreateCatalog(
            displayName: "Forged replacement",
            sourceInstanceId: ReplacementInstanceId));

        var result = await harness.Sync.SynchronizeAsync(
            source.Id,
            PublicationSet(PrimaryPublicationId));

        var after = await harness.LoadStateAsync(source.Id);
        Assert.Equal(SharedProviderSourceOperationOutcome.SourceIdentityMismatch, result.Outcome);
        Assert.Equal(SharedProviderFailureCategory.Conflict, result.Failure!.Category);
        Assert.Equal(PrimaryInstanceId, after.Source.RemoteInstanceId);
        Assert.Equal(SharedProviderSourceStatus.SourceIdentityMismatch, after.Source.Status);
        Assert.Equal(before.Imports.Single().RemoteRevision, after.Imports.Single().RemoteRevision);
        Assert.Equal(
            SharedProviderAvailabilityState.SourceIdentityMismatch,
            after.Imports.Single().AvailabilityState);
        Assert.Equal(before.Profiles.Single().Id, after.Profiles.Single().Id);
    }

    [Fact]
    public async Task Explicit_identity_reset_allows_operator_to_trust_replacement_source()
    {
        await using var harness = await SourceSyncHarness.CreateAsync("sp-sync-reset");
        var source = await harness.CreateSourceAsync();
        var trustedCatalog = CreateCatalog(sourceInstanceId: PrimaryInstanceId);
        harness.Http.EnqueueCatalog(trustedCatalog);
        await harness.Sync.SynchronizeAsync(source.Id, PublicationSet(PrimaryPublicationId));
        var original = await harness.LoadStateAsync(source.Id);
        var replacementCatalog = CreateCatalog(
            displayName: "Replacement source provider",
            sourceInstanceId: ReplacementInstanceId);
        harness.Http.EnqueueCatalog(replacementCatalog);
        await harness.Sync.SynchronizeAsync(source.Id, PublicationSet(PrimaryPublicationId));
        var mismatched = await harness.Sources.GetAsync(source.Id);

        await harness.Sources.ResetTrustedIdentityAsync(
            source.Id,
            mismatched.ConcurrencyToken);
        harness.Http.EnqueueCatalog(replacementCatalog);
        var result = await harness.Sync.SynchronizeAsync(
            source.Id,
            PublicationSet(PrimaryPublicationId));

        var after = await harness.LoadStateAsync(source.Id);
        Assert.Equal(SharedProviderSourceOperationOutcome.Succeeded, result.Outcome);
        Assert.Equal(ReplacementInstanceId, after.Source.RemoteInstanceId);
        Assert.Equal(SharedProviderAvailabilityState.Available, after.Imports.Single().AvailabilityState);
        Assert.Equal(original.Imports.Single().Id, after.Imports.Single().Id);
        Assert.Equal(original.Profiles.Single().Id, after.Profiles.Single().Id);
    }

    [Fact]
    public async Task Selecting_multiple_publications_creates_profiles_with_one_source_credential_reference()
    {
        await using var harness = await SourceSyncHarness.CreateAsync("sp-sync-multi-select");
        var source = await harness.CreateSourceAsync();
        var catalog = CreateCatalogWithTwoPublications();
        harness.Http.EnqueueCatalog(catalog);

        var result = await harness.Sync.SynchronizeAsync(
            source.Id,
            PublicationSet(PrimaryPublicationId, SecondaryPublicationId));

        await using var dbContext = harness.Factory.CreateDbContext();
        var imports = await dbContext.Set<SharedProviderImport>()
            .OrderBy(importItem => importItem.RemotePublicationId)
            .ToArrayAsync();
        var profiles = await dbContext.Set<ProviderProfile>()
            .OrderBy(profile => profile.Id)
            .ToArrayAsync();
        Assert.Equal(SharedProviderSourceOperationOutcome.Succeeded, result.Outcome);
        Assert.Equal(2, imports.Length);
        Assert.Equal(2, profiles.Length);
        Assert.All(imports, importItem => Assert.Equal(source.Id, importItem.SourceId));
        Assert.All(profiles, profile => Assert.Equal((Guid?)source.SecretId, profile.ApiKeySecretId));
        Assert.Equal(1, await dbContext.Set<SecretRecord>().CountAsync());
        Assert.Equal(source.SecretId, (await harness.Sources.GetAsync(source.Id)).ApiTokenSecretId);

        var secondaryImport = imports.Single(importItem =>
            importItem.RemotePublicationId == SecondaryPublicationId);
        harness.Http.EnqueueCatalog(catalog);
        var replacement = await harness.Sync.SynchronizeAsync(
            source.Id,
            PublicationSet(PrimaryPublicationId));

        var afterReplacement = await harness.LoadStateAsync(source.Id);
        var retiredImport = afterReplacement.Imports.Single(importItem =>
            importItem.RemotePublicationId == SecondaryPublicationId);
        Assert.Equal(SharedProviderSourceOperationOutcome.Succeeded, replacement.Outcome);
        Assert.Equal(2, afterReplacement.Imports.Count);
        Assert.Equal(2, afterReplacement.Profiles.Count);
        Assert.Equal(secondaryImport.Id, retiredImport.Id);
        Assert.Equal(secondaryImport.ProviderProfileId, retiredImport.ProviderProfileId);
        Assert.Equal(SharedProviderSelectionState.Retired, retiredImport.SelectionState);
    }

    [Fact]
    public async Task Repeated_successful_sync_is_idempotent_and_creates_no_duplicates()
    {
        await using var harness = await SourceSyncHarness.CreateAsync("sp-sync-idempotent");
        var source = await harness.CreateSourceAsync();
        var catalog = CreateCatalog();
        harness.Http.EnqueueCatalog(catalog);
        await harness.Sync.SynchronizeAsync(source.Id, PublicationSet(PrimaryPublicationId));
        var before = await harness.LoadStateAsync(source.Id);
        harness.Http.EnqueueCatalog(catalog);

        var result = await harness.Sync.SynchronizeAsync(
            source.Id,
            PublicationSet(PrimaryPublicationId));

        var after = await harness.LoadStateAsync(source.Id);
        Assert.Equal(SharedProviderSourceOperationOutcome.Succeeded, result.Outcome);
        Assert.Single(after.Imports);
        Assert.Single(after.Profiles);
        Assert.Equal(before.Imports.Single().Id, after.Imports.Single().Id);
        Assert.Equal(before.Profiles.Single().Id, after.Profiles.Single().Id);
    }

    [Fact]
    public async Task Remote_refresh_preserves_local_profile_identity_alias_and_enabled_intent()
    {
        await using var harness = await SourceSyncHarness.CreateAsync("sp-sync-local-intent");
        var source = await harness.CreateSourceAsync();
        var originalCatalog = CreateCatalog(displayName: "Remote original");
        harness.Http.EnqueueCatalog(originalCatalog);
        await harness.Sync.SynchronizeAsync(source.Id, PublicationSet(PrimaryPublicationId));
        var before = await harness.LoadStateAsync(source.Id);
        await using (var editContext = harness.Factory.CreateDbContext())
        {
            var profile = await editContext.Set<ProviderProfile>().SingleAsync();
            profile.Name = "Local operator alias";
            profile.IsEnabled = false;
            await editContext.SaveChangesAsync();
        }

        var updatedCatalog = CreateCatalog(
            displayName: "Remote renamed",
            upstreamModelId: "upstream-revised",
            capabilities:
            [
                SharedProviderCapability.Responses,
                SharedProviderCapability.FunctionTools,
                SharedProviderCapability.StructuredOutput,
                SharedProviderCapability.VisionInput
            ]);
        harness.Http.EnqueueCatalog(updatedCatalog);
        await harness.Sync.SynchronizeAsync(source.Id, PublicationSet(PrimaryPublicationId));

        var after = await harness.LoadStateAsync(source.Id);
        Assert.Equal(before.Imports.Single().Id, after.Imports.Single().Id);
        Assert.Equal(before.Profiles.Single().Id, after.Profiles.Single().Id);
        Assert.Equal("Remote renamed", after.Imports.Single().RemoteDisplayName);
        Assert.Equal("Local operator alias", after.Profiles.Single().Name);
        Assert.False(after.Profiles.Single().IsEnabled);
        Assert.Equal(
            updatedCatalog.Providers.Single().DefaultModelId.Value,
            after.Profiles.Single().DefaultModel);
        Assert.False(after.Profiles.Single().SupportsStreaming);
        Assert.True(after.Profiles.Single().SupportsToolCalling);
        Assert.True(after.Profiles.Single().SupportsStructuredOutput);
        Assert.True(after.Profiles.Single().SupportsVision);
    }

    [Fact]
    public async Task Transport_authorization_and_schema_failures_are_non_destructive()
    {
        await using var harness = await SourceSyncHarness.CreateAsync("sp-sync-failures");
        var source = await harness.CreateSourceAsync();
        var catalog = CreateCatalog();
        harness.Http.EnqueueCatalog(catalog);
        await harness.Sync.SynchronizeAsync(source.Id, PublicationSet(PrimaryPublicationId));
        var baseline = await harness.LoadStateAsync(source.Id);

        harness.Http.EnqueueTransportFailure();
        var transport = await harness.Sync.SynchronizeAsync(
            source.Id,
            PublicationSet(PrimaryPublicationId));
        await AssertNonDestructiveFailureAsync(
            harness,
            source.Id,
            baseline,
            transport,
            SharedProviderSourceStatus.SourceOffline,
            SharedProviderAvailabilityState.SourceOffline);

        harness.Http.EnqueueStatus(HttpStatusCode.Unauthorized, "credential rejected");
        var authorization = await harness.Sync.SynchronizeAsync(
            source.Id,
            PublicationSet(PrimaryPublicationId));
        await AssertNonDestructiveFailureAsync(
            harness,
            source.Id,
            baseline,
            authorization,
            SharedProviderSourceStatus.AuthorizationFailed,
            SharedProviderAvailabilityState.AuthorizationFailed);

        harness.Http.EnqueueStatus(HttpStatusCode.NotFound, "catalog route missing");
        var missingCatalogRoute = await harness.Sync.SynchronizeAsync(
            source.Id,
            PublicationSet(PrimaryPublicationId));
        await AssertNonDestructiveFailureAsync(
            harness,
            source.Id,
            baseline,
            missingCatalogRoute,
            SharedProviderSourceStatus.IncompatibleContract,
            SharedProviderAvailabilityState.IncompatibleContract);

        harness.Http.EnqueueInvalidCatalog(EntityTag(catalog));
        var schema = await harness.Sync.SynchronizeAsync(
            source.Id,
            PublicationSet(PrimaryPublicationId));
        await AssertNonDestructiveFailureAsync(
            harness,
            source.Id,
            baseline,
            schema,
            SharedProviderSourceStatus.IncompatibleContract,
            SharedProviderAvailabilityState.IncompatibleContract);

        harness.Observer.Clear();
        harness.Http.EnqueueCatalog(catalog);
        var directRecovery = await harness.Sync.SynchronizeAsync(
            source.Id,
            PublicationSet(PrimaryPublicationId));
        var directlyRecovered = await harness.LoadStateAsync(source.Id);
        Assert.Equal(SharedProviderSourceOperationOutcome.Succeeded, directRecovery.Outcome);
        Assert.Empty(harness.Http.Requests[^1].GetHeaderValues("If-None-Match"));
        Assert.Equal(SharedProviderSourceStatus.Available, directlyRecovered.Source.Status);
        Assert.Equal(
            SharedProviderAvailabilityState.Available,
            directlyRecovered.Imports.Single().AvailabilityState);
        Assert.Single(harness.Observer.SavedProviderIds);

        harness.Http.EnqueueTransportFailure();
        await harness.Sync.SynchronizeAsync(source.Id, PublicationSet(PrimaryPublicationId));
        harness.Http.EnqueueCatalog(catalog);
        var connectionTest = await harness.Sync.TestAsync(source.Id);
        var afterConnectionTest = await harness.LoadStateAsync(source.Id);
        Assert.Equal(SharedProviderSourceOperationOutcome.Succeeded, connectionTest.Outcome);
        Assert.Equal(SharedProviderSourceStatus.Available, afterConnectionTest.Source.Status);
        Assert.Equal(
            SharedProviderAvailabilityState.SourceOffline,
            afterConnectionTest.Imports.Single().AvailabilityState);

        harness.Observer.Clear();
        harness.Http.EnqueueCatalog(catalog);
        var recoveryAfterTest = await harness.Sync.SynchronizeAsync(
            source.Id,
            PublicationSet(PrimaryPublicationId));
        var recoveredAfterTest = await harness.LoadStateAsync(source.Id);
        Assert.Equal(SharedProviderSourceOperationOutcome.Succeeded, recoveryAfterTest.Outcome);
        Assert.Empty(harness.Http.Requests[^1].GetHeaderValues("If-None-Match"));
        Assert.Equal(
            SharedProviderAvailabilityState.Available,
            recoveredAfterTest.Imports.Single().AvailabilityState);
        Assert.Single(harness.Observer.SavedProviderIds);
    }

    [Fact]
    public async Task Only_successful_authoritative_catalog_absence_marks_selected_import_missing()
    {
        await using var harness = await SourceSyncHarness.CreateAsync("sp-sync-missing");
        var source = await harness.CreateSourceAsync();
        var catalog = CreateCatalog();
        harness.Http.EnqueueCatalog(catalog);
        await harness.Sync.SynchronizeAsync(source.Id, PublicationSet(PrimaryPublicationId));
        var identity = await harness.LoadStateAsync(source.Id);

        harness.Http.EnqueueStatus(HttpStatusCode.ServiceUnavailable, "temporary outage");
        await harness.Sync.SynchronizeAsync(source.Id, PublicationSet(PrimaryPublicationId));
        var afterOutage = await harness.LoadStateAsync(source.Id);
        Assert.Equal(identity.Imports.Single().Id, afterOutage.Imports.Single().Id);
        Assert.NotEqual(
            SharedProviderAvailabilityState.Missing,
            afterOutage.Imports.Single().AvailabilityState);

        harness.Http.EnqueueCatalog(CreateEmptyCatalog());
        var authoritative = await harness.Sync.SynchronizeAsync(
            source.Id,
            PublicationSet(PrimaryPublicationId));

        var afterSuccess = await harness.LoadStateAsync(source.Id);
        Assert.Equal(SharedProviderSourceOperationOutcome.Succeeded, authoritative.Outcome);
        Assert.Equal(SharedProviderAvailabilityState.Missing, afterSuccess.Imports.Single().AvailabilityState);
        Assert.Equal(identity.Imports.Single().Id, afterSuccess.Imports.Single().Id);
        Assert.Equal(identity.Profiles.Single().Id, afterSuccess.Profiles.Single().Id);
    }

    [Fact]
    public async Task Reappearing_publication_reuses_existing_import_and_provider_profile()
    {
        await using var harness = await SourceSyncHarness.CreateAsync("sp-sync-reappear");
        var source = await harness.CreateSourceAsync();
        var catalog = CreateCatalog();
        harness.Http.EnqueueCatalog(catalog);
        await harness.Sync.SynchronizeAsync(source.Id, PublicationSet(PrimaryPublicationId));
        var original = await harness.LoadStateAsync(source.Id);
        harness.Http.EnqueueCatalog(CreateEmptyCatalog());
        await harness.Sync.SynchronizeAsync(source.Id, PublicationSet(PrimaryPublicationId));
        harness.Http.EnqueueCatalog(catalog);

        var result = await harness.Sync.SynchronizeAsync(
            source.Id,
            PublicationSet(PrimaryPublicationId));

        var restored = await harness.LoadStateAsync(source.Id);
        Assert.Equal(SharedProviderSourceOperationOutcome.Succeeded, result.Outcome);
        Assert.Equal(original.Imports.Single().Id, restored.Imports.Single().Id);
        Assert.Equal(original.Profiles.Single().Id, restored.Profiles.Single().Id);
        Assert.Equal(SharedProviderAvailabilityState.Available, restored.Imports.Single().AvailabilityState);
        Assert.Equal(SharedProviderSelectionState.Selected, restored.Imports.Single().SelectionState);
    }

    [Fact]
    public async Task Source_edit_atomically_propagates_endpoint_and_secret_to_every_import_profile()
    {
        await using var harness = await SourceSyncHarness.CreateAsync("sp-sync-source-edit");
        var source = await harness.CreateSourceAsync();
        var catalog = CreateCatalogWithTwoPublications();
        harness.Http.EnqueueCatalog(catalog);
        await harness.Sync.SynchronizeAsync(
            source.Id,
            PublicationSet(PrimaryPublicationId, SecondaryPublicationId));
        await using (var editContext = harness.Factory.CreateDbContext())
        {
            var profiles = await editContext.Set<ProviderProfile>()
                .OrderBy(profile => profile.Id)
                .ToArrayAsync();
            profiles[0].Name = "Local first alias";
            profiles[0].IsEnabled = false;
            profiles[1].Name = "Local second alias";
            await editContext.SaveChangesAsync();
        }

        var newSecretId = await harness.CreateSecretAsync("rotated-source-token", "Rotated source token");
        var current = await harness.Sources.GetAsync(source.Id);
        var newBaseUri = new Uri("https://moved.example.test/new/reverse-proxy");
        harness.Observer.Clear();

        await harness.Sources.UpdateAsync(
            source.Id,
            current.ConcurrencyToken,
            new SharedProviderSourceWriteRequest(
                "Moved source",
                newBaseUri,
                newSecretId,
                IsEnabled: true,
                AllowInsecurePrivateNetwork: false));

        await using var verifyContext = harness.Factory.CreateDbContext();
        var updated = await verifyContext.Set<ProviderProfile>()
            .OrderBy(profile => profile.Name)
            .ToArrayAsync();
        var expectedBaseUri = SharedProviderRoutes.ResolveOpenAiBase(
            new Uri("https://moved.example.test/new/reverse-proxy/")).AbsoluteUri;
        Assert.Equal(2, updated.Length);
        Assert.All(updated, profile =>
        {
            Assert.Equal(expectedBaseUri, profile.BaseUrl);
            Assert.Equal((Guid?)newSecretId, profile.ApiKeySecretId);
        });
        Assert.Contains(updated, profile => profile.Name == "Local first alias" && !profile.IsEnabled);
        Assert.Contains(updated, profile => profile.Name == "Local second alias" && profile.IsEnabled);
        Assert.Equal(2, harness.Observer.SavedProviderIds.Count);
        Assert.True(harness.Observer.AllObservedProfilesWereCommitted);
        Assert.All(harness.Observer.CommittedProfileSets, committedProfiles =>
        {
            Assert.Equal(2, committedProfiles.Count);
            Assert.All(committedProfiles, profile =>
            {
                Assert.Equal(expectedBaseUri, profile.BaseUrl);
                Assert.Equal((Guid?)newSecretId, profile.ApiKeySecretId);
            });
        });
    }

    [Fact]
    public async Task Profile_observers_run_after_commit_and_skip_identical_and_not_modified_syncs()
    {
        await using var harness = await SourceSyncHarness.CreateAsync("sp-sync-observer");
        var source = await harness.CreateSourceAsync();
        var catalog = CreateCatalog();
        harness.Http.EnqueueCatalog(catalog);

        await harness.Sync.SynchronizeAsync(source.Id, PublicationSet(PrimaryPublicationId));

        Assert.Single(harness.Observer.SavedProviderIds);
        Assert.True(harness.Observer.AllObservedProfilesWereCommitted);
        harness.Observer.Clear();
        harness.Http.EnqueueCatalog(catalog);
        await harness.Sync.SynchronizeAsync(source.Id, PublicationSet(PrimaryPublicationId));
        Assert.Empty(harness.Observer.SavedProviderIds);
        harness.Http.EnqueueNotModified(EntityTag(catalog));
        await harness.Sync.SynchronizeAsync(source.Id, PublicationSet(PrimaryPublicationId));
        Assert.Empty(harness.Observer.SavedProviderIds);
    }

    [Fact]
    public async Task Failure_status_cache_persistence_and_logs_exclude_secret_and_remote_content()
    {
        await using var harness = await SourceSyncHarness.CreateAsync("sp-sync-containment");
        var source = await harness.CreateSourceAsync(token: SourceToken);
        var catalog = CreateCatalog();
        harness.Http.EnqueueCatalog(catalog);
        await harness.Sync.SynchronizeAsync(source.Id, PublicationSet(PrimaryPublicationId));
        var accessContext = new AccessContextReference(AccessContextValue);
        var hostileBody = $"credential={SourceToken}; context={AccessContextValue}; endpoint={PrivateUpstreamValue}";
        harness.Http.EnqueueStatus(HttpStatusCode.Unauthorized, hostileBody);
        harness.AccessContext.Current = accessContext;

        var result = await harness.Sync.SynchronizeAsync(
            source.Id,
            PublicationSet(PrimaryPublicationId));

        Assert.Equal(SharedProviderSourceOperationOutcome.Failed, result.Outcome);
        await using var dbContext = harness.Factory.CreateDbContext();
        var persistedSource = await dbContext.Set<SharedProviderSource>().SingleAsync();
        var persistedImport = await dbContext.Set<SharedProviderImport>().SingleAsync();
        var persistedProfile = await dbContext.Set<ProviderProfile>().SingleAsync();
        var persistedSecret = await dbContext.Set<SecretRecord>().SingleAsync();
        var persistedText = string.Join(
            '\n',
            persistedSource.Name,
            persistedSource.BaseUri,
            persistedSource.LastStatusMessage,
            persistedImport.RemoteCatalogSnapshotJson,
            persistedProfile.Name,
            persistedProfile.BaseUrl,
            persistedProfile.DefaultModel,
            persistedProfile.ExtraSettingsJson,
            persistedSecret.Name,
            persistedSecret.EncryptedPayload,
            persistedSecret.MetadataJson);
        var logText = string.Join('\n', harness.Logger.Messages);
        Assert.DoesNotContain(SourceToken, persistedText, StringComparison.Ordinal);
        Assert.DoesNotContain(AccessContextValue, persistedText, StringComparison.Ordinal);
        Assert.DoesNotContain(PrivateUpstreamValue, persistedText, StringComparison.Ordinal);
        Assert.DoesNotContain(SourceToken, logText, StringComparison.Ordinal);
        Assert.DoesNotContain(AccessContextValue, logText, StringComparison.Ordinal);
        Assert.DoesNotContain(PrivateUpstreamValue, logText, StringComparison.Ordinal);
        Assert.Equal(
            "The shared-provider source rejected the catalog credential.",
            persistedSource.LastStatusMessage);
    }

    private static async Task AssertNonDestructiveFailureAsync(
        SourceSyncHarness harness,
        Guid sourceId,
        PersistedSourceState baseline,
        SharedProviderSourceOperationResult result,
        SharedProviderSourceStatus expectedSourceStatus,
        SharedProviderAvailabilityState expectedAvailability)
    {
        var after = await harness.LoadStateAsync(sourceId);
        Assert.Equal(SharedProviderSourceOperationOutcome.Failed, result.Outcome);
        Assert.Equal(expectedSourceStatus, after.Source.Status);
        Assert.Equal(expectedAvailability, after.Imports.Single().AvailabilityState);
        Assert.Equal(baseline.Imports.Single().Id, after.Imports.Single().Id);
        Assert.Equal(baseline.Profiles.Single().Id, after.Profiles.Single().Id);
        Assert.Equal(
            baseline.Imports.Single().RemoteCatalogSnapshotJson,
            after.Imports.Single().RemoteCatalogSnapshotJson);
        Assert.Single(after.Imports);
        Assert.Single(after.Profiles);
    }

    private static IReadOnlySet<SharedProviderPublicationId> PublicationSet(
        params SharedProviderPublicationId[] publicationIds)
        => publicationIds.ToHashSet();

    private static SharedProviderCatalogEntityTag EntityTag(SharedProviderCatalogDocument catalog)
        => SharedProviderCatalogEntityTag.FromRevision(catalog.CatalogRevision);

    private static SharedProviderCatalogDocument CreateCatalog(
        string displayName = "Remote provider",
        SharedProviderSourceInstanceId? sourceInstanceId = null,
        string upstreamModelId = "upstream-model",
        IReadOnlyList<SharedProviderCapability>? capabilities = null)
        => CreateCatalogDocument(
            sourceInstanceId ?? PrimaryInstanceId,
            [CreatePublication(
                PrimaryPublicationId,
                displayName,
                upstreamModelId,
                capabilities)]);

    private static SharedProviderCatalogDocument CreateCatalogWithTwoPublications()
        => CreateCatalogDocument(
            PrimaryInstanceId,
            [
                CreatePublication(PrimaryPublicationId, "Remote primary provider", "upstream-primary"),
                CreatePublication(SecondaryPublicationId, "Remote secondary provider", "upstream-secondary")
            ]);

    private static SharedProviderCatalogDocument CreateEmptyCatalog()
        => CreateCatalogDocument(PrimaryInstanceId, []);

    private static SharedProviderCatalogDocument CreateCatalogDocument(
        SharedProviderSourceInstanceId sourceInstanceId,
        IReadOnlyList<SharedProviderCatalogPublication> publications)
    {
        var catalog = new SharedProviderCatalogDocument(
            SharedProviderProtocolVersion.Current,
            sourceInstanceId,
            new SharedProviderPublicRevision($"sha256:{new string('b', 64)}"),
            new SharedProviderProtocolDescriptor(SharedProviderRoutes.OpenAiBase),
            publications);
        return catalog with
        {
            CatalogRevision = SharedProviderCanonicalRevision.ComputeCatalog(catalog)
        };
    }

    private static SharedProviderCatalogPublication CreatePublication(
        SharedProviderPublicationId publicationId,
        string displayName,
        string upstreamModelId,
        IReadOnlyList<SharedProviderCapability>? capabilities = null)
    {
        var modelId = SharedProviderRoutingModelIdCodec.Create(publicationId, upstreamModelId);
        var publication = new SharedProviderCatalogPublication(
            publicationId,
            new SharedProviderPublicRevision($"sha256:{new string('a', 64)}"),
            displayName,
            SharedProviderPurpose.Chat,
            SharedProviderTransport.OpenAiCompatible,
            modelId,
            [
                new SharedProviderCatalogModel(
                    modelId,
                    "Remote model",
                    capabilities ??
                    [
                        SharedProviderCapability.Responses,
                        SharedProviderCapability.Streaming
                    ])
            ],
            new SharedProviderCatalogHealth(SharedProviderHealthState.Available));
        return publication with
        {
            Revision = SharedProviderCanonicalRevision.ComputePublication(publication)
        };
    }

    private sealed class SourceSyncHarness : IAsyncDisposable
    {
        private readonly SourceSyncTestDatabase database;
        private readonly InMemorySecretVault vault;

        private SourceSyncHarness(
            SourceSyncTestDatabase database,
            InMemorySecretVault vault,
            ScriptedCatalogHttpHandler http,
            RecordingCatalogLogger logger,
            RecordingProfileObserver observer,
            ScriptedAccessContextAccessor accessContext,
            SharedProviderSourceService sources,
            SharedProviderSourceSyncService sync)
        {
            this.database = database;
            this.vault = vault;
            Http = http;
            Logger = logger;
            Observer = observer;
            AccessContext = accessContext;
            Sources = sources;
            Sync = sync;
        }

        public SharedProviderDbContextFactory Factory => database.Factory;

        public ScriptedCatalogHttpHandler Http { get; }

        public RecordingCatalogLogger Logger { get; }

        public RecordingProfileObserver Observer { get; }

        public ScriptedAccessContextAccessor AccessContext { get; }

        public SharedProviderSourceService Sources { get; }

        public SharedProviderSourceSyncService Sync { get; }

        public static async Task<SourceSyncHarness> CreateAsync(string key)
        {
            var database = await SourceSyncTestDatabase.CreateAsync(key);
            var clock = new FixedClock(Now);
            var vault = new InMemorySecretVault();
            var secretResolver = new SecretRuntimeResolver(
                database.Factory,
                vault,
                new UnusedSecretProtector());
            var handler = new ScriptedCatalogHttpHandler();
            var logger = new RecordingCatalogLogger();
            var uriPolicy = new SharedProviderSourceUriPolicy();
            var accessContext = new ScriptedAccessContextAccessor();
            var catalogClient = new SharedProviderCatalogClient(
                new ScriptedHttpClientFactory(handler),
                uriPolicy,
                logger,
                accessContext);
            var observer = new RecordingProfileObserver(database.Factory);
            var sourceService = new SharedProviderSourceService(
                database.Factory,
                clock,
                [observer],
                uriPolicy);
            var reconciliation = new SharedProviderReconciliationCoordinator(
                database.Factory,
                clock,
                [observer]);
            var sync = new SharedProviderSourceSyncService(
                database.Factory,
                sourceService,
                reconciliation,
                catalogClient,
                secretResolver);
            return new SourceSyncHarness(
                database,
                vault,
                handler,
                logger,
                observer,
                accessContext,
                sourceService,
                sync);
        }

        public async Task<SourceSeed> CreateSourceAsync(
            Uri? baseUri = null,
            string token = SourceToken)
        {
            var secretId = await CreateSecretAsync(token, "Shared source token");
            var created = await Sources.CreateAsync(new SharedProviderSourceWriteRequest(
                "Central source",
                baseUri ?? new Uri("https://central.example.test/root"),
                secretId,
                IsEnabled: true,
                AllowInsecurePrivateNetwork: false));
            return new SourceSeed(created.Id, created.ConcurrencyToken, secretId, token);
        }

        public async Task<Guid> CreateSecretAsync(string value, string name)
        {
            var secret = new SecretRecord
            {
                Name = name,
                Kind = SecretKind.Token,
                Scope = "workspace",
                MetadataJson = "{}",
                CreatedAtUtc = Now,
                UpdatedAtUtc = Now
            };
            var vaultKey = SecretVaultRecordReference.BuildKey(secret.Id, Guid.NewGuid());
            await vault.SetAsync(vaultKey, value);
            secret.EncryptedPayload = SecretVaultRecordReference.Create(vaultKey);
            await using var dbContext = Factory.CreateDbContext();
            dbContext.Add(secret);
            await dbContext.SaveChangesAsync();
            return secret.Id;
        }

        public async Task<PersistedSourceState> LoadStateAsync(Guid sourceId)
        {
            var source = await Sources.GetAsync(sourceId);
            await using var dbContext = Factory.CreateDbContext();
            var imports = await dbContext.Set<SharedProviderImport>()
                .AsNoTracking()
                .Where(importItem => importItem.SourceId == sourceId)
                .OrderBy(importItem => importItem.RemotePublicationId)
                .Select(importItem => new PersistedImportState(
                    importItem.Id,
                    importItem.ProviderProfileId,
                    importItem.RemotePublicationId,
                    importItem.RemoteDisplayName,
                    importItem.RemoteRevision,
                    importItem.RemoteCatalogSnapshotJson,
                    importItem.SelectionState,
                    importItem.AvailabilityState,
                    importItem.ConcurrencyToken))
                .ToArrayAsync();
            var providerIds = imports.Select(importItem => importItem.ProviderProfileId).ToArray();
            var profiles = await dbContext.Set<ProviderProfile>()
                .AsNoTracking()
                .Where(profile => providerIds.Contains(profile.Id))
                .OrderBy(profile => profile.Id)
                .Select(profile => new PersistedProfileState(
                    profile.Id,
                    profile.Name,
                    profile.IsEnabled,
                    profile.BaseUrl,
                    profile.ApiKeySecretId,
                    profile.DefaultModel,
                    profile.SupportsStreaming,
                    profile.SupportsToolCalling,
                    profile.SupportsStructuredOutput,
                    profile.SupportsVision,
                    profile.ConcurrencyToken))
                .ToArrayAsync();
            return new PersistedSourceState(source, imports, profiles);
        }

        public ValueTask DisposeAsync() => database.DisposeAsync();
    }

    private sealed class ScriptedCatalogHttpHandler : HttpMessageHandler
    {
        private readonly ConcurrentQueue<
            Func<CapturedCatalogRequest, CancellationToken, Task<HttpResponseMessage>>> scripts = new();
        private readonly List<CapturedCatalogRequest> requests = [];

        public IReadOnlyList<CapturedCatalogRequest> Requests
        {
            get
            {
                lock (requests)
                {
                    return requests.ToArray();
                }
            }
        }

        public void EnqueueCatalog(SharedProviderCatalogDocument catalog)
            => Enqueue((_, _) => Task.FromResult(CreateCatalogResponse(catalog)));

        public void EnqueueNotModified(SharedProviderCatalogEntityTag entityTag)
            => Enqueue((_, _) => Task.FromResult(CreateNotModifiedResponse(entityTag)));

        public void EnqueueStatus(HttpStatusCode statusCode, string body)
            => Enqueue((_, _) => Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(body, Encoding.UTF8, "text/plain")
            }));

        public void EnqueueInvalidCatalog(SharedProviderCatalogEntityTag entityTag)
            => Enqueue((_, _) =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        "{\"schemaVersion\":\"unsupported\",\"secret\":\"must-not-persist\"}",
                        Encoding.UTF8,
                        "application/json")
                };
                response.Headers.ETag = EntityTagHeaderValue.Parse(entityTag.Value);
                return Task.FromResult(response);
            });

        public void EnqueueTransportFailure()
            => Enqueue((_, _) => Task.FromException<HttpResponseMessage>(
                new HttpRequestException("Deterministic source transport failure.")));

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var captured = await CapturedCatalogRequest.CreateAsync(request, cancellationToken);
            lock (requests)
            {
                requests.Add(captured);
            }

            if (!scripts.TryDequeue(out var script))
            {
                throw new InvalidOperationException("No catalog HTTP response was scripted.");
            }

            return await script(captured, cancellationToken);
        }

        private void Enqueue(
            Func<CapturedCatalogRequest, CancellationToken, Task<HttpResponseMessage>> script)
            => scripts.Enqueue(script);

        private static HttpResponseMessage CreateCatalogResponse(
            SharedProviderCatalogDocument catalog)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    SharedProviderProtocolJson.SerializeCatalog(catalog),
                    Encoding.UTF8,
                    "application/json")
            };
            response.Headers.ETag = EntityTagHeaderValue.Parse(EntityTag(catalog).Value);
            return response;
        }

        private static HttpResponseMessage CreateNotModifiedResponse(
            SharedProviderCatalogEntityTag entityTag)
        {
            var response = new HttpResponseMessage(HttpStatusCode.NotModified)
            {
                Content = new ByteArrayContent([])
            };
            response.Headers.ETag = EntityTagHeaderValue.Parse(entityTag.Value);
            return response;
        }
    }

    private sealed class ScriptedHttpClientFactory(ScriptedCatalogHttpHandler handler)
        : IHttpClientFactory
    {
        public ConcurrentQueue<string> RequestedClientNames { get; } = new();

        public HttpClient CreateClient(string name)
        {
            RequestedClientNames.Enqueue(name);
            return new HttpClient(handler, disposeHandler: false)
            {
                Timeout = Timeout.InfiniteTimeSpan
            };
        }
    }

    private sealed record CapturedCatalogRequest(
        HttpMethod Method,
        Uri RequestUri,
        IReadOnlyDictionary<string, string[]> Headers,
        string? Content,
        string? AuthorizationScheme,
        string? AuthorizationParameter)
    {
        public IReadOnlyList<string> GetHeaderValues(string name)
            => Headers.TryGetValue(name, out var values)
                ? values
                : [];

        public static async Task<CapturedCatalogRequest> CreateAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var headers = request.Headers.ToDictionary(
                header => header.Key,
                header => header.Value.ToArray(),
                StringComparer.OrdinalIgnoreCase);
            if (request.Content is not null)
            {
                foreach (var header in request.Content.Headers)
                {
                    headers[header.Key] = header.Value.ToArray();
                }
            }

            var content = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return new CapturedCatalogRequest(
                request.Method,
                request.RequestUri ?? throw new InvalidOperationException("Catalog request URI is missing."),
                headers,
                content,
                request.Headers.Authorization?.Scheme,
                request.Headers.Authorization?.Parameter);
        }
    }

    private sealed class RecordingCatalogLogger : ILogger<SharedProviderCatalogClient>
    {
        private readonly ConcurrentQueue<string> messages = new();

        public IReadOnlyList<string> Messages => messages.ToArray();

        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull
            => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            messages.Enqueue(formatter(state, exception));
            if (exception is not null)
            {
                messages.Enqueue(exception.ToString());
            }
        }
    }

    private sealed class RecordingProfileObserver(SharedProviderDbContextFactory factory)
        : IProviderProfileCommitObserver
    {
        private readonly List<Guid> savedProviderIds = [];
        private readonly List<IReadOnlyList<ObservedProfileConfiguration>> committedProfileSets = [];

        public IReadOnlyList<Guid> SavedProviderIds
        {
            get
            {
                lock (savedProviderIds)
                {
                    return savedProviderIds.ToArray();
                }
            }
        }

        public bool AllObservedProfilesWereCommitted { get; private set; } = true;

        public IReadOnlyList<IReadOnlyList<ObservedProfileConfiguration>> CommittedProfileSets
        {
            get
            {
                lock (savedProviderIds)
                {
                    return committedProfileSets.ToArray();
                }
            }
        }

        public async Task ProviderSavedAsync(
            Guid providerId,
            CancellationToken cancellationToken = default)
        {
            await using var dbContext = factory.CreateDbContext();
            var committedProfiles = await dbContext.Set<ProviderProfile>()
                .AsNoTracking()
                .Where(profile =>
                    profile.ConnectorPluginKey == SharedProviderReconciliationCoordinator.ImportedConnectorPluginKey)
                .OrderBy(profile => profile.Id)
                .Select(profile => new ObservedProfileConfiguration(
                    profile.Id,
                    profile.BaseUrl,
                    profile.ApiKeySecretId))
                .ToArrayAsync(cancellationToken);
            AllObservedProfilesWereCommitted &= committedProfiles.Any(profile => profile.Id == providerId);
            lock (savedProviderIds)
            {
                savedProviderIds.Add(providerId);
                committedProfileSets.Add(committedProfiles);
            }
        }

        public Task ProviderDeletedAsync(
            Guid providerId,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public void Clear()
        {
            lock (savedProviderIds)
            {
                savedProviderIds.Clear();
                committedProfileSets.Clear();
            }

            AllObservedProfilesWereCommitted = true;
        }
    }

    private sealed class ScriptedAccessContextAccessor : IAccessContextReferenceAccessor
    {
        public AccessContextReference? Current { get; set; }
        public AccessContextReferenceType? CurrentType { get; set; }
    }

    private sealed class SourceSyncTestDatabase : IAsyncDisposable
    {
        private readonly PostgresTestDatabaseLease lease;

        private SourceSyncTestDatabase(PostgresTestDatabaseLease lease)
        {
            this.lease = lease;
            Factory = new SharedProviderDbContextFactory(lease.CreateAppDbContextOptions());
        }

        public SharedProviderDbContextFactory Factory { get; }

        public static async Task<SourceSyncTestDatabase> CreateAsync(string key)
        {
            AppDbContextModelRegistry.ConfigureAssemblies(ModuleAssemblies.All);
            var database = new SourceSyncTestDatabase(PostgresTestDatabaseLease.Create(key));
            await using var dbContext = database.Factory.CreateDbContext();
            await dbContext.Database.EnsureCreatedAsync();
            return database;
        }

        public ValueTask DisposeAsync() => lease.DisposeAsync();
    }

    private sealed class SharedProviderDbContextFactory(DbContextOptions<AppDbContext> options)
        : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext() => new(options);
    }

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset GetUtcNow() => now;
    }

    private sealed class UnusedSecretProtector : ISecretProtector
    {
        public string Protect(string plainText) => throw new NotSupportedException();

        public string Unprotect(string protectedValue) => throw new NotSupportedException();
    }

    private sealed class NullScope : IDisposable
    {
        public static NullScope Instance { get; } = new();

        public void Dispose()
        {
        }
    }

    private sealed record SourceSeed(
        Guid Id,
        Guid ConcurrencyToken,
        Guid SecretId,
        string Token);

    private sealed record PersistedSourceState(
        SharedProviderSourceSnapshot Source,
        IReadOnlyList<PersistedImportState> Imports,
        IReadOnlyList<PersistedProfileState> Profiles);

    private sealed record PersistedImportState(
        Guid Id,
        Guid ProviderProfileId,
        SharedProviderPublicationId RemotePublicationId,
        string RemoteDisplayName,
        SharedProviderPublicRevision RemoteRevision,
        string RemoteCatalogSnapshotJson,
        SharedProviderSelectionState SelectionState,
        SharedProviderAvailabilityState AvailabilityState,
        Guid ConcurrencyToken);

    private sealed record PersistedProfileState(
        Guid Id,
        string Name,
        bool IsEnabled,
        string BaseUrl,
        Guid? ApiKeySecretId,
        string DefaultModel,
        bool SupportsStreaming,
        bool SupportsToolCalling,
        bool SupportsStructuredOutput,
        bool SupportsVision,
        Guid ConcurrencyToken);

    private sealed record ObservedProfileConfiguration(
        Guid Id,
        string BaseUrl,
        Guid? ApiKeySecretId);
}
