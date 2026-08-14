using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Processes.Templates;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Unit;

[Trait("Category", "UnixRuntimePortability")]
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
        Assert.Contains("Decision rights:", prompt, StringComparison.Ordinal);
        Assert.Contains("Select an evidence-backed outcome within the assigned step boundary.", prompt, StringComparison.Ordinal);
        Assert.Contains("Exception policy:", prompt, StringComparison.Ordinal);
        Assert.Contains("Escalate only a concrete access, policy, environment, or contract boundary.", prompt, StringComparison.Ordinal);
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
    public void Driver_step_brief_builder_uses_replaceable_prompt_composition_driver()
    {
        var builder = new DriverProcessStepBriefBuilder(
        [
            new FakePromptCompositionDriver("business-analysis-driver")
        ]);

        var prompt = BuildStepPrompt(
            builder,
            ProcessRunId.New(),
            CreatePromptStep("Prepare supplier risk analysis."),
            CreateDefinition("supplier-risk-review", "Supplier risk review", "Review supplier risk."),
            variables: new Dictionary<string, string>
            {
                ["SupplierPortfolio"] = "critical-vendors"
            });

        Assert.Equal("fake prompt from business-analysis-driver for supplier-risk-review/resolve-contract", prompt);
    }

    [Fact]
    public void Generic_step_brief_bounds_large_launch_variable_values()
    {
        var runId = new ProcessRunId(Guid.Parse("d9450dd1-4920-457c-92a4-48d1ec648181"));
        var largeValue = string.Concat(
            "important-prefix",
            new string('x', 5000),
            "important-suffix");
        var prompt = BuildStepPrompt(
            new GenericProcessStepBriefBuilder(),
            runId,
            variables: new Dictionary<string, string>
            {
                ["LargeContext"] = largeValue
            });

        Assert.Contains("LargeContext", prompt, StringComparison.Ordinal);
        Assert.Contains("important-prefix", prompt, StringComparison.Ordinal);
        Assert.Contains("important-suffix", prompt, StringComparison.Ordinal);
        Assert.Contains("launch variable truncated", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain(new string('x', 3000), prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void Generic_step_brief_omits_oversized_typed_contract_atomically()
    {
        var contract = JsonSerializer.Serialize(new
        {
            schema = "example.large/v1",
            payload = new string('x', 9000)
        });
        var prompt = BuildStepPrompt(
            new GenericProcessStepBriefBuilder(),
            ProcessRunId.New(),
            variables: new Dictionary<string, string>
            {
                ["ExampleContract"] = contract
            });

        Assert.Contains(
            "typed launch contract 'ExampleContract' omitted atomically",
            prompt,
            StringComparison.Ordinal);
        Assert.DoesNotContain("\"schema\":\"example.large/v1\"", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("launch variable truncated", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void Generic_step_brief_renders_resolved_template_execution_guidance()
    {
        var step = CreatePromptStep("Review the supplied evidence.");
        step.ResolvedExecutionGuidance =
        [
            new ProcessTemplateExecutionGuidanceDocument(
                "processes/example/steps/review.md",
                "Rework an observed product defect before collecting another proof artifact.",
                "sha256:guidance")
        ];

        var prompt = BuildStepPrompt(
            new GenericProcessStepBriefBuilder(),
            ProcessRunId.New(),
            step);

        Assert.Contains("Template execution guidance:", prompt, StringComparison.Ordinal);
        Assert.Contains("processes/example/steps/review.md (sha256:guidance)", prompt, StringComparison.Ordinal);
        Assert.Contains("Rework an observed product defect before collecting another proof artifact.", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void Generic_step_brief_requires_current_run_managed_browser_evidence()
    {
        var runId = ProcessRunId.New();

        var prompt = BuildStepPrompt(
            new GenericProcessStepBriefBuilder(),
            runId);

        Assert.Contains(
            $"artifacts/process-runs/{runId.Value:D}/browser/<evidence-name>.<ext>",
            prompt,
            StringComparison.Ordinal);
        Assert.Contains(
            "A bare filename is provider-native interaction state and cannot satisfy governed evidence.",
            prompt,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Software_delivery_qa_execution_guidance_is_pack_resolved_and_hashed()
    {
        var loader = new ProcessTemplatePackLoader();
        var definition = loader.LoadDefinition("software-delivery");
        var qaValidation = Assert.Single(definition.Steps, step =>
            string.Equals(step.Key, "qa-validation", StringComparison.Ordinal));

        var guidance = Assert.Single(qaValidation.ResolvedExecutionGuidance);

        Assert.Equal("processes/software-delivery/steps/qa-validation.md", guidance.Reference);
        Assert.StartsWith("sha256:", guidance.ContentHash, StringComparison.Ordinal);
        Assert.Contains("Select `repair-required` when the UI proof shows a concrete product defect", guidance.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void Generic_step_brief_preserves_full_acceptance_contract_and_hides_runtime_matrix()
    {
        var acceptanceContract = string.Join(
            Environment.NewLine,
            Enumerable.Range(1, 20).Select(index =>
                $"AC-{index:000}: Required observable behavior {index} {new string((char)('a' + index % 20), 120)} [proof=browser-proof]"));
        var prompt = BuildStepPrompt(
            new GenericProcessStepBriefBuilder(),
            ProcessRunId.New(),
            variables: new Dictionary<string, string>
            {
                [ProcessRuntimeLaunchVariables.ProductAcceptanceCriteriaContract] = acceptanceContract,
                [ProcessRuntimeLaunchVariables.AcceptanceCriteriaMatrix] = "internal-matrix-payload",
                [ProcessRuntimeLaunchVariables.AcceptanceCriteriaAcceptedBranchOutcomeKeys] = "quality-accepted"
            });

        Assert.Contains("AC-001", prompt, StringComparison.Ordinal);
        Assert.Contains("AC-020", prompt, StringComparison.Ordinal);
        Assert.Contains("acceptanceCriteriaEvidence", prompt, StringComparison.Ordinal);
        Assert.Contains(
            "inside the sole submit_process_step_outcome result object, never as a sibling argument",
            prompt,
            StringComparison.Ordinal);
        Assert.Contains("criterionId", prompt, StringComparison.Ordinal);
        Assert.Contains("status Passed", prompt, StringComparison.Ordinal);
        Assert.Contains("status Failed", prompt, StringComparison.Ordinal);
        Assert.Contains("summary", prompt, StringComparison.Ordinal);
        Assert.Contains("evidenceRefs", prompt, StringComparison.Ordinal);
        Assert.Contains("Do not substitute aliases such as id, passed, or proofRefs", prompt, StringComparison.Ordinal);
        Assert.Contains(
            "marked kind=ProductAcceptance and required=true",
            prompt,
            StringComparison.Ordinal);
        Assert.Contains(
            "Legacy criterion lines without kind/required markers default to required ProductAcceptance",
            prompt,
            StringComparison.Ordinal);
        Assert.Contains(
            "Preserve kind=DeliveryPlanning entries as nonblocking context",
            prompt,
            StringComparison.Ordinal);
        Assert.Contains(
            "do not submit Passed or Failed acceptance evidence for them",
            prompt,
            StringComparison.Ordinal);
        Assert.DoesNotContain("launch variable truncated", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("internal-matrix-payload", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("AcceptanceCriteriaAcceptedBranchOutcomeKeys", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void Setup_step_with_acceptance_contract_is_not_presented_as_final_acceptance_owner()
    {
        var prompt = BuildStepPrompt(
            new GenericProcessStepBriefBuilder(),
            ProcessRunId.New(),
            variables: new Dictionary<string, string>
            {
                [ProcessRuntimeLaunchVariables.ProductAcceptanceCriteriaContract] =
                    "AC-001: Product behavior is implemented. [proof=browser-proof]"
            });

        Assert.Contains(
            "This step contributes evidence to a later acceptance owner",
            prompt,
            StringComparison.Ordinal);
        Assert.Contains(
            "do not claim end-to-end acceptance solely from this step",
            prompt,
            StringComparison.Ordinal);
        Assert.DoesNotContain("For these final-acceptance branches only", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void Template_completion_policy_materializes_direct_step_contract_without_named_workflow_code()
    {
        var step = CreatePromptStep("Validate the result.");
        step.Key = "renamed-proof-step";
        step.CompletionPolicy = new ProcessTemplateStepCompletionPolicyDocument
        {
            RequiredProductToolReceipts =
            [
                new ProcessTemplateProductToolReceiptRequirementDocument
                {
                    Key = "renamed-proof-success",
                    ToolName = "workspace_validate",
                    Purpose = "AcceptanceProof",
                    EnforceBranchOutcomeKeys = ["accepted"],
                    Reason = "Current-run validation proof is required.",
                    AllowFailedExecutionReceipt = false
                },
                new ProcessTemplateProductToolReceiptRequirementDocument
                {
                    Key = "renamed-proof-attempt",
                    ToolName = "workspace_validate",
                    Purpose = "DefectEvidence",
                    EnforceBranchOutcomeKeys = ["repair-required"],
                    Reason = "Current-run failed validation is accepted as repair evidence.",
                    AllowFailedExecutionReceipt = true
                }
            ],
            CompletionIssueRoutes =
            [
                new ProcessTemplateCompletionIssueRouteDocument
                {
                    IssueCode = "process.adapter.required_tool_receipt_missing",
                    TargetBranchOutcomeKey = "repair-required",
                    TargetBranchOutcomeTitle = "Repair required"
                }
            ],
            AcceptanceCriteriaRequiredBranchOutcomeKeys = ["accepted"],
            RequiresProductSourceInspection = true,
            ProductSourceInspectionRequiredBranchOutcomeKeys = ["accepted"],
            ProductMutationRequiredBranchOutcomeKeys = ["repair-applied"],
            RequiresProductMutationBeforeManagedOutput = true,
            ProductMutationToolNames = ["workspace_write_file"],
            RuntimeRoutedBranchOutcomeKeys = ["repair-attempt-incomplete"]
        };
        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceipts] = "spoofed-receipt"
        };

        ProcessTemplateStepCompletionPolicyMaterializer.Apply(variables, step);

        Assert.Contains("workspace_validate", variables[ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceipts], StringComparison.Ordinal);
        Assert.Contains("renamed-proof-success", variables[ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceipts], StringComparison.Ordinal);
        Assert.Contains("renamed-proof-attempt", variables[ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceipts], StringComparison.Ordinal);
        Assert.Contains("allowFailedExecutionReceipt\":true", variables[ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceipts], StringComparison.Ordinal);
        Assert.DoesNotContain("spoofed-receipt", variables[ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceipts], StringComparison.Ordinal);
        Assert.Contains("repair-required", variables[ProcessRuntimeLaunchVariables.CompletionIssueRoutes], StringComparison.Ordinal);
        Assert.Equal(
            "[\"repair-applied\"]",
            variables[ProcessRuntimeLaunchVariables.ProductMutationRequiredBranchOutcomeKeys]);
        Assert.Equal(
            "[\"repair-attempt-incomplete\"]",
            variables[ProcessRuntimeLaunchVariables.RuntimeRoutedBranchOutcomeKeys]);
        Assert.Equal(
            "[\"accepted\"]",
            variables[ProcessRuntimeLaunchVariables.AcceptanceCriteriaAcceptedBranchOutcomeKeys]);
        variables[ProcessRuntimeLaunchVariables.ProductAcceptanceCriteriaContract] =
            "AC-001: Required behavior. [kind=ProductAcceptance; required=true; proof=planned-validation]";
        var prompt = BuildStepPrompt(
            new GenericProcessStepBriefBuilder(),
            ProcessRunId.New(),
            variables: variables);
        Assert.Contains(
            "For these final-acceptance branches only: accepted",
            prompt,
            StringComparison.Ordinal);
        Assert.Equal(
            "[\"renamed-proof-step\"]",
            variables[ProcessRuntimeLaunchVariables.ProductSourceInspectionRequiredStepKeys]);
        Assert.Contains(
            "renamed-proof-step",
            variables[ProcessRuntimeLaunchVariables.ProductSourceInspectionRequiredBranchOutcomeKeysByStep],
            StringComparison.Ordinal);
    }

    [Fact]
    public void Generic_step_brief_hides_orchestration_provenance_from_agent_task_context()
    {
        var prompt = BuildStepPrompt(
            new GenericProcessStepBriefBuilder(),
            ProcessRunId.New(),
            variables: new Dictionary<string, string>
            {
                ["BranchName"] = "memory-providers",
                ["RepositoryRoot"] = @"C:\repositories\CanDoItAll",
                ["AgentId"] = "codex-observer-manager",
                ["SessionId"] = "observer-session",
                ["ProductRootAlias"] = "external-target/C/programovani/dotnet/output"
            });

        Assert.Contains("ProductRootAlias: external-target/C/programovani/dotnet/output", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("memory-providers", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain(@"C:\repositories\CanDoItAll", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("codex-observer-manager", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("observer-session", prompt, StringComparison.Ordinal);
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
        Assert.Contains(
            "the runtime builds the typed manager packet from those records",
            prompt,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "Include the assigned agent name, step key, process run id",
            prompt,
            StringComparison.Ordinal);
        Assert.Contains("Do not put native absolute filesystem paths", prompt, StringComparison.Ordinal);
        Assert.Contains("scoped storage paths under artifacts/scopes", prompt, StringComparison.Ordinal);
        Assert.Contains("ignore that scoped echo in artifact prose and evidenceRefs", prompt, StringComparison.Ordinal);
        Assert.Contains("Governed launch tool: project_structure_process_subprocess_launch", prompt, StringComparison.Ordinal);
        Assert.Contains("Do not mark Completed until the child run receipt", prompt, StringComparison.Ordinal);
        Assert.Contains("Mandatory-launch rule:", prompt, StringComparison.Ordinal);
        Assert.Contains("your first non-read external action for this step must be project_structure_process_subprocess_launch", prompt, StringComparison.Ordinal);
        Assert.Contains("Parent-tool boundary rule:", prompt, StringComparison.Ordinal);
        Assert.Contains("direct child-work tools are not required in the parent subprocess step", prompt, StringComparison.Ordinal);
        Assert.Contains("Stopped-child rule:", prompt, StringComparison.Ordinal);
        Assert.Contains("Do not return Blocked only because a stopped child run exists", prompt, StringComparison.Ordinal);
        Assert.Contains("Do not relaunch a Blocked child or a child with escalation/no-go evidence", prompt, StringComparison.Ordinal);
        Assert.Contains("leave LiveRunProfileKey empty", prompt, StringComparison.Ordinal);
        Assert.Contains("BranchName, RepositoryRoot, SessionId", prompt, StringComparison.Ordinal);
        Assert.Contains("ChildManagedArtifactRoot", prompt, StringComparison.Ordinal);
        Assert.Contains("ParentDeferredOutcomeJson", prompt, StringComparison.Ordinal);
        Assert.Contains("when the launch tool result has RunId and ParentDeferredOutcomeJson, call submit_process_step_outcome with that JSON exactly", prompt, StringComparison.Ordinal);
        Assert.Contains("for Stage Completed it completes the parent from child evidence", prompt, StringComparison.Ordinal);
        Assert.Contains("Treat artifacts under ChildManagedArtifactRoot as the child evidence bundle", prompt, StringComparison.Ordinal);
        Assert.Contains("ExpectedChildEvidenceRefs are preferred lookup candidates after the child run is stopped", prompt, StringComparison.Ordinal);
        Assert.Contains("Managed artifact refs are workspace-managed relative paths", prompt, StringComparison.Ordinal);
        Assert.Contains("Include the written managed artifact paths from this brief in evidenceRefs", prompt, StringComparison.Ordinal);
        Assert.Contains("Never write `Status: InProgress` to a primary managed artifact", prompt, StringComparison.Ordinal);
        Assert.Contains("use that final-evidence ref as a scratch or progress file", prompt, StringComparison.Ordinal);
        Assert.Contains("Do not put native absolute filesystem paths", prompt, StringComparison.Ordinal);
        Assert.Contains("scoped storage paths under artifacts/scopes", prompt, StringComparison.Ordinal);
        Assert.Contains("never convert them to external-target paths", prompt, StringComparison.Ordinal);
        Assert.Contains("Project-structure evidence hygiene:", prompt, StringComparison.Ordinal);
        Assert.Contains("Create only the durable project-structure records required by the current template", prompt, StringComparison.Ordinal);
        Assert.Contains("Keep intermediate subprocess details, logs, and step evidence in managed artifacts", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("one run-app proof node", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void AgentFramework_runtime_owned_subprocess_prompt_uses_single_runtime_launch_owner()
    {
        var runId = new ProcessRunId(Guid.Parse("d9450dd1-4920-457c-92a4-48d1ec648181"));
        var step = CreatePromptStep("Coordinate the typed child evidence for this delivery slice.");
        step.StepKind = ProcessTemplateStepKinds.Subprocess;
        step.SubprocessProcessKey = "dotnet-development-slice";
        step.SubprocessContract = new ProcessSubprocessContract
        {
            DefinitionKey = "dotnet-development-slice",
            LaunchMode = ProcessSubprocessLaunchMode.RuntimeOwned
        };
        step.AllowedOperations =
        [
            ProcessOperationContractNames.ReadProjectStructure,
            ProcessOperationContractNames.ExecuteExternalAction
        ];

        var genericPrompt = BuildStepPrompt(new GenericProcessStepBriefBuilder(), runId, step);
        var prompt = BuildStepPrompt(new AgentFrameworkProcessStepBriefBuilder(), runId, step);

        Assert.Contains("Launch ownership: process runtime owned", genericPrompt, StringComparison.Ordinal);
        Assert.Contains("Do not call project_structure_process_subprocess_launch", genericPrompt, StringComparison.Ordinal);
        Assert.DoesNotContain("Child-outcome rule: when the subprocess launch tool returns", genericPrompt, StringComparison.Ordinal);
        Assert.Contains("Launch ownership: process runtime owned", prompt, StringComparison.Ordinal);
        Assert.Contains("Do not call project_structure_process_subprocess_launch", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("Governed launch tool: project_structure_process_subprocess_launch", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("your first non-read external action", prompt, StringComparison.Ordinal);
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
        Assert.Contains("relevant workspace file tool has produced a current failed receipt", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void AgentFramework_step_brief_uses_launch_variables_as_project_structure_context_instead_of_invented_snapshot_file()
    {
        var productAlias = ExternalTargetAliasCodec.BuildAlias(
            "0123456789abcdef01234567",
            ["programovani", "dotnet", "output"]);
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
                [ProcessRuntimeLaunchVariables.ProductRootAlias] = productAlias,
                ["ParentProcessRunId"] = "f22fa5af-9cad-44fb-a56d-e5d5d1eeae4d",
                ["SubprocessDefinitionKey"] = "dotnet-architecture-design-review"
            });

        Assert.Contains("AgentFramework project-structure context source:", prompt, StringComparison.Ordinal);
        Assert.Contains("A native or storage path-like value remains non-citable final evidence", prompt, StringComparison.Ordinal);
        Assert.Contains("retry diagnostics, or previous failed attempts", prompt, StringComparison.Ordinal);
        Assert.Contains("ProjectStructureContextSummary in Launch variables is the current project-structure context for this run", prompt, StringComparison.Ordinal);
        Assert.Contains("Ignore generated process evidence from prior runs", prompt, StringComparison.Ordinal);
        Assert.Contains("Path-like storage details in ProjectStructureContextSummary are lookup context only", prompt, StringComparison.Ordinal);
        Assert.Contains("Do not copy native absolute paths", prompt, StringComparison.Ordinal);
        Assert.Contains("Launch variables whose names end with Contract are typed project-structure facts", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("DotNetScaffoldContract and DotNet* launch variables are typed project-structure facts", prompt, StringComparison.Ordinal);
        Assert.Contains("ProductRoot, OutputRoot, and ExternalTargetRoot launch variables identify the external target", prompt, StringComparison.Ordinal);
        Assert.Contains($"Grounded external-target aliases for structured workspace tool path arguments: {productAlias}", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("The project-structure context lists visual target assets", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("compare the delivered screenshot against that visual target", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("Exact visual target media path rule", prompt, StringComparison.Ordinal);
        Assert.Contains("Use normalized external-target aliases for structured workspace path arguments", prompt, StringComparison.Ordinal);
        Assert.Contains("retry the same structured workspace tool with that alias before returning Blocked", prompt, StringComparison.Ordinal);
        Assert.Contains("parent launch variables are copied into the child run", prompt, StringComparison.Ordinal);
        Assert.Contains("Do not call workspace_read_file on artifacts/process-runs/d9450dd1-4920-457c-92a4-48d1ec648181/project-structure.json", prompt, StringComparison.Ordinal);
        Assert.Contains("Project-structure context is not materialized as a managed JSON file by default", prompt, StringComparison.Ordinal);
        Assert.Contains("write the relevant facts into the step's primary managed artifact", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("Tetris app", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void AgentFramework_step_brief_exposes_exact_inherited_parent_artifact_refs()
    {
        var parentArtifactRefs = new[]
        {
            "artifacts/process-runs/parent/steps/review.md",
            "artifacts/process-runs/parent/steps/implementation.md"
        };
        var prompt = BuildStepPrompt(
            new AgentFrameworkProcessStepBriefBuilder(),
            ProcessRunId.New(),
            CreatePromptStep("Diagnose the inherited evidence before continuing."),
            CreateDefinition(
                "generic-repair-review",
                "Generic repair review",
                "Review failed output evidence."),
            variables: new Dictionary<string, string>
            {
                [ProcessRuntimeLaunchVariables.ParentProcessRunId] = Guid.NewGuid().ToString("D"),
                [ProcessRuntimeLaunchVariables.ParentRequiredArtifactRefs] =
                    ProcessRuntimeLaunchVariables.SerializeParentRequiredArtifactRefs(parentArtifactRefs)
            });

        Assert.Contains("AgentFramework inherited parent-step artifact refs:", prompt, StringComparison.Ordinal);
        Assert.Contains(parentArtifactRefs[0], prompt, StringComparison.Ordinal);
        Assert.Contains(parentArtifactRefs[1], prompt, StringComparison.Ordinal);
        Assert.Contains("runtime-appended gate findings", prompt, StringComparison.Ordinal);
        Assert.Contains("process adapter loads every ref", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("runtime-hydrated inherited artifact section", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("marked truncated", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Tetris", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Calculator", prompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AgentFramework_step_brief_adds_product_mutation_gate_for_mutable_steps()
    {
        var outputAlias = ExternalTargetAliasCodec.BuildAlias(
            "0123456789abcdef01234567",
            ["programovani", "dotnet", "calculator-output"]);
        var runId = new ProcessRunId(Guid.Parse("d9450dd1-4920-457c-92a4-48d1ec648181"));
        var step = CreatePromptStep("Implement the focused product behavior.");
        step.Key = "code-change";
        step.Title = "Implement code change";
        step.AllowedOperations =
        [
            ProcessOperationContractNames.ReadUpstreamArtifacts,
            ProcessOperationContractNames.MutateProductTarget,
            ProcessOperationContractNames.RunValidation,
            ProcessOperationContractNames.WriteManagedProcessArtifacts
        ];
        step.OperationTargetScope = ProcessOperationContractNames.ExternalProductTargetMutable;

        var prompt = BuildStepPrompt(
            new AgentFrameworkProcessStepBriefBuilder(),
            runId,
            step,
            variables: new Dictionary<string, string>
            {
                ["OutputRoot"] = @"C:\programovani\dotnet\calculator-output",
                [ProcessRuntimeLaunchVariables.OutputRootAlias] = outputAlias
            });

        Assert.Contains("AgentFramework product mutation gate:", prompt, StringComparison.Ordinal);
        Assert.Contains("This step is product-mutating.", prompt, StringComparison.Ordinal);
        Assert.Contains("produce a current-run successful product-target mutation receipt", prompt, StringComparison.Ordinal);
        Assert.Contains(outputAlias, prompt, StringComparison.Ordinal);
        Assert.Contains("Writing only artifacts/process-runs/... is managed evidence, not product mutation.", prompt, StringComparison.Ordinal);
        Assert.Contains("Do not claim changed product files until those files exist under the grounded product target.", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void AgentFramework_step_brief_explains_typed_proof_only_repair_branch()
    {
        var step = CreatePromptStep("Apply the diagnosis-guided quality action.");
        step.Key = "implement-quality-repair";
        step.Title = "Apply repair action";
        step.AllowedOperations =
        [
            ProcessOperationContractNames.ReadUpstreamArtifacts,
            ProcessOperationContractNames.MutateProductTarget,
            ProcessOperationContractNames.RunValidation,
            ProcessOperationContractNames.CaptureRuntimeProof,
            ProcessOperationContractNames.WriteManagedProcessArtifacts
        ];
        step.OperationTargetScope = ProcessOperationContractNames.ExternalProductTargetMutable;
        step.BranchOutcomes =
        [
            new ProcessTemplateDefinitionStepBranchOutcomeDocument { Key = "product-repair-applied" },
            new ProcessTemplateDefinitionStepBranchOutcomeDocument { Key = "proof-only-revalidation-prepared" },
            new ProcessTemplateDefinitionStepBranchOutcomeDocument { Key = "repair-attempt-incomplete" }
        ];
        step.CompletionPolicy = new ProcessTemplateStepCompletionPolicyDocument
        {
            RequiresProductMutationBeforeManagedOutput = true
        };
        var mutationBranchMap = JsonSerializer.Serialize(new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            [step.Key] = ["product-repair-applied"]
        });
        var runtimeRoutedBranchMap = JsonSerializer.Serialize(new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            [step.Key] = ["repair-attempt-incomplete"]
        });

        var prompt = BuildStepPrompt(
            new AgentFrameworkProcessStepBriefBuilder(),
            ProcessRunId.New(),
            step,
            variables: new Dictionary<string, string>
            {
                ["OutputRoot"] = @"C:\programovani\dotnet\business-output",
                [ProcessRuntimeLaunchVariables.ProductMutationRequiredBranchOutcomeKeysByStep] = mutationBranchMap,
                [ProcessRuntimeLaunchVariables.RuntimeRoutedBranchOutcomeKeysByStep] = runtimeRoutedBranchMap
            });

        Assert.Contains("branch-specific mutation semantics", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("product-repair-applied", prompt, StringComparison.Ordinal);
        Assert.Contains("proof-only-revalidation-prepared", prompt, StringComparison.Ordinal);
        Assert.Contains("Do not mutate unrelated product files merely to satisfy a receipt gate.", prompt, StringComparison.Ordinal);
        Assert.Contains("required current-execution validation/proof", prompt, StringComparison.Ordinal);
        Assert.Contains("Before writing the final primary managed artifact, select one declared branch outcome.", prompt, StringComparison.Ordinal);
        Assert.Contains("The governed policy checks the canonical Branch outcome key", prompt, StringComparison.Ordinal);
        Assert.Contains("intentionally omitted from Available branch outcomes", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Never select, infer, copy, or write a branch key that is not listed", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("repair-attempt-incomplete", prompt, StringComparison.Ordinal);
        var availableBranchSection = prompt[
            prompt.IndexOf("Available branch outcomes:", StringComparison.Ordinal)..
            prompt.IndexOf("Return the executor-specific", StringComparison.Ordinal)];
        Assert.DoesNotContain("repair-attempt-incomplete", availableBranchSection, StringComparison.Ordinal);
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
        Assert.Contains("Tool precondition rule", prompt, StringComparison.Ordinal);
        Assert.Contains("follow each tool's declared creation, inspection, and invocation prerequisites", prompt, StringComparison.Ordinal);
        Assert.Contains("first workspace mutation for this slot must create the primary write ref with workspace_write_file or workspace_append_file", prompt, StringComparison.Ordinal);
        Assert.Contains("the first workspace mutation for the produced output must be workspace_write_file or workspace_append_file to the listed Primary write ref", prompt, StringComparison.Ordinal);
        Assert.Contains("AgentFramework own-output bootstrap:", prompt, StringComparison.Ordinal);
        Assert.Contains("This step has produced artifact slots and no required upstream artifact slots.", prompt, StringComparison.Ordinal);
        Assert.Contains("Do not return Blocked for missing optional context or prior evidence before creating your own managed artifact.", prompt, StringComparison.Ordinal);
        Assert.Contains("First satisfy any explicit current-execution evidence obligation in this step brief", prompt, StringComparison.Ordinal);
        Assert.Contains("Primary own-output write ref: artifacts/process-runs/d9450dd1-4920-457c-92a4-48d1ec648181/steps/feature-intake.md", prompt, StringComparison.Ordinal);
        Assert.Contains("return Completed with evidenceRefs containing the exact primary own-output write ref", prompt, StringComparison.Ordinal);
        Assert.Contains("Do not read or stat an external target looking for a same-named own-output packet before writing this managed artifact", prompt, StringComparison.Ordinal);
        Assert.Contains("own process outputs are generated under managed artifact refs", prompt, StringComparison.Ordinal);
        Assert.Contains("Do not require evidence that the template assigns to later steps", prompt, StringComparison.Ordinal);
        Assert.Contains("Blocked is valid only when you cannot create the primary managed artifact", prompt, StringComparison.Ordinal);
        Assert.Contains("If optional project context is missing, include assumptions and known gaps inside the artifact instead of blocking.", prompt, StringComparison.Ordinal);
        Assert.Contains("Do not finalize Completed with an empty evidenceRefs array", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void AgentFramework_step_brief_appends_process_scoped_instruction_fragments()
    {
        var runId = new ProcessRunId(Guid.Parse("d9450dd1-4920-457c-92a4-48d1ec648181"));
        var step = CreatePromptStep("Coordinate management status.");
        step.CapabilityScope = new ProcessCapabilityScope
        {
            InstructionFragments =
            [
                new ProcessScopedInstructionFragment
                {
                    Key = "management-only",
                    Title = "Management-only scope",
                    Content = "Coordinate staffing and status. Do not implement product code."
                }
            ]
        };

        var prompt = BuildStepPrompt(new AgentFrameworkProcessStepBriefBuilder(), runId, step);

        Assert.Contains("AgentFramework process-scoped instructions:", prompt, StringComparison.Ordinal);
        Assert.Contains("Management-only scope: Coordinate staffing and status. Do not implement product code.", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void Dotnet_solution_context_template_exposes_schema_and_authoring_contract_to_the_agent()
    {
        var definition = new ProcessTemplatePackLoader().LoadDefinition("dotnet-development-slice");
        var step = definition.Steps.Single(candidate =>
            string.Equals(candidate.Key, "slice-architecture-check", StringComparison.Ordinal));
        var executionGuidance = Assert.Single(step.ResolvedExecutionGuidance);
        var decisionSlot = ArtifactSlotId.New();
        var solutionContextSlot = ArtifactSlotId.New();

        var prompt = BuildStepPrompt(
            new AgentFrameworkProcessStepBriefBuilder(),
            ProcessRunId.New(),
            step,
            definition,
            requiredSlots: [],
            producedSlots: [decisionSlot, solutionContextSlot],
            artifactSlotByStepExpectation: new Dictionary<(string StepKey, string ExpectationKey), ArtifactSlotId>
            {
                [(step.Key, "slice-architecture-decision")] = decisionSlot,
                [(step.Key, "dotnet-solution-context")] = solutionContextSlot
            });

        Assert.Contains("Payload schema: dotnet.solution-context/v1", prompt, StringComparison.Ordinal);
        Assert.Contains("## Solution-context contract", prompt, StringComparison.Ordinal);
        Assert.Contains("exactly one fenced `json` block", prompt, StringComparison.Ordinal);
        Assert.Contains("Whole-payload self-check before writing", prompt, StringComparison.Ordinal);
        Assert.Contains("`application.templateOptions` is only for optional `dotnet new` option flags", prompt, StringComparison.Ordinal);
        Assert.Equal("processes/dotnet-development-slice/steps/slice-architecture-check.md", executionGuidance.Reference);
        Assert.Contains("The runtime independently checks the workspace-approved options", executionGuidance.Content, StringComparison.Ordinal);
        Assert.Contains("The runtime independently checks the workspace-approved options", prompt, StringComparison.Ordinal);
        Assert.Contains("single source of truth for both the app and test projects", executionGuidance.Content, StringComparison.Ordinal);
        Assert.Contains("never rely on the installed SDK default", prompt, StringComparison.Ordinal);
        Assert.Contains("machine fields, not display labels", executionGuidance.Content, StringComparison.Ordinal);
        Assert.Contains("Blazor WebAssembly App", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void Slice_intake_template_exposes_product_target_state_provenance_contract_to_the_agent()
    {
        var definition = new ProcessTemplatePackLoader().LoadDefinition("dotnet-development-slice");
        var step = definition.Steps.Single(candidate =>
            string.Equals(candidate.Key, "slice-intake", StringComparison.Ordinal));
        var executionGuidance = Assert.Single(step.ResolvedExecutionGuidance);

        var prompt = BuildStepPrompt(
            new AgentFrameworkProcessStepBriefBuilder(),
            ProcessRunId.New(),
            step,
            definition);

        Assert.Equal("processes/dotnet-development-slice/steps/slice-intake.md", executionGuidance.Reference);
        Assert.Contains("Product target state decision", prompt, StringComparison.Ordinal);
        Assert.Contains("semantic baseline-provenance decision", prompt, StringComparison.Ordinal);
        Assert.Contains("configured target alias, output root, or directory existence is not enough", prompt, StringComparison.Ordinal);
        Assert.Contains("When the requested deliverable is new and no authoritative baseline is identified, select greenfield", prompt, StringComparison.Ordinal);
        Assert.Contains("Select `existing` only when current-run project structure or an upstream artifact identifies the baseline", executionGuidance.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void Slice_validation_prompt_uses_verified_child_payload_without_inventing_product_repair()
    {
        var definition = new ProcessTemplatePackLoader().LoadDefinition("dotnet-development-slice");
        var step = definition.Steps.Single(candidate =>
            string.Equals(candidate.Key, "add-tests-and-proof", StringComparison.Ordinal));
        var executionGuidance = Assert.Single(step.ResolvedExecutionGuidance);

        var prompt = BuildStepPrompt(
            new AgentFrameworkProcessStepBriefBuilder(),
            ProcessRunId.New(),
            step,
            definition);

        Assert.Contains("hash-verified selected child payload", executionGuidance.Content, StringComparison.Ordinal);
        Assert.Contains("runtime-authenticated child-output payload", prompt, StringComparison.Ordinal);
        Assert.Contains(
            "Do not re-decide the typed bridge from payload prose",
            prompt,
            StringComparison.Ordinal);
        Assert.Contains(
            "absent separate agent-authored restatement",
            prompt,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Software_delivery_feature_intake_template_reaches_the_agent_prompt()
    {
        var definition = new ProcessTemplatePackLoader().LoadDefinition("software-delivery");
        var step = definition.Steps.Single(candidate =>
            string.Equals(candidate.Key, "feature-intake", StringComparison.Ordinal));
        var executionGuidance = Assert.Single(step.ResolvedExecutionGuidance);

        var prompt = BuildStepPrompt(
            new AgentFrameworkProcessStepBriefBuilder(),
            ProcessRunId.New(),
            step,
            definition);

        Assert.Equal("processes/software-delivery/steps/feature-intake.md", executionGuidance.Reference);
        Assert.Contains("Treat the active launch request and selected project node as authoritative", executionGuidance.Content, StringComparison.Ordinal);
        Assert.Contains("Treat the active launch request and selected project node as authoritative", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void Slice_repair_escalation_template_reaches_the_manager_prompt()
    {
        var definition = new ProcessTemplatePackLoader().LoadDefinition("dotnet-development-slice");
        var step = definition.Steps.Single(candidate =>
            string.Equals(candidate.Key, "slice-repair-escalation", StringComparison.Ordinal));
        var executionGuidance = Assert.Single(step.ResolvedExecutionGuidance);

        var prompt = BuildStepPrompt(
            new AgentFrameworkProcessStepBriefBuilder(),
            ProcessRunId.New(),
            step,
            definition);

        Assert.Equal("processes/dotnet-development-slice/steps/slice-repair-escalation.md", executionGuidance.Reference);
        Assert.Contains("The absence of an accepted child handoff is a routing symptom", executionGuidance.Content, StringComparison.Ordinal);
        Assert.Contains("The absence of an accepted child handoff is a routing symptom", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void AgentFramework_step_brief_keeps_declared_external_evidence_before_own_output_bootstrap()
    {
        var runId = new ProcessRunId(Guid.Parse("d9450dd1-4920-457c-92a4-48d1ec648181"));
        var step = CreatePromptStep("Diagnose the observed failure.");
        step.CapabilityScope = new ProcessCapabilityScope
        {
            InstructionFragments =
            [
                new ProcessScopedInstructionFragment
                {
                    Key = "ground-owning-source",
                    Title = "Owning product source proof",
                    Content = "Read a concrete owning product file under the grounded external-target alias before writing the diagnosis artifact."
                }
            ],
            RequiredReceipts =
            [
                new ProcessRequiredToolReceipt
                {
                    Key = "read-owning-product-source",
                    Kind = ProcessRequiredToolReceiptKind.RuntimeToolName,
                    ToolName = "workspace_read_file",
                    Purpose = ProcessRequiredToolReceiptPurpose.DefectEvidence,
                    RequireCurrentRun = true,
                    RequireSuccessfulExit = true,
                    Activation = ProcessRequiredToolReceiptActivation.Always,
                    Reason = "Source grounding is required."
                }
            ]
        };

        var prompt = BuildStepPrompt(
            new AgentFrameworkProcessStepBriefBuilder(),
            runId,
            step,
            variables: new Dictionary<string, string>
            {
                [ProcessRuntimeLaunchVariables.ProductRootAlias] = "external-target/C/work/product"
            });

        Assert.Contains("Owning product source proof: Read a concrete owning product file", prompt, StringComparison.Ordinal);
        Assert.Contains("First satisfy any explicit current-execution evidence obligation in this step brief", prompt, StringComparison.Ordinal);
        Assert.Contains("Primary own-output write ref: artifacts/process-runs/d9450dd1-4920-457c-92a4-48d1ec648181/steps/resolve-contract.md", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("Your first evidence action must be workspace_write_file", prompt, StringComparison.Ordinal);
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
            DecisionRightsSummary = "Select an evidence-backed outcome within the assigned step boundary.",
            ExceptionPolicySummary = "Escalate only a concrete access, policy, environment, or contract boundary.",
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

    private sealed class FakePromptCompositionDriver(string name) : IProcessPromptCompositionDriver
    {
        public DriverId DriverId { get; } = new(name);

        public bool CanCompose(ProcessStepBriefBuildRequest request)
            => string.Equals(request.Definition.Key, "supplier-risk-review", StringComparison.Ordinal);

        public string Compose(ProcessStepBriefBuildRequest request)
            => $"fake prompt from {DriverId.Value} for {request.Definition.Key}/{request.Step.Key}";
    }
}
