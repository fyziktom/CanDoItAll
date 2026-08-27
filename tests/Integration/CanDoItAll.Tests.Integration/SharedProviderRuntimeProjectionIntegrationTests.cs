using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Providers;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Security;
using CanDoItAll.Modules.AgentFramework.ProviderManagement;
using CanDoItAll.Security.Abstractions;
using CanDoItAll.SharedProviders.Abstractions;
using CanDoItAll.Modules.Workspace;
using Microsoft.Agents.AI;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Tests.Integration.SharedProviders;

using CanonicalProviderRuntimeProfile = CanDoItAll.Modules.AgentFramework.ProviderManagement.CanonicalProviderRuntimeProfile;
using IProviderRuntimeProfileSnapshotLoader = CanDoItAll.Modules.AgentFramework.ProviderManagement.IProviderRuntimeProfileSnapshotLoader;

using AgentProviderKind = CanDoItAll.AgentFramework.Models.ProviderKind;
using AgentProviderProfile = CanDoItAll.AgentFramework.Models.ProviderProfile;
using PersistedProviderKind = CanDoItAll.Modules.AgentFramework.ProviderManagement.ProviderKind;
using PersistedProviderProfile = CanDoItAll.Modules.AgentFramework.ProviderManagement.ProviderProfile;

public sealed class SharedProviderRuntimeProjectionIntegrationTests(
    SharedProviderRuntimeProjectionFixture fixture) :
    IClassFixture<SharedProviderRuntimeProjectionFixture>
{
    private const string SourceToken = "shared-source-token";
    private static readonly DateTimeOffset Now =
        new(2026, 8, 25, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Persisted_shared_graph_projects_through_materializer_mapper_snapshot_and_catalog()
    {
        var seed = await SeedGraphAsync();
        await using (var dbContext = await fixture.Factory.CreateDbContextAsync())
        {
            Assert.True(dbContext.Database.IsNpgsql());
            Assert.NotNull(await dbContext.Set<PersistedProviderProfile>()
                .SingleOrDefaultAsync(item => item.Id == seed.ProfileId));
            Assert.NotNull(await dbContext.Set<SharedProviderImport>()
                .SingleOrDefaultAsync(item => item.Id == seed.ImportId));
            Assert.NotNull(await dbContext.Set<SharedProviderSource>()
                .SingleOrDefaultAsync(item => item.Id == seed.SourceId));
        }

        await fixture.Services
            .GetRequiredService<IProviderRuntimeProfileSnapshotInitializer>()
            .InitializeAsync();
        await using var scope = fixture.Services.CreateAsyncScope();
        var catalogProfile = await scope.ServiceProvider
            .GetRequiredService<IProviderProfileRegistry>()
            .GetProviderAsync(seed.ProfileId);
        var snapshotProfile = await scope.ServiceProvider
            .GetRequiredService<IProviderRuntimeProfileSource>()
            .GetProviderAsync(seed.ProfileId);

        Assert.NotNull(catalogProfile);
        Assert.NotNull(snapshotProfile);
        Assert.Equal(seed.ProfileId, catalogProfile.Id);
        Assert.Equal(catalogProfile.Id, snapshotProfile.Id);
        Assert.Equal(catalogProfile.Name, snapshotProfile.Name);
        Assert.Equal(catalogProfile.Kind, snapshotProfile.Kind);
        Assert.Equal(catalogProfile.BaseUrl, snapshotProfile.BaseUrl);
        Assert.Equal(catalogProfile.DefaultModel, snapshotProfile.DefaultModel);
        Assert.Equal(catalogProfile.ConnectorPluginKey,
            snapshotProfile.ConnectorPluginKey);
        Assert.Equal(catalogProfile.CredentialBinding,
            snapshotProfile.CredentialBinding);
        Assert.Equal(catalogProfile.NetworkAccessPolicy,
            snapshotProfile.NetworkAccessPolicy);
        Assert.Equal(catalogProfile.FeatureConstraints,
            snapshotProfile.FeatureConstraints);
        Assert.Equal(catalogProfile.SuggestedModels,
            snapshotProfile.SuggestedModels);
        var catalogConstraint = Assert.IsType<
            ProviderModelSelectionConstraint>(
            catalogProfile.ModelSelectionConstraint);
        var snapshotConstraint = Assert.IsType<
            ProviderModelSelectionConstraint>(
            snapshotProfile.ModelSelectionConstraint);
        Assert.Equal(catalogProfile.SuggestedModels,
            catalogConstraint.AllowedModels);
        Assert.Equal(snapshotProfile.SuggestedModels,
            snapshotConstraint.AllowedModels);
        Assert.True(snapshotConstraint.Allows(snapshotProfile.DefaultModel));
        var changedConstraintProfile = snapshotProfile with
        {
            ModelSelectionConstraint = new ProviderModelSelectionConstraint(
                [snapshotProfile.DefaultModel, CreateForeignRoutingModel()])
        };
        Assert.NotEqual(
            ProviderConfigurationFingerprintFactory.Create(snapshotProfile),
            ProviderConfigurationFingerprintFactory.Create(
                changedConstraintProfile));
        Assert.NotEqual(
            ProviderRuntimeDescriptor.FromProfile(snapshotProfile).Key,
            ProviderRuntimeDescriptor.FromProfile(changedConstraintProfile).Key);
        Assert.Equal(catalogProfile.Tags, snapshotProfile.Tags);
        Assert.True(snapshotProfile.IsPrivateProvider);
        Assert.Equal(seed.Publication.Models[0].DisplayName,
            snapshotProfile.GetModelDisplayName(snapshotProfile.DefaultModel));
        Assert.Equal(seed.Publication.Models[0].Price,
            SharedProviderPriceMapper.ToCatalog(Assert.Single(snapshotProfile.ModelPrices)));
        Assert.Equal(seed.Publication.DefaultModelId.Value, snapshotProfile.DefaultModel);
        Assert.Equal(SharedProviderReconciliationCoordinator.ImportedConnectorPluginKey,
            snapshotProfile.ConnectorPluginKey);
    }

    [Fact]
    public async Task Composite_revision_changes_when_profile_token_changes()
    {
        await AssertCompositeRevisionChangesAsync(RevisionOwner.Profile);
    }

    [Fact]
    public async Task Composite_revision_changes_when_import_token_changes()
    {
        await AssertCompositeRevisionChangesAsync(RevisionOwner.Import);
    }

    [Fact]
    public async Task Composite_revision_changes_when_source_token_changes()
    {
        await AssertCompositeRevisionChangesAsync(RevisionOwner.Source);
    }

    [Fact]
    public async Task Projection_preserves_shared_origin_typed_credential_network_and_remote_capability_constraints()
    {
        var seed = await SeedGraphAsync(
            modelCapabilities:
            [
                [
                    SharedProviderCapability.Responses,
                    SharedProviderCapability.Streaming,
                    SharedProviderCapability.FunctionTools,
                    SharedProviderCapability.StructuredOutput,
                    SharedProviderCapability.VisionInput
                ],
                [
                    SharedProviderCapability.Responses,
                    SharedProviderCapability.Streaming
                ]
            ]);

        var projected = (await LoadCanonicalAsync(seed.ProfileId)).Profile;

        Assert.Equal(AgentProviderKind.OpenAi, projected.Kind);
        Assert.Equal(SharedProviderReconciliationCoordinator.ImportedConnectorPluginKey,
            projected.ConnectorPluginKey);
        Assert.Equal(ProviderNetworkAccessPolicy.PublicOnly,
            projected.NetworkAccessPolicy);
        var binding = Assert.IsType<ProviderCredentialBinding>(
            projected.CredentialBinding);
        Assert.Equal(seed.SecretId, binding.SecretId);
        Assert.Equal(ProviderCredentialPurpose.SourceAccessToken, binding.Purpose);
        Assert.Equal(ProviderCredentialConsumerKind.Source, binding.ConsumerKind);
        Assert.Equal(seed.SourceId, binding.ConsumerId);
        Assert.StartsWith("secret:", projected.ApiKeyEnvironmentVariable,
            StringComparison.Ordinal);
        Assert.True(projected.SupportsStreaming);
        Assert.False(projected.SupportsTools);
        var constraints = Assert.IsType<ProviderFeatureConstraints>(
            projected.FeatureConstraints);
        var modelConstraint = Assert.IsType<
            ProviderModelSelectionConstraint>(
            projected.ModelSelectionConstraint);
        Assert.Equal(
            projected.SuggestedModels,
            modelConstraint.AllowedModels);
        Assert.All(
            projected.SuggestedModels,
            model => Assert.True(modelConstraint.Allows(model)));
        Assert.False(constraints.AllowsStructuredOutput);
        Assert.False(constraints.AllowsVision);
        Assert.False(constraints.AllowsNativeTools);
        Assert.False(constraints.AllowsHostedMcp);
        Assert.False(ProviderAudioCapabilityPolicy.IsAvailable(projected));
        Assert.Contains("shared", projected.Tags);
        Assert.DoesNotContain(SourceToken, JsonSerializer.Serialize(projected),
            StringComparison.Ordinal);

        var secretRuntimeResolver = new CapturingSecretRuntimeResolver();
        var credentialResolver = new SecretStoreAgentProviderCredentialResolver(
            secretRuntimeResolver,
            new ConfigurationBuilder().Build());
        var resolution = credentialResolver.Resolve(projected);
        var request = Assert.IsType<SecretRuntimeRequest>(
            secretRuntimeResolver.Request);

        Assert.Equal(SourceToken, resolution.ApiKey);
        Assert.Equal(seed.SecretId, request.SecretId);
        Assert.Equal(SecretRuntimePurposes.SharedProviderSourceToken,
            request.Purpose);
        Assert.Equal([seed.SecretId], request.AllowedSecretIds);
        Assert.Equal(SecretRuntimeConsumerTypes.SharedProviderSource,
            request.ConsumerType);
        Assert.Equal(seed.SourceId.ToString("D"), request.ConsumerId);
    }

    [Fact]
    public async Task Hardened_selector_rejects_invalid_shared_binding_without_default_fallback()
    {
        var seed = await SeedGraphAsync();
        var projected = (await LoadCanonicalAsync(seed.ProfileId)).Profile;
        var invalid = projected with { CredentialBinding = null };
        var selector = fixture.Services
            .GetRequiredService<IProviderHttpClientSelector>();

        var exception = Assert.Throws<ProviderHttpClientSelectionException>(() =>
            selector.TryGetClient(invalid, out _));

        Assert.Equal(projected.Id, exception.ProviderId);
        Assert.DoesNotContain(projected.BaseUrl, exception.Message,
            StringComparison.Ordinal);
        Assert.DoesNotContain(projected.ApiKeyEnvironmentVariable, exception.Message,
            StringComparison.Ordinal);

        var missingConstraint = projected with
        {
            ModelSelectionConstraint = null
        };
        var constraintException = Assert.Throws<
            ProviderHttpClientSelectionException>(() =>
            selector.TryGetClient(missingConstraint, out _));
        Assert.Equal(projected.Id, constraintException.ProviderId);
        var modelException = Assert.Throws<
            ProviderModelSelectionException>(() =>
            ProviderModelSelectionPolicy.EnsureAllowed(
                missingConstraint,
                projected.DefaultModel));
        Assert.Equal(projected.Id, modelException.ProviderProfileId);
        Assert.Equal(
            ProviderModelSelectionException.PublicMessage,
            modelException.Message);

        var missingOrigin = projected with
        {
            ConnectorPluginKey = string.Empty,
            NetworkAccessPolicy = ProviderNetworkAccessPolicy.Default
        };
        Assert.Throws<ProviderHttpClientSelectionException>(() =>
            selector.TryGetClient(missingOrigin, out _));

        var insecurePublicProfile = projected with
        {
            BaseUrl = "http://127.0.0.1:43123/openai/v1",
            NetworkAccessPolicy = ProviderNetworkAccessPolicy.PublicOnly
        };
        Assert.Throws<ProviderHttpClientSelectionException>(() =>
            selector.TryGetClient(insecurePublicProfile, out _));
    }

    [Fact]
    public async Task Raw_openai_driver_routes_chat_completions_through_hardened_shared_client()
    {
        AgentProviderProfile? projectedProfile = null;
        var foreignModel = CreateForeignRoutingModel();
        await using var server = await DeterministicOpenAiServer.CreateAsync(
            request => request.PathAndQuery ==
                $"/tenant{SharedProviderRoutes.Models}"
                ? JsonSerializer.Serialize(new
                {
                    data = new[]
                    {
                        new { id = projectedProfile!.DefaultModel },
                        new { id = foreignModel }
                    }
                })
                : CreateChatCompletionResponse(request, "raw shared chat"));
        var profile = await ProjectRuntimeProfileAsync(
            server.SourceBaseUri,
            SharedProviderPurpose.Chat,
            [SharedProviderCapability.ChatCompletions]);
        projectedProfile = profile;
        using var defaultClient = new HttpClient(new ThrowingHandler());
        var driver = new OpenAiProviderDriver(
            defaultClient,
            FixedDriverCredentialResolver.Instance,
            fixture.Services.GetRequiredService<IProviderHttpClientSelector>());
        var listedModels = await driver.ListModelsAsync(
            new ProviderModelCatalogRequest(
                profile,
                AgentProviderCapabilityKind.ChatCompletion));
        var ambientContext = fixture.Services
            .GetRequiredService<IHttpContextAccessor>();
        ProviderChatCompletionResult firstResult;
        ProviderChatCompletionResult secondResult;
        ProviderChatCompletionResult noContextResult;
        try
        {
            await using (var requestServices =
                CreateAccessContextRequestServices("runtime-request-a"))
            {
                ambientContext.HttpContext = new DefaultHttpContext
                {
                    RequestServices = requestServices
                };
                firstResult = await driver.CompleteChatAsync(
                    CreateChatRequest(profile));
            }

            await using (var requestServices =
                CreateAccessContextRequestServices("runtime-request-b"))
            {
                ambientContext.HttpContext = new DefaultHttpContext
                {
                    RequestServices = requestServices
                };
                secondResult = await driver.CompleteChatAsync(
                    CreateChatRequest(profile));
            }

            ambientContext.HttpContext = null;
            noContextResult = await driver.CompleteChatAsync(
                CreateChatRequest(profile));
        }
        finally
        {
            ambientContext.HttpContext = null;
        }

        Assert.Equal("raw shared chat", firstResult.ResponseText);
        Assert.Equal("raw shared chat", secondResult.ResponseText);
        Assert.Equal("raw shared chat", noContextResult.ResponseText);
        Assert.Equal(
            profile.DefaultModel,
            Assert.Single(listedModels).Model);
        Assert.DoesNotContain(
            listedModels,
            model => model.Model == foreignModel);
        Assert.Collection(
            server.Requests,
            request =>
            {
                Assert.Equal(
                    $"/tenant{SharedProviderRoutes.Models}",
                    request.PathAndQuery);
                Assert.Empty(request.AccessContextReference);
            },
            request =>
            {
                AssertRuntimeRequest(
                    request,
                    SharedProviderRoutes.ChatCompletions,
                    profile.DefaultModel);
                Assert.Equal(
                    "runtime-request-a",
                    request.AccessContextReference);
            },
            request =>
            {
                AssertRuntimeRequest(
                    request,
                    SharedProviderRoutes.ChatCompletions,
                    profile.DefaultModel);
                Assert.Equal(
                    "runtime-request-b",
                    request.AccessContextReference);
            },
            request =>
            {
                AssertRuntimeRequest(
                    request,
                    SharedProviderRoutes.ChatCompletions,
                    profile.DefaultModel);
                Assert.Empty(request.AccessContextReference);
            });
    }

    [Fact]
    public async Task Raw_openai_driver_routes_responses_through_hardened_shared_client()
    {
        await using var server = await DeterministicOpenAiServer.CreateAsync(
            request => CreateResponsesResponse(request, "raw shared response"));
        var profile = await ProjectRuntimeProfileAsync(
            server.SourceBaseUri,
            SharedProviderPurpose.Chat,
            [SharedProviderCapability.Responses]);
        using var defaultClient = new HttpClient(new ThrowingHandler());
        var driver = new OpenAiProviderDriver(
            defaultClient,
            FixedDriverCredentialResolver.Instance,
            fixture.Services.GetRequiredService<IProviderHttpClientSelector>());

        var result = await driver.CompleteChatAsync(CreateChatRequest(profile));

        Assert.Equal("raw shared response", result.ResponseText);
        AssertRuntimeRequest(
            Assert.Single(server.Requests),
            SharedProviderRoutes.Responses,
            profile.DefaultModel);

        AgentProviderProfile disconnectedProfile;
        await using (var disconnectedServer =
            await DeterministicOpenAiServer.CreateAsync(
                request => CreateResponsesResponse(
                    request,
                    "unreachable response")))
        {
            disconnectedProfile = await ProjectRuntimeProfileAsync(
                disconnectedServer.SourceBaseUri,
                SharedProviderPurpose.Chat,
                [SharedProviderCapability.Responses]);
        }

        var runtimeFailure = await Assert.ThrowsAnyAsync<Exception>(() =>
            RunMafAgentAsync(
                disconnectedProfile,
                "private-prompt-must-not-escape"));
        var disclosedFailure = runtimeFailure.ToString();
        Assert.Contains(
            ProviderFailureDisclosurePolicy.SanitizedRuntimeFailureMessage,
            disclosedFailure,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            disconnectedProfile.BaseUrl,
            disclosedFailure,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            SourceToken,
            disclosedFailure,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "private-prompt-must-not-escape",
            disclosedFailure,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Raw_openai_driver_routes_images_through_hardened_shared_client()
    {
        await using var server = await DeterministicOpenAiServer.CreateAsync(
            _ => JsonSerializer.Serialize(new
            {
                data = new[]
                {
                    new
                    {
                        b64_json = Convert.ToBase64String([1, 2, 3, 4])
                    }
                }
            }));
        var profile = await ProjectRuntimeProfileAsync(
            server.SourceBaseUri,
            SharedProviderPurpose.ImageGeneration,
            [
                SharedProviderCapability.ImageGenerations,
                SharedProviderCapability.Base64Json
            ]);
        using var defaultClient = new HttpClient(new ThrowingHandler());
        var driver = new OpenAiProviderDriver(
            defaultClient,
            FixedDriverCredentialResolver.Instance,
            fixture.Services.GetRequiredService<IProviderHttpClientSelector>());

        var rejectedModel = CreateForeignRoutingModel();
        var exception = await Assert.ThrowsAsync<
            ProviderModelSelectionException>(() => driver.GenerateImageAsync(
                new ProviderImageGenerationRequest(
                    profile,
                    rejectedModel,
                    "draw a rejected red square",
                    "1024x1024",
                    "standard",
                    ProviderGeneratedImageFormat.Png,
                    [])));
        Assert.Equal(profile.Id, exception.ProviderProfileId);
        Assert.Equal(rejectedModel, exception.RequestedModel);
        Assert.Equal(
            ProviderModelSelectionException.PublicMessage,
            exception.Message);
        Assert.DoesNotContain(
            profile.Id.ToString("D"),
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
        Assert.Empty(server.Requests);

        var result = await driver.GenerateImageAsync(
            new ProviderImageGenerationRequest(
                profile,
                profile.DefaultModel,
                "draw a bounded blue square",
                "1024x1024",
                "standard",
                ProviderGeneratedImageFormat.Png,
                []));

        Assert.Equal([1, 2, 3, 4], Assert.Single(result.Images).Bytes);
        AssertRuntimeRequest(
            Assert.Single(server.Requests),
            SharedProviderRoutes.ImageGenerations,
            profile.DefaultModel);
    }

    [Fact]
    public async Task Maf_sdk_chat_completions_routes_through_hardened_shared_client()
    {
        await using var server = await DeterministicOpenAiServer.CreateAsync(
            request => CreateChatCompletionResponse(request, "sdk shared chat"));
        var profile = await ProjectRuntimeProfileAsync(
            server.SourceBaseUri,
            SharedProviderPurpose.Chat,
            [SharedProviderCapability.ChatCompletions]);

        var text = await RunMafAgentAsync(profile, "send a chat request");

        Assert.Equal("sdk shared chat", text);
        AssertRuntimeRequest(
            Assert.Single(server.Requests),
            SharedProviderRoutes.ChatCompletions,
            profile.DefaultModel);
    }

    [Fact]
    public async Task Maf_sdk_responses_routes_through_hardened_shared_client()
    {
        await using var server = await DeterministicOpenAiServer.CreateAsync(
            request => CreateResponsesResponse(request, "sdk shared response"));
        var profile = await ProjectRuntimeProfileAsync(
            server.SourceBaseUri,
            SharedProviderPurpose.Chat,
            [SharedProviderCapability.Responses]);

        var rejectedModel = CreateForeignRoutingModel();
        var exception = await Assert.ThrowsAsync<
            ProviderModelSelectionException>(() =>
            RunMafAgentAsync(
                profile,
                "reject a cross-publication model",
                rejectedModel));
        Assert.Equal(profile.Id, exception.ProviderProfileId);
        Assert.Equal(rejectedModel, exception.RequestedModel);
        Assert.Equal(
            ProviderModelSelectionException.PublicMessage,
            exception.Message);
        Assert.Empty(server.Requests);

        var text = await RunMafAgentAsync(profile, "send a responses request");

        Assert.Equal("sdk shared response", text);
        AssertRuntimeRequest(
            Assert.Single(server.Requests),
            SharedProviderRoutes.Responses,
            profile.DefaultModel);
    }

    [Fact]
    public async Task Personal_openai_profile_preserves_the_default_driver_client()
    {
        var handler = new CapturingHandler(
            request => CreateChatCompletionResponse(request, "personal default"));
        using var defaultClient = new HttpClient(handler);
        var selector = fixture.Services
            .GetRequiredService<IProviderHttpClientSelector>();
        var profile = CreatePersonalProfile();
        Assert.False(selector.TryGetClient(profile, out var selected));
        Assert.Null(selected);
        var driver = new OpenAiProviderDriver(
            defaultClient,
            FixedDriverCredentialResolver.Instance,
            selector);

        const string personalOverrideModel = "personal-override-model";
        var result = await driver.CompleteChatAsync(
            CreateChatRequest(profile, personalOverrideModel));

        Assert.Equal("personal default", result.ResponseText);
        var request = Assert.Single(handler.Requests);
        Assert.Equal("/v1/chat/completions", request.PathAndQuery);
        Assert.Equal($"Bearer {SourceToken}", request.Authorization);
        Assert.Equal(personalOverrideModel, ReadRequestedModel(request.Body));
    }

    [Fact]
    public async Task Production_di_registers_singleton_selector_snapshot_and_shared_manifest()
    {
        var rootSelector = fixture.Services
            .GetRequiredService<IProviderHttpClientSelector>();
        await using var firstScope = fixture.Services.CreateAsyncScope();
        await using var secondScope = fixture.Services.CreateAsyncScope();

        Assert.Same(rootSelector, firstScope.ServiceProvider
            .GetRequiredService<IProviderHttpClientSelector>());
        Assert.Same(
            firstScope.ServiceProvider.GetRequiredService<IProviderRuntimeProfileSource>(),
            secondScope.ServiceProvider.GetRequiredService<IProviderRuntimeProfileSource>());
        Assert.NotSame(
            firstScope.ServiceProvider.GetRequiredService<IProviderProfileRegistry>(),
            secondScope.ServiceProvider.GetRequiredService<IProviderProfileRegistry>());
        var manifest = firstScope.ServiceProvider
            .GetRequiredService<ConnectorPluginRegistry>()
            .Resolve(SharedProviderReconciliationCoordinator.ImportedConnectorPluginKey);
        Assert.True(manifest.Capabilities.HasFlag(
            ConnectorManifestCapability.ProviderExecution));
        Assert.True(manifest.Capabilities.HasFlag(
            ConnectorManifestCapability.AgentExposure));
        Assert.Empty(manifest.ConfigurationSchema.Fields);
        Assert.Empty(manifest.SecretRequirements);

        var healthService = firstScope.ServiceProvider
            .GetRequiredService<IProviderHealthCheckService>();
        var promptExecutionService = firstScope.ServiceProvider
            .GetRequiredService<IProviderPromptExecutionService>();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            healthService.CheckHealthAsync(Guid.NewGuid(), cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            promptExecutionService.ExecuteAsync(
                new ProviderPromptExecutionRequest(
                    Guid.NewGuid(),
                    "cancelled request"),
                cancellation.Token));
    }

    [Fact]
    public async Task Operationally_unavailable_shared_profile_is_retained_but_disabled()
    {
        var seed = await SeedGraphAsync(
            sourceStatus: SharedProviderSourceStatus.SourceOffline);
        await using var scope = fixture.Services.CreateAsyncScope();
        var projected = Assert.Single(
            await scope.ServiceProvider
                .GetRequiredService<IProviderProfileRegistry>()
                .ListProvidersAsync(),
            item => item.Id == seed.ProfileId);

        Assert.False(projected.IsEnabled);
        Assert.Equal(nameof(SharedProviderRuntimeProfileAvailability.SourceOffline),
            projected.HealthStatus);
        Assert.Contains("source-offline", projected.Tags);
        Assert.Equal(seed.ProfileId, projected.Id);
        var selector = fixture.Services
            .GetRequiredService<IProviderHttpClientSelector>();
        Assert.Throws<ProviderHttpClientSelectionException>(() =>
            selector.TryGetClient(projected, out _));
    }

    [Fact]
    public async Task Corrupt_or_missing_shared_graph_is_omitted_from_runtime_catalog()
    {
        var corrupt = await SeedGraphAsync();
        await using (var dbContext = await fixture.Factory.CreateDbContextAsync())
        {
            var import = await dbContext.Set<SharedProviderImport>()
                .SingleAsync(item => item.Id == corrupt.ImportId);
            import.RemoteCatalogSnapshotJson = "{}";
            await dbContext.SaveChangesAsync();
        }

        var missingProfileId = await SeedProfileWithoutImportAsync();
        await using var scope = fixture.Services.CreateAsyncScope();
        var ids = (await scope.ServiceProvider
                .GetRequiredService<IProviderProfileRegistry>()
                .ListProvidersAsync())
            .Select(item => item.Id)
            .ToHashSet();

        Assert.DoesNotContain(corrupt.ProfileId, ids);
        Assert.DoesNotContain(missingProfileId, ids);
    }

    [Fact]
    public void Inner_provider_runtime_has_no_workspace_or_shared_provider_dependency()
    {
        var innerAssemblies = new[]
        {
            typeof(OpenAiProviderDriver).Assembly,
            typeof(MafProviderAgentFactory).Assembly
        };

        foreach (var assembly in innerAssemblies)
        {
            var references = assembly.GetReferencedAssemblies()
                .Select(item => item.Name ?? string.Empty)
                .ToArray();
            Assert.DoesNotContain(references, item => string.Equals(
                item,
                "CanDoItAll.Modules.Workspace",
                StringComparison.Ordinal));
            Assert.DoesNotContain(references, item => item.StartsWith(
                "CanDoItAll.SharedProviders",
                StringComparison.Ordinal));
        }
    }

    private async Task AssertCompositeRevisionChangesAsync(RevisionOwner owner)
    {
        var seed = await SeedGraphAsync();
        var before = await LoadRevisionAsync(seed.ProfileId);
        await using (var dbContext = await fixture.Factory.CreateDbContextAsync())
        {
            switch (owner)
            {
                case RevisionOwner.Profile:
                    (await dbContext.Set<PersistedProviderProfile>()
                        .SingleAsync(item => item.Id == seed.ProfileId)).ConcurrencyToken =
                        Guid.NewGuid();
                    break;
                case RevisionOwner.Import:
                    (await dbContext.Set<SharedProviderImport>()
                        .SingleAsync(item => item.Id == seed.ImportId)).ConcurrencyToken =
                        Guid.NewGuid();
                    break;
                case RevisionOwner.Source:
                    (await dbContext.Set<SharedProviderSource>()
                        .SingleAsync(item => item.Id == seed.SourceId)).ConcurrencyToken =
                        Guid.NewGuid();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(owner), owner, null);
            }

            await dbContext.SaveChangesAsync();
        }

        var after = await LoadRevisionAsync(seed.ProfileId);
        Assert.NotEqual(before, after);
    }

    private async Task<ProviderConfigurationRevision> LoadRevisionAsync(Guid profileId)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var revision = await scope.ServiceProvider
            .GetRequiredService<IProviderRuntimeProfileSnapshotLoader>()
            .LoadRevisionAsync(profileId);
        Assert.True(revision.HasValue);
        return revision.Value;
    }

    private async Task<CanonicalProviderRuntimeProfile> LoadCanonicalAsync(
        Guid profileId)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var canonical = await scope.ServiceProvider
            .GetRequiredService<IProviderRuntimeProfileSnapshotLoader>()
            .LoadAsync(profileId);
        return Assert.IsType<CanonicalProviderRuntimeProfile>(canonical);
    }

    private async Task<AgentProviderProfile> ProjectRuntimeProfileAsync(
        Uri sourceBaseUri,
        SharedProviderPurpose purpose,
        IReadOnlyList<SharedProviderCapability> capabilities)
    {
        var seed = await SeedGraphAsync(
            sourceBaseUri,
            purpose,
            modelCapabilities: [capabilities]);
        return (await LoadCanonicalAsync(seed.ProfileId)).Profile;
    }

    private async Task<string> RunMafAgentAsync(
        AgentProviderProfile profile,
        string prompt,
        string? requestedModel = null)
    {
        var factory = new MafProviderAgentFactory(
            new MafProviderCredentialService(FixedAgentCredentialResolver.Instance),
            NoOpMafProviderStreamingDispatchGate.Instance,
            loggerFactory: null,
            fixture.Services.GetRequiredService<IProviderHttpClientSelector>());
        var agent = factory.CreateFrameworkAgent(
            profile,
            requestedModel ?? profile.DefaultModel,
            MafChatClientAgentOptionsFactory.Create(new ChatOptions()),
            frameworkManagedHistory: true,
            allowBackgroundResponses: false);
        try
        {
            return (await agent.RunAsync(prompt)).Text;
        }
        finally
        {
            await DisposeAgentAsync(agent);
        }
    }

    private async Task<GraphSeed> SeedGraphAsync(
        Uri? sourceBaseUri = null,
        SharedProviderPurpose purpose = SharedProviderPurpose.Chat,
        SharedProviderSourceStatus sourceStatus =
            SharedProviderSourceStatus.Available,
        IReadOnlyList<IReadOnlyList<SharedProviderCapability>>?
            modelCapabilities = null)
    {
        var graph = CreateGraph(
            sourceBaseUri ?? new Uri(
                $"https://central.example.test/{Guid.NewGuid():N}/"),
            purpose,
            sourceStatus,
            modelCapabilities);
        await using var dbContext = await fixture.Factory.CreateDbContextAsync();
        dbContext.AddRange(graph.Secret, graph.Source, graph.Profile, graph.Import);
        await dbContext.SaveChangesAsync();
        return new GraphSeed(
            graph.Profile.Id,
            graph.Import.Id,
            graph.Source.Id,
            graph.Secret.Id,
            graph.Publication);
    }

    private async Task<Guid> SeedProfileWithoutImportAsync()
    {
        var graph = CreateGraph(
            new Uri($"https://missing.example.test/{Guid.NewGuid():N}/"),
            SharedProviderPurpose.Chat,
            SharedProviderSourceStatus.Available,
            modelCapabilities: null);
        await using var dbContext = await fixture.Factory.CreateDbContextAsync();
        dbContext.AddRange(graph.Secret, graph.Profile);
        await dbContext.SaveChangesAsync();
        return graph.Profile.Id;
    }

    private static UnpersistedGraph CreateGraph(
        Uri sourceBaseUri,
        SharedProviderPurpose purpose,
        SharedProviderSourceStatus sourceStatus,
        IReadOnlyList<IReadOnlyList<SharedProviderCapability>>?
            modelCapabilities)
    {
        var secret = new SecretRecord
        {
            Name = "Shared source token",
            Kind = SecretKind.Token,
            EncryptedPayload = "test-only-vault-reference",
            Scope = "workspace",
            MetadataJson = "{}",
            CreatedAtUtc = Now,
            UpdatedAtUtc = Now
        };
        var publicationId = new SharedProviderPublicationId(Guid.NewGuid());
        modelCapabilities ??=
        [
            purpose == SharedProviderPurpose.ImageGeneration
                ?
                [
                    SharedProviderCapability.ImageGenerations,
                    SharedProviderCapability.Base64Json
                ]
                :
                [
                    SharedProviderCapability.Responses,
                    SharedProviderCapability.Streaming
                ]
        ];
        var models = modelCapabilities
            .Select((capabilities, index) => new SharedProviderCatalogModel(
                SharedProviderRoutingModelIdCodec.Create(
                    publicationId,
                    $"upstream-model-{index + 1}"),
                $"Remote model {index + 1}",
                Array.AsReadOnly(capabilities.ToArray())) { Price = new(1.25m, 0m, 2.75m) })
            .ToArray();
        var publication = new SharedProviderCatalogPublication(
            publicationId,
            new SharedProviderPublicRevision(
                $"{SharedProviderPublicRevision.Prefix}{new string('0', SharedProviderPublicRevision.HashLength)}"),
            "Remote shared provider",
            purpose,
            SharedProviderTransport.OpenAiCompatible,
            models[0].Id,
            Array.AsReadOnly(models),
            new SharedProviderCatalogHealth(SharedProviderHealthState.Available)) { IsPrivateProvider = true };
        publication = publication with
        {
            Revision = SharedProviderCanonicalRevision.ComputePublication(publication)
        };
        var allowPrivateNetwork = sourceBaseUri.Scheme == Uri.UriSchemeHttp;
        var source = SharedProviderSourceTransitions.Create(
            "Central source",
            sourceBaseUri.AbsoluteUri,
            secret.Id,
            allowPrivateNetwork,
            isEnabled: true,
            Now);
        source.Status = sourceStatus;
        source.RemoteInstanceId = new SharedProviderSourceInstanceId(Guid.NewGuid());
        source.LastSyncAtUtc = Now;
        source.LastStatusCode = sourceStatus == SharedProviderSourceStatus.Available
            ? HttpStatusCode.OK.GetHashCode()
            : null;
        source.LastStatusMessage = sourceStatus == SharedProviderSourceStatus.Available
            ? "Catalog synchronized."
            : "Source unavailable.";
        source.ConcurrencyToken = Guid.NewGuid();
        var defaultCapabilities = publication.Models[0].Capabilities;
        var profile = new PersistedProviderProfile
        {
            Name = "Local shared alias",
            ProviderKind = PersistedProviderKind.OpenAi,
            ConnectorPluginKey =
                SharedProviderReconciliationCoordinator.ImportedConnectorPluginKey,
            ConfigSchemaVersion =
                SharedProviderReconciliationCoordinator.ImportedConfigurationSchemaVersion,
            BaseUrl = SharedProviderRoutes.ResolveOpenAiBase(
                new Uri(source.BaseUri)).AbsoluteUri,
            ApiKeySecretId = secret.Id,
            DefaultModel = publication.DefaultModelId.Value,
            TimeoutSeconds = 45,
            IsEnabled = true,
            SupportsStreaming = defaultCapabilities.Contains(
                SharedProviderCapability.Streaming),
            SupportsToolCalling = defaultCapabilities.Contains(
                SharedProviderCapability.FunctionTools),
            SupportsStructuredOutput = defaultCapabilities.Contains(
                SharedProviderCapability.StructuredOutput),
            SupportsVision = defaultCapabilities.Contains(
                SharedProviderCapability.VisionInput),
            ExtraSettingsJson = "{}",
            LastHealthStatus = "source-managed",
            ConcurrencyToken = Guid.NewGuid()
        };
        var import = SharedProviderImportTransitions.Create(
            source.Id,
            profile.Id,
            SharedProviderRemotePublicationState.Create(publication),
            Now);
        import.ConcurrencyToken = Guid.NewGuid();
        return new UnpersistedGraph(
            secret,
            source,
            profile,
            import,
            publication);
    }

    private static ProviderChatCompletionRequest CreateChatRequest(
        AgentProviderProfile profile,
        string? requestedModel = null)
        => new(
            profile,
            requestedModel ?? profile.DefaultModel,
            "system",
            [],
            "return the deterministic response");

    private static string CreateForeignRoutingModel()
        => SharedProviderRoutingModelIdCodec.Create(
            new SharedProviderPublicationId(Guid.NewGuid()),
            "upstream-model-1").Value;

    private static AgentProviderProfile CreatePersonalProfile()
        => new(
            Guid.NewGuid(),
            "Personal OpenAI",
            AgentProviderKind.OpenAi,
            "https://personal.example.test/v1",
            "PERSONAL_TEST_KEY",
            "personal-model",
            ProviderTransportKind.ChatCompletions,
            IsEnabled: true,
            SupportsStreaming: false,
            SupportsTools: false,
            PreferFrameworkManagedChatHistory: true,
            SupportsBackgroundResponses: false,
            ConfigurationJson: "{}",
            Notes: string.Empty,
            HealthStatus: "ok",
            LastCheckedAtUtc: null,
            SuggestedModels: ["personal-model"])
        {
            ConnectorPluginKey = CanDoItAll.Modules.AgentFramework.ProviderManagement.ProviderConnectorKeys.OpenAi,
            NetworkAccessPolicy = ProviderNetworkAccessPolicy.Default
        };

    private static ServiceProvider CreateAccessContextRequestServices(
        string value)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAccessContextReferenceAccessor>(
            new FixedAccessContextReferenceAccessor(
                new AccessContextReference(value)));
        return services.BuildServiceProvider();
    }

    private static string CreateChatCompletionResponse(
        CapturedOpenAiRequest request,
        string text)
        => JsonSerializer.Serialize(new
        {
            id = "chatcmpl-shared-runtime",
            @object = "chat.completion",
            created = 1_787_616_000,
            model = ReadRequestedModel(request.Body),
            choices = new[]
            {
                new
                {
                    index = 0,
                    message = new
                    {
                        role = "assistant",
                        content = text
                    },
                    finish_reason = "stop"
                }
            },
            usage = new
            {
                prompt_tokens = 2,
                completion_tokens = 3,
                total_tokens = 5
            }
        });

    private static string CreateResponsesResponse(
        CapturedOpenAiRequest request,
        string text)
        => JsonSerializer.Serialize(new
        {
            id = "resp_shared_runtime",
            @object = "response",
            created_at = 1_787_616_000,
            status = "completed",
            model = ReadRequestedModel(request.Body),
            output_text = text,
            output = new[]
            {
                new
                {
                    id = "msg_shared_runtime",
                    type = "message",
                    status = "completed",
                    role = "assistant",
                    content = new[]
                    {
                        new
                        {
                            type = "output_text",
                            text,
                            annotations = Array.Empty<object>()
                        }
                    }
                }
            },
            parallel_tool_calls = false,
            tools = Array.Empty<object>(),
            usage = new
            {
                input_tokens = 2,
                output_tokens = 3,
                total_tokens = 5
            }
        });

    private static string ReadRequestedModel(string body)
    {
        using var document = JsonDocument.Parse(body);
        return document.RootElement.GetProperty("model").GetString()
            ?? throw new InvalidOperationException(
                "The deterministic request did not contain a model.");
    }

    private static void AssertRuntimeRequest(
        CapturedOpenAiRequest request,
        string expectedRoute,
        string expectedModel)
    {
        Assert.Equal($"/tenant{expectedRoute}", request.PathAndQuery);
        Assert.Equal($"Bearer {SourceToken}", request.Authorization);
        Assert.Equal(expectedModel, ReadRequestedModel(request.Body));
    }

    private static async ValueTask DisposeAgentAsync(AIAgent agent)
    {
        switch (agent)
        {
            case IAsyncDisposable asyncDisposable:
                await asyncDisposable.DisposeAsync();
                break;
            case IDisposable disposable:
                disposable.Dispose();
                break;
        }
    }

    private enum RevisionOwner
    {
        Profile,
        Import,
        Source
    }

    private sealed record GraphSeed(
        Guid ProfileId,
        Guid ImportId,
        Guid SourceId,
        Guid SecretId,
        SharedProviderCatalogPublication Publication);

    private sealed record UnpersistedGraph(
        SecretRecord Secret,
        SharedProviderSource Source,
        PersistedProviderProfile Profile,
        SharedProviderImport Import,
        SharedProviderCatalogPublication Publication);

    private sealed class FixedDriverCredentialResolver :
        IProviderDriverCredentialResolver
    {
        public static FixedDriverCredentialResolver Instance { get; } = new();

        public ProviderDriverCredential Resolve(AgentProviderProfile provider)
            => ProviderDriverCredential.Resolved(SourceToken);
    }

    private sealed class FixedAgentCredentialResolver :
        IAgentProviderCredentialResolver
    {
        public static FixedAgentCredentialResolver Instance { get; } = new();

        public ProviderCredentialResolution Resolve(AgentProviderProfile provider)
            => new(SourceToken, "shared source test token", string.Empty);
    }

    private sealed class FixedAccessContextReferenceAccessor(
        AccessContextReference current) : IAccessContextReferenceAccessor
    {
        public AccessContextReference? Current { get; } = current;
    }

    private sealed class CapturingSecretRuntimeResolver :
        ISecretRuntimeResolver
    {
        public SecretRuntimeRequest? Request { get; private set; }

        public Task<string?> ResolveValueAsync(
            SecretRuntimeRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Request = request;
            return Task.FromResult<string?>(SourceToken);
        }
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => throw new InvalidOperationException(
                "A shared profile attempted to use the personal default HTTP client.");
    }

    private sealed class CapturingHandler(
        Func<CapturedOpenAiRequest, string> responseFactory) : HttpMessageHandler
    {
        public ConcurrentQueue<CapturedOpenAiRequest> Requests { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var captured = new CapturedOpenAiRequest(
                request.RequestUri?.PathAndQuery ?? string.Empty,
                request.Headers.Authorization?.ToString() ?? string.Empty,
                request.Content is null
                    ? string.Empty
                    : await request.Content.ReadAsStringAsync(cancellationToken));
            Requests.Enqueue(captured);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    responseFactory(captured),
                    Encoding.UTF8,
                    "application/json")
            };
        }
    }

    private sealed record CapturedOpenAiRequest(
        string PathAndQuery,
        string Authorization,
        string Body,
        string AccessContextReference = "");

    private sealed class DeterministicOpenAiServer : IAsyncDisposable
    {
        private readonly WebApplication application;

        private DeterministicOpenAiServer(
            WebApplication application,
            Uri sourceBaseUri,
            ConcurrentQueue<CapturedOpenAiRequest> requests)
        {
            this.application = application;
            SourceBaseUri = sourceBaseUri;
            Requests = requests;
        }

        public Uri SourceBaseUri { get; }

        public ConcurrentQueue<CapturedOpenAiRequest> Requests { get; }

        public static async Task<DeterministicOpenAiServer> CreateAsync(
            Func<CapturedOpenAiRequest, string> responseFactory)
        {
            var builder = WebApplication.CreateSlimBuilder();
            builder.Logging.ClearProviders();
            builder.WebHost.ConfigureKestrel(options =>
                options.Listen(IPAddress.Loopback, 0));
            var application = builder.Build();
            var requests = new ConcurrentQueue<CapturedOpenAiRequest>();
            application.MapMethods(
                "/{**path}",
                [HttpMethods.Get, HttpMethods.Post],
                async context =>
            {
                using var reader = new StreamReader(
                    context.Request.Body,
                    Encoding.UTF8,
                    detectEncodingFromByteOrderMarks: false,
                    leaveOpen: true);
                var captured = new CapturedOpenAiRequest(
                    $"{context.Request.Path}{context.Request.QueryString}",
                    context.Request.Headers.Authorization.ToString(),
                    await reader.ReadToEndAsync(context.RequestAborted),
                    context.Request.Headers[
                        SharedProviderHeaders.AccessContextReference]
                        .ToString());
                requests.Enqueue(captured);
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(
                    responseFactory(captured),
                    context.RequestAborted);
            });
            await application.StartAsync();
            var address = application.Services
                .GetRequiredService<IServer>()
                .Features
                .Get<IServerAddressesFeature>()!
                .Addresses
                .Single();
            return new DeterministicOpenAiServer(
                application,
                new Uri($"{address.TrimEnd('/')}/tenant/"),
                requests);
        }

        public async ValueTask DisposeAsync()
        {
            await application.StopAsync();
            await application.DisposeAsync();
        }
    }
}

public sealed class SharedProviderRuntimeProjectionFixture : IAsyncLifetime
{
    private ApiTestHost? host;

    internal IServiceProvider Services => RequireHost().App.Services;

    internal IDbContextFactory<AppDbContext> Factory => Services
        .GetRequiredService<IDbContextFactory<AppDbContext>>();

    public async Task InitializeAsync()
    {
        host = await ApiTestHost.CreateAsync(jwtEnabled: false);
    }

    public async Task DisposeAsync()
    {
        if (host is not null)
        {
            await host.DisposeAsync();
        }
    }

    private ApiTestHost RequireHost()
        => host ?? throw new InvalidOperationException(
            "The shared-provider runtime projection fixture is not initialized.");
}
