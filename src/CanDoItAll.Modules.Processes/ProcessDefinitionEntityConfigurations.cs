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
        builder.Property(definition => definition.ConcurrencyToken).IsConcurrencyToken();
        builder.HasIndex(definition => definition.ProjectId);
        builder.HasIndex(definition => definition.Slug).IsUnique();
        builder.HasIndex(definition => definition.Status);
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
        builder.Property(version => version.ImportedFrom).HasMaxLength(200);
        builder.Property(version => version.ImportWarnings).HasColumnType("TEXT");
        builder.Property(version => version.PublishedBy).HasMaxLength(160);
        builder.Property(version => version.ConcurrencyToken).IsConcurrencyToken();
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
        builder.Property(step => step.InputContractSummary).HasColumnType("TEXT");
        builder.Property(step => step.OutputContractSummary).HasColumnType("TEXT");
        builder.Property(step => step.EvidenceContractSummary).HasColumnType("TEXT");
        builder.Property(step => step.DecisionRightsSummary).HasColumnType("TEXT");
        builder.Property(step => step.ExceptionPolicySummary).HasColumnType("TEXT");
        builder.HasIndex(step => new { step.ProcessDefinitionVersionId, step.OrderIndex });
        builder.HasIndex(step => new { step.ProcessDefinitionVersionId, step.Key }).IsUnique();
        builder.HasIndex(step => step.DependsOnStepId);
        builder.HasIndex(step => step.DependsOnBranchOutcomeId);
        builder.HasIndex(step => step.DecisionRoleRequirementId);
        builder.HasOne<ProcessDefinitionVersion>()
            .WithMany()
            .HasForeignKey(step => step.ProcessDefinitionVersionId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ProcessRoleRequirement>()
            .WithMany()
            .HasForeignKey(step => step.DecisionRoleRequirementId)
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
        builder.HasIndex(item => new { item.StepDefinitionId, item.DependsOnStepId, item.DependsOnBranchOutcomeId }).IsUnique();
        builder.HasIndex(item => new { item.StepDefinitionId, item.DisplayOrder });
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
    }
}
