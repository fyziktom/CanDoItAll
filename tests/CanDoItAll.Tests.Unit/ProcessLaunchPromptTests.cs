using System.Reflection;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Processes.Templates;

namespace CanDoItAll.Tests.Unit;

public sealed class ProcessLaunchPromptTests
{
    [Fact]
    public void Step_prompt_includes_process_run_id_and_managed_artifact_root()
    {
        var runId = new ProcessRunId(Guid.Parse("d9450dd1-4920-457c-92a4-48d1ec648181"));
        var prompt = BuildStepPrompt(runId);

        Assert.Contains("Process run id: d9450dd1-4920-457c-92a4-48d1ec648181", prompt, StringComparison.Ordinal);
        Assert.Contains("Managed process artifact root: artifacts/process-runs/d9450dd1-4920-457c-92a4-48d1ec648181", prompt, StringComparison.Ordinal);
        Assert.Contains("Do not write evidence under output/", prompt, StringComparison.Ordinal);
    }

    private static string BuildStepPrompt(ProcessRunId runId)
    {
        var request = new ProcessLaunchRequest(
            DefinitionKey: "blazor-app-delivery",
            ProcessDefinitionId: null,
            LiveRunProfileKey: null,
            ProjectId: Guid.Parse("3324868f-66e2-478a-bb8f-14f32a5db1e9"),
            ProjectNodeId: "custom:bd8169fc3fa944dbafd13998fb167fe8",
            RequestedBy: "codex-process-e2e",
            Variables: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["RepositoryRoot"] = @"C:\programovani\dotnet\output"
            },
            RunReadiness: true,
            Execute: true);
        var step = new ProcessTemplateDefinitionStepDocument
        {
            Key = "resolve-blazor-contract",
            Title = "Resolve Blazor delivery contract",
            Notes = "Resolve product and evidence paths.",
            InputContractSummary = "Use project structure.",
            OutputContractSummary = "Produce the handoff contract.",
            EvidenceContractSummary = "Write durable evidence.",
            AllowedOperations =
            [
                ProcessOperationContractNames.ReadProjectStructure,
                ProcessOperationContractNames.WriteManagedProcessArtifacts
            ],
            OperationTargetScope = ProcessOperationContractNames.ExternalProductTargetReadOnly
        };
        var selection = CreateSelection(new ProcessTemplateDefinitionDocument
        {
            Key = "blazor-app-delivery",
            DisplayName = "Blazor app delivery",
            Summary = "Deliver a Blazor app."
        });
        var method = typeof(ProcessLaunchApplicationService).GetMethod(
            "BuildStepPrompt",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildStepPrompt was not found.");

        return Assert.IsType<string>(method.Invoke(
            null,
            [
                request,
                selection,
                step,
                null,
                Array.Empty<ArtifactSlotId>(),
                new[] { ArtifactSlotId.New() },
                runId
            ]));
    }

    private static object CreateSelection(ProcessTemplateDefinitionDocument definition)
    {
        var selectionType = typeof(ProcessLaunchApplicationService).GetNestedType(
            "ProcessTemplateSelection",
            BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("ProcessTemplateSelection was not found.");
        var pack = new ProcessTemplatePack(
            RootPath: string.Empty,
            new ProcessTemplatePackManifest
            {
                PackKey = "test-pack",
                Name = "Test pack",
                Version = "1.0"
            },
            Definitions: []);
        return Activator.CreateInstance(
            selectionType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [pack, definition, null],
            culture: null)
            ?? throw new InvalidOperationException("ProcessTemplateSelection could not be created.");
    }
}
