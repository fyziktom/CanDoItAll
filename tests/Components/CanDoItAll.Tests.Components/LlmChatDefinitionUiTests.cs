using Bunit;
using CanDoItAll.AgentFramework.Components;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Conversations.Components;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Application;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Common;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Definitions;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Components;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Conversations.Components.Presentation;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.LlmChats;

public sealed class LlmChatDefinitionUiTests
{
    private static readonly Guid DefinitionId = Guid.Parse("10101010-1010-1010-1010-101010101010");
    private static readonly Guid ProviderId = Guid.Parse("20202020-2020-2020-2020-202020202020");

    [Theory]
    [InlineData("")]
    [InlineData("_content/CanDoItAll.Components.BaseLib/assets/identity/avatars/avatar-02.jpg")]
    [InlineData("data:image/png;base64,AQID")]
    public void Existing_avatar_matches_catalog_in_editor_and_picker_and_is_preserved_on_save(string avatarUrl) {
        var editor = CreateEditor(avatarUrl: avatarUrl);
        var gateway = new StubDefinitionGateway(editor);
        using var context = CreateContext(gateway, new StubProviderGateway(), new StubAuthorization(true, true));
        var catalog = context.Render<LlmChatDefinitionCatalogPanel>();
        var expected = catalog.WaitForElement($"[data-testid='llm-chat-definition-{DefinitionId:D}'] img").GetAttribute("src");
        var cut = context.Render<LlmChatDefinitionEditorDialog>(parameters => parameters
            .Add(component => component.DefinitionId, DefinitionId));

        Assert.Equal(expected, cut.WaitForElement("[data-testid='llm-chat-definition-avatar-summary'] img").GetAttribute("src"));
        cut.Find("[data-testid='llm-chat-definition-avatar-open']").Click();
        Assert.Equal(expected, cut.WaitForElement("[data-testid='llm-chat-definition-avatar-dialog'] img").GetAttribute("src"));
        cut.Find("[data-testid='llm-chat-definition-avatar-close']").Click();
        cut.Find("[data-testid='llm-chat-definition-editor-save']").Click();
        cut.WaitForAssertion(() => Assert.Equal(avatarUrl, gateway.UpdatedMutation?.AvatarImageUrl));
    }

    [Fact]
    public void Renaming_existing_definition_keeps_its_default_avatar_identity() {
        var gateway = new StubDefinitionGateway(CreateEditor());
        using var context = CreateContext(gateway, new StubProviderGateway(), new StubAuthorization(true, true));
        var cut = context.Render<LlmChatDefinitionEditorDialog>(parameters => parameters
            .Add(component => component.DefinitionId, DefinitionId));
        cut.WaitForElement("[data-testid='llm-chat-definition-name']");
        cut.Find("[data-testid='llm-chat-definition-name']").Change("Renamed assistant");

        Assert.Equal(DefinitionId.ToString("D"), Assert.IsType<ConversationAvatarPresentation>(
            cut.FindComponent<ConversationIdentityFields>().Instance.Avatar).Seed);
        cut.Find("[data-testid='llm-chat-definition-avatar-open']").Click();
        cut.WaitForElement("[data-testid='llm-chat-definition-avatar-dialog']");
        Assert.Equal(DefinitionId.ToString("D"), cut.FindComponent<AvatarPicker>().FindComponent<Avatar>().Instance.DefaultImageSeed);
    }

    [Fact]
    public void Resetting_explicit_avatar_restores_catalog_default_in_both_previews() {
        var gateway = new StubDefinitionGateway(CreateEditor(avatarUrl: AgentAvatarImageCatalog.BundledAvatarUrls[1]));
        using var context = CreateContext(gateway, new StubProviderGateway(), new StubAuthorization(true, true));
        var cut = context.Render<LlmChatDefinitionEditorDialog>(parameters => parameters
            .Add(component => component.DefinitionId, DefinitionId));
        cut.WaitForElement("[data-testid='llm-chat-definition-avatar-clear']").Click();

        var avatar = Assert.IsType<ConversationAvatarPresentation>(cut.FindComponent<ConversationIdentityFields>().Instance.Avatar);
        Assert.Equal(string.Empty, avatar.ImageUrl);
        Assert.Equal(DefinitionId.ToString("D"), avatar.Seed);
        cut.Find("[data-testid='llm-chat-definition-avatar-open']").Click();
        var preview = cut.WaitForElement("[data-testid='llm-chat-definition-avatar-dialog'] img");
        Assert.Equal(cut.Find("[data-testid='llm-chat-definition-avatar-summary'] img").GetAttribute("src"), preview.GetAttribute("src"));
    }

    [Fact]
    public void Shared_definition_shows_labels_but_saves_nondefault_route_and_forbids_override() {
        var gateway = new StubDefinitionGateway(CreateEditor());
        using var context = CreateContext(gateway,
            new StubProviderGateway("opaque-default", "opaque-secondary", true),
            new StubAuthorization(canRead: true, canManage: true));
        var cut = context.Render<LlmChatDefinitionEditorDialog>();
        cut.WaitForElement("[data-testid='llm-chat-definition-editor-save']");
        cut.Find("[data-testid='llm-chat-definition-name']").Change("Shared chat");
        cut.Find("[data-testid='llm-chat-definition-tab-runtime']").Click();
        cut.Find("[data-testid='llm-chat-definition-provider']").Change("0");

        cut.WaitForAssertion(() => Assert.Equal(["Provider default (Readable default)", "Readable secondary"],
            cut.FindAll("[data-testid='llm-chat-definition-model'] option").Select(option => option.TextContent)));
        Assert.Empty(cut.FindAll("[data-testid='llm-chat-definition-model-override']"));
        cut.Find("[data-testid='llm-chat-definition-model']").Change("1");
        cut.Find("[data-testid='llm-chat-definition-editor-save']").Click();
        cut.WaitForAssertion(() => Assert.Equal("opaque-secondary", gateway.CreatedMutation?.Model));
    }

    [Fact]
    public void Read_only_catalog_renders_neutral_cards_without_mutation_controls_or_prompt_access()
    {
        var gateway = new StubDefinitionGateway(CreateEditor());
        using var context = CreateContext(
            gateway,
            new StubProviderGateway(),
            new StubAuthorization(canRead: true, canManage: false));

        var cut = context.Render<LlmChatDefinitionCatalogPanel>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Research assistant", cut.Markup, StringComparison.Ordinal);
            Assert.NotNull(cut.Find($"[data-testid='llm-chat-definition-{DefinitionId:D}']"));
        });
        Assert.Empty(cut.FindAll("[data-testid='llm-chat-definition-create']"));
        Assert.Empty(cut.FindAll($"[data-testid='llm-chat-definition-edit-{DefinitionId:D}']"));
        Assert.DoesNotContain("secret system prompt", cut.Markup, StringComparison.Ordinal);
        Assert.Equal(0, gateway.GetEditorCalls);
    }

    [Fact]
    public async Task Catalog_keeps_filters_in_the_header_and_queries_search_tags_and_status_server_side()
    {
        var research = CreateEditor();
        var operations = CreateEditor(
            name: "Operations assistant",
            summary: "Handles production incidents.",
            status: LlmChatDefinitionStatus.Active,
            definitionId: Guid.Parse("30303030-3030-3030-3030-303030303030"),
            tags: ["operations", "incident-response"]);
        var gateway = new StubDefinitionGateway(research)
        {
            ListItems = [research.Definition, operations.Definition]
        };
        using var context = CreateContext(
            gateway,
            new StubProviderGateway(),
            new StubAuthorization(canRead: true, canManage: true));
        var cut = context.Render<LlmChatDefinitionCatalogPanel>();
        cut.WaitForAssertion(() => Assert.Equal(
            2,
            cut.FindAll("article[data-testid^='llm-chat-definition-']").Count));
        Assert.DoesNotContain(
            "Choose the provider, model, prompt, and output contract reused by conversations.",
            cut.Markup,
            StringComparison.Ordinal);
        var header = cut.Find("[data-testid='llm-chat-definition-header']");
        Assert.NotNull(header.QuerySelector("[data-testid='llm-chat-definition-filter-bar']"));
        var filters = cut.Find("[data-testid='llm-chat-definition-filters']");
        Assert.Contains("flex-nowrap", filters.ClassList);
        Assert.NotNull(filters.QuerySelector("[data-testid='llm-chat-definition-search']"));
        Assert.NotNull(filters.QuerySelector("[data-testid='llm-chat-definition-tag-filter']"));
        Assert.NotNull(filters.QuerySelector("[data-testid='llm-chat-definition-status-filter']"));
        Assert.NotNull(filters.QuerySelector("[data-testid='llm-chat-definition-filter-reset']"));
        Assert.All(
            cut.FindComponents<ConversationParticipantCard>(),
            card => Assert.True(card.Instance.Layout.HasFlag(ParticipantCardLayout.Centered)));
        Assert.Equal(
            "repeat(auto-fill,minmax(min(100%,17rem),22rem))",
            cut.FindComponent<Grid>().Instance.ColumnTemplate);

        var search = cut.Find("[data-testid='llm-chat-definition-search']");
        Assert.Equal(
            LlmChatDefinitionQuery.MaximumSearchLength.ToString(),
            search.GetAttribute("maxlength"));
        var supersededInput = search.InputAsync(new ChangeEventArgs { Value = "production" });
        await Task.Delay(25);
        var currentInput = search.InputAsync(new ChangeEventArgs { Value = "incident" });
        await Task.WhenAll(supersededInput, currentInput);

        cut.WaitForAssertion(() =>
        {
            var card = Assert.Single(cut.FindAll("article[data-testid^='llm-chat-definition-']"));
            Assert.Contains("Operations assistant", card.TextContent, StringComparison.Ordinal);
        });
        Assert.Equal("incident", gateway.ListQueries[^1].SearchText);
        Assert.DoesNotContain(gateway.ListQueries.Skip(1), query => query.SearchText == "production");

        cut.Find("[data-testid='llm-chat-definition-filter-reset']").Click();
        cut.WaitForAssertion(() => Assert.Equal(
            2,
            cut.FindAll("article[data-testid^='llm-chat-definition-']").Count));
        Assert.Equal(string.Empty, gateway.ListQueries[^1].SearchText);

        var tagEditor = cut.FindComponent<TagEditor>();
        await cut.InvokeAsync(() => tagEditor.Instance.ValueChanged.InvokeAsync(["incident-response"]));
        cut.WaitForAssertion(() =>
        {
            var card = Assert.Single(cut.FindAll("article[data-testid^='llm-chat-definition-']"));
            Assert.Contains("Operations assistant", card.TextContent, StringComparison.Ordinal);
        });
        Assert.Equal(["incident-response"], gateway.ListQueries[^1].Tags);

        cut.Find("[data-testid='llm-chat-definition-filter-reset']").Click();
        cut.WaitForAssertion(() => Assert.Equal(
            2,
            cut.FindAll("article[data-testid^='llm-chat-definition-']").Count));
        Assert.Empty(gateway.ListQueries[^1].Tags);

        cut.Find("[data-testid='llm-chat-definition-status-filter']").Change("2");
        cut.WaitForAssertion(() =>
        {
            var card = Assert.Single(cut.FindAll("article[data-testid^='llm-chat-definition-']"));
            Assert.Contains("Operations assistant", card.TextContent, StringComparison.Ordinal);
        });
        Assert.Equal(LlmChatDefinitionStatus.Active, gateway.ListQueries[^1].Status);
    }

    [Fact]
    public void Wide_editor_composes_reusable_fields_and_saves_provider_capability_revision_values()
    {
        var gateway = new StubDefinitionGateway(CreateEditor());
        var avatarGateway = new StubAvatarGenerationGateway();
        using var context = CreateContext(
            gateway,
            new StubProviderGateway(),
            new StubAuthorization(canRead: true, canManage: true),
            avatarGateway);

        var cut = context.Render<LlmChatDefinitionEditorDialog>(parameters => parameters
            .Add(component => component.DefinitionId, DefinitionId));

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='llm-chat-definition-editor-save']")));
        Assert.Equal(ModalSize.Wide, cut.FindComponent<Dialog>().Instance.Size);
        Assert.NotNull(cut.FindComponent<ConversationDefinitionEditorShell>());
        Assert.NotNull(cut.FindComponent<ConversationIdentityFields>());
        Assert.NotNull(cut.FindComponent<AvatarPicker>());
        Assert.NotNull(cut.FindComponent<TagEditor>());
        Assert.NotNull(cut.Find("[data-testid='llm-chat-definition-tab-identity']"));
        Assert.NotNull(cut.Find("[data-testid='llm-chat-definition-tab-runtime']"));
        Assert.NotNull(cut.Find("[data-testid='llm-chat-definition-tab-output']"));
        cut.Find("[data-testid='llm-chat-definition-tags-input']").Input("Research, Summary,");

        cut.Find("[data-testid='llm-chat-definition-avatar-open']").Click();
        cut.WaitForElement("[data-testid='llm-chat-definition-avatar-ai-prompt']");
        cut.Find("[data-testid='llm-chat-definition-avatar-ai-prompt']").Input("A concise blue research symbol");
        cut.Find("[data-testid='llm-chat-definition-avatar-ai-generate']").Click();
        cut.WaitForAssertion(() => Assert.Equal(1, avatarGateway.GenerateCalls));
        cut.Find("[data-testid='llm-chat-definition-avatar-close']").Click();

        cut.Find("[data-testid='llm-chat-definition-tab-runtime']").Click();
        cut.WaitForElement("[data-testid='llm-chat-definition-system-prompt']");
        Assert.NotNull(cut.FindComponent<ConversationProviderSelector>());
        Assert.NotNull(cut.FindComponent<ConversationProviderModelSelector>());
        Assert.NotNull(cut.FindComponent<ConversationTemperatureField>());

        cut.Find("[data-testid='llm-chat-definition-system-prompt']").Change("updated system prompt");
        cut.Find("[data-testid='llm-chat-definition-thinking-effort']").Change("1");
        cut.Find("[data-testid='llm-chat-definition-timeout']").Change("90");
        cut.Find("[data-testid='llm-chat-definition-tab-output']").Click();
        cut.WaitForElement("[data-testid='llm-chat-definition-response-format']");
        cut.Find("[data-testid='llm-chat-definition-response-format']").Change("2");
        cut.WaitForElement("[data-testid='llm-chat-definition-schema-json']");
        cut.Find("[data-testid='llm-chat-definition-schema-name']").Change("answer");
        cut.Find("[data-testid='llm-chat-definition-schema-description']").Change("Structured answer");
        cut.Find("[data-testid='llm-chat-definition-schema-json']").Input("{\"type\":\"object\"}");
        cut.Find("[data-testid='llm-chat-definition-revision-reason']").Change("Tune structured output");
        cut.Find("[data-testid='llm-chat-definition-editor-save']").Click();

        cut.WaitForAssertion(() => Assert.NotNull(gateway.UpdatedMutation));
        Assert.Equal(7, gateway.UpdateExpectedConcurrencyToken);
        Assert.Equal("updated system prompt", gateway.UpdatedMutation!.SystemPrompt);
        Assert.Equal("data:image/jpeg;base64,AQID", gateway.UpdatedMutation.AvatarImageUrl);
        Assert.Equal(LlmChatThinkingEffort.High, gateway.UpdatedMutation.ThinkingEffort);
        Assert.Equal(TimeSpan.FromSeconds(90), gateway.UpdatedMutation.Timeout);
        Assert.Equal(LlmChatUiResponseFormatKind.JsonSchema, gateway.UpdatedMutation.ResponseFormat);
        Assert.Equal("answer", gateway.UpdatedMutation.SchemaName);
        Assert.Equal(["research", "summary"], gateway.UpdatedMutation.Tags);
        Assert.Equal("Tune structured output", gateway.UpdatedMutation.RevisionReason);
    }

    [Fact]
    public void Selecting_bundled_avatar_saves_the_catalog_asset_path()
    {
        var gateway = new StubDefinitionGateway(CreateEditor());
        using var context = CreateContext(
            gateway,
            new StubProviderGateway(),
            new StubAuthorization(canRead: true, canManage: true));
        var cut = context.Render<LlmChatDefinitionEditorDialog>(parameters => parameters
            .Add(component => component.DefinitionId, DefinitionId));
        cut.WaitForElement("[data-testid='llm-chat-definition-editor-save']");

        cut.Find("[data-testid='llm-chat-definition-avatar-open']").Click();
        cut.WaitForElement("[data-testid='llm-chat-definition-avatar-option-2']");
        cut.Find("[data-testid='llm-chat-definition-avatar-option-2']").Click();
        cut.Find("[data-testid='llm-chat-definition-avatar-close']").Click();
        cut.Find("[data-testid='llm-chat-definition-editor-save']").Click();

        cut.WaitForAssertion(() => Assert.NotNull(gateway.UpdatedMutation));
        Assert.Equal(AgentAvatarImageCatalog.BundledAvatarUrls[1], gateway.UpdatedMutation!.AvatarImageUrl);
    }

    [Fact]
    public void Concurrency_conflict_is_sanitized_and_reload_replaces_stale_editor_state()
    {
        var initial = CreateEditor(
            name: "Stale name",
            summary: "Stale summary",
            concurrencyToken: 7);
        var refreshed = CreateEditor(
            name: "Current server name",
            summary: "Current server summary",
            concurrencyToken: 9);
        var gateway = new StubDefinitionGateway(initial, refreshed)
        {
            UpdateFailure = new(
                LlmChatErrorCodes.DefinitionConcurrencyConflict,
                "The definition changed after it was opened. Reload it before saving again.")
        };
        using var context = CreateContext(
            gateway,
            new StubProviderGateway(),
            new StubAuthorization(canRead: true, canManage: true));

        var cut = context.Render<LlmChatDefinitionEditorDialog>(parameters => parameters
            .Add(component => component.DefinitionId, DefinitionId));
        cut.WaitForElement("[data-testid='llm-chat-definition-editor-save']");
        var staleSummary = cut.Find("[data-testid='llm-chat-definition-summary']");
        staleSummary.Change("Unsaved stale summary");

        cut.Find("[data-testid='llm-chat-definition-editor-save']").Click();

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='llm-chat-definition-editor-reload']")));
        Assert.DoesNotContain("provider", cut.Find("[data-testid='llm-chat-definition-editor-error']").TextContent, StringComparison.OrdinalIgnoreCase);
        cut.Find("[data-testid='llm-chat-definition-editor-reload']").Click();

        cut.WaitForAssertion(() =>
        {
            var currentSummary = cut.Find("[data-testid='llm-chat-definition-summary']");
            Assert.Equal("Current server name", cut.Find("[data-testid='llm-chat-definition-name']").GetAttribute("value"));
            Assert.Equal("Current server summary", currentSummary.TextContent);
            Assert.NotSame(staleSummary, currentSummary);
            Assert.Equal(2, gateway.GetEditorCalls);
        });
    }

    [Fact]
    public void Draft_editor_exposes_only_declared_status_transitions()
    {
        var gateway = new StubDefinitionGateway(CreateEditor(status: LlmChatDefinitionStatus.Draft));
        using var context = CreateContext(
            gateway,
            new StubProviderGateway(),
            new StubAuthorization(canRead: true, canManage: true));
        var cut = context.Render<LlmChatDefinitionEditorDialog>(parameters => parameters
            .Add(component => component.DefinitionId, DefinitionId));

        cut.WaitForElement("[data-testid='llm-chat-definition-status-active']");
        Assert.NotNull(cut.Find("[data-testid='llm-chat-definition-status-archived']"));
        Assert.Empty(cut.FindAll("[data-testid='llm-chat-definition-status-suspended']"));
        cut.Find("[data-testid='llm-chat-definition-status-active']").Click();

        cut.WaitForAssertion(() => Assert.Equal(LlmChatDefinitionStatus.Active, gateway.RequestedStatus));
        Assert.Equal(7, gateway.StatusExpectedConcurrencyToken);
    }

    [Fact]
    public void Invalid_schema_stays_in_editor_and_never_calls_update_gateway()
    {
        var gateway = new StubDefinitionGateway(CreateEditor());
        using var context = CreateContext(
            gateway,
            new StubProviderGateway(),
            new StubAuthorization(canRead: true, canManage: true));
        var cut = context.Render<LlmChatDefinitionEditorDialog>(parameters => parameters
            .Add(component => component.DefinitionId, DefinitionId));
        cut.WaitForElement("[data-testid='llm-chat-definition-editor-save']");

        cut.Find("[data-testid='llm-chat-definition-tab-output']").Click();
        cut.WaitForElement("[data-testid='llm-chat-definition-response-format']");
        cut.Find("[data-testid='llm-chat-definition-response-format']").Change("2");
        cut.WaitForElement("[data-testid='llm-chat-definition-schema-json']");
        cut.Find("[data-testid='llm-chat-definition-schema-name']").Change("answer");
        cut.Find("[data-testid='llm-chat-definition-schema-json']").Input("not-json");
        cut.Find("[data-testid='llm-chat-definition-editor-save']").Click();

        cut.WaitForAssertion(() => Assert.Contains(
            "valid JSON schema",
            cut.Find("[data-testid='llm-chat-definition-editor-error']").TextContent,
            StringComparison.Ordinal));
        Assert.Null(gateway.UpdatedMutation);
    }

    [Theory]
    [InlineData("0", "gemma4-12b-256k")]
    [InlineData("1", "gptoss20b64k")]
    public void New_ollama_definition_saves_the_selected_concrete_model(
        string modelChoiceKey,
        string expectedModel)
    {
        var gateway = new StubDefinitionGateway(CreateEditor());
        using var context = CreateContext(
            gateway,
            new StubProviderGateway("gemma4-12b-256k", "gptoss20b64k"),
            new StubAuthorization(canRead: true, canManage: true));
        var cut = context.Render<LlmChatDefinitionEditorDialog>();
        cut.WaitForElement("[data-testid='llm-chat-definition-editor-save']");

        cut.Find("[data-testid='llm-chat-definition-name']").Change("Ollama chat");
        cut.Find("[data-testid='llm-chat-definition-tab-runtime']").Click();
        cut.WaitForElement("[data-testid='llm-chat-definition-provider']");
        cut.Find("[data-testid='llm-chat-definition-provider']").Change("0");
        cut.Find("[data-testid='llm-chat-definition-model']").Change(modelChoiceKey);
        cut.Find("[data-testid='llm-chat-definition-editor-save']").Click();

        cut.WaitForAssertion(() => Assert.NotNull(gateway.CreatedMutation));
        Assert.Equal(ProviderId, gateway.CreatedMutation!.ProviderProfileId);
        Assert.Equal(expectedModel, gateway.CreatedMutation.Model);
        Assert.DoesNotContain(
            "Select or enter a model",
            cut.Markup,
            StringComparison.OrdinalIgnoreCase);
    }

    private static BunitContext CreateContext(
        ILlmChatDefinitionUiGateway definitions,
        ILlmChatProviderUiGateway providers,
        ILlmChatUiAuthorizationFacade authorization,
        IAvatarGenerationGateway? avatarGeneration = null)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
        context.Services.AddSingleton(definitions);
        context.Services.AddSingleton(providers);
        context.Services.AddSingleton(authorization);
        context.Services.AddSingleton<IAvatarGenerationGateway>(avatarGeneration ?? new StubAvatarGenerationGateway());
        return context;
    }

    private static LlmChatDefinitionEditor CreateEditor(
        string name = "Research assistant",
        string summary = "Summarizes research.",
        long concurrencyToken = 7,
        LlmChatDefinitionStatus status = LlmChatDefinitionStatus.Draft,
        Guid? definitionId = null,
        IReadOnlyList<string>? tags = null,
        string avatarUrl = "")
    {
        var definition = new LlmChatDefinitionListItem(
            definitionId ?? DefinitionId,
            name,
            summary,
            avatarUrl,
            status,
            3,
            concurrencyToken,
            DateTimeOffset.Parse("2026-08-16T12:00:00Z"),
            tags ?? ["research"]);
        return new(
            definition,
            "secret system prompt",
            ProviderId,
            "Primary provider",
            "model-a",
            0.2,
            LlmChatThinkingEffort.Low,
            "{}",
            TimeSpan.FromSeconds(30),
            LlmChatUiResponseFormatKind.Text,
            string.Empty,
            string.Empty,
            string.Empty,
            "Previous revision");
    }

    private sealed class StubDefinitionGateway(params LlmChatDefinitionEditor[] editorValues)
        : ILlmChatDefinitionUiGateway
    {
        private readonly Queue<LlmChatDefinitionEditor> editors = new(editorValues);
        private LlmChatDefinitionEditor current = editorValues[0];

        public int GetEditorCalls { get; private set; }

        public IReadOnlyList<LlmChatDefinitionListItem>? ListItems { get; init; }

        public List<LlmChatDefinitionQuery> ListQueries { get; } = [];

        public LlmChatDefinitionMutation? UpdatedMutation { get; private set; }

        public LlmChatDefinitionMutation? CreatedMutation { get; private set; }

        public long? UpdateExpectedConcurrencyToken { get; private set; }

        public LlmChatDefinitionStatus? RequestedStatus { get; private set; }

        public long? StatusExpectedConcurrencyToken { get; private set; }

        public LlmChatUiFailure? UpdateFailure { get; init; }

        public Task<LlmChatUiResult<LlmChatPage<LlmChatDefinitionListItem, LlmChatDefinitionCursor>>> ListPageAsync(
            LlmChatDefinitionQuery query,
            CancellationToken cancellationToken = default)
        {
            ListQueries.Add(query);
            var items = (ListItems ?? [current.Definition])
                .Where(item => query.Status is null || item.Status == query.Status)
                .Where(item => MatchesSearch(item, query.SearchText))
                .Where(item => query.Tags.All(filter =>
                    item.Tags.Contains(filter, StringComparer.OrdinalIgnoreCase)))
                .Take(query.Take)
                .ToArray();
            return Task.FromResult(LlmChatUiResult<LlmChatPage<LlmChatDefinitionListItem, LlmChatDefinitionCursor>>.Success(
                new(items, null)));
        }

        public Task<LlmChatUiResult<LlmChatDefinitionListItem>> GetAsync(
            Guid definitionId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(LlmChatUiResult<LlmChatDefinitionListItem>.Success(current.Definition));

        public Task<LlmChatUiResult<LlmChatDefinitionEditor>> GetEditorAsync(
            Guid definitionId,
            CancellationToken cancellationToken = default)
        {
            GetEditorCalls++;
            if (editors.Count > 0)
            {
                current = editors.Dequeue();
            }

            return Task.FromResult(LlmChatUiResult<LlmChatDefinitionEditor>.Success(current));
        }

        public Task<LlmChatUiResult<LlmChatDefinitionEditor>> CreateAsync(
            LlmChatDefinitionMutation mutation,
            CancellationToken cancellationToken = default)
        {
            CreatedMutation = mutation;
            return Task.FromResult(LlmChatUiResult<LlmChatDefinitionEditor>.Success(current));
        }

        public Task<LlmChatUiResult<LlmChatDefinitionEditor>> UpdateAsync(
            Guid definitionId,
            LlmChatDefinitionMutation mutation,
            long expectedConcurrencyToken,
            CancellationToken cancellationToken = default)
        {
            UpdatedMutation = mutation;
            UpdateExpectedConcurrencyToken = expectedConcurrencyToken;
            return Task.FromResult(UpdateFailure is null
                ? LlmChatUiResult<LlmChatDefinitionEditor>.Success(current)
                : LlmChatUiResult<LlmChatDefinitionEditor>.Failure(UpdateFailure));
        }

        public Task<LlmChatUiResult<LlmChatDefinitionListItem>> ChangeStatusAsync(
            Guid definitionId,
            LlmChatDefinitionStatus status,
            long expectedConcurrencyToken,
            CancellationToken cancellationToken = default)
        {
            RequestedStatus = status;
            StatusExpectedConcurrencyToken = expectedConcurrencyToken;
            return Task.FromResult(LlmChatUiResult<LlmChatDefinitionListItem>.Success(
                current.Definition with { Status = status }));
        }

        private static bool MatchesSearch(LlmChatDefinitionListItem definition, string searchText)
        {
            if (searchText.Length == 0)
            {
                return true;
            }

            return definition.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                   definition.Summary.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                   definition.Tags.Any(tag => tag.Contains(searchText, StringComparison.OrdinalIgnoreCase));
        }
    }

    private sealed class StubProviderGateway(
        string defaultModel = "model-a",
        string? suggestedModel = null, bool sourceManaged = false) : ILlmChatProviderUiGateway
    {
        public Task<LlmChatUiResult<IReadOnlyList<LlmChatProviderOptionPresentation>>> ListAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult(LlmChatUiResult<IReadOnlyList<LlmChatProviderOptionPresentation>>.Success(
            [
                new(
                    ProviderId,
                    "Primary provider",
                    [
                        new(
                            defaultModel,
                            new(
                                LlmChatThinkingEffortSupport.Supported,
                                LlmChatThinkingEffortControl.EffortLevels,
                                [LlmChatThinkingEffort.Low, LlmChatThinkingEffort.High],
                                LlmChatThinkingEffort.Low)) {
                            DisplayName = sourceManaged ? "Readable default" : defaultModel
                        },
                        .. suggestedModel is null
                            ? []
                            : new LlmChatModelOptionPresentation[]
                            {
                                new(
                                    suggestedModel,
                                    new(
                                        LlmChatThinkingEffortSupport.Supported,
                                        LlmChatThinkingEffortControl.EffortLevels,
                                        [LlmChatThinkingEffort.Low, LlmChatThinkingEffort.High],
                                        LlmChatThinkingEffort.Low)) {
                                    DisplayName = sourceManaged ? "Readable secondary" : suggestedModel
                                }
                            }
                    ]) { IsSourceManaged = sourceManaged }
            ]));
    }

    private sealed class StubAuthorization(bool canRead, bool canManage)
        : ILlmChatUiAuthorizationFacade
    {
        public ValueTask<LlmChatUiAuthorizationSnapshot> GetAsync(
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult(new LlmChatUiAuthorizationSnapshot(canRead, canManage, false));

        public ValueTask<bool> IsAllowedAsync(
            LlmChatUiPermission permission,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult(permission switch
            {
                LlmChatUiPermission.Read => canRead,
                LlmChatUiPermission.Manage => canManage,
                LlmChatUiPermission.Execute => false,
                _ => throw new ArgumentOutOfRangeException(nameof(permission), permission, "Unknown permission.")
            });
    }

    private sealed class StubAvatarGenerationGateway : IAvatarGenerationGateway
    {
        public int GenerateCalls { get; private set; }

        public Task<AvatarGenerationSource?> GetDefaultSourceAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult<AvatarGenerationSource?>(new(
                ProviderId,
                "Image provider",
                "image-model"));

        public Task<AvatarGenerationResult> GenerateAsync(
            AvatarGenerationRequest request,
            CancellationToken cancellationToken = default)
        {
            GenerateCalls++;
            return Task.FromResult(new AvatarGenerationResult("data:image/jpeg;base64,AQID"));
        }
    }
}
