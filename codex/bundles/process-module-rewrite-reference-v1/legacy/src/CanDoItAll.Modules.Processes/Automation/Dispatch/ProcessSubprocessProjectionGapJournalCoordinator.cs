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
        ProcessDispatchSubprocessRuntimeInput input,
        ProcessSubprocessRunStartResult subprocessRun,
        ProcessArtifactExpectation expectation,
        string projectionDiagnostic,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(subprocessRun);
        ArgumentNullException.ThrowIfNull(expectation);

        var fingerprint = CreateFingerprint(input.Run.Id, input.StepRun.Id, subprocessRun.RunId, expectation.Id);
        var existingGap = await dbContext.Set<ProcessJournalEntry>()
            .AsNoTracking()
            .AnyAsync(
                item =>
                    item.ProcessRunId == input.Run.Id &&
                    item.StepRunId == input.StepRun.Id &&
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
                ProcessRunId = input.Run.Id,
                StepRunId = input.StepRun.Id,
                EventType = ProcessRuntimeEventTypes.ArtifactValidationDiagnostic,
                Title = $"Subprocess artifact projection gap: {expectation.Title}",
                Description = string.IsNullOrWhiteSpace(projectionDiagnostic)
                    ? $"Completed subprocess run '{subprocessRun.RunName}' did not produce a child artifact that can satisfy parent expectation '{expectation.Title}'."
                    : $"Completed subprocess run '{subprocessRun.RunName}' did not produce a child artifact that can satisfy parent expectation '{expectation.Title}'. {projectionDiagnostic}",
                CorrelationId = fingerprint,
                OperatingMode = input.Run.OperatingMode,
                PolicyVersion = $"definition-version:{input.Run.ProcessDefinitionVersionId:D}",
                EnvironmentMode = input.Run.OperatingMode.ToString(),
                ReplayContextJson = JsonSerializer.Serialize(new
                {
                    input.Run.Id,
                    StepRunId = input.StepRun.Id,
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
