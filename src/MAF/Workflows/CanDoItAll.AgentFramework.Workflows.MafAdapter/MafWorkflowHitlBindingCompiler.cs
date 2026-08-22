using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using Microsoft.Agents.AI.Workflows;

namespace CanDoItAll.AgentFramework.Maf;

internal enum MafWorkflowBindingRole
{
    Execute,
    HumanRequestPreparation,
    HumanRequestPort,
    HumanResponseMapping,
    ApprovalRequestPreparation,
    ApprovalRequestPort,
    ApprovalContinuation
}

internal sealed record MafWorkflowBindingComponent(
    MafWorkflowBindingRole Role,
    ExecutorBinding Binding);

internal sealed record MafWorkflowBindingEdge(
    ExecutorBinding Source,
    ExecutorBinding Target);

internal sealed record MafCompiledNodeBinding(
    WorkflowNodeId NodeId,
    ExecutorBinding Entry,
    ExecutorBinding Exit,
    IReadOnlyList<MafWorkflowBindingComponent> Components,
    IReadOnlyList<MafWorkflowBindingEdge> InternalEdges)
{
    public bool HasNativeExternalRequest => Components.Any(component =>
        component.Role is MafWorkflowBindingRole.HumanRequestPort or MafWorkflowBindingRole.ApprovalRequestPort);
}

internal sealed record MafWorkflowHumanInputRequest(
    WorkflowId WorkflowId,
    WorkflowVersionId WorkflowVersionId,
    WorkflowNodeId NodeId,
    WorkflowExternalRequestKind Kind,
    string Prompt,
    WorkflowValueShape? ResponseShape,
    WorkflowNodeInput Context);

internal sealed record MafWorkflowHumanInputResponse(string PayloadJson);

internal sealed record MafWorkflowApprovalRequest(
    WorkflowExecutorApprovalRequestId RequestId,
    WorkflowExecutorApprovalToken ApprovalToken,
    WorkflowRunId RunId,
    WorkflowId WorkflowId,
    WorkflowVersionId WorkflowVersionId,
    WorkflowNodeId NodeId,
    WorkflowExecutorId ExecutorId,
    WorkflowExecutorCapabilityFlags RequiredCapabilities,
    WorkflowExecutorApprovalRequirement ApprovalRequirement,
    WorkflowExecutorInputHash InputHash,
    WorkflowNodeInput OriginalInput,
    string Prompt,
    string RedactedSettingsSummary);

internal sealed record MafWorkflowApprovalContinuation(
    WorkflowExecutorApprovalRequestId RequestId,
    WorkflowExecutorApprovalToken ExpectedToken,
    WorkflowExecutorApprovalToken PresentedToken,
    WorkflowRunId RunId,
    WorkflowId WorkflowId,
    WorkflowVersionId WorkflowVersionId,
    WorkflowNodeId NodeId,
    WorkflowExecutorId ExecutorId,
    WorkflowExecutorCapabilityFlags RequiredCapabilities,
    WorkflowExecutorApprovalRequirement ApprovalRequirement,
    WorkflowExecutorInputHash InputHash,
    WorkflowNodeInput OriginalInput,
    WorkflowExternalResponseAuthorization ExternalResponseAuthorization,
    bool Approved,
    string Message)
{
    public static MafWorkflowApprovalContinuation Create(
        MafWorkflowApprovalRequest request,
        WorkflowExternalResponseAuthorization authorization,
        bool approved,
        string message)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(authorization);
        return new MafWorkflowApprovalContinuation(
            request.RequestId,
            request.ApprovalToken,
            request.ApprovalToken,
            request.RunId,
            request.WorkflowId,
            request.WorkflowVersionId,
            request.NodeId,
            request.ExecutorId,
            request.RequiredCapabilities,
            request.ApprovalRequirement,
            request.InputHash,
            request.OriginalInput,
            authorization,
            approved,
            message ?? string.Empty);
    }

    public WorkflowExecutorInvocationContext CreateInvocationContext(
        WorkflowExecutorInvocationContext baseContext)
    {
        ArgumentNullException.ThrowIfNull(baseContext);
        if (baseContext.ExternalResponseAuthorization != ExternalResponseAuthorization)
        {
            throw new InvalidOperationException(
                "Workflow approval continuation authorization does not match the reconstructed external-response authorization.");
        }

        return baseContext with
        {
            ApprovalAuthorization = new WorkflowExecutorApprovalAuthorization(
                RequestId,
                ExpectedToken,
                PresentedToken,
                RunId,
                WorkflowId,
                WorkflowVersionId,
                NodeId,
                ExecutorId,
                RequiredCapabilities,
                ApprovalRequirement,
                InputHash,
                ExternalResponseAuthorization,
                Approved,
                Message)
        };
    }
}

internal sealed record MafWorkflowApprovalDeniedOutcome(
    bool Approved,
    WorkflowExecutorApprovalRequestId RequestId,
    WorkflowNodeId NodeId,
    WorkflowExecutorId ExecutorId,
    string Message);

internal static class MafWorkflowBindingIds
{
    private const string HumanPreparationRole = "hitl-human-prepare";
    private const string HumanRequestRole = "hitl-human-request";
    private const string HumanResponseRole = "hitl-human-response";
    private const string ApprovalPreparationRole = "hitl-approval-prepare";
    private const string ApprovalRequestRole = "hitl-approval-request";
    private const string ApprovalContinuationRole = "hitl-approval-continue";

    public static string HumanPreparation(WorkflowVersionId workflowVersionId, WorkflowNodeId nodeId)
        => Create(workflowVersionId, nodeId, HumanPreparationRole);

    public static string HumanRequest(WorkflowVersionId workflowVersionId, WorkflowNodeId nodeId)
        => Create(workflowVersionId, nodeId, HumanRequestRole);

    public static string HumanResponse(WorkflowVersionId workflowVersionId, WorkflowNodeId nodeId)
        => Create(workflowVersionId, nodeId, HumanResponseRole);

    public static string ApprovalPreparation(WorkflowVersionId workflowVersionId, WorkflowNodeId nodeId)
        => Create(workflowVersionId, nodeId, ApprovalPreparationRole);

    public static string ApprovalRequest(WorkflowVersionId workflowVersionId, WorkflowNodeId nodeId)
        => Create(workflowVersionId, nodeId, ApprovalRequestRole);

    public static string ApprovalContinuation(WorkflowVersionId workflowVersionId, WorkflowNodeId nodeId)
        => Create(workflowVersionId, nodeId, ApprovalContinuationRole);

    private static string Create(
        WorkflowVersionId workflowVersionId,
        WorkflowNodeId nodeId,
        string role)
        => $"{workflowVersionId.Value:N}::{nodeId.Value}::{role}";
}

internal sealed class MafWorkflowHitlBindingCompiler(
    IWorkflowExecutorInvoker? executorInvoker = null,
    IWorkflowLlmComponentInvoker? llmComponentInvoker = null,
    IWorkflowExecutorCatalog? executorCatalog = null,
    TimeProvider? timeProvider = null)
{
    private readonly MafWorkflowNodeExecutionBindingFactory nodeExecution = new(
        executorInvoker,
        llmComponentInvoker,
        timeProvider);
    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;

    public IReadOnlyDictionary<WorkflowNodeId, MafCompiledNodeBinding> Compile(
        WorkflowDefinition definition,
        IReadOnlyList<LlmCallComponent> components,
        WorkflowPreviewSimulationPlan? previewSimulationPlan = null,
        WorkflowExecutorInvocationContext? invocationContext = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(components);

        var componentsById = components.ToDictionary(component => component.Id);
        var simulationSteps = (previewSimulationPlan ?? WorkflowPreviewSimulationPlan.Empty).Steps
            .ToDictionary(step => step.NodeId);
        var resolvedInvocationContext = invocationContext ?? WorkflowExecutorInvocationContext.Empty;
        var bindings = definition.Graph.Nodes.ToDictionary(
            node => node.Id,
            node => CompileNode(
                definition,
                node,
                componentsById,
                simulationSteps,
                resolvedInvocationContext));

        ThrowIfBindingIdsCollide(bindings.Values);
        return bindings;
    }

    private MafCompiledNodeBinding CompileNode(
        WorkflowDefinition definition,
        WorkflowNode node,
        IReadOnlyDictionary<WorkflowComponentId, LlmCallComponent> componentsById,
        IReadOnlyDictionary<WorkflowNodeId, WorkflowPreviewSimulationStep> simulationSteps,
        WorkflowExecutorInvocationContext invocationContext)
    {
        if (node.Kind == WorkflowNodeKind.HumanInput)
        {
            return CreateHumanInputBinding(definition, node);
        }

        if (TryGetApprovalDescriptor(node, out var approvalDescriptor, out var invokesExecutor))
        {
            return CreateApprovalBinding(
                definition,
                node,
                approvalDescriptor,
                invokesExecutor,
                invocationContext);
        }

        return nodeExecution.Create(
            definition,
            node,
            componentsById,
            simulationSteps,
            invocationContext);
    }

    private static MafCompiledNodeBinding CreateHumanInputBinding(
        WorkflowDefinition definition,
        WorkflowNode node)
    {
        MafWorkflowHumanInputRequest Prepare(WorkflowNodeInput input)
            => new(
                definition.Id,
                definition.VersionId,
                node.Id,
                node.Settings.ExternalRequestKind ?? WorkflowExternalRequestKind.HumanInput,
                string.IsNullOrWhiteSpace(node.Settings.Instructions)
                    ? $"Provide input for workflow node '{node.Id}'."
                    : node.Settings.Instructions.Trim(),
                node.Settings.ResultShape,
                input);

        static WorkflowNodeInput MapResponse(MafWorkflowHumanInputResponse response)
        {
            ArgumentNullException.ThrowIfNull(response);
            return new WorkflowNodeInput(response.PayloadJson);
        }

        var preparation = ((Func<WorkflowNodeInput, MafWorkflowHumanInputRequest>)Prepare)
            .BindAsExecutor(MafWorkflowBindingIds.HumanPreparation(definition.VersionId, node.Id), threadsafe: true);
        var requestPort = RequestPort
            .Create<MafWorkflowHumanInputRequest, MafWorkflowHumanInputResponse>(
                MafWorkflowBindingIds.HumanRequest(definition.VersionId, node.Id))
            .BindAsExecutor(allowWrappedRequests: false);
        var responseMapping = ((Func<MafWorkflowHumanInputResponse, WorkflowNodeInput>)MapResponse)
            .BindAsExecutor(MafWorkflowBindingIds.HumanResponse(definition.VersionId, node.Id), threadsafe: true);

        return new MafCompiledNodeBinding(
            node.Id,
            preparation,
            responseMapping,
            [
                new MafWorkflowBindingComponent(MafWorkflowBindingRole.HumanRequestPreparation, preparation),
                new MafWorkflowBindingComponent(MafWorkflowBindingRole.HumanRequestPort, requestPort),
                new MafWorkflowBindingComponent(MafWorkflowBindingRole.HumanResponseMapping, responseMapping)
            ],
            [
                new MafWorkflowBindingEdge(preparation, requestPort),
                new MafWorkflowBindingEdge(requestPort, responseMapping)
            ]);
    }

    private MafCompiledNodeBinding CreateApprovalBinding(
        WorkflowDefinition definition,
        WorkflowNode node,
        WorkflowExecutorDescriptor descriptor,
        bool invokesExecutor,
        WorkflowExecutorInvocationContext invocationContext)
    {
        var approvalRequirement = invokesExecutor
            ? descriptor.PermissionPolicy.ApprovalRequirement
            : WorkflowExecutorApprovalRequirement.AlwaysRequired;

        MafWorkflowApprovalRequest Prepare(WorkflowNodeInput input)
        {
            var runId = WorkflowExecutorExecutionAuditScope.CurrentRunId
                ?? throw new InvalidOperationException($"Approval-required workflow node '{node.Id}' requires an active workflow run id.");
            var settingsJson = string.IsNullOrWhiteSpace(node.Settings.ExecutorSettingsJson)
                ? descriptor.DefaultSettingsJson
                : node.Settings.ExecutorSettingsJson;

            return new MafWorkflowApprovalRequest(
                WorkflowExecutorApprovalRequestId.New(),
                WorkflowExecutorApprovalToken.New(),
                runId,
                definition.Id,
                definition.VersionId,
                node.Id,
                descriptor.Id,
                descriptor.PermissionPolicy.RequiredCapabilities,
                approvalRequirement,
                WorkflowExecutorInputHash.Compute(input),
                input,
                ResolveApprovalPrompt(node, descriptor, invokesExecutor),
                WorkflowExecutorRedaction.RedactSettingsJson(settingsJson));
        }

        async ValueTask<WorkflowNodeInput> ContinueAsync(
            MafWorkflowApprovalContinuation continuation,
            IWorkflowContext context,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(continuation);
            ValidateApprovalContinuation(
                definition,
                node,
                descriptor,
                approvalRequirement,
                invocationContext,
                continuation,
                clock.GetUtcNow());
            if (!continuation.Approved)
            {
                return new WorkflowNodeInput(WorkflowExecutorJson.Serialize(
                    new MafWorkflowApprovalDeniedOutcome(
                        Approved: false,
                        continuation.RequestId,
                        continuation.NodeId,
                        continuation.ExecutorId,
                        continuation.Message)));
            }

            if (!invokesExecutor)
            {
                return continuation.OriginalInput;
            }

            return await nodeExecution.ExecuteAsync(
                definition,
                node,
                continuation.OriginalInput,
                new Dictionary<WorkflowComponentId, LlmCallComponent>(),
                new Dictionary<WorkflowNodeId, WorkflowPreviewSimulationStep>(),
                continuation.CreateInvocationContext(invocationContext),
                cancellationToken);
        }

        var preparation = ((Func<WorkflowNodeInput, MafWorkflowApprovalRequest>)Prepare)
            .BindAsExecutor(MafWorkflowBindingIds.ApprovalPreparation(definition.VersionId, node.Id), threadsafe: true);
        var requestPort = RequestPort
            .Create<MafWorkflowApprovalRequest, MafWorkflowApprovalContinuation>(
                MafWorkflowBindingIds.ApprovalRequest(definition.VersionId, node.Id))
            .BindAsExecutor(allowWrappedRequests: false);
        var continuation = ((Func<MafWorkflowApprovalContinuation, IWorkflowContext, CancellationToken, ValueTask<WorkflowNodeInput>>)ContinueAsync)
            .BindAsExecutor(MafWorkflowBindingIds.ApprovalContinuation(definition.VersionId, node.Id), threadsafe: true);

        return new MafCompiledNodeBinding(
            node.Id,
            preparation,
            continuation,
            [
                new MafWorkflowBindingComponent(MafWorkflowBindingRole.ApprovalRequestPreparation, preparation),
                new MafWorkflowBindingComponent(MafWorkflowBindingRole.ApprovalRequestPort, requestPort),
                new MafWorkflowBindingComponent(MafWorkflowBindingRole.ApprovalContinuation, continuation)
            ],
            [
                new MafWorkflowBindingEdge(preparation, requestPort),
                new MafWorkflowBindingEdge(requestPort, continuation)
            ]);
    }

    private bool TryGetApprovalDescriptor(
        WorkflowNode node,
        out WorkflowExecutorDescriptor descriptor,
        out bool invokesExecutor)
    {
        descriptor = null!;
        invokesExecutor = true;
        if (node.Settings.ExecutorId is not { } executorId)
        {
            return false;
        }

        if (executorId == WorkflowExecutorIds.ApprovalRequest)
        {
            invokesExecutor = false;
            descriptor = executorCatalog is not null && executorCatalog.TryGetExecutor(executorId, out var catalogDescriptor)
                ? catalogDescriptor
                : BuiltInWorkflowExecutorDescriptors.ApprovalRequest;
            return true;
        }

        return executorCatalog is not null &&
            executorCatalog.TryGetExecutor(executorId, out descriptor) &&
            descriptor.PermissionPolicy.RequiresApproval;
    }

    private static void ValidateApprovalContinuation(
        WorkflowDefinition definition,
        WorkflowNode node,
        WorkflowExecutorDescriptor descriptor,
        WorkflowExecutorApprovalRequirement approvalRequirement,
        WorkflowExecutorInvocationContext invocationContext,
        MafWorkflowApprovalContinuation continuation,
        DateTimeOffset nowUtc)
    {
        var activeRunId = WorkflowExecutorExecutionAuditScope.CurrentRunId;
        var authorization = continuation.ExternalResponseAuthorization;
        var mismatch = activeRunId switch
        {
            null => "active run identity",
            _ when continuation.RunId != activeRunId => "run identity",
            _ when continuation.WorkflowId != definition.Id => "workflow identity",
            _ when continuation.WorkflowVersionId != definition.VersionId => "workflow version identity",
            _ when continuation.NodeId != node.Id => "node identity",
            _ when continuation.ExecutorId != descriptor.Id => "executor identity",
            _ when continuation.RequiredCapabilities != descriptor.PermissionPolicy.RequiredCapabilities => "required capabilities",
            _ when continuation.ApprovalRequirement != approvalRequirement => "approval requirement",
            _ when continuation.InputHash != WorkflowExecutorInputHash.Compute(continuation.OriginalInput) => "original input hash",
            _ when !continuation.ExpectedToken.FixedTimeEquals(continuation.PresentedToken) => "approval token",
            _ when invocationContext.ExternalResponseAuthorization != authorization => "reconstructed authorization",
            _ when invocationContext.CausationOperationId != authorization.OperationId => "response operation identity",
            _ when invocationContext.CausationRequestId != authorization.RequestId => "external request identity",
            _ when invocationContext.CausationRequestVersion != authorization.RequestVersion => "external request version",
            _ when invocationContext.InvocationGeneration.Value != authorization.RequestVersion.Value => "invocation generation",
            _ when authorization.RunId != continuation.RunId => "authorization run identity",
            _ when authorization.WorkflowId != continuation.WorkflowId => "authorization workflow identity",
            _ when authorization.WorkflowVersionId != continuation.WorkflowVersionId => "authorization workflow version identity",
            _ when authorization.RequestKind is not (WorkflowExternalRequestKind.Approval or WorkflowExternalRequestKind.ToolApproval) => "authorization request kind",
            _ when authorization.Action != ResolveExpectedAction(continuation.Approved) => "authorization action",
            _ when authorization.Actor is null => "authorization actor",
            _ when authorization.Actor.Kind == WorkflowLaunchActorKind.Agent => "authorization actor",
            _ when IsAutonomousSelfApproval(authorization) => "authorization origin actor",
            _ when authorization.AuthorizationScope is null => "authorization scope",
            _ when !string.Equals(
                authorization.AuthorizationPolicyFingerprint,
                WorkflowExternalResponseAuthorizationPolicy.CurrentFingerprint,
                StringComparison.Ordinal) => "authorization policy fingerprint",
            _ when authorization.AuthorizedAtUtc == default || authorization.AuthorizedAtUtc > nowUtc => "authorization time",
            _ when authorization.ExpiresAtUtc <= authorization.AuthorizedAtUtc ||
                authorization.ExpiresAtUtc != authorization.AuthorizedAtUtc.AddSeconds(
                    WorkflowExternalResponseAuthorizationPolicy.ResponseLifetimeSeconds) ||
                authorization.IsExpired(nowUtc) => "authorization expiry",
            _ => null
        };
        if (mismatch is not null)
        {
            throw new InvalidOperationException(
                $"Workflow approval continuation does not match the checkpointed request for node '{node.Id}': {mismatch} differs.");
        }
    }

    private static WorkflowExternalResponseAction ResolveExpectedAction(bool approved)
        => approved
            ? WorkflowExternalResponseAction.Approve
            : WorkflowExternalResponseAction.Deny;

    private static bool IsAutonomousSelfApproval(WorkflowExternalResponseAuthorization authorization)
        => authorization.OriginActor is { Kind: WorkflowLaunchActorKind.Agent or WorkflowLaunchActorKind.Service } origin &&
            origin == authorization.Actor;

    private static string ResolveApprovalPrompt(
        WorkflowNode node,
        WorkflowExecutorDescriptor descriptor,
        bool invokesExecutor)
    {
        if (!invokesExecutor)
        {
            var settingsJson = string.IsNullOrWhiteSpace(node.Settings.ExecutorSettingsJson)
                ? descriptor.DefaultSettingsJson
                : node.Settings.ExecutorSettingsJson;
            var settings = WorkflowExecutorJson.Deserialize<WorkflowApprovalExecutorSettings>(settingsJson);
            if (!string.IsNullOrWhiteSpace(settings.Prompt))
            {
                return settings.Prompt.Trim();
            }
        }

        return $"Approve workflow executor '{descriptor.Name}' for node '{node.Id}'.";
    }

    private static void ThrowIfBindingIdsCollide(IEnumerable<MafCompiledNodeBinding> bindings)
    {
        var duplicateIds = bindings
            .SelectMany(binding => binding.Components)
            .GroupBy(component => component.Binding.Id, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        if (duplicateIds.Length > 0)
        {
            throw new InvalidOperationException(
                $"MAF workflow compilation produced duplicate executor binding id(s): {string.Join(", ", duplicateIds)}.");
        }
    }

}
