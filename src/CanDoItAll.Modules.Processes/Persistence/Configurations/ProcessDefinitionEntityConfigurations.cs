using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CanDoItAll.Modules.Processes;

internal sealed class ProcessDefinitionConfiguration : IEntityTypeConfiguration<ProcessDefinition>
{
    public void Configure(EntityTypeBuilder<ProcessDefinition> builder)
    {
        builder.ToTable("Processes_Definitions");
        builder.HasKey(definition => definition.Id);
        builder.Property(definition => definition.Name).HasMaxLength(200).IsRequired();
        builder.Property(definition => definition.Slug).HasMaxLength(200).IsRequired();
        builder.Property(definition => definition.Summary).HasColumnType("TEXT");
        builder.Property(definition => definition.ValueStatement).HasColumnType("TEXT");
        builder.Property(definition => definition.CustomerName).HasMaxLength(200);
        builder.Property(definition => definition.OwnerName).HasMaxLength(200);
        builder.Property(definition => definition.InterfaceContractSummary).HasColumnType("TEXT");
        builder.Property(definition => definition.GovernanceNotes).HasColumnType("TEXT");
        builder.Property(definition => definition.Criticality).HasConversion<string>().HasMaxLength(48);
        builder.Property(definition => definition.AutonomyLevel).HasConversion<string>().HasMaxLength(48);
        builder.Property(definition => definition.Status).HasConversion<string>().HasMaxLength(48);
        builder.Property(definition => definition.NextVersionNumber).HasDefaultValue(1);
        builder.Property(definition => definition.ConcurrencyToken).IsConcurrencyToken();
        builder.HasIndex(definition => definition.ProjectId);
        builder.HasIndex(definition => definition.Slug)
            .HasDatabaseName(ProcessPersistenceConstraintNames.DefinitionSlugUniqueIndex)
            .IsUnique();
        builder.HasIndex(definition => definition.Status);
        builder.HasIndex(definition => definition.ActivePublishedVersionId);
        builder.HasOne<ProcessDefinitionVersion>()
            .WithMany()
            .HasForeignKey(definition => new { definition.Id, definition.ActivePublishedVersionId })
            .HasPrincipalKey(version => new { version.ProcessDefinitionId, version.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ProcessDefinitionVersionConfiguration : IEntityTypeConfiguration<ProcessDefinitionVersion>
{
    public void Configure(EntityTypeBuilder<ProcessDefinitionVersion> builder)
    {
        builder.ToTable("Processes_DefinitionVersions");
        builder.HasKey(version => version.Id);
        builder.Property(version => version.Status).HasConversion<string>().HasMaxLength(48);
        builder.Property(version => version.ChangeSummary).HasColumnType("TEXT");
        builder.Property(version => version.GovernancePolicySummary).HasColumnType("TEXT");
        builder.Property(version => version.ConstitutionRuleSummary).HasColumnType("TEXT");
        builder.Property(version => version.OperatingModeSummary).HasColumnType("TEXT");
        builder.Property(version => version.SimulationReadinessSummary).HasColumnType("TEXT");
        builder.Property(version => version.ManagerAgentOverrideName).HasMaxLength(200);
        builder.Property(version => version.ImportedFrom).HasMaxLength(200);
        builder.Property(version => version.ImportWarnings).HasColumnType("TEXT");
        builder.Property(version => version.PublishedBy).HasMaxLength(160);
        builder.Property(version => version.ConcurrencyToken).IsConcurrencyToken();
        builder.HasIndex(version => version.ManagerAgentOverrideId);
        builder.HasAlternateKey(version => new { version.ProcessDefinitionId, version.Id });
        builder.HasIndex(version => new { version.ProcessDefinitionId, version.Status })
            .HasDatabaseName(ProcessPersistenceConstraintNames.VersionDraftPerDefinitionUniqueIndex)
            .HasFilter("\"Status\" = 'Draft'")
            .IsUnique();
        builder.HasIndex(version => version.ProcessDefinitionId)
            .HasDatabaseName(ProcessPersistenceConstraintNames.VersionPublishedPerDefinitionUniqueIndex)
            .HasFilter("\"Status\" = 'Published'")
            .IsUnique();
        builder.HasIndex(version => new { version.ProcessDefinitionId, version.VersionNumber }).IsUnique();
        builder.HasIndex(version => new { version.ProcessDefinitionId, version.Status });
        builder.HasOne<ProcessDefinition>()
            .WithMany()
            .HasForeignKey(version => version.ProcessDefinitionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class ProcessRoleRequirementConfiguration : IEntityTypeConfiguration<ProcessRoleRequirement>
{
    public void Configure(EntityTypeBuilder<ProcessRoleRequirement> builder)
    {
        builder.ToTable("Processes_RoleRequirements");
        builder.HasKey(role => role.Id);
        builder.Property(role => role.Key).HasMaxLength(120).IsRequired();
        builder.Property(role => role.DisplayName).HasMaxLength(160).IsRequired();
        builder.Property(role => role.Purpose).HasColumnType("TEXT");
        builder.Property(role => role.StaffingIntent).HasColumnType("TEXT");
        builder.Property(role => role.PreferredExecutorKind).HasMaxLength(80);
        builder.Property(role => role.PreferredProjectAssignmentRole).HasConversion<string>().HasMaxLength(64);
        builder.Property(role => role.RoleTemplateSourceKey).HasMaxLength(160);
        builder.Property(role => role.RoleTemplateSnapshotName).HasMaxLength(200);
        builder.Property(role => role.SnapshotSummary).HasColumnType("TEXT");
        builder.HasIndex(role => new { role.ProcessDefinitionVersionId, role.Key }).IsUnique();
        builder.HasOne<ProcessDefinitionVersion>()
            .WithMany()
            .HasForeignKey(role => role.ProcessDefinitionVersionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class ProcessRoleSkillRequirementConfiguration : IEntityTypeConfiguration<ProcessRoleSkillRequirement>
{
    public void Configure(EntityTypeBuilder<ProcessRoleSkillRequirement> builder)
    {
        builder.ToTable("Processes_RoleSkillRequirements");
        builder.HasKey(item => item.Id);
        builder.HasIndex(item => new { item.RoleRequirementId, item.SkillId }).IsUnique();
        builder.HasIndex(item => item.SkillId);
        builder.HasOne<ProcessRoleRequirement>()
            .WithMany()
            .HasForeignKey(item => item.RoleRequirementId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class ProcessRoleMessagingPolicyDefinitionConfiguration : IEntityTypeConfiguration<ProcessRoleMessagingPolicyDefinition>
{
    public void Configure(EntityTypeBuilder<ProcessRoleMessagingPolicyDefinition> builder)
    {
        builder.ToTable("Processes_RoleMessagingPolicies");
        builder.HasKey(item => item.Id);
        builder.HasIndex(item => new { item.ProcessDefinitionVersionId, item.SourceRoleRequirementId, item.TargetRoleRequirementId })
            .HasDatabaseName(ProcessPersistenceConstraintNames.DefinitionMessagingPolicyUniqueIndex)
            .IsUnique();
        builder.HasIndex(item => new { item.ProcessDefinitionVersionId, item.DisplayOrder });
        builder.HasIndex(item => item.SourceRoleRequirementId);
        builder.HasIndex(item => item.TargetRoleRequirementId);
        builder.HasOne<ProcessDefinitionVersion>()
            .WithMany()
            .HasForeignKey(item => item.ProcessDefinitionVersionId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ProcessRoleRequirement>()
            .WithMany()
            .HasForeignKey(item => item.SourceRoleRequirementId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ProcessRoleRequirement>()
            .WithMany()
            .HasForeignKey(item => item.TargetRoleRequirementId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ProcessStepDefinitionConfiguration : IEntityTypeConfiguration<ProcessStepDefinition>
{
    public void Configure(EntityTypeBuilder<ProcessStepDefinition> builder)
    {
        builder.ToTable("Processes_StepDefinitions");
        builder.HasKey(step => step.Id);
        builder.Property(step => step.Key).HasMaxLength(120).IsRequired();
        builder.Property(step => step.Title).HasMaxLength(200).IsRequired();
        builder.Property(step => step.Subtitle).HasMaxLength(200);
        builder.Property(step => step.Notes).HasColumnType("TEXT");
        builder.Property(step => step.StepKind).HasConversion<string>().HasMaxLength(48);
        builder.Property(step => step.SubprocessDefinitionSnapshotName).HasMaxLength(200);
        builder.Property(step => step.InputContractSummary).HasColumnType("TEXT");
        builder.Property(step => step.OutputContractSummary).HasColumnType("TEXT");
        builder.Property(step => step.EvidenceContractSummary).HasColumnType("TEXT");
        builder.Property(step => step.DecisionRightsSummary).HasColumnType("TEXT");
        builder.Property(step => step.ExceptionPolicySummary).HasColumnType("TEXT");
        builder.HasIndex(step => new { step.ProcessDefinitionVersionId, step.OrderIndex });
        builder.HasIndex(step => new { step.ProcessDefinitionVersionId, step.Key }).IsUnique();
        builder.HasIndex(step => step.DecisionRoleRequirementId);
        builder.HasIndex(step => step.SubprocessDefinitionId);
        builder.HasOne<ProcessDefinitionVersion>()
            .WithMany()
            .HasForeignKey(step => step.ProcessDefinitionVersionId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ProcessRoleRequirement>()
            .WithMany()
            .HasForeignKey(step => step.DecisionRoleRequirementId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ProcessDefinition>()
            .WithMany()
            .HasForeignKey(step => step.SubprocessDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ProcessStepDependencyDefinitionConfiguration : IEntityTypeConfiguration<ProcessStepDependencyDefinition>
{
    public void Configure(EntityTypeBuilder<ProcessStepDependencyDefinition> builder)
    {
        builder.ToTable("Processes_StepDependencies");
        builder.HasKey(item => item.Id);
        builder.HasIndex(item => item.StepDefinitionId);
        builder.HasIndex(item => item.DependsOnStepId);
        builder.HasIndex(item => item.DependsOnBranchOutcomeId);
        builder.HasIndex(item => new { item.StepDefinitionId, item.DependsOnStepId })
            .HasDatabaseName(ProcessPersistenceConstraintNames.StepDependencyUnconditionalUniqueIndex)
            .HasFilter("\"DependsOnBranchOutcomeId\" IS NULL")
            .IsUnique();
        builder.HasIndex(item => new { item.StepDefinitionId, item.DependsOnStepId, item.DependsOnBranchOutcomeId })
            .HasDatabaseName(ProcessPersistenceConstraintNames.StepDependencyConditionalUniqueIndex)
            .HasFilter("\"DependsOnBranchOutcomeId\" IS NOT NULL")
            .IsUnique();
        builder.HasIndex(item => new { item.StepDefinitionId, item.DisplayOrder });
        builder.HasOne<ProcessStepDefinition>()
            .WithMany()
            .HasForeignKey(item => item.StepDefinitionId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ProcessStepDefinition>()
            .WithMany()
            .HasForeignKey(item => item.DependsOnStepId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ProcessStepBranchOutcomeDefinition>()
            .WithMany()
            .HasForeignKey(item => item.DependsOnBranchOutcomeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ProcessStepBranchOutcomeDefinitionConfiguration : IEntityTypeConfiguration<ProcessStepBranchOutcomeDefinition>
{
    public void Configure(EntityTypeBuilder<ProcessStepBranchOutcomeDefinition> builder)
    {
        builder.ToTable("Processes_StepBranchOutcomes");
        builder.HasKey(outcome => outcome.Id);
        builder.Property(outcome => outcome.Key).HasMaxLength(120).IsRequired();
        builder.Property(outcome => outcome.Title).HasMaxLength(200).IsRequired();
        builder.Property(outcome => outcome.Description).HasColumnType("TEXT");
        builder.HasIndex(outcome => new { outcome.StepDefinitionId, outcome.Key }).IsUnique();
        builder.HasIndex(outcome => new { outcome.StepDefinitionId, outcome.DisplayOrder });
        builder.HasOne<ProcessStepDefinition>()
            .WithMany()
            .HasForeignKey(outcome => outcome.StepDefinitionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class ProcessStepRoleAssignmentRequirementConfiguration : IEntityTypeConfiguration<ProcessStepRoleAssignmentRequirement>
{
    public void Configure(EntityTypeBuilder<ProcessStepRoleAssignmentRequirement> builder)
    {
        builder.ToTable("Processes_StepRoleRequirements");
        builder.HasKey(requirement => requirement.Id);
        builder.Property(requirement => requirement.ResponsibilityKind).HasConversion<string>().HasMaxLength(48);
        builder.Property(requirement => requirement.RebindPolicySummary).HasColumnType("TEXT");
        builder.HasIndex(requirement => new { requirement.StepDefinitionId, requirement.RoleRequirementId, requirement.ResponsibilityKind }).IsUnique();
        builder.HasOne<ProcessStepDefinition>()
            .WithMany()
            .HasForeignKey(requirement => requirement.StepDefinitionId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ProcessRoleRequirement>()
            .WithMany()
            .HasForeignKey(requirement => requirement.RoleRequirementId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ProcessArtifactExpectationConfiguration : IEntityTypeConfiguration<ProcessArtifactExpectation>
{
    public void Configure(EntityTypeBuilder<ProcessArtifactExpectation> builder)
    {
        builder.ToTable("Processes_ArtifactExpectations");
        builder.HasKey(expectation => expectation.Id);
        builder.Property(expectation => expectation.ArtifactKind).HasConversion<string>().HasMaxLength(48);
        builder.Property(expectation => expectation.Title).HasMaxLength(160).IsRequired();
        builder.Property(expectation => expectation.TrustRequirement).HasConversion<string>().HasMaxLength(48);
        builder.Property(expectation => expectation.SensitivityLevel).HasConversion<string>().HasMaxLength(48);
        builder.Property(expectation => expectation.AllowedFutureUsageSummary).HasColumnType("TEXT");
        builder.Property(expectation => expectation.ValidationRequirementSummary).HasColumnType("TEXT");
        builder.HasIndex(expectation => expectation.StepDefinitionId);
        builder.HasOne<ProcessStepDefinition>()
            .WithMany()
            .HasForeignKey(expectation => expectation.StepDefinitionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class ProcessStepArtifactInputDefinitionConfiguration : IEntityTypeConfiguration<ProcessStepArtifactInputDefinition>
{
    public void Configure(EntityTypeBuilder<ProcessStepArtifactInputDefinition> builder)
    {
        builder.ToTable("Processes_StepArtifactInputs");
        builder.HasKey(item => item.Id);
        builder.HasIndex(item => item.StepDefinitionId);
        builder.HasIndex(item => item.ArtifactExpectationId);
        builder.HasIndex(item => new { item.StepDefinitionId, item.ArtifactExpectationId }).IsUnique();
        builder.HasIndex(item => new { item.StepDefinitionId, item.DisplayOrder });
        builder.HasOne<ProcessStepDefinition>()
            .WithMany()
            .HasForeignKey(item => item.StepDefinitionId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ProcessArtifactExpectation>()
            .WithMany()
            .HasForeignKey(item => item.ArtifactExpectationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
