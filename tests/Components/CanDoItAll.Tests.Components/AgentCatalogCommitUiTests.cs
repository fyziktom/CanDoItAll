using System.Reflection;
using Bunit;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class AgentCatalogCommitUiTests {
    [Fact]
    public async Task Team_known_commit_keeps_later_input_and_accepts_its_exact_original_without_reading_a_later_peer() {
        using var context = Context(out var owner);
        var cut = context.Render<AgentTeamDetailsDialog>(p => p.Add(c => c.TeamId, owner.TeamId));
        cut.WaitForElement("[data-testid='agents-team-name']").Input("Submitted team");
        var editContext = cut.FindComponent<EditForm>().Instance.EditContext;
        var saving = cut.Find("form").SubmitAsync();
        await owner.Started.Task;
        cut.Find("[data-testid='agents-team-name']").Input("Later team input");
        owner.Release.SetResult();
        await saving;
        Assert.Same(editContext, cut.FindComponent<EditForm>().Instance.EditContext);
        Assert.Equal("Later team input", cut.Find("[data-testid='agents-team-name']").GetAttribute("value"));
        Assert.Contains("secondary update", cut.Find("[data-testid='agents-team-status']").TextContent, StringComparison.Ordinal);
        Assert.False(cut.Find("[data-testid='agents-team-save']").HasAttribute("disabled"));
        Assert.Equal(1, owner.Reads);
        Assert.Equal(1, owner.Writes);
        Assert.Contains(context.Services.GetRequiredService<NotificationService>().Messages, message => message.Summary == "Team saved with warning");
        Assert.DoesNotContain("PRIVATE_COMMIT_SENTINEL", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Capability_known_commit_keeps_later_input_and_warns_without_entering_unknown_or_rereading() {
        using var context = Context(out var owner);
        var cut = context.Render<CapabilityDetailsDialog>(p => p.Add(c => c.CapabilityId, owner.CapabilityId));
        cut.WaitForElement("[data-testid='agents-capability-details-name']").Input("Submitted capability");
        var editContext = cut.FindComponent<EditForm>().Instance.EditContext;
        var saving = cut.Find("form").SubmitAsync();
        await owner.Started.Task;
        cut.Find("[data-testid='agents-capability-details-name']").Input("Later capability input");
        owner.Release.SetResult();
        await saving;
        Assert.Same(editContext, cut.FindComponent<EditForm>().Instance.EditContext);
        Assert.Equal("Later capability input", cut.Find("[data-testid='agents-capability-details-name']").GetAttribute("value"));
        Assert.Contains("secondary update", cut.Find("[data-testid='capability-authoring-status']").TextContent, StringComparison.Ordinal);
        Assert.DoesNotContain("save outcome is unknown", cut.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, owner.Reads);
        Assert.Equal(1, owner.Writes);
        Assert.Contains(context.Services.GetRequiredService<NotificationService>().Messages, message => message.Summary == "Capability saved with warning");
        Assert.DoesNotContain("PRIVATE_COMMIT_SENTINEL", cut.Markup, StringComparison.Ordinal);
    }

    private static BunitContext Context(out Owner owner) {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
        context.Services.AddAgentConfigurationFixture();
        context.Services.AddSingleton<IDatabaseSwitchNotificationService, DatabaseSwitchNotificationService>();
        var workspace = DispatchProxy.Create<IAgentFrameworkWorkspaceService, Owner>();
        owner = (Owner)(object)workspace;
        context.Services.AddSingleton(workspace);
        context.Services.AddSingleton(DispatchProxy.Create<IAgentCapabilitySetupFlowService, Owner>());
        context.SetRendererInfo(new Microsoft.AspNetCore.Components.RendererInfo("Server", true));
        return context;
    }

    public class Owner : DispatchProxy {
        public Guid TeamId { get; } = Guid.NewGuid();
        public Guid CapabilityId { get; } = Guid.NewGuid();
        public int Reads { get; private set; }
        public int Writes { get; private set; }
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override object? Invoke(MethodInfo? method, object?[]? args) {
            switch (method?.Name) {
                case nameof(IAgentFrameworkWorkspaceService.GetAgentTeamEditorAsync):
                    Reads++;
                    return Task.FromResult(AgentTeamEditorModel.FromDefinition(new(TeamId, "Original team", "Description", [],
                        DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch)));
                case nameof(IAgentFrameworkWorkspaceService.GetCapabilityEditorAsync):
                    Reads++;
                    return Task.FromResult(new CapabilityEditorModel {
                        Id = CapabilityId, Name = "Original capability", Key = "owned-known-capability", Kind = CapabilityKind.McpServer,
                        ExpectedFingerprint = "original-known-fingerprint",
                        ConfigurationJson = """{"transport":"logical","serverName":"fixture-server","allowedTools":["fixture"]}"""
                    });
                case nameof(IAgentFrameworkWorkspaceService.SaveAgentTeamMetadataAsync):
                    Writes++;
                    return SaveTeamAsync((AgentTeamEditorModel)args![0]!);
                case nameof(IAgentFrameworkWorkspaceService.SaveCapabilityEditorAsync):
                    Writes++;
                    return SaveCapabilityAsync((CapabilityEditorModel)args![0]!);
                default:
                    throw new InvalidOperationException("Unexpected native commit UI fixture call.");
            }
        }
        private async Task<AgentTeamEditorModel> SaveTeamAsync(AgentTeamEditorModel submitted) {
            var accepted = AgentTeamEditorModel.FromDefinition(new(TeamId, submitted.Name, submitted.Description, [],
                DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddSeconds(1), submitted.Icon));
            Started.SetResult();
            await Release.Task;
            throw new AgentCatalogCommittedException<AgentTeamEditorModel>(accepted, new IOException("PRIVATE_COMMIT_SENTINEL"));
        }
        private async Task<CapabilityEditorModel> SaveCapabilityAsync(CapabilityEditorModel submitted) {
            var accepted = new CapabilityEditorModel {
                Id = CapabilityId, Name = submitted.Name, Key = submitted.Key, Kind = submitted.Kind, ConfigurationJson = submitted.ConfigurationJson,
                ExpectedFingerprint = "accepted-known-fingerprint"
            };
            Started.SetResult();
            await Release.Task;
            throw new AgentCatalogCommittedException<CapabilityEditorModel>(accepted, new IOException("PRIVATE_COMMIT_SENTINEL"));
        }
    }
}
