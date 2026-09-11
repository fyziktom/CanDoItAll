using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Runtime;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CanDoItAll.Processes.Persistence;

public sealed class ProcessPreparedLaunchEntity {
    public Guid Id { get; set; }
    public Guid? CallerIntentId { get; set; }
    public long AdmissionSequence { get; set; }
    public Guid RunId { get; set; }
    public Guid PlanId { get; set; }
    public string RequestFingerprint { get; set; } = string.Empty;
    public string PreparationFingerprint { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = string.Empty;
    public DateTimeOffset PreparedAtUtc { get; set; }
    public DateTimeOffset? AcceptedAtUtc { get; set; }
    public bool? Execute { get; set; }
    public ProcessLaunchContinuationState State { get; set; }
    public Guid? ContinuationOwner { get; set; }
    public long ContinuationGeneration { get; set; }
    public DateTimeOffset? ContinuationLeaseExpiresAtUtc { get; set; }
    public ProcessLaunchLinkDeliveryState LinkDeliveryState { get; set; }
    public Guid? DeliveredLinkId { get; set; }
    public ProcessLaunchLinkConflictReason? LinkConflictReason { get; set; }
    public string? PublicFailure { get; set; }
}

internal sealed class ProcessPreparedLaunchEntityConfiguration : IEntityTypeConfiguration<ProcessPreparedLaunchEntity> {
    public void Configure(EntityTypeBuilder<ProcessPreparedLaunchEntity> builder) {
        builder.ToTable("process_prepared_launches");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.AdmissionSequence).ValueGeneratedOnAdd();
        builder.Property(item => item.RequestFingerprint).HasMaxLength(71).IsRequired();
        builder.Property(item => item.PreparationFingerprint).HasMaxLength(71).IsRequired();
        builder.Property(item => item.PayloadJson).IsRequired();
        builder.Property(item => item.State).HasConversion<string>().HasMaxLength(32);
        builder.Property(item => item.LinkDeliveryState).HasConversion<string>().HasMaxLength(32);
        builder.Property(item => item.LinkConflictReason).HasConversion<string>().HasMaxLength(32);
        builder.Property(item => item.PublicFailure).HasMaxLength(2_000);
        builder.HasIndex(item => item.CallerIntentId).IsUnique();
        builder.HasIndex(item => item.RunId).IsUnique();
        builder.HasIndex(item => item.AdmissionSequence).IsUnique();
        builder.HasIndex(item => new { item.State, item.ContinuationLeaseExpiresAtUtc, item.AdmissionSequence });
        builder.HasIndex(item => new { item.LinkDeliveryState, item.AdmissionSequence });
    }
}

internal static class ProcessPreparedLaunchCodec {
    private static readonly JsonSerializerOptions Options = ProcessInstancePlanPersistenceMapper.CreateSerializerOptions();

    public static ProcessPreparedLaunchEntity ToEntity(ProcessPreparedLaunch preparation) {
        ArgumentNullException.ThrowIfNull(preparation);
        preparation.Authority?.Validate();
        preparation = preparation with { PreparedAtUtc = NormalizeTimestamp(preparation.PreparedAtUtc) };
        if (preparation.AdmissionId.Value == Guid.Empty || preparation.CallerIntentId is { } intent && intent.Value == Guid.Empty ||
                preparation.InitialCommit.InitialPlan is null ||
                preparation.InitialCommit.InitialAssignments is null || preparation.InitialCommit.InitialLaunchAdmission is not null ||
                preparation.InitialCommit.Mutation.State.LaunchAdmissionId != preparation.AdmissionId ||
                preparation.InitialCommit.OriginalState.LaunchAdmissionId != preparation.AdmissionId ||
                preparation.Authority is not null && preparation.InitialCommit.Mutation.State.ProjectAdmission != preparation.Authority.ProjectAdmission ||
                preparation.Authority is null && preparation.CallerIntentId is not null ||
                preparation.PreparedAtUtc.Offset != TimeSpan.Zero || preparation.RequestFingerprint.Length != 71) {
            throw new InvalidOperationException("The prepared process launch has an invalid admission, authority or immutable initial commit.");
        }
        var plan = preparation.InitialCommit.InitialPlan;
        if (plan.Header.PlanId != preparation.Review.PlanId || plan.Definition.DefinitionId != preparation.Review.DefinitionId ||
                plan.Definition.VersionId != preparation.Review.DefinitionVersionId || plan.PlanHash != preparation.Review.PlanHash ||
                !plan.Steps.Select(step => step.StepInstanceId).OrderBy(id => id.Value)
                    .SequenceEqual(preparation.Review.Steps.Select(step => step.StepInstanceId).OrderBy(id => id.Value))) {
            throw new InvalidOperationException("The process launch review does not describe the immutable prepared plan.");
        }
        var payload = JsonSerializer.Serialize(preparation, Options);
        return new() {
            Id = preparation.AdmissionId.Value,
            CallerIntentId = preparation.CallerIntentId?.Value,
            RunId = preparation.InitialCommit.Mutation.State.RunId.Value,
            PlanId = preparation.InitialCommit.InitialPlan.Header.PlanId.Value,
            RequestFingerprint = preparation.RequestFingerprint,
            PreparationFingerprint = Hash(payload),
            PayloadJson = payload,
            PreparedAtUtc = preparation.PreparedAtUtc,
            State = ProcessLaunchContinuationState.Prepared,
            LinkDeliveryState = preparation.LinkTarget is null ? ProcessLaunchLinkDeliveryState.NotRequested : ProcessLaunchLinkDeliveryState.Pending
        };
    }

    public static ProcessPreparedLaunchSnapshot Read(ProcessPreparedLaunchEntity entity) {
        if (Hash(entity.PayloadJson) != entity.PreparationFingerprint) {
            throw new InvalidOperationException($"Process launch admission '{entity.Id:D}' has inconsistent immutable evidence.");
        }
        var preparation = JsonSerializer.Deserialize<ProcessPreparedLaunch>(entity.PayloadJson, Options)
            ?? throw new InvalidOperationException($"Process launch admission '{entity.Id:D}' has no prepared payload.");
        preparation.Authority?.Validate();
        if (preparation.AdmissionId.Value != entity.Id || preparation.InitialCommit.Mutation.State.RunId.Value != entity.RunId ||
                preparation.InitialCommit.InitialPlan?.Header.PlanId.Value != entity.PlanId ||
                preparation.RequestFingerprint != entity.RequestFingerprint || preparation.CallerIntentId?.Value != entity.CallerIntentId ||
                preparation.PreparedAtUtc != entity.PreparedAtUtc) {
            throw new InvalidOperationException($"Process launch admission '{entity.Id:D}' does not match its durable identity.");
        }
        return new(preparation, entity.PreparationFingerprint, entity.AdmissionSequence, entity.State,
            entity.AcceptedAtUtc, entity.Execute, entity.LinkDeliveryState, entity.DeliveredLinkId, entity.PublicFailure) { LinkConflictReason = entity.LinkConflictReason };
    }

    public static void RequireInitialCommit(ProcessPreparedLaunchSnapshot saved, ProcessRuntimeCommitRequest request) {
        var reference = request.InitialLaunchAdmission
            ?? throw new InvalidOperationException("The prepared process launch requires its typed admission reference.");
        if (saved.Preparation.AdmissionId != reference.AdmissionId || saved.PreparationFingerprint != reference.PreparationFingerprint ||
                JsonSerializer.Serialize(request with { InitialLaunchAdmission = null }, Options) != JsonSerializer.Serialize(saved.Preparation.InitialCommit, Options) ||
                saved.AcceptedAtUtc is not null && saved.Execute != reference.Execute) {
            throw new ProcessLaunchIntentConflictException(saved.Preparation.CallerIntentId,
                "The process launch retry changed the accepted plan, state, assignments or execution choice.");
        }
    }

    internal static DateTimeOffset NormalizeTimestamp(DateTimeOffset value) {
        var utc = value.ToUniversalTime();
        return new(utc.Ticks - utc.Ticks % 10, TimeSpan.Zero);
    }

    private static string Hash(string value) => "sha256:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
