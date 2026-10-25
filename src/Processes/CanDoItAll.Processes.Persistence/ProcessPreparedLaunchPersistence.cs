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
    public ProcessAcceptedSourceVersion AcceptedSourceVersion { get; set; }
    public string? AcceptedSourceHeaderJson { get; set; }
    public string? AcceptedSourceHeaderFingerprint { get; set; }
    public bool? Execute { get; set; }
    public ProcessLaunchContinuationState State { get; set; }
    public Guid? ContinuationOwner { get; set; }
    public long ContinuationGeneration { get; set; }
    public DateTimeOffset? ContinuationLeaseExpiresAtUtc { get; set; }
    public ProcessLaunchLinkDeliveryState LinkDeliveryState { get; set; }
    public Guid? DeliveredLinkId { get; set; }
    public ProcessLaunchLinkConflictReason? LinkConflictReason { get; set; }
    public string? PublicFailure { get; set; }

    public bool ReferencesProject() {
        var preparation = ProcessPreparedLaunchCodec.Read(this).Preparation;
        return LinkDeliveryState != ProcessLaunchLinkDeliveryState.NotRequested || DeliveredLinkId.HasValue ||
            preparation.Authority?.ProjectAdmission is not null || preparation.LinkTarget is not null ||
            preparation.ToolSource?.Execution.SourceAuthority?.ProjectAdmission is not null ||
            preparation.Request.ProjectId.HasValue || !string.IsNullOrWhiteSpace(preparation.Request.ProjectNodeId) ||
            HasProjectVariables(preparation.Request.Variables) ||
            preparation.InitialCommit.OriginalState.ProjectAdmission is not null ||
            preparation.InitialCommit.Mutation.State.ProjectAdmission is not null ||
            preparation.InitialCommit.InitialAssignments!.Any(assignment => HasProjectVariables(assignment.LaunchVariables));

        static bool HasProjectVariables(IReadOnlyDictionary<string, string> variables)
            => variables.ContainsKey(ProcessRuntimeLaunchVariables.ProjectId) || variables.ContainsKey(ProcessRuntimeLaunchVariables.ProjectNodeId);
    }
}

internal sealed class ProcessPreparedLaunchEntityConfiguration : IEntityTypeConfiguration<ProcessPreparedLaunchEntity> {
    public void Configure(EntityTypeBuilder<ProcessPreparedLaunchEntity> builder) {
        builder.ToTable("process_prepared_launches", table => table.HasCheckConstraint("CK_process_prepared_launch_source", $$"""
            ("AcceptedSourceVersion" = 0 AND "AcceptedSourceHeaderJson" IS NULL AND "AcceptedSourceHeaderFingerprint" IS NULL)
            OR ("AcceptedSourceVersion" = 1 AND "AcceptedAtUtc" IS NOT NULL
                AND "AcceptedSourceHeaderJson" IS NOT NULL AND octet_length("AcceptedSourceHeaderJson") BETWEEN 2 AND {{ProcessAcceptedSourceCodec.MaximumPayloadUtf8Bytes}}
                AND "AcceptedSourceHeaderFingerprint" IS NOT NULL AND "AcceptedSourceHeaderFingerprint" ~ '^sha256:[0-9a-f]{64}$')
            """));
        builder.HasKey(item => item.Id);
        builder.Property(item => item.AdmissionSequence).ValueGeneratedOnAdd();
        builder.Property(item => item.RequestFingerprint).HasMaxLength(71).IsRequired();
        builder.Property(item => item.PreparationFingerprint).HasMaxLength(71).IsRequired();
        builder.Property(item => item.PayloadJson).IsRequired();
        builder.Property(item => item.AcceptedSourceVersion).HasConversion<int>().HasDefaultValue(ProcessAcceptedSourceVersion.LegacyPreparedLaunch);
        builder.Property(item => item.AcceptedSourceHeaderFingerprint).HasMaxLength(71);
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
    internal const int MaximumGovernedPayloadUtf8Bytes = 33_554_432;
    private const string ToolSourceHashDomain = "process-tool-source-v1\n";
    private const string ProjectMutationHashDomain = "process-project-mutations-v1\n";
    private const string SourceLifetimeHashDomain = "process-agent-source-lifetime-v2\n";
    private const string DecisionPolicyHashDomain = "process-decision-policy-v1\n";
    private const string RootDecisionPolicyHashDomain = "process-root-decision-policy-v1\n";
    private const string SharedRunScopeHashDomain = "process-shared-run-scope-v1\n";
    private const string DecisionSelectionHashDomain = "process-launch-decision-selection/v1\n";
    private static readonly JsonSerializerOptions Options = ProcessInstancePlanPersistenceMapper.CreateSerializerOptions();

    public static ProcessPreparedLaunchEntity ToEntity(ProcessPreparedLaunch preparation) {
        ArgumentNullException.ThrowIfNull(preparation);
        preparation.Authority?.Validate();
        preparation.ToolSource?.RequirePreparation(preparation);
        preparation = preparation with { PreparedAtUtc = NormalizeTimestamp(preparation.PreparedAtUtc) };
        if (preparation.AdmissionId.Value == Guid.Empty || preparation.CallerIntentId is { } intent && intent.Value == Guid.Empty ||
                preparation.InitialCommit.InitialPlan is null || preparation.InitialCommit.DecisionBudget is not null ||
                preparation.InitialCommit.Decision is not null || preparation.InitialCommit.DecisionMaterial is not null || preparation.InitialCommit.DecisionRunScope is not null ||
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
        RequireDecisionPolicyBindings(preparation);
        var payload = preparation.InitialCommit.InitialAssignments.Any(item => item.DecisionDataPolicy is not null)
            ? ProcessBoundedJson.Serialize(preparation, Options, MaximumGovernedPayloadUtf8Bytes)
            : JsonSerializer.Serialize(preparation, Options);
        return new() {
            Id = preparation.AdmissionId.Value,
            CallerIntentId = preparation.CallerIntentId?.Value,
            RunId = preparation.InitialCommit.Mutation.State.RunId.Value,
            PlanId = preparation.InitialCommit.InitialPlan.Header.PlanId.Value,
            RequestFingerprint = preparation.RequestFingerprint,
            PreparationFingerprint = HashPayload(preparation, payload),
            PayloadJson = payload,
            PreparedAtUtc = preparation.PreparedAtUtc,
            State = ProcessLaunchContinuationState.Prepared,
            LinkDeliveryState = preparation.LinkTarget is null ? ProcessLaunchLinkDeliveryState.NotRequested : ProcessLaunchLinkDeliveryState.Pending
        };
    }

    public static ProcessPreparedLaunchSnapshot Read(ProcessPreparedLaunchEntity entity) {
        var preparation = JsonSerializer.Deserialize<ProcessPreparedLaunch>(entity.PayloadJson, Options)
            ?? throw new InvalidOperationException($"Process launch admission '{entity.Id:D}' has no prepared payload.");
        if (HashPayload(preparation, entity.PayloadJson) != entity.PreparationFingerprint) {
            throw new InvalidOperationException($"Process launch admission '{entity.Id:D}' has inconsistent immutable evidence.");
        }
        preparation.Authority?.Validate();
        preparation.ToolSource?.RequirePreparation(preparation);
        RequireDecisionPolicyBindings(preparation);
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

    private static string HashPayload(ProcessPreparedLaunch preparation, string payload) {
        preparation.ToolSource?.Validate();
        var sourceDomain = preparation.ToolSource is null ? string.Empty : ToolSourceHashDomain;
        var projectDomain = preparation.Authority?.ProjectMutations is not null ||
            preparation.ToolSource?.Execution.SourceAuthority?.ProjectMutations is not null ? ProjectMutationHashDomain : string.Empty;
        var lifetimeDomain = HasSourceLifetimeSchema(preparation.Authority) || HasSourceLifetimeSchema(preparation.ToolSource?.Execution.SourceAuthority)
            ? SourceLifetimeHashDomain : string.Empty;
        var decisionDomain = preparation.InitialCommit.InitialAssignments?.Any(assignment => assignment.DecisionDataPolicy is not null) == true
            ? DecisionPolicyHashDomain : string.Empty;
        var rootDecisionDomain = preparation.InitialCommit.InitialRunDecisionAdmission is null ? string.Empty : RootDecisionPolicyHashDomain;
        var scopeDomain = preparation.InitialCommit.InitialRunDecisionAdmission?.MaterialScope is null ? string.Empty : SharedRunScopeHashDomain;
        var selectionDomain = preparation.Request.Decision is null ? string.Empty : DecisionSelectionHashDomain;
        return Hash(selectionDomain + scopeDomain + rootDecisionDomain + decisionDomain + lifetimeDomain + projectDomain + sourceDomain + payload);
    }

    private static void RequireDecisionPolicyBindings(ProcessPreparedLaunch preparation) {
        if (preparation.InitialCommit.InitialPlan is not { } plan || preparation.InitialCommit.InitialAssignments is not { } assignments) {
            throw new InvalidDataException("The prepared Process launch requires its original plan and assignments.");
        }
        foreach (var assignment in assignments) {
            assignment.DecisionDataPolicy?.RequireBinding(plan.Definition.VersionId, assignment.StepInstanceId);
            preparation.InitialCommit.InitialRunDecisionAdmission?.RequireChild(assignment.DecisionDataPolicy
                ?? throw new InvalidDataException("A governed root requires the original funded policy of every assignment."));
        }
        preparation.InitialCommit.InitialRunDecisionAdmission?.RequireRoot(preparation.InitialCommit.Mutation.State, plan.Definition.VersionId);
        if (preparation.Request.Decision is { } selection) {
            ProcessDecisionLaunchAdmissionCoordinator.RequireRequest(preparation.Request with {
                Authority = preparation.Authority,
                ProjectAdmission = preparation.InitialCommit.Mutation.State.ProjectAdmission,
                ToolSource = preparation.ToolSource
            });
            ProcessDecisionLaunchAdmissionCoordinator.RequireBindings(selection, preparation.InitialCommit, preparation.Review.Decision);
        } else if (preparation.Review.Decision is not null) {
            throw new InvalidDataException("A selected Process policy review requires its original launch selection.");
        }
    }

    private static bool HasSourceLifetimeSchema(ProcessLaunchAuthority? authority)
        => authority?.Principal is ProcessLaunchPrincipal.AgentExecution { Ceiling.EffectiveSchemaVersion: >= ProcessLaunchAgentCeiling.CurrentSchemaVersion };
}
