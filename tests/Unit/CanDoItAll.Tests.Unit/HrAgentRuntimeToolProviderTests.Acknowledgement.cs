using System.Reflection;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.AI;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed partial class HrAgentRuntimeToolProviderTests {
    public enum AcknowledgementBehavior { Success, Failure, LostAcknowledgement, MissingIdentity }

    [Theory]
    [InlineData(false, AcknowledgementBehavior.Success)]
    [InlineData(true, AcknowledgementBehavior.Success)]
    [InlineData(false, AcknowledgementBehavior.Failure)]
    [InlineData(true, AcknowledgementBehavior.Failure)]
    [InlineData(false, AcknowledgementBehavior.LostAcknowledgement)]
    [InlineData(true, AcknowledgementBehavior.LostAcknowledgement)]
    [InlineData(false, AcknowledgementBehavior.MissingIdentity)]
    [InlineData(true, AcknowledgementBehavior.MissingIdentity)]
    public async Task CRM_acknowledgement_is_captured_only_after_a_successful_typed_owner_result(bool affiliation, AcknowledgementBehavior behavior) {
        var context = CreateContext(HrAgentCapabilityKeys.ToolNameToCapabilityKey.Values, allowCrmScope: true);
        var workspace = DispatchProxy.Create<IAgentFrameworkWorkspaceService, DisclosureWorkspace>();
        var state = (DisclosureWorkspace)(object)workspace;
        state.Agents = [context.Agent];
        state.Capabilities = context.Capabilities;
        var owner = new AcknowledgementCrmOwner(behavior);
        var provider = CreateDisclosureProvider(workspace, new ThrowingCrmHrAgentQueryService(), owner);
        var name = affiliation ? HrAgentToolPolicy.HrCrmAffiliationUpsert : HrAgentToolPolicy.HrCrmPartyCreate;
        var tool = Assert.IsAssignableFrom<AIFunction>((await provider.CreateToolsAsync(context, default)).Single(item => item.Name == name));
        object request = affiliation
            ? new CrmPartyAffiliationUpsertCommand(null, Guid.NewGuid(), Guid.NewGuid(), PartyOrganizationAffiliationKind.Employee, true)
            : new CrmPartyCreateCommand(PartyType.Person, "Acknowledged person");
        using var capture = AgentToolInvocationEffectScope.Begin();
        owner.BeforeAcknowledgement = () => Assert.Null(capture.CommittedEffect);
        if (behavior == AcknowledgementBehavior.Success) {
            Assert.NotNull(await tool.InvokeAsync(new AIFunctionArguments { ["request"] = request }));
            Assert.Equal(new AgentToolCommittedEffect(affiliation ? "crm-affiliation" : "crm-party", owner.Id.ToString("D")), capture.CommittedEffect);
        } else {
            var failure = await Assert.ThrowsAnyAsync<Exception>(() => tool.InvokeAsync(new AIFunctionArguments { ["request"] = request }).AsTask());
            Assert.Contains(behavior switch {
                AcknowledgementBehavior.Failure => "owner denied",
                AcknowledgementBehavior.LostAcknowledgement => "owner acknowledgement lost",
                _ => "no persisted target identity"
            }, failure.ToString(), StringComparison.Ordinal);
            Assert.Null(capture.CommittedEffect);
        }
        Assert.Equal(1, owner.Calls);
        Assert.True(provider.GetToolMetadata(context).Single(item => item.ToolName == name).RequiresApprovalByDefault);
    }

    private sealed class AcknowledgementCrmOwner(AcknowledgementBehavior behavior) : ICrmPartyCommandService {
        public Guid Id { get; } = behavior == AcknowledgementBehavior.MissingIdentity ? Guid.Empty : Guid.NewGuid();
        public int Calls { get; private set; }
        public Action BeforeAcknowledgement { get; set; } = () => { };

        public Task<Result<CrmPartyCreateResult>> CreatePartyAsync(CrmPartyCreateCommand command, string actor,
            CancellationToken cancellationToken = default) => Complete(actor,
                new CrmPartyCreateResult(Id, PartyType.Person, PartyLifecycleStatus.Draft, command.DisplayName, "", []));

        public Task<Result<CrmPartyAffiliationResult>> UpsertAffiliationAsync(CrmPartyAffiliationUpsertCommand command,
            string actor, CancellationToken cancellationToken = default) => Complete(actor,
                new CrmPartyAffiliationResult(Id, command.PersonPartyId, command.OrganizationPartyId, "Organization",
                    command.AffiliationKind, command.IsPrimary, command.JobTitle, null, null, null, null, true, DateTimeOffset.UtcNow));

        public Task<Result<IReadOnlyList<CrmPartyAffiliationResult>>> ListAffiliationsAsync(Guid personPartyId,
            CancellationToken cancellationToken = default) => throw new InvalidOperationException("The mutation must call its exact owner method.");

        private Task<Result<T>> Complete<T>(string actor, T result) {
            Calls++;
            Assert.Equal($"hr-agent:{HrAgentIdentity.AgentId:D}", actor);
            BeforeAcknowledgement();
            return behavior switch {
                AcknowledgementBehavior.Failure => Task.FromResult(Result<T>.Failure(new Error("owner-denied", "owner denied"))),
                AcknowledgementBehavior.LostAcknowledgement => throw new IOException("owner acknowledgement lost"),
                _ => Task.FromResult(Result<T>.Success(result))
            };
        }
    }
}
