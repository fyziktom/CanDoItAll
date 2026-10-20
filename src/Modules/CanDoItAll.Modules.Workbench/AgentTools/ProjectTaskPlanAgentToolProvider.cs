using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using Microsoft.Extensions.AI;

namespace CanDoItAll.Modules.Workbench;

internal sealed class ProjectTaskPlanAgentToolProvider(IAgentToolAdmissionVerifier verifier,
    ProjectTaskPlanSavedPublicationResolver publications,
    ProjectTaskPlanPublisher publisher, ProjectTaskPlanReceiptReader receipts)
    : IAgentRuntimeToolProvider, IAgentToolReceiptReconciliationProvider {
    private const string ProviderKey = "workbench.task-planning";
    public int Order => 901;
    public AgentRuntimeToolProviderDescriptor Descriptor { get; } = new(ProviderKey, "Task plan publication",
        "Publishes one original approved task plan against its captured basis.", ["project-structure"],
        [AgentRuntimeToolProviderPurpose.InteractiveChat, AgentRuntimeToolProviderPurpose.GovernedProcessAutomation]) {
            AttachmentPhase = AgentRuntimeToolAttachmentPhase.RuntimeProviders
        };
    public bool Supports(string toolName) => toolName == ProjectStructureToolPolicy.ProjectTaskPlanPublish;
    public bool SupportsSavedEffect(AgentToolPreparedPayload original) => Supports(original.ToolName);
    public ValueTask<IReadOnlyList<AITool>> CreateToolsAsync(AgentRuntimeToolProviderContext context, CancellationToken cancellationToken)
        => ValueTask.FromResult<IReadOnlyList<AITool>>([]);

    public ValueTask<AgentRuntimeSavedEffectTool> RestoreSavedEffectAsync(AgentRuntimeSavedEffectContext context,
        AgentToolPreparedPayload original, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        if (!SupportsSavedEffect(original)) {
            throw ProjectTaskPlanAgentCodec.Denied("This provider cannot restore another module's effect.");
        }
        IProjectTaskPlanSavedEffectAuthority? authorized = null;
        ProjectTaskPlanSavedPublication? command = null;
        var function = AIFunctionFactory.Create(async (ProjectTaskPlanDraft draft, CancellationToken token) => {
            var held = authorized ?? throw ProjectTaskPlanAgentCodec.Denied("The original publication has no held authorization.");
            var arguments = JsonSerializer.SerializeToElement(new ProjectTaskPlanAgentArguments(draft), ProjectTaskPlanAgentCodec.Json);
            if (ProjectTaskPlanAgentCodec.Restore(arguments, original.SourcePreparation!) != original) {
                throw ProjectTaskPlanAgentCodec.Denied("The invocation changed the original approved draft.");
            }
            var originalCommand = command ?? throw ProjectTaskPlanAgentCodec.Denied("The original publication was not resolved.");
            var outcome = await publisher.PublishAsync(originalCommand.Command, new(originalCommand.Caller, held, originalCommand.ReceiptAccess), token);
            var result = ProjectTaskPlanAgentResult.From(outcome);
            if (result.EffectState == AgentToolEffectState.Committed) {
                AgentToolInvocationEffectScope.RecordCommitted(ProjectTaskPlanAgentCodec.EffectSourceKind, result.OperationId.ToString("D"));
            } else if (result.EffectState == AgentToolEffectState.NotCommitted) {
                AgentToolInvocationEffectScope.RecordRejectedBeforeEffect();
            }
            return result;
        }, new AIFunctionFactoryOptions {
            Name = original.ToolName, MarshalResult = static (value, _, _) => ValueTask.FromResult(value)
        });
        var metadata = new AgentRuntimeToolMetadata(ProviderKey, original.ToolName, AgentRuntimeToolOperationKind.Mutation, true) {
            RestoreAdmission = ProjectTaskPlanAgentCodec.Restore,
            RecoveryPolicy = AgentRuntimeToolRecoveryPolicy.ReconcileBeforeRetry,
            AuthorizeAdmissionAsync = async (payload, token) => {
                if (payload != original || authorized is not null) {
                    throw ProjectTaskPlanAgentCodec.Denied("One restoration permits only its original publication.");
                }
                var invocation = await verifier.RequireInvocationAsync(context.Session.Reference, payload.ToolName, payload.Digest, token);
                var observation = await verifier.RequireSessionObservationAsync(context.Session.Reference, token);
                var planning = observation.Planning ?? throw ProjectTaskPlanAgentCodec.Denied("The original planning decision is missing.");
                var binding = new AgentToolApprovalBinding(invocation.BatchId, invocation.IntentId, payload.SemanticVersion, payload.Digest);
                command = publications.Resolve(planning, context.Session, payload, binding);
                var work = AgentToolRunLease.Current?.PlanningWork ?? throw ProjectTaskPlanAgentCodec.Denied("The original live work interval is missing.");
                authorized = command.CreateAuthority(work);
                return authorized;
            },
            AuthorizeResultDisclosureAsync = async (result, token) => {
                if (result.Payload != original) {
                    throw new AgentToolReceiptAccessDeniedException();
                }
                var observed = await verifier.RequireSessionObservationAsync(context.Session.Reference, token);
                if (!await publications.CanReadAsync(observed, token)) {
                    throw new AgentToolReceiptAccessDeniedException();
                }
                return null;
            }
        };
        return ValueTask.FromResult(new AgentRuntimeSavedEffectTool(function, metadata));
    }

    public async ValueTask<AgentToolReceiptObservation> ReconcileAsync(AgentToolReceiptReconciliationClaim claim,
        CancellationToken cancellationToken = default) {
        var admitted = await claim.RequireAsync(cancellationToken);
        var planning = admitted.Planning ?? throw ProjectTaskPlanAgentCodec.Denied("The original receipt has no planning decision.");
        var original = publications.Resolve(planning, admitted.Session, admitted.Payload, admitted.Binding);
        await publications.RequireReadAsync(planning.Decision, cancellationToken);
        var lookup = await receipts.LookupAsync(original.Command.Operation, new(original.Caller, original.ReceiptAccess), cancellationToken);
        await claim.RequireAsync(cancellationToken);
        if (lookup is { Status: ProjectTaskPlanLookupStatus.Found, Accepted: { } accepted }) {
            return ProjectTaskPlanAgentResult.Receipt(original.Command, accepted);
        }
        return lookup.Status switch {
            ProjectTaskPlanLookupStatus.Open or ProjectTaskPlanLookupStatus.Draining or ProjectTaskPlanLookupStatus.ClosedAbsent
                => AgentToolReceiptObservation.NotObserved,
            ProjectTaskPlanLookupStatus.DetailUnavailable when lookup.DetailFailure == ProjectTaskPlanDetailFailure.AccessDenied
                => throw new AgentToolReceiptAccessDeniedException(),
            _ => throw new InvalidDataException("The original task-plan receipt could not be observed; dispatch remains refused.")
        };
    }
}
