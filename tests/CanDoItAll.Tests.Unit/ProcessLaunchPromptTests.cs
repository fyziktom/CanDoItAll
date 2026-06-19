using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Templates;

namespace CanDoItAll.Tests.Unit;

public sealed class ProcessLaunchPromptTests
{
    [Fact]
    public void Generic_step_brief_includes_runtime_context_without_agent_or_project_guidance()
    {
        var runId = new ProcessRunId(Guid.Parse("d9450dd1-4920-457c-92a4-48d1ec648181"));
        var prompt = BuildStepPrompt(new GenericProcessStepBriefBuilder(), runId);

        Assert.Contains("Process run id: d9450dd1-4920-457c-92a4-48d1ec648181", prompt, StringComparison.Ordinal);
        Assert.Contains("Managed artifact root: artifacts/process-runs/d9450dd1-4920-457c-92a4-48d1ec648181", prompt, StringComparison.Ordinal);
        Assert.Contains("Managed artifact path rule: paths under the managed artifact root are workspace-managed relative refs.", prompt, StringComparison.Ordinal);
        Assert.Contains("never prefix them with external-target aliases or absolute output roots", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("Project id:", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("Project node id:", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("process_step_outcome_result", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("project_structure_process_subprocess_launch", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("Do not write evidence under output/", prompt, StringComparison.Ordinal);
        Assert.Contains("Write refs (choose at least one concrete managed ref for this slot):", prompt, StringComparison.Ordinal);
        Assert.Contains("include each written concrete artifact ref in evidenceRefs before returning Completed", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void Generic_step_brief_handles_non_software_process_domains_without_runtime_domain_leaks()
    {
        var scenarios = new[]
        {
            ("business-market-sizing", "Business market sizing", "Estimate market segments and buying triggers."),
            ("claims-quality-review", "Claims quality review", "Review sampled claims for policy quality issues."),
            ("multistep-data-analysis", "Multistep data analysis", "Clean, enrich, and summarize quarterly operating data."),
            ("marketing-campaign-planning", "Marketing campaign planning", "Plan channels, offers, and launch calendar.")
        };

        foreach (var (definitionKey, displayName, notes) in scenarios)
        {
            var definition = CreateDefinition(definitionKey, displayName, notes);
            var prompt = BuildStepPrompt(
                new GenericProcessStepBriefBuilder(),
                ProcessRunId.New(),
                CreatePromptStep(notes),
                definition,
                variables: new Dictionary<string, string>
                {
                    ["SourceData"] = $"{definitionKey}.csv"
                });

            Assert.Contains(displayName, prompt, StringComparison.Ordinal);
            Assert.Contains(notes, prompt, StringComparison.Ordinal);
            Assert.DoesNotContain("Blazor", prompt, StringComparison.Ordinal);
            Assert.DoesNotContain(".NET", prompt, StringComparison.Ordinal);
            Assert.DoesNotContain("RepositoryRoot", prompt, StringComparison.Ordinal);
            Assert.DoesNotContain("BranchName", prompt, StringComparison.Ordinal);
            Assert.DoesNotContain("project_structure_process_subprocess_launch", prompt, StringComparison.Ordinal);
            Assert.DoesNotContain("process_step_outcome_result", prompt, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void AgentFramework_step_brief_keeps_project_structure_guidance_outside_generic_application_layer()
    {
        var runId = new ProcessRunId(Guid.Parse("d9450dd1-4920-457c-92a4-48d1ec648181"));
        var step = CreatePromptStep("Resolve product and evidence paths.");
        step.Key = "architecture-review";
        step.Title = ".NET architecture review";
        step.StepKind = ProcessTemplateStepKinds.Subprocess;
        step.SubprocessProcessKey = "dotnet-architecture-design-review";
        step.SubprocessDefinitionSnapshotName = ".NET architecture design and review subprocess";
        step.AllowedOperations =
        [
            ProcessOperationContractNames.ReadProjectStructure,
            ProcessOperationContractNames.ExecuteExternalAction
        ];

        var prompt = BuildStepPrompt(new AgentFrameworkProcessStepBriefBuilder(), runId, step);

        Assert.Contains("Step kind: Subprocess", prompt, StringComparison.Ordinal);
        Assert.Contains("Project id: 3324868f-66e2-478a-bb8f-14f32a5db1e9", prompt, StringComparison.Ordinal);
        Assert.Contains("Project node id: custom:bd8169fc3fa944dbafd13998fb167fe8", prompt, StringComparison.Ordinal);
        Assert.Contains("AgentFramework execution contract:", prompt, StringComparison.Ordinal);
        Assert.Contains("Return only JSON matching the process_step_outcome_result structured output contract.", prompt, StringComparison.Ordinal);
        Assert.Contains("Governed launch tool: project_structure_process_subprocess_launch", prompt, StringComparison.Ordinal);
        Assert.Contains("Do not mark Completed until the child run receipt", prompt, StringComparison.Ordinal);
        Assert.Contains("Stopped-child rule:", prompt, StringComparison.Ordinal);
        Assert.Contains("Do not return Blocked only because a stopped child run exists", prompt, StringComparison.Ordinal);
        Assert.Contains("leave LiveRunProfileKey empty", prompt, StringComparison.Ordinal);
        Assert.Contains("BranchName, RepositoryRoot, SessionId", prompt, StringComparison.Ordinal);
        Assert.Contains("ChildManagedArtifactRoot", prompt, StringComparison.Ordinal);
        Assert.Contains("Treat artifacts under ChildManagedArtifactRoot as the child evidence bundle", prompt, StringComparison.Ordinal);
        Assert.Contains("ExpectedChildEvidenceRefs are preferred lookup candidates", prompt, StringComparison.Ordinal);
        Assert.Contains("Managed artifact refs are workspace-managed relative paths", prompt, StringComparison.Ordinal);
        Assert.Contains("keep the managed relative ref in evidenceRefs", prompt, StringComparison.Ordinal);
        Assert.Contains("never convert them to external-target paths", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void Generic_step_brief_maps_required_upstream_slots_to_producer_artifact_paths()
    {
        var runId = new ProcessRunId(Guid.Parse("d9450dd1-4920-457c-92a4-48d1ec648181"));
        var designSlot = new ArtifactSlotId(Guid.Parse("0dd0f0c0-4224-faba-36b0-6df9286b51b3"));
        var producer = CreatePromptStep("Draft architecture design.");
        producer.Key = "draft-architecture-design";
        producer.Title = "Draft architecture design";
        producer.ArtifactExpectations =
        [
            new ProcessTemplateDefinitionArtifactExpectationDocument
            {
                Key = "architecture-design",
                Title = "Architecture design draft",
                ArtifactKind = "Decision",
                IsRequired = true,
                ValidationRequirementSummary = "Must describe boundaries and testability."
            }
        ];
        var consumer = CreatePromptStep("Review architecture design.");
        consumer.Key = "review-architecture-design";
        consumer.Title = "Review architecture design";
        consumer.ArtifactInputs =
        [
            new ProcessTemplateDefinitionArtifactInputDocument
            {
                SourceStepKey = "draft-architecture-design",
                ArtifactExpectationKey = "architecture-design"
            }
        ];
        var definition = new ProcessTemplateDefinitionDocument
        {
            Key = "architecture-design-review",
            DisplayName = "Architecture design and review",
            Summary = "Design and review architecture.",
            Steps = [producer, consumer]
        };
        var prompt = BuildStepPrompt(
            new GenericProcessStepBriefBuilder(),
            runId,
            consumer,
            definition,
            requiredSlots: [designSlot],
            producedSlots: [],
            artifactSlotByStepExpectation: new Dictionary<(string StepKey, string ExpectationKey), ArtifactSlotId>
            {
                [("draft-architecture-design", "architecture-design")] = designSlot
            });

        Assert.Contains("Producer step: draft-architecture-design - Draft architecture design", prompt, StringComparison.Ordinal);
        Assert.Contains("Artifact expectation: architecture-design - Architecture design draft (Decision)", prompt, StringComparison.Ordinal);
        Assert.Contains("Artifact refs to inspect (alternatives for this same slot):", prompt, StringComparison.Ordinal);
        Assert.Contains("artifacts/process-runs/d9450dd1-4920-457c-92a4-48d1ec648181/steps/draft-architecture-design.md", prompt, StringComparison.Ordinal);
        Assert.Contains("Use the first existing readable ref for this slot", prompt, StringComparison.Ordinal);
        Assert.Contains("Must describe boundaries and testability.", prompt, StringComparison.Ordinal);
    }

    private static string BuildStepPrompt(
        IProcessStepBriefBuilder builder,
        ProcessRunId runId,
        ProcessTemplateDefinitionStepDocument? step = null,
        ProcessTemplateDefinitionDocument? definition = null,
        IReadOnlyList<ArtifactSlotId>? requiredSlots = null,
        IReadOnlyList<ArtifactSlotId>? producedSlots = null,
        IReadOnlyDictionary<(string StepKey, string ExpectationKey), ArtifactSlotId>? artifactSlotByStepExpectation = null,
        IReadOnlyDictionary<string, string>? variables = null)
    {
        var request = new ProcessLaunchRequest(
            DefinitionKey: "blazor-app-delivery",
            ProcessDefinitionId: null,
            LiveRunProfileKey: null,
            ProjectId: Guid.Parse("3324868f-66e2-478a-bb8f-14f32a5db1e9"),
            ProjectNodeId: "custom:bd8169fc3fa944dbafd13998fb167fe8",
            RequestedBy: "codex-process-e2e",
            Variables: variables ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            RunReadiness: true,
            Execute: true);
        step ??= CreatePromptStep("Resolve product and evidence paths.");
        definition ??= CreateDefinition("blazor-app-delivery", "Blazor app delivery", "Deliver a Blazor app.");

        return builder.Build(new ProcessStepBriefBuildRequest(
            request,
            definition,
            step,
            ExecutorBinding: null,
            requiredSlots ?? Array.Empty<ArtifactSlotId>(),
            producedSlots ?? new[] { ArtifactSlotId.New() },
            artifactSlotByStepExpectation ?? new Dictionary<(string StepKey, string ExpectationKey), ArtifactSlotId>(),
            runId,
            ProcessLaunchApplicationService.BuildManagedProcessArtifactRoot(runId),
            variables ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)));
    }

    private static ProcessTemplateDefinitionStepDocument CreatePromptStep(string notes)
    {
        return new ProcessTemplateDefinitionStepDocument
        {
            Key = "resolve-contract",
            Title = "Resolve process contract",
            Notes = notes,
            InputContractSummary = "Use supplied process inputs.",
            OutputContractSummary = "Produce the requested process output.",
            EvidenceContractSummary = "Record durable completion evidence.",
            AllowedOperations =
            [
                "read-inputs",
                "write-managed-artifacts"
            ],
            OperationTargetScope = "managed-process-artifacts"
        };
    }

    private static ProcessTemplateDefinitionDocument CreateDefinition(
        string definitionKey,
        string displayName,
        string summary)
    {
        return new ProcessTemplateDefinitionDocument
        {
            Key = definitionKey,
            DisplayName = displayName,
            Summary = summary
        };
    }
}
