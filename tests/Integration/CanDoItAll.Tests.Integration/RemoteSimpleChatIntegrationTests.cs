using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Application;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Conversations;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Definitions;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Security;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.Modules.Workspace.Pages.Components;
using CanDoItAll.RemoteInstances;
using CanDoItAll.Tests.Integration.Api;
using CanDoItAll.Web.Api;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.LlmChats;

[Trait("Category", "HostPlatform")]
public sealed class RemoteSimpleChatIntegrationTests {
    [Fact]
    public async Task Api_availability_filters_before_paging_and_rejects_new_work_independently_of_template_sharing() {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: true);
        await using var scope = host.App.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var definitions = services.GetRequiredService<ILlmChatDefinitionApplicationService>();
        var conversations = services.GetRequiredService<ILlmChatConversationApplicationService>();
        var publisher = services.GetRequiredService<RemoteSimpleChatPublisher>();
        var provider = await AddProviderAsync(services, "Visibility");
        var command = CreateDefinition(provider, "Visibility");
        var visible = (await definitions.CreateAsync(command with { Name = "API selection" })).Value!.Definition;
        var hidden = (await definitions.CreateAsync(command with { Name = "Template only" })).Value!.Definition;
        var active = (await definitions.ChangeStatusAsync(new(hidden.Id, LlmChatDefinitionStatus.Active, hidden.ConcurrencyToken))).Value!.Definition;
        var archived = (await conversations.CreateAsync(new(hidden.Id, "Archived transcript", LlmChatConversationOrigin.Api))).Value!.Conversation;
        Assert.True((await conversations.ArchiveAsync(new(archived.Id, archived.ConcurrencyToken))).IsSuccess);
        var ongoing = (await conversations.CreateAsync(new(hidden.Id, "Local discussion", LlmChatConversationOrigin.Api))).Value!.Conversation;
        var token = services.GetRequiredService<IApiTokenService>().IssueToken(new() {
            Subject = "simple-chat-sharing", Scopes = [ApiAccessScopeNames.ReadLlmChats, ApiAccessScopeNames.ManageLlmChats,
                ApiAccessScopeNames.ExecuteLlmChats, ApiAccessScopeNames.ReadSharedSimpleChats]
        }).Token;
        host.Client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        var definitionPath = $"/api/llm-chats/{hidden.Id.Value:D}";
        var conversationPath = $"/api/llm-conversations/{ongoing.Id.Value:D}";
        using (var before = await host.Client.GetAsync(definitionPath)) {
            Assert.Equal(HttpStatusCode.OK, before.StatusCode);
            var initial = await before.Content.ReadFromJsonAsync<JsonObject>();
            Assert.Equal(["api", "review"], initial!["tags"]!.AsArray().Select(node => node!.GetValue<string>()));
        }
        var templateOnly = new DefinitionSharingSettings(false, true);
        await publisher.SetSharingAsync(hidden.Id.Value, 1, DefinitionSharingSettings.Default, templateOnly, Allow, default);
        var templateTags = (await definitions.ListPageAsync(new(take: 1, tags: ["shared", "review"]))).Value!;
        Assert.Equal(hidden.Id, Assert.Single(templateTags.Items).Definition.Id);
        Assert.Equal(["shared", "review"], templateTags.Items[0].EffectiveTags);
        Assert.Empty((await definitions.ListPageAsync(new(tags: ["shared", "api"]))).Value!.Items);

        var page = await host.Client.GetFromJsonAsync<JsonObject>("/api/llm-chats?take=1");
        Assert.Equal(visible.Id.Value, Assert.Single(page!["items"]!.AsArray())!["id"]!.GetValue<Guid>());
        Assert.Null(page["nextCursor"]);
        var conversationPage = await host.Client.GetFromJsonAsync<JsonObject>($"/api/llm-conversations?take=1&definitionId={hidden.Id.Value:D}");
        Assert.Equal(archived.Id.Value, Assert.Single(conversationPage!["items"]!.AsArray())!["id"]!.GetValue<Guid>());
        Assert.Null(conversationPage["nextCursor"]);
        using var detail = await host.Client.GetAsync(definitionPath);
        using var editor = await host.Client.GetAsync(definitionPath + "/editor");
        using var edit = await host.Client.PutAsJsonAsync(definitionPath, new { });
        using var suspend = await host.Client.PostAsJsonAsync(definitionPath + "/suspend", new { expectedConcurrencyToken = active.ConcurrencyToken });
        using var create = await host.Client.PostAsJsonAsync(definitionPath + "/conversations", new { title = "Rejected" });
        var operationId = Guid.NewGuid();
        using var send = await host.Client.PostAsJsonAsync(conversationPath + "/turns", new { operationId, expectedTranscriptRevision = 1, message = "Rejected" });
        using var operation = await host.Client.GetAsync($"/api/llm-chat-operations/{operationId:D}");
        Assert.All(new[] { detail, editor, edit, suspend, create, send, operation }, response => Assert.Equal(HttpStatusCode.NotFound, response.StatusCode));
        using var history = await host.Client.GetAsync(conversationPath);
        Assert.Equal(HttpStatusCode.OK, history.StatusCode);
        Assert.Equal(LlmChatDefinitionStatus.Active, (await definitions.GetAsync(hidden.Id)).Value!.Definition.Status);
        using var template = await host.Client.GetAsync(RemoteSimpleChatRoutes.Details(hidden.Id.Value));
        Assert.Equal(HttpStatusCode.OK, template.StatusCode);

        await publisher.SetSharingAsync(hidden.Id.Value, 1, templateOnly, DefinitionSharingSettings.Default, Allow, default);
        using var restored = await host.Client.GetAsync(definitionPath);
        using var withdrawn = await host.Client.GetAsync(RemoteSimpleChatRoutes.Details(hidden.Id.Value));
        Assert.Equal(HttpStatusCode.OK, restored.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, withdrawn.StatusCode);
    }

    [Fact]
    public async Task Only_explicitly_shared_current_definitions_are_visible_with_a_scoped_token() {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: true);
        await using var scope = host.App.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var definitions = services.GetRequiredService<ILlmChatDefinitionApplicationService>();
        var conversations = services.GetRequiredService<ILlmChatConversationApplicationService>();
        var publisher = services.GetRequiredService<RemoteSimpleChatPublisher>();
        var provider = await AddProviderAsync(services, "Source");
        var command = CreateDefinition(provider, "Source");
        var created = await definitions.CreateAsync(command);
        Assert.True(created.IsSuccess);
        var id = created.Value!.Definition.Id;
        var other = await definitions.CreateAsync(command with { Name = "Unpublished definition" });
        Assert.True(other.IsSuccess);
        var active = await definitions.ChangeStatusAsync(new(id, LlmChatDefinitionStatus.Active, created.Value.Definition.ConcurrencyToken));
        Assert.True(active.IsSuccess);
        var conversation = await conversations.CreateAsync(new(id, "Private discussion sentinel", LlmChatConversationOrigin.Api));
        Assert.True(conversation.IsSuccess);

        using var anonymous = await host.Client.GetAsync(RemoteSimpleChatRoutes.Catalog);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        using var unrelated = await GetAsync(RemoteSimpleChatRoutes.Catalog, ApiAccessScopeNames.ReadSharedAgentTemplates);
        Assert.Equal(HttpStatusCode.Forbidden, unrelated.StatusCode);
        using var privateDefinition = await GetAsync(RemoteSimpleChatRoutes.Details(id.Value));
        Assert.Equal(HttpStatusCode.NotFound, privateDefinition.StatusCode);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => publisher.SetPublicationAsync(id.Value, 1, true,
            _ => ValueTask.FromException(new UnauthorizedAccessException()), default));
        await publisher.SetPublicationAsync(id.Value, 1, true, Allow, default);
        using var response = await GetAsync(RemoteSimpleChatRoutes.Catalog);
        response.EnsureSuccessStatusCode();
        var row = Assert.Single((await response.Content.ReadFromJsonAsync<RemoteTemplateRow[]>())!);
        Assert.Equal(id.Value, row.Id);
        Assert.Equal(command.AvatarImageUrl, row.AvatarImageUrl);
        Assert.True(row.CatalogRevision > 0);
        using var details = await GetAsync(RemoteSimpleChatRoutes.Details(id.Value));
        details.EnsureSuccessStatusCode();
        var body = await details.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Private discussion sentinel", body, StringComparison.Ordinal);
        Assert.DoesNotContain(provider.ToString("D"), body, StringComparison.Ordinal);
        var document = await details.Content.ReadFromJsonAsync<RemoteSimpleChatTemplate>();
        Assert.Equal(row, document!.Row);
        Assert.Equal(command.SystemPrompt, document.SystemPrompt);
        Assert.Equal(command.Settings, document.Settings.ToLocal());
        Assert.Equal(command.ResponseFormat, document.ResponseFormat!.ToLocal());
        Assert.Equal(command.Tags, document.Tags);
        Assert.True(document.Model.StructuredOutput);

        await publisher.SetPublicationAsync(id.Value, 1, false, Allow, default);
        using var withdrawn = await GetAsync(RemoteSimpleChatRoutes.Details(id.Value));
        Assert.Equal(HttpStatusCode.NotFound, withdrawn.StatusCode);
        await publisher.SetPublicationAsync(id.Value, 1, true, Allow, default);
        var archived = await definitions.ChangeStatusAsync(new(id, LlmChatDefinitionStatus.Archived, active.Value!.Definition.ConcurrencyToken));
        Assert.True(archived.IsSuccess);
        using var archivedRead = await GetAsync(RemoteSimpleChatRoutes.Details(id.Value));
        Assert.Equal(HttpStatusCode.NotFound, archivedRead.StatusCode);
        Assert.Empty(await publisher.ListAsync(default));
        await Assert.ThrowsAsync<RemoteInstanceReadException>(() => publisher.SetPublicationAsync(id.Value, 1, true, Allow, default));
        var api = await host.Client.GetFromJsonAsync<JsonObject>("/openapi/v1.json");
        Assert.NotNull(api);
        Assert.Empty(OpenApiDescriptionCoverage.FindGaps(api, "/" + RemoteSimpleChatRoutes.Catalog));

        async Task<HttpResponseMessage> GetAsync(string path, string permission = ApiAccessScopeNames.ReadSharedSimpleChats) {
            using var request = new HttpRequestMessage(HttpMethod.Get, path);
            request.Headers.Authorization = new("Bearer", IssueToken(services, permission));
            return await host.Client.SendAsync(request);
        }
    }

    [Fact]
    public async Task Import_over_HTTP_creates_one_draft_with_the_chosen_model_and_preserves_local_edits() {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: true, configureServices: services => {
            services.AddRazorComponents().AddInteractiveServerComponents();
            services.AddAgentFrameworkUi();
        });
        await using var scope = host.App.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var definitions = services.GetRequiredService<ILlmChatDefinitionApplicationService>();
        var publisher = services.GetRequiredService<RemoteSimpleChatPublisher>();
        const string publisherModel = "gpt-oss:publisher";
        const string compatibleModel = "gpt-oss:consumer";
        const string toggleOnlyModel = "qwen3:consumer";
        var origin = await AddProviderAsync(services, publisherModel);
        var substitute = await AddProviderAsync(services, compatibleModel, toggleOnlyModel);
        var original = CreateDefinition(origin, publisherModel);
        var command = original with { AvatarImageUrl = EmbeddedGif(), Settings = original.Settings with { ThinkingEffort = AgentReasoningEffortLevel.High } };
        Assert.True(command.AvatarImageUrl.Length > LlmChatDefinitionValidation.MaximumAvatarImageUrlLength);
        var created = await definitions.CreateAsync(command);
        Assert.True(created.IsSuccess);
        var definition = created.Value!.Definition;
        await publisher.SetPublicationAsync(definition.Id.Value, 1, true, Allow, default);
        var compact = Assert.Single(await publisher.ListAsync(default));
        Assert.True(compact.HasEmbeddedAvatar);
        Assert.Null(compact.AvatarImageUrl);
        var credential = await services.GetRequiredService<SecretService>().SaveAsync(new() {
            Name = "Shared Simple Chat credential", Kind = SecretKind.ApiKey,
            SecretValue = IssueToken(services, ApiAccessScopeNames.ReadSharedSimpleChats)
        });
        Assert.True(credential.IsSuccess);
        var registry = services.GetRequiredService<IRemoteInstanceRegistry>();
        var connection = await registry.CreateAsync(Guid.NewGuid(), new("Publisher test", host.Client.BaseAddress!, credential.Value, true, true));
        var source = new RemoteInstanceSelection(connection.Id, connection.ConcurrencyToken);
        var feature = Assert.Single(services.GetServices<IRemoteTemplateImportFeature>(), candidate => candidate.Kind == RemoteTemplateKind.SimpleChat);
        var coordinator = services.GetRequiredService<RemoteTemplateImportCoordinator>();
        var workspace = await services.GetRequiredService<IAgentFrameworkWorkspaceService>().ReadAgentDecisionDataPolicyContextAsync();
        var row = Assert.Single(await feature.ListAsync(source, default));
        Assert.Equal(command.AvatarImageUrl, row.AvatarImageUrl);
        var preview = await feature.PreviewAsync(source, row, default);
        Assert.Equal(command.Settings.ThinkingEffort, Assert.Single(preview.Models).ThinkingEffort);
        Assert.Equal(command.Settings.ThinkingEffort, (await publisher.FindAsync(definition.Id.Value, default))!.Model.ThinkingEffort);
        var choice = new RemoteTemplateModelChoice(substitute, compatibleModel);
        var edited = await definitions.UpdateAsync(new(definition.Id, command.Name, command.Summary, command.AvatarImageUrl, "Revised source instructions",
            origin, command.Model, command.Settings, command.Timeout, command.ResponseFormat, "New revision", definition.ConcurrencyToken, command.Tags));
        Assert.True(edited.IsSuccess);
        await Assert.ThrowsAsync<RemoteInstanceReadException>(() => coordinator.ImportAsync(feature, source, preview, [choice], workspace, default));
        row = Assert.Single(await feature.ListAsync(source, default));
        preview = await feature.PreviewAsync(source, row, default);
        var alternatives = await services.GetRequiredService<RemoteTemplateProviderCatalog>().ListAsync(source.Id, row.PublisherId, default, feature.Kind);
        var incompatible = Assert.Single(alternatives, option => option.Choice == new RemoteTemplateModelChoice(substitute, toggleOnlyModel));
        Assert.True(incompatible.Supports(preview.Models[0] with { ThinkingEffort = null }));
        Assert.False(incompatible.Supports(preview.Models[0]));
        await Assert.ThrowsAsync<RemoteInstanceReadException>(() => coordinator.ImportAsync(feature, source, preview, [incompatible.Choice], workspace, default));
        await Assert.ThrowsAsync<RemoteInstanceReadException>(() => coordinator.ImportAsync(feature, source, preview,
            [new(substitute, "unavailable")], workspace, default));
        var localId = await coordinator.ImportAsync(feature, source, preview, [choice], workspace, default);
        Assert.NotEqual(definition.Id.Value, localId);
        var local = (await definitions.GetAsync(new(localId))).Value!;
        Assert.Equal(LlmChatDefinitionStatus.Draft, local.Definition.Status);
        Assert.Equal(choice.ProviderId, local.Revision.ProviderProfileId);
        Assert.Equal(choice.Model, local.Revision.Model);
        Assert.Equal("Revised source instructions", local.Revision.SystemPrompt);
        Assert.Equal(command.AvatarImageUrl, local.Revision.AvatarImageUrl);
        Assert.Equal(command.Settings, local.Revision.Settings);
        Assert.Equal(command.ResponseFormat, local.Revision.ResponseFormat);
        Assert.Equal(command.Tags, local.NormalizedTags);
        Assert.Equal(["api", .. command.Tags!], local.EffectiveTags);
        Assert.False(await services.GetRequiredService<RemoteTemplatePublicationStore>().IsPublishedAsync(RemoteTemplateKind.SimpleChat, localId, default));
        var change = await definitions.UpdateAsync(new(new(localId), "Human title", local.Revision.Summary, local.Revision.AvatarImageUrl, "Local instructions",
            choice.ProviderId, choice.Model, command.Settings, command.Timeout, command.ResponseFormat, "Edit after import", local.Definition.ConcurrencyToken));
        Assert.True(change.IsSuccess);
        Assert.Equal(localId, await coordinator.ImportAsync(feature, source, preview, [choice], workspace, default));
        Assert.Equal("Local instructions", (await definitions.GetAsync(new(localId))).Value!.Revision.SystemPrompt);
        var secondConnection = await registry.CreateAsync(Guid.NewGuid(), new("Second connection", host.Client.BaseAddress!, credential.Value, true, true));
        var second = new RemoteInstanceSelection(secondConnection.Id, secondConnection.ConcurrencyToken);
        var secondPreview = await feature.PreviewAsync(second, Assert.Single(await feature.ListAsync(second, default)), default);
        Assert.NotEqual(localId, await coordinator.ImportAsync(feature, second, secondPreview, [choice], workspace, default));
        var conversations = await services.GetRequiredService<ILlmChatConversationApplicationService>().ListAsync(new(definitionId: new(localId)));
        Assert.True(conversations.IsSuccess);
        Assert.Empty(conversations.Value!);
        await publisher.SetPublicationAsync(definition.Id.Value, (int)row.CatalogRevision, false, Allow, default);
        Assert.Null(await publisher.FindAvatarAsync(definition.Id.Value, default));
        await Assert.ThrowsAsync<RemoteInstanceReadException>(() => coordinator.ImportAsync(feature, source, preview, [choice], workspace, default));
    }

    [Fact]
    public void Portable_settings_reject_arbitrary_provider_configuration() {
        Assert.Throws<RemoteInstanceReadException>(() => RemoteSimpleChatSettings.From(new(null,
            """{"modelParameters":{"maxOutputTokens":128},"apiKey":"not-for-publication"}""")));
    }

    [Theory]
    [InlineData("data:image/svg+xml;base64,PHN2Zy8+")]
    [InlineData("data:image/png;base64,PHN2Zy8+")]
    [InlineData("data:image/jpeg;base64,%")]
    public void Embedded_avatar_requires_a_bounded_raster(string image) {
        Assert.Throws<ArgumentException>(() => LlmChatDefinitionValidation.NormalizeAvatarImageUrl(image));
    }

    private static string EmbeddedGif() {
        var pixel = Convert.FromBase64String("R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7");
        using var image = new MemoryStream();
        image.Write(pixel.AsSpan(0, pixel.Length - 1));
        image.Write([0x21, 0xfe]);
        var comment = Enumerable.Repeat((byte)'x', 255).ToArray();
        for (var block = 0; block < 7; block++) {
            image.WriteByte((byte)comment.Length);
            image.Write(comment);
        }
        image.Write([0, 0x3b]);
        return AgentAvatarImagePolicy.BuildDataUrl("image/gif", image.ToArray());
    }

    private static ValueTask Allow(CancellationToken token) => ValueTask.CompletedTask;

    private static Task<Guid> AddProviderAsync(IServiceProvider services, string model, params string[] additionalModels) =>
        services.GetRequiredService<IProviderProfileRegistry>().SaveProviderAsync(new() {
            Name = $"{model} Simple Chat provider", Kind = ProviderKind.Ollama, Transport = ProviderTransportKind.ChatCompletions,
            BaseUrl = "https://provider.example.invalid", DefaultModel = model, SuggestedModels = [model, .. additionalModels], IsEnabled = true
        });

    private static CreateLlmChatDefinitionCommand CreateDefinition(Guid provider, string model) => new("Reusable chat", "Draft a response",
        AgentAvatarImageCatalog.BundledAvatarUrls[0], "Review the request", provider, model, new(0.3, """{"modelParameters":{"maxOutputTokens":128}}"""),
        TimeSpan.FromSeconds(70), new(true, """{"type":"object"}""", "draft", "A drafted response"), "Created for publication", ["review"]);

    private static string IssueToken(IServiceProvider services, string permission) =>
        services.GetRequiredService<IApiTokenService>().IssueToken(new() { Subject = "shared-chat-reader", Scopes = [permission] }).Token;
}
