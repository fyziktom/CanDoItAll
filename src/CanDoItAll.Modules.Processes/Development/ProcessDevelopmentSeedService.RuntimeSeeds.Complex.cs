using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Processes;

public sealed partial class ProcessDevelopmentSeedService
{
    private async Task<Result> EnsureAssignmentAsync(
        Guid runId,
        Guid roleRequirementId,
        Guid? stepDefinitionId,
        string? displayName,
        string executorKind,
        string bindingReason,
        bool isFallback,
        CancellationToken cancellationToken)
    {
        if (roleRequirementId == Guid.Empty)
        {
            return Result.Success();
        }

        var normalizedDisplayName = string.IsNullOrWhiteSpace(displayName)
            ? string.Empty
            : displayName.Trim();
        var assignments = await processesService.ListAssignmentsAsync(runId, cancellationToken);
        var existingAssignment = assignments.FirstOrDefault(item =>
            item.RoleRequirementId == roleRequirementId &&
            item.StepDefinitionId == stepDefinitionId &&
            string.Equals(item.DisplayName ?? string.Empty, normalizedDisplayName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(item.ExecutorKind ?? string.Empty, executorKind ?? string.Empty, StringComparison.OrdinalIgnoreCase) &&
            item.IsFallback == isFallback);
        if (existingAssignment is not null)
        {
            return Result.Success();
        }

        return await processesService.ResolveAssignmentAsync(
            new ProcessAssignmentResolutionRequest
            {
                ProcessRunId = runId,
                RoleRequirementId = roleRequirementId,
                StepDefinitionId = stepDefinitionId,
                DisplayName = normalizedDisplayName,
                ExecutorKind = string.IsNullOrWhiteSpace(executorKind) ? "person" : executorKind.Trim(),
                BindingReason = bindingReason?.Trim() ?? string.Empty,
                IsFallback = isFallback
            },
            cancellationToken);
    }

    private async Task<Result> EnsureStepStatusAsync(
        Guid runId,
        Guid stepRunId,
        ProcessStepRunStatus targetStatus,
        Guid? selectedBranchOutcomeId,
        ProcessStepBlockCause? blockCause,
        string reason,
        string decidedBy,
        CancellationToken cancellationToken)
    {
        var stepRun = (await processesService.ListStepRunsAsync(runId, cancellationToken))
            .FirstOrDefault(item => item.Id == stepRunId);
        if (stepRun is null)
        {
            return Result.Success();
        }

        if (stepRun.Status == targetStatus &&
            (!selectedBranchOutcomeId.HasValue || stepRun.SelectedBranchOutcomeId == selectedBranchOutcomeId))
        {
            return Result.Success();
        }

        if (stepRun.Status == ProcessStepRunStatus.Skipped &&
            targetStatus != ProcessStepRunStatus.Skipped)
        {
            return Result.Failure(
                Error.Validation(
                    $"Baseline seeding cannot transition skipped step '{stepRun.Title}' to {targetStatus}. Skip reason: {stepRun.DecisionSummary}",
                    "processes.seed-step-already-skipped"));
        }

        var transitionSequenceResult = BuildTransitionSequence(stepRun.Status, targetStatus);
        if (transitionSequenceResult.IsFailure)
        {
            return Result.Failure(
                transitionSequenceResult.Errors
                    .Select(error =>
                        Error.Validation(
                            $"{error.Message} Step '{stepRun.Title}' in run '{runId:D}' triggered the invalid baseline transition.",
                            error.Code))
                    .ToArray());
        }

        var transitionSequence = transitionSequenceResult.Value;
        if (transitionSequence is null)
        {
            return Result.Failure(
                Error.Validation(
                    "Baseline seeding could not resolve a transition sequence.",
                    "processes.seed-transition-sequence-missing"));
        }

        foreach (var transitionStatus in transitionSequence)
        {
            var transitionResult = await processesService.TransitionStepAsync(
                new ProcessStepTransitionRequest
                {
                    StepRunId = stepRunId,
                    TargetStatus = transitionStatus,
                    SelectedBranchOutcomeId = transitionStatus == targetStatus ? selectedBranchOutcomeId : null,
                    BlockCause = transitionStatus == targetStatus && transitionStatus == ProcessStepRunStatus.Blocked
                        ? blockCause
                        : null,
                    Reason = transitionStatus == targetStatus ? reason?.Trim() ?? string.Empty : string.Empty,
                    DecidedBy = string.IsNullOrWhiteSpace(decidedBy) ? "process-template-pack" : decidedBy.Trim()
                },
                cancellationToken);
            if (transitionResult.IsFailure)
            {
                return transitionResult;
            }
        }

        return Result.Success();
    }

    private async Task<Result> EnsureArtifactAsync(
        Guid runId,
        Guid? stepRunId,
        IReadOnlyList<ProcessStepRunArtifactPortViewModel> artifactOutputs,
        ProcessArtifactKind artifactKind,
        string title,
        ProcessArtifactTrustStatus trustStatus,
        ProcessSensitivityLevel sensitivityLevel,
        string provenanceSummary,
        string allowedFutureUsageSummary,
        string reviewSummary,
        CancellationToken cancellationToken,
        bool forceMarkdownPath = false)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return Result.Success();
        }

        var normalizedTitle = title.Trim();
        var artifactExpectationId = ResolveArtifactExpectationId(artifactOutputs, normalizedTitle);
        var artifacts = await processesService.ListArtifactsAsync(runId, cancellationToken);
        var existingArtifacts = artifacts
            .Where(item =>
                artifactExpectationId.HasValue
                    ? item.ArtifactExpectationId == artifactExpectationId.Value
                    : string.Equals(item.Title, normalizedTitle, StringComparison.OrdinalIgnoreCase) &&
                      item.ArtifactKind == artifactKind)
            .ToList();
        var hasReusableExistingArtifact = existingArtifacts.Count > 0 &&
            (!forceMarkdownPath ||
             existingArtifacts.Any(item => item.ManagedStoragePath.EndsWith(".md", StringComparison.OrdinalIgnoreCase)));
        if (hasReusableExistingArtifact)
        {
            return Result.Success();
        }

        var managedStoragePath = BuildSeedArtifactManagedStoragePath(
            runId,
            normalizedTitle,
            provenanceSummary,
            allowedFutureUsageSummary,
            reviewSummary,
            forceMarkdownPath);
        await WriteSeedManagedArtifactAsync(
            managedStoragePath,
            normalizedTitle,
            artifactKind,
            provenanceSummary,
            allowedFutureUsageSummary,
            reviewSummary,
            cancellationToken);

        var result = await processesService.RecordArtifactAsync(
            new ProcessArtifactRecordRequest
            {
                ProcessRunId = runId,
                StepRunId = stepRunId,
                ArtifactExpectationId = artifactExpectationId,
                ArtifactKind = artifactKind,
                Title = normalizedTitle,
                TrustStatus = trustStatus,
                SensitivityLevel = sensitivityLevel,
                ProvenanceSummary = provenanceSummary?.Trim() ?? string.Empty,
                AllowedFutureUsageSummary = allowedFutureUsageSummary?.Trim() ?? string.Empty,
                ReviewSummary = reviewSummary?.Trim() ?? string.Empty,
                ManagedStoragePath = managedStoragePath
            },
            cancellationToken);
        return result.IsFailure
            ? Result.Failure(result.Errors.ToArray())
            : Result.Success();
    }

    private async Task WriteSeedManagedArtifactAsync(
        string managedStoragePath,
        string title,
        ProcessArtifactKind artifactKind,
        string provenanceSummary,
        string allowedFutureUsageSummary,
        string reviewSummary,
        CancellationToken cancellationToken)
    {
        var fullPath = Path.GetFullPath(Path.Combine(
            workspacePathResolver.ResolveWorkspaceRoot(),
            managedStoragePath.Replace('/', Path.DirectorySeparatorChar)));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await File.WriteAllTextAsync(
            fullPath,
            BuildSeedManagedArtifactContent(
                managedStoragePath,
                title,
                artifactKind,
                provenanceSummary,
                allowedFutureUsageSummary,
                reviewSummary),
            cancellationToken);
    }

    private static string BuildSeedArtifactManagedStoragePath(
        Guid runId,
        string title,
        string provenanceSummary,
        string allowedFutureUsageSummary,
        string reviewSummary,
        bool forceMarkdownPath = false)
    {
        var useImagePath = !forceMarkdownPath &&
            RequiresImageSeedArtifactPath(
                title,
                provenanceSummary,
                allowedFutureUsageSummary,
                reviewSummary);
        var extension = useImagePath ? ".svg" : ".md";
        return $"artifacts/baseline-seed/{runId:N}/{FileSafeSlugBuilder.Build(title)}{extension}";
    }

    private static bool RequiresImageSeedArtifactPath(
        string title,
        string provenanceSummary,
        string allowedFutureUsageSummary,
        string reviewSummary)
    {
        var text = string.Join(
            ' ',
            title,
            provenanceSummary,
            allowedFutureUsageSummary,
            reviewSummary);
        return text.Contains("screenshot", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("image asset", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("regression evidence pack", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildSeedManagedArtifactContent(
        string managedStoragePath,
        string title,
        ProcessArtifactKind artifactKind,
        string provenanceSummary,
        string allowedFutureUsageSummary,
        string reviewSummary)
    {
        if (managedStoragePath.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
        {
            return BuildSeedManagedArtifactSvgContent(
                title,
                artifactKind,
                provenanceSummary,
                allowedFutureUsageSummary,
                reviewSummary);
        }

        return $"""
            # {title}

            Artifact kind: {artifactKind}
            Provenance: {provenanceSummary?.Trim() ?? string.Empty}
            Future usage: {allowedFutureUsageSummary?.Trim() ?? string.Empty}
            Review: {reviewSummary?.Trim() ?? string.Empty}
            Baseline seed evidence: This managed artifact is generated as durable current-run evidence for the process template baseline scenario and is bound to the recorded artifact metadata.
            """;
    }

    private static string BuildSeedManagedArtifactSvgContent(
        string title,
        ProcessArtifactKind artifactKind,
        string provenanceSummary,
        string allowedFutureUsageSummary,
        string reviewSummary)
    {
        var titleText = EscapeSvgText(title);
        var detailText = EscapeSvgText($"Kind: {artifactKind}. {reviewSummary?.Trim() ?? string.Empty}");
        var provenanceText = EscapeSvgText(provenanceSummary?.Trim() ?? string.Empty);
        var usageText = EscapeSvgText(allowedFutureUsageSummary?.Trim() ?? string.Empty);
        return $$"""
            <svg xmlns="http://www.w3.org/2000/svg" width="960" height="540" viewBox="0 0 960 540" role="img" aria-label="{{titleText}}">
              <rect width="960" height="540" fill="#f8fafc" />
              <rect x="32" y="32" width="896" height="476" fill="#ffffff" stroke="#1f2937" stroke-width="2" />
              <text x="64" y="96" font-family="Arial, sans-serif" font-size="30" font-weight="700" fill="#111827">{{titleText}}</text>
              <text x="64" y="156" font-family="Arial, sans-serif" font-size="20" fill="#1f2937">{{detailText}}</text>
              <text x="64" y="214" font-family="Arial, sans-serif" font-size="18" fill="#374151">{{provenanceText}}</text>
              <text x="64" y="270" font-family="Arial, sans-serif" font-size="18" fill="#374151">{{usageText}}</text>
              <text x="64" y="448" font-family="Arial, sans-serif" font-size="18" fill="#111827">Baseline managed visual proof for the current process run.</text>
            </svg>
            """;
    }

    private static string EscapeSvgText(string value)
    {
        return value
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("'", "&apos;", StringComparison.Ordinal);
    }

    private static Guid? ResolveArtifactExpectationId(
        IReadOnlyList<ProcessStepRunArtifactPortViewModel> artifactOutputs,
        string title)
    {
        if (artifactOutputs.Count == 0)
        {
            return null;
        }

        if (artifactOutputs.Count == 1)
        {
            return artifactOutputs[0].ArtifactExpectationId;
        }

        var exactMatch = artifactOutputs.FirstOrDefault(item =>
            string.Equals(item.Title, title, StringComparison.OrdinalIgnoreCase));
        if (exactMatch is not null)
        {
            return exactMatch.ArtifactExpectationId;
        }

        var overlappingMatches = artifactOutputs
            .Where(item => ArtifactTitlesOverlap(item.Title, title))
            .ToList();
        if (overlappingMatches.Count == 1)
        {
            return overlappingMatches[0].ArtifactExpectationId;
        }

        var requiredOutputs = artifactOutputs
            .Where(item => item.IsRequired)
            .ToList();
        return requiredOutputs.Count == 1
            ? requiredOutputs[0].ArtifactExpectationId
            : null;
    }

    private static bool ArtifactTitlesOverlap(string left, string right)
    {
        var normalizedLeft = NormalizeArtifactTitle(left);
        var normalizedRight = NormalizeArtifactTitle(right);
        if (normalizedLeft.Length == 0 || normalizedRight.Length == 0)
        {
            return false;
        }

        return normalizedLeft.Contains(normalizedRight, StringComparison.Ordinal) ||
               normalizedRight.Contains(normalizedLeft, StringComparison.Ordinal);
    }

    private static string NormalizeArtifactTitle(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return new string(value
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());
    }

    private static Result<IReadOnlyList<ProcessStepRunStatus>> BuildTransitionSequence(
        ProcessStepRunStatus currentStatus,
        ProcessStepRunStatus targetStatus)
    {
        if (currentStatus == targetStatus)
        {
            return Result<IReadOnlyList<ProcessStepRunStatus>>.Success([]);
        }

        if (ProcessStepRunTransitions.IsAllowed(currentStatus, targetStatus))
        {
            return Result<IReadOnlyList<ProcessStepRunStatus>>.Success([targetStatus]);
        }

        if (currentStatus == ProcessStepRunStatus.Ready &&
            targetStatus is ProcessStepRunStatus.Completed or ProcessStepRunStatus.Failed)
        {
            return Result<IReadOnlyList<ProcessStepRunStatus>>.Success(
            [
                ProcessStepRunStatus.InProgress,
                targetStatus
            ]);
        }

        if (currentStatus == ProcessStepRunStatus.Blocked &&
            targetStatus is ProcessStepRunStatus.Completed or ProcessStepRunStatus.Failed)
        {
            return Result<IReadOnlyList<ProcessStepRunStatus>>.Success(
            [
                ProcessStepRunStatus.InProgress,
                targetStatus
            ]);
        }

        return Result<IReadOnlyList<ProcessStepRunStatus>>.Failure(
            Error.Validation(
                $"Baseline seeding cannot move a step from {currentStatus} to {targetStatus} without violating runtime transition rules.",
                "processes.seed-transition-path-not-found"));
    }
}
