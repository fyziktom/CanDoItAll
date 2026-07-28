using System.Reflection;
using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.Prompts;
using CanDoItAll.Modules.Prompts.Pages;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components;

public sealed class PromptGalleryPageTests
{
    [Fact]
    public void Curator_context_is_active_before_chat_opens_and_is_released_with_the_page()
    {
        var launcher = new RecordingCuratorLauncher();
        using (var context = new BunitContext())
        {
            context.Services.AddCanDoItAllBaseLib();
            context.Services.AddSingleton(
                DispatchProxy.Create<IPromptGalleryService, EmptyPromptGalleryServiceProxy>());
            context.Services.AddSingleton<IPromptGalleryCuratorLauncher>(launcher);

            var cut = context.Render<PromptGalleryPage>();
            var button = cut.WaitForElement("[data-testid='prompt-gallery-prompts-curator-open']");

            Assert.Equal($"Open {launcher.Presentation.Name}", button.GetAttribute("aria-label"));
            Assert.DoesNotContain(launcher.Presentation.Name, button.TextContent, StringComparison.Ordinal);
            var avatarImageUrl = Assert.IsType<string>(launcher.Presentation.AvatarImageUrl);
            Assert.Contains(avatarImageUrl, button.InnerHtml, StringComparison.Ordinal);
            Assert.Equal(1, launcher.ActivateCount);
            Assert.Equal(1, launcher.ContextLease.SynchronizeCount);
            Assert.Equal(1, launcher.PresentationCount);
            Assert.Equal(["activate", "synchronize", "presentation"], launcher.Events);

            var tooltipTarget = Assert.IsAssignableFrom<AngleSharp.Dom.IElement>(button.ParentElement);
            tooltipTarget.TriggerEvent("onmouseenter", new MouseEventArgs { ClientX = 120, ClientY = 80 });

            var tooltip = context.Services.GetRequiredService<TooltipService>().Current;
            Assert.Equal($"Open {launcher.Presentation.Name}", tooltip?.Text);
            Assert.Equal(TooltipPosition.Bottom, tooltip?.Options.Position);
            Assert.Equal("prompt-gallery-prompts-curator-tooltip", tooltip?.Options.TestId);

            button.Click();

            cut.WaitForAssertion(() => Assert.Equal(1, launcher.OpenCount));
            Assert.Equal(["activate", "synchronize", "presentation", "open"], launcher.Events);
            Assert.Contains(
                context.Services.GetRequiredService<NotificationService>().Messages,
                notification => notification.Summary == "Chat ready");

            cut.Render();

            Assert.Equal(1, launcher.ActivateCount);
            Assert.Equal(2, launcher.ContextLease.SynchronizeCount);
            Assert.Equal(1, launcher.PresentationCount);

            cut.Instance.Dispose();

            Assert.Equal(1, launcher.ContextLease.DisposeCount);
        }
    }

    [Fact]
    public void Curator_action_remains_visible_and_retries_a_transient_presentation_failure()
    {
        var launcher = new RecordingCuratorLauncher
        {
            RemainingPresentationFailures = 1
        };
        using var context = new BunitContext();
        context.Services.AddCanDoItAllBaseLib();
        context.Services.AddSingleton(
            DispatchProxy.Create<IPromptGalleryService, EmptyPromptGalleryServiceProxy>());
        context.Services.AddSingleton<IPromptGalleryCuratorLauncher>(launcher);

        var cut = context.Render<PromptGalleryPage>();

        cut.WaitForAssertion(() => Assert.Equal(1, launcher.PresentationCount));
        var button = cut.Find("[data-testid='prompt-gallery-prompts-curator-open']");
        Assert.False(button.HasAttribute("disabled"));
        Assert.Equal("Open Prompts Curator Agent", button.GetAttribute("aria-label"));
        Assert.Equal(0, launcher.OpenCount);

        button.Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(2, launcher.PresentationCount);
            Assert.Equal(1, launcher.OpenCount);
            Assert.Equal(
                $"Open {launcher.Presentation.Name}",
                cut.Find("[data-testid='prompt-gallery-prompts-curator-open']").GetAttribute("aria-label"));
        });
    }

    private sealed class RecordingCuratorLauncher : IPromptGalleryCuratorLauncher
    {
        public bool IsAvailable => true;

        public int ActivateCount { get; private set; }

        public int OpenCount { get; private set; }

        public int PresentationCount { get; private set; }

        public int RemainingPresentationFailures { get; set; }

        public PromptGalleryCuratorPresentation Presentation { get; } = new(
            "Canonical Prompts Curator",
            "/images/agents/canonical-prompts-curator.png");

        public RecordingCuratorContextLease ContextLease { get; } = new();

        public List<string> Events { get; } = [];

        public IPromptGalleryCuratorContextLease ActivateContext()
        {
            ActivateCount++;
            Events.Add("activate");
            ContextLease.Synchronized = () => Events.Add("synchronize");
            return ContextLease;
        }

        public Task<PromptGalleryCuratorPresentation> GetPresentationAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PresentationCount++;
            Events.Add("presentation");
            if (RemainingPresentationFailures > 0)
            {
                RemainingPresentationFailures--;
                throw new InvalidOperationException("Transient curator presentation failure.");
            }

            return Task.FromResult(Presentation);
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
