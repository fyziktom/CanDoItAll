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

    [Fact]
    public void Step_prompt_includes_subprocess_mapping_and_launch_tool_guidance()
    {
        var runId = new ProcessRunId(Guid.Parse("d9450dd1-4920-457c-92a4-48d1ec648181"));
        var step = CreatePromptStep();
        step.Key = "architecture-review";
        step.Title = ".NET architecture review";
        step.StepKind = "Subprocess";
        step.SubprocessProcessKey = "dotnet-architecture-design-review";
        step.SubprocessDefinitionSnapshotName = ".NET architecture design and review subprocess";
        step.AllowedOperations =
        [
            ProcessOperationContractNames.ReadProjectStructure,
            ProcessOperationContractNames.ExecuteExternalAction
        ];
        var prompt = BuildStepPrompt(runId, step);

        Assert.Contains("Step kind: Subprocess", prompt, StringComparison.Ordinal);
        Assert.Contains("Subprocess mapping:", prompt, StringComparison.Ordinal);
        Assert.Contains("Child process definition key: dotnet-architecture-design-review", prompt, StringComparison.Ordinal);
        Assert.Contains("Governed launch tool: project_structure_process_subprocess_launch", prompt, StringComparison.Ordinal);
        Assert.Contains("Do not mark Completed until the child run receipt", prompt, StringComparison.Ordinal);
        Assert.Contains("leave LiveRunProfileKey empty", prompt, StringComparison.Ordinal);
        Assert.Contains("BranchName, RepositoryRoot, SessionId", prompt, StringComparison.Ordinal);
        Assert.Contains("ChildManagedArtifactRoot", prompt, StringComparison.Ordinal);
        Assert.Contains("Treat artifacts under ChildManagedArtifactRoot as the child evidence bundle", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void Step_prompt_maps_required_upstream_slots_to_producer_evidence_paths()
    {
        var runId = new ProcessRunId(Guid.Parse("d9450dd1-4920-457c-92a4-48d1ec648181"));
        var designSlot = new ArtifactSlotId(Guid.Parse("0dd0f0c0-4224-faba-36b0-6df9286b51b3"));
        var producer = CreatePromptStep();
        producer.Key = "draft-architecture-design";
        producer.Title = "Draft .NET architecture design";
        producer.ArtifactExpectations =
        [
            new ProcessTemplateDefinitionArtifactExpectationDocument
            {
                Key = "dotnet-architecture-design",
                Title = ".NET architecture design draft",
                ArtifactKind = "Decision",
                IsRequired = true,
                ValidationRequirementSummary = "Must describe boundaries and testability seams."
            }
        ];
        var consumer = CreatePromptStep();
        consumer.Key = "review-architecture-design";
        consumer.Title = "Review .NET architecture design";
        consumer.ArtifactInputs =
        [
            new ProcessTemplateDefinitionArtifactInputDocument
            {
                SourceStepKey = "draft-architecture-design",
                ArtifactExpectationKey = "dotnet-architecture-design"
            }
        ];
        var definition = new ProcessTemplateDefinitionDocument
        {
            Key = "dotnet-architecture-design-review",
            DisplayName = ".NET architecture design and review",
            Summary = "Design and review architecture.",
            Steps = [producer, consumer]
        };
        var prompt = BuildStepPrompt(
            runId,
            consumer,
            definition,
            requiredSlots: [designSlot],
            producedSlots: [],
            artifactSlotByStepExpectation: new Dictionary<(string StepKey, string ExpectationKey), ArtifactSlotId>
            {
                [("draft-architecture-design", "dotnet-architecture-design")] = designSlot
            });

        Assert.Contains("Producer step: draft-architecture-design - Draft .NET architecture design", prompt, StringComparison.Ordinal);
        Assert.Contains("Artifact expectation: dotnet-architecture-design - .NET architecture design draft (Decision)", prompt, StringComparison.Ordinal);
        Assert.Contains("artifacts/process-runs/d9450dd1-4920-457c-92a4-48d1ec648181/steps/draft-architecture-design.md", prompt, StringComparison.Ordinal);
        Assert.Contains("Do not block only because a slot-id directory is absent", prompt, StringComparison.Ordinal);
        Assert.Contains("Must describe boundaries and testability seams.", prompt, StringComparison.Ordinal);
    }

    private static string BuildStepPrompt(
        ProcessRunId runId,
        ProcessTemplateDefinitionStepDocument? step = null,
        ProcessTemplateDefinitionDocument? definition = null,
        IReadOnlyList<ArtifactSlotId>? requiredSlots = null,
        IReadOnlyList<ArtifactSlotId>? producedSlots = null,
        IReadOnlyDictionary<(string StepKey, string ExpectationKey), ArtifactSlotId>? artifactSlotByStepExpectation = null)
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
        step ??= CreatePromptStep();
        definition ??= new ProcessTemplateDefinitionDocument
        {
            Key = "blazor-app-delivery",
            DisplayName = "Blazor app delivery",
            Summary = "Deliver a Blazor app."
        };
        var selection = CreateSelection(definition);
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
                requiredSlots ?? Array.Empty<ArtifactSlotId>(),
                producedSlots ?? new[] { ArtifactSlotId.New() },
                artifactSlotByStepExpectation ?? new Dictionary<(string StepKey, string ExpectationKey), ArtifactSlotId>(),
                runId
            ]));
    }

    private static ProcessTemplateDefinitionStepDocument CreatePromptStep()
    {
        return new ProcessTemplateDefinitionStepDocument
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
