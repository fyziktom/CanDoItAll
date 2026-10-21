using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Modules.Workbench;

public sealed class ProcessPlanningManagerAuthority {
    private readonly IProjectDefinitionCurrentWriteAccess currentWrite;

    public ProcessPlanningManagerAuthority(ProcessLaunchAuthority authority, string? issuer,
        IProjectDefinitionCurrentWriteAccess currentWrite) {
        ArgumentNullException.ThrowIfNull(authority);
        ArgumentNullException.ThrowIfNull(currentWrite);
        authority.Validate();
        if (authority.ProjectAdmission is not null || !authority.CanCreateTasks ||
                authority.Principal is not (ProcessLaunchPrincipal.LocalOperator or ProcessLaunchPrincipal.AuthenticatedOperator)) {
            throw new ArgumentException("Manager review requires an operator authority without a new Project admission.", nameof(authority));
        }
        var authenticated = authority.Principal as ProcessLaunchPrincipal.AuthenticatedOperator;
        if (authenticated is not null
                ? string.IsNullOrWhiteSpace(issuer) || issuer.Length > 512 || authenticated.SubjectId.Length > 512
                : issuer is not null) {
            throw new ArgumentException("An authenticated manager requires its verified issuer; local authority has no issuer.", nameof(issuer));
        }
        Original = authority;
        this.currentWrite = currentWrite;
        Actor = new(ProcessEventActorKind.Manager, new(AgentToolProtocolEnvelope.ComputeDigest(JsonSerializer.Serialize(new {
            Domain = "process-planning-manager/v1", authority.DatabaseProfileId,
            Surface = (authority.Principal as ProcessLaunchPrincipal.LocalOperator)?.Surface,
            Issuer = issuer, Subject = authenticated?.SubjectId
        })).Value));
    }

    public ProcessLaunchAuthority Original { get; }
    internal ProcessEventActor Actor { get; }

    internal async Task RequireCurrentAsync(CancellationToken cancellationToken) {
        using var activity = AgentFrameworkTelemetry.ActivitySource.StartActivity("process.planning.manager.observe");
        cancellationToken.ThrowIfCancellationRequested();
        var decision = await currentWrite.ObserveAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        switch (decision) {
            case ProjectDefinitionWriteAccessDecision.Allowed:
                return;
            case ProjectDefinitionWriteAccessDecision.Unavailable:
                throw new AgentToolReceiptReadUnavailableException(new IOException("Current manager authority could not be observed."));
            case ProjectDefinitionWriteAccessDecision.Denied:
            case ProjectDefinitionWriteAccessDecision.CallerChanged:
            case ProjectDefinitionWriteAccessDecision.AuthorityChanged:
                throw new AgentToolReceiptAccessDeniedException();
            default:
                throw new InvalidOperationException("The manager authority observer returned an unsupported decision.");
        }
    }

    internal async Task RequireCurrentAsync(AgentToolProfileBinding original, ICanonicalRuntimeDatabase database,
        IAgentExecutionProfileGenerationSource generations, IProcessLaunchAuthorityLease authority, CancellationToken cancellationToken) {
        RequireProfile();
        await RequireCurrentAsync(cancellationToken);
        try {
            using var activity = AgentFrameworkTelemetry.ActivitySource.StartActivity("process.planning.source.observe");
            await authority.RequireForMutationAsync(linkTarget: null, cancellationToken);
        } catch (ProcessLaunchAuthorityRejectedException) {
            throw new AgentToolReceiptAccessDeniedException();
        }
        RequireProfile();
        cancellationToken.ThrowIfCancellationRequested();

        void RequireProfile() {
            if (Original.DatabaseProfileId != original.ProfileId || database.Profile.Profile.Id != original.ProfileId ||
                    database.Profile.Profile.Runtime.Fingerprint != original.Fingerprint || generations.GetGeneration() != original.Generation) {
                throw new AgentToolReceiptAccessDeniedException();
            }
        }
    }
}
