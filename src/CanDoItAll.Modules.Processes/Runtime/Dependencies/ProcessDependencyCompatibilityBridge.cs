namespace CanDoItAll.Modules.Processes;

internal static class ProcessDependencyCompatibilityBridge
{
    public static ProcessDefinitionImportExportModel ToImportExportModel(ProcessDefinitionEditorModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        return new ProcessDefinitionImportExportModel
        {
            Id = model.Id,
            ProjectId = model.ProjectId,
            WorkingVersionId = model.WorkingVersionId,
            DefinitionConcurrencyToken = model.DefinitionConcurrencyToken,
            WorkingVersionConcurrencyToken = model.WorkingVersionConcurrencyToken,
            WorkingVersionNumber = model.WorkingVersionNumber,
            Name = model.Name,
            Summary = model.Summary,
            ValueStatement = model.ValueStatement,
            CustomerName = model.CustomerName,
            OwnerName = model.OwnerName,
            InterfaceContractSummary = model.InterfaceContractSummary,
            GovernanceNotes = model.GovernanceNotes,
            ChangeSummary = model.ChangeSummary,
            GovernancePolicySummary = model.GovernancePolicySummary,
            ConstitutionRuleSummary = model.ConstitutionRuleSummary,
            OperatingModeSummary = model.OperatingModeSummary,
            SimulationReadinessSummary = model.SimulationReadinessSummary,
            ManagerAgentOverrideId = model.ManagerAgentOverrideId,
            ManagerAgentOverrideName = model.ManagerAgentOverrideName,
            Criticality = model.Criticality,
            AutonomyLevel = model.AutonomyLevel,
            Status = model.Status,
            Roles = model.Roles
                .Select(CloneRole)
                .ToList(),
            MessagingPolicies = model.MessagingPolicies
                .Select(CloneMessagingPolicy)
                .ToList(),
            Steps = model.Steps
                .Select(ToImportExportStep)
                .ToList()
        };
    }

    public static ProcessDefinitionEditorModel ToEditorModel(ProcessDefinitionImportExportModel definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        return new ProcessDefinitionEditorModel
        {
            Id = definition.Id,
            ProjectId = definition.ProjectId,
            WorkingVersionId = definition.WorkingVersionId,
            DefinitionConcurrencyToken = definition.DefinitionConcurrencyToken,
            WorkingVersionConcurrencyToken = definition.WorkingVersionConcurrencyToken,
            WorkingVersionNumber = definition.WorkingVersionNumber,
            Name = definition.Name,
            Summary = definition.Summary,
            ValueStatement = definition.ValueStatement,
            CustomerName = definition.CustomerName,
            OwnerName = definition.OwnerName,
            InterfaceContractSummary = definition.InterfaceContractSummary,
            GovernanceNotes = definition.GovernanceNotes,
            ChangeSummary = definition.ChangeSummary,
            GovernancePolicySummary = definition.GovernancePolicySummary,
            ConstitutionRuleSummary = definition.ConstitutionRuleSummary,
            OperatingModeSummary = definition.OperatingModeSummary,
            SimulationReadinessSummary = definition.SimulationReadinessSummary,
            ManagerAgentOverrideId = definition.ManagerAgentOverrideId,
            ManagerAgentOverrideName = definition.ManagerAgentOverrideName,
            Criticality = definition.Criticality,
            AutonomyLevel = definition.AutonomyLevel,
            Status = definition.Status,
            Roles = definition.Roles
                .Select(CloneRole)
                .ToList(),
            MessagingPolicies = definition.MessagingPolicies
                .Select(CloneMessagingPolicy)
                .ToList(),
            Steps = definition.Steps
                .Select(ToEditorStep)
                .ToList()
        };
    }

    private static ProcessRoleEditorModel CloneRole(ProcessRoleEditorModel role)
    {
        return new ProcessRoleEditorModel
        {
            Id = role.Id,
            Key = role.Key,
            DisplayName = role.DisplayName,
            Purpose = role.Purpose,
            StaffingIntent = role.StaffingIntent,
            PreferredExecutorKind = role.PreferredExecutorKind,
            PreferredProjectAssignmentRole = role.PreferredProjectAssignmentRole,
            IsRequired = role.IsRequired,
            AllowsFallback = role.AllowsFallback,
            RequiresExplicitApproval = role.RequiresExplicitApproval,
            DefaultAllocationPercent = role.DefaultAllocationPercent,
            RoleTemplateSourceKey = role.RoleTemplateSourceKey,
            RoleTemplateSnapshotName = role.RoleTemplateSnapshotName,
            SnapshotSummary = role.SnapshotSummary,
            RequiredSkillIds = role.RequiredSkillIds.ToList(),
            CanvasX = role.CanvasX,
            CanvasY = role.CanvasY
        };
    }

    private static ProcessRoleMessagingPolicyEditorModel CloneMessagingPolicy(ProcessRoleMessagingPolicyEditorModel policy)
    {
        return new ProcessRoleMessagingPolicyEditorModel
        {
            Id = policy.Id,
            SourceRoleRequirementId = policy.SourceRoleRequirementId,
            TargetRoleRequirementId = policy.TargetRoleRequirementId
        };
    }

    private static ProcessStepImportExportModel ToImportExportStep(ProcessStepEditorModel step)
    {
        return new ProcessStepImportExportModel
        {
            Id = step.Id,
            Key = step.Key,
            Title = step.Title,
            Subtitle = step.Subtitle,
            Notes = step.Notes,
            StepKind = step.StepKind,
            SubprocessDefinitionId = step.SubprocessDefinitionId,
            SubprocessDefinitionSnapshotName = step.SubprocessDefinitionSnapshotName,
            AllowsManualSkip = step.AllowsManualSkip,
            AllowsSafeRefusal = step.AllowsSafeRefusal,
            RequiresApproval = step.RequiresApproval,
            RequiresDecisionRecord = step.RequiresDecisionRecord,
            InputContractSummary = step.InputContractSummary,
            OutputContractSummary = step.OutputContractSummary,
            EvidenceContractSummary = step.EvidenceContractSummary,
            DecisionRightsSummary = step.DecisionRightsSummary,
            ExceptionPolicySummary = step.ExceptionPolicySummary,
            TargetLeadHours = step.TargetLeadHours,
            DecisionRoleRequirementId = step.DecisionRoleRequirementId,
            CanvasX = step.CanvasX,
            CanvasY = step.CanvasY,
            BranchCanvasX = step.BranchCanvasX,
            BranchCanvasY = step.BranchCanvasY,
            BranchOutcomes = step.BranchOutcomes
                .Select(
                    outcome => new ProcessStepBranchOutcomeEditorModel
                    {
                        Id = outcome.Id,
                        Key = outcome.Key,
                        Title = outcome.Title,
                        Description = outcome.Description
                    })
                .ToList(),
            Dependencies = ProcessStepDependencyCollection.GetOrderedEditorDependencies(step)
                .Select(
                    dependency => new ProcessStepDependencyEditorModel
                    {
                        Id = dependency.Id,
                        DependsOnStepId = dependency.DependsOnStepId,
                        DependsOnBranchOutcomeId = dependency.DependsOnBranchOutcomeId
                    })
                .ToList(),
            RoleAssignments = step.RoleAssignments
                .Select(
                    assignment => new ProcessStepRoleRequirementEditorModel
                    {
                        Id = assignment.Id,
                        RoleRequirementId = assignment.RoleRequirementId,
                        ResponsibilityKind = assignment.ResponsibilityKind,
                        IsRequired = assignment.IsRequired,
                        FallbackOrder = assignment.FallbackOrder,
                        RebindPolicySummary = assignment.RebindPolicySummary
                    })
                .ToList(),
            ArtifactExpectations = step.ArtifactExpectations
                .Select(
                    artifact => new ProcessArtifactExpectationEditorModel
                    {
                        Id = artifact.Id,
                        ArtifactKind = artifact.ArtifactKind,
                        Title = artifact.Title,
                        IsRequired = artifact.IsRequired,
                        TrustRequirement = artifact.TrustRequirement,
                        SensitivityLevel = artifact.SensitivityLevel,
                        RetentionDays = artifact.RetentionDays,
                        AllowedFutureUsageSummary = artifact.AllowedFutureUsageSummary,
                        ValidationRequirementSummary = artifact.ValidationRequirementSummary
                    })
                .ToList(),
            ArtifactInputs = step.ArtifactInputs
                .Select(
                    artifactInput => new ProcessStepArtifactInputEditorModel
                    {
                        Id = artifactInput.Id,
                        ArtifactExpectationId = artifactInput.ArtifactExpectationId
                    })
                .ToList()
        };
    }

    private static ProcessStepEditorModel ToEditorStep(ProcessStepImportExportModel step)
    {
        var editorStep = new ProcessStepEditorModel
        {
            Id = step.Id,
            Key = step.Key,
            Title = step.Title,
            Subtitle = step.Subtitle,
            Notes = step.Notes,
            StepKind = step.StepKind,
            SubprocessDefinitionId = step.SubprocessDefinitionId,
            SubprocessDefinitionSnapshotName = step.SubprocessDefinitionSnapshotName,
            AllowsManualSkip = step.AllowsManualSkip,
            AllowsSafeRefusal = step.AllowsSafeRefusal,
            RequiresApproval = step.RequiresApproval,
            RequiresDecisionRecord = step.RequiresDecisionRecord,
            InputContractSummary = step.InputContractSummary,
            OutputContractSummary = step.OutputContractSummary,
            EvidenceContractSummary = step.EvidenceContractSummary,
            DecisionRightsSummary = step.DecisionRightsSummary,
            ExceptionPolicySummary = step.ExceptionPolicySummary,
            TargetLeadHours = step.TargetLeadHours,
            DecisionRoleRequirementId = step.DecisionRoleRequirementId,
            CanvasX = step.CanvasX,
            CanvasY = step.CanvasY,
            BranchCanvasX = step.BranchCanvasX,
            BranchCanvasY = step.BranchCanvasY,
            BranchOutcomes = step.BranchOutcomes
                .Select(
                    outcome => new ProcessStepBranchOutcomeEditorModel
                    {
                        Id = outcome.Id,
                        Key = outcome.Key,
                        Title = outcome.Title,
                        Description = outcome.Description
                    })
                .ToList(),
            RoleAssignments = step.RoleAssignments
                .Select(
                    assignment => new ProcessStepRoleRequirementEditorModel
                    {
                        Id = assignment.Id,
                        RoleRequirementId = assignment.RoleRequirementId,
                        ResponsibilityKind = assignment.ResponsibilityKind,
                        IsRequired = assignment.IsRequired,
                        FallbackOrder = assignment.FallbackOrder,
                        RebindPolicySummary = assignment.RebindPolicySummary
                    })
                .ToList(),
            ArtifactExpectations = step.ArtifactExpectations
                .Select(
                    artifact => new ProcessArtifactExpectationEditorModel
                    {
                        Id = artifact.Id,
                        ArtifactKind = artifact.ArtifactKind,
                        Title = artifact.Title,
                        IsRequired = artifact.IsRequired,
                        TrustRequirement = artifact.TrustRequirement,
                        SensitivityLevel = artifact.SensitivityLevel,
                        RetentionDays = artifact.RetentionDays,
                        AllowedFutureUsageSummary = artifact.AllowedFutureUsageSummary,
                        ValidationRequirementSummary = artifact.ValidationRequirementSummary
                    })
                .ToList(),
            ArtifactInputs = step.ArtifactInputs
                .Select(
                    artifactInput => new ProcessStepArtifactInputEditorModel
                    {
                        Id = artifactInput.Id,
                        ArtifactExpectationId = artifactInput.ArtifactExpectationId
                    })
                .ToList()
        };

        ProcessStepDependencyCollection.SetEditorDependencies(editorStep, ResolveCanonicalDependencies(step));
        return editorStep;
    }

    private static IReadOnlyList<ProcessStepDependencyEditorModel> ResolveCanonicalDependencies(ProcessStepImportExportModel step)
    {
        var dependencies = step.Dependencies
            .Where(dependency => dependency.DependsOnStepId.HasValue && dependency.DependsOnStepId.Value != Guid.Empty)
            .Select(
                dependency => new ProcessStepDependencyEditorModel
                {
                    Id = dependency.Id ?? Guid.NewGuid(),
                    DependsOnStepId = dependency.DependsOnStepId,
                    DependsOnBranchOutcomeId = dependency.DependsOnBranchOutcomeId
                })
            .ToList();
        if (dependencies.Count > 0)
        {
            return dependencies;
        }

        if (!step.DependsOnStepId.HasValue || step.DependsOnStepId.Value == Guid.Empty)
        {
            return [];
        }

        return
        [
            ProcessStepDependencyCollection.CreateEditorDependency(
                step.DependsOnStepId.Value,
                step.DependsOnBranchOutcomeId)
        ];
    }
}
