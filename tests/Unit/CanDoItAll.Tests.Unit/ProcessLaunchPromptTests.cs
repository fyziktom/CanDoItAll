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
        Assert.Contains("Primary write ref: artifacts/process-runs/d9450dd1-4920-457c-92a4-48d1ec648181/steps", prompt, StringComparison.Ordinal);
        Assert.Contains("first workspace mutation for this slot must create the primary write ref with workspace_write_file or workspace_append_file", prompt, StringComparison.Ordinal);
        Assert.Contains("Do not list, search, stat, or read this run's managed artifact root to discover your own missing output before that write", prompt, StringComparison.Ordinal);
        Assert.Contains("Absence of your own output before you write it is expected and is not a blocker", prompt, StringComparison.Ordinal);
        Assert.Contains("consolidate this slot into the primary managed ref first and include that exact primary ref in evidenceRefs before returning Completed", prompt, StringComparison.Ordinal);
        Assert.Contains("write this primary managed ref next instead of returning a generic no-prior-evidence blocker", prompt, StringComparison.Ordinal);
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
        Assert.Contains("This is a tool-backed process step, not a chat-only response", prompt, StringComparison.Ordinal);
        Assert.Contains("Only after the required evidence exists", prompt, StringComparison.Ordinal);
        Assert.Contains("Governed launch tool: project_structure_process_subprocess_launch", prompt, StringComparison.Ordinal);
        Assert.Contains("Do not mark Completed until the child run receipt", prompt, StringComparison.Ordinal);
        Assert.Contains("Stopped-child rule:", prompt, StringComparison.Ordinal);
        Assert.Contains("Do not return Blocked only because a stopped child run exists", prompt, StringComparison.Ordinal);
        Assert.Contains("Do not relaunch a Blocked child or a child with escalation/no-go evidence", prompt, StringComparison.Ordinal);
        Assert.Contains("leave LiveRunProfileKey empty", prompt, StringComparison.Ordinal);
        Assert.Contains("BranchName, RepositoryRoot, SessionId", prompt, StringComparison.Ordinal);
        Assert.Contains("ChildManagedArtifactRoot", prompt, StringComparison.Ordinal);
        Assert.Contains("ParentDeferredOutcomeJson", prompt, StringComparison.Ordinal);
        Assert.Contains("when the launch tool result has RunId and Stage Running, call submit_process_step_outcome with ParentDeferredOutcomeJson exactly", prompt, StringComparison.Ordinal);
        Assert.Contains("the process runtime will defer the parent step until the child run stops", prompt, StringComparison.Ordinal);
        Assert.Contains("Treat artifacts under ChildManagedArtifactRoot as the child evidence bundle", prompt, StringComparison.Ordinal);
        Assert.Contains("ExpectedChildEvidenceRefs are preferred lookup candidates after the child run is stopped", prompt, StringComparison.Ordinal);
        Assert.Contains("Managed artifact refs are workspace-managed relative paths", prompt, StringComparison.Ordinal);
        Assert.Contains("keep the managed relative ref in evidenceRefs", prompt, StringComparison.Ordinal);
        Assert.Contains("never convert them to external-target paths", prompt, StringComparison.Ordinal);
        Assert.Contains("Project-structure evidence hygiene:", prompt, StringComparison.Ordinal);
        Assert.Contains("Do not create project-structure nodes for every subprocess, intermediate screenshot, log, or step detail", prompt, StringComparison.Ordinal);
        Assert.Contains("one run-app proof node, one run-tests proof node, and one manager summary node", prompt, StringComparison.Ordinal);
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
        Assert.Contains("Use workspace_stat_path or workspace_read_file on the listed refs", prompt, StringComparison.Ordinal);
        Assert.Contains("use the first existing readable ref for this slot", prompt, StringComparison.Ordinal);
        Assert.Contains("Project structure is supplemental context, not a substitute", prompt, StringComparison.Ordinal);
        Assert.Contains("A successful stat or read of a listed current-run ref is process evidence for this step", prompt, StringComparison.Ordinal);
        Assert.Contains("do not return Blocked claiming no prior assistant text, tool result, or process artifact evidence after that", prompt, StringComparison.Ordinal);
        Assert.Contains("cite the failed workspace file-tool receipt before returning Blocked", prompt, StringComparison.Ordinal);
        Assert.Contains("Must describe boundaries and testability.", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void AgentFramework_step_brief_requires_workspace_file_probe_before_project_structure_fallback()
    {
        var runId = new ProcessRunId(Guid.Parse("d9450dd1-4920-457c-92a4-48d1ec648181"));
        var designSlot = new ArtifactSlotId(Guid.Parse("0dd0f0c0-4224-faba-36b0-6df9286b51b3"));
        var producer = CreatePromptStep("Draft scope packet.");
        producer.Key = "feature-intake";
        producer.Title = "Feature intake";
        producer.ArtifactExpectations =
        [
            new ProcessTemplateDefinitionArtifactExpectationDocument
            {
                Key = "scope-boundary-packet",
                Title = "Scope boundary packet",
                ArtifactKind = "Brief",
                IsRequired = true,
                ValidationRequirementSummary = "Must describe requested scope."
            }
        ];
        var consumer = CreatePromptStep("Coordinate downstream work.");
        consumer.Key = "architecture-review";
        consumer.Title = "Architecture review";
        consumer.StepKind = ProcessTemplateStepKinds.Subprocess;
        consumer.SubprocessProcessKey = "architecture-design-review";
        consumer.DependsOnStepKey = "feature-intake";
        consumer.Dependencies =
        [
            new ProcessTemplateDefinitionStepDependencyDocument
            {
                DependsOnStepKey = "feature-intake"
            }
        ];
        consumer.AllowedOperations =
        [
            ProcessOperationContractNames.ReadProjectStructure,
            ProcessOperationContractNames.ReadUpstreamArtifacts,
            ProcessOperationContractNames.ExecuteExternalAction
        ];
        consumer.ArtifactInputs =
        [
            new ProcessTemplateDefinitionArtifactInputDocument
            {
                SourceStepKey = "feature-intake",
                ArtifactExpectationKey = "scope-boundary-packet"
            }
        ];
        var definition = new ProcessTemplateDefinitionDocument
        {
            Key = "software-delivery",
            DisplayName = "Software delivery",
            Summary = "Deliver software.",
            Steps = [producer, consumer]
        };

        var prompt = BuildStepPrompt(
            new AgentFrameworkProcessStepBriefBuilder(),
            runId,
            consumer,
            definition,
            requiredSlots: [designSlot],
            producedSlots: [],
            artifactSlotByStepExpectation: new Dictionary<(string StepKey, string ExpectationKey), ArtifactSlotId>
            {
                [("feature-intake", "scope-boundary-packet")] = designSlot
            });

        Assert.Contains("AgentFramework upstream artifact read rule:", prompt, StringComparison.Ordinal);
        Assert.Contains("AgentFramework dependency artifact refs:", prompt, StringComparison.Ordinal);
        Assert.Contains("Dependency step: feature-intake - Feature intake", prompt, StringComparison.Ordinal);
        Assert.Contains("Primary completed-step artifact ref: artifacts/process-runs/d9450dd1-4920-457c-92a4-48d1ec648181/steps/feature-intake.md", prompt, StringComparison.Ordinal);
        Assert.Contains("call workspace_stat_path or workspace_read_file on those exact refs before using project-structure hierarchy as fallback context", prompt, StringComparison.Ordinal);
        Assert.Contains("upstream process artifacts are read through workspace file tools", prompt, StringComparison.Ordinal);
        Assert.Contains("Do not abbreviate, ellipsize, shorten, or guess managed refs", prompt, StringComparison.Ordinal);
        Assert.Contains("current failed workspace file-tool receipt", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void AgentFramework_step_brief_uses_launch_variables_as_project_structure_context_instead_of_invented_snapshot_file()
    {
        var runId = new ProcessRunId(Guid.Parse("d9450dd1-4920-457c-92a4-48d1ec648181"));
        var step = CreatePromptStep("Classify app type from current process context.");
        step.Key = "classify-dotnet-application";
        step.Title = "Classify .NET application";
        step.AllowedOperations =
        [
            ProcessOperationContractNames.ReadProjectStructure,
            ProcessOperationContractNames.ReadUpstreamArtifacts,
            ProcessOperationContractNames.WriteManagedProcessArtifacts
        ];

        var prompt = BuildStepPrompt(
            new AgentFrameworkProcessStepBriefBuilder(),
            runId,
            step,
            variables: new Dictionary<string, string>
            {
                ["ProjectStructureContextSummary"] = """
                Project structure source: TetrisGame. Selected node: Delivery scope.
                Visual target assets:
                - Application layout proposal (custom:image1) [ImageAsset/generated; image/png; media=managed-files/project-media/images/tetris/proposal.png; file=proposal.png; parent=custom:architecture]: target look.
                Visual target rule: implementation and QA must fetch or analyze the relevant asset content before accepting visual alignment.
                """,
                ["DotNetScaffoldContract"] = "AppArchetype: Blazor WebAssembly PWA",
                ["ProductRoot"] = @"C:\programovani\dotnet\output",
                ["ParentProcessRunId"] = "f22fa5af-9cad-44fb-a56d-e5d5d1eeae4d",
                ["SubprocessDefinitionKey"] = "dotnet-architecture-design-review"
            });

        Assert.Contains("AgentFramework project-structure context source:", prompt, StringComparison.Ordinal);
        Assert.Contains("ProjectStructureContextSummary in Launch variables is the current project-structure context for this run", prompt, StringComparison.Ordinal);
        Assert.Contains("Ignore generated process evidence from prior runs", prompt, StringComparison.Ordinal);
        Assert.Contains("DotNetScaffoldContract and DotNet* launch variables are typed project-structure facts", prompt, StringComparison.Ordinal);
        Assert.Contains("ProductRoot, OutputRoot, and ExternalTargetRoot launch variables identify the product target", prompt, StringComparison.Ordinal);
        Assert.Contains("Grounded external-target aliases for structured workspace tool path arguments: external-target/C/programovani/dotnet/output", prompt, StringComparison.Ordinal);
        Assert.Contains("The project-structure context lists visual target assets", prompt, StringComparison.Ordinal);
        Assert.Contains("compare the delivered screenshot against that visual target", prompt, StringComparison.Ordinal);
        Assert.Contains("Do not accept visual quality from generated app screenshots in isolation", prompt, StringComparison.Ordinal);
        Assert.Contains("Do not call workspace_read_file", prompt, StringComparison.Ordinal);
        Assert.Contains("with native absolute ProductRoot or OutputRoot paths", prompt, StringComparison.Ordinal);
        Assert.Contains("retry the same structured workspace tool with that alias before returning Blocked", prompt, StringComparison.Ordinal);
        Assert.Contains("parent launch variables are copied into the child run", prompt, StringComparison.Ordinal);
        Assert.Contains("Do not call workspace_read_file on artifacts/process-runs/d9450dd1-4920-457c-92a4-48d1ec648181/project-structure.json", prompt, StringComparison.Ordinal);
        Assert.Contains("Project-structure context is not materialized as a managed JSON file by default", prompt, StringComparison.Ordinal);
        Assert.Contains("write the relevant facts into the step's primary managed artifact", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("Tetris app", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void AgentFramework_step_brief_lists_dependency_artifact_refs_without_slot_mapping()
    {
        var runId = new ProcessRunId(Guid.Parse("d9450dd1-4920-457c-92a4-48d1ec648181"));
        var producer = CreatePromptStep("Draft scope packet.");
        producer.Key = "feature-intake";
        producer.Title = "Feature intake";
        var consumer = CreatePromptStep("Coordinate downstream work.");
        consumer.Key = "architecture-review";
        consumer.Title = "Architecture review";
        consumer.DependsOnStepKey = "feature-intake";
        consumer.Dependencies =
        [
            new ProcessTemplateDefinitionStepDependencyDocument
            {
                DependsOnStepKey = "feature-intake"
            }
        ];
        var definition = new ProcessTemplateDefinitionDocument
        {
            Key = "software-delivery",
            DisplayName = "Software delivery",
            Summary = "Deliver software.",
            Steps = [producer, consumer]
        };

        var prompt = BuildStepPrompt(
            new AgentFrameworkProcessStepBriefBuilder(),
            runId,
            consumer,
            definition,
            requiredSlots: [],
            producedSlots: [],
            artifactSlotByStepExpectation: new Dictionary<(string StepKey, string ExpectationKey), ArtifactSlotId>());

        Assert.Contains("Required upstream artifact slots:", prompt, StringComparison.Ordinal);
        Assert.Contains("No required upstream artifact slots.", prompt, StringComparison.Ordinal);
        Assert.Contains("AgentFramework dependency artifact refs:", prompt, StringComparison.Ordinal);
        Assert.Contains("Dependency step: feature-intake - Feature intake", prompt, StringComparison.Ordinal);
        Assert.Contains("Primary completed-step artifact ref: artifacts/process-runs/d9450dd1-4920-457c-92a4-48d1ec648181/steps/feature-intake.md", prompt, StringComparison.Ordinal);
        Assert.Contains("before listing, searching, or using project-structure fallback context", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("artifacts/process-r...", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void Step_brief_treats_artifact_expectation_keys_as_labels_not_filenames()
    {
        var runId = new ProcessRunId(Guid.Parse("d9450dd1-4920-457c-92a4-48d1ec648181"));
        var producer = CreatePromptStep("Draft feature intake packet.");
        producer.Key = "feature-slice-intake";
        producer.Title = "Feature slice intake";
        producer.ArtifactExpectations =
        [
            new ProcessTemplateDefinitionArtifactExpectationDocument
            {
                Key = "feature-scope-packet",
                Title = "Feature scope packet",
                ArtifactKind = "Evidence",
                IsRequired = true
            },
            new ProcessTemplateDefinitionArtifactExpectationDocument
            {
                Key = "feature-acceptance-criteria",
                Title = "Feature acceptance criteria",
                ArtifactKind = "Evidence",
                IsRequired = true
            }
        ];
        var consumer = CreatePromptStep("Plan implementation.");
        consumer.Key = "implementation-approach";
        consumer.Title = "Implementation approach";
        consumer.ArtifactInputs =
        [
            new ProcessTemplateDefinitionArtifactInputDocument
            {
                ArtifactExpectationKey = "feature-scope-packet",
                SourceStepKey = "feature-slice-intake"
            },
            new ProcessTemplateDefinitionArtifactInputDocument
            {
                ArtifactExpectationKey = "feature-acceptance-criteria",
                SourceStepKey = "feature-slice-intake"
            }
        ];
        var scopeSlot = ArtifactSlotId.New();
        var acceptanceSlot = ArtifactSlotId.New();
        var definition = new ProcessTemplateDefinitionDocument
        {
            Key = "dotnet-feature-function-implementation",
            DisplayName = ".NET feature function implementation",
            Summary = "Implement feature.",
            Steps = [producer, consumer]
        };

        var prompt = BuildStepPrompt(
            new GenericProcessStepBriefBuilder(),
            runId,
            consumer,
            definition,
            requiredSlots: [scopeSlot, acceptanceSlot],
            producedSlots: [],
            artifactSlotByStepExpectation: new Dictionary<(string StepKey, string ExpectationKey), ArtifactSlotId>
            {
                [("feature-slice-intake", "feature-scope-packet")] = scopeSlot,
                [("feature-slice-intake", "feature-acceptance-criteria")] = acceptanceSlot
            });

        Assert.Contains("Expectation key rule:", prompt, StringComparison.Ordinal);
        Assert.Contains("the artifact expectation key is a contract label, not a filename", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Do not invent a managed file named artifacts/process-runs/d9450dd1-4920-457c-92a4-48d1ec648181/steps/feature-acceptance-criteria.md", prompt, StringComparison.Ordinal);
        Assert.Contains("its primary completed-step artifact ref can satisfy each slot when it is readable", prompt, StringComparison.Ordinal);
        Assert.Contains("artifacts/process-runs/d9450dd1-4920-457c-92a4-48d1ec648181/steps/feature-slice-intake.md", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void AgentFramework_step_brief_requires_evidence_tools_before_finalizer()
    {
        var runId = new ProcessRunId(Guid.Parse("d9450dd1-4920-457c-92a4-48d1ec648181"));
        var producedSlot = new ArtifactSlotId(Guid.Parse("0dd0f0c0-4224-faba-36b0-6df9286b51b3"));
        var step = CreatePromptStep("Clarify scope and write a scope packet.");
        step.Key = "feature-intake";
        step.Title = "Feature intake";
        step.AllowedOperations =
        [
            ProcessOperationContractNames.ReadProjectStructure,
            ProcessOperationContractNames.WriteManagedProcessArtifacts
        ];
        step.ArtifactExpectations =
        [
            new ProcessTemplateDefinitionArtifactExpectationDocument
            {
                Key = "scope-boundary-packet",
                Title = "Scope boundary packet",
                ArtifactKind = "Brief",
                IsRequired = true,
                ValidationRequirementSummary = "Must describe requested scope."
            }
        ];
        var definition = new ProcessTemplateDefinitionDocument
        {
            Key = "software-delivery",
            DisplayName = "Software delivery",
            Summary = "Deliver software.",
            Steps = [step]
        };

        var prompt = BuildStepPrompt(
            new AgentFrameworkProcessStepBriefBuilder(),
            runId,
            step,
            definition,
            requiredSlots: [],
            producedSlots: [producedSlot],
            artifactSlotByStepExpectation: new Dictionary<(string StepKey, string ExpectationKey), ArtifactSlotId>
            {
                [("feature-intake", "scope-boundary-packet")] = producedSlot
            });

        Assert.Contains("This is a tool-backed process step, not a chat-only response", prompt, StringComparison.Ordinal);
        Assert.Contains("Only after the required evidence exists", prompt, StringComparison.Ordinal);
        Assert.Contains("Primary write ref: artifacts/process-runs/d9450dd1-4920-457c-92a4-48d1ec648181/steps/feature-intake.md", prompt, StringComparison.Ordinal);
        Assert.Contains("first workspace mutation for this slot must create the primary write ref with workspace_write_file or workspace_append_file", prompt, StringComparison.Ordinal);
        Assert.Contains("the first workspace mutation for that produced output must be workspace_write_file or workspace_append_file to the listed Primary write ref", prompt, StringComparison.Ordinal);
        Assert.Contains("AgentFramework own-output bootstrap:", prompt, StringComparison.Ordinal);
        Assert.Contains("This step has produced artifact slots and no required upstream artifact slots.", prompt, StringComparison.Ordinal);
        Assert.Contains("Do not return Blocked for missing upstream artifacts, insufficient evidence, missing prior logs, or absent screenshots before creating your own managed artifact.", prompt, StringComparison.Ordinal);
        Assert.Contains("Primary own-output write ref: artifacts/process-runs/d9450dd1-4920-457c-92a4-48d1ec648181/steps/feature-intake.md", prompt, StringComparison.Ordinal);
        Assert.Contains("return Completed with evidenceRefs containing the exact primary own-output write ref", prompt, StringComparison.Ordinal);
        Assert.Contains("Do not read or stat ProductRoot, OutputRoot, ExternalTargetRoot, or their external-target aliases looking for a same-named own-output packet before writing this managed artifact", prompt, StringComparison.Ordinal);
        Assert.Contains("own process outputs are generated under managed artifact refs", prompt, StringComparison.Ordinal);
        Assert.Contains("Do not require build, test, runtime, screenshot, deployment, approval, or downstream handoff evidence that belongs to later steps", prompt, StringComparison.Ordinal);
        Assert.Contains("Blocked is valid only when you cannot create the primary managed artifact", prompt, StringComparison.Ordinal);
        Assert.Contains("write a managed Markdown artifact with assumptions and known gaps instead of blocking on optional context", prompt, StringComparison.Ordinal);
        Assert.Contains("Do not finalize Completed with an empty evidenceRefs array", prompt, StringComparison.Ordinal);
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
