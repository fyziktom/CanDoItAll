using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Tests.Unit;

public sealed class ProcessSubprocessParentArtifactContextBuilderTests
{
    [Fact]
    public void Apply_exposes_only_exact_required_parent_artifact_refs()
    {
        var parentStepId = ProcessStepInstanceId.New();
        var requiredSlotId = ArtifactSlotId.New();
        var producedSlotId = ArtifactSlotId.New();
        var parentState = CreateState(
            new ProcessRuntimeStepState(
                parentStepId,
                ProcessStepDefinitionId.New(),
                ProcessRuntimeStepStatus.Running,
                IsExecutable: true,
                AttemptNumber: 1,
                DependencyStepIds: new HashSet<ProcessStepInstanceId>(),
                RequiredArtifactSlots: new HashSet<ArtifactSlotId> { requiredSlotId },
                ActiveClaimToken: null,
                CompletedResultKey: null)
            {
                ProducedArtifactSlots = new HashSet<ArtifactSlotId> { producedSlotId },
                ArtifactDescriptors =
                [
                    Descriptor(requiredSlotId, "qa-validation", "artifacts/process-runs/parent/steps/qa-validation.md"),
                    Descriptor(producedSlotId, "quality-repair", "artifacts/process-runs/parent/steps/quality-repair.md")
                ]
            });
        var variables = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [ProcessRuntimeLaunchVariables.ParentRequiredArtifactRefs] = "[\"stale-grandparent-ref\"]"
        };

        ProcessSubprocessParentArtifactContextBuilder.Apply(variables, parentState, parentStepId);

        Assert.True(ProcessRuntimeLaunchVariables.TryReadParentRequiredArtifactRefs(variables, out var artifactRefs));
        var artifactRef = Assert.Single(artifactRefs);
        Assert.Equal("artifacts/process-runs/parent/steps/qa-validation.md", artifactRef);
    }

    [Fact]
    public void Apply_removes_stale_inherited_refs_when_current_parent_has_no_required_artifacts()
    {
        var variables = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [ProcessRuntimeLaunchVariables.ParentRequiredArtifactRefs] = "[\"stale-grandparent-ref\"]"
        };

        ProcessSubprocessParentArtifactContextBuilder.Apply(
            variables,
            CreateState(),
            ProcessStepInstanceId.New());

        Assert.DoesNotContain(ProcessRuntimeLaunchVariables.ParentRequiredArtifactRefs, variables);
    }

    [Fact]
    public void Apply_exposes_child_evidence_cited_by_runtime_synthesized_parent_handoff()
    {
        var workspaceRoot = Path.Combine(Path.GetTempPath(), $"CanDoItAll.SubprocessContext.{Guid.NewGuid():N}");
        Directory.CreateDirectory(workspaceRoot);
        try
        {
            var parentStepId = ProcessStepInstanceId.New();
            var requiredSlotId = ArtifactSlotId.New();
            var childRunId = Guid.NewGuid();
            var parentRef = "artifacts/process-runs/parent/steps/implement-code-change.md";
            var childRef = $"artifacts/process-runs/{childRunId:D}/steps/feature-repair-escalation.md";
            var workspaceFiles = new WorkspaceFileService(workspaceRoot);
            var writeResult = workspaceFiles.WriteTextFile(
                parentRef,
                $"""
                ## Runtime Captured Structured Outcome

                Matching child process run {childRunId:D} completed with typed evidence.

                ## Subprocess handoff completed

                ## Child evidence

                - `{childRef}`

                ## Runtime Accepted Completion Gates
                """);
            Assert.True(writeResult.Succeeded, writeResult.Message);
            var parentState = CreateState(
                new ProcessRuntimeStepState(
                    parentStepId,
                    ProcessStepDefinitionId.New(),
                    ProcessRuntimeStepStatus.Running,
                    IsExecutable: true,
                    AttemptNumber: 1,
                    DependencyStepIds: new HashSet<ProcessStepInstanceId>(),
                    RequiredArtifactSlots: new HashSet<ArtifactSlotId> { requiredSlotId },
                    ActiveClaimToken: null,
                    CompletedResultKey: null)
                {
                    ArtifactDescriptors =
                    [
                        Descriptor(
                            requiredSlotId,
                            "implement-code-change",
                            parentRef,
                            ProcessArtifactMaterializationMode.RuntimeSynthesizedParentHandoff)
                    ]
                });
            var variables = new Dictionary<string, string>(StringComparer.Ordinal);

            ProcessSubprocessParentArtifactContextBuilder.Apply(
                variables,
                parentState,
                parentStepId,
                workspaceFiles);

            Assert.True(ProcessRuntimeLaunchVariables.TryReadParentRequiredArtifactRefs(variables, out var artifactRefs));
            Assert.Equal(
                new[] { parentRef, childRef }.Order(StringComparer.OrdinalIgnoreCase).ToArray(),
                artifactRefs);
        }
        finally
        {
            Directory.Delete(workspaceRoot, recursive: true);
        }
    }

    private static ProcessRuntimeStateSnapshot CreateState(params ProcessRuntimeStepState[] steps)
    {
        var runId = ProcessRunId.New();
        return new ProcessRuntimeStateSnapshot(
            runId,
            runId,
            ProcessInstancePlanId.New(),
            "sha256:plan",
            ProcessRuntimeStatus.Active,
            steps,
            [],
            [],
            new HashSet<ArtifactSlotId>(),
            DateTimeOffset.UtcNow);
    }

    private static ProcessArtifactSlotDescriptor Descriptor(
        ArtifactSlotId slotId,
        string stepKey,
        string primaryManagedRef,
        ProcessArtifactMaterializationMode materializationMode = ProcessArtifactMaterializationMode.AgentWritten)
        => new(
            slotId,
            $"{stepKey}:artifact",
            stepKey,
            "artifact",
            "Artifact",
            "Evidence",
            primaryManagedRef,
            materializationMode);
}
