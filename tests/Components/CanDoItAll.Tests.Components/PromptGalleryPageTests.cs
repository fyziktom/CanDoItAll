using System.Reflection;
using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.Prompts;
using CanDoItAll.Modules.Prompts.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components;

public sealed class PromptGalleryPageTests
{
    [Fact]
    public void Curator_context_is_active_before_chat_opens_and_is_released_with_the_page()
    {
        var launcher = new RecordingCuratorLauncher();
        using (var context = new TestContext())
        {
            context.Services.AddCanDoItAllBaseLib();
            context.Services.AddSingleton(
                DispatchProxy.Create<IPromptGalleryService, EmptyPromptGalleryServiceProxy>());
            context.Services.AddSingleton<IPromptGalleryCuratorLauncher>(launcher);

            var cut = context.RenderComponent<PromptGalleryPage>();
            var button = cut.Find("[data-testid='prompt-gallery-prompts-curator-open']");

            Assert.Contains("Prompts Curator", button.TextContent, StringComparison.Ordinal);
            Assert.Equal(1, launcher.ActivateCount);
            Assert.Equal(1, launcher.ContextLease.SynchronizeCount);
            Assert.Equal(["activate", "synchronize"], launcher.Events);

            button.Click();

            cut.WaitForAssertion(() => Assert.Equal(1, launcher.OpenCount));
            Assert.Equal(["activate", "synchronize", "open"], launcher.Events);
            Assert.Contains(
                context.Services.GetRequiredService<NotificationService>().Messages,
                notification => notification.Summary == "Chat ready");

            cut.Render();

            Assert.Equal(1, launcher.ActivateCount);
            Assert.Equal(2, launcher.ContextLease.SynchronizeCount);

            cut.Instance.Dispose();

            Assert.Equal(1, launcher.ContextLease.DisposeCount);
        }
    }

    private sealed class RecordingCuratorLauncher : IPromptGalleryCuratorLauncher
    {
        public bool IsAvailable => true;

        public int ActivateCount { get; private set; }

        public int OpenCount { get; private set; }

        public RecordingCuratorContextLease ContextLease { get; } = new();

        public List<string> Events { get; } = [];

        public IPromptGalleryCuratorContextLease ActivateContext()
        {
            ActivateCount++;
            Events.Add("activate");
            ContextLease.Synchronized = () => Events.Add("synchronize");
            return ContextLease;
        }

        public Task OpenAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            OpenCount++;
            Events.Add("open");
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingCuratorContextLease : IPromptGalleryCuratorContextLease
    {
        public int SynchronizeCount { get; private set; }

        public int DisposeCount { get; private set; }

        public Action? Synchronized { get; set; }

        public void SynchronizeNavigation()
        {
            SynchronizeCount++;
            Synchronized?.Invoke();
        }

        public void Dispose()
        {
            DisposeCount++;
        }
    }

    private class EmptyPromptGalleryServiceProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == nameof(IPromptGalleryService.SearchAsync))
            {
                return Task.FromResult(
                    new PromptGalleryPage<PromptGallerySearchItem>([], 0, 25, 0));
            }

            throw new InvalidOperationException(
                $"Gallery service member '{targetMethod?.Name}' was not expected in this component test.");
        }
    }
}
