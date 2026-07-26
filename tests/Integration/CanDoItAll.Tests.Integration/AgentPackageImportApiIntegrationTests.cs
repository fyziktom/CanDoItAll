using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Integration;

public sealed class AgentPackageImportApiIntegrationTests
{
    [Fact]
    public async Task ImportPackage_CreateReplayAndConflictingReplay_HaveStableHttpSemantics()
    {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: false);
        var agent = CreateAgent();
        var packageBytes = CreatePackage(agent);
        var idempotencyKey = $"package-import-{Guid.NewGuid():N}";
        var externalKey = $"partner-agent-{Guid.NewGuid():N}";

        using var createResponse = await SendImportAsync(
            host.Client,
            packageBytes,
            idempotencyKey,
            externalKey);
        var createBody = await createResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = JsonSerializer.Deserialize<AgentPackageImportReceipt>(createBody, JsonOptions);
        Assert.NotNull(created);
        Assert.Equal(agent.Id, created.AgentId);
        Assert.Equal(AgentPackageImportMode.Create, created.Mode);
        Assert.Equal(externalKey, created.ExternalKey);
        Assert.Equal("1.0", created.PackageSchemaVersion);
        Assert.False(created.Replayed);
        Assert.Equal($"/api/agents/{agent.Id:D}", createResponse.Headers.Location?.OriginalString);

        using var readResponse = await host.Client.GetAsync($"/api/agents/{agent.Id:D}");
        var readBody = await readResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, readResponse.StatusCode);
        using (var agentDocument = JsonDocument.Parse(readBody))
        {
            Assert.Equal(agent.Name, agentDocument.RootElement.GetProperty("name").GetString());
        }

        using var bindingResponse = await host.Client.GetAsync(
            $"/api/agents/by-external-key/{AgentExternalIdentityNormalizer.PackageImportNamespace}/{externalKey}");
        var binding = await bindingResponse.Content.ReadFromJsonAsync<AgentExternalProvisioningResource>(
            JsonOptions);
        Assert.Equal(HttpStatusCode.OK, bindingResponse.StatusCode);
        Assert.NotNull(binding);
        Assert.Equal(agent.Id, binding.AgentId);
        Assert.Equal(created.ConfigurationSha256, binding.ConfigurationVersion);

        using var replayResponse = await SendImportAsync(
            host.Client,
            packageBytes,
            idempotencyKey,
            externalKey);
        var replayBody = await replayResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, replayResponse.StatusCode);
        var replayed = JsonSerializer.Deserialize<AgentPackageImportReceipt>(replayBody, JsonOptions);
        Assert.NotNull(replayed);
        Assert.True(replayed.Replayed);
        Assert.Equal(created.AgentId, replayed.AgentId);
        Assert.Equal(created.PackageSha256, replayed.PackageSha256);
        Assert.Equal(created.ConfigurationSha256, replayed.ConfigurationSha256);

        using var conflictResponse = await SendImportAsync(
            host.Client,
            packageBytes,
            idempotencyKey,
            $"{externalKey}-changed");
        var conflictBody = await conflictResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Conflict, conflictResponse.StatusCode);
        Assert.Contains("agent-package.idempotency-conflict", conflictBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ImportPackage_WhenAuthorizationIsEnabled_RequiresBearerToken()
    {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: true);
        var packageBytes = CreatePackage(CreateAgent());

        using var response = await SendImportAsync(
            host.Client,
            packageBytes,
            $"unauthorized-{Guid.NewGuid():N}",
            $"partner-agent-{Guid.NewGuid():N}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static async Task<HttpResponseMessage> SendImportAsync(
        HttpClient client,
        byte[] packageBytes,
        string idempotencyKey,
        string externalKey)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/agents/import-package");
        request.Headers.Add("Idempotency-Key", idempotencyKey);
        request.Content = CreateMultipartContent(packageBytes, externalKey);
        return await client.SendAsync(request);
    }

    private static MultipartFormDataContent CreateMultipartContent(
        byte[] packageBytes,
        string externalKey)
    {
        var content = new MultipartFormDataContent();
        var packageContent = new ByteArrayContent(packageBytes);
        packageContent.Headers.ContentType =
            new System.Net.Http.Headers.MediaTypeHeaderValue("application/zip");
        content.Add(packageContent, "Package", "agent-package.zip");
        content.Add(new StringContent("create"), "Mode");
        content.Add(new StringContent(externalKey), "ExternalKey");
        return content;
    }

    private static byte[] CreatePackage(AgentDefinition agent)
    {
        var manifest = new Dictionary<string, object?>
        {
            ["schemaVersion"] = "1.0",
            ["agent"] = agent,
            ["sessions"] = Array.Empty<object>(),
            ["executionLog"] = Array.Empty<object>(),
            ["metrics"] = Array.Empty<object>(),
            ["memory"] = Array.Empty<object>(),
            ["providers"] = Array.Empty<object>(),
            ["capabilities"] = Array.Empty<object>(),
            ["runs"] = Array.Empty<object>(),
            ["approvals"] = Array.Empty<object>(),
            ["artifacts"] = Array.Empty<object>(),
            ["checkpoints"] = Array.Empty<object>(),
            ["toolReceipts"] = Array.Empty<object>()
        };

        using var package = new MemoryStream();
        using (var archive = new ZipArchive(package, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("manifest.json");
            using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
            writer.Write(JsonSerializer.Serialize(manifest, JsonOptions));
        }

        return package.ToArray();
    }

    private static AgentDefinition CreateAgent()
    {
        var now = DateTimeOffset.UtcNow;
        var suffix = Guid.NewGuid().ToString("N");
        return new AgentDefinition(
            Guid.NewGuid(),
            $"Remote package agent {suffix}",
            "Partner automation",
            "Imported through the remote package API.",
            "Perform the partner automation task.",
            AgentLifecycleStatus.Active,
            ProviderProfileId: null,
            Model: "gpt-test",
            AgentWorkloadKind.Programming,
            AgentChatHistoryMode.FrameworkManaged,
            Temperature: 0.1,
            RequirePerServiceCallChatHistoryPersistence: false,
            EnableBackgroundResponses: false,
            ConfigurationJson: """{"responseMode":"concise"}""",
            IsTemplate: false,
            TemplateKey: $"remote-package-{suffix}",
            AgentPermissionsPolicy.Default,
            Capabilities: [],
            Tags: ["remote-package"],
            CreatedAtUtc: now,
            UpdatedAtUtc: now);
    }

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);
}
