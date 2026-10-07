using System.Collections.Immutable;
using Bunit;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Common;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Components;
using CanDoItAll.AgentFramework.Llm.SimpleChats.UI;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging.Abstractions;
using static CanDoItAll.Tests.Components.LlmChats.LlmChatDefinitionUiTests;

namespace CanDoItAll.Tests.Components.LlmChats;

public sealed class DefinitionEditorProviderOrderingTests {
    private static readonly Guid FirstProvider = Guid.Parse("20202020-2020-2020-2020-202020202020");
    private static readonly Guid SecondProvider = Guid.Parse("30303030-3030-3030-3030-303030303030");
    private static readonly IReadOnlyList<LlmChatProviderOptionPresentation> Providers = [
        Provider(FirstProvider, "OpenAI", "openai"),
        Provider(SecondProvider, "Ollama", "ollama")
    ];

    [Fact]
    public async Task Held_provider_read_keeps_acquired_edits_and_acknowledged_selection_uses_exact_models() {
        var pending = Pending();
        var gateway = new StubDefinitionGateway(CreateEditor());
        using var context = CreateContext(gateway, new ControlledProviders(_ => pending.Task), new StubAuthorization(true, true));
        var cut = context.Render<LlmChatDefinitionEditorDialog>();
        await ChangeAsync(cut, "llm-chat-definition-name", "Typed before metadata");
        await ClickAsync(cut, "llm-chat-definition-tab-runtime");
        Assert.True(cut.Find("[data-testid='llm-chat-definition-provider']").HasAttribute("disabled"));
        await ChangeAsync(cut, "llm-chat-definition-system-prompt", "Keep my prompt");
        pending.SetResult(Success());
        cut.WaitForAssertion(() => Assert.False(cut.Find("[data-testid='llm-chat-definition-provider']").HasAttribute("disabled")));
        await ChangeAsync(cut, "llm-chat-definition-provider", "1");
        AssertModels(cut, "ollama");
        await ChangeAsync(cut, "llm-chat-definition-model", "2");
        await ClickAsync(cut, "llm-chat-definition-editor-save");
        Assert.Equal(SecondProvider, gateway.CreatedMutation!.ProviderProfileId);
        Assert.Equal("ollama-vision", gateway.CreatedMutation.Model);
        Assert.Equal("Typed before metadata", gateway.CreatedMutation.Name);
        Assert.Equal("Keep my prompt", gateway.CreatedMutation.SystemPrompt);
    }

    [Fact]
    public async Task Ordered_provider_changes_and_same_source_echo_preserve_draft_and_exact_selected_identity() {
        using var session = Session(new ControlledProviders(_ => Task.FromResult(Success())));
        await session.SetTargetAsync(null);
        using var context = CreateContext(new StubDefinitionGateway(CreateEditor()), new StubProviderGateway(), new StubAuthorization(true, true));
        var intents = new List<DefinitionEditorIntent>();
        var cut = context.Render<LlmChatDefinitionEditorSurface>(p => p.Add(x => x.Presentation, session.Presentation).Add(x => x.Intent, intent => intents.Add(intent)));
        await ChangeAsync(cut, "llm-chat-definition-name", "Unicode Žluťoučký");
        await ClickAsync(cut, "llm-chat-definition-tab-runtime");
        foreach (var (index, model) in new[] { ("0", "openai"), ("1", "ollama"), ("0", "openai"), ("1", "ollama") }) {
            await ChangeAsync(cut, "llm-chat-definition-provider", index);
            AssertModels(cut, model);
        }
        await ChangeAsync(cut, "llm-chat-definition-model", "1");
        cut.Render(p => p.Add(x => x.Presentation, session.Presentation with { ProvidersLoading = false }));
        AssertModels(cut, "ollama");
        await ClickAsync(cut, "llm-chat-definition-editor-save");
        var values = Assert.Single(intents).Submission!;
        Assert.Equal("Unicode Žluťoučký", values.Name);
        Assert.Equal(SecondProvider, values.ProviderProfileId);
        Assert.Equal("ollama-secondary", values.Model);
    }

    [Fact]
    public async Task Metadata_arriving_after_local_selection_rebuilds_options_without_replacing_model_or_edits() {
        using var session = Session(new ControlledProviders(_ => Task.FromResult(Success())));
        await session.SetTargetAsync(null);
        var complete = session.Presentation;
        var limited = complete with { Providers = complete.Providers.Select(p => p with {
            Option = p.Option with { SuggestedModels = [p.Option.DefaultModel] }
        }).ToImmutableArray() };
        using var context = CreateContext(new StubDefinitionGateway(CreateEditor()), new StubProviderGateway(), new StubAuthorization(true, true));
        var intents = new List<DefinitionEditorIntent>();
        var cut = context.Render<LlmChatDefinitionEditorSurface>(p => p.Add(x => x.Presentation, limited).Add(x => x.Intent, intent => intents.Add(intent)));
        await ChangeAsync(cut, "llm-chat-definition-name", "Local edit");
        await ClickAsync(cut, "llm-chat-definition-tab-runtime");
        await ChangeAsync(cut, "llm-chat-definition-provider", "1");
        Assert.Single(cut.FindAll("[data-testid='llm-chat-definition-model'] option"));
        cut.Render(p => p.Add(x => x.Presentation, complete));
        AssertModels(cut, "ollama");
        await ClickAsync(cut, "llm-chat-definition-editor-save");
        var values = Assert.Single(intents).Submission!;
        Assert.Equal("Local edit", values.Name);
        Assert.Equal(SecondProvider, values.ProviderProfileId);
        Assert.Equal("ollama-default", values.Model);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Retired_A_provider_read_cannot_change_successor_A_after_B(bool failure) {
        var pending = Pending();
        var calls = 0;
        using var session = Session(new ControlledProviders(_ => ++calls == 1 ? pending.Task : Task.FromResult(Success())));
        var first = session.SetTargetAsync(null);
        var originalSource = session.Presentation.Source;
        await session.SetTargetAsync(CreateEditor().Definition.DefinitionId);
        await session.SetTargetAsync(null);
        var successor = session.Presentation;
        Assert.NotSame(originalSource, successor.Source);
        pending.SetResult(failure ? LlmChatUiResult<IReadOnlyList<LlmChatProviderOptionPresentation>>.Failure(new LlmChatUiFailure("test.failure", "Retired")) : Success());
        await first;
        Assert.Same(successor, session.Presentation);
        Assert.False(session.Presentation.ProvidersLoading);
        Assert.False(session.Presentation.ProvidersUnavailable);
        Assert.Equal(SecondProvider.ToString("D"), session.Presentation.Providers[1].Option.Key.Value);
    }

    [Fact]
    public async Task Cancelled_editor_has_no_late_provider_publication_and_independent_editor_completes() {
        var pending = Pending();
        using var closed = Session(new ControlledProviders(_ => pending.Task));
        var first = closed.SetTargetAsync(null);
        Assert.True(closed.Cancel(closed.Presentation.Generation));
        var publications = 0;
        closed.Changed += () => publications++;
        using var independent = Session(new ControlledProviders(_ => Task.FromResult(Success())));
        await independent.SetTargetAsync(null);
        pending.SetResult(Success());
        await first;
        Assert.Equal(0, publications);
        Assert.Equal(DefinitionEditorPhase.Ready, independent.Presentation.Phase);
        Assert.Equal(2, independent.Presentation.Providers.Length);
        Assert.False(independent.Presentation.ProvidersLoading);
    }

    private static Task ChangeAsync<T>(IRenderedComponent<T> cut, string testId, string value) where T : IComponent
        => cut.InvokeAsync(() => cut.Find($"[data-testid='{testId}']").ChangeAsync(new ChangeEventArgs { Value = value }));

    private static Task ClickAsync<T>(IRenderedComponent<T> cut, string testId) where T : IComponent
        => cut.InvokeAsync(() => cut.Find($"[data-testid='{testId}']").ClickAsync());

    private static void AssertModels<T>(IRenderedComponent<T> cut, string model) where T : IComponent {
        Assert.Equal(new[] { $"Provider default ({model}-default)", $"{model}-secondary", $"{model}-vision" },
            cut.FindAll("[data-testid='llm-chat-definition-model'] option").Select(option => option.TextContent));
        Assert.Empty(cut.FindAll("[data-testid='llm-chat-definition-model-override']"));
    }

    private static LlmChatProviderOptionPresentation Provider(Guid id, string name, string prefix) => new(id, name,
        new[] { "default", "secondary", "vision" }.Select(suffix => new LlmChatModelOptionPresentation($"{prefix}-{suffix}",
            new(LlmChatThinkingEffortSupport.Unsupported, LlmChatThinkingEffortControl.EffortLevels, [], null))).ToArray()) { IsSourceManaged = true };

    private static TaskCompletionSource<LlmChatUiResult<IReadOnlyList<LlmChatProviderOptionPresentation>>> Pending()
        => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static LlmChatUiResult<IReadOnlyList<LlmChatProviderOptionPresentation>> Success()
        => LlmChatUiResult<IReadOnlyList<LlmChatProviderOptionPresentation>>.Success(Providers);

    private static LlmChatDefinitionEditorSession Session(ILlmChatProviderUiGateway providers)
        => new(new StubDefinitionGateway(CreateEditor()), providers, new StubAuthorization(true, true), NullLogger<LlmChatDefinitionEditorSession>.Instance);

    private sealed class ControlledProviders(Func<CancellationToken, Task<LlmChatUiResult<IReadOnlyList<LlmChatProviderOptionPresentation>>>> read) : ILlmChatProviderUiGateway {
        public Task<LlmChatUiResult<IReadOnlyList<LlmChatProviderOptionPresentation>>> ListAsync(CancellationToken cancellationToken = default) => read(cancellationToken);
    }
}
