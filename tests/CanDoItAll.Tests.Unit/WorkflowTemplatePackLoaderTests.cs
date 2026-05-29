using CanDoItAll.Modules.AgentFramework;

namespace CanDoItAll.Tests.Unit;

public sealed class WorkflowTemplatePackLoaderTests
{
    [Fact]
    public void Load_rejects_semantically_invalid_template_graph_with_source_context()
    {
        using var packDirectory = new TemporaryWorkflowTemplatePack(
            "invalid-graph.yaml",
            """
            workflows:
              - key: invalid-missing-target
                name: Invalid missing target
                description: Invalid test workflow.
                routingInstructions: Return JSON.
                graph:
                  startNodeId: start
                  nodes:
                    - id: start
                      kind: Start
                      name: Start
                      x: 0
                      y: 0
                    - id: end
                      kind: End
                      name: End
                      x: 300
                      y: 0
                  edges:
                    - id: start-to-missing
                      source: start
                      target: missing-node
            """);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            new WorkflowTemplatePackLoader(packDirectory.RootPath).Load());

        Assert.Contains("invalid-missing-target", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("invalid-graph.yaml", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("start-to-missing", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class TemporaryWorkflowTemplatePack : IDisposable
    {
        public TemporaryWorkflowTemplatePack(string workflowFileName, string workflowYaml)
        {
            RootPath = Path.Combine(Path.GetTempPath(), $"workflow-template-pack-{Guid.NewGuid():N}");
            var workflowsPath = Path.Combine(RootPath, "workflows");
            Directory.CreateDirectory(workflowsPath);

            File.WriteAllText(
                Path.Combine(RootPath, "manifest.yaml"),
                $$"""
                packKey: test-pack
                name: Test Pack
                version: 1.0.0
                seedMarker: TEST-SEED
                seedVersion: test
                definitionNamePrefix: ""
                componentNamePrefix: ""
                component:
                  modelSettings:
                    temperature: 0.2
                    maxOutputTokens: 256
                    requireJsonOutput: true
                    responseFormatJsonSchema: "{}"
                  instructionsTemplate: "{name}\n{routingInstructions}"
                jsonShape:
                  kind: Json
                  schemaJson: "{}"
                  description: JSON payload
                runtimePolicy:
                  preferredBackend: InProcess
                  allowInProcessPreviewRuns: true
                  requireDurableProductionRuns: false
                  exposeAzureFunctionsStatusEndpoint: false
                  exposeAzureFunctionsMcpTool: false
                executorPolicies:
                  slow:
                    timeoutSeconds: 30
                    maxRetryAttempts: 0
                    retryDelayMilliseconds: 250
                    captureOutputArtifact: false
                nodeInstructionDefaults: {}
                workflowFiles:
                  - relativePath: workflows/{{workflowFileName}}
                """);
            File.WriteAllText(Path.Combine(workflowsPath, workflowFileName), workflowYaml);
        }

        public string RootPath { get; }

        public void Dispose()
        {
            if (Directory.Exists(RootPath))
            {
                Directory.Delete(RootPath, recursive: true);
            }
        }
    }
}
