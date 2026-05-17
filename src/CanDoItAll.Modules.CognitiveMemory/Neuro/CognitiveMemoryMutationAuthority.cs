using System.Text.Json;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.CognitiveMemory;

public sealed class CognitiveMemoryMutationAuthority(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IClock clock) : ICognitiveMemoryMutationAuthority
{
    public async ValueTask<CognitiveMemoryMutationResult> SubmitAsync(
        CognitiveMemoryMutationCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ValidateCommand(command);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var existingCommand = await dbContext.Set<CognitiveMemoryMutationCommandRecord>()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                storedCommand => storedCommand.ProjectId == command.ProjectId &&
                                 storedCommand.IdempotencyKey == command.IdempotencyKey.Value,
                cancellationToken);

        if (existingCommand is not null)
        {
            return await CreateIdempotentReplayResultAsync(dbContext, existingCommand, cancellationToken);
        }

        var now = clock.GetUtcNow();
        var status = ResolveStatus(command, out var reviewReason);
        var commandRecord = new CognitiveMemoryMutationCommandRecord
        {
            Id = Guid.NewGuid(),
            ProjectId = command.ProjectId,
            CommandKind = command.CommandKind,
            Status = status,
            ActorKind = command.ActorKind,
            ActorId = command.ActorId.Trim(),
            IdempotencyKey = command.IdempotencyKey.Value,
            AffectedMemoryRecordIdsJson = SerializeGuidList(command.AffectedMemoryRecordIds),
            AffectedClaimIdsJson = SerializeGuidList(command.AffectedClaimIds),
            EvidenceAnchorIdsJson = SerializeGuidList(command.EvidenceAnchorIds),
            PayloadJson = string.IsNullOrWhiteSpace(command.PayloadJson) ? "{}" : command.PayloadJson,
            ExpectedVersionToken = command.ExpectedVersionToken ?? string.Empty,
            RequiresHumanReview = command.RequiresHumanReview,
            ReviewReason = reviewReason ?? string.Empty,
            ResultVersionToken = CreateResultVersionToken(command, status),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        var auditEvent = new CognitiveMemoryMutationAuditEventRecord
        {
            Id = Guid.NewGuid(),
            MutationCommandId = commandRecord.Id,
            ProjectId = command.ProjectId,
            Sequence = 1,
            EventKind = ToAuditEventKind(status),
            Message = CreateAuditMessage(commandRecord),
            CreatedAtUtc = now
        };

        dbContext.Set<CognitiveMemoryMutationCommandRecord>().Add(commandRecord);
        dbContext.Set<CognitiveMemoryMutationAuditEventRecord>().Add(auditEvent);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new CognitiveMemoryMutationResult(
            commandRecord.Id,
            status != CognitiveMemoryMutationCommandStatus.Rejected,
            Applied: false,
            status == CognitiveMemoryMutationCommandStatus.ReviewRequired,
            reviewReason,
            commandRecord.ResultVersionToken,
            [auditEvent.Id],
            CreateWarnings(status));
    }

    private static void ValidateCommand(CognitiveMemoryMutationCommand command)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command.ActorId);
        ArgumentNullException.ThrowIfNull(command.AffectedMemoryRecordIds);
        ArgumentNullException.ThrowIfNull(command.AffectedClaimIds);
        ArgumentNullException.ThrowIfNull(command.EvidenceAnchorIds);
    }

    private static CognitiveMemoryMutationCommandStatus ResolveStatus(
        CognitiveMemoryMutationCommand command,
        out string? reviewReason)
    {
        if (CognitiveMemoryNeuroFoundationPolicies.RequiresEvidenceAnchors(command.CommandKind) &&
            command.EvidenceAnchorIds.Count == 0)
        {
            reviewReason = "Evidence anchors are required before authoritative claim or evidence mutation commands can proceed.";
            return CognitiveMemoryMutationCommandStatus.Rejected;
        }

        if (command.RequiresHumanReview)
        {
            reviewReason = "Command requires human review before authoritative claim state can change.";
            return CognitiveMemoryMutationCommandStatus.ReviewRequired;
        }

        reviewReason = null;
        return CognitiveMemoryMutationCommandStatus.Accepted;
    }

    private static async Task<CognitiveMemoryMutationResult> CreateIdempotentReplayResultAsync(
        AppDbContext dbContext,
        CognitiveMemoryMutationCommandRecord existingCommand,
        CancellationToken cancellationToken)
    {
        var auditEventIds = await dbContext.Set<CognitiveMemoryMutationAuditEventRecord>()
            .AsNoTracking()
            .Where(auditEvent => auditEvent.MutationCommandId == existingCommand.Id)
            .OrderBy(auditEvent => auditEvent.Sequence)
            .Select(auditEvent => auditEvent.Id)
            .ToListAsync(cancellationToken);

        return new CognitiveMemoryMutationResult(
            existingCommand.Id,
            existingCommand.Status != CognitiveMemoryMutationCommandStatus.Rejected,
            Applied: false,
            existingCommand.Status == CognitiveMemoryMutationCommandStatus.ReviewRequired,
            string.IsNullOrWhiteSpace(existingCommand.ReviewReason) ? null : existingCommand.ReviewReason,
            existingCommand.ResultVersionToken,
            auditEventIds,
            ["Idempotent replay returned the original mutation command; no additional audit event was created."]);
    }

    private static CognitiveMemoryMutationAuditEventKind ToAuditEventKind(CognitiveMemoryMutationCommandStatus status)
        => status switch
        {
            CognitiveMemoryMutationCommandStatus.Rejected => CognitiveMemoryMutationAuditEventKind.Rejected,
            CognitiveMemoryMutationCommandStatus.ReviewRequired => CognitiveMemoryMutationAuditEventKind.ReviewRequired,
            _ => CognitiveMemoryMutationAuditEventKind.AcceptedForHandler
        };

    private static string CreateAuditMessage(CognitiveMemoryMutationCommandRecord command)
        => command.Status switch
        {
            CognitiveMemoryMutationCommandStatus.Rejected => command.ReviewReason,
            CognitiveMemoryMutationCommandStatus.ReviewRequired => command.ReviewReason,
            _ => "Mutation command accepted for a governed downstream operation handler; no claim truth was changed by command submission."
        };

    private static IReadOnlyList<string> CreateWarnings(CognitiveMemoryMutationCommandStatus status)
        => status == CognitiveMemoryMutationCommandStatus.Accepted
            ? ["Command accepted only into the mutation authority ledger; downstream handlers must apply accepted operations explicitly."]
            : [];

    private static string CreateResultVersionToken(
        CognitiveMemoryMutationCommand command,
        CognitiveMemoryMutationCommandStatus status)
        => CognitiveMemoryHash
            .FromUtf8($"{command.ProjectId:D}|{command.CommandKind}|{command.IdempotencyKey}|{status}")
            .Value;

    private static string SerializeGuidList(IReadOnlyList<Guid> values)
        => JsonSerializer.Serialize(
            values.ToArray(),
            CognitiveMemoryJsonSerializerContext.Default.GuidArray);
}
