using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Processes;

public sealed partial class ProcessesService
{
    private async Task<DefinitionChildrenSaveContext> LoadDefinitionChildrenSaveContextAsync(
        AppDbContext dbContext,
        Guid workingVersionId,
        CancellationToken cancellationToken)
    {
        var existingRoles = await dbContext.Set<ProcessRoleRequirement>()
            .Where(item => item.ProcessDefinitionVersionId == workingVersionId)
            .ToListAsync(cancellationToken);
        var existingSteps = await dbContext.Set<ProcessStepDefinition>()
            .Where(item => item.ProcessDefinitionVersionId == workingVersionId)
            .ToListAsync(cancellationToken);
        var existingRoleIds = existingRoles.Select(item => item.Id).ToHashSet();
        var existingStepIds = existingSteps.Select(item => item.Id).ToHashSet();
        var existingRoleSkills = await dbContext.Set<ProcessRoleSkillRequirement>()
            .Where(item => existingRoleIds.Contains(item.RoleRequirementId))
            .ToListAsync(cancellationToken);
        var existingMessagingPolicies = await dbContext.Set<ProcessRoleMessagingPolicyDefinition>()
            .Where(item => item.ProcessDefinitionVersionId == workingVersionId)
            .ToListAsync(cancellationToken);
        var existingDependencies = await dbContext.Set<ProcessStepDependencyDefinition>()
            .Where(item => existingStepIds.Contains(item.StepDefinitionId))
            .ToListAsync(cancellationToken);
        var existingAssignments = await dbContext.Set<ProcessStepRoleAssignmentRequirement>()
            .Where(item => existingStepIds.Contains(item.StepDefinitionId))
            .ToListAsync(cancellationToken);
        var existingArtifactExpectations = await dbContext.Set<ProcessArtifactExpectation>()
            .Where(item => existingStepIds.Contains(item.StepDefinitionId))
            .ToListAsync(cancellationToken);
        var existingArtifactInputs = await dbContext.Set<ProcessStepArtifactInputDefinition>()
            .Where(item => existingStepIds.Contains(item.StepDefinitionId))
            .ToListAsync(cancellationToken);
        var existingBranchOutcomes = await dbContext.Set<ProcessStepBranchOutcomeDefinition>()
            .Where(item => existingStepIds.Contains(item.StepDefinitionId))
            .ToListAsync(cancellationToken);

        return new DefinitionChildrenSaveContext(
            dbContext,
            workingVersionId,
            existingRoles,
            existingSteps,
            existingRoleSkills,
            existingMessagingPolicies,
            existingDependencies,
            existingAssignments,
            existingArtifactExpectations,
            existingArtifactInputs,
            existingBranchOutcomes);
    }

    private static Guid ResolveStableChildId(Guid? requestedId, HashSet<Guid> assignedIds, string childKind)
    {
        if (requestedId.HasValue && requestedId.Value != Guid.Empty)
        {
            if (!assignedIds.Add(requestedId.Value))
            {
                throw new InvalidOperationException($"Duplicate {childKind} id '{requestedId.Value:D}' detected during save.");
            }

            return requestedId.Value;
        }

        Guid generatedId;
        do
        {
            generatedId = Guid.NewGuid();
        } while (!assignedIds.Add(generatedId));

        return generatedId;
    }

    private static void RemoveDeletedDefinitionChildren(DefinitionChildrenSaveContext context)
    {
        context.DbContext.RemoveRange(context.ExistingArtifactInputs.Where(item => !context.RetainedArtifactInputIds.Contains(item.Id)).ToList());
        context.DbContext.RemoveRange(context.ExistingAssignments.Where(item => !context.RetainedAssignmentIds.Contains(item.Id)).ToList());
        context.DbContext.RemoveRange(context.ExistingDependencies.Where(item => !context.RetainedDependencyIds.Contains(item.Id)).ToList());
        context.DbContext.RemoveRange(context.ExistingBranchOutcomes.Where(item => !context.RetainedBranchOutcomeIds.Contains(item.Id)).ToList());
        context.DbContext.RemoveRange(context.ExistingArtifactExpectations.Where(item => !context.RetainedArtifactExpectationIds.Contains(item.Id)).ToList());
        context.DbContext.RemoveRange(context.ExistingMessagingPolicies.Where(item => !context.RetainedMessagingPolicyIds.Contains(item.Id)).ToList());
        context.DbContext.RemoveRange(context.ExistingRoleSkills.Where(item => !context.RetainedRoleSkillIds.Contains(item.Id)).ToList());
        context.DbContext.RemoveRange(context.ExistingSteps.Where(item => !context.RetainedStepIds.Contains(item.Id)).ToList());
        context.DbContext.RemoveRange(context.ExistingRoles.Where(item => !context.RetainedRoleIds.Contains(item.Id)).ToList());
    }

    private sealed class DefinitionChildrenSaveContext
    {
        public DefinitionChildrenSaveContext(
            AppDbContext dbContext,
            Guid workingVersionId,
            List<ProcessRoleRequirement> existingRoles,
            List<ProcessStepDefinition> existingSteps,
            List<ProcessRoleSkillRequirement> existingRoleSkills,
            List<ProcessRoleMessagingPolicyDefinition> existingMessagingPolicies,
            List<ProcessStepDependencyDefinition> existingDependencies,
            List<ProcessStepRoleAssignmentRequirement> existingAssignments,
            List<ProcessArtifactExpectation> existingArtifactExpectations,
            List<ProcessStepArtifactInputDefinition> existingArtifactInputs,
            List<ProcessStepBranchOutcomeDefinition> existingBranchOutcomes)
        {
            DbContext = dbContext;
            WorkingVersionId = workingVersionId;
            ExistingRoles = existingRoles;
            ExistingSteps = existingSteps;
            ExistingRoleSkills = existingRoleSkills;
            ExistingMessagingPolicies = existingMessagingPolicies;
            ExistingDependencies = existingDependencies;
            ExistingAssignments = existingAssignments;
            ExistingArtifactExpectations = existingArtifactExpectations;
            ExistingArtifactInputs = existingArtifactInputs;
            ExistingBranchOutcomes = existingBranchOutcomes;

            RolesById = existingRoles.ToDictionary(item => item.Id);
            StepsById = existingSteps.ToDictionary(item => item.Id);
            RoleSkillsById = existingRoleSkills.ToDictionary(item => item.Id);
            MessagingPoliciesById = existingMessagingPolicies.ToDictionary(item => item.Id);
            DependenciesById = existingDependencies.ToDictionary(item => item.Id);
            AssignmentsById = existingAssignments.ToDictionary(item => item.Id);
            ArtifactExpectationsById = existingArtifactExpectations.ToDictionary(item => item.Id);
            ArtifactInputsById = existingArtifactInputs.ToDictionary(item => item.Id);
            BranchOutcomesById = existingBranchOutcomes.ToDictionary(item => item.Id);
            ExistingRoleSkillsByRoleId = existingRoleSkills
                .GroupBy(item => item.RoleRequirementId)
                .ToDictionary(group => group.Key, group => group.ToDictionary(item => item.SkillId));
            ExistingBranchOutcomesByStepId = existingBranchOutcomes
                .GroupBy(item => item.StepDefinitionId)
                .ToDictionary(group => group.Key, group => group.ToList());
            ExistingDependenciesByStepId = existingDependencies
                .GroupBy(item => item.StepDefinitionId)
                .ToDictionary(group => group.Key, group => group.ToList());
            ExistingAssignmentsByStepId = existingAssignments
                .GroupBy(item => item.StepDefinitionId)
                .ToDictionary(group => group.Key, group => group.ToList());
            ExistingArtifactInputsByStepId = existingArtifactInputs
                .GroupBy(item => item.StepDefinitionId)
                .ToDictionary(group => group.Key, group => group.ToList());
        }

        public AppDbContext DbContext { get; }

        public Guid WorkingVersionId { get; }

        public List<ProcessRoleRequirement> ExistingRoles { get; }

        public List<ProcessStepDefinition> ExistingSteps { get; }

        public List<ProcessRoleSkillRequirement> ExistingRoleSkills { get; }

        public List<ProcessRoleMessagingPolicyDefinition> ExistingMessagingPolicies { get; }

        public List<ProcessStepDependencyDefinition> ExistingDependencies { get; }

        public List<ProcessStepRoleAssignmentRequirement> ExistingAssignments { get; }

        public List<ProcessArtifactExpectation> ExistingArtifactExpectations { get; }

        public List<ProcessStepArtifactInputDefinition> ExistingArtifactInputs { get; }

        public List<ProcessStepBranchOutcomeDefinition> ExistingBranchOutcomes { get; }

        public Dictionary<Guid, ProcessRoleRequirement> RolesById { get; }

        public Dictionary<Guid, ProcessStepDefinition> StepsById { get; }

        public Dictionary<Guid, ProcessRoleSkillRequirement> RoleSkillsById { get; }

        public Dictionary<Guid, ProcessRoleMessagingPolicyDefinition> MessagingPoliciesById { get; }

        public Dictionary<Guid, ProcessStepDependencyDefinition> DependenciesById { get; }

        public Dictionary<Guid, ProcessStepRoleAssignmentRequirement> AssignmentsById { get; }

        public Dictionary<Guid, ProcessArtifactExpectation> ArtifactExpectationsById { get; }

        public Dictionary<Guid, ProcessStepArtifactInputDefinition> ArtifactInputsById { get; }

        public Dictionary<Guid, ProcessStepBranchOutcomeDefinition> BranchOutcomesById { get; }

        public Dictionary<Guid, Dictionary<Guid, ProcessRoleSkillRequirement>> ExistingRoleSkillsByRoleId { get; }

        public Dictionary<Guid, List<ProcessStepBranchOutcomeDefinition>> ExistingBranchOutcomesByStepId { get; }

        public Dictionary<Guid, List<ProcessStepDependencyDefinition>> ExistingDependenciesByStepId { get; }

        public Dictionary<Guid, List<ProcessStepRoleAssignmentRequirement>> ExistingAssignmentsByStepId { get; }

        public Dictionary<Guid, List<ProcessStepArtifactInputDefinition>> ExistingArtifactInputsByStepId { get; }

        public Dictionary<Guid, Guid> RoleIdMap { get; } = [];

        public Dictionary<Guid, Guid> StepIdMap { get; } = [];

        public Dictionary<Guid, Guid> BranchOutcomeIdMap { get; } = [];

        public Dictionary<Guid, Guid> ArtifactExpectationIdMap { get; } = [];

        public HashSet<Guid> AssignedRoleIds { get; } = [];

        public HashSet<Guid> AssignedMessagingPolicyIds { get; } = [];

        public HashSet<Guid> AssignedStepIds { get; } = [];

        public HashSet<Guid> AssignedBranchOutcomeIds { get; } = [];

        public HashSet<Guid> AssignedDependencyIds { get; } = [];

        public HashSet<Guid> AssignedAssignmentIds { get; } = [];

        public HashSet<Guid> AssignedArtifactExpectationIds { get; } = [];

        public HashSet<Guid> AssignedArtifactInputIds { get; } = [];

        public HashSet<Guid> RetainedRoleIds { get; } = [];

        public HashSet<Guid> RetainedRoleSkillIds { get; } = [];

        public HashSet<Guid> RetainedMessagingPolicyIds { get; } = [];

        public HashSet<Guid> RetainedStepIds { get; } = [];

        public HashSet<Guid> RetainedBranchOutcomeIds { get; } = [];

        public HashSet<Guid> RetainedDependencyIds { get; } = [];

        public HashSet<Guid> RetainedAssignmentIds { get; } = [];

        public HashSet<Guid> RetainedArtifactExpectationIds { get; } = [];

        public HashSet<Guid> RetainedArtifactInputIds { get; } = [];
    }
}
