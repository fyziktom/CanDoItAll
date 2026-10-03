using System.Reflection;
using Bunit;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Mcp.Abstractions;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using Microsoft.Extensions.DependencyInjection;
using AccessKind = CanDoItAll.AgentFramework.Capabilities.Abstractions.CapabilityKind;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class CapabilityAuthoringRegressionTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Setup_success_after_configuration_changes_and_returns_does_not_verify_current_draft(bool wizard) {
        using var context = CreateContext(out var owner, out var setup);
        Func<string> markup;
        Func<string, AngleSharp.Dom.IElement> find;
        Task operation;
        if (wizard) {
            var cut = context.Render<CapabilitySetupWizardDialog>();
            markup = () => cut.Markup;
            find = cut.Find;
            cut.Find("[data-testid='agents-capability-setup-name']").Change("Fixture");
            await cut.Find("[data-testid='agents-capability-setup-next']").ClickAsync();
            cut.Find("[data-testid='agents-capability-setup-mcp-transport']").Change("logical");
            cut.Find("[data-testid='agents-capability-setup-mcp-server']").Change("fixture-server");
            operation = cut.Find("[data-testid='agents-capability-setup-test']").ClickAsync();
        } else {
            var cut = context.Render<CapabilityDetailsDialog>(p => p.Add(x => x.CapabilityId, owner.Model.Id!.Value));
            markup = () => cut.Markup;
            find = cut.Find;
            await cut.InvokeAsync(() => cut.FindComponent<Tabs>().Instance.SelectedIndexChanged.InvokeAsync(1));
            operation = cut.Find("[data-testid='agents-capability-details-test-setup']").ClickAsync();
        }
        var server = find(wizard ? "[data-testid='agents-capability-setup-mcp-server']" : "[data-testid='agents-capability-details-mcp-server']");
        server.Change("different-server");
        server.Change("fixture-server");
        setup.Pending.SetResult(new(true, new(AccessKind.McpServer, new("fixture")), new("fixture-server"), "fixture", [], [], [], true));
        await operation;
        Assert.DoesNotContain("Setup passed", markup(), StringComparison.Ordinal);
        Assert.Equal(1, setup.Calls);
        Assert.Equal(0, owner.SaveCalls);
    }

    [Fact]
    public async Task Invalid_raw_json_is_retained_after_submit() {
        using var context = CreateContext(out var owner, out _);
        owner.Model.Kind = CapabilityKind.Plugin;
        owner.Model.ConfigurationJson = "{}";
        var cut = context.Render<CapabilityDetailsDialog>(p => p.Add(x => x.CapabilityId, owner.Model.Id!.Value));
        await cut.InvokeAsync(() => cut.FindComponent<Tabs>().Instance.SelectedIndexChanged.InvokeAsync(2));
        cut.Find("[data-testid='agents-capability-details-raw-json']").Change("{invalid");
        await cut.Find("form").SubmitAsync();
        Assert.Equal("{invalid", cut.Find("[data-testid='agents-capability-details-raw-json']").TextContent);
        Assert.Equal(0, owner.SaveCalls);
    }

    [Fact]
    public async Task Malformed_stored_configuration_is_exposed_for_explicit_repair() {
        using var context = CreateContext(out var owner, out _);
        owner.Model.ConfigurationJson = "{malformed";
        var cut = context.Render<CapabilityDetailsDialog>(p => p.Add(x => x.CapabilityId, owner.Model.Id!.Value));
        Assert.Contains("configuration", cut.Find("[data-testid='capability-configuration-invalid']").TextContent, StringComparison.OrdinalIgnoreCase);
        await cut.InvokeAsync(() => cut.FindComponent<Tabs>().Instance.SelectedIndexChanged.InvokeAsync(2));
        var raw = cut.Find("[data-testid='agents-capability-details-raw-json']");
        Assert.False(raw.HasAttribute("disabled"));
        Assert.Equal("{malformed", raw.TextContent);
        await cut.Find("form").SubmitAsync();
        Assert.Equal(0, owner.SaveCalls);
    }

    [Fact]
    public async Task Unknown_save_acknowledgement_does_not_offer_a_blind_retry() {
        using var context = CreateContext(out var owner, out _);
        var cut = context.Render<CapabilityDetailsDialog>(p => p.Add(x => x.CapabilityId, owner.Model.Id!.Value));
        await cut.Find("form").SubmitAsync();
        await cut.Find("form").SubmitAsync();
        Assert.Equal(1, owner.SaveCalls);
        Assert.Contains("unknown", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    private static BunitContext CreateContext(out Owner owner, out Setup setup) {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
        context.Services.AddSingleton<IDatabaseSwitchNotificationService, DatabaseSwitchNotificationService>();
        var service = DispatchProxy.Create<IAgentFrameworkWorkspaceService, Owner>();
        owner = (Owner)(object)service;
        context.Services.AddSingleton(service);
        var setupService = DispatchProxy.Create<IAgentCapabilitySetupFlowService, Setup>();
        setup = (Setup)(object)setupService;
        context.Services.AddSingleton(setupService);
        return context;
    }

    public class Owner : DispatchProxy {
        public CapabilityEditorModel Model { get; } = new() {
            Id = Guid.NewGuid(), Name = "Fixture", Key = "fixture", Kind = CapabilityKind.McpServer,
            ConfigurationJson = """{"transport":"logical","serverName":"fixture-server","allowedTools":["fixture"]}"""
        };
        public int SaveCalls { get; private set; }
        protected override object? Invoke(MethodInfo? method, object?[]? args) {
            if (method?.Name == nameof(IAgentFrameworkWorkspaceService.GetCapabilityEditorAsync)) {
                return Task.FromResult(Model);
            }
            if (method?.Name == nameof(IAgentFrameworkWorkspaceService.SaveCapabilityEditorAsync)) {
                SaveCalls++;
                return Task.FromException<CapabilityEditorModel>(new IOException("Controlled ambiguous acknowledgement."));
            }
            throw new InvalidOperationException("Unexpected capability owner call.");
        }
    }

    public class Setup : DispatchProxy {
        public int Calls { get; private set; }
        public TaskCompletionSource<McpSetupTestResult> Pending { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override object? Invoke(MethodInfo? method, object?[]? args) {
            if (method?.Name == nameof(IAgentCapabilitySetupFlowService.TestMcpSetupAsync)) {
                Calls++;
                return Pending.Task;
            }
            throw new InvalidOperationException("Unexpected setup call.");
        }
    }
}
