using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Security;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.SharedProviders.Abstractions;

namespace CanDoItAll.SharedProviders.E2E;

using AgentProviderEditor = CanDoItAll.AgentFramework.Models.ProviderProfileEditorModel;
using AgentProviderKind = CanDoItAll.AgentFramework.Models.ProviderKind;
using AgentProviderProfile = CanDoItAll.AgentFramework.Models.ProviderProfile;
using IProviderAdministrationService = CanDoItAll.Modules.AgentFramework.ProviderManagement.IProviderAdministrationService;

internal sealed class E2eOrchestrator(
    E2eOptions options,
    E2eArtifactStore artifacts,
    E2eSnapshotService snapshots,
    ICanDoItAllAgentWorkspaceFactory workspaceFactory,
    IProviderAdministrationService providerAdministrationService,
    SecretService secretService,
    SharedProviderServiceIdentityStore identityStore,
    SharedProviderPublicationStore publicationStore,
    SharedProviderPublicationApplicationService publicationApplicationService,
    IApiTokenService apiTokenService,
    SharedProviderSourceService sourceService,
    SharedProviderSourceSyncService sourceSyncService)
{
    private const int E2eTokenLifetimeMinutes = 10080;
    private const string SecretScope = "workspace";

    public async Task ExecuteAsync(
        E2eCommand command,
        E2eRole role,
        CancellationToken cancellationToken)
    {
        await (command switch
        {
            E2eCommand.SeedCentral => SeedCentralAsync(cancellationToken),
            E2eCommand.SeedClientA => SeedClientAAsync(cancellationToken),
            E2eCommand.SeedClientB => SeedClientBAsync(cancellationToken),
            E2eCommand.Snapshot => WriteSnapshotAsync(role, cancellationToken),
            E2eCommand.UnpublishText => SetTextPublicationAsync(
                SharedProviderPublicationAction.Unpublish,
                cancellationToken),
            E2eCommand.RepublishText => SetTextPublicationAsync(
                SharedProviderPublicationAction.Publish,
                cancellationToken),
            E2eCommand.SyncClientA => SynchronizeClientAsync(
                E2eRole.ClientA,
                E2eFixtures.ClientASelection,
                cancellationToken),
            E2eCommand.SyncClientB => SynchronizeClientAsync(
                E2eRole.ClientB,
                E2eFixtures.ClientBSelection,
                cancellationToken),
            E2eCommand.SyncClientAExpectOffline => SynchronizeClientExpectOfflineAsync(
                E2eRole.ClientA,
                E2eFixtures.ClientASelection,
                cancellationToken),
            E2eCommand.SyncClientBExpectOffline => SynchronizeClientExpectOfflineAsync(
                E2eRole.ClientB,
                E2eFixtures.ClientBSelection,
                cancellationToken),
            E2eCommand.PointClientAAtClientB => PointClientAAtClientBAsync(cancellationToken),
            E2eCommand.RestoreClientASource => RestoreClientASourceAsync(cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(command), command, null)
        });
    }

    private async Task SeedCentralAsync(CancellationToken cancellationToken)
    {
        var upstreamToken = await ReadUpstreamTokenAsync(cancellationToken);
        var upstreamSecretId = await SaveSecretAsync(
            E2eFixtures.CentralProviderSecretName,
            SecretKind.ApiKey,
            upstreamToken,
            cancellationToken);
        var workspace = workspaceFactory.GetOrganizationWorkspaceService();

        foreach (var fixture in E2eFixtures.CentralProviders)
        {
            var providerId = await SaveProviderAsync(
                workspace,
                fixture,
                fixture.RequiresSecret ? upstreamSecretId : null,
                fixture.Kind == AgentProviderKind.ComfyUi
                    ? options.ComfyUiBaseUri
                    : options.UpstreamBaseUri,
                cancellationToken);
            await SetPublicationAsync(
                providerId,
                fixture.IsPublished
                    ? SharedProviderPublicationAction.Publish
                    : SharedProviderPublicationAction.Unpublish,
                cancellationToken);
        }

        await identityStore.GetOrCreateAsync(cancellationToken);
        await IssueCentralCredentialsAsync(cancellationToken);
        await WriteSnapshotAsync(E2eRole.Central, cancellationToken);
    }

    private async Task SeedClientAAsync(CancellationToken cancellationToken)
    {
        await identityStore.GetOrCreateAsync(cancellationToken);
        var clientToken = IssueToken(
            "shared-providers-e2e-client-a",
            "Shared providers E2E client A",
            ApiAccessScopeNames.ReadSharedProviderCatalog,
            ApiAccessScopeNames.InvokeSharedProviders);
        await artifacts.WriteCredentialAsync(
            E2eFixtures.ClientAAccessCredentialFileName,
            clientToken.Token,
            cancellationToken);
        var upstreamToken = await ReadUpstreamTokenAsync(cancellationToken);
        var personalSecretId = await SaveSecretAsync(
            E2eFixtures.ClientAPersonalSecretName,
            SecretKind.ApiKey,
            upstreamToken,
            cancellationToken);
        var workspace = workspaceFactory.GetOrganizationWorkspaceService();
        await SaveProviderAsync(
            workspace,
            E2eFixtures.ClientAPersonalProvider,
            personalSecretId,
            options.UpstreamBaseUri,
            cancellationToken);

        await EnsureCentralSourceAsync(cancellationToken);
        await SynchronizeClientAsync(
            E2eRole.ClientA,
            E2eFixtures.ClientASelection,
            cancellationToken);
        await WriteCheckpointSnapshotAsync(E2eRole.ClientA, "seed", cancellationToken);
    }

    private async Task SeedClientBAsync(CancellationToken cancellationToken)
    {
        await identityStore.GetOrCreateAsync(cancellationToken);
        var token = IssueToken(
            "shared-providers-e2e-client-b",
            "Shared providers E2E client B",
            ApiAccessScopeNames.ReadSharedProviderCatalog,
            ApiAccessScopeNames.InvokeSharedProviders);
        await artifacts.WriteCredentialAsync(
            E2eFixtures.ClientBAccessCredentialFileName,
            token.Token,
            cancellationToken);

        await EnsureCentralSourceAsync(cancellationToken);
        await SynchronizeClientAsync(
            E2eRole.ClientB,
            E2eFixtures.ClientBSelection,
            cancellationToken);
        await WriteCheckpointSnapshotAsync(E2eRole.ClientB, "seed", cancellationToken);
    }

    private async Task SetTextPublicationAsync(
        SharedProviderPublicationAction action,
        CancellationToken cancellationToken)
    {
        var workspace = workspaceFactory.GetOrganizationWorkspaceService();
        var providerId = await FindProviderIdAsync(
            workspace,
            E2eFixtures.CentralProviders.Single(fixture =>
                fixture.Id == E2eFixtures.ChatCompletions).Name,
            cancellationToken);
        await SetPublicationAsync(providerId, action, cancellationToken);
        await WriteSnapshotAsync(E2eRole.Central, cancellationToken);
    }

    private async Task SynchronizeClientAsync(
        E2eRole role,
        IReadOnlyList<string> fixtureIds,
        CancellationToken cancellationToken)
    {
        var source = await GetCentralSourceAsync(cancellationToken);
        var selection = await ResolveSelectionAsync(fixtureIds, cancellationToken);
        var result = await sourceSyncService.SynchronizeAsync(
            source.Id,
            selection,
            cancellationToken);
        if (result.Outcome is not (
            SharedProviderSourceOperationOutcome.Succeeded or
            SharedProviderSourceOperationOutcome.NotModified))
        {
            throw new E2eSafeException(
                $"Shared-provider source synchronization failed with outcome '{result.Outcome}'.");
        }

        await artifacts.WriteSyncOutcomeAsync(role, result.Outcome, cancellationToken);
        await WriteSnapshotAsync(role, cancellationToken);
    }

    private async Task PointClientAAtClientBAsync(CancellationToken cancellationToken)
    {
        var source = await GetCentralSourceAsync(cancellationToken);
        if (source.RemoteInstanceId is null)
        {
            throw new E2eSafeException(
                "Client A must synchronize with central before the identity-mismatch command.");
        }

        var clientBToken = await artifacts.ReadCredentialAsync(
            E2eFixtures.ClientBAccessCredentialFileName,
            cancellationToken);
        var secretId = await SaveSecretAsync(
            E2eFixtures.ClientBMismatchTokenSecretName,
            SecretKind.Token,
            clientBToken,
            cancellationToken);
        await sourceService.UpdateAsync(
            source.Id,
            source.ConcurrencyToken,
            CreateSourceRequest(options.ClientBBaseUri, secretId),
            cancellationToken);

        var result = await sourceSyncService.TestAsync(source.Id, cancellationToken);
        if (result.Outcome != SharedProviderSourceOperationOutcome.SourceIdentityMismatch)
        {
            throw new E2eSafeException(
                $"The client-B source probe returned '{result.Outcome}' instead of SourceIdentityMismatch.");
        }

        await WriteSnapshotAsync(E2eRole.ClientA, cancellationToken);
    }

    private async Task SynchronizeClientExpectOfflineAsync(
        E2eRole role,
        IReadOnlyList<string> fixtureIds,
        CancellationToken cancellationToken)
    {
        var source = await GetCentralSourceAsync(cancellationToken);
        var selection = await ResolveSelectionAsync(fixtureIds, cancellationToken);
        var result = await sourceSyncService.SynchronizeAsync(
            source.Id,
            selection,
            cancellationToken);
        if (result.Outcome != SharedProviderSourceOperationOutcome.Failed ||
            result.Failure is not
            {
                Category: SharedProviderFailureCategory.Unavailable,
                Code: var failureCode
            } ||
            failureCode != SharedProviderCatalogFailureCodes.Unavailable)
        {
            throw new E2eSafeException(
                $"The expected offline synchronization returned outcome '{result.Outcome}'.");
        }

        await WriteSnapshotAsync(role, cancellationToken);
    }

    private async Task RestoreClientASourceAsync(CancellationToken cancellationToken)
    {
        var source = await GetCentralSourceAsync(cancellationToken);
        var centralToken = await artifacts.ReadCredentialAsync(
            E2eFixtures.CentralAccessCredentialFileName,
            cancellationToken);
        var secretId = await SaveSecretAsync(
            E2eFixtures.CentralSourceTokenSecretName,
            SecretKind.Token,
            centralToken,
            cancellationToken);
        await sourceService.UpdateAsync(
            source.Id,
            source.ConcurrencyToken,
            CreateSourceRequest(options.CentralBaseUri, secretId),
            cancellationToken);

        var testResult = await sourceSyncService.TestAsync(source.Id, cancellationToken);
        if (testResult.Outcome != SharedProviderSourceOperationOutcome.Succeeded)
        {
            throw new E2eSafeException(
                $"The restored central source probe failed with outcome '{testResult.Outcome}'.");
        }

        await SynchronizeClientAsync(
            E2eRole.ClientA,
            E2eFixtures.ClientASelection,
            cancellationToken);
    }

    private async Task EnsureCentralSourceAsync(CancellationToken cancellationToken)
    {
        var centralToken = await artifacts.ReadCredentialAsync(
            E2eFixtures.CentralAccessCredentialFileName,
            cancellationToken);
        var tokenSecretId = await SaveSecretAsync(
            E2eFixtures.CentralSourceTokenSecretName,
            SecretKind.Token,
            centralToken,
            cancellationToken);
        var sources = await sourceService.ListAsync(cancellationToken);
        var matching = sources
            .Where(source => string.Equals(
                source.Name,
                E2eFixtures.CentralSourceName,
                StringComparison.Ordinal))
            .ToArray();
        if (matching.Length > 1)
        {
            throw new E2eSafeException("The role database contains duplicate E2E central sources.");
        }

        Guid sourceId;
        if (matching.Length == 0)
        {
            var created = await sourceService.CreateAsync(
                CreateSourceRequest(options.CentralBaseUri, tokenSecretId),
                cancellationToken);
            sourceId = created.Id;
        }
        else
        {
            var current = matching[0];
            var updated = await sourceService.UpdateAsync(
                current.Id,
                current.ConcurrencyToken,
                CreateSourceRequest(options.CentralBaseUri, tokenSecretId),
                cancellationToken);
            sourceId = updated.Id;
        }

        var testResult = await sourceSyncService.TestAsync(sourceId, cancellationToken);
        if (testResult.Outcome != SharedProviderSourceOperationOutcome.Succeeded)
        {
            throw new E2eSafeException(
                $"The central source probe failed with outcome '{testResult.Outcome}'.");
        }
    }

    private async Task<IReadOnlySet<SharedProviderPublicationId>> ResolveSelectionAsync(
        IReadOnlyList<string> fixtureIds,
        CancellationToken cancellationToken)
    {
        var central = await artifacts.ReadCentralSnapshotAsync(cancellationToken);
        var selection = new HashSet<SharedProviderPublicationId>();
        foreach (var fixtureId in fixtureIds)
        {
            var matches = central.Fixtures
                .Where(fixture => string.Equals(
                    fixture.FixtureId,
                    fixtureId,
                    StringComparison.Ordinal))
                .ToArray();
            if (matches.Length != 1 || matches[0].PublicationId is not { } publicationId)
            {
                throw new E2eSafeException(
                    "The central handoff does not contain every required publication identity.");
            }

            selection.Add(new SharedProviderPublicationId(publicationId));
        }

        return selection;
    }

    private async Task<Guid> SaveProviderAsync(
        CanDoItAll.AgentFramework.Core.IAgentFrameworkWorkspaceService workspace,
        ProviderFixture fixture,
        Guid? secretId,
        Uri baseUri,
        CancellationToken cancellationToken)
    {
        var providers = await workspace.ListProvidersAsync(cancellationToken);
        var existing = FindProvider(providers, fixture.Name);
        var isChat = fixture.Purpose == ProviderProfilePurpose.Chat;
        var editor = new AgentProviderEditor
        {
            Id = existing?.Id,
            Name = fixture.Name,
            Kind = fixture.Kind,
            BaseUrl = baseUri.AbsoluteUri.TrimEnd('/'),
            ApiKeyEnvironmentVariable = string.Empty,
            DefaultModel = fixture.DefaultModel,
            Transport = fixture.Transport,
            Purpose = fixture.Purpose,
            IsEnabled = true,
            SupportsStreaming = isChat,
            SupportsTools = isChat,
            PreferFrameworkManagedChatHistory = false,
            SupportsBackgroundResponses = isChat && fixture.Transport == ProviderTransportKind.Responses,
            ConfigurationJson = E2eFixtures.CreateConfigurationJson(fixture, secretId),
            Notes = string.Empty,
            IsPrivateProvider = !fixture.IsPublished,
            SuggestedModels = [fixture.DefaultModel],
            Tags = ["shared-providers-e2e", fixture.Id]
        };

        try
        {
            var providerId = await workspace.SaveProviderAsync(editor, cancellationToken);
            var workspaceEditor = await providerAdministrationService.GetProviderAsync(
                providerId,
                cancellationToken);
            workspaceEditor.SupportsStructuredOutput = fixture.SupportsStructuredOutput;
            var capabilityUpdate = await providerAdministrationService.SaveProviderAsync(
                workspaceEditor,
                cancellationToken);
            if (capabilityUpdate.IsFailure || capabilityUpdate.Value != providerId)
            {
                throw new E2eSafeException(
                    $"Saving provider fixture '{fixture.Id}' capabilities through provider administration failed.");
            }

            return providerId;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new E2eSafeException(
                $"Saving provider fixture '{fixture.Id}' through the AgentFramework workspace failed.",
                exception);
        }
    }

    private async Task<Guid> FindProviderIdAsync(
        CanDoItAll.AgentFramework.Core.IAgentFrameworkWorkspaceService workspace,
        string name,
        CancellationToken cancellationToken)
    {
        var providers = await workspace.ListProvidersAsync(cancellationToken);
        return FindProvider(providers, name)?.Id
            ?? throw new E2eSafeException("The required E2E provider fixture does not exist.");
    }

    private static AgentProviderProfile? FindProvider(
        IReadOnlyList<AgentProviderProfile> providers,
        string name)
    {
        var matches = providers
            .Where(provider => string.Equals(provider.Name, name, StringComparison.Ordinal))
            .ToArray();
        return matches.Length switch
        {
            0 => null,
            1 => matches[0],
            _ => throw new E2eSafeException("The role database contains duplicate E2E provider fixtures.")
        };
    }

    private async Task SetPublicationAsync(
        Guid providerId,
        SharedProviderPublicationAction action,
        CancellationToken cancellationToken)
    {
        var publication = await publicationStore.GetOrCreateAsync(
            providerId,
            cancellationToken);
        await publicationApplicationService.ChangeAsync(
            new SharedProviderPublicationChangeRequest(
                providerId,
                action,
                publication.ConcurrencyToken),
            cancellationToken);
    }

    private async Task<Guid> SaveSecretAsync(
        string name,
        SecretKind kind,
        string value,
        CancellationToken cancellationToken)
    {
        var existing = (await secretService.ListAsync(cancellationToken))
            .Where(secret => string.Equals(secret.Name, name, StringComparison.Ordinal))
            .ToArray();
        if (existing.Length > 1)
        {
            throw new E2eSafeException("The role database contains duplicate E2E secret records.");
        }

        var result = await secretService.SaveAsync(
            new SecretEditorModel
            {
                Id = existing.SingleOrDefault()?.Id,
                Name = name,
                Kind = kind,
                SecretValue = value,
                Scope = SecretScope,
                MetadataJson = "{}"
            },
            cancellationToken);
        if (result.IsFailure || result.Value == Guid.Empty)
        {
            throw new E2eSafeException("Saving an E2E credential through SecretService failed.");
        }

        return result.Value;
    }

    private async Task IssueCentralCredentialsAsync(CancellationToken cancellationToken)
    {
        var access = IssueToken(
            "shared-providers-e2e-client",
            "Shared providers E2E client",
            ApiAccessScopeNames.ReadSharedProviderCatalog,
            ApiAccessScopeNames.InvokeSharedProviders);
        var catalogOnly = IssueToken(
            "shared-providers-e2e-catalog-only",
            "Shared providers E2E catalog-only client",
            ApiAccessScopeNames.ReadSharedProviderCatalog);
        var invokeOnly = IssueToken(
            "shared-providers-e2e-invoke-only",
            "Shared providers E2E invoke-only client",
            ApiAccessScopeNames.InvokeSharedProviders);

        await artifacts.WriteCredentialAsync(
            E2eFixtures.CentralAccessCredentialFileName,
            access.Token,
            cancellationToken);
        await artifacts.WriteCredentialAsync(
            E2eFixtures.CentralCatalogOnlyCredentialFileName,
            catalogOnly.Token,
            cancellationToken);
        await artifacts.WriteCredentialAsync(
            E2eFixtures.CentralInvokeOnlyCredentialFileName,
            invokeOnly.Token,
            cancellationToken);
    }

    private ApiTokenIssueResult IssueToken(
        string subject,
        string displayName,
        params string[] scopes)
        => apiTokenService.IssueToken(new ApiTokenIssueRequest
        {
            Subject = subject,
            DisplayName = displayName,
            LifetimeMinutes = E2eTokenLifetimeMinutes,
            Scopes = [.. scopes]
        });

    private async Task<string> ReadUpstreamTokenAsync(CancellationToken cancellationToken)
    {
        if (options.UpstreamTokenFilePath is null)
        {
            throw new E2eSafeException(
                "The upstream token secret file is required for this seed command.");
        }

        return await E2eSecretFile.ReadRequiredAsync(
            options.UpstreamTokenFilePath,
            "upstream token",
            cancellationToken);
    }

    private async Task<SharedProviderSourceSnapshot> GetCentralSourceAsync(
        CancellationToken cancellationToken)
    {
        var sources = (await sourceService.ListAsync(cancellationToken))
            .Where(source => string.Equals(
                source.Name,
                E2eFixtures.CentralSourceName,
                StringComparison.Ordinal))
            .ToArray();
        return sources.Length switch
        {
            1 => sources[0],
            0 => throw new E2eSafeException("The E2E central source does not exist in this role database."),
            _ => throw new E2eSafeException("The role database contains duplicate E2E central sources.")
        };
    }

    private static SharedProviderSourceWriteRequest CreateSourceRequest(
        Uri baseUri,
        Guid tokenSecretId)
        => new(
            E2eFixtures.CentralSourceName,
            baseUri,
            tokenSecretId,
            IsEnabled: true,
            AllowInsecurePrivateNetwork: true);

    private async Task WriteSnapshotAsync(
        E2eRole role,
        CancellationToken cancellationToken)
    {
        var snapshot = await snapshots.CaptureAsync(role, cancellationToken);
        await artifacts.WriteSnapshotAsync(snapshot, cancellationToken);
    }

    private async Task WriteCheckpointSnapshotAsync(
        E2eRole role,
        string checkpoint,
        CancellationToken cancellationToken)
    {
        var snapshot = await snapshots.CaptureAsync(role, cancellationToken);
        await artifacts.WriteSnapshotCheckpointAsync(
            snapshot,
            checkpoint,
            cancellationToken);
    }
}
