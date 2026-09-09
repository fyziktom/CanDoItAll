using CanDoItAll.AgentFramework.Llm.SimpleChats.Application;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Common;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Components;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Definitions;
using CanDoItAll.AgentFramework.UiSandbox;
using CatalogResult = CanDoItAll.AgentFramework.Llm.SimpleChats.Components.LlmChatUiResult<CanDoItAll.AgentFramework.Llm.SimpleChats.Common.LlmChatPage<CanDoItAll.AgentFramework.Llm.SimpleChats.Components.LlmChatDefinitionListItem, CanDoItAll.AgentFramework.Llm.SimpleChats.Common.LlmChatDefinitionCursor>>;

internal enum DefinitionCatalogBrowserMode { Normal, ReadOnly, Denied, FailList, ProviderFailure, Conflict }

internal sealed class DefinitionCatalogBrowserFixture : ILlmChatDefinitionUiGateway, ILlmChatUiAuthorizationFacade, ILlmChatProviderUiGateway {
    public const string PrivatePrompt = "definition-browser-private-prompt";
    private static readonly Guid ProviderId = Guid.Parse("31000000-0000-0000-0000-000000000099");
    private readonly List<LlmChatDefinitionListItem> items = Enumerable.Range(1, 27).Select(index => new LlmChatDefinitionListItem(
        Guid.Parse($"31000000-0000-0000-0000-{index:D12}"), index == 1 ? "Research assistant" : index == 2 ? "Operations assistant"
            : index == 3 ? "<script id='definitions-injected'>unsafe()</script> " + new string('界', 240) : $"Definition {index:D2}",
        index == 3 ? new string('W', 500) : "Synthetic browser catalog record.", "",
        index == 2 ? LlmChatDefinitionStatus.Suspended : LlmChatDefinitionStatus.Active, 3, 1,
        new(2026, 9, 8, 12, 0, 0, TimeSpan.Zero), index == 2 ? ["operations"] : ["research"])).ToList();
    public DefinitionCatalogBrowserMode Mode { get; set; }
    public bool ExecutionEnabled { get; set; }
    private readonly Dictionary<Guid, LlmChatDefinitionMutation> drafts = [];
    public int StatusChanges { get; private set; }
    public int ListReads { get; private set; }
    public int EditorReads { get; private set; }
    public int Saves { get; private set; }
    public string Search { get; private set; } = "";
    public string[] Tags { get; private set; } = [];
    public LlmChatDefinitionStatus? Status { get; private set; }
    private bool CanRead => Mode != DefinitionCatalogBrowserMode.Denied;
    private bool CanManage => CanRead && Mode != DefinitionCatalogBrowserMode.ReadOnly;

    public ValueTask<LlmChatUiAuthorizationSnapshot> GetAsync(CancellationToken cancellationToken = default)
        => ValueTask.FromResult(new LlmChatUiAuthorizationSnapshot(CanRead, CanManage, CanManage && ExecutionEnabled));
    public ValueTask<bool> IsAllowedAsync(LlmChatUiPermission permission, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(permission switch { LlmChatUiPermission.Read => CanRead, LlmChatUiPermission.Manage => CanManage, LlmChatUiPermission.Execute => CanManage && ExecutionEnabled, _ => false });
    public Task<CatalogResult> ListPageAsync(LlmChatDefinitionQuery query, CancellationToken cancellationToken = default) {
        if (!CanRead) {
            throw new InvalidOperationException("Denied catalog must not read.");
        }
        ListReads++;
        Search = query.SearchText;
        Tags = query.Tags.ToArray();
        Status = query.Status;
        if (Mode == DefinitionCatalogBrowserMode.FailList) {
            throw new IOException(PrivatePrompt);
        }
        var matches = items.Where(item => (query.Status is null || item.Status == query.Status)
            && query.Tags.All(tag => item.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase))
            && (item.Name.Contains(query.SearchText, StringComparison.OrdinalIgnoreCase) || item.Summary.Contains(query.SearchText, StringComparison.OrdinalIgnoreCase))).ToArray();
        var offset = query.Cursor is { } cursor ? Array.FindIndex(matches, item => item.DefinitionId == cursor.DefinitionId.Value) + 1 : 0;
        var page = matches.Skip(offset).Take(query.Take).ToArray();
        LlmChatDefinitionCursor? next = offset + page.Length < matches.Length ? new(page[^1].UpdatedAtUtc, new(page[^1].DefinitionId)) : null;
        return Task.FromResult(CatalogResult.Success(new(page, next)));
    }
    public Task<LlmChatUiResult<LlmChatDefinitionListItem>> GetAsync(Guid definitionId, CancellationToken cancellationToken = default)
        => Task.FromResult(LlmChatUiResult<LlmChatDefinitionListItem>.Success(items.Single(item => item.DefinitionId == definitionId)));
    public Task<LlmChatUiResult<LlmChatDefinitionEditor>> GetEditorAsync(Guid definitionId, CancellationToken cancellationToken = default) {
        if (!CanManage) {
            throw new InvalidOperationException("Read-only catalog must not read editor data.");
        }
        EditorReads++;
        return Task.FromResult(LlmChatUiResult<LlmChatDefinitionEditor>.Success(Editor(items.Single(item => item.DefinitionId == definitionId))));
    }
    public Task<LlmChatUiResult<LlmChatDefinitionEditor>> CreateAsync(LlmChatDefinitionMutation mutation, CancellationToken cancellationToken = default) {
        Saves++;
        var item = new LlmChatDefinitionListItem(Guid.NewGuid(), mutation.Name, mutation.Summary, mutation.AvatarImageUrl,
            LlmChatDefinitionStatus.Draft, 1, 1, DateTimeOffset.UtcNow, mutation.Tags.ToArray());
        items.Insert(0, item);
        drafts[item.DefinitionId] = mutation;
        return Task.FromResult(LlmChatUiResult<LlmChatDefinitionEditor>.Success(Editor(item)));
    }
    public Task<LlmChatUiResult<LlmChatDefinitionEditor>> UpdateAsync(Guid definitionId, LlmChatDefinitionMutation mutation,
        long expectedConcurrencyToken, CancellationToken cancellationToken = default) {
        Saves++;
        if (Mode == DefinitionCatalogBrowserMode.Conflict) {
            return Task.FromResult(LlmChatUiResult<LlmChatDefinitionEditor>.Failure(new LlmChatUiFailure(LlmChatErrorCodes.DefinitionConcurrencyConflict, "Synthetic conflict")));
        }
        var index = items.FindIndex(item => item.DefinitionId == definitionId);
        items[index] = items[index] with { Name = mutation.Name, Summary = mutation.Summary, AvatarImageUrl = mutation.AvatarImageUrl, Tags = mutation.Tags.ToArray(), ConcurrencyToken = expectedConcurrencyToken + 1, Revision = items[index].Revision + 1 };
        drafts[definitionId] = mutation;
        return Task.FromResult(LlmChatUiResult<LlmChatDefinitionEditor>.Success(Editor(items[index])));
    }
    public Task<LlmChatUiResult<LlmChatDefinitionListItem>> ChangeStatusAsync(Guid definitionId, LlmChatDefinitionStatus status,
        long expectedConcurrencyToken, CancellationToken cancellationToken = default) {
        StatusChanges++;
        var index = items.FindIndex(item => item.DefinitionId == definitionId);
        items[index] = items[index] with { Status = status, ConcurrencyToken = expectedConcurrencyToken + 1 };
        return Task.FromResult(LlmChatUiResult<LlmChatDefinitionListItem>.Success(items[index]));
    }
    public Task<LlmChatUiResult<IReadOnlyList<LlmChatProviderOptionPresentation>>> ListAsync(CancellationToken cancellationToken = default)
        => Mode == DefinitionCatalogBrowserMode.ProviderFailure ? throw new IOException("Synthetic provider catalog failure")
            : Task.FromResult(LlmChatUiResult<IReadOnlyList<LlmChatProviderOptionPresentation>>.Success([
                new(ProviderId, "Synthetic rendering provider", [new("fixture-model", new(LlmChatThinkingEffortSupport.Supported,
                    LlmChatThinkingEffortControl.EffortLevels, [LlmChatThinkingEffort.Low, LlmChatThinkingEffort.High], null))])
            ]));
    private LlmChatDefinitionEditor Editor(LlmChatDefinitionListItem item) => drafts.TryGetValue(item.DefinitionId, out var value)
        ? new(item, value.SystemPrompt, value.ProviderProfileId, "Synthetic rendering provider", value.Model, value.Temperature, value.ThinkingEffort,
            value.ModelParameterConfigurationJson, value.Timeout, value.ResponseFormat, value.SchemaJson, value.SchemaName, value.SchemaDescription, value.RevisionReason)
        : new(item, PrivatePrompt, ProviderId, "Synthetic rendering provider", "fixture-model", null, null, "{}", TimeSpan.FromSeconds(30), LlmChatUiResponseFormatKind.Text, "", "", "", "Browser fixture");
}
