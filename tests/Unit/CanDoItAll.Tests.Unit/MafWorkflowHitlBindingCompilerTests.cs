using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using Microsoft.Agents.AI.Workflows;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class MafWorkflowHitlBindingCompilerTests
{
    private static readonly WorkflowId WorkflowId = new(Guid.Parse("10000000-0000-0000-0000-000000000001"));
    private static readonly WorkflowVersionId WorkflowVersionId = new(Guid.Parse("20000000-0000-0000-0000-000000000001"));

    [Fact]
    public void BindingCompilerCreatesStableNativeHumanAndApprovalTopologies()
    {
        var protectedExecutor = new RecordingExecutor(BuiltInWorkflowExecutorDescriptors.HttpFetch);
        var catalog = new WorkflowExecutorCatalog([protectedExecutor]);
        var definition = CreateDefinition(
            [
                CreateNode("start", WorkflowNodeKind.Start),
                CreateNode("human", WorkflowNodeKind.HumanInput),
                CreateExecutorNode("protected", WorkflowExecutorIds.HttpFetch),
                CreateExecutorNode("approval", WorkflowExecutorIds.ApprovalRequest),
                CreateNode("end", WorkflowNodeKind.End)
            ],
            []);
        var compiler = new MafWorkflowHitlBindingCompiler(executorCatalog: catalog);

        var bindings = compiler.Compile(definition, []);

        AssertBinding(
            bindings[new WorkflowNodeId("human")],
            MafWorkflowBindingIds.HumanPreparation(WorkflowVersionId, new WorkflowNodeId("human")),
            MafWorkflowBindingIds.HumanRequest(WorkflowVersionId, new WorkflowNodeId("human")),
            MafWorkflowBindingIds.HumanResponse(WorkflowVersionId, new WorkflowNodeId("human")));
        AssertBinding(
            bindings[new WorkflowNodeId("protected")],
            MafWorkflowBindingIds.ApprovalPreparation(WorkflowVersionId, new WorkflowNodeId("protected")),
            MafWorkflowBindingIds.ApprovalRequest(WorkflowVersionId, new WorkflowNodeId("protected")),
            MafWorkflowBindingIds.ApprovalContinuation(WorkflowVersionId, new WorkflowNodeId("protected")));
        AssertBinding(
            bindings[new WorkflowNodeId("approval")],
            MafWorkflowBindingIds.ApprovalPreparation(WorkflowVersionId, new WorkflowNodeId("approval")),
            MafWorkflowBindingIds.ApprovalRequest(WorkflowVersionId, new WorkflowNodeId("approval")),
            MafWorkflowBindingIds.ApprovalContinuation(WorkflowVersionId, new WorkflowNodeId("approval")));
        Assert.Same(bindings[new WorkflowNodeId("start")].Entry, bindings[new WorkflowNodeId("start")].Exit);
    }

    [Fact]
    public void BindingRoleIdsAreStableForExactVersionAndDistinctAcrossVersions()
    {
        var definition = CreateDefinition(
            [
                CreateNode("human", WorkflowNodeKind.HumanInput),
                CreateExecutorNode("approval", WorkflowExecutorIds.ApprovalRequest)
            ],
            [],
            "human");
        var otherVersionId = new WorkflowVersionId(Guid.Parse("20000000-0000-0000-0000-000000000002"));
        var otherVersionDefinition = definition with { VersionId = otherVersionId };
        var compiler = new MafWorkflowHitlBindingCompiler();

        var first = GetHiddenBindingIds(compiler.Compile(definition, []));
        var reconstructed = GetHiddenBindingIds(compiler.Compile(definition, []));
        var otherVersion = GetHiddenBindingIds(compiler.Compile(otherVersionDefinition, []));

        Assert.Equal(first, reconstructed);
        Assert.Equal(first.Length, first.Distinct(StringComparer.Ordinal).Count());
        Assert.All(first.Zip(otherVersion), pair => Assert.NotEqual(pair.First, pair.Second));
        Assert.All(first, id => Assert.StartsWith($"{WorkflowVersionId.Value:N}::", id, StringComparison.Ordinal));
        Assert.All(otherVersion, id => Assert.StartsWith($"{otherVersionId.Value:N}::", id, StringComparison.Ordinal));
    }

    [Fact]
    public async Task NodeExecutionBindingFactoryIsDirectlyConstructibleAndExecutesPassthroughNode()
    {
        var node = CreateNode("start", WorkflowNodeKind.Start);
        var definition = CreateDefinition([node], []);
        var input = new WorkflowNodeInput("{\"value\":42}");
        var components = new Dictionary<WorkflowComponentId, LlmCallComponent>();
        var simulationSteps = new Dictionary<WorkflowNodeId, WorkflowPreviewSimulationStep>();
        var factory = new MafWorkflowNodeExecutionBindingFactory();

        var binding = factory.Create(definition, node, components, simulationSteps);
        var output = await factory.ExecuteAsync(
            definition,
            node,
            input,
            components,
            simulationSteps,
            WorkflowExecutorInvocationContext.Empty);

        Assert.Same(binding.Entry, binding.Exit);
        Assert.Equal(node.Id.Value, binding.Entry.Id);
        Assert.Equal(input, output);
    }

    [Fact]
    public void TopologyFingerprintIsOrderIndependentAndChangesWithExactVersionOrTopology()
    {
        var nodes = new[]
        {
            CreateNode("start", WorkflowNodeKind.Start),
            CreateNode("human", WorkflowNodeKind.HumanInput),
            CreateNode("end", WorkflowNodeKind.End)
        };
        var edges = new[]
        {
            CreateEdge("start-human", "start", "human"),
            CreateEdge("human-end", "human", "end")
        };
        var first = CreateDefinition(nodes, edges);
        var reordered = CreateDefinition(nodes.Reverse().ToArray(), edges.Reverse().ToArray());
        var compiler = new MafWorkflowHitlBindingCompiler();

        var firstFingerprint = MafWorkflowTopologyFingerprintFactory.Create(first, compiler.Compile(first, []));
        var reorderedFingerprint = MafWorkflowTopologyFingerprintFactory.Create(reordered, compiler.Compile(reordered, []));
        var newVersion = first with { VersionId = new WorkflowVersionId(Guid.Parse("20000000-0000-0000-0000-000000000002")) };
        var changedTopology = first with
        {
            Graph = first.Graph with
            {
                Edges =
                [
                    edges[0] with { Routing = WorkflowEdgeRouting.Always with { Label = "changed" } },
                    edges[1]
                ]
            }
        };
        var changedRequestKind = first with
        {
            Graph = first.Graph with
            {
                Nodes = first.Graph.Nodes.Select(node => node.Id == new WorkflowNodeId("human")
                    ? node with
                    {
                        Settings = node.Settings with { ExternalRequestKind = WorkflowExternalRequestKind.ToolApproval }
                    }
                    : node).ToArray()
            }
        };
        var changedPort = first with
        {
            Graph = first.Graph with
            {
                Nodes = first.Graph.Nodes.Select(node => node.Id == new WorkflowNodeId("human")
                    ? node with
                    {
                        Ports =
                        [
                            new WorkflowPort(
                                new WorkflowPortId("review-response"),
                                "Review response",
                                WorkflowPortDirection.Output,
                                WorkflowValueShape.Text,
                                Required: true)
                        ]
                    }
                    : node).ToArray()
            }
        };
        var executorDefinition = CreateDefinition(
            [CreateExecutorNode("executor", WorkflowExecutorIds.HttpFetch)],
            [],
            "executor");
        var changedExecutor = executorDefinition with
        {
            Graph = executorDefinition.Graph with
            {
                Nodes =
                [
                    CreateExecutorNode("executor", WorkflowExecutorIds.Delay)
                ]
            }
        };
        var executorFingerprint = MafWorkflowTopologyFingerprintFactory.Create(
            executorDefinition,
            compiler.Compile(executorDefinition, []));

        Assert.Equal(firstFingerprint, reorderedFingerprint);
        Assert.NotEqual(firstFingerprint, MafWorkflowTopologyFingerprintFactory.Create(newVersion, compiler.Compile(newVersion, [])));
        Assert.NotEqual(firstFingerprint, MafWorkflowTopologyFingerprintFactory.Create(changedTopology, compiler.Compile(changedTopology, [])));
        Assert.NotEqual(firstFingerprint, MafWorkflowTopologyFingerprintFactory.Create(changedRequestKind, compiler.Compile(changedRequestKind, [])));
        Assert.NotEqual(firstFingerprint, MafWorkflowTopologyFingerprintFactory.Create(changedPort, compiler.Compile(changedPort, [])));
        Assert.NotEqual(
            executorFingerprint,
            MafWorkflowTopologyFingerprintFactory.Create(changedExecutor, compiler.Compile(changedExecutor, [])));
    }

    [Fact]
    public async Task ApprovalAuthorizationInvokesProtectedExecutorExactlyOnceWithoutReprompting()
    {
        var executor = new RecordingExecutor(BuiltInWorkflowExecutorDescriptors.HttpFetch);
        var catalog = new WorkflowExecutorCatalog([executor]);
        var approvalGate = new RecordingApprovalGate();
        var invoker = new WorkflowExecutorInvoker(catalog, [executor], approvalGate: approvalGate);
        var node = CreateExecutorNode("protected", executor.Descriptor.Id);
        var definition = CreateDefinition([node], [], "protected");
        var input = new WorkflowNodeInput("{\"immutable\":true}");
        var runId = WorkflowRunId.New();
        using var auditScope = WorkflowExecutorExecutionAuditScope.Push(runId);

        var result = await invoker.ExecuteAsync(
            definition,
            node,
            input,
            CreateInvocationContext(definition, node, executor.Descriptor, input, runId, approved: true));

        Assert.Equal(input.PayloadJson, result.PayloadJson);
        Assert.Equal(input, executor.LastInput);
        Assert.Equal(1, executor.InvocationCount);
        Assert.Equal(0, approvalGate.RequestCount);
    }

    [Fact]
    public async Task ApprovalDenialNeverInvokesProtectedExecutor()
    {
        var executor = new RecordingExecutor(BuiltInWorkflowExecutorDescriptors.HttpFetch);
        var catalog = new WorkflowExecutorCatalog([executor]);
        var approvalGate = new RecordingApprovalGate();
        var invoker = new WorkflowExecutorInvoker(catalog, [executor], approvalGate: approvalGate);
        var node = CreateExecutorNode("protected", executor.Descriptor.Id);
        var definition = CreateDefinition([node], [], "protected");
        var input = new WorkflowNodeInput("{\"immutable\":true}");
        var runId = WorkflowRunId.New();
        using var auditScope = WorkflowExecutorExecutionAuditScope.Push(runId);

        await Assert.ThrowsAsync<InvalidOperationException>(() => invoker.ExecuteAsync(
            definition,
            node,
            input,
            CreateInvocationContext(definition, node, executor.Descriptor, input, runId, approved: false)).AsTask());

        Assert.Equal(0, executor.InvocationCount);
        Assert.Equal(0, approvalGate.RequestCount);
    }

    [Theory]
    [InlineData(ApprovalBindingMismatch.MissingActiveRun)]
    [InlineData(ApprovalBindingMismatch.RunId)]
    [InlineData(ApprovalBindingMismatch.WorkflowId)]
    [InlineData(ApprovalBindingMismatch.WorkflowVersionId)]
    [InlineData(ApprovalBindingMismatch.NodeId)]
    [InlineData(ApprovalBindingMismatch.ExecutorId)]
    [InlineData(ApprovalBindingMismatch.RequiredCapabilities)]
    [InlineData(ApprovalBindingMismatch.ApprovalRequirement)]
    [InlineData(ApprovalBindingMismatch.InputHash)]
    [InlineData(ApprovalBindingMismatch.PresentedToken)]
    [InlineData(ApprovalBindingMismatch.MissingExecutorGrant)]
    [InlineData(ApprovalBindingMismatch.MissingInvocationGrant)]
    [InlineData(ApprovalBindingMismatch.OperationId)]
    [InlineData(ApprovalBindingMismatch.ExternalRequestId)]
    [InlineData(ApprovalBindingMismatch.ExternalRequestVersion)]
    [InlineData(ApprovalBindingMismatch.InvocationGeneration)]
    [InlineData(ApprovalBindingMismatch.RequestKind)]
    [InlineData(ApprovalBindingMismatch.Action)]
    [InlineData(ApprovalBindingMismatch.MissingActor)]
    [InlineData(ApprovalBindingMismatch.AutonomousActor)]
    [InlineData(ApprovalBindingMismatch.AutonomousSelfApproval)]
    [InlineData(ApprovalBindingMismatch.AuthorizationScope)]
    [InlineData(ApprovalBindingMismatch.AuthorizationPolicyFingerprint)]
    [InlineData(ApprovalBindingMismatch.AuthorizedAtUtc)]
    [InlineData(ApprovalBindingMismatch.ExpiresAtUtc)]
    [InlineData(ApprovalBindingMismatch.DecisionDenied)]
    public async Task ApprovalAuthorizationRejectsEveryEnforcedMismatchAndDenialBeforeInvocation(
        ApprovalBindingMismatch mismatch)
    {
        var executor = new RecordingExecutor(BuiltInWorkflowExecutorDescriptors.HttpFetch);
        var catalog = new WorkflowExecutorCatalog([executor]);
        var approvalGate = new RecordingApprovalGate();
        var invoker = new WorkflowExecutorInvoker(catalog, [executor], approvalGate: approvalGate);
        var node = CreateExecutorNode("protected", executor.Descriptor.Id);
        var definition = CreateDefinition([node], [], "protected");
        var input = new WorkflowNodeInput("{\"immutable\":true}");
        var runId = WorkflowRunId.New();
        var valid = CreateInvocationContext(definition, node, executor.Descriptor, input, runId, approved: true);
        var authorization = Assert.IsType<WorkflowExecutorApprovalAuthorization>(valid.ApprovalAuthorization);
        var responseAuthorization = authorization.ExternalResponseAuthorization;
        var invalidResponseAuthorization = mismatch switch
        {
            ApprovalBindingMismatch.OperationId => responseAuthorization with
            {
                OperationId = WorkflowExternalResponseOperationId.New()
            },
            ApprovalBindingMismatch.ExternalRequestId => responseAuthorization with
            {
                RequestId = WorkflowExternalRequestId.New()
            },
            ApprovalBindingMismatch.ExternalRequestVersion => responseAuthorization with
            {
                RequestVersion = new WorkflowExternalRequestVersion(responseAuthorization.RequestVersion.Value + 1)
            },
            ApprovalBindingMismatch.RequestKind => responseAuthorization with
            {
                RequestKind = WorkflowExternalRequestKind.HumanInput
            },
            ApprovalBindingMismatch.Action => responseAuthorization with
            {
                Action = WorkflowExternalResponseAction.SubmitInput
            },
            ApprovalBindingMismatch.MissingActor => responseAuthorization with
            {
                Actor = null!
            },
            ApprovalBindingMismatch.AutonomousActor => responseAuthorization with
            {
                Actor = new WorkflowLaunchActor(WorkflowLaunchActorKind.Agent, "autonomous-agent")
            },
            ApprovalBindingMismatch.AutonomousSelfApproval => responseAuthorization with
            {
                Actor = new WorkflowLaunchActor(WorkflowLaunchActorKind.Service, "autonomous-service"),
                OriginActor = new WorkflowLaunchActor(WorkflowLaunchActorKind.Service, "autonomous-service")
            },
            ApprovalBindingMismatch.AuthorizationScope => responseAuthorization with
            {
                AuthorizationScope = null!
            },
            ApprovalBindingMismatch.AuthorizationPolicyFingerprint => responseAuthorization with
            {
                AuthorizationPolicyFingerprint = string.Empty
            },
            ApprovalBindingMismatch.AuthorizedAtUtc => responseAuthorization with
            {
                AuthorizedAtUtc = TimeProvider.System.GetUtcNow().AddMinutes(1),
                ExpiresAtUtc = TimeProvider.System.GetUtcNow().AddMinutes(2)
            },
            ApprovalBindingMismatch.ExpiresAtUtc => responseAuthorization with
            {
                ExpiresAtUtc = responseAuthorization.AuthorizedAtUtc
            },
            _ => responseAuthorization
        };
        var invalidAuthorization = mismatch switch
        {
            ApprovalBindingMismatch.RunId => authorization with { RunId = WorkflowRunId.New() },
            ApprovalBindingMismatch.WorkflowId => authorization with
            {
                WorkflowId = new WorkflowId(Guid.Parse("10000000-0000-0000-0000-000000000002"))
            },
            ApprovalBindingMismatch.WorkflowVersionId => authorization with
            {
                WorkflowVersionId = new WorkflowVersionId(Guid.Parse("20000000-0000-0000-0000-000000000002"))
            },
            ApprovalBindingMismatch.NodeId => authorization with { NodeId = new WorkflowNodeId("other") },
            ApprovalBindingMismatch.ExecutorId => authorization with { ExecutorId = WorkflowExecutorIds.Delay },
            ApprovalBindingMismatch.RequiredCapabilities => authorization with
            {
                RequiredCapabilities = WorkflowExecutorCapabilityFlags.None
            },
            ApprovalBindingMismatch.ApprovalRequirement => authorization with
            {
                ApprovalRequirement = WorkflowExecutorApprovalRequirement.NotRequired
            },
            ApprovalBindingMismatch.InputHash => authorization with
            {
                InputHash = new WorkflowExecutorInputHash(new string('0', 64))
            },
            ApprovalBindingMismatch.PresentedToken => authorization with
            {
                PresentedToken = WorkflowExecutorApprovalToken.New()
            },
            ApprovalBindingMismatch.MissingExecutorGrant => authorization with
            {
                ExternalResponseAuthorization = null!
            },
            ApprovalBindingMismatch.DecisionDenied => authorization with { Approved = false },
            _ => authorization with
            {
                ExternalResponseAuthorization = invalidResponseAuthorization
            }
        };
        var invalid = valid with
        {
            ApprovalAuthorization = invalidAuthorization,
            ExternalResponseAuthorization = mismatch == ApprovalBindingMismatch.MissingInvocationGrant
                ? null
                : invalidResponseAuthorization,
            InvocationGeneration = mismatch == ApprovalBindingMismatch.InvocationGeneration
                ? new WorkflowExecutorInvocationGeneration(valid.InvocationGeneration.Value + 1)
                : valid.InvocationGeneration
        };
        var auditScope = mismatch == ApprovalBindingMismatch.MissingActiveRun
            ? null
            : WorkflowExecutorExecutionAuditScope.Push(runId);

        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => invoker.ExecuteAsync(
                definition,
                node,
                input,
                invalid).AsTask());
        }
        finally
        {
            auditScope?.Dispose();
        }

        Assert.Equal(0, executor.InvocationCount);
        Assert.Equal(0, approvalGate.RequestCount);
    }

    [Fact]
    public async Task ExplicitApprovalRequestUsesNativePortAndReturnsCheckpointedOriginalInput()
    {
        var executor = new RecordingExecutor(BuiltInWorkflowExecutorDescriptors.ApprovalRequest);
        var result = await RunExplicitApprovalAsync(executor, approved: true);

        Assert.Equal(RunStatus.Idle, result.Status);
        Assert.DoesNotContain(result.Events, workflowEvent => workflowEvent is WorkflowErrorEvent);
        Assert.Equal(0, executor.InvocationCount);
        Assert.Contains(result.Events.OfType<WorkflowOutputEvent>(), output =>
            output.Is<WorkflowNodeInput>(out var value) && value.PayloadJson == "{\"immutable\":true}");
    }

    [Fact]
    public async Task ExplicitApprovalDenialIsGovernedAndTamperingNeverInvokesLegacyApprovalExecutor()
    {
        var deniedExecutor = new RecordingExecutor(BuiltInWorkflowExecutorDescriptors.ApprovalRequest);
        var denied = await RunExplicitApprovalAsync(deniedExecutor, approved: false);
        var tamperedExecutor = new RecordingExecutor(BuiltInWorkflowExecutorDescriptors.ApprovalRequest);
        var tampered = await RunExplicitApprovalAsync(
            tamperedExecutor,
            approved: true,
            static continuation => continuation with
            {
                OriginalInput = new WorkflowNodeInput("{\"tampered\":true}")
            });

        Assert.DoesNotContain(denied.Events, workflowEvent => workflowEvent is WorkflowErrorEvent);
        Assert.Contains(denied.Events.OfType<WorkflowOutputEvent>(), output =>
            output.Is<WorkflowNodeInput>(out var value) &&
            WorkflowExecutorJson.Deserialize<MafWorkflowApprovalDeniedOutcome>(value.PayloadJson) is
            {
                Approved: false,
                Message: "denied"
            });
        Assert.Contains(tampered.Events, workflowEvent => workflowEvent is WorkflowErrorEvent);
        Assert.Equal(0, deniedExecutor.InvocationCount);
        Assert.Equal(0, tamperedExecutor.InvocationCount);
    }

    [Theory]
    [InlineData(ContinuationAuthorizationMismatch.RequestKind)]
    [InlineData(ContinuationAuthorizationMismatch.Action)]
    [InlineData(ContinuationAuthorizationMismatch.MissingActor)]
    [InlineData(ContinuationAuthorizationMismatch.AutonomousActor)]
    [InlineData(ContinuationAuthorizationMismatch.AutonomousSelfApproval)]
    [InlineData(ContinuationAuthorizationMismatch.AuthorizationScope)]
    [InlineData(ContinuationAuthorizationMismatch.AuthorizationPolicyFingerprint)]
    [InlineData(ContinuationAuthorizationMismatch.AuthorizedAtUtc)]
    [InlineData(ContinuationAuthorizationMismatch.ExpiresAtUtc)]
    public async Task ExplicitApprovalContinuationRejectsInvalidReconstructedGrant(
        ContinuationAuthorizationMismatch mismatch)
    {
        var executor = new RecordingExecutor(BuiltInWorkflowExecutorDescriptors.ApprovalRequest);
        var result = await RunExplicitApprovalAsync(
            executor,
            approved: true,
            mutateAuthorization: authorization => mismatch switch
            {
                ContinuationAuthorizationMismatch.RequestKind => authorization with
                {
                    RequestKind = WorkflowExternalRequestKind.HumanInput
                },
                ContinuationAuthorizationMismatch.Action => authorization with
                {
                    Action = WorkflowExternalResponseAction.SubmitInput
                },
                ContinuationAuthorizationMismatch.MissingActor => authorization with
                {
                    Actor = null!
                },
                ContinuationAuthorizationMismatch.AutonomousActor => authorization with
                {
                    Actor = new WorkflowLaunchActor(WorkflowLaunchActorKind.Agent, "autonomous-agent")
                },
                ContinuationAuthorizationMismatch.AutonomousSelfApproval => authorization with
                {
                    Actor = new WorkflowLaunchActor(WorkflowLaunchActorKind.Service, "autonomous-service"),
                    OriginActor = new WorkflowLaunchActor(WorkflowLaunchActorKind.Service, "autonomous-service")
                },
                ContinuationAuthorizationMismatch.AuthorizationScope => authorization with
                {
                    AuthorizationScope = null!
                },
                ContinuationAuthorizationMismatch.AuthorizationPolicyFingerprint => authorization with
                {
                    AuthorizationPolicyFingerprint = string.Empty
                },
                ContinuationAuthorizationMismatch.AuthorizedAtUtc => authorization with
                {
                    AuthorizedAtUtc = TimeProvider.System.GetUtcNow().AddMinutes(1),
                    ExpiresAtUtc = TimeProvider.System.GetUtcNow().AddMinutes(16)
                },
                ContinuationAuthorizationMismatch.ExpiresAtUtc => authorization with
                {
                    ExpiresAtUtc = authorization.AuthorizedAtUtc
                },
                _ => throw new ArgumentOutOfRangeException(nameof(mismatch), mismatch, null)
            });

        Assert.Contains(result.Events, workflowEvent => workflowEvent is WorkflowErrorEvent);
        Assert.Equal(0, executor.InvocationCount);
    }

    private static async Task<ApprovalRunResult> RunExplicitApprovalAsync(
        RecordingExecutor executor,
        bool approved,
        Func<MafWorkflowApprovalContinuation, MafWorkflowApprovalContinuation>? mutate = null,
        Func<WorkflowExternalResponseAuthorization, WorkflowExternalResponseAuthorization>?
            mutateAuthorization = null)
    {
        var catalog = new WorkflowExecutorCatalog([executor]);
        var invoker = new WorkflowExecutorInvoker(catalog, [executor]);
        var definition = CreateDefinition(
            [
                CreateNode("start", WorkflowNodeKind.Start),
                CreateExecutorNode("approval", WorkflowExecutorIds.ApprovalRequest),
                CreateNode("end", WorkflowNodeKind.End)
            ],
            [
                CreateEdge("start-approval", "start", "approval"),
                CreateEdge("approval-end", "approval", "end")
             ]);
        var runId = WorkflowRunId.New();
        var authorization = CreateExternalResponseAuthorization(
            definition,
            runId,
            approved);
        authorization = mutateAuthorization?.Invoke(authorization) ?? authorization;
        var invocationContext = CreateRecoveryInvocationContext(authorization);
        var compiler = new MafWorkflowCompiler(
            new WorkflowDefinitionValidator(catalog),
            invoker,
            executorCatalog: catalog);
        var build = compiler.Compile(
            definition,
            [],
            WorkflowPreviewSimulationPlan.Empty,
            invocationContext);
        var workflow = Assert.IsType<Workflow>(build.Workflow);
        using var auditScope = WorkflowExecutorExecutionAuditScope.Push(runId);
        using var cancellationSource = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await using var run = await InProcessExecution.RunStreamingAsync(
            workflow,
            new WorkflowNodeInput("{\"immutable\":true}"),
            cancellationToken: cancellationSource.Token);
        var events = new List<WorkflowEvent>();
        ExternalRequest? externalRequest = null;
        await foreach (var workflowEvent in run.WatchStreamAsync(
            blockOnPendingRequest: false,
            cancellationSource.Token))
        {
            events.Add(workflowEvent);
            if (workflowEvent is RequestInfoEvent requestInfoEvent)
            {
                externalRequest = requestInfoEvent.Request;
            }
        }

        Assert.NotNull(externalRequest);
        Assert.True(externalRequest.TryGetDataAs<MafWorkflowApprovalRequest>(out var approvalRequest));
        Assert.NotNull(approvalRequest);
        var continuation = MafWorkflowApprovalContinuation.Create(
            approvalRequest,
            authorization,
            approved,
            approved ? "approved" : "denied");
        await run.SendResponseAsync(externalRequest.CreateResponse(mutate?.Invoke(continuation) ?? continuation));
        await foreach (var workflowEvent in run.WatchStreamAsync(
            blockOnPendingRequest: false,
            cancellationSource.Token))
        {
            events.Add(workflowEvent);
        }

        return new ApprovalRunResult(await run.GetStatusAsync(cancellationSource.Token), events);
    }

    private static WorkflowExecutorInvocationContext CreateInvocationContext(
        WorkflowDefinition definition,
        WorkflowNode node,
        WorkflowExecutorDescriptor descriptor,
        WorkflowNodeInput input,
        WorkflowRunId runId,
        bool approved)
    {
        var token = WorkflowExecutorApprovalToken.New();
        var authorization = CreateExternalResponseAuthorization(definition, runId, approved);
        return CreateRecoveryInvocationContext(authorization) with
        {
            ApprovalAuthorization = new WorkflowExecutorApprovalAuthorization(
                WorkflowExecutorApprovalRequestId.New(),
                token,
                token,
                runId,
                definition.Id,
                definition.VersionId,
                node.Id,
                descriptor.Id,
                descriptor.PermissionPolicy.RequiredCapabilities,
                descriptor.PermissionPolicy.ApprovalRequirement,
                WorkflowExecutorInputHash.Compute(input),
                authorization,
                approved,
                approved ? "approved" : "denied")
        };
    }

    private static WorkflowExecutorInvocationContext CreateRecoveryInvocationContext(
        WorkflowExternalResponseAuthorization authorization)
        => new()
        {
            ExternalResponseAuthorization = authorization,
            CausationRequestId = authorization.RequestId,
            CausationRequestVersion = authorization.RequestVersion,
            CausationOperationId = authorization.OperationId,
            InvocationGeneration = new WorkflowExecutorInvocationGeneration(authorization.RequestVersion.Value)
        };

    private static WorkflowExternalResponseAuthorization CreateExternalResponseAuthorization(
        WorkflowDefinition definition,
        WorkflowRunId runId,
        bool approved)
    {
        var now = TimeProvider.System.GetUtcNow();
        return new WorkflowExternalResponseAuthorization(
            WorkflowExternalResponseOperationId.New(),
            WorkflowExternalRequestId.New(),
            WorkflowExternalRequestVersion.Initial,
            runId,
            definition.Id,
            definition.VersionId,
            WorkflowExternalRequestKind.Approval,
            approved ? WorkflowExternalResponseAction.Approve : WorkflowExternalResponseAction.Deny,
            new WorkflowLaunchActor(WorkflowLaunchActorKind.User, "test-approver"),
            WorkspaceScopeDescriptor.Organization("test-profile"),
            new WorkflowLaunchActor(WorkflowLaunchActorKind.Agent, "test-origin-agent"),
            WorkflowExternalResponseAuthorizationPolicy.CurrentFingerprint,
            now,
            now.AddSeconds(WorkflowExternalResponseAuthorizationPolicy.ResponseLifetimeSeconds));
    }

    private static string[] GetHiddenBindingIds(
        IReadOnlyDictionary<WorkflowNodeId, MafCompiledNodeBinding> bindings)
        => bindings.Values
            .SelectMany(binding => binding.Components)
            .Select(component => component.Binding.Id)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();

    private static void AssertBinding(
        MafCompiledNodeBinding binding,
        string entryId,
        string requestId,
        string exitId)
    {
        Assert.True(binding.HasNativeExternalRequest);
        Assert.Equal(entryId, binding.Entry.Id);
        Assert.Equal(exitId, binding.Exit.Id);
        Assert.Equal(3, binding.Components.Count);
        Assert.Equal(2, binding.InternalEdges.Count);
        var request = Assert.Single(binding.Components, component => component.Binding.Id == requestId);
        var requestPort = Assert.IsType<RequestPortBinding>(request.Binding);
        Assert.False(requestPort.AllowWrapped);
    }

    private static WorkflowDefinition CreateDefinition(
        IReadOnlyList<WorkflowNode> nodes,
        IReadOnlyList<WorkflowEdge> edges,
        string startNodeId = "start")
        => new(
            WorkflowId,
            WorkflowVersionId,
            "HITL workflow",
            "HITL workflow tests.",
            WorkflowLifecycleStatus.Draft,
            new WorkflowGraph(new WorkflowNodeId(startNodeId), nodes, edges),
            new WorkflowRuntimePolicy(
                WorkflowRuntimeBackendKind.InProcess,
                AllowInProcessPreviewRuns: true,
                RequireDurableProductionRuns: false,
                ExposeAzureFunctionsStatusEndpoint: false,
                ExposeAzureFunctionsMcpTool: false),
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);

    private static WorkflowNode CreateExecutorNode(string id, WorkflowExecutorId executorId)
        => CreateNode(id, WorkflowNodeKind.Executor) with
        {
            Settings = CreateNode(id, WorkflowNodeKind.Executor).Settings with
            {
                ExecutorId = executorId,
                ExecutorSettingsJson = "{}",
                ExecutionPolicy = WorkflowExecutorExecutionPolicy.Default
            }
        };

    private static WorkflowNode CreateNode(string id, WorkflowNodeKind kind)
        => new(
            new WorkflowNodeId(id),
            kind,
            id,
            [],
            new WorkflowNodeSettings(
                ComponentId: null,
                AgentId: null,
                SubworkflowId: null,
                ExternalRequestKind: kind == WorkflowNodeKind.HumanInput
                    ? WorkflowExternalRequestKind.HumanInput
                    : null,
                Instructions: kind == WorkflowNodeKind.HumanInput ? "Provide review data." : string.Empty,
                InputShape: WorkflowValueShape.Text,
                ResultShape: WorkflowValueShape.Text));

    private static WorkflowEdge CreateEdge(string id, string source, string target)
        => new(
            new WorkflowEdgeId(id),
            new WorkflowNodeId(source),
            SourcePortId: null,
            new WorkflowNodeId(target),
            TargetPortId: null,
            WorkflowEdgeKind.Direct,
            ConditionExpression: string.Empty);

    private sealed class RecordingExecutor(WorkflowExecutorDescriptor descriptor) : IWorkflowExecutor
    {
        public WorkflowExecutorDescriptor Descriptor { get; } = descriptor;

        public int InvocationCount { get; private set; }

        public WorkflowNodeInput? LastInput { get; private set; }

        public ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
            WorkflowExecutorExecutionContext context,
            WorkflowNodeInput input,
            CancellationToken cancellationToken = default)
        {
            InvocationCount++;
            LastInput = input;
            return ValueTask.FromResult(new WorkflowNodeExecutionResult(
                context.Node.Id,
                input.PayloadJson,
                context.Descriptor.ResultShape));
        }
    }

    private sealed class RecordingApprovalGate : IWorkflowExecutorApprovalGate
    {
        public int RequestCount { get; private set; }

        public ValueTask<WorkflowExecutorApprovalDecision> RequestApprovalAsync(
            WorkflowExecutorApprovalRequest request,
            CancellationToken cancellationToken = default)
        {
            RequestCount++;
            return ValueTask.FromResult(new WorkflowExecutorApprovalDecision(true, "approved"));
        }
    }

    private sealed record ApprovalRunResult(
        RunStatus Status,
        IReadOnlyList<WorkflowEvent> Events);

    public enum ApprovalBindingMismatch
    {
        MissingActiveRun,
        RunId,
        WorkflowId,
        WorkflowVersionId,
        NodeId,
        ExecutorId,
        RequiredCapabilities,
        ApprovalRequirement,
        InputHash,
        PresentedToken,
        MissingExecutorGrant,
        MissingInvocationGrant,
        OperationId,
        ExternalRequestId,
        ExternalRequestVersion,
        InvocationGeneration,
        RequestKind,
        Action,
        MissingActor,
        AutonomousActor,
        AutonomousSelfApproval,
        AuthorizationScope,
        AuthorizationPolicyFingerprint,
        AuthorizedAtUtc,
        ExpiresAtUtc,
        DecisionDenied
    }

    public enum ContinuationAuthorizationMismatch
    {
        RequestKind,
        Action,
        MissingActor,
        AutonomousActor,
        AutonomousSelfApproval,
        AuthorizationScope,
        AuthorizationPolicyFingerprint,
        AuthorizedAtUtc,
        ExpiresAtUtc
    }
}
