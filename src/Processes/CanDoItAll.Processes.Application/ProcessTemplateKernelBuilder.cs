using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Templates;

namespace CanDoItAll.Processes.Application;

internal static class ProcessTemplateKernelBuilder
{
    public static ProcessTemplateKernelBuildResult Build(
        ProcessTemplateDefinitionDocument definition,
        string packVersion,
        StrategyId stepExecutionStrategyId)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrWhiteSpace(packVersion);

        var definitionContentHash = ComputeDefinitionContentHash(definition);
        var definitionId = CreateDefinitionId(definition.Key);
        var versionId = new ProcessDefinitionVersionId(CreateDeterministicGuid($"definition-version:{definition.Key}:{definitionContentHash}"));
        var stepIds = definition.Steps.ToDictionary(
            step => step.Key,
            step => new ProcessStepDefinitionId(CreateDeterministicGuid($"step:{definition.Key}:{step.Key}")),
            StringComparer.OrdinalIgnoreCase);
        var artifactDefinitions = new List<ProcessArtifactDefinition>();
        var artifactSlots = new List<ProcessArtifactSlotDefinition>();
        var artifactSlotByExpectation = new Dictionary<string, ArtifactSlotId>(StringComparer.OrdinalIgnoreCase);
        var artifactSlotByStepExpectation = new Dictionary<(string StepKey, string ExpectationKey), ArtifactSlotId>();

        foreach (var step in definition.Steps)
        {
            foreach (var expectation in step.ArtifactExpectations)
            {
                var artifactKey = string.IsNullOrWhiteSpace(expectation.TemplateKey)
                    ? $"{step.Key}:{expectation.Key}"
                    : expectation.TemplateKey.Trim();
                var artifactDefinitionId = new ArtifactDefinitionId(CreateDeterministicGuid($"artifact:{definition.Key}:{artifactKey}"));
                var slotId = new ArtifactSlotId(CreateDeterministicGuid($"slot:{definition.Key}:{step.Key}:{expectation.Key}"));

                if (!artifactDefinitions.Any(item => item.Id == artifactDefinitionId))
                {
                    artifactDefinitions.Add(new ProcessArtifactDefinition(
                        artifactDefinitionId,
                        artifactKey,
                        ProcessArtifactSensitivity.Normal));
                }

                artifactSlots.Add(new ProcessArtifactSlotDefinition(
                    slotId,
                    $"{step.Key}:{expectation.Key}",
                    artifactDefinitionId,
                    expectation.IsRequired ? ProcessArtifactRequirementMode.Produced : ProcessArtifactRequirementMode.Optional,
                    ProcessArtifactScope.Local,
                    HasBoundaryPolicy: false));
                artifactSlotByExpectation[expectation.Key] = slotId;
                artifactSlotByStepExpectation[(step.Key, expectation.Key)] = slotId;
            }
        }

        var nodes = definition.Steps
            .OrderBy(step => step.Order)
            .Select(step => new ProcessGraphNode(
                stepIds[step.Key],
                step.Key,
                ProcessStepKind.Activity,
                stepExecutionStrategyId))
            .ToArray();
        var edges = BuildEdges(definition, stepIds);
        var branches = BuildBranches(definition, stepIds);
        var kernel = new ProcessDefinitionKernel(
            definitionId,
            versionId,
            nodes,
            edges,
            artifactDefinitions,
            artifactSlots,
            branches);

        return new ProcessTemplateKernelBuildResult(
            kernel,
            definitionContentHash,
            artifactSlotByStepExpectation,
            artifactSlotByExpectation);
    }

    public static ProcessDefinitionId CreateDefinitionId(string definitionKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(definitionKey);
        return new ProcessDefinitionId(CreateDeterministicGuid($"definition:{definitionKey.Trim()}"));
    }

    private static IReadOnlyList<ProcessGraphEdge> BuildEdges(
        ProcessTemplateDefinitionDocument definition,
        IReadOnlyDictionary<string, ProcessStepDefinitionId> stepIds)
    {
        var edges = new List<ProcessGraphEdge>();
        foreach (var step in definition.Steps)
        {
            foreach (var dependency in EnumerateDependencies(step).Where(dependency => string.IsNullOrWhiteSpace(dependency.BranchOutcomeKey)))
            {
                if (stepIds.TryGetValue(dependency.StepKey, out var sourceId) &&
                    stepIds.TryGetValue(step.Key, out var targetId))
                {
                    edges.Add(new ProcessGraphEdge(sourceId, targetId));
                }
            }
        }

        return edges;
    }

    private static IReadOnlyList<ProcessBranchDefinition> BuildBranches(
        ProcessTemplateDefinitionDocument definition,
        IReadOnlyDictionary<string, ProcessStepDefinitionId> stepIds)
    {
        var branches = new List<ProcessBranchDefinition>();
        foreach (var step in definition.Steps.Where(step => step.BranchOutcomes.Count > 0))
        {
            if (!stepIds.TryGetValue(step.Key, out var stepId))
            {
                continue;
            }

            branches.Add(new ProcessBranchDefinition(
                stepId,
                new BranchFamilyId($"branch.{step.Key}"),
                [],
                step.BranchOutcomes
                    .Select(outcome => new BranchOutcomeDefinition(
                        new BranchOutcomeId(outcome.Key),
                        string.IsNullOrWhiteSpace(outcome.Title) ? outcome.Key : outcome.Title,
                        ResolveOutcomeCategory(outcome),
                        ResolveRouteTarget(outcome, stepIds),
                        ResolveLoopBudget(outcome, stepIds)))
                    .ToArray()));
        }

        return branches;
    }

    public static IEnumerable<ProcessTemplateStepDependency> EnumerateDependencies(ProcessTemplateDefinitionStepDocument step)
    {
        foreach (var dependency in step.Dependencies)
        {
            if (!string.IsNullOrWhiteSpace(dependency.DependsOnStepKey))
            {
                yield return new ProcessTemplateStepDependency(
                    dependency.DependsOnStepKey.Trim(),
                    dependency.DependsOnBranchOutcomeKey.Trim());
            }
        }

        if (!string.IsNullOrWhiteSpace(step.DependsOnStepKey))
        {
            yield return new ProcessTemplateStepDependency(
                step.DependsOnStepKey.Trim(),
                step.DependsOnBranchOutcomeKey.Trim());
        }
    }

    private static BranchOutcomeCategory ResolveOutcomeCategory(ProcessTemplateDefinitionStepBranchOutcomeDocument outcome)
    {
        if (outcome.IsBackwardRoute)
        {
            return BranchOutcomeCategory.Repeat;
        }

        var token = $"{outcome.Key} {outcome.Title} {outcome.Description}".ToLowerInvariant();
        if (token.Contains("repair", StringComparison.Ordinal) || token.Contains("repeat", StringComparison.Ordinal))
        {
            return BranchOutcomeCategory.Repeat;
        }

        if (token.Contains("fail", StringComparison.Ordinal))
        {
            return BranchOutcomeCategory.Fail;
        }

        if (token.Contains("cancel", StringComparison.Ordinal))
        {
            return BranchOutcomeCategory.Cancel;
        }

        if (token.Contains("hold", StringComparison.Ordinal) || token.Contains("wait", StringComparison.Ordinal))
        {
            return BranchOutcomeCategory.Wait;
        }

        return BranchOutcomeCategory.Continue;
    }

    private static ProcessRouteTarget ResolveRouteTarget(
        ProcessTemplateDefinitionStepBranchOutcomeDocument outcome,
        IReadOnlyDictionary<string, ProcessStepDefinitionId> stepIds)
    {
        if (!string.IsNullOrWhiteSpace(outcome.RouteTargetStepKey) &&
            stepIds.TryGetValue(outcome.RouteTargetStepKey.Trim(), out var stepId))
        {
            return new ProcessRouteTarget(ProcessRouteTargetKind.SpecificStep, stepId);
        }

        if (outcome.IsBackwardRoute)
        {
            return new ProcessRouteTarget(ProcessRouteTargetKind.PreviousStep);
        }

        return new ProcessRouteTarget(ProcessRouteTargetKind.NextStep);
    }

    private static LoopBudgetDefinition? ResolveLoopBudget(
        ProcessTemplateDefinitionStepBranchOutcomeDocument outcome,
        IReadOnlyDictionary<string, ProcessStepDefinitionId> stepIds)
    {
        if (!outcome.IsBackwardRoute)
        {
            return null;
        }

        var policyId = string.IsNullOrWhiteSpace(outcome.LoopFingerprintPolicyKey)
            ? $"loop.{outcome.Key}"
            : outcome.LoopFingerprintPolicyKey.Trim();
        return new LoopBudgetDefinition(
            Math.Max(1, outcome.LoopBudgetMaximumRepeats),
            new LoopFingerprintPolicyId(policyId),
            ResolveEscalationTarget(outcome, stepIds));
    }

    private static ProcessRouteTarget ResolveEscalationTarget(
        ProcessTemplateDefinitionStepBranchOutcomeDocument outcome,
        IReadOnlyDictionary<string, ProcessStepDefinitionId> stepIds)
    {
        if (!string.IsNullOrWhiteSpace(outcome.LoopEscalationTargetKind) &&
            Enum.TryParse<ProcessRouteTargetKind>(outcome.LoopEscalationTargetKind, ignoreCase: true, out var kind))
        {
            return new ProcessRouteTarget(kind);
        }

        return new ProcessRouteTarget(ProcessRouteTargetKind.Escalate);
    }

    private static string ComputeDefinitionContentHash(ProcessTemplateDefinitionDocument definition)
    {
        var json = JsonSerializer.Serialize(definition, ProcessTemplateJsonContext.Default.ProcessTemplateDefinitionDocument);
        var executionGuidanceFingerprint = string.Join(
            "|",
            definition.Steps
                .OrderBy(step => step.Key, StringComparer.Ordinal)
                .SelectMany(step => step.ResolvedExecutionGuidance
                    .OrderBy(guidance => guidance.Reference, StringComparer.Ordinal)
                    .Select(guidance => $"{step.Key}:{guidance.Reference}:{guidance.ContentHash}")));
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{json}\n{executionGuidanceFingerprint}"));
        return "sha256:" + Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static Guid CreateDeterministicGuid(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes("CanDoItAll.Processes.Application/v1:" + value));
        Span<byte> guidBytes = stackalloc byte[16];
        bytes.AsSpan(0, 16).CopyTo(guidBytes);
        return new Guid(guidBytes);
    }
}

internal sealed record ProcessTemplateKernelBuildResult(
    ProcessDefinitionKernel Definition,
    string DefinitionContentHash,
    IReadOnlyDictionary<(string StepKey, string ExpectationKey), ArtifactSlotId> ArtifactSlotByStepExpectation,
    IReadOnlyDictionary<string, ArtifactSlotId> ArtifactSlotByExpectation);

internal sealed record ProcessTemplateStepDependency(
    string StepKey,
    string BranchOutcomeKey);
