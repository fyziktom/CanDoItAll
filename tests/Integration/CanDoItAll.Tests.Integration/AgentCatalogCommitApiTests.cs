using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Web.Api;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.AgentFramework;

public sealed class AgentCatalogCommitApiTests {
    [Fact]
    public async Task Http_keeps_normal_payloads_and_declares_known_catalog_commits_without_repeating_commands() {
        var workspace = DispatchProxy.Create<IAgentFrameworkWorkspaceService, Owner>();
        var owner = (Owner)(object)workspace;
        await using var host = await ApiTestHost.CreateAsync(false, services => services.AddSingleton(workspace), useInMemoryDatabase: true);
        using var agent = await host.Client.PostAsJsonAsync("/api/agents/", new AgentEditorModel { Name = "Accepted agent" });
        await AssertGuidAsync(agent, owner.Agent.Id);
        owner.DirectoryWarning = true;
        using var projection = await host.Client.PostAsJsonAsync("/api/agents/", new AgentEditorModel { Name = "Accepted projected agent" });
        await AssertGuidAsync(projection, owner.Agent.Id);
        using var team = await host.Client.PostAsJsonAsync("/api/agents/teams", new AgentTeamEditorModel { Name = "Accepted team" });
        await AssertGuidAsync(team, owner.Team.Id);
        using var capability = await host.Client.PostAsJsonAsync("/api/agents/capabilities", new CapabilityEditorModel { Name = "Accepted capability" });
        await AssertGuidAsync(capability, owner.Capability.Id!.Value);
        using var members = await host.Client.PutAsJsonAsync($"/api/agents/teams/{owner.Team.Id:D}/members",
            new AgentTeamMembersApiRequest([owner.Agent.Id], owner.Team.UpdatedAtUtc));
        AssertKnownCommit(members);
        var acceptedMembers = await members.Content.ReadFromJsonAsync<AgentTeamDefinition>();
        Assert.Equal(owner.Team.Id, acceptedMembers!.Id);
        Assert.Equal([owner.Agent.Id], acceptedMembers.AgentIds);
        using var deletedTeam = await host.Client.DeleteAsync($"/api/agents/teams/{owner.Team.Id:D}?expectedUpdatedAtUtc={Uri.EscapeDataString(owner.Team.UpdatedAtUtc.ToString("O"))}");
        AssertKnownCommit(deletedTeam);
        Assert.True((await deletedTeam.Content.ReadFromJsonAsync<ApiAck>())!.Ok);
        using var deletedCapability = await host.Client.DeleteAsync($"/api/agents/capabilities/{owner.Capability.Id:D}?expectedFingerprint=accepted-api-fingerprint");
        AssertKnownCommit(deletedCapability);
        Assert.True((await deletedCapability.Content.ReadFromJsonAsync<ApiAck>())!.Ok);
        using var clone = await host.Client.PostAsJsonAsync($"/api/agents/{owner.Agent.Id:D}/clone", new AgentCloneApiRequest("Owned warning clone"));
        await AssertGuidAsync(clone, owner.CopyId);
        using var template = await host.Client.PostAsJsonAsync($"/api/agents/{owner.Agent.Id:D}/convert-to-template", new AgentTemplateConversionApiRequest("owned-warning-template"));
        await AssertGuidAsync(template, owner.CopyId);
        Assert.Equal(9, owner.Writes);
    }

    private static async Task AssertGuidAsync(HttpResponseMessage response, Guid expected) {
        AssertKnownCommit(response);
        Assert.Equal(expected, await response.Content.ReadFromJsonAsync<Guid>());
        Assert.DoesNotContain("PRIVATE_COMMIT_SENTINEL", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    private static void AssertKnownCommit(HttpResponseMessage response) {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(AgentCatalogApiResults.ReconciliationPending, Assert.Single(response.Headers.GetValues(AgentCatalogApiResults.OutcomeHeader)));
    }

    public class Owner : DispatchProxy {
        public AgentDefinition Agent { get; } = new(Guid.NewGuid(), "Accepted agent", "Role", "Summary", "Instruction",
            AgentLifecycleStatus.Active, null, "model", AgentWorkloadKind.General, AgentChatHistoryMode.FrameworkManaged,
            0.2, true, false, "{}", false, "", AgentPermissionsPolicy.Default, [], [], DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch);
        public AgentTeamDefinition Team => new(teamId, "Accepted team", "Description", [Agent.Id], DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch);
        private readonly Guid teamId = Guid.NewGuid();
        public CapabilityEditorModel Capability { get; } = new() { Id = Guid.NewGuid(), Name = "Accepted capability", ExpectedFingerprint = "accepted-api-fingerprint" };
        public bool DirectoryWarning { get; set; }
        public Guid CopyId { get; } = Guid.NewGuid();
        public int Writes { get; private set; }
        protected override object? Invoke(MethodInfo? method, object?[]? args) {
            var cause = new IOException("PRIVATE_COMMIT_SENTINEL");
            switch (method?.Name) {
                case nameof(IAgentFrameworkWorkspaceService.SaveAgentAsync):
                    Writes++;
                    return Task.FromException<Guid>(DirectoryWarning
                        ? new AgentDirectoryProjectionSynchronizationException(Agent.Id, cause, acceptedDefinition: Agent)
                        : new AgentCatalogCommittedException<AgentDefinition>(Agent, cause));
                case nameof(IAgentFrameworkWorkspaceService.SaveAgentTeamAsync):
                    Writes++;
                    return Task.FromException<Guid>(new AgentCatalogCommittedException<AgentTeamDefinition>(Team, cause));
                case nameof(IAgentFrameworkWorkspaceService.SaveCapabilityAsync):
                    Writes++;
                    return Task.FromException<Guid>(new AgentCatalogCommittedException<CapabilityEditorModel>(Capability, cause));
                case nameof(IAgentFrameworkWorkspaceService.UpdateAgentTeamMembersAsync):
                    Writes++;
                    return Task.FromException<AgentTeamDefinition>(new AgentCatalogCommittedException<AgentTeamDefinition>(Team, cause));
                case nameof(IAgentFrameworkWorkspaceService.DeleteAgentTeamAsync):
                    Writes++;
                    return Task.FromException(new AgentCatalogCommittedException<Guid>(Team.Id, cause));
                case nameof(IAgentFrameworkWorkspaceService.DeleteCapabilityAsync):
                    Writes++;
                    return Task.FromException(new AgentCatalogCommittedException<Guid>(Capability.Id!.Value, cause));
                case nameof(IAgentFrameworkWorkspaceService.CloneAgentAsync):
                    Writes++;
                    return Task.FromException<Guid>(new AgentCatalogCommittedException<Guid>(CopyId, cause));
                case nameof(IAgentFrameworkWorkspaceService.ConvertToTemplateAsync):
                    Writes++;
                    return Task.FromException<Guid>(new AgentDirectoryProjectionSynchronizationException(CopyId, cause));
                case nameof(IAgentFrameworkWorkspaceService.ListAgentsAsync):
                    return Task.FromResult<IReadOnlyList<AgentDefinition>>([Agent]);
                case nameof(IAgentFrameworkWorkspaceService.ListAgentTeamsAsync):
                    return Task.FromResult<IReadOnlyList<AgentTeamDefinition>>([Team]);
                default:
                    throw new InvalidOperationException($"Unexpected catalog API fixture call: {method?.Name}.");
            }
        }
    }
}
