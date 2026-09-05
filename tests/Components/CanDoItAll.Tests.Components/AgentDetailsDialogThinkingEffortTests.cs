using System.Reflection;
using Bunit;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using CanDoItAll.Modules.Workspace;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ProviderKind = CanDoItAll.AgentFramework.Models.ProviderKind;
using ProviderProfile = CanDoItAll.AgentFramework.Models.ProviderProfile;
using IProviderRuntimeAdministrationService = CanDoItAll.Modules.AgentFramework.ProviderManagement.IProviderRuntimeAdministrationService;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class AgentDetailsDialogThinkingEffortTests
{
    private const string ThinkingEffortTestId = "agents-catalog-thinking-effort";
    private const string ThinkingEffortSupportTestId = "agents-catalog-thinking-effort-support";
    private const string ModelChoiceTestId = "agents-catalog-model-choice";
    private const string ModelOverrideTestId = "agents-catalog-model-override";
    private const string ModelInputTestId = "agents-catalog-model";
    private const string ProviderTestId = "agents-catalog-provider";
    private const string SaveTestId = "agents-catalog-save";
    private const string UnknownModel = "custom-deployment-west";

    [Fact]
    public void Supported_override_selection_updates_the_editor_and_can_be_saved()
    {
        using var context = CreateContext(out var workspaceProxy);
        var provider = CreateProvider("OpenAI reasoning", OpenAiModelIds.Gpt56Sol);
        var editor = CreateEditor(provider);
        var cut = RenderRuntimeTab(context, editor, [provider]);

        ChangeSelectToLabel(cut, ThinkingEffortTestId, "High");

        Assert.Equal(AgentReasoningEffortLevel.High, editor.ThinkingEffortOverride);
        Assert.False(FindSaveButton(cut).HasAttribute("disabled"));

        FindSaveButton(cut).Click();

        cut.WaitForAssertion(() =>
            Assert.Equal(
                AgentReasoningEffortLevel.High,
                Assert.Single(workspaceProxy.SavedThinkingEfforts)));
    }

    [Fact]
    public void Switching_to_an_unsupported_provider_preserves_the_override_and_disables_save()
    {
        using var context = CreateContext(out var workspaceProxy);
        var supportedProvider = CreateProvider("OpenAI reasoning", OpenAiModelIds.Gpt56Sol);
        var unsupportedProvider = CreateProvider("OpenAI non-reasoning", "gpt-4.1");
        var editor = CreateEditor(supportedProvider);
        var cut = RenderRuntimeTab(context, editor, [supportedProvider, unsupportedProvider]);

        ChangeSelectToLabel(cut, ThinkingEffortTestId, "High");
        ChangeSelectToLabel(cut, ProviderTestId, unsupportedProvider.Name);

        Assert.Equal(unsupportedProvider.Id, editor.ProviderProfileId);
        Assert.Equal(string.Empty, editor.Model);
        Assert.Equal(AgentReasoningEffortLevel.High, editor.ThinkingEffortOverride);
        Assert.True(FindSaveButton(cut).HasAttribute("disabled"));
        Assert.False(FindSelect(cut, ThinkingEffortTestId).HasAttribute("disabled"));
        Assert.Empty(workspaceProxy.SavedThinkingEfforts);

        var guidance = cut.Find($"[data-testid='{ThinkingEffortSupportTestId}']").TextContent;
        Assert.Contains("cannot be applied", guidance, StringComparison.Ordinal);
        Assert.Contains("does not support configurable thinking effort", guidance, StringComparison.Ordinal);
        Assert.DoesNotContain("not defined", guidance, StringComparison.Ordinal);
    }

    [Fact]
    public void Selecting_provider_default_clears_an_incompatible_override_and_saves_null()
    {
        using var context = CreateContext(out var workspaceProxy);
        var provider = CreateProvider("OpenAI non-reasoning", "gpt-4.1");
        var editor = CreateEditor(provider);
        editor.ThinkingEffortOverride = AgentReasoningEffortLevel.High;
        var cut = RenderRuntimeTab(context, editor, [provider]);

        Assert.True(FindSaveButton(cut).HasAttribute("disabled"));

        ChangeSelectToLabel(cut, ThinkingEffortTestId, "Provider default");

        Assert.Null(editor.ThinkingEffortOverride);
        Assert.True(editor.IsThinkingEffortOverrideEdited);
        Assert.False(FindSaveButton(cut).HasAttribute("disabled"));

        FindSaveButton(cut).Click();

        cut.WaitForAssertion(() =>
            Assert.Null(Assert.Single(workspaceProxy.SavedThinkingEfforts)));
    }

    [Fact]
    public void Switching_to_an_unknown_model_preserves_the_override_and_blocks_persistence()
    {
        using var context = CreateContext(out var workspaceProxy);
        var provider = CreateProvider(
            "OpenAI reasoning",
            OpenAiModelIds.Gpt56Sol,
            [OpenAiModelIds.Gpt56Sol, UnknownModel]);
        var editor = CreateEditor(provider);
        var cut = RenderRuntimeTab(context, editor, [provider]);

        ChangeSelectToLabel(cut, ThinkingEffortTestId, "High");
        cut.Find($"[data-testid='{ModelOverrideTestId}']").Change(true);
        cut.Find($"[data-testid='{ModelInputTestId}']").Input(UnknownModel);

        Assert.Equal(UnknownModel, editor.Model);
        Assert.Equal(AgentReasoningEffortLevel.High, editor.ThinkingEffortOverride);
        Assert.True(FindSaveButton(cut).HasAttribute("disabled"));
        Assert.Empty(workspaceProxy.SavedThinkingEfforts);

        var guidance = cut.Find($"[data-testid='{ThinkingEffortSupportTestId}']").TextContent;
        Assert.Contains("cannot be applied", guidance, StringComparison.Ordinal);
        Assert.Contains("not defined", guidance, StringComparison.Ordinal);
        Assert.DoesNotContain("does not support configurable thinking effort", guidance, StringComparison.Ordinal);
    }

    [Fact]
    public void Invalid_provider_default_blocks_save_until_a_supported_agent_override_is_selected()
    {
        using var context = CreateContext(out var workspaceProxy);
        var provider = CreateProvider(
            "OpenAI invalid default",
            "gpt-5.4",
            providerDefault: AgentReasoningEffortLevel.Max);
        var editor = CreateEditor(provider);
        var cut = RenderRuntimeTab(context, editor, [provider]);

        Assert.True(FindSaveButton(cut).HasAttribute("disabled"));
        Assert.Contains(
            "provider default cannot be applied",
            cut.Find($"[data-testid='{ThinkingEffortSupportTestId}']").TextContent,
            StringComparison.OrdinalIgnoreCase);

        ChangeSelectToLabel(cut, ThinkingEffortTestId, "High");

        Assert.Equal(AgentReasoningEffortLevel.High, editor.ThinkingEffortOverride);
        Assert.False(FindSaveButton(cut).HasAttribute("disabled"));
        Assert.Empty(workspaceProxy.SavedThinkingEfforts);
    }

    [Fact]
    public void Switching_provider_clears_custom_model_before_validating_the_new_default()
    {
        using var context = CreateContext(out _);
        var currentProvider = CreateProvider("OpenAI reasoning", OpenAiModelIds.Gpt56Sol);
        var invalidDefaultProvider = CreateProvider(
            "OpenAI invalid default",
            "gpt-5.4",
            providerDefault: AgentReasoningEffortLevel.Max);
        var editor = CreateEditor(currentProvider);
        var cut = RenderRuntimeTab(
            context,
            editor,
            [currentProvider, invalidDefaultProvider]);

        cut.Find($"[data-testid='{ModelOverrideTestId}']").Change(true);
        cut.Find($"[data-testid='{ModelInputTestId}']").Input(UnknownModel);
        Assert.Equal(UnknownModel, editor.Model);

        ChangeSelectToLabel(cut, ProviderTestId, invalidDefaultProvider.Name);

        Assert.Equal(string.Empty, editor.Model);
        Assert.Equal(
            "Provider default (unavailable)",
            FindSelect(cut, ThinkingEffortTestId)
                .QuerySelector("option:checked")!
                .TextContent
                .Trim());
        Assert.True(FindSaveButton(cut).HasAttribute("disabled"));
        Assert.Contains(
            "provider default cannot be applied",
            cut.Find($"[data-testid='{ThinkingEffortSupportTestId}']").TextContent,
            StringComparison.OrdinalIgnoreCase);
    }

    private static BunitContext CreateContext(out RecordingWorkspaceServiceProxy workspaceProxy)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
        context.Services.AddSingleton<IExternalTargetPathRegistryFactory>(new ExternalTargetPathRegistryFactory());
        context.Services.AddSingleton<IStorageCatalogSelectionSource>(new EmptyStorageCatalogSelectionSource());
        context.Services.AddSingleton(new AgentAvatarGenerationService(
            new UnavailableAgentImageGenerationService(),
            NullLogger<AgentAvatarGenerationService>.Instance));

        var workspaceService =
            DispatchProxy.Create<IAgentFrameworkWorkspaceService, RecordingWorkspaceServiceProxy>();
        workspaceProxy = (RecordingWorkspaceServiceProxy)(object)workspaceService;
        context.Services.AddSingleton(workspaceService);
        context.Services.AddSingleton(
            DispatchProxy.Create<IProviderRuntimeAdministrationService, RecordingWorkspaceServiceProxy>());
        context.Services.AddAgentEditorReadFixture();
        return context;
    }

    private static IRenderedComponent<AgentDetailsDialog> RenderRuntimeTab(
        BunitContext context,
        AgentEditorModel editor,
        IReadOnlyList<ProviderProfile> providers)
    {
        return context.RenderEditor(editor, AgentEditorSection.Runtime, providers: providers);
    }

    private static AgentEditorModel CreateEditor(ProviderProfile provider)
    {
        return new AgentEditorModel
        {
            Name = "Thinking effort test agent",
            ProviderProfileId = provider.Id
        };
    }

    private static ProviderProfile CreateProvider(
        string name,
        string defaultModel,
        IReadOnlyList<string>? suggestedModels = null,
        AgentReasoningEffortLevel? providerDefault = null)
    {
        return new ProviderProfile(
            Guid.NewGuid(),
            name,
            ProviderKind.OpenAi,
            "https://api.openai.com/v1",
            "OPENAI_API_KEY",
            defaultModel,
            ProviderTransportKind.Responses,
            IsEnabled: true,
            SupportsStreaming: true,
            SupportsTools: true,
            PreferFrameworkManagedChatHistory: false,
            SupportsBackgroundResponses: true,
            ConfigurationJson: AgentThinkingEffortPolicy.WriteProviderDefault("{}", providerDefault),
            Notes: string.Empty,
            HealthStatus: "Not checked",
            LastCheckedAtUtc: null,
            SuggestedModels: suggestedModels ?? [defaultModel]);
    }

    private static void ChangeSelectToLabel(
        IRenderedComponent<AgentDetailsDialog> component,
        string testId,
        string label)
    {
        var select = FindSelect(component, testId);
        var option = select.QuerySelectorAll("option")
            .Single(item => string.Equals(item.TextContent.Trim(), label, StringComparison.Ordinal));
        var value = option.GetAttribute("value")
            ?? throw new InvalidOperationException($"Option '{label}' does not define a value.");
        select.Change(value);
    }

    private static AngleSharp.Dom.IElement FindSelect(
        IRenderedComponent<AgentDetailsDialog> component,
        string testId)
    {
        var element = component.Find($"[data-testid='{testId}']");
        if (string.Equals(element.TagName, "SELECT", StringComparison.OrdinalIgnoreCase))
        {
            return element;
        }

        return element.QuerySelector("select")
            ?? throw new InvalidOperationException($"Control '{testId}' does not contain a select element.");
    }

    private static AngleSharp.Dom.IElement FindSaveButton(
        IRenderedComponent<AgentDetailsDialog> component)
    {
        var element = component.Find($"[data-testid='{SaveTestId}']");
        if (string.Equals(element.TagName, "BUTTON", StringComparison.OrdinalIgnoreCase))
        {
            return element;
        }

        return element.QuerySelector("button")
            ?? throw new InvalidOperationException($"Control '{SaveTestId}' does not contain a button element.");
    }

    public class RecordingWorkspaceServiceProxy : DispatchProxy
    {
        public List<AgentReasoningEffortLevel?> SavedThinkingEfforts { get; } = [];

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            return targetMethod?.Name switch
            {
                nameof(IAgentFrameworkWorkspaceService.SaveAgentAsync) =>
                    SaveAgent((AgentEditorModel)args![0]!),
                nameof(IAgentFrameworkWorkspaceService.ListAgentsAsync) =>
                    Task.FromResult<IReadOnlyList<AgentDefinition>>([]),
                nameof(IAgentFrameworkWorkspaceService.ListCapabilitiesAsync) =>
                    Task.FromResult<IReadOnlyList<CapabilityCatalogItem>>([]),
                "add_ExecutionUpdated" or "remove_ExecutionUpdated" => null,
                _ => throw new InvalidOperationException(
                    $"Workspace service member '{targetMethod?.Name}' was not expected in this component test.")
            };
        }

        private Task<Guid> SaveAgent(AgentEditorModel model)
        {
            SavedThinkingEfforts.Add(model.ThinkingEffortOverride);
            return Task.FromResult(model.Id ?? Guid.NewGuid());
        }
    }
}
