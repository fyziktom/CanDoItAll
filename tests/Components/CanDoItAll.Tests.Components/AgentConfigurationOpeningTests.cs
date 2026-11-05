using Bunit;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Editing;
using CanDoItAll.Infrastructure.Editing;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class AgentConfigurationOpeningTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Original_actor_or_dataset_change_refuses_a_pending_read_result_and_next_write(bool changeDataset) {
        var scope = new EditingScope(EditingScopeKind.Dataset, Guid.NewGuid(), 1);
        var access = new EditingUiTestAccess();
        var feed = new EditingChangeFeed();
        var changes = new AgentConfigurationChanges(feed, () => scope, access, new(), NullLogger<EditingObservation>.Instance);
        await using var opening = new AgentConfigurationOpening(changes, EditingOwner.AgentDefinition, Guid.NewGuid(), Guid.NewGuid(), () => true);
        await opening.StartAsync(_ => Task.CompletedTask, () => Task.CompletedTask);
        var pending = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var read = opening.ReadAsync(_ => {
            started.SetResult();
            return pending.Task;
        }, default);
        await started.Task;
        if (changeDataset) {
            scope = scope with { ActivationGeneration = scope.ActivationGeneration + 1 };
        } else {
            access.Principal = "different-current-actor";
        }
        pending.SetResult("Protected late result");
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => read);
        var writes = 0;
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => opening.WriteAsync(_ => {
            writes++;
            return Task.CompletedTask;
        }, default));
        Assert.Equal(0, writes);
        Assert.Equal(0, feed.ActiveSubscriptions);
    }

    [Fact]
    public async Task Component_disposal_releases_the_original_targeted_observation() {
        await using var context = AgentDetailsDialogAvatarGenerationTests.CreateContext(new UnavailableAgentImageGenerationService());
        var id = Guid.NewGuid();
        var cut = context.RenderEditor(new AgentEditorModel { Id = id, Name = "Owned editor", ExpectedUpdatedAtUtc = DateTimeOffset.UtcNow }, AgentEditorSection.Identity);
        var feed = context.Services.GetRequiredService<EditingChangeFeed>();
        Assert.Equal(1, feed.ActiveSubscriptions);
        await context.DisposeAsync();
        Assert.Equal(0, feed.ActiveSubscriptions);
    }
}
