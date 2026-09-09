using System.Reflection;
using Bunit;
using CanDoItAll.AgentFramework.Components;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using CanDoItAll.Modules.AgentFramework.ProviderManagement;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class AgentChildAdversarialTests {
    [Fact]
    public async Task Team_load_failure_is_public_and_retry_preserves_the_requested_identity() {
        using var context = CreateContext(out var workspace);
        workspace.Fail = true;
        var id = Guid.NewGuid();
        var child = context.Render<AgentTeamDetailsDialog>(parameters => parameters.Add(component => component.TeamId, id));
        Assert.Empty(child.FindAll("[data-testid='agents-team-save']"));
        Assert.DoesNotContain("CHILD_PRIVATE_SENTINEL", child.Markup, StringComparison.Ordinal);
        Assert.Contains(context.Services.GetRequiredService<NotificationService>().Messages, message => message.Summary == "Team details unavailable");
        workspace.Fail = false;
        await child.FindAll("button").Single(button => button.TextContent.Trim() == "Retry").ClickAsync();
        Assert.NotNull(child.Find("[data-testid='agents-team-save']"));
        Assert.Equal(id, child.Instance.TeamId);
    }
    private const string Poison = "CHILD_PRIVATE_SENTINEL api_key=test-only-child-private-key /srv/private/child C:\\private\\child at Internal.Read()";

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Capability_setup_errors_are_public_and_disposed_tokens_live_until_completion(bool wizard, bool dispose) {
        using var context = CreateContext(out _);
        var setup = (ChildWorkspace)context.Services.GetRequiredService<IAgentCapabilitySetupFlowService>();
        Task operation;
        Action stop;
        Func<string> markup;
        if (wizard) {
            var child = context.Render<CapabilitySetupWizardDialog>();
            child.Find("[data-testid='agents-capability-setup-name']").Change("Fixture capability");
            await child.Find("[data-testid='agents-capability-setup-next']").ClickAsync();
            child.Find("[data-testid='agents-capability-setup-mcp-transport']").Change("logical");
            child.Find("[data-testid='agents-capability-setup-mcp-server']").Change("fixture-server");
            operation = child.Find("[data-testid='agents-capability-setup-test']").ClickAsync();
            stop = child.Instance.Dispose;
            markup = () => child.Markup;
        } else {
            var child = context.Render<CapabilityDetailsDialog>(parameters => parameters.Add(component => component.CapabilityId, Guid.NewGuid()));
            await child.InvokeAsync(() => child.FindComponent<Tabs>().Instance.SelectedIndexChanged.InvokeAsync(1));
            operation = child.Find("[data-testid='agents-capability-details-test-setup']").ClickAsync();
            stop = child.Instance.Dispose;
            markup = () => child.Markup;
        }
        Assert.Equal(1, setup.SetupCalls);
        if (dispose) {
            stop();
            Assert.True(setup.Token.IsCancellationRequested);
            var callbacks = 0;
            using var registration = setup.Token.Register(() => callbacks++);
            Assert.Equal(1, callbacks);
            Assert.True(setup.Token.WaitHandle.WaitOne(0));
        }
        setup.Setup.SetException(new IOException(Poison));
        await operation;
        Assert.DoesNotContain("CHILD_PRIVATE_SENTINEL", markup(), StringComparison.Ordinal);
        var notifications = context.Services.GetRequiredService<NotificationService>().Messages;
        Assert.DoesNotContain(notifications, message => (message.Detail ?? "").Contains("CHILD_PRIVATE_SENTINEL", StringComparison.Ordinal));
        if (dispose) {
            Assert.Empty(notifications);
            Assert.Throws<ObjectDisposedException>(() => setup.Token.WaitHandle);
        } else {
            Assert.Contains(notifications, message => message.Summary == "Setup test failed");
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Child_save_errors_do_not_expose_backend_detail(bool team) {
        using var context = CreateContext(out var workspace);
        if (team) {
            var child = context.Render<AgentTeamDetailsDialog>();
            child.Find("[data-testid='agents-team-name']").Input("Retained team");
            await child.Find("[data-testid='agents-team-save']").ClickAsync();
        } else {
            var child = context.Render<CapabilityDetailsDialog>(parameters => parameters.Add(component => component.CapabilityId, Guid.NewGuid()));
            await child.Find("form").SubmitAsync();
        }
        Assert.Equal(1, workspace.SaveCalls);
        Assert.DoesNotContain(context.Services.GetRequiredService<NotificationService>().Messages,
            message => (message.Detail ?? "").Contains("CHILD_PRIVATE_SENTINEL", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Owned_child_read_token_remains_usable_until_completion(bool team) {
        using var context = CreateContext(out var workspace);
        workspace.Delay = true;
        Action<Action> waitForAssertion;
        if (team) {
            var child = context.Render<AgentTeamDetailsDialog>();
            waitForAssertion = assertion => child.WaitForAssertion(assertion);
            await child.InvokeAsync(child.Instance.Dispose);
        } else {
            var child = context.Render<CapabilityDetailsDialog>(parameters => parameters.Add(component => component.CapabilityId, Guid.NewGuid()));
            waitForAssertion = assertion => child.WaitForAssertion(assertion);
            await child.InvokeAsync(child.Instance.Dispose);
        }
        Assert.True(workspace.Token.IsCancellationRequested);
        var callbacks = 0;
        using var delayed = workspace.Token.Register(() => callbacks++);
        Assert.Equal(1, callbacks);
        Assert.True(workspace.Token.WaitHandle.WaitOne(0));
        workspace.Complete();
        waitForAssertion(() => Assert.Throws<ObjectDisposedException>(() => workspace.Token.WaitHandle));
        Assert.DoesNotContain(context.Services.GetRequiredService<NotificationService>().Messages,
            message => (message.Detail ?? "").Contains("CHILD_PRIVATE_SENTINEL", StringComparison.Ordinal));
    }

    [Fact]
    public void Capability_load_failure_is_public_and_does_not_enable_an_empty_editor() {
        using var context = CreateContext(out var workspace);
        workspace.Fail = true;
        var child = context.Render<CapabilityDetailsDialog>(parameters => parameters.Add(component => component.CapabilityId, Guid.NewGuid()));
        child.WaitForAssertion(() => Assert.NotEmpty(context.Services.GetRequiredService<NotificationService>().Messages));
        Assert.DoesNotContain("CHILD_PRIVATE_SENTINEL", child.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain(context.Services.GetRequiredService<NotificationService>().Messages,
            message => (message.Detail ?? "").Contains("CHILD_PRIVATE_SENTINEL", StringComparison.Ordinal));
        Assert.Empty(child.FindAll("form"));
        Assert.Empty(child.FindAll("[data-testid='agents-capability-details-save']"));
        Assert.Contains(child.FindAll("button"), button => button.TextContent.Trim() == "Retry");
    }

    [Fact]
    public async Task Agent_switch_favorite_failure_does_not_expose_backend_detail() {
        using var context = CreateContext(out _);
        var agent = AgentChatPanelResponsivenessTests.CreateAgent();
        var child = context.Render<AgentSwitchDialog>(parameters => parameters
            .Add(component => component.Agents, new[] { agent })
            .Add(component => component.FavoriteToggled, _ => Task.FromException<AgentDefinition>(new IOException(Poison))));
        await child.Find("[data-testid='agent-favorite-toggle']").ClickAsync();
        Assert.Contains(context.Services.GetRequiredService<NotificationService>().Messages, message => message.Summary == "Favorite update failed");
        Assert.DoesNotContain(context.Services.GetRequiredService<NotificationService>().Messages,
            message => (message.Detail ?? "").Contains("CHILD_PRIVATE_SENTINEL", StringComparison.Ordinal));
        Assert.Contains(agent.Name, child.Markup, StringComparison.Ordinal);
    }

    private static BunitContext CreateContext(out ChildWorkspace workspace) {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
        var service = DispatchProxy.Create<IAgentFrameworkWorkspaceService, ChildWorkspace>();
        workspace = (ChildWorkspace)(object)service;
        context.Services.AddSingleton(service);
        context.Services.AddSingleton(DispatchProxy.Create<IAgentCapabilitySetupFlowService, ChildWorkspace>());
        return context;
    }

    public class ChildWorkspace : DispatchProxy {
        public bool Delay { get; set; }
        public bool Fail { get; set; }
        public CancellationToken Token { get; private set; }
        public int SaveCalls { get; private set; }
        public int SetupCalls { get; private set; }
        public TaskCompletionSource<CanDoItAll.AgentFramework.Mcp.Abstractions.McpSetupTestResult> Setup { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<CapabilityEditorModel> capability = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<AgentTeamEditorModel> team = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public void Complete() {
            capability.TrySetException(new IOException(Poison));
            team.TrySetException(new IOException(Poison));
        }
        protected override object? Invoke(MethodInfo? method, object?[]? args) {
            Token = args?.OfType<CancellationToken>().SingleOrDefault() ?? default;
            if (method?.Name == nameof(IAgentCapabilitySetupFlowService.TestMcpSetupAsync)) {
                SetupCalls++;
                return Setup.Task;
            }
            if (method?.Name is nameof(IAgentFrameworkWorkspaceService.SaveCapabilityAsync) or nameof(IAgentFrameworkWorkspaceService.SaveAgentTeamAsync)) {
                SaveCalls++;
                return Task.FromException<Guid>(new IOException(Poison));
            }
            return method?.Name switch {
                nameof(IAgentFrameworkWorkspaceService.GetCapabilityEditorAsync) => Delay ? capability.Task
                    : Fail ? Task.FromException<CapabilityEditorModel>(new IOException(Poison)) : Task.FromResult(new CapabilityEditorModel {
                        Id = (Guid?)args![0], Name = "Fixture capability", Key = "fixture-capability", Kind = CapabilityKind.McpServer,
                        ConfigurationJson = """{"transport":"logical","serverName":"fixture-server","allowedTools":["fixture"]}"""
                    }),
                nameof(IAgentFrameworkWorkspaceService.GetAgentTeamEditorAsync) => Delay ? team.Task
                    : Fail ? Task.FromException<AgentTeamEditorModel>(new IOException(Poison)) : Task.FromResult(new AgentTeamEditorModel()),
                _ => throw new InvalidOperationException("Unexpected child backend call.")
            };
        }
    }
}
