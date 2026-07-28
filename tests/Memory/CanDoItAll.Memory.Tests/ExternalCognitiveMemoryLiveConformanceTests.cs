using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;
using CanDoItAll.Memory.Drivers.CognitiveMemory;
using CanDoItAll.Memory.Http;
using CanDoItAll.Memory.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Memory.Tests;

public sealed class ExternalCognitiveMemoryLiveConformanceTests
{
    private const string BaseUrlEnvironmentVariable = "CANDOITALL_EXTERNAL_MEMORY_BASE_URL";
    private const string ApiKeyEnvironmentVariable = "CANDOITALL_EXTERNAL_MEMORY_API_KEY";
    private const string ProjectIdEnvironmentVariable = "CANDOITALL_EXTERNAL_MEMORY_PROJECT_ID";
    private static readonly MemoryProviderInstanceId ProviderId =
        MemoryProviderInstanceId.Parse("provider.cognitive-memory.live");

    [ExternalCognitiveMemoryFact]
    [Trait("Category", "LiveProcess")]
    public async Task Native_remote_driver_and_generic_handler_interoperate_with_isolated_service()
    {
        var baseUrl = new Uri(Environment.GetEnvironmentVariable(BaseUrlEnvironmentVariable)!);
        var projectId = Guid.Parse(Environment.GetEnvironmentVariable(ProjectIdEnvironmentVariable)!);
        var apiKey = Environment.GetEnvironmentVariable(ApiKeyEnvironmentVariable)!;
        await SeedExternalMemoryAsync(baseUrl, projectId, apiKey);

        using var root = CreateServiceProvider();
        using var scope = root.CreateScope();
        var services = scope.ServiceProvider;
        var profile = CreateProfile(baseUrl);
        await services.GetRequiredService<IMemoryProviderProfileStore>()
            .UpsertAsync(profile, DateTimeOffset.UtcNow);

        var query = new MemoryContextQueryRequest(
            "external wire compatibility evidence",
            [MemoryCapabilityIds.ContextQuerySync],
            MemorySourceProvenance.None)
        {
            Context = MemoryRequestContext.Default with
            {
                Execution = new MemoryExecutionContext(
                    projectId.ToString("D"),
                    "External memory conformance",
                    null,
                    null,
                    null,
                    null,
                    null,
                    [])
            }
        };
        var policy = MemoryProviderSelectionPolicy.RequireCapability(
            MemoryCapabilityIds.ContextQuerySync) with
        {
            ExplicitProviderId = ProviderId,
            AllowedProviderIds = [ProviderId]
        };
        var result = await services.GetRequiredService<IMemoryOperationHandler>()
            .ExecuteQueryAsync(MemoryOperationRequestBuilder.Query(
                MemoryOperationCaller.Tool("test.external-memory.live", CreateLedgerRequester()),
                policy,
                query,
                CreateRetention()));

        Assert.Equal(MemoryOperationHandlerStatus.Completed, result.Status);
        Assert.True(result.DriverDispatchAttempted);
        Assert.Equal(ProviderId, result.Selection.SelectedProvider?.InstanceId);
        Assert.Contains(result.Output!.Sections, section =>
            section.Text.Contains("external wire compatibility evidence", StringComparison.OrdinalIgnoreCase));
        var operation = await services.GetRequiredService<IMemoryOperationLedgerStore>()
            .GetAsync(result.OperationRecord!.OperationId);
        Assert.Equal(MemoryLedgerStatus.Completed, operation?.Status);
        Assert.Equal(result.OperationRecord.CorrelationId, operation?.CorrelationId);
    }

    private static async Task SeedExternalMemoryAsync(
        Uri baseUrl,
        Guid projectId,
        string apiKey)
    {
        using var client = new HttpClient { BaseAddress = baseUrl };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        var ingestion = MemoryOperationEnvelope.Create(
            ProviderId,
            MemoryOperationKind.Ingestion,
            MemoryRequesterContext.Agent(
                "live-conformance",
                "Seed external memory interoperability evidence.",
                "agent-live-conformance",
                "test",
                "session-live-conformance"),
            new MemoryWorkspaceContext(
                "workspace-live-conformance",
                "External memory conformance",
                null,
                "testing",
                []),
            new MemoryExecutionContext(
                projectId.ToString("D"),
                "External memory conformance",
                null,
                null,
                null,
                null,
                null,
                []),
            MemoryPolicyContext.InternalDefault,
            MemoryBudget.Default,
            new MemoryIngestionRequest(
                MemorySourceSnapshotId.Parse($"snapshot.live.{Guid.NewGuid():N}"),
                MemorySourceKind.ManualPayload,
                MemoryPayload.FromText(
                    "External wire compatibility evidence from the real NativeRemote driver conformance test."),
                [MemoryCapabilityIds.IngestionSnapshot]));

        using var response = await client.PostAsJsonAsync("/memory/ingest", ingestion);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<MemoryOperationResult>();
        Assert.Equal(MemoryOperationStatus.Succeeded, result?.Status);
    }

    private static ServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContextFactory<AppDbContext>(options =>
            options.UseInMemoryDatabase($"external-memory-live-{Guid.NewGuid():N}"));
        services.AddGenericMemoryModule();
        services.AddNativeRemoteMemoryProviderDriver();
        return services.BuildServiceProvider(validateScopes: true);
    }

    private static MemoryProviderProfile CreateProfile(Uri baseUrl)
    {
        return new MemoryProviderProfile(
            ProviderId,
            "External Cognitive Memory live service",
            MemoryProviderDriverKind.NativeRemote,
            IsEnabled: true,
            MemoryProviderHealthState.Healthy,
            MemoryProviderWorkspaceScope.AllWorkspaces,
            ["external", "live-conformance"],
            MemoryProviderProfilePolicy.Default,
            new MemoryProviderManifest(
                MemoryProviderKind.Parse("memory.cognitive-native"),
                MemoryProtocolVersion.Current,
                [new MemoryCapabilityDescriptor(MemoryCapabilityIds.ContextQuerySync, "1", Supported: true)],
                MemoryProviderInteractionSupport.SyncQueryOnly,
                [],
                MemoryProviderLimits.Default,
                MemoryExtensionData.From(
                    (NativeRemoteMemoryProviderConfigurationKeys.ServiceBaseUrl,
                        JsonSerializer.SerializeToElement(baseUrl.ToString())),
                    (NativeRemoteMemoryProviderConfigurationKeys.ApiKeyEnvironmentVariable,
                        JsonSerializer.SerializeToElement(ApiKeyEnvironmentVariable)))));
    }

    private static MemoryLedgerRequester CreateLedgerRequester()
    {
        return new MemoryLedgerRequester(
            "live-conformance",
            "agent-live-conformance",
            "test",
            "session-live-conformance",
            null,
            null,
            null,
            null);
    }

    private static MemoryLedgerRetentionPolicy CreateRetention()
    {
        var now = DateTimeOffset.UtcNow;
        return MemoryLedgerRetentionPolicy.Expiring(now.AddDays(1), now.AddDays(7));
    }
}

internal sealed class ExternalCognitiveMemoryFactAttribute : FactAttribute
{
    public ExternalCognitiveMemoryFactAttribute()
    {
        if (!Uri.TryCreate(
                Environment.GetEnvironmentVariable("CANDOITALL_EXTERNAL_MEMORY_BASE_URL"),
                UriKind.Absolute,
                out _) ||
            string.IsNullOrWhiteSpace(
                Environment.GetEnvironmentVariable("CANDOITALL_EXTERNAL_MEMORY_API_KEY")) ||
            !Guid.TryParse(
                Environment.GetEnvironmentVariable("CANDOITALL_EXTERNAL_MEMORY_PROJECT_ID"),
                out _))
        {
            Skip = "Set the external Cognitive Memory live-process environment variables to run this conformance test.";
        }
    }
}
