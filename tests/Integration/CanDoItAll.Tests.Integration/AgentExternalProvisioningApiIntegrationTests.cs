using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.Modules.AgentFramework;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class AgentExternalProvisioningApiIntegrationTests
{
    private const string ExternalNamespace = "partner-system";
    private const string ExternalKey = "review-agent";

    [Fact]
    public async Task ProvisioningApi_ParallelReplayConflictGetAndArchive_PreserveOneStableBinding()
    {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: false);
        var editor = CreateEditor();
        var idempotencyKey = $"external-provision-{Guid.NewGuid():N}";

        var responses = await Task.WhenAll(
            PutAsync(host.Client, "Partner-System", "Review-Agent", editor, idempotencyKey),
            PutAsync(host.Client, "Partner-System", "Review-Agent", editor, idempotencyKey));
        using var firstResponse = responses[0];
        using var secondResponse = responses[1];
        var firstBody = await firstResponse.Content.ReadAsStringAsync();
        var secondBody = await secondResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, secondResponse.StatusCode);
        var receipts = new[]
        {
            Deserialize<AgentExternalProvisioningReceipt>(firstBody),
            Deserialize<AgentExternalProvisioningReceipt>(secondBody)
        };
        var created = Assert.Single(receipts, receipt => !receipt.Replayed);
        var replayed = Assert.Single(receipts, receipt => receipt.Replayed);
        Assert.True(created.Created);
        Assert.Equal(ExternalNamespace, created.Namespace);
        Assert.Equal(ExternalKey, created.Key);
        Assert.Equal(created.AgentId, replayed.AgentId);
        Assert.Equal(created.ConfigurationVersion, replayed.ConfigurationVersion);
        Assert.Equal(created.Archived, replayed.Archived);
        Assert.Empty(created.Warnings);
        Assert.Empty(replayed.Warnings);

        var changedEditor = CreateEditor();
        changedEditor.Summary = "A changed request must not reuse the completed idempotency key.";
        using var conflictResponse = await PutAsync(
            host.Client,
            ExternalNamespace,
            ExternalKey,
            changedEditor,
            idempotencyKey);
        var conflictBody = await conflictResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.Conflict, conflictResponse.StatusCode);
        Assert.Contains(
            "agents.external-key-idempotency-conflict",
            conflictBody,
            StringComparison.Ordinal);

        using var getResponse = await host.Client.GetAsync(
            $"/api/agents/by-external-key/{ExternalNamespace}/{ExternalKey}");
        var resource = await getResponse.Content.ReadFromJsonAsync<AgentExternalProvisioningResource>(
            JsonOptions);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.NotNull(resource);
        Assert.Equal(created.AgentId, resource.AgentId);
        Assert.Equal(created.ConfigurationVersion, resource.ConfigurationVersion);
        Assert.False(resource.IsArchived);
        Assert.Equal(QuoteEtag(created.ConfigurationVersion), getResponse.Headers.ETag?.Tag);

        var catalogAfterProvision = await LoadCatalogAsync(host);
        var bindingAfterProvision = Assert.Single(
            catalogAfterProvision.AgentExternalBindings,
            binding => binding.Namespace == ExternalNamespace && binding.Key == ExternalKey);
        Assert.Equal(created.AgentId, bindingAfterProvision.AgentId);
        Assert.Single(catalogAfterProvision.Agents, agent => agent.Id == created.AgentId);
        Assert.Single(
            catalogAfterProvision.AgentExternalProvisioningOperations,
            operation => operation.IdempotencyKey == idempotencyKey);

        using var staleArchiveResponse = await DeleteAsync(
            host.Client,
            ExternalNamespace,
            ExternalKey,
            $"archive-stale-{Guid.NewGuid():N}",
            new string('0', 64));
        var staleArchiveBody = await staleArchiveResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.PreconditionFailed, staleArchiveResponse.StatusCode);
        Assert.Contains(
            "agents.external-key-version-conflict",
            staleArchiveBody,
            StringComparison.Ordinal);
        var catalogAfterStaleArchive = await LoadCatalogAsync(host);
        var bindingAfterStaleArchive = Assert.Single(
            catalogAfterStaleArchive.AgentExternalBindings,
            binding => binding.Namespace == ExternalNamespace && binding.Key == ExternalKey);
        Assert.False(bindingAfterStaleArchive.IsArchived);
        Assert.Equal(created.ConfigurationVersion, bindingAfterStaleArchive.ConfigurationVersion);

        using var archiveResponse = await DeleteAsync(
            host.Client,
            ExternalNamespace,
            ExternalKey,
            $"archive-correct-{Guid.NewGuid():N}",
            created.ConfigurationVersion);
        var archiveBody = await archiveResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, archiveResponse.StatusCode);
        var archived = Deserialize<AgentExternalProvisioningReceipt>(archiveBody);
        Assert.True(archived.Archived);
        Assert.False(archived.Created);
        Assert.False(archived.Replayed);
        Assert.Equal(created.AgentId, archived.AgentId);
        Assert.NotEqual(created.ConfigurationVersion, archived.ConfigurationVersion);
        Assert.Equal(QuoteEtag(archived.ConfigurationVersion), archiveResponse.Headers.ETag?.Tag);

        using var archivedGetResponse = await host.Client.GetAsync(
            $"/api/agents/by-external-key/{ExternalNamespace}/{ExternalKey}");
        var archivedResource =
            await archivedGetResponse.Content.ReadFromJsonAsync<AgentExternalProvisioningResource>(
                JsonOptions);
        Assert.Equal(HttpStatusCode.OK, archivedGetResponse.StatusCode);
        Assert.NotNull(archivedResource);
        Assert.True(archivedResource.IsArchived);
        Assert.Equal(archived.ConfigurationVersion, archivedResource.ConfigurationVersion);
        Assert.Equal(
            QuoteEtag(archived.ConfigurationVersion),
            archivedGetResponse.Headers.ETag?.Tag);

        var catalogAfterArchive = await LoadCatalogAsync(host);
        var archivedBinding = Assert.Single(
            catalogAfterArchive.AgentExternalBindings,
            binding => binding.Namespace == ExternalNamespace && binding.Key == ExternalKey);
        Assert.True(archivedBinding.IsArchived);
        var archivedAgent = Assert.Single(
            catalogAfterArchive.Agents,
            agent => agent.Id == created.AgentId);
        Assert.Equal(AgentLifecycleStatus.Archived, archivedAgent.Status);
    }

    [Fact]
    public async Task ProvisioningApi_WhenAuthorizationIsEnabled_InheritsApiGroupAuthorization()
    {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: true);

        using var response = await host.Client.GetAsync(
            $"/api/agents/by-external-key/{ExternalNamespace}/{ExternalKey}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static async Task<HttpResponseMessage> PutAsync(
        HttpClient client,
        string externalNamespace,
        string key,
        AgentEditorModel editor,
        string idempotencyKey,
        string? expectedVersion = null)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Put,
            $"/api/agents/by-external-key/{externalNamespace}/{key}")
        {
            Content = JsonContent.Create(editor)
        };
        request.Headers.Add("Idempotency-Key", idempotencyKey);
        if (expectedVersion is not null)
        {
            request.Headers.IfMatch.Add(new EntityTagHeaderValue(QuoteEtag(expectedVersion)));
        }

        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> DeleteAsync(
        HttpClient client,
        string externalNamespace,
        string key,
        string idempotencyKey,
        string expectedVersion)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Delete,
            $"/api/agents/by-external-key/{externalNamespace}/{key}");
        request.Headers.Add("Idempotency-Key", idempotencyKey);
        request.Headers.IfMatch.Add(new EntityTagHeaderValue(QuoteEtag(expectedVersion)));
        return await client.SendAsync(request);
    }

    private static async Task<SandboxWorkspaceCatalog> LoadCatalogAsync(ApiTestHost host)
    {
        await using var scope = host.App.Services.CreateAsyncScope();
        var workspaceFactory =
            scope.ServiceProvider.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();
        var store = new FileSandboxWorkspaceStore(
            workspaceFactory.GetWorkspaceRoot(),
            workspaceFactory.GetOrganizationScope());
        return await store.LoadCatalogAsync();
    }

    private static AgentEditorModel CreateEditor()
    {
        return new AgentEditorModel
        {
            Name = "Partner API policy reviewer",
            RoleTitle = "Review specialist",
            Summary = "Reviews partner API automation policy.",
            Instructions = "Review the supplied API automation policy.",
            Status = AgentLifecycleStatus.Active,
            Model = "gpt-test",
            Workload = AgentWorkloadKind.Programming,
            ChatHistoryMode = AgentChatHistoryMode.FrameworkManaged,
            Temperature = 0.1,
            ConfigurationJson = "{}",
            TemplateKey = $"partner-api-policy-reviewer-{Guid.NewGuid():N}",
            Permissions = AgentPermissionsPolicy.Default,
            SelectedCapabilityIds = [],
            Tags = ["external-provisioning", "partner-api"]
        };
    }

    private static T Deserialize<T>(string json)
        where T : class
        => JsonSerializer.Deserialize<T>(json, JsonOptions)
           ?? throw new InvalidOperationException(
               $"Expected a {typeof(T).Name} JSON response but received: {json}");

    private static string QuoteEtag(string version) => $"\"{version}\"";

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);
}
