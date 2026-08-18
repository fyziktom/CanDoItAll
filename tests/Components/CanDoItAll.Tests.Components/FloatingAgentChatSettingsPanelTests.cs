using System.Reflection;
using Bunit;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class FloatingAgentChatSettingsPanelTests
{
    [Fact]
    public void Saves_neutral_lifecycle_fields_and_agent_preparation_fields_together()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
        var settingsService = new RecordingSettingsService(new(10, 12, 3, true, 15));
        var coordinator = DispatchProxy.Create<IFloatingAgentChatCoordinator, RecordingCoordinatorProxy>();
        context.Services.AddSingleton<IFloatingAgentChatSettingsService>(settingsService);
        context.Services.AddSingleton(coordinator);

        var cut = context.Render<FloatingAgentChatSettingsPanel>();
        cut.WaitForElement("[data-testid='floating-agent-chat-retention']");

        cut.Find("[data-testid='floating-agent-chat-retention']").Change("25");
        cut.Find("[data-testid='floating-agent-chat-maximum-active']").Change("9");
        cut.Find("[data-testid='floating-agent-chat-maximum-prepared']").Change("7");
        cut.Find("[data-testid='floating-agent-chat-prepared-retention']").Change("30");
        cut.Find("[data-testid='floating-agent-chat-settings-save']").Click();

        cut.WaitForAssertion(() => Assert.NotNull(settingsService.SavedSettings));
        var expected = new FloatingAgentChatSettings(25, 9, 7, true, 30);
        Assert.Equal(expected, settingsService.SavedSettings);
        Assert.Equal(expected, ((RecordingCoordinatorProxy)(object)coordinator).AppliedSettings);
        Assert.Contains("Saved", cut.Markup);
    }

    private sealed class RecordingSettingsService(FloatingAgentChatSettings settings)
        : IFloatingAgentChatSettingsService
    {
        public FloatingAgentChatSettings? SavedSettings { get; private set; }

        public Task<FloatingAgentChatSettings> GetSettingsAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult(settings);

        public Task<FloatingAgentChatSettings> SaveSettingsAsync(
            FloatingAgentChatSettings nextSettings,
            CancellationToken cancellationToken = default)
        {
            SavedSettings = nextSettings;
            return Task.FromResult(nextSettings);
        }
    }

    private class RecordingCoordinatorProxy : DispatchProxy
    {
        public FloatingAgentChatSettings? AppliedSettings { get; private set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == nameof(IFloatingAgentChatCoordinator.ApplySettings))
            {
                AppliedSettings = Assert.IsType<FloatingAgentChatSettings>(args![0]);
                return null;
            }

            if (targetMethod?.Name is "add_Changed" or "remove_Changed")
            {
                return null;
            }

            throw new InvalidOperationException($"Unexpected coordinator call: {targetMethod?.Name}");
        }
    }
}
