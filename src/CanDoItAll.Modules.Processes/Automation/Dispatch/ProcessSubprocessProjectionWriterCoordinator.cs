using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;
using System.Text;
using System.Text.Json;

namespace CanDoItAll.Modules.Processes;

internal sealed class ProcessSubprocessProjectionWriterCoordinator(IWorkspacePathResolver workspacePathResolver)
{
    public async Task WriteAsync(
        AppDbContext dbContext,
        ProcessRunAutomationDispatchService.DispatchCandidate candidate,
        ProcessSubprocessRunStartResult subprocessRun,
        ProcessSubprocessArtifactProjectionPlan plan,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(subprocessRun);
        ArgumentNullException.ThrowIfNull(plan);

        var workspaceRoot = Path.GetFullPath(workspacePathResolver.ResolveWorkspaceRoot());
        var fullPath = Path.GetFullPath(Path.Combine(
            workspaceRoot,
            plan.ManagedStoragePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!IsWithinWorkspace(workspaceRoot, fullPath))
        {
            throw new InvalidOperationException(
                $"Projected subprocess artifact path '{plan.ManagedStoragePath}' resolves outside the workspace root.");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await File.WriteAllTextAsync(
            fullPath,
            plan.MarkdownContent,
            Encoding.UTF8,
            cancellationToken);

        var artifact = new ProcessArtifactRecord
        {
            ProcessRunId = candidate.Run.Id,
            StepRunId = candidate.StepRun.Id,
            ArtifactExpectationId = plan.ArtifactExpectationId,
            ArtifactKind = plan.ArtifactKind,
            Title = plan.Title,
            TrustStatus = plan.TrustStatus,
            SensitivityLevel = plan.SensitivityLevel,
            ProvenanceSummary = plan.ProvenanceSummary,
            AllowedFutureUsageSummary = plan.AllowedFutureUsageSummary,
            ReviewSummary = plan.ReviewSummary,
            ManagedStoragePath = plan.ManagedStoragePath,
            ExternalReferenceKey = plan.ExternalReferenceKey,
            ProjectionLineageJson = ProcessArtifactProjectionLineageJson.SerializeNormalized(plan.ProjectionLineage),
            ProjectionIdentityHash = plan.ProjectionLineage.ProjectionIdentityHash,
            CreatedAtUtc = now
        };
        await dbContext.Set<ProcessArtifactRecord>().AddAsync(artifact, cancellationToken);
        await dbContext.Set<ProcessJournalEntry>().AddAsync(
            new ProcessJournalEntry
            {
                ProcessRunId = candidate.Run.Id,
                StepRunId = candidate.StepRun.Id,
                EventType = "artifact-recorded",
                Title = "Recorded process artifact",
                Description = artifact.Title,
                CorrelationId = Guid.NewGuid().ToString("N"),
                OperatingMode = candidate.Run.OperatingMode,
                PolicyVersion = $"definition-version:{candidate.Run.ProcessDefinitionVersionId:D}",
                EnvironmentMode = candidate.Run.OperatingMode.ToString(),
                ReplayContextJson = JsonSerializer.Serialize(new
                {
                    RunId = candidate.Run.Id,
                    StepRunId = candidate.StepRun.Id,
                    SubprocessRunId = subprocessRun.RunId,
                    SourceArtifactId = plan.ProjectionLineage.SourceArtifactId,
                    Summary = artifact.ProvenanceSummary
                }),
                OccurredAtUtc = now
            },
            cancellationToken);
    }

    private static bool IsWithinWorkspace(string workspaceRoot, string fullPath)
    {
        var normalizedRoot = Path.GetFullPath(workspaceRoot)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var normalizedPath = Path.GetFullPath(fullPath);

        return normalizedPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
    }
}
