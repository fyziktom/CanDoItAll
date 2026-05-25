using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Processes;

public sealed class ProcessTemplateProjectionService
{
    private readonly ProcessTemplatePackLoader packLoader;
    private readonly ProcessCanvasRecompositionService recompositionService;

    public ProcessTemplateProjectionService(
        ProcessTemplatePackLoader packLoader,
        ProcessCanvasRecompositionService? recompositionService = null)
    {
        this.packLoader = packLoader;
        this.recompositionService = recompositionService ??
            new ProcessCanvasRecompositionService(
                new ProcessCanvasSurfaceFactory(
                    new ProcessCanvasChromeCatalogService(packLoader)));
    }

    public ProcessImportExportEnvelope GetProjectedEnvelope(
        string processKey,
        Guid? projectId = null,
        string? definitionName = null)
    {
        var pack = packLoader.Load();
        var process = GetProcess(pack, processKey);
        var definition = BuildDefinition(pack, process, projectId, definitionName);
        ApplyBalancedFlowComposition(definition);

        return new ProcessImportExportEnvelope
        {
            Definition = ProcessDependencyCompatibilityBridge.ToImportExportModel(
                definition),
            SourceFormat = "CanDoItAll.ProcessTemplatePack/current-module-projection",
            Warnings =
            [
                $"Projected from template pack process '{process.Key}'.",
                "Canvas positions were normalized with the Balanced Flow composition.",
                "Detailed sidecar metadata remains in the process-template pack files."
            ]
        };
    }

    public string GetCompatibilityReportJson(string processKey)
    {
        var process = GetProcess(packLoader.Load(), processKey);
        return File.ReadAllText(process.CurrentModuleCompatibilityReportPath);
    }

    public string GetCompatibilityReportMarkdown(string processKey)
    {
        var process = GetProcess(packLoader.Load(), processKey);
        return File.ReadAllText(process.CurrentModuleCompatibilityReportMarkdownPath);
    }

    private static ProcessDefinitionEditorModel BuildDefinition(
        ProcessTemplatePack pack,
        ProcessTemplateDefinition process,
        Guid? projectId,
        string? definitionName)
    {
        var definition = new ProcessDefinitionEditorModel
        {
            ProjectId = projectId,
            Name = string.IsNullOrWhiteSpace(definitionName) ? process.DisplayName : definitionName.Trim(),
            Summary = process.Summary,
            ValueStatement = process.ValueStatement,
            CustomerName = process.CustomerName,
            OwnerName = process.OwnerName,
            InterfaceContractSummary = process.InterfaceContractSummary,
            GovernanceNotes = process.GovernanceNotes,
            ChangeSummary = process.ChangeSummary,
            GovernancePolicySummary = process.GovernancePolicySummary,
            ConstitutionRuleSummary = process.ConstitutionRuleSummary,
            OperatingModeSummary = process.OperatingModeSummary,
            SimulationReadinessSummary = process.SimulationReadinessSummary,
            Criticality = EnumValueParser.ParseOrDefault(process.Criticality, ProcessCriticality.Standard),
            AutonomyLevel = EnumValueParser.ParseOrDefault(process.AutonomyLevel, ProcessAutonomyLevel.Assisted)
        };

        var roleIdsByKey = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        foreach (var usage in process.RoleUsages)
        {
            var roleId = Guid.NewGuid();
            var roleResource = ResolveRoleResource(pack, process, usage);

            roleIdsByKey[usage.Key] = roleId;
            definition.Roles.Add(ProcessTemplateEditorModelFactory.CreateRoleFromUsage(usage, roleResource, roleId));
        }

        var orderedTemplates = process.Steps
            .OrderBy(item => item.Order)
            .ToList();
        var stepIdsByKey = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        var branchOutcomeIdsByKey = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        var artifactExpectationIdsByKey = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

        foreach (var template in orderedTemplates)
        {
            var stepId = Guid.NewGuid();
            stepIdsByKey[template.Key] = stepId;

            var step = new ProcessStepEditorModel
            {
                Id = stepId,
                Key = template.Key,
                Title = template.Title,
                Subtitle = template.Subtitle,
                Notes = template.Notes,
                StepKind = EnumValueParser.ParseOrDefault(template.StepKind, ProcessStepKind.Work),
                AllowedOperations = ProcessStepOperationContractState.NormalizeAllowedOperations(template.AllowedOperations),
                OperationTargetScope = template.OperationTargetScope,
                SubprocessDefinitionSnapshotName = string.IsNullOrWhiteSpace(template.SubprocessDefinitionSnapshotName)
                    ? template.SubprocessProcessKey
                    : template.SubprocessDefinitionSnapshotName,
                AllowsManualSkip = template.AllowsManualSkip,
                AllowsSafeRefusal = template.AllowsSafeRefusal,
                RequiresApproval = template.RequiresApproval,
                RequiresDecisionRecord = template.RequiresDecisionRecord,
                InputContractSummary = template.InputContractSummary,
                OutputContractSummary = template.OutputContractSummary,
                EvidenceContractSummary = template.EvidenceContractSummary,
                DecisionRightsSummary = template.DecisionRightsSummary,
                ExceptionPolicySummary = template.ExceptionPolicySummary,
                TargetLeadHours = template.TargetLeadHours,
                DecisionRoleRequirementId = ResolveRoleId(
                    roleIdsByKey,
                    template.DecisionRoleKey,
                    process.Key,
                    template.Key,
                    "decision role"),
                CanvasX = template.CanvasX,
                CanvasY = template.CanvasY,
                BranchCanvasX = template.BranchCanvasX,
                BranchCanvasY = template.BranchCanvasY
            };

            foreach (var branchOutcome in template.BranchOutcomes)
            {
                var branchOutcomeId = Guid.NewGuid();
                branchOutcomeIdsByKey[BuildCompositeKey(template.Key, branchOutcome.Key)] = branchOutcomeId;

                step.BranchOutcomes.Add(new ProcessStepBranchOutcomeEditorModel
                {
                    Id = branchOutcomeId,
                    Key = branchOutcome.Key,
                    Title = branchOutcome.Title,
                    Description = branchOutcome.Description
                });
            }

            foreach (var artifactExpectation in template.ArtifactExpectations)
            {
                var artifactExpectationId = Guid.NewGuid();
                var artifactResource = ResolveArtifactResource(pack, process, artifactExpectation.TemplateKey);

                artifactExpectationIdsByKey[BuildCompositeKey(template.Key, artifactExpectation.Key)] = artifactExpectationId;
                if (!string.IsNullOrWhiteSpace(artifactExpectation.TemplateKey))
                {
                    artifactExpectationIdsByKey[BuildCompositeKey(template.Key, artifactExpectation.TemplateKey)] = artifactExpectationId;
                }

                step.ArtifactExpectations.Add(
                    ProcessTemplateEditorModelFactory.CreateArtifactExpectationFromTemplate(
                        artifactExpectation,
                        artifactResource,
                        artifactExpectationId));
            }

            definition.Steps.Add(step);
        }

        foreach (var template in orderedTemplates)
        {
            var step = definition.Steps.Single(item => string.Equals(item.Key, template.Key, StringComparison.OrdinalIgnoreCase));

            var dependencies = template.Dependencies.Count > 0
                ? template.Dependencies
                : string.IsNullOrWhiteSpace(template.DependsOnStepKey)
                    ? []
                    : [new ProcessTemplateStepDependency
                    {
                        DependsOnStepKey = template.DependsOnStepKey,
                        DependsOnBranchOutcomeKey = template.DependsOnBranchOutcomeKey
                    }];
            foreach (var dependency in dependencies)
            {
                var dependsOnStepId = ResolveStepId(stepIdsByKey, dependency.DependsOnStepKey, process.Key, template.Key);
                Guid? dependsOnBranchOutcomeId = null;
                if (!string.IsNullOrWhiteSpace(dependency.DependsOnBranchOutcomeKey))
                {
                    dependsOnBranchOutcomeId = ResolveBranchOutcomeId(
                        branchOutcomeIdsByKey,
                        dependency.DependsOnStepKey,
                        dependency.DependsOnBranchOutcomeKey,
                        process.Key,
                        template.Key);
                }

                step.Dependencies.Add(new ProcessStepDependencyEditorModel
                {
                    Id = Guid.NewGuid(),
                    DependsOnStepId = dependsOnStepId,
                    DependsOnBranchOutcomeId = dependsOnBranchOutcomeId
                });
            }

            foreach (var assignment in template.RoleAssignments)
            {
                step.RoleAssignments.Add(new ProcessStepRoleRequirementEditorModel
                {
                    Id = Guid.NewGuid(),
                    RoleRequirementId = ResolveRoleId(
                        roleIdsByKey,
                        assignment.RoleKey,
                        process.Key,
                        template.Key,
                        "role assignment"),
                    ResponsibilityKind = EnumValueParser.ParseOrDefault(assignment.ResponsibilityKind, ProcessResponsibilityKind.Responsible),
                    IsRequired = assignment.IsRequired,
                    FallbackOrder = assignment.FallbackOrder,
                    RebindPolicySummary = assignment.RebindPolicySummary
                });
            }

            foreach (var artifactInput in template.ArtifactInputs)
            {
                step.ArtifactInputs.Add(new ProcessStepArtifactInputEditorModel
                {
                    Id = Guid.NewGuid(),
                    ArtifactExpectationId = ResolveArtifactExpectationId(
                        artifactExpectationIdsByKey,
                        artifactInput.SourceStepKey,
                        artifactInput.ArtifactExpectationKey,
                        process.Key,
                        template.Key)
                });
            }
        }

        return definition;
    }

    private void ApplyBalancedFlowComposition(ProcessDefinitionEditorModel definition)
    {
        recompositionService.Apply(definition, ProcessCanvasRecompositionMode.Recompose);
    }

    private static ProcessTemplateDefinition GetProcess(ProcessTemplatePack pack, string processKey)
    {
        if (!pack.Processes.TryGetValue(processKey, out var process))
        {
            throw new InvalidOperationException($"Process template '{processKey}' was not found in the template pack.");
        }

        return process;
    }

    private static ProcessTemplateRoleResource? ResolveRoleResource(
        ProcessTemplatePack pack,
        ProcessTemplateDefinition process,
        ProcessTemplateRoleUsage usage)
    {
        if (!string.IsNullOrWhiteSpace(usage.RoleResourceKey))
        {
            var localRole = process.LocalRoles.FirstOrDefault(item => string.Equals(item.Key, usage.RoleResourceKey, StringComparison.OrdinalIgnoreCase));
            if (localRole is not null)
            {
                return localRole;
            }

            if (pack.SharedRoles.TryGetValue(usage.RoleResourceKey, out var sharedRole))
            {
                return sharedRole;
            }
        }

        var usageLocalRole = process.LocalRoles.FirstOrDefault(item => string.Equals(item.Key, usage.Key, StringComparison.OrdinalIgnoreCase));
        if (usageLocalRole is not null)
        {
            return usageLocalRole;
        }

        return pack.SharedRoles.GetValueOrDefault(usage.Key);
    }

    private static ProcessTemplateArtifactResource? ResolveArtifactResource(
        ProcessTemplatePack pack,
        ProcessTemplateDefinition process,
        string artifactKey)
    {
        if (string.IsNullOrWhiteSpace(artifactKey))
        {
            return null;
        }

        var localArtifact = process.LocalArtifacts.FirstOrDefault(item => string.Equals(item.Key, artifactKey, StringComparison.OrdinalIgnoreCase));
        if (localArtifact is not null)
        {
            return localArtifact;
        }

        return pack.SharedArtifacts.GetValueOrDefault(artifactKey);
    }

    private static Guid? ResolveRoleId(
        IReadOnlyDictionary<string, Guid> roleIdsByKey,
        string roleKey,
        string processKey,
        string stepKey,
        string referenceKind)
    {
        if (string.IsNullOrWhiteSpace(roleKey))
        {
            return null;
        }

        if (roleIdsByKey.TryGetValue(roleKey, out var roleId))
        {
            return roleId;
        }

        throw new InvalidOperationException(
            $"Process template '{processKey}' step '{stepKey}' references unknown {referenceKind} '{roleKey}'.");
    }

    private static Guid ResolveStepId(
        IReadOnlyDictionary<string, Guid> stepIdsByKey,
        string stepKey,
        string processKey,
        string ownerStepKey)
    {
        if (!stepIdsByKey.TryGetValue(stepKey, out var stepId))
        {
            throw new InvalidOperationException(
                $"Process template '{processKey}' step '{ownerStepKey}' depends on unknown step '{stepKey}'.");
        }

        return stepId;
    }

    private static Guid ResolveBranchOutcomeId(
        IReadOnlyDictionary<string, Guid> branchOutcomeIdsByKey,
        string sourceStepKey,
        string branchOutcomeKey,
        string processKey,
        string ownerStepKey)
    {
        var compositeKey = BuildCompositeKey(sourceStepKey, branchOutcomeKey);
        if (!branchOutcomeIdsByKey.TryGetValue(compositeKey, out var branchOutcomeId))
        {
            throw new InvalidOperationException(
                $"Process template '{processKey}' step '{ownerStepKey}' depends on unknown branch outcome '{branchOutcomeKey}' from step '{sourceStepKey}'.");
        }

        return branchOutcomeId;
    }

    private static Guid ResolveArtifactExpectationId(
        IReadOnlyDictionary<string, Guid> artifactExpectationIdsByKey,
        string sourceStepKey,
        string artifactExpectationKey,
        string processKey,
        string ownerStepKey)
    {
        var compositeKey = BuildCompositeKey(sourceStepKey, artifactExpectationKey);
        if (!artifactExpectationIdsByKey.TryGetValue(compositeKey, out var artifactExpectationId))
        {
            throw new InvalidOperationException(
                $"Process template '{processKey}' step '{ownerStepKey}' references unknown artifact expectation '{artifactExpectationKey}' from step '{sourceStepKey}'.");
        }

        return artifactExpectationId;
    }

    private static string BuildCompositeKey(string left, string right)
    {
        return left.Trim() + "::" + right.Trim();
    }

}
