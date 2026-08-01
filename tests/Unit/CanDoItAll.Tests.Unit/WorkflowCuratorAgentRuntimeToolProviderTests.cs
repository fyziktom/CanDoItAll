using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Modules.AgentFramework;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Unit;

[Collection(AppDbContextModelRegistryTestCollectionNames.Name)]
public sealed class WorkflowCuratorAgentRuntimeToolProviderTests
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    [Fact]
    public async Task Provider_fails_closed_for_identity_lifecycle_permission_purpose_and_catalog_spoofs()
    {
        var harness = CreateHarness();
        var wrongId = harness.Context with
        {
            Agent = harness.Context.Agent with { Id = Guid.NewGuid() }
        };
        var wrongTemplate = harness.Context with
        {
            Agent = harness.Context.Agent with { TemplateKey = "workflow-curator-agent-spoof" }
        };
        var suspended = harness.Context with
        {
            Agent = harness.Context.Agent with { Status = AgentLifecycleStatus.Suspended }
        };
        var template = harness.Context with
        {
            Agent = harness.Context.Agent with { IsTemplate = true }
        };
        var toolsDisabled = harness.Context with
        {
            Agent = harness.Context.Agent with
            {
                Permissions = harness.Context.Agent.Permissions with { CanUseTools = false }
            }
        };
        var processPurpose = harness.Context with
        {
            Purpose = AgentRuntimeToolProviderPurpose.GovernedProcessAutomation
        };
        var searchAssignment = Assert.Single(
            harness.Context.Agent.Capabilities,
            item => item.CapabilityKey == WorkflowCuratorAgentCapabilityKeys.CatalogSearch);
        var wrongCatalog = harness.Context with
        {
            Agent = harness.Context.Agent with { Capabilities = [searchAssignment] },
            Capabilities = harness.Context.Capabilities
                .Where(item => item.Id == searchAssignment.CapabilityId)
                .Select(item => item with { Key = $"{item.Key}-spoof" })
                .ToArray()
        };

        foreach (var context in new[]
                 {
                     wrongId,
                     wrongTemplate,
                     suspended,
                     template,
                     toolsDisabled,
                     processPurpose,
                     wrongCatalog
                 })
        {
            Assert.Empty(await harness.Provider.CreateToolsAsync(context, CancellationToken.None));
            Assert.Empty(harness.Provider.GetToolMetadata(context));
        }

        var wrongCase = CreateHarness(
            [WorkflowCuratorAgentCapabilityKeys.CatalogSearch.ToUpperInvariant()]);
        Assert.Empty(await wrongCase.Provider.CreateToolsAsync(wrongCase.Context, CancellationToken.None));
    }

    [Fact]
    public async Task Attached_tool_reauthorizes_capability_and_lifecycle_at_invocation_time()
    {
        var harness = CreateHarness([WorkflowCuratorAgentCapabilityKeys.CatalogSearch]);
        var searchTool = Assert.IsAssignableFrom<AIFunction>(Assert.Single(
            await harness.Provider.CreateToolsAsync(harness.Context, CancellationToken.None)));

        harness.Workspace.Agents =
        [
            harness.Context.Agent with { Capabilities = [] }
        ];

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            searchTool.InvokeAsync(new AIFunctionArguments
            {
                ["request"] = new WorkflowCuratorCatalogSearchInput()
            }).AsTask());

        harness.Workspace.Agents =
        [
            harness.Context.Agent with { Status = AgentLifecycleStatus.Suspended }
        ];

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            searchTool.InvokeAsync(new AIFunctionArguments
            {
                ["request"] = new WorkflowCuratorCatalogSearchInput()
            }).AsTask());
    }

    [Fact]
    public async Task Catalog_search_delegates_the_validated_page_query_to_the_bounded_search_service()
    {
        var item = new WorkflowCatalogItem(
            WorkflowId.New(),
            WorkflowVersionId.New(),
            "Matching workflow",
            "Matching metadata only.",
            WorkflowLifecycleStatus.Suspended,
            WorkflowRuntimeBackendKind.InProcess,
            DateTimeOffset.Parse("2026-07-19T11:30:00Z"));
        var search = new RecordingWorkflowCatalogSearchService(new WorkflowCatalogSearchPage(
            [item],
            PageIndex: 2,
            PageSize: 3,
            TotalCount: 8));
        var harness = CreateHarness(
            [WorkflowCuratorAgentCapabilityKeys.CatalogSearch],
            search);
        var searchTool = Assert.Single(
            await harness.Provider.CreateToolsAsync(harness.Context, CancellationToken.None));

        var result = await InvokeAsync<WorkflowCuratorCatalogSearchResult>(
            searchTool,
            new WorkflowCuratorCatalogSearchInput(
                "  MATCH  ",
                WorkflowLifecycleStatus.Suspended,
                pageIndex: 2,
                pageSize: 3));

        Assert.Equal([item], result.Items);
        Assert.Equal(2, result.PageIndex);
        Assert.Equal(3, result.PageSize);
        Assert.Equal(8, result.TotalCount);
        Assert.Equal(3, result.TotalPages);
        Assert.Equal(1, search.CallCount);
        Assert.NotNull(search.LastQuery);
        Assert.Equal("MATCH", search.LastQuery.Text);
        Assert.Equal(WorkflowLifecycleStatus.Suspended, search.LastQuery.Status);
        Assert.Equal(2, search.LastQuery.PageIndex);
        Assert.Equal(3, search.LastQuery.PageSize);
        Assert.Equal(6, search.LastQuery.Offset);
    }

    [Fact]
    public async Task Curator_tools_create_get_edit_one_node_preserve_graph_reject_stale_version_and_activate()
    {
        var harness = CreateHarness();
        var tools = (await harness.Provider.CreateToolsAsync(harness.Context, CancellationToken.None))
            .ToDictionary(tool => tool.Name, StringComparer.Ordinal);

        var created = await InvokeAsync<WorkflowCuratorDefinitionEditorResult>(
            tools[AgentToolInvocationPolicyMetadata.WorkflowCuratorDraftCreate],
            new WorkflowCuratorDraftCreateInput(
                "Curator acceptance workflow",
                "A minimal workflow authored through the managed curator."));

        Assert.Equal(WorkflowLifecycleStatus.Draft, created.Definition.Status);
        Assert.True(created.Validation.Succeeded);
        Assert.Equal("start", created.Definition.Graph.StartNodeId.Value);
        Assert.Collection(
            created.Definition.Graph.Nodes,
            node =>
            {
                Assert.Equal("start", node.Id.Value);
                Assert.Equal(WorkflowNodeKind.Start, node.Kind);
            },
            node =>
            {
                Assert.Equal("end", node.Id.Value);
                Assert.Equal(WorkflowNodeKind.End, node.Kind);
            });
        var originalEdge = Assert.Single(created.Definition.Graph.Edges);
        Assert.Equal("start", originalEdge.SourceNodeId.Value);
        Assert.Equal("end", originalEdge.TargetNodeId.Value);

        var editor = await InvokeAsync<WorkflowCuratorDefinitionEditorResult>(
            tools[AgentToolInvocationPolicyMetadata.WorkflowCuratorDefinitionEditorGet],
            new WorkflowCuratorDefinitionEditorInput(
                created.Definition.Id.Value,
                created.Definition.VersionId.Value));
        Assert.Equal(created.Definition.VersionId, editor.Definition.VersionId);

        var updated = await InvokeAsync<WorkflowCuratorDefinitionEditorResult>(
            tools[AgentToolInvocationPolicyMetadata.WorkflowCuratorNodeUpdate],
            new WorkflowCuratorNodeUpdateInput(
                editor.Definition.Id.Value,
                editor.Definition.VersionId.Value,
                "end",
                name: "Deliver accepted result",
                instructions: "Return the final accepted workflow result."));

        Assert.NotEqual(editor.Definition.VersionId, updated.Definition.VersionId);
        Assert.Equal(editor.Definition.Graph.StartNodeId, updated.Definition.Graph.StartNodeId);
        Assert.Equal(editor.Definition.Graph.Edges, updated.Definition.Graph.Edges);
        Assert.Equal(2, updated.Definition.Graph.Nodes.Count);
        var originalStart = editor.Definition.Graph.Nodes.Single(node => node.Id.Value == "start");
        var updatedStart = updated.Definition.Graph.Nodes.Single(node => node.Id.Value == "start");
        Assert.Equal(originalStart.Id, updatedStart.Id);
        Assert.Equal(originalStart.Kind, updatedStart.Kind);
        Assert.Equal(originalStart.Name, updatedStart.Name);
        Assert.Equal(originalStart.Settings, updatedStart.Settings);
        var updatedEnd = updated.Definition.Graph.Nodes.Single(node => node.Id.Value == "end");
        Assert.Equal("Deliver accepted result", updatedEnd.Name);
        Assert.Equal("Return the final accepted workflow result.", updatedEnd.Settings.Instructions);

        var staleException = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            InvokeAsync<WorkflowCuratorDefinitionEditorResult>(
                tools[AgentToolInvocationPolicyMetadata.WorkflowCuratorNodeUpdate],
                new WorkflowCuratorNodeUpdateInput(
                    editor.Definition.Id.Value,
                    editor.Definition.VersionId.Value,
                    "end",
                    name: "Stale overwrite")));
        Assert.Contains("updated by another request", staleException.Message, StringComparison.OrdinalIgnoreCase);

        var active = await InvokeAsync<WorkflowCuratorDefinitionEditorResult>(
            tools[AgentToolInvocationPolicyMetadata.WorkflowCuratorLifecycleChange],
            new WorkflowCuratorLifecycleChangeInput(
                updated.Definition.Id.Value,
                updated.Definition.VersionId.Value,
                WorkflowLifecycleStatus.Active));

        Assert.Equal(WorkflowLifecycleStatus.Active, active.Definition.Status);
        Assert.True(active.Validation.Succeeded);
        Assert.NotEqual(updated.Definition.VersionId, active.Definition.VersionId);
        Assert.Equal(updated.Definition.Graph.StartNodeId, active.Definition.Graph.StartNodeId);
        Assert.Equal(
            SnapshotNodes(updated.Definition.Graph),
            SnapshotNodes(active.Definition.Graph));
        Assert.Equal(
            SnapshotEdges(updated.Definition.Graph),
            SnapshotEdges(active.Definition.Graph));
    }

    [Fact]
    public async Task Curator_full_graph_create_and_update_round_trip_all_canonical_node_and_edge_fields()
    {
        var harness = CreateHarness();
        var tools = (await harness.Provider.CreateToolsAsync(harness.Context, CancellationToken.None))
            .ToDictionary(tool => tool.Name, StringComparer.Ordinal);
        var inputShape = new WorkflowCuratorValueShapeInput(
            WorkflowValueShapeKind.Object,
            """{"type":"object","properties":{"request":{"type":"string"}}}""",
            "Structured workflow request");
        var resultShape = new WorkflowCuratorValueShapeInput(
            WorkflowValueShapeKind.Object,
            """{"type":"object","properties":{"accepted":{"type":"boolean"}}}""",
            "Structured workflow result");
        var nodes = new[]
        {
            new WorkflowCuratorNodeInput(
                "start",
                WorkflowNodeKind.Start,
                "Receive request",
                inputShape: inputShape,
                resultShape: inputShape,
                ports:
                [
                    new WorkflowCuratorPortInput(
                        "request-out",
                        "Request output",
                        WorkflowPortDirection.Output,
                        inputShape,
                        required: true)
                ],
                canvasX: 12.5,
                canvasY: 25.75),
            new WorkflowCuratorNodeInput(
                "route",
                WorkflowNodeKind.StrictLogic,
                "Route request",
                "Evaluate the canonical request payload.",
                inputShape,
                resultShape,
                ports:
                [
                    new WorkflowCuratorPortInput(
                        "request-in",
                        "Request input",
                        WorkflowPortDirection.Input,
                        inputShape,
                        required: true),
                    new WorkflowCuratorPortInput(
                        "result-out",
                        "Result output",
                        WorkflowPortDirection.Output,
                        resultShape)
                ],
                model: "deterministic-router-v1",
                canvasX: 200.25,
                canvasY: 80.5),
            new WorkflowCuratorNodeInput(
                "end",
                WorkflowNodeKind.End,
                "Return result",
                inputShape: resultShape,
                resultShape: resultShape,
                ports:
                [
                    new WorkflowCuratorPortInput(
                        "result-in",
                        "Result input",
                        WorkflowPortDirection.Input,
                        resultShape,
                        required: true)
                ],
                canvasX: 410.75,
                canvasY: 25.25)
        };
        var edges = new[]
        {
            new WorkflowCuratorEdgeInput(
                "start",
                "route",
                "start-route",
                WorkflowEdgeKind.FanOut,
                WorkflowRouteKind.FanOutSelector,
                "Accepted request",
                "$.accepted",
                WorkflowRouteOperator.Equals,
                "true",
                WorkflowRouteValueKind.Boolean,
                caseSensitive: true,
                fanOutTargetIndex: 0,
                sourcePortId: "request-out",
                targetPortId: "request-in",
                conditionExpression: "legacy condition metadata",
                routingLanguage: WorkflowRoutingLanguages.BuiltInJsonV1),
            new WorkflowCuratorEdgeInput(
                "route",
                "end",
                "route-end",
                WorkflowEdgeKind.Conditional,
                label: "Complete route",
                sourcePortId: "result-out",
                targetPortId: "result-in",
                conditionExpression: "$.accepted == true",
                routingLanguage: WorkflowRoutingLanguages.LegacyConditionExpression)
        };

        var created = await InvokeAsync<WorkflowCuratorDefinitionEditorResult>(
            tools[AgentToolInvocationPolicyMetadata.WorkflowCuratorDraftCreate],
            new WorkflowCuratorDraftCreateInput(
                "Lossless graph workflow",
                startNodeId: "start",
                nodes: nodes,
                edges: edges));

        Assert.True(created.Validation.Succeeded);
        var createdRouteNode = created.Definition.Graph.Nodes.Single(node => node.Id.Value == "route");
        Assert.Equal(ToValueShape(inputShape), createdRouteNode.Settings.InputShape);
        Assert.Equal(ToValueShape(resultShape), createdRouteNode.Settings.ResultShape);
        Assert.Collection(
            createdRouteNode.Ports,
            port => Assert.Equal(
                new WorkflowPort(
                    new WorkflowPortId("request-in"),
                    "Request input",
                    WorkflowPortDirection.Input,
                    ToValueShape(inputShape),
                    Required: true),
                port),
            port => Assert.Equal(
                new WorkflowPort(
                    new WorkflowPortId("result-out"),
                    "Result output",
                    WorkflowPortDirection.Output,
                    ToValueShape(resultShape),
                    Required: false),
                port));
        var createdFanOut = created.Definition.Graph.Edges.Single(edge => edge.Id.Value == "start-route");
        Assert.Equal(new WorkflowPortId("request-out"), createdFanOut.SourcePortId);
        Assert.Equal(new WorkflowPortId("request-in"), createdFanOut.TargetPortId);
        Assert.Equal("legacy condition metadata", createdFanOut.ConditionExpression);
        Assert.Equal(WorkflowRoutingLanguages.BuiltInJsonV1, createdFanOut.Routing.RoutingLanguage);
        Assert.Equal(0, createdFanOut.Routing.FanOutTargetIndex);
        var createdLegacy = created.Definition.Graph.Edges.Single(edge => edge.Id.Value == "route-end");
        Assert.Equal(new WorkflowPortId("result-out"), createdLegacy.SourcePortId);
        Assert.Equal(new WorkflowPortId("result-in"), createdLegacy.TargetPortId);
        Assert.Equal("$.accepted == true", createdLegacy.ConditionExpression);
        Assert.Equal(WorkflowRoutingLanguages.LegacyConditionExpression, createdLegacy.Routing.RoutingLanguage);

        var updated = await InvokeAsync<WorkflowCuratorDefinitionEditorResult>(
            tools[AgentToolInvocationPolicyMetadata.WorkflowCuratorDraftUpdate],
            new WorkflowCuratorDraftUpdateInput(
                created.Definition.Id.Value,
                created.Definition.VersionId.Value,
                description: "Re-saved through the full graph authoring contract.",
                startNodeId: created.Definition.Graph.StartNodeId.Value,
                nodes: created.Definition.Graph.Nodes.Select(ToCuratorInput).ToArray(),
                edges: created.Definition.Graph.Edges.Select(ToCuratorInput).ToArray()));

        Assert.True(updated.Validation.Succeeded);
        Assert.NotEqual(created.Definition.VersionId, updated.Definition.VersionId);
        AssertGraphEqual(created.Definition.Graph, updated.Definition.Graph);
    }

    [Fact]
    public async Task Curator_full_graph_round_trip_preserves_null_and_explicit_node_defaults()
    {
        var harness = CreateHarness();
        var tools = (await harness.Provider.CreateToolsAsync(harness.Context, CancellationToken.None))
            .ToDictionary(tool => tool.Name, StringComparer.Ordinal);
        var explicitShape = new WorkflowCuratorValueShapeInput(
            WorkflowValueShapeKind.Object,
            """{"type":"object","required":["result"]}""",
            "Explicit executor payload");
        var explicitPolicy = new WorkflowExecutorExecutionPolicy(
            TimeoutSeconds: 90,
            MaxRetryAttempts: 2,
            RetryDelayMilliseconds: 750,
            CaptureOutputArtifact: true);
        var nodes = new[]
        {
            new WorkflowCuratorNodeInput("start", WorkflowNodeKind.Start),
            new WorkflowCuratorNodeInput(
                "default-executor",
                WorkflowNodeKind.Executor,
                executorId: "test.default-executor"),
            new WorkflowCuratorNodeInput(
                "null-executor",
                WorkflowNodeKind.Executor,
                executorId: "test.null-executor",
                omittedValueBehavior: WorkflowCuratorNodeOmittedValueBehavior.PreserveNulls),
            new WorkflowCuratorNodeInput(
                "explicit-executor",
                WorkflowNodeKind.Executor,
                inputShape: explicitShape,
                resultShape: explicitShape,
                executorId: "test.explicit-executor",
                executionPolicy: explicitPolicy,
                omittedValueBehavior: WorkflowCuratorNodeOmittedValueBehavior.PreserveNulls),
            new WorkflowCuratorNodeInput(
                "end",
                WorkflowNodeKind.End,
                inputShape: explicitShape,
                resultShape: explicitShape)
        };

        var created = await InvokeAsync<WorkflowCuratorDefinitionEditorResult>(
            tools[AgentToolInvocationPolicyMetadata.WorkflowCuratorDraftCreate],
            new WorkflowCuratorDraftCreateInput(
                "Nullable node settings workflow",
                nodes: nodes));

        Assert.True(created.Validation.Succeeded);
        var defaultExecutor = created.Definition.Graph.Nodes.Single(node => node.Id.Value == "default-executor");
        Assert.Equal(WorkflowValueShape.Text, defaultExecutor.Settings.InputShape);
        Assert.Equal(WorkflowValueShape.Text, defaultExecutor.Settings.ResultShape);
        Assert.Equal(WorkflowExecutorExecutionPolicy.Default, defaultExecutor.Settings.ExecutionPolicy);
        var nullExecutor = created.Definition.Graph.Nodes.Single(node => node.Id.Value == "null-executor");
        Assert.Null(nullExecutor.Settings.InputShape);
        Assert.Null(nullExecutor.Settings.ResultShape);
        Assert.Null(nullExecutor.Settings.ExecutionPolicy);
        var explicitExecutor = created.Definition.Graph.Nodes.Single(node => node.Id.Value == "explicit-executor");
        Assert.Equal(ToValueShape(explicitShape), explicitExecutor.Settings.InputShape);
        Assert.Equal(ToValueShape(explicitShape), explicitExecutor.Settings.ResultShape);
        Assert.Equal(explicitPolicy, explicitExecutor.Settings.ExecutionPolicy);

        var updated = await InvokeAsync<WorkflowCuratorDefinitionEditorResult>(
            tools[AgentToolInvocationPolicyMetadata.WorkflowCuratorDraftUpdate],
            new WorkflowCuratorDraftUpdateInput(
                created.Definition.Id.Value,
                created.Definition.VersionId.Value,
                description: "Re-saved without materializing canonical null node settings.",
                startNodeId: created.Definition.Graph.StartNodeId.Value,
                nodes: created.Definition.Graph.Nodes.Select(ToCuratorInput).ToArray(),
                edges: created.Definition.Graph.Edges.Select(ToCuratorInput).ToArray()));

        Assert.True(updated.Validation.Succeeded);
        Assert.NotEqual(created.Definition.VersionId, updated.Definition.VersionId);
        AssertGraphEqual(created.Definition.Graph, updated.Definition.Graph);
        var updatedNullExecutor = updated.Definition.Graph.Nodes.Single(node => node.Id.Value == "null-executor");
        Assert.Null(updatedNullExecutor.Settings.InputShape);
        Assert.Null(updatedNullExecutor.Settings.ResultShape);
        Assert.Null(updatedNullExecutor.Settings.ExecutionPolicy);
    }

    [Fact]
    public void Metadata_requires_approval_for_mutations_and_redacts_workflow_content_from_audit_data()
    {
        var mutationNames = new[]
        {
            AgentToolInvocationPolicyMetadata.WorkflowCuratorDraftCreate,
            AgentToolInvocationPolicyMetadata.WorkflowCuratorDraftUpdate,
            AgentToolInvocationPolicyMetadata.WorkflowCuratorNodeUpdate,
            AgentToolInvocationPolicyMetadata.WorkflowCuratorLifecycleChange
        };
        var readNames = new[]
        {
            AgentToolInvocationPolicyMetadata.WorkflowCuratorCatalogSearch,
            AgentToolInvocationPolicyMetadata.WorkflowCuratorDefinitionEditorGet,
            AgentToolInvocationPolicyMetadata.WorkflowCuratorAuthoringOptionsGet
        };

        Assert.All(mutationNames, toolName =>
        {
            Assert.True(ToolContractCatalog.IsKnownToolName(toolName));
            Assert.Equal(ToolInvocationClassification.Mutation, AgentToolInvocationPolicyMetadata.Classify(toolName));
            Assert.True(AgentToolInvocationPolicyMetadata.RequiresApprovalByDefault(toolName));
            Assert.True(ToolCapabilityRegistry.TryResolve(toolName, out var metadata));
            Assert.Equal(ToolCapabilitySideEffectKind.InternalStateMutation, metadata.SideEffectKind);
        });
        Assert.All(readNames, toolName =>
        {
            Assert.True(ToolContractCatalog.IsKnownToolName(toolName));
            Assert.Equal(ToolInvocationClassification.Read, AgentToolInvocationPolicyMetadata.Classify(toolName));
            Assert.False(AgentToolInvocationPolicyMetadata.RequiresApprovalByDefault(toolName));
            Assert.True(ToolCapabilityRegistry.TryResolve(toolName, out var metadata));
            Assert.Equal(ToolCapabilitySideEffectKind.InternalDataRead, metadata.SideEffectKind);
        });

        var workflowId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var expectedVersionId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        const string nodeId = "confidential-review";
        const string name = "Confidential customer workflow";
        const string instructions = "Process private Project Nightfall records.";
        var request = new
        {
            workflowId,
            expectedVersionId,
            nodeId,
            name,
            instructions
        };
        var redacted = AgentToolInvocationPolicyMetadata.RedactArguments(
            AgentToolInvocationPolicyMetadata.WorkflowCuratorNodeUpdate,
        [
            new KeyValuePair<string, object?>("request", request)
        ]);
        var signature = AgentToolInvocationPolicyMetadata.BuildSignature(
            AgentToolInvocationPolicyMetadata.WorkflowCuratorNodeUpdate,
            redacted);
        var audit = AgentToolInvocationPolicyMetadata.ProtectApprovalArgumentsForAudit(
            AgentToolInvocationPolicyMetadata.WorkflowCuratorNodeUpdate,
            JsonSerializer.Serialize(new { request }));

        Assert.Contains(expectedVersionId.ToString("D"), signature, StringComparison.Ordinal);
        Assert.DoesNotContain(name, signature, StringComparison.Ordinal);
        Assert.DoesNotContain(instructions, signature, StringComparison.Ordinal);
        Assert.Contains("workflow-curator-approval-redacted-v1", audit, StringComparison.Ordinal);
        Assert.Contains(workflowId.ToString("D"), audit, StringComparison.Ordinal);
        Assert.Contains(expectedVersionId.ToString("D"), audit, StringComparison.Ordinal);
        Assert.Contains(nodeId, audit, StringComparison.Ordinal);
        Assert.DoesNotContain(name, audit, StringComparison.Ordinal);
        Assert.DoesNotContain(instructions, audit, StringComparison.Ordinal);
    }

    [Fact]
    public void AddAgentFrameworkModule_registers_curator_authorization_and_provider_as_scoped()
    {
        var services = new ServiceCollection();
        services.AddAgentFrameworkModule(new ConfigurationBuilder().Build());

        var authorization = Assert.Single(
            services,
            descriptor =>
                descriptor.ServiceType == typeof(WorkflowCuratorAgentRuntimeAuthorizationService) &&
                descriptor.ImplementationType == typeof(WorkflowCuratorAgentRuntimeAuthorizationService));
        var provider = Assert.Single(
            services,
            descriptor =>
                descriptor.ServiceType == typeof(IAgentRuntimeToolProvider) &&
                descriptor.ImplementationType == typeof(WorkflowCuratorAgentRuntimeToolProvider));
        var search = Assert.Single(
            services,
            descriptor => descriptor.ServiceType == typeof(IWorkflowCatalogSearchService));

        Assert.Equal(ServiceLifetime.Scoped, authorization.Lifetime);
        Assert.Equal(ServiceLifetime.Scoped, provider.Lifetime);
        Assert.Equal(ServiceLifetime.Scoped, search.Lifetime);
    }

    private static IReadOnlyList<(string Id, WorkflowNodeKind Kind, string Name, string Instructions)> SnapshotNodes(
        WorkflowGraph graph)
    {
        return graph.Nodes
            .Select(node => (node.Id.Value, node.Kind, node.Name, node.Settings.Instructions))
            .ToArray();
    }

    private static IReadOnlyList<(string Id, string SourceNodeId, string TargetNodeId, WorkflowEdgeKind Kind)> SnapshotEdges(
        WorkflowGraph graph)
    {
        return graph.Edges
            .Select(edge => (
                edge.Id.Value,
                edge.SourceNodeId.Value,
                edge.TargetNodeId.Value,
                edge.Kind))
            .ToArray();
    }

    private static WorkflowCuratorNodeInput ToCuratorInput(WorkflowNode node)
    {
        return new WorkflowCuratorNodeInput(
            node.Id.Value,
            node.Kind,
            node.Name,
            node.Settings.Instructions,
            ToCuratorInput(node.Settings.InputShape),
            ToCuratorInput(node.Settings.ResultShape),
            node.Ports.Select(ToCuratorInput).ToArray(),
            node.Settings.ComponentId?.Value,
            node.Settings.ProviderProfileId,
            node.Settings.Model,
            node.Settings.AgentId,
            node.Settings.SubworkflowId?.Value,
            node.Settings.ExternalRequestKind,
            node.Settings.ExecutorId?.Value,
            node.Settings.ExecutorSettingsJson,
            node.Settings.ExecutionPolicy,
            node.CanvasX,
            node.CanvasY,
            WorkflowCuratorNodeOmittedValueBehavior.PreserveNulls);
    }

    private static WorkflowCuratorValueShapeInput? ToCuratorInput(WorkflowValueShape? shape)
        => shape is null
            ? null
            : new WorkflowCuratorValueShapeInput(shape.Kind, shape.SchemaJson, shape.Description);

    private static WorkflowValueShape ToValueShape(WorkflowCuratorValueShapeInput shape)
        => new(shape.Kind, shape.SchemaJson, shape.Description);

    private static WorkflowCuratorPortInput ToCuratorInput(WorkflowPort port)
        => new(
            port.Id.Value,
            port.Name,
            port.Direction,
            ToCuratorInput(port.Shape),
            port.Required);

    private static WorkflowCuratorEdgeInput ToCuratorInput(WorkflowEdge edge)
        => new(
            edge.SourceNodeId.Value,
            edge.TargetNodeId.Value,
            edge.Id.Value,
            edge.Kind,
            edge.Routing.Kind,
            edge.Routing.Label,
            edge.Routing.JsonPath,
            edge.Routing.Operator,
            edge.Routing.ExpectedValueJson,
            edge.Routing.ExpectedValueKind,
            edge.Routing.CaseSensitive,
            edge.Routing.FanOutTargetIndex,
            edge.SourcePortId?.Value,
            edge.TargetPortId?.Value,
            edge.ConditionExpression,
            edge.Routing.RoutingLanguage);

    private static void AssertGraphEqual(WorkflowGraph expected, WorkflowGraph actual)
    {
        Assert.Equal(expected.StartNodeId, actual.StartNodeId);
        Assert.Equal(expected.Nodes.Count, actual.Nodes.Count);
        for (var index = 0; index < expected.Nodes.Count; index++)
        {
            var expectedNode = expected.Nodes[index];
            var actualNode = actual.Nodes[index];
            Assert.Equal(expectedNode.Id, actualNode.Id);
            Assert.Equal(expectedNode.Kind, actualNode.Kind);
            Assert.Equal(expectedNode.Name, actualNode.Name);
            Assert.Equal(expectedNode.Settings, actualNode.Settings);
            Assert.Equal(expectedNode.CanvasX, actualNode.CanvasX);
            Assert.Equal(expectedNode.CanvasY, actualNode.CanvasY);
            Assert.Equal(expectedNode.Ports.Count, actualNode.Ports.Count);
            for (var portIndex = 0; portIndex < expectedNode.Ports.Count; portIndex++)
            {
                Assert.Equal(expectedNode.Ports[portIndex], actualNode.Ports[portIndex]);
            }
        }

        Assert.Equal(expected.Edges.Count, actual.Edges.Count);
        for (var index = 0; index < expected.Edges.Count; index++)
        {
            Assert.Equal(expected.Edges[index], actual.Edges[index]);
        }
    }

    private static RuntimeHarness CreateHarness(
        IEnumerable<string>? capabilityKeys = null,
        IWorkflowCatalogSearchService? catalogSearch = null)
    {
        var now = DateTimeOffset.Parse("2026-07-19T12:00:00Z");
        var keys = (capabilityKeys ?? WorkflowCuratorAgentCapabilityKeys.ToolNameToCapabilityKey.Values)
            .ToArray();
        var capabilities = keys
            .Select(key => new CapabilityCatalogItem(
                Guid.NewGuid(),
                CapabilityKind.Tool,
                key,
                key,
                string.Empty,
                string.Empty,
                string.Empty,
                CapabilityProofStatus.Verified,
                string.Empty,
                now,
                IsBuiltIn: true))
            .ToArray();
        var assignments = capabilities
            .Select(capability => new AgentCapabilityAssignment(
                capability.Id,
                capability.Key,
                capability.Kind,
                capability.ProofStatus,
                capability.LastVerifiedAtUtc,
                capability.ProofNotes))
            .ToArray();
        var providerProfileId = Guid.NewGuid();
        var agent = new AgentDefinition(
            WorkflowCuratorAgentIdentity.AgentId,
            "Workflow Curator Agent",
            "Workflow catalog curator",
            "Authors and operates governed workflows.",
            "Use only the dedicated Workflow Curator tools.",
            AgentLifecycleStatus.Active,
            providerProfileId,
            "gpt-5.4-mini",
            AgentWorkloadKind.Management,
            AgentChatHistoryMode.FrameworkManaged,
            0.2,
            RequirePerServiceCallChatHistoryPersistence: false,
            EnableBackgroundResponses: false,
            "{}",
            IsTemplate: false,
            WorkflowCuratorAgentIdentity.TemplateKey,
            AgentPermissionsPolicy.Default with { CanUseTools = true },
            assignments,
            [],
            now,
            now);
        var providerProfile = new ProviderProfile(
            providerProfileId,
            "OpenAI default",
            ProviderKind.OpenAi,
            "https://api.openai.com/v1",
            "OPENAI_API_KEY",
            agent.Model,
            ProviderTransportKind.Responses,
            IsEnabled: true,
            SupportsStreaming: true,
            SupportsTools: true,
            PreferFrameworkManagedChatHistory: true,
            SupportsBackgroundResponses: false,
            ConfigurationJson: string.Empty,
            Notes: string.Empty,
            HealthStatus: string.Empty,
            LastCheckedAtUtc: null,
            SuggestedModels: [],
            ProviderProfilePurpose.Chat);
        var workspaceService = DispatchProxy.Create<IAgentFrameworkWorkspaceService, AuthorizationWorkspaceProxy>();
        var workspace = (AuthorizationWorkspaceProxy)(object)workspaceService;
        workspace.Agents = [agent];
        workspace.Capabilities = capabilities;
        var catalog = new InMemoryWorkflowCatalogService(
            new InMemoryWorkflowCatalogStore(),
            new WorkflowDefinitionValidator());
        var runtimeProvider = new WorkflowCuratorAgentRuntimeToolProvider(
            catalog,
            catalogSearch ?? catalog,
            catalog,
            WorkflowExecutorCatalog.FromDescriptors([]),
            new WorkflowRuntimeBackendCatalog([WorkflowRuntimeBackendKind.InProcess]),
            new WorkflowCuratorAgentRuntimeAuthorizationService(workspaceService));
        var context = new AgentRuntimeToolProviderContext(
            agent,
            providerProfile,
            capabilities,
            SuppressApprovalRequirements: false,
            AgentRuntimeToolProviderPurpose.InteractiveChat,
            RuntimeSessionKey: "workflow-curator-runtime-test",
            AgentRuntimeContextIntent.Empty,
            Tags: new Dictionary<string, string>());
        return new RuntimeHarness(runtimeProvider, context, workspace);
    }

    private static async Task<TResult> InvokeAsync<TResult>(AITool tool, object request)
    {
        var function = Assert.IsAssignableFrom<AIFunction>(tool);
        var rawResult = await function.InvokeAsync(new AIFunctionArguments
        {
            ["request"] = request
        });
        return rawResult switch
        {
            TResult result => result,
            JsonElement element => JsonSerializer.Deserialize<TResult>(element.GetRawText(), JsonOptions)
                ?? throw new InvalidOperationException("Workflow Curator runtime tool returned null JSON."),
            _ => throw new InvalidOperationException(
                $"Unexpected Workflow Curator runtime tool result type '{rawResult?.GetType().FullName ?? "<null>"}'.")
        };
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private sealed record RuntimeHarness(
        WorkflowCuratorAgentRuntimeToolProvider Provider,
        AgentRuntimeToolProviderContext Context,
        AuthorizationWorkspaceProxy Workspace);

    private sealed class RecordingWorkflowCatalogSearchService(WorkflowCatalogSearchPage result) :
        IWorkflowCatalogSearchService
    {
        public int CallCount { get; private set; }

        public WorkflowCatalogSearchQuery? LastQuery { get; private set; }

        public Task<WorkflowCatalogSearchPage> SearchDefinitionsAsync(
            WorkflowCatalogSearchQuery query,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            LastQuery = query;
            return Task.FromResult(result);
        }
    }

    private class AuthorizationWorkspaceProxy : DispatchProxy
    {
        public IReadOnlyList<AgentDefinition> Agents { get; set; } = [];

        public IReadOnlyList<CapabilityCatalogItem> Capabilities { get; set; } = [];

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            return targetMethod?.Name switch
            {
                nameof(IAgentFrameworkWorkspaceService.ListAgentsAsync) =>
                    Task.FromResult(Agents),
                nameof(IAgentFrameworkWorkspaceService.ListCapabilitiesAsync) =>
                    Task.FromResult(Capabilities),
                _ => throw new InvalidOperationException(
                    $"Workspace service member '{targetMethod?.Name}' was not expected in this runtime-provider test.")
            };
        }
    }
}
