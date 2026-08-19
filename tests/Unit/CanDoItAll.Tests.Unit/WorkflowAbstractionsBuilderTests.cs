using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.AgentFramework.Workflows.Builder;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class WorkflowAbstractionsBuilderTests
{
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

    [Fact]
    public void WorkflowDefinitionBuilderCreatesDeterministicLinearLlmWorkflow()
    {
        var componentId = WorkflowComponentId.New();
        var definition = WorkflowFixtureFactory.CreateLinearLlmWorkflow(componentId);

        Assert.Equal("Linear LLM workflow", definition.Name);
        Assert.Equal(new WorkflowNodeId("start"), definition.Graph.StartNodeId);
        Assert.Collection(
            definition.Graph.Nodes,
            node => Assert.Equal(WorkflowNodeKind.Start, node.Kind),
            node =>
            {
                Assert.Equal(WorkflowNodeKind.LlmCall, node.Kind);
                Assert.Equal(componentId, node.Settings.ComponentId);
            },
            node => Assert.Equal(WorkflowNodeKind.End, node.Kind));
        Assert.Equal(
            [new WorkflowEdgeId("start-to-llm"), new WorkflowEdgeId("llm-to-end")],
            definition.Graph.Edges.Select(edge => edge.Id).ToArray());
    }

    [Fact]
    public void WorkflowDefinitionBuilderRejectsMissingStartWhenBuildingValidFixture()
    {
        var builder = WorkflowDefinitionBuilder
            .Create("Invalid graph")
            .AddNode(WorkflowNodeBuilder.End("end"));

        var exception = Assert.Throws<InvalidOperationException>(() => builder.Build());

        Assert.Contains("start node", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WorkflowDefinitionBuilderCanCreateExplicitInvalidFixtureForValidatorTests()
    {
        var definition = WorkflowFixtureFactory.CreateInvalidMissingStartWorkflow();

        Assert.Equal(new WorkflowNodeId("__missing-start__"), definition.Graph.StartNodeId);
        Assert.Single(definition.Graph.Nodes);
        Assert.Equal(WorkflowNodeKind.End, definition.Graph.Nodes[0].Kind);
    }

    [Fact]
    public void WorkflowNodeBuilderRejectsExecutorNodeWithoutExplicitExecutorContract()
    {
        var missingExecutorId = WorkflowNodeBuilder.For("execute", WorkflowNodeKind.Executor);
        var emptySettings = WorkflowNodeBuilder.For("execute", WorkflowNodeKind.Executor);

        var missingExecutorException = Assert.Throws<InvalidOperationException>(() => missingExecutorId.Build());
        var emptySettingsException = Assert.Throws<ArgumentException>(() => emptySettings.WithExecutor(WorkflowExecutorIds.ProjectStructure, " "));

        Assert.Contains("executor id", missingExecutorException.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("settingsJson", emptySettingsException.ParamName);
    }

    [Fact]
    public void WorkflowBuilderPreservesSerializedWorkflowFields()
    {
        var definition = WorkflowDefinitionBuilder
            .Create("Serialization workflow")
            .WithDescription("Round-trip proof.")
            .AddInputParameter(WorkflowInputParameterBuilder
                .Create("projectId")
                .WithLabel("Project")
                .WithKind(WorkflowInputParameterKind.ProjectId)
                .WithJsonPath("$.project.id")
                .Build())
            .AddNode(WorkflowNodeBuilder.Start("start"))
            .AddNode(WorkflowNodeBuilder.Executor("read-project", WorkflowExecutorIds.ProjectStructure, """{"operation":"ReadTree"}"""))
            .AddNode(WorkflowNodeBuilder.End("end"))
            .AddEdge(WorkflowEdgeBuilder.Direct("start-to-read", "start", "read-project"))
            .AddEdge(WorkflowEdgeBuilder.Direct("read-to-end", "read-project", "end"))
            .Build();

        var json = JsonSerializer.Serialize(definition, SerializerOptions);
        var roundTripped = JsonSerializer.Deserialize<WorkflowDefinition>(json, SerializerOptions);

        Assert.NotNull(roundTripped);
        Assert.Equal(definition.Graph.StartNodeId, roundTripped.Graph.StartNodeId);
        Assert.Equal(WorkflowExecutorIds.ProjectStructure, roundTripped.Graph.Nodes[1].Settings.ExecutorId);
        Assert.Equal("$.project.id", Assert.Single(roundTripped.InputParameters).JsonPath);
        Assert.Contains("inputParameters", json, StringComparison.Ordinal);
        Assert.Contains("runtimePolicy", json, StringComparison.Ordinal);
    }

    [Fact]
    public void WorkflowFixtureFactoryCreatesBranchingExecutorWorkflowWithPorts()
    {
        var definition = WorkflowFixtureFactory.CreateBranchingExecutorWorkflow(
            WorkflowExecutorIds.ProjectStructure,
            WorkflowExecutorIds.ProjectStructure);

        var triage = definition.Graph.Nodes.Single(node => node.Id == new WorkflowNodeId("triage"));
        var predicate = definition.Graph.Edges.Single(edge => edge.Id == new WorkflowEdgeId("triage-to-matched"));
        var defaultRoute = definition.Graph.Edges.Single(edge => edge.Id == new WorkflowEdgeId("triage-to-fallback"));

        Assert.Equal(WorkflowNodeKind.StrictLogic, triage.Kind);
        Assert.Collection(
            triage.Ports,
            port =>
            {
                Assert.Equal(new WorkflowPortId("input"), port.Id);
                Assert.Equal(WorkflowPortDirection.Input, port.Direction);
                Assert.True(port.Required);
            },
            port =>
            {
                Assert.Equal(new WorkflowPortId("matched"), port.Id);
                Assert.Equal(WorkflowPortDirection.Output, port.Direction);
                Assert.True(port.Required);
            },
            port =>
            {
                Assert.Equal(new WorkflowPortId("default"), port.Id);
                Assert.Equal(WorkflowPortDirection.Output, port.Direction);
                Assert.False(port.Required);
            });
        Assert.Equal(WorkflowRouteKind.Predicate, predicate.Routing.Kind);
        Assert.Equal(WorkflowRouteOperator.Equals, predicate.Routing.Operator);
        Assert.Equal(WorkflowRouteKind.SwitchDefault, defaultRoute.Routing.Kind);
    }

    [Fact]
    public void WorkflowFailureDiagnosticEnvelopeSerializesRepairableContext()
    {
        var diagnostic = WorkflowFixtureFactory.CreateExecutorFailureDiagnostic(
            new WorkflowNodeId("read-project"),
            WorkflowExecutorIds.ProjectStructure,
            "corr-123");

        var json = JsonSerializer.Serialize(diagnostic, SerializerOptions);
        var roundTripped = JsonSerializer.Deserialize<WorkflowFailureDiagnosticEnvelope>(json, SerializerOptions);

        Assert.NotNull(roundTripped);
        Assert.Equal(WorkflowFailureKind.Executor, roundTripped.Kind);
        Assert.Equal(WorkflowFailureRetryability.RetryableAfterRepair, roundTripped.Retryability);
        Assert.Equal(new WorkflowNodeId("read-project"), roundTripped.NodeId);
        Assert.Equal(WorkflowExecutorIds.ProjectStructure, roundTripped.ExecutorId);
        Assert.Equal(WorkflowFailureSourceKind.Executor, roundTripped.Source.Kind);
        Assert.Contains("repairHint", json, StringComparison.Ordinal);
        Assert.Contains("[REDACTED]", json, StringComparison.Ordinal);
    }

    [Fact]
    public void WorkflowAbstractionAndBuilderProjectsDoNotReferenceForbiddenImplementationProjects()
    {
        var root = FindRepositoryRoot();
        var projectPaths = new[]
        {
            Path.Combine(root, "src", "MAF", "Workflows", "CanDoItAll.AgentFramework.Workflows.Abstractions", "CanDoItAll.AgentFramework.Workflows.Abstractions.csproj"),
            Path.Combine(root, "src", "MAF", "Workflows", "CanDoItAll.AgentFramework.Workflows.Builder", "CanDoItAll.AgentFramework.Workflows.Builder.csproj")
        };
        var forbiddenReferences = new[]
        {
            "CanDoItAll.AgentFramework.Maf",
            "CanDoItAll.Modules.AgentFramework",
            "CanDoItAll.Modules.Plugins",
            "CanDoItAll.Plugins.Abstractions",
            "CanDoItAll.AgentFramework.Persistence"
        };

        foreach (var projectPath in projectPaths)
        {
            var project = XDocument.Load(projectPath);
            var references = project
                .Descendants("ProjectReference")
                .Select(element => element.Attribute("Include")?.Value ?? string.Empty)
                .Concat(project
                    .Descendants("PackageReference")
                    .Select(element => element.Attribute("Include")?.Value ?? string.Empty))
                .ToArray();

            foreach (var forbiddenReference in forbiddenReferences)
            {
                Assert.DoesNotContain(
                    references,
                    reference => reference.Contains(forbiddenReference, StringComparison.OrdinalIgnoreCase));
            }
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CanDoItAll.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate the repository root.");
    }

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
