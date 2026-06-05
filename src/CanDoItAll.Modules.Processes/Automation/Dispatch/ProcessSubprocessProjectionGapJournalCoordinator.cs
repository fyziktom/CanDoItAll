using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CanDoItAll.Modules.Processes;

internal sealed class ProcessSubprocessProjectionGapJournalCoordinator
{
    public async Task RecordAsync(
        AppDbContext dbContext,
        ProcessRunAutomationDispatchService.DispatchCandidate candidate,
        ProcessSubprocessRunStartResult subprocessRun,
        ProcessArtifactExpectation expectation,
        string projectionDiagnostic,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(subprocessRun);
        ArgumentNullException.ThrowIfNull(expectation);

        var fingerprint = CreateFingerprint(candidate.Run.Id, candidate.StepRun.Id, subprocessRun.RunId, expectation.Id);
        var existingGap = await dbContext.Set<ProcessJournalEntry>()
            .AsNoTracking()
            .AnyAsync(
                item =>
                    item.ProcessRunId == candidate.Run.Id &&
                    item.StepRunId == candidate.StepRun.Id &&
                    item.EventType == ProcessRuntimeEventTypes.ArtifactValidationDiagnostic &&
                    item.CorrelationId == fingerprint,
                cancellationToken);
        if (existingGap)
        {
            return;
        }

        await dbContext.Set<ProcessJournalEntry>().AddAsync(
            new ProcessJournalEntry
            {
                ProcessRunId = candidate.Run.Id,
                StepRunId = candidate.StepRun.Id,
                EventType = ProcessRuntimeEventTypes.ArtifactValidationDiagnostic,
                Title = $"Subprocess artifact projection gap: {expectation.Title}",
                Description = string.IsNullOrWhiteSpace(projectionDiagnostic)
                    ? $"Completed subprocess run '{subprocessRun.RunName}' did not produce a child artifact that can satisfy parent expectation '{expectation.Title}'."
                    : $"Completed subprocess run '{subprocessRun.RunName}' did not produce a child artifact that can satisfy parent expectation '{expectation.Title}'. {projectionDiagnostic}",
                CorrelationId = fingerprint,
                OperatingMode = candidate.Run.OperatingMode,
                PolicyVersion = $"definition-version:{candidate.Run.ProcessDefinitionVersionId:D}",
                EnvironmentMode = candidate.Run.OperatingMode.ToString(),
                ReplayContextJson = JsonSerializer.Serialize(new
                {
                    candidate.Run.Id,
                    StepRunId = candidate.StepRun.Id,
                    SubprocessRunId = subprocessRun.RunId,
                    ExpectationId = expectation.Id,
                    ExpectationTitle = expectation.Title,
                    ProjectionDiagnostic = projectionDiagnostic
                }),
                OccurredAtUtc = now
            },
            cancellationToken);
    }

    internal static string CreateFingerprint(
        Guid processRunId,
        Guid stepRunId,
        Guid subprocessRunId,
        Guid expectationId)
    {
        var normalized = string.Join(
            "|",
            "subprocess-projection-gap",
            processRunId.ToString("D"),
            stepRunId.ToString("D"),
            subprocessRunId.ToString("D"),
            expectationId.ToString("D"));

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized))).ToLowerInvariant();
    }
}
