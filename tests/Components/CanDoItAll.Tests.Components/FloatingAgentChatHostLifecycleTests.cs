using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Conversations.Components.Presentation;
using CanDoItAll.Conversations.Shell;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class FloatingAgentChatHostLifecycleTests
{
    [Fact]
    public async Task Compatibility_host_keeps_pending_contributor_initialization_non_blocking_and_cancels_on_disposal()
    {
        var contributor = new PendingContributor();
        using var context = CreateContext(contributor);
        var coordinator = context.Services.GetRequiredService<IConversationShellCoordinator>();

        var cut = context.Render<FloatingAgentChatHost>();

        await contributor.InitializationStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.NotNull(cut.Find("[data-testid='floating-agent-chat-host']"));
        Assert.Equal(1, contributor.InitializeCallCount);
        Assert.False(contributor.InitializationSettled.Task.IsCompleted);

        coordinator.ShowCatalog(ConversationCatalogKindFilter.Agents);

        cut.WaitForElement("[data-testid='floating-agent-catalog-window']");
        Assert.Equal(1, contributor.InitializeCallCount);
        Assert.False(contributor.InitializationSettled.Task.IsCompleted);

        var shellHost = cut.FindComponent<ConversationShellHost>();
        await shellHost.Instance.DisposeAsync();
        cut.Dispose();

        await contributor.InitializationCancelled.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await contributor.InitializationSettled.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(0, contributor.ChangedSubscriberCount);
    }

    private static BunitContext CreateContext(PendingContributor contributor)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddLogging();
        context.Services.AddCanDoItAllBaseLib();
        context.Services.AddConversationShell();
        context.Services.AddSingleton<IConversationShellContributor>(contributor);
        return context;
    }

    private sealed class PendingContributor : IConversationShellContributor
    {
        private EventHandler? changed;
        private int initializeCallCount;

        public TaskCompletionSource InitializationStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource InitializationCancelled { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource InitializationSettled { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int InitializeCallCount => Volatile.Read(ref initializeCallCount);

        public int ChangedSubscriberCount { get; private set; }

        public string SourceId => "agents";

        public ConversationParticipantKind Kind => ConversationParticipantKind.Agent;

        public event EventHandler? Changed
        {
            add
            {
                changed += value;
                ChangedSubscriberCount++;
            }
            remove
            {
                changed -= value;
                ChangedSubscriberCount--;
            }
        }

        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref initializeCallCount);
            InitializationStarted.TrySetResult();
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                InitializationCancelled.TrySetResult();
                throw;
            }
            finally
            {
                InitializationSettled.TrySetResult();
            }
        }

        public ConversationShellContributorSnapshot Snapshot()
            => ConversationShellContributorSnapshot.Empty;

        public Task HandleParticipantActionAsync(
            ParticipantActionRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task HandleActiveActionAsync(
            ConversationActionRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task HandleWindowCloseAsync(
            string windowId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
