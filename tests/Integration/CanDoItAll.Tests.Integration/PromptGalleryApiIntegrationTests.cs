using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Runtime.CompilerServices;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Search;
using CanDoItAll.Modules.Prompts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class PromptGalleryApiIntegrationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Prompt_gallery_api_round_trips_items_search_compatibility_and_projection_status()
    {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: false);
        await using (var scope = host.App.Services.CreateAsyncScope())
        {
            Assert.NotNull(scope.ServiceProvider.GetRequiredService<IPromptGalleryService>());
            Assert.NotNull(scope.ServiceProvider.GetRequiredService<IPromptGalleryProjectionCoordinator>());
        }

        var unique = $"api-gallery-{Guid.NewGuid():N}";
        var draft = new PromptGalleryDraft(
            Id: null,
            ProjectId: null,
            CollectionId: null,
            Title: $"API Gallery {unique}",
            Summary: "Prompt Gallery API integration proof.",
            PromptGalleryItemKind.FullPrompt,
            Phase: "integration-test",
            Content: $"Use the immutable API prompt {unique}.",
            Tags: ["api", unique],
            SupportedModels: [new PromptProviderModel("OpenAi", "gpt-test")],
            SupportedConsumers: [PromptGalleryConsumer.AgentRuntime, PromptGalleryConsumer.Chat],
            Recommendations: new PromptModelRecommendations(0.2, 800, 0.9));

        var saveResponse = await host.Client.PostAsJsonAsync("/api/prompt-gallery/items", draft);
        var saveBody = await saveResponse.Content.ReadAsStringAsync();
        Assert.True(
            saveResponse.IsSuccessStatusCode,
            $"{(int)saveResponse.StatusCode} {saveResponse.StatusCode}: {saveBody}");
        var promptId = JsonSerializer.Deserialize<Guid>(saveBody, JsonOptions);

        var versionResponse = await host.Client.PostAsJsonAsync(
            $"/api/prompt-gallery/items/{promptId:D}/versions",
            new PromptVersionCreateRequest("API integration proof"));
        var versionBody = await versionResponse.Content.ReadAsStringAsync();
        Assert.True(versionResponse.IsSuccessStatusCode, versionBody);
        var version = JsonSerializer.Deserialize<PromptVersionSnapshot>(versionBody, JsonOptions)!;

        var detail = await GetAsync<PromptGalleryItemDetails>(
            host.Client,
            $"/api/prompt-gallery/items/{promptId:D}");
        var page = await GetAsync<PromptGalleryPage<PromptGallerySearchItem>>(
            host.Client,
            $"/api/prompt-gallery/items?text={Uri.EscapeDataString(unique)}&tag={Uri.EscapeDataString(unique)}&pageSize=10");
        var compatibilityResponse = await host.Client.PostAsJsonAsync(
            "/api/prompt-gallery/compatibility/evaluate",
            new
            {
                PromptArtifactId = promptId,
                Context = new PromptGalleryConsumerContext(
                    PromptGalleryConsumer.AgentRuntime,
                    PromptGalleryCompatibilityPurpose.Execution,
                    Provider: "OpenAi",
                    Model: "gpt-test",
                    RequiresFinalVersion: true)
            });
        var compatibilityBody = await compatibilityResponse.Content.ReadAsStringAsync();
        Assert.True(compatibilityResponse.IsSuccessStatusCode, compatibilityBody);
        var compatibility = JsonSerializer.Deserialize<PromptCompatibilityResult>(compatibilityBody, JsonOptions)!;
        var projection = await GetAsync<PromptGalleryProjectionStatus>(
            host.Client,
            "/api/prompt-gallery/projection");
        var rebuildResponse = await host.Client.PostAsync(
            "/api/prompt-gallery/projection/rebuild",
            content: null);
        var rebuildBody = await rebuildResponse.Content.ReadAsStringAsync();
        Assert.True(rebuildResponse.IsSuccessStatusCode, rebuildBody);
        var rebuild = JsonSerializer.Deserialize<PromptGalleryProjectionOperationResult>(rebuildBody, JsonOptions)!;

        Assert.Equal(promptId, detail.Id);
        Assert.Equal(version.PromptVersionId, Assert.Single(detail.Versions).Id);
        Assert.Equal(promptId, Assert.Single(page.Items).Id);
        Assert.True(compatibility.CanUse);
        Assert.False(projection.Enabled);
        Assert.Equal(PromptGalleryProjectionHealth.Disabled, projection.Health);
        Assert.Equal(PromptGalleryProjectionOperationState.Disabled, rebuild.State);

        var archiveResponse = await host.Client.PostAsJsonAsync(
            $"/api/prompt-gallery/items/{promptId:D}/archive",
            new { Archived = true });
        Assert.True(archiveResponse.IsSuccessStatusCode, await archiveResponse.Content.ReadAsStringAsync());
        var archivedSearch = await GetAsync<PromptGalleryPage<PromptGallerySearchItem>>(
            host.Client,
            $"/api/prompt-gallery/items?text={Uri.EscapeDataString(unique)}&pageSize=10");
        Assert.Empty(archivedSearch.Items);

        var missingResponse = await host.Client.GetAsync($"/api/prompt-gallery/items/{Guid.NewGuid():D}");
        Assert.Equal(HttpStatusCode.NotFound, missingResponse.StatusCode);

        using var nullBody = new StringContent("null", Encoding.UTF8, "application/json");
        var nullResponse = await host.Client.PostAsync("/api/prompt-gallery/items", nullBody);
        Assert.Equal(HttpStatusCode.BadRequest, nullResponse.StatusCode);
    }

    [Fact]
    public async Task Search_index_projection_rebuild_rolls_back_when_source_stream_fails()
    {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: false);
        var factory = host.App.Services.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var existingPromptId = Guid.NewGuid();
        var unrelatedId = Guid.NewGuid();
        await using (var arrangeContext = await factory.CreateDbContextAsync())
        {
            arrangeContext.AddRange(
                new SearchDocument
                {
                    SourceType = SearchIndexPromptGalleryProjectionDriver.SourceType,
                    SourceKey = existingPromptId.ToString(),
                    Category = "Prompts",
                    Title = "Existing prompt projection",
                    Route = $"/prompt-gallery?promptId={existingPromptId:D}"
                },
                new SearchDocument
                {
                    SourceType = "project",
                    SourceKey = unrelatedId.ToString(),
                    Category = "Projects",
                    Title = "Unrelated projection",
                    Route = $"/projects/{unrelatedId:D}"
                });
            await arrangeContext.SaveChangesAsync();
        }

        var driver = new SearchIndexPromptGalleryProjectionDriver(factory);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            driver.RebuildAsync(FailingProjectionDocuments()));

        await using var assertContext = await factory.CreateDbContextAsync();
        var documents = await assertContext.Set<SearchDocument>()
            .AsNoTracking()
            .OrderBy(document => document.SourceType)
            .ThenBy(document => document.SourceKey)
            .ToListAsync();
        Assert.Contains(documents, document =>
            document.SourceType == SearchIndexPromptGalleryProjectionDriver.SourceType &&
            document.SourceKey == existingPromptId.ToString());
        Assert.Contains(documents, document =>
            document.SourceType == "project" && document.SourceKey == unrelatedId.ToString());
        Assert.DoesNotContain(documents, document => document.Title.StartsWith("Replacement", StringComparison.Ordinal));
    }

    private static async Task<T> GetAsync<T>(HttpClient client, string route)
    {
        var response = await client.GetAsync(route);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
        return JsonSerializer.Deserialize<T>(body, JsonOptions)
            ?? throw new InvalidOperationException($"Response from '{route}' deserialized to null.");
    }

    private static async IAsyncEnumerable<PromptGalleryProjectionDocument> FailingProjectionDocuments(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        for (var index = 0; index < 250; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var promptId = Guid.NewGuid();
            yield return new PromptGalleryProjectionDocument(
                promptId,
                ProjectId: null,
                $"Replacement {index}",
                "Projection rollback proof.",
                "Replacement content.",
                PromptGalleryItemKind.FullPrompt,
                PromptArtifactStatus.Final,
                Tags: [],
                $"/prompt-gallery?promptId={promptId:D}",
                DateTimeOffset.UnixEpoch);
        }

        await Task.Yield();
        throw new InvalidOperationException("Synthetic projection source failure.");
    }
}
