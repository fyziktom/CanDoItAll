using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Editing;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Editing;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.AgentFramework;

[Trait("Category", "HostPlatform")]
public sealed class AgentPolicyEditingObservationTests {
    [Fact]
    public async Task Actual_policy_publication_emits_a_targeted_hint_and_stale_refusal_emits_none() {
        await using var app = await TestApplication.CreateAsync();
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var workspace = services.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var id = await workspace.SaveAgentAsync(new() { Name = "Owned policy observation agent" });
        var original = await workspace.ReadAgentDecisionDataPolicyAsync(id);
        var database = services.GetRequiredService<ICanonicalRuntimeDatabase>();
        var topic = new EditingTopic(new(EditingScopeKind.Dataset, database.Profile.Profile.Id, database.Generation), EditingOwner.AgentDefinition, id);
        var feed = services.GetRequiredService<EditingChangeFeed>();
        using var subscriber = feed.Subscribe(topic);
        var command = new ReplaceAgentDecisionDataPolicyCommand(Guid.NewGuid(), original.Expectation, new(MaximumReplans: 2));
        var editor = Guid.NewGuid();
        using (services.GetRequiredService<AgentConfigurationWriteContext>().Enter(topic.Scope, new(editor, command.OperationId), _ => ValueTask.CompletedTask)) {
            Assert.IsType<AgentDecisionDataPolicySaveOutcome.Accepted>(await workspace.ReplaceAgentDecisionDataPolicyAsync(command));
        }
        Assert.Equal(1, feed.DeliveredHints);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var change = Assert.Single((await subscriber.ReadAsync(timeout.Token)).Changes);
        Assert.Equal(topic, change.Resource);
        Assert.Equal(editor, change.OriginEditorId);
        Assert.Equal(command.OperationId, change.OperationId);
        Assert.Equal(command.Policy, (await workspace.ReadAgentDecisionDataPolicyAsync(id)).Policy);
        var rejected = Assert.IsType<AgentDecisionDataPolicySaveOutcome.Rejected>(await workspace.ReplaceAgentDecisionDataPolicyAsync(
            new(Guid.NewGuid(), original.Expectation, new(MaximumReplans: 9))));
        Assert.Equal(AgentDecisionDataPolicyRejection.Conflict, rejected.Reason);
        Assert.Equal(1, feed.DeliveredHints);
        Assert.Equal(command.Policy, (await workspace.ReadAgentDecisionDataPolicyAsync(id)).Policy);
    }

    [Fact]
    public async Task Retired_original_UI_authority_cannot_publish_policy_or_a_receipt() {
        await using var app = await TestApplication.CreateAsync();
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var workspace = services.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var id = await workspace.SaveAgentAsync(new() { Name = "Owned policy authority agent" });
        var original = await workspace.ReadAgentDecisionDataPolicyAsync(id);
        var database = services.GetRequiredService<ICanonicalRuntimeDatabase>();
        var originalScope = new EditingScope(EditingScopeKind.Dataset, database.Profile.Profile.Id, database.Generation);
        var command = new ReplaceAgentDecisionDataPolicyCommand(Guid.NewGuid(), original.Expectation, new(MaximumReplans: 3));
        using (services.GetRequiredService<AgentConfigurationWriteContext>().Enter(originalScope, new(Guid.NewGuid(), command.OperationId),
            _ => ValueTask.FromException(new UnauthorizedAccessException("Retired original policy actor.")))) {
            Assert.IsType<AgentDecisionDataPolicySaveOutcome.Rejected>(await workspace.ReplaceAgentDecisionDataPolicyAsync(command));
        }
        Assert.Equal(original, await workspace.ReadAgentDecisionDataPolicyAsync(id));
        Assert.IsType<AgentDecisionDataPolicyLookup.NotObserved>(await workspace.LookupAgentDecisionDataPolicyAsync(command));
    }
}
