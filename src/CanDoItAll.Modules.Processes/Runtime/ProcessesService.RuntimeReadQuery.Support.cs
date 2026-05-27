using System.Text.Json;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Collaboration;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Processes;

public sealed partial class ProcessRuntimeReadQueryService
{
    private static IReadOnlyList<ProcessStepDependencyViewModel> BuildRuntimeDependencies(
        ProcessStepDefinition stepDefinition,
        IReadOnlyDictionary<Guid, List<ProcessStepDependencyDefinition>> dependenciesByStepId)
    {
        return ProcessStepDependencyCollection.BuildRuntimeDependencies(stepDefinition.Id, dependenciesByStepId);
    }

    private static IReadOnlyList<ProcessStepRunResponsibilityPortViewModel> BuildRuntimeResponsibilityPorts(
        Guid stepDefinitionId,
        IReadOnlyDictionary<Guid, List<ProcessStepRoleAssignmentRequirement>> assignmentsByStepId)
    {
        if (!assignmentsByStepId.TryGetValue(stepDefinitionId, out var assignments) || assignments.Count == 0)
        {
            return [];
        }

        var orderedKinds = new[]
        {
            ProcessResponsibilityKind.Responsible,
            ProcessResponsibilityKind.Accountable,
            ProcessResponsibilityKind.Reviewer,
            ProcessResponsibilityKind.Approver,
            ProcessResponsibilityKind.Backup
        };

        return orderedKinds
            .Select(
                responsibilityKind =>
                {
                    var matchingAssignments = assignments
                        .Where(item => item.ResponsibilityKind == responsibilityKind)
                        .ToList();
                    return new ProcessStepRunResponsibilityPortViewModel(
                        responsibilityKind,
                        matchingAssignments.Any(item => item.IsRequired),
                        matchingAssignments.Count);
                })
            .Where(item => item.AssignmentCount > 0)
            .ToList();
    }

    private static int Average(int total, int count)
    {
        return count == 0
            ? 0
            : (int)Math.Round((double)total / count, MidpointRounding.AwayFromZero);
    }

    private static async Task<IReadOnlyList<ProcessDecisionViewModel>> ListDecisionRecordsAsync(
        AppDbContext dbContext,
        Guid runId,
        CancellationToken cancellationToken)
    {
        var items = await dbContext.Set<ProcessDecisionRecord>()
            .AsNoTracking()
            .Where(item => item.ProcessRunId == runId)
            .Select(item => new ProcessDecisionViewModel(
                item.Id,
                item.DecisionKind,
                item.Outcome,
                item.Title,
                item.Reason,
                item.BranchOutcomeTitle,
                item.DecidedBy,
                item.CreatedAtUtc))
            .ToListAsync(cancellationToken);
        return items
            .OrderByDescending(item => item.CreatedAtUtc)
            .ToList();
    }

    private static async Task<IReadOnlyList<ProcessArtifactViewModel>> ListArtifactsAsync(
        AppDbContext dbContext,
        Guid runId,
        CancellationToken cancellationToken)
    {
        var items = await dbContext.Set<ProcessArtifactRecord>()
            .AsNoTracking()
            .Where(item => item.ProcessRunId == runId)
            .Select(item => new ProcessArtifactViewModel(
                item.Id,
                item.StepRunId,
                item.ArtifactExpectationId,
                item.ArtifactKind,
                item.Title,
                item.TrustStatus,
                item.SensitivityLevel,
                item.ProvenanceSummary,
                item.AllowedFutureUsageSummary,
                item.ManagedStoragePath,
                item.ExternalReferenceKey,
                item.CreatedAtUtc)
            {
                ProjectionLineageJson = item.ProjectionLineageJson,
                ProjectionIdentityHash = item.ProjectionIdentityHash
            })
            .ToListAsync(cancellationToken);
        return items
            .OrderByDescending(item => item.CreatedAtUtc)
            .ToList();
    }

    private static async Task<IReadOnlyList<ProcessOutboxRecordViewModel>> ListOutboxRecordsAsync(
        AppDbContext dbContext,
        Guid runId,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var records = await dbContext.Set<ProcessOutboxRecord>()
            .AsNoTracking()
            .Where(item => item.ProcessRunId == runId)
            .ToListAsync(cancellationToken);

        return records
            .OrderByDescending(item => item.UpdatedAtUtc)
            .ThenByDescending(item => item.CreatedAtUtc)
            .Select(item => MapOutboxRecord(item, now))
            .ToList();
    }

    private static ProcessOutboxRecordViewModel MapOutboxRecord(ProcessOutboxRecord record, DateTimeOffset now)
    {
        var automationDispatch = TryReadAutomationDispatch(record.PayloadJson);
        return new ProcessOutboxRecordViewModel(
            record.Id,
            automationDispatch?.StepRunId,
            record.CommandKey,
            record.Status,
            ResolveOutboxHealth(record, now),
            record.AttemptCount,
            record.LastAttemptAtUtc,
            record.NextAttemptAtUtc,
            record.LeaseExpiresAtUtc,
            record.CompletedAtUtc,
            record.LastError,
            automationDispatch?.Trigger ?? string.Empty,
            record.UpdatedAtUtc);
    }

    private static ProcessOutboxHealthStatus ResolveOutboxHealth(ProcessOutboxRecord record, DateTimeOffset now)
    {
        return ResolveOutboxHealth(record.Status, record.LeaseExpiresAtUtc, record.NextAttemptAtUtc, now);
    }

    private static ProcessOutboxHealthStatus ResolveOutboxHealth(
        ProcessOutboxRecordStatus status,
        DateTimeOffset? leaseExpiresAtUtc,
        DateTimeOffset? nextAttemptAtUtc,
        DateTimeOffset now)
    {
        return status switch
        {
            ProcessOutboxRecordStatus.Completed => ProcessOutboxHealthStatus.Completed,
            ProcessOutboxRecordStatus.DeadLettered => ProcessOutboxHealthStatus.DeadLettered,
            _ when leaseExpiresAtUtc.HasValue && leaseExpiresAtUtc.Value > now => ProcessOutboxHealthStatus.Leased,
            _ when nextAttemptAtUtc.HasValue && nextAttemptAtUtc.Value > now => ProcessOutboxHealthStatus.WaitingToRetry,
            _ => ProcessOutboxHealthStatus.Pending
        };
    }

    private static ProcessOutboxAutomationDispatchRequest? TryReadAutomationDispatch(string payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<ProcessOutboxPayload>(payloadJson, OutboxPayloadSerializerOptions)?.AutomationDispatch;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private const string ArtifactValidationExpectationIdPropertyName = "ExpectationId";
    private const string ArtifactValidationStatusPropertyName = "Status";
    private const string ArtifactValidationArtifactRecordIdPropertyName = "ArtifactRecordId";
    private const string ArtifactValidationAttemptedPathPropertyName = "AttemptedPath";
    private const string ArtifactValidationDiagnosticPropertyName = "Diagnostic";

    private static ProcessArtifactValidationDiagnosticProjection? TryReadArtifactValidationDiagnostic(
        ProcessJournalEntry entry)
    {
        if (!entry.StepRunId.HasValue || string.IsNullOrWhiteSpace(entry.ReplayContextJson))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(entry.ReplayContextJson);
            var root = document.RootElement;
            if (!TryReadGuid(root, ArtifactValidationExpectationIdPropertyName, out var expectationId) ||
                !TryReadArtifactValidationStatus(root, out var status))
            {
                return null;
            }

            if (!TryReadNullableGuid(root, ArtifactValidationArtifactRecordIdPropertyName, out var artifactRecordId))
            {
                return null;
            }

            var diagnostic = ReadString(root, ArtifactValidationDiagnosticPropertyName);
            return new ProcessArtifactValidationDiagnosticProjection(
                entry.StepRunId.Value,
                expectationId,
                status,
                artifactRecordId,
                ReadString(root, ArtifactValidationAttemptedPathPropertyName),
                string.IsNullOrWhiteSpace(diagnostic) ? entry.Description : diagnostic,
                entry.OccurredAtUtc);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool TryReadArtifactValidationStatus(
        JsonElement root,
        out ProcessRunAutomationDispatchService.ProcessArtifactValidationStatus status)
    {
        status = default;
        if (!root.TryGetProperty(ArtifactValidationStatusPropertyName, out var property))
        {
            return false;
        }

        if (property.ValueKind == JsonValueKind.String &&
            Enum.TryParse(property.GetString(), ignoreCase: true, out status))
        {
            return true;
        }

        if (property.ValueKind == JsonValueKind.Number &&
            property.TryGetInt32(out var numericStatus) &&
            Enum.IsDefined(typeof(ProcessRunAutomationDispatchService.ProcessArtifactValidationStatus), numericStatus))
        {
            status = (ProcessRunAutomationDispatchService.ProcessArtifactValidationStatus)numericStatus;
            return true;
        }

        return false;
    }

    private static bool TryReadGuid(JsonElement root, string propertyName, out Guid value)
    {
        value = Guid.Empty;
        return root.TryGetProperty(propertyName, out var property) &&
               property.ValueKind == JsonValueKind.String &&
               Guid.TryParse(property.GetString(), out value);
    }

    private static bool TryReadNullableGuid(JsonElement root, string propertyName, out Guid? value)
    {
        value = null;
        if (!root.TryGetProperty(propertyName, out var property) ||
            property.ValueKind == JsonValueKind.Null)
        {
            return true;
        }

        if (property.ValueKind != JsonValueKind.String ||
            !Guid.TryParse(property.GetString(), out var parsed))
        {
            return false;
        }

        value = parsed;
        return true;
    }

    private static string ReadString(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? string.Empty
            : string.Empty;
    }

    private static IReadOnlyList<ProcessArtifactExpectationSatisfactionViewModel> BuildArtifactLedger(
        ProcessStepRun stepRun,
        IReadOnlyDictionary<Guid, List<ProcessArtifactExpectation>> artifactExpectationsByStepId,
        IReadOnlyDictionary<Guid, List<ProcessArtifactRecord>> artifactRecordsByStepRunId,
        IReadOnlyDictionary<Guid, List<ProcessArtifactValidationDiagnosticProjection>> artifactValidationDiagnosticsByStepRunId)
    {
        if (!artifactExpectationsByStepId.TryGetValue(stepRun.StepDefinitionId, out var expectations) || expectations.Count == 0)
        {
            return [];
        }

        artifactRecordsByStepRunId.TryGetValue(stepRun.Id, out var stepArtifacts);
        stepArtifacts ??= [];
        artifactValidationDiagnosticsByStepRunId.TryGetValue(stepRun.Id, out var artifactValidationDiagnostics);
        artifactValidationDiagnostics ??= [];

        return expectations
            .Select(expectation => BuildArtifactLedgerItem(stepRun, expectation, stepArtifacts, artifactValidationDiagnostics))
            .ToList();
    }

    private sealed record ProcessArtifactValidationDiagnosticProjection(
        Guid StepRunId,
        Guid ExpectationId,
        ProcessRunAutomationDispatchService.ProcessArtifactValidationStatus Status,
        Guid? ArtifactRecordId,
        string AttemptedPath,
        string Diagnostic,
        DateTimeOffset OccurredAtUtc);

    private static ProcessArtifactExpectationSatisfactionViewModel BuildArtifactLedgerItem(
        ProcessStepRun stepRun,
        ProcessArtifactExpectation expectation,
        IReadOnlyList<ProcessArtifactRecord> stepArtifacts,
        IReadOnlyList<ProcessArtifactValidationDiagnosticProjection> artifactValidationDiagnostics)
    {
        var artifact = ResolveBestArtifactForExpectation(expectation, stepArtifacts);
        if (artifact is not null)
        {
            var sourceKind = ResolveArtifactSourceKind(artifact.ExternalReferenceKey);
            var contentUnavailableDiagnostic = ResolveContentUnavailableArtifactDiagnostic(
                expectation,
                artifact,
                artifactValidationDiagnostics);
            if (contentUnavailableDiagnostic is not null)
            {
                return new ProcessArtifactExpectationSatisfactionViewModel(
                    stepRun.Id,
                    expectation.Id,
                    expectation.ArtifactKind,
                    expectation.Title,
                    expectation.IsRequired,
                    ProcessArtifactExpectationSatisfactionStatus.ContentUnavailable,
                    sourceKind,
                    artifact.Id,
                    artifact.Title,
                    artifact.ManagedStoragePath,
                    contentUnavailableDiagnostic.Diagnostic);
            }

            return new ProcessArtifactExpectationSatisfactionViewModel(
                stepRun.Id,
                expectation.Id,
                expectation.ArtifactKind,
                expectation.Title,
                expectation.IsRequired,
                sourceKind is ProcessArtifactExpectationSourceKind.AgentExecutionArtifact or ProcessArtifactExpectationSourceKind.ProcessArtifactRecord
                    ? ProcessArtifactExpectationSatisfactionStatus.Satisfied
                    : ProcessArtifactExpectationSatisfactionStatus.AutoProjected,
                sourceKind,
                artifact.Id,
                artifact.Title,
                artifact.ManagedStoragePath,
                BuildArtifactSatisfiedDiagnostic(sourceKind));
        }

        var status = ResolveUnsatisfiedArtifactStatus(stepRun, expectation);
        return new ProcessArtifactExpectationSatisfactionViewModel(
            stepRun.Id,
            expectation.Id,
            expectation.ArtifactKind,
            expectation.Title,
            expectation.IsRequired,
            status,
            ProcessArtifactExpectationSourceKind.None,
            null,
            string.Empty,
            string.Empty,
            BuildUnsatisfiedArtifactDiagnostic(stepRun, expectation, status));
    }

    private static ProcessArtifactValidationDiagnosticProjection? ResolveContentUnavailableArtifactDiagnostic(
        ProcessArtifactExpectation expectation,
        ProcessArtifactRecord artifact,
        IReadOnlyList<ProcessArtifactValidationDiagnosticProjection> artifactValidationDiagnostics)
    {
        return artifactValidationDiagnostics
            .Where(item => IsMatchingContentUnavailableDiagnostic(expectation, artifact, item))
            .OrderByDescending(item => item.OccurredAtUtc)
            .FirstOrDefault();
    }

    private static bool IsMatchingContentUnavailableDiagnostic(
        ProcessArtifactExpectation expectation,
        ProcessArtifactRecord artifact,
        ProcessArtifactValidationDiagnosticProjection diagnostic)
    {
        if (diagnostic.ExpectationId != expectation.Id ||
            diagnostic.Status != ProcessRunAutomationDispatchService.ProcessArtifactValidationStatus.ContentUnavailable ||
            diagnostic.OccurredAtUtc < artifact.CreatedAtUtc)
        {
            return false;
        }

        if (diagnostic.ArtifactRecordId == artifact.Id)
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(diagnostic.AttemptedPath))
        {
            return string.Equals(diagnostic.AttemptedPath, artifact.ManagedStoragePath, StringComparison.OrdinalIgnoreCase);
        }

        return diagnostic.ArtifactRecordId is null;
    }

    internal static ProcessArtifactRecord? ResolveBestArtifactForExpectation(
        ProcessArtifactExpectation expectation,
        IReadOnlyList<ProcessArtifactRecord> stepArtifacts)
    {
        return stepArtifacts
            .Where(item => SatisfiesArtifactExpectation(item, expectation))
            .OrderBy(item => ResolveArtifactExpectationSpecificityPriority(expectation, item))
            .ThenBy(ResolveArtifactSourcePriority)
            .ThenByDescending(item => item.CreatedAtUtc)
            .FirstOrDefault();
    }

    private static int ResolveArtifactExpectationSpecificityPriority(
        ProcessArtifactExpectation expectation,
        ProcessArtifactRecord artifact)
    {
        if (string.Equals(artifact.Title, expectation.Title, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        var expectedSlug = FileSafeSlugBuilder.Build(expectation.Title);
        if (string.IsNullOrWhiteSpace(expectedSlug))
        {
            return artifact.ArtifactExpectationId == expectation.Id ? 2 : 3;
        }

        if (string.Equals(FileSafeSlugBuilder.Build(artifact.Title), expectedSlug, StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        var pathSlug = FileSafeSlugBuilder.Build(Path.GetFileNameWithoutExtension(artifact.ManagedStoragePath));
        if (string.Equals(pathSlug, expectedSlug, StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        return artifact.ArtifactExpectationId == expectation.Id ? 2 : 3;
    }

    private static int ResolveArtifactSourcePriority(ProcessArtifactRecord artifact)
    {
        return ResolveArtifactSourceKind(artifact.ExternalReferenceKey) switch
        {
            ProcessArtifactExpectationSourceKind.ProcessArtifactRecord => 0,
            ProcessArtifactExpectationSourceKind.AgentExecutionArtifact => 0,
            ProcessArtifactExpectationSourceKind.ProcessMockArtifact => 1,
            ProcessArtifactExpectationSourceKind.AssistantResponse => 2,
            ProcessArtifactExpectationSourceKind.CompletedDecision => 3,
            ProcessArtifactExpectationSourceKind.ProviderNativeBrowserArtifact => 4,
            _ => 5
        };
    }

    private static ProcessArtifactExpectationSatisfactionStatus ResolveUnsatisfiedArtifactStatus(
        ProcessStepRun stepRun,
        ProcessArtifactExpectation expectation)
    {
        if (!expectation.IsRequired)
        {
            return ProcessArtifactExpectationSatisfactionStatus.Expected;
        }

        if (stepRun.Status == ProcessStepRunStatus.Failed &&
            ContainsProjectionFailureSignal(stepRun.ExceptionSummary))
        {
            return ProcessArtifactExpectationSatisfactionStatus.ProjectionFailed;
        }

        if (stepRun.Status is ProcessStepRunStatus.Pending or ProcessStepRunStatus.Ready)
        {
            return ProcessArtifactExpectationSatisfactionStatus.Expected;
        }

        return ProcessArtifactExpectationSatisfactionStatus.Missing;
    }

    private static bool ContainsProjectionFailureSignal(string value)
    {
        return value.Contains("artifact", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("projection", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("storage", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildArtifactSatisfiedDiagnostic(ProcessArtifactExpectationSourceKind sourceKind)
    {
        return sourceKind switch
        {
            ProcessArtifactExpectationSourceKind.AgentExecutionArtifact => "Satisfied by a projected AgentFramework artifact.",
            ProcessArtifactExpectationSourceKind.AssistantResponse => "Auto-projected from the final assistant response.",
            ProcessArtifactExpectationSourceKind.ProcessMockArtifact => "Auto-projected from deterministic process mock output.",
            ProcessArtifactExpectationSourceKind.CompletedDecision => "Auto-recorded from the governed decision outcome.",
            ProcessArtifactExpectationSourceKind.ProviderNativeBrowserArtifact => "Auto-projected from provider-native browser evidence.",
            _ => "Satisfied by a process artifact record."
        };
    }

    private static string BuildUnsatisfiedArtifactDiagnostic(
        ProcessStepRun stepRun,
        ProcessArtifactExpectation expectation,
        ProcessArtifactExpectationSatisfactionStatus status)
    {
        if (stepRun.Status == ProcessStepRunStatus.Skipped)
        {
            return "Artifact expectation was not required because this step was skipped.";
        }

        if (!expectation.IsRequired)
        {
            return "Optional artifact expectation has not been recorded.";
        }

        return status switch
        {
            ProcessArtifactExpectationSatisfactionStatus.ProjectionFailed => "Required artifact projection failed; inspect the failed step reason before retrying.",
            ProcessArtifactExpectationSatisfactionStatus.Expected => "Required artifact is expected when this step runs.",
            _ => string.IsNullOrWhiteSpace(stepRun.BlockedReason) && string.IsNullOrWhiteSpace(stepRun.ExceptionSummary)
                ? "Required artifact is missing from process evidence."
                : $"{(string.IsNullOrWhiteSpace(stepRun.BlockedReason) ? stepRun.ExceptionSummary : stepRun.BlockedReason)}"
        };
    }

    private static ProcessArtifactExpectationSourceKind ResolveArtifactSourceKind(string externalReferenceKey)
    {
        if (externalReferenceKey.StartsWith("agentframework-artifact:", StringComparison.OrdinalIgnoreCase))
        {
            return ProcessArtifactExpectationSourceKind.AgentExecutionArtifact;
        }

        if (externalReferenceKey.StartsWith("assistant-response|", StringComparison.OrdinalIgnoreCase))
        {
            return ProcessArtifactExpectationSourceKind.AssistantResponse;
        }

        if (externalReferenceKey.StartsWith("process-mock-artifact:", StringComparison.OrdinalIgnoreCase))
        {
            return ProcessArtifactExpectationSourceKind.ProcessMockArtifact;
        }

        if (externalReferenceKey.StartsWith("process-step-decision:", StringComparison.OrdinalIgnoreCase))
        {
            return ProcessArtifactExpectationSourceKind.CompletedDecision;
        }

        if (externalReferenceKey.StartsWith("agentframework-browser-artifact:", StringComparison.OrdinalIgnoreCase))
        {
            return ProcessArtifactExpectationSourceKind.ProviderNativeBrowserArtifact;
        }

        return string.IsNullOrWhiteSpace(externalReferenceKey)
            ? ProcessArtifactExpectationSourceKind.ProcessArtifactRecord
            : ProcessArtifactExpectationSourceKind.ProcessArtifactRecord;
    }

    private static bool SatisfiesArtifactExpectation(
        ProcessArtifactRecord artifact,
        ProcessArtifactExpectation expectation)
    {
        if (artifact.ArtifactKind != expectation.ArtifactKind)
        {
            return false;
        }

        if (artifact.SensitivityLevel < expectation.SensitivityLevel)
        {
            return false;
        }

        if (!SatisfiesTrustRequirement(artifact.TrustStatus, expectation.TrustRequirement))
        {
            return false;
        }

        if (artifact.ArtifactExpectationId.HasValue)
        {
            return artifact.ArtifactExpectationId.Value == expectation.Id;
        }

        return string.Equals(artifact.Title, expectation.Title, StringComparison.OrdinalIgnoreCase);
    }

    private static bool SatisfiesTrustRequirement(
        ProcessArtifactTrustStatus trustStatus,
        ProcessArtifactTrustRequirement trustRequirement)
    {
        return trustRequirement switch
        {
            ProcessArtifactTrustRequirement.None => true,
            ProcessArtifactTrustRequirement.ReviewRequired => trustStatus is
                ProcessArtifactTrustStatus.ReviewRequired or
                ProcessArtifactTrustStatus.Approved or
                ProcessArtifactTrustStatus.TrustedSource,
            ProcessArtifactTrustRequirement.HumanApproved => trustStatus == ProcessArtifactTrustStatus.Approved,
            ProcessArtifactTrustRequirement.ApprovalRequired => trustStatus == ProcessArtifactTrustStatus.Approved,
            ProcessArtifactTrustRequirement.TrustedSource => trustStatus == ProcessArtifactTrustStatus.TrustedSource,
            _ => false
        };
    }

    private static ProcessStepRunHealthViewModel BuildInitialStepHealth(
        ProcessStepRun stepRun,
        IReadOnlyList<ProcessArtifactExpectationSatisfactionViewModel> artifactLedger,
        string manualRecoveryDirective)
    {
        return ProcessHealthInvariantAuditor.BuildStepHealth(stepRun, artifactLedger, manualRecoveryDirective);
    }

    private static async Task<IReadOnlyList<ProcessRunAssignmentViewModel>> ListAssignmentsAsync(
        AppDbContext dbContext,
        Guid runId,
        CancellationToken cancellationToken)
    {
        var assignments = await dbContext.Set<ProcessRunAssignment>()
            .AsNoTracking()
            .Where(item => item.ProcessRunId == runId)
            .ToListAsync(cancellationToken);
        if (assignments.Count == 0)
        {
            return [];
        }

        var roleIds = assignments
            .Select(item => item.RoleRequirementId)
            .Distinct()
            .ToList();
        var roleDisplayNames = await dbContext.Set<ProcessRoleRequirement>()
            .AsNoTracking()
            .Where(item => roleIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, item => item.DisplayName, cancellationToken);

        return assignments
            .OrderBy(item => roleDisplayNames.GetValueOrDefault(item.RoleRequirementId, item.DisplayName), StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(
                item => new ProcessRunAssignmentViewModel(
                    item.Id,
                    item.RoleRequirementId,
                    item.StepDefinitionId,
                    item.PartyId,
                    item.WorkflowDefinitionId,
                    item.WorkflowVersionId,
                    item.DisplayName,
                    item.ExecutorKind,
                    item.BindingReason,
                    item.SourceRegistryKey,
                    item.SnapshotSummary,
                    item.IsFallback,
                    item.IsCapabilityGap,
                    item.AllowsDirectMessaging)
                {
                    RoleDisplayName = roleDisplayNames.GetValueOrDefault(item.RoleRequirementId, string.Empty)
                })
            .ToList();
    }

    private static async Task<IReadOnlyList<ProcessWorkBriefViewModel>> ListWorkBriefsAsync(
        AppDbContext dbContext,
        Guid runId,
        CancellationToken cancellationToken)
    {
        var items = await dbContext.Set<ProcessWorkBrief>()
            .AsNoTracking()
            .Where(item => item.ProcessRunId == runId)
            .Select(item => new ProcessWorkBriefViewModel(
                item.Id,
                item.StepRunId,
                item.Title,
                item.WorkBriefText,
                item.HandoffSummary,
                item.AssignmentReason,
                item.ExpectedOutcome,
                item.EvidenceExpectationSummary,
                item.CreatedAtUtc))
            .ToListAsync(cancellationToken);
        return items
            .OrderBy(item => item.CreatedAtUtc)
            .ToList();
    }

    private static async Task<IReadOnlyList<ProcessWorkflowRunViewModel>> ListWorkflowRunsAsync(
        AppDbContext dbContext,
        Guid runId,
        CancellationToken cancellationToken)
    {
        var links = await dbContext.Set<ProcessWorkflowRunLink>()
            .AsNoTracking()
            .Where(item => item.ProcessRunId == runId)
            .ToListAsync(cancellationToken);
        if (links.Count == 0)
        {
            return [];
        }

        var stepRunIds = links
            .Select(item => item.StepRunId)
            .Distinct()
            .ToList();
        var assignmentIds = links
            .Select(item => item.AssignmentId)
            .Distinct()
            .ToList();
        var stepTitlesById = await dbContext.Set<ProcessStepRun>()
            .AsNoTracking()
            .Where(item => stepRunIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, item => item.Title, cancellationToken);
        var assignmentNamesById = await dbContext.Set<ProcessRunAssignment>()
            .AsNoTracking()
            .Where(item => assignmentIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, item => item.DisplayName, cancellationToken);

        return links
            .OrderByDescending(item => item.UpdatedAtUtc)
            .Select(item => new ProcessWorkflowRunViewModel(
                item.Id,
                item.ProcessRunId,
                item.StepRunId,
                item.AssignmentId,
                item.WorkflowDefinitionId,
                item.WorkflowVersionId,
                item.WorkflowRunId,
                assignmentNamesById.GetValueOrDefault(item.AssignmentId, "Workflow"),
                stepTitlesById.GetValueOrDefault(item.StepRunId, string.Empty),
                assignmentNamesById.GetValueOrDefault(item.AssignmentId, string.Empty),
                item.WorkflowBackend,
                item.WorkflowBackendRunId,
                item.State,
                item.Summary,
                ArtifactCount: 0,
                PendingRequestCount: 0,
                item.CreatedAtUtc,
                item.UpdatedAtUtc))
            .ToList();
    }

    private static async Task<IReadOnlyList<ProcessConformanceObservationViewModel>> ListConformanceObservationsAsync(
        AppDbContext dbContext,
        Guid runId,
        CancellationToken cancellationToken)
    {
        var items = await dbContext.Set<ProcessConformanceObservation>()
            .AsNoTracking()
            .Where(item => item.ProcessRunId == runId)
            .Select(item => new ProcessConformanceObservationViewModel(
                item.Id,
                item.StepRunId,
                item.Severity,
                item.Category,
                item.Observation,
                item.DeviationReason,
                item.IsSafeNonAction,
                item.ContainsSensitiveAssessment,
                item.CreatedAtUtc))
            .ToListAsync(cancellationToken);
        return items
            .OrderByDescending(item => item.CreatedAtUtc)
            .ToList();
    }

    private static async Task<IReadOnlyList<ProcessDirectMessageThreadViewModel>> ListDirectMessageThreadsAsync(
        AppDbContext dbContext,
        Guid runId,
        CancellationToken cancellationToken)
    {
        var threads = await dbContext.Set<CollaborationThreadRecord>()
            .AsNoTracking()
            .Where(item => item.ContextKind == CollaborationContextKind.ProcessRun && item.ContextId == runId)
            .Select(item => new ProcessDirectMessageThreadProjection(
                item.Id,
                item.Subject,
                item.LastActivityAtUtc))
            .ToListAsync(cancellationToken);
        if (threads.Count == 0)
        {
            return [];
        }

        var threadIds = threads
            .Select(item => item.ThreadId)
            .ToArray();
        var inboxItems = await dbContext.Set<CollaborationInboxItemRecord>()
            .AsNoTracking()
            .Where(item => threadIds.Contains(item.ThreadId))
            .Select(item => new ProcessDirectMessageInboxProjection(
                item.ThreadId,
                item.Route,
                item.UnreadCount))
            .ToListAsync(cancellationToken);
        var participants = await dbContext.Set<CollaborationParticipantRecord>()
            .AsNoTracking()
            .Where(item => threadIds.Contains(item.ThreadId) && item.ParticipantKind == CollaborationParticipantKind.Role)
            .Select(item => new ProcessDirectMessageParticipantProjection(
                item.ThreadId,
                item.DisplayName))
            .ToListAsync(cancellationToken);
        var messages = await dbContext.Set<CollaborationMessageRecord>()
            .AsNoTracking()
            .Where(item => threadIds.Contains(item.ThreadId))
            .Select(item => new ProcessDirectMessageMessageProjection(
                item.ThreadId,
                item.Id,
                item.Kind,
                item.AuthorName,
                item.Body,
                item.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        var inboxByThreadId = inboxItems.ToDictionary(item => item.ThreadId);
        var participantsByThreadId = participants
            .GroupBy(item => item.ThreadId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(item => item.DisplayName)
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
                    .ToList());
        var messagesByThreadId = messages
            .GroupBy(item => item.ThreadId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(item => new ProcessDirectMessageEntryViewModel(
                        item.MessageId,
                        item.MessageKind,
                        item.AuthorName,
                        item.Body,
                        item.CreatedAtUtc))
                    .ToList());

        return threads
            .OrderByDescending(item => item.LastActivityAtUtc)
            .Where(item => participantsByThreadId.ContainsKey(item.ThreadId))
            .Select(item =>
            {
                var roleLabels = participantsByThreadId[item.ThreadId];
                var threadMessages = (messagesByThreadId.GetValueOrDefault(item.ThreadId) ?? [])
                    .OrderBy(message => message.CreatedAtUtc)
                    .ToList();
                inboxByThreadId.TryGetValue(item.ThreadId, out var inbox);
                return new ProcessDirectMessageThreadViewModel(
                    item.ThreadId,
                    item.Subject,
                    inbox?.Route ?? string.Empty,
                    roleLabels.Count == 0 ? "Process roles" : string.Join(" / ", roleLabels),
                    threadMessages.Count,
                    inbox?.UnreadCount ?? 0,
                    item.LastActivityAtUtc,
                    threadMessages);
            })
            .ToList();
    }

    private sealed record ProcessRunListProjection(
        Guid Id,
        Guid ProcessDefinitionId,
        Guid ProcessDefinitionVersionId,
        Guid? ParentRunId,
        Guid? ParentStepRunId,
        Guid RootRunId,
        int HierarchyDepth,
        Guid? ProjectId,
        string Name,
        ProcessRunStatus Status,
        ProcessOperatingMode OperatingMode,
        Guid? ManagerAgentId,
        string ManagerAgentName,
        decimal EstimatedCost,
        decimal ActualCost,
        DateTimeOffset UpdatedAtUtc);

    private sealed record ProcessRunStepSummaryProjection(
        Guid ProcessRunId,
        int CompletedCount,
        int TotalCount,
        int BlockedCount,
        int CapabilityGapCount)
    {
        public static ProcessRunStepSummaryProjection Empty(Guid runId)
        {
            return new ProcessRunStepSummaryProjection(runId, 0, 0, 0, 0);
        }
    }

    private sealed record ProcessArtifactOutputProjection(
        Guid StepDefinitionId,
        ProcessStepRunArtifactPortViewModel ArtifactOutput);

    private sealed record ProcessStepArtifactInputCountProjection(Guid StepDefinitionId, int Count);

    private sealed record ProcessBranchOutcomeProjection(
        Guid StepDefinitionId,
        ProcessStepBranchOutcomeOptionViewModel BranchOutcome);

    private sealed record ProcessSubprocessRunProjection(
        Guid Id,
        Guid ProcessDefinitionId,
        Guid? ProjectId,
        Guid ParentStepRunId,
        string Name,
        ProcessRunStatus Status,
        DateTimeOffset UpdatedAtUtc);

    private sealed record ProcessAnalyticsRunStatsProjection(
        int TotalCount,
        int ActiveCount,
        int CompletedCount,
        int BlockedCount,
        decimal EstimatedCost,
        decimal ActualCost);

    private sealed record ProcessStepAnalyticsStatsProjection(
        int StepCount,
        int CapabilityGapCount,
        int TotalCycleMinutes,
        int TotalWaitMinutes,
        int TotalBlockedMinutes);

    private sealed record ProcessConformanceStatsProjection(
        int TotalCount,
        int SafeNonActionCount);

    private sealed record ProcessActiveRunStepHealthProjection(
        Guid RunId,
        Guid StepRunId,
        string Title,
        ProcessStepRunStatus Status);

    private sealed record ProcessActiveRunOutboxHealthProjection(
        Guid RunId,
        ProcessOutboxRecordStatus Status,
        DateTimeOffset? LeaseExpiresAtUtc,
        DateTimeOffset? NextAttemptAtUtc);

    private sealed record ProcessActiveRunOutboxSummaryProjection(
        int PendingCount,
        int DeadLetteredCount);

    private sealed record ProcessDirectMessageThreadProjection(
        Guid ThreadId,
        string Subject,
        DateTimeOffset LastActivityAtUtc);

    private sealed record ProcessDirectMessageInboxProjection(
        Guid ThreadId,
        string Route,
        int UnreadCount);

    private sealed record ProcessDirectMessageParticipantProjection(
        Guid ThreadId,
        string DisplayName);

    private sealed record ProcessDirectMessageMessageProjection(
        Guid ThreadId,
        Guid MessageId,
        CollaborationMessageKind MessageKind,
        string AuthorName,
        string Body,
        DateTimeOffset CreatedAtUtc);

    private static readonly JsonSerializerOptions OutboxPayloadSerializerOptions = new(JsonSerializerDefaults.Web);
}
