namespace CanDoItAll.Modules.Processes;

public sealed partial class ProcessesService
{
    private async Task<IReadOnlyList<ResolvedProcessRole>> PersistDefinitionRolesAsync(
        DefinitionChildrenSaveContext context,
        ProcessDefinitionEditorModel model,
        CancellationToken cancellationToken)
    {
        var resolvedRoles = new List<ResolvedProcessRole>(model.Roles.Count);
        for (var index = 0; index < model.Roles.Count; index++)
        {
            var roleModel = model.Roles[index];
            var roleId = ResolveStableChildId(roleModel.Id, context.AssignedRoleIds, "role");
            if (roleModel.Id.HasValue && roleModel.Id.Value != Guid.Empty)
            {
                context.RoleIdMap[roleModel.Id.Value] = roleId;
            }

            if (!context.RolesById.TryGetValue(roleId, out var role))
            {
                role = new ProcessRoleRequirement
                {
                    Id = roleId,
                    ProcessDefinitionVersionId = context.WorkingVersionId
                };

                await context.DbContext.Set<ProcessRoleRequirement>().AddAsync(role, cancellationToken);
                context.RolesById[roleId] = role;
            }

            role.ProcessDefinitionVersionId = context.WorkingVersionId;
            role.Key = string.IsNullOrWhiteSpace(roleModel.Key)
                ? BuildKey(roleModel.DisplayName, $"role-{index + 1}")
                : BuildKey(roleModel.Key, $"role-{index + 1}");
            role.DisplayName = roleModel.DisplayName.Trim();
            role.Purpose = roleModel.Purpose.Trim();
            role.StaffingIntent = roleModel.StaffingIntent.Trim();
            role.PreferredExecutorKind = roleModel.PreferredExecutorKind.Trim();
            role.PreferredProjectAssignmentRole = roleModel.PreferredProjectAssignmentRole;
            role.IsRequired = roleModel.IsRequired;
            role.AllowsFallback = roleModel.AllowsFallback;
            role.RequiresExplicitApproval = roleModel.RequiresExplicitApproval;
            role.DefaultAllocationPercent = Math.Clamp(roleModel.DefaultAllocationPercent, 0, 100);
            role.RoleTemplateSourceKey = roleModel.RoleTemplateSourceKey.Trim();
            role.RoleTemplateSnapshotName = roleModel.RoleTemplateSnapshotName.Trim();
            role.SnapshotSummary = roleModel.SnapshotSummary.Trim();
            role.DisplayOrder = index;
            role.CanvasX = roleModel.CanvasX;
            role.CanvasY = roleModel.CanvasY;

            context.RetainedRoleIds.Add(roleId);
            resolvedRoles.Add(new ResolvedProcessRole(roleId, roleModel));
        }

        foreach (var resolvedRole in resolvedRoles)
        {
            if (!context.ExistingRoleSkillsByRoleId.TryGetValue(resolvedRole.RoleId, out var existingRoleSkillsForRole))
            {
                existingRoleSkillsForRole = [];
                context.ExistingRoleSkillsByRoleId[resolvedRole.RoleId] = existingRoleSkillsForRole;
            }

            foreach (var skillId in resolvedRole.Model.RequiredSkillIds.Distinct())
            {
                if (skillId == Guid.Empty)
                {
                    continue;
                }

                if (!existingRoleSkillsForRole.TryGetValue(skillId, out var existingRoleSkill))
                {
                    existingRoleSkill = new ProcessRoleSkillRequirement
                    {
                        RoleRequirementId = resolvedRole.RoleId,
                        SkillId = skillId,
                        IsRequired = true
                    };

                    await context.DbContext.Set<ProcessRoleSkillRequirement>().AddAsync(existingRoleSkill, cancellationToken);
                    context.ExistingRoleSkills.Add(existingRoleSkill);
                    context.RoleSkillsById[existingRoleSkill.Id] = existingRoleSkill;
                    existingRoleSkillsForRole[skillId] = existingRoleSkill;
                }
                else
                {
                    existingRoleSkill.IsRequired = true;
                }

                context.RetainedRoleSkillIds.Add(existingRoleSkill.Id);
            }
        }

        var existingMessagingPoliciesByShape = context.ExistingMessagingPolicies
            .GroupBy(item => (item.SourceRoleRequirementId, item.TargetRoleRequirementId))
            .ToDictionary(group => group.Key, group => group.ToList());

        for (var index = 0; index < model.MessagingPolicies.Count; index++)
        {
            var messagingPolicyModel = model.MessagingPolicies[index];
            if (!messagingPolicyModel.SourceRoleRequirementId.HasValue ||
                !messagingPolicyModel.TargetRoleRequirementId.HasValue ||
                messagingPolicyModel.SourceRoleRequirementId.Value == Guid.Empty ||
                messagingPolicyModel.TargetRoleRequirementId.Value == Guid.Empty)
            {
                continue;
            }

            var sourceRoleId = context.RoleIdMap.TryGetValue(messagingPolicyModel.SourceRoleRequirementId.Value, out var remappedSourceRoleId)
                ? remappedSourceRoleId
                : messagingPolicyModel.SourceRoleRequirementId.Value;
            var targetRoleId = context.RoleIdMap.TryGetValue(messagingPolicyModel.TargetRoleRequirementId.Value, out var remappedTargetRoleId)
                ? remappedTargetRoleId
                : messagingPolicyModel.TargetRoleRequirementId.Value;
            if (!context.RolesById.ContainsKey(sourceRoleId) || !context.RolesById.ContainsKey(targetRoleId))
            {
                throw new InvalidOperationException("Messaging policy references a role that could not be resolved during save.");
            }

            ProcessRoleMessagingPolicyDefinition? messagingPolicy = null;
            var requestedMessagingPolicyId = ResolveStableChildId(
                messagingPolicyModel.Id,
                context.AssignedMessagingPolicyIds,
                "messaging policy");
            if (messagingPolicyModel.Id.HasValue &&
                messagingPolicyModel.Id.Value != Guid.Empty &&
                context.MessagingPoliciesById.TryGetValue(requestedMessagingPolicyId, out var existingMessagingPolicy))
            {
                messagingPolicy = existingMessagingPolicy;
            }
            else if ((!messagingPolicyModel.Id.HasValue || messagingPolicyModel.Id.Value == Guid.Empty) &&
                     existingMessagingPoliciesByShape.TryGetValue((sourceRoleId, targetRoleId), out var matchingPolicies))
            {
                messagingPolicy = matchingPolicies.FirstOrDefault(candidate => !context.RetainedMessagingPolicyIds.Contains(candidate.Id));
            }

            if (messagingPolicy is null)
            {
                messagingPolicy = new ProcessRoleMessagingPolicyDefinition
                {
                    Id = requestedMessagingPolicyId
                };

                await context.DbContext.Set<ProcessRoleMessagingPolicyDefinition>().AddAsync(messagingPolicy, cancellationToken);
                context.ExistingMessagingPolicies.Add(messagingPolicy);
                context.MessagingPoliciesById[messagingPolicy.Id] = messagingPolicy;
            }

            messagingPolicy.ProcessDefinitionVersionId = context.WorkingVersionId;
            messagingPolicy.SourceRoleRequirementId = sourceRoleId;
            messagingPolicy.TargetRoleRequirementId = targetRoleId;
            messagingPolicy.DisplayOrder = index;

            context.RetainedMessagingPolicyIds.Add(messagingPolicy.Id);
        }

        return resolvedRoles;
    }
}
