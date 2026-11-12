using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CanDoItAll.Modules.AgentFramework;

public sealed class FloatingAgentChatSettingsService(
    IDbContextFactory<WorkflowDbContext> dbContextFactory,
    TimeProvider timeProvider,
    WorkflowChanges changes) : IFloatingAgentChatSettingsService
{
    private const string SettingsId = "floating-agent-chats.v1";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<FloatingAgentChatSettings> GetSettingsAsync(
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var record = await dbContext.Set<WorkflowSettingsRecord>()
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == SettingsId, cancellationToken);
        if (record is null)
        {
            return FloatingAgentChatSettings.Default;
        }

        var settings = JsonSerializer.Deserialize<FloatingAgentChatSettings>(
            record.SettingsJson,
            JsonOptions);
        return FloatingAgentChatSettingsValidator.Normalize(settings
            ?? throw new InvalidOperationException("Stored floating chat settings are invalid.")) with { UpdatedAtUtc = record.UpdatedAtUtc };
    }

    public async Task<FloatingAgentChatSettings> SaveSettingsAsync(
        FloatingAgentChatSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings = FloatingAgentChatSettingsValidator.Normalize(settings);
        await changes.DemandCommitAsync(cancellationToken);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var record = await dbContext.Set<WorkflowSettingsRecord>()
            .SingleOrDefaultAsync(item => item.Id == SettingsId, cancellationToken);
        if (settings.UpdatedAtUtc != record?.UpdatedAtUtc) {
            throw new WorkflowSettingsConcurrencyException();
        }
        var accepted = settings with { UpdatedAtUtc = WorkflowEditRevision.Next(record?.UpdatedAtUtc, timeProvider.GetUtcNow()) };
        var settingsJson = JsonSerializer.Serialize(settings with { UpdatedAtUtc = null }, JsonOptions);
        if (record is null)
        {
            dbContext.Set<WorkflowSettingsRecord>().Add(new WorkflowSettingsRecord
            {
                Id = SettingsId,
                SettingsJson = settingsJson,
                UpdatedAtUtc = accepted.UpdatedAtUtc.Value
            });
        }
        else
        {
            record.SettingsJson = settingsJson;
            record.UpdatedAtUtc = accepted.UpdatedAtUtc.Value;
        }

        try {
            await changes.DemandCommitAsync(cancellationToken);
            await using var transaction = dbContext.Database.IsRelational()
                ? await dbContext.Database.BeginTransactionAsync(cancellationToken) : null;
            await dbContext.SaveChangesAsync(cancellationToken);
            await changes.DemandCommitAsync(cancellationToken);
            if (transaction is not null) {
                await transaction.CommitAsync(cancellationToken);
            }
        } catch (DbUpdateConcurrencyException exception) {
            throw new WorkflowSettingsConcurrencyException(exception);
        } catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation }) {
            throw new WorkflowSettingsConcurrencyException(exception);
        }
        changes.PublishSettings(CanDoItAll.Editing.EditingOwner.FloatingChatSettings, accepted);
        return accepted;
    }
}
