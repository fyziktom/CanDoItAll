using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.AgentFramework;

public sealed class FloatingAgentChatSettingsService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    TimeProvider timeProvider) : IFloatingAgentChatSettingsService
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
        return FloatingAgentChatSettingsValidator.Normalize(
            settings ?? FloatingAgentChatSettings.Default);
    }

    public async Task<FloatingAgentChatSettings> SaveSettingsAsync(
        FloatingAgentChatSettings settings,
        CancellationToken cancellationToken = default)
    {
        settings = FloatingAgentChatSettingsValidator.Normalize(settings);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var record = await dbContext.Set<WorkflowSettingsRecord>()
            .SingleOrDefaultAsync(item => item.Id == SettingsId, cancellationToken);
        var settingsJson = JsonSerializer.Serialize(settings, JsonOptions);
        var now = timeProvider.GetUtcNow();
        if (record is null)
        {
            dbContext.Set<WorkflowSettingsRecord>().Add(new WorkflowSettingsRecord
            {
                Id = SettingsId,
                SettingsJson = settingsJson,
                UpdatedAtUtc = now
            });
        }
        else
        {
            record.SettingsJson = settingsJson;
            record.UpdatedAtUtc = now;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return settings;
    }
}
