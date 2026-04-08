using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.CrmHr;

internal sealed class CrmHrAutomationSignalProvider(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IClock clock) : IAutomationSignalSource
{
    public async Task<IReadOnlyList<AutomationSignalItem>> ListSignalsAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var now = clock.GetUtcNow();
        var items = new List<AutomationSignalItem>();

        var overdueFollowUpCandidates = await (
            from interaction in dbContext.Set<InteractionRecord>()
            join accountLink in dbContext.Set<InteractionPartyLink>() on interaction.Id equals accountLink.InteractionId
            join account in dbContext.Set<Party>() on accountLink.PartyId equals account.Id
            where accountLink.Role == InteractionPartyRole.Account
                  && !account.IsSensitive
            select new
            {
                interaction.Subject,
                interaction.NextActionText,
                interaction.NextActionDueUtc,
                AccountName = account.DisplayName
            })
            .ToListAsync(cancellationToken);

        var overdueFollowUps = overdueFollowUpCandidates
            .Where(item =>
                !string.IsNullOrWhiteSpace(item.NextActionText) &&
                item.NextActionDueUtc.HasValue &&
                item.NextActionDueUtc.Value <= now)
            .OrderBy(item => item.NextActionDueUtc)
            .Select(item => new
            {
                item.Subject,
                item.NextActionText,
                DueAtUtc = item.NextActionDueUtc!.Value,
                item.AccountName
            })
            .ToList();

        if (overdueFollowUps.Count > 0)
        {
            var earliestFollowUp = overdueFollowUps[0];
            items.Add(new AutomationSignalItem(
                "CRM / HR",
                "CRM follow-ups overdue",
                $"{overdueFollowUps.Count} next action(s) need attention. Earliest: {earliestFollowUp.AccountName} / {earliestFollowUp.Subject} / {earliestFollowUp.NextActionText}.",
                "/crm-hr/crm",
                "danger",
                earliestFollowUp.DueAtUtc,
                overdueFollowUps.Count));
        }

        var lifecycleTaskCandidates = await (
            from task in dbContext.Set<OnboardingTask>()
            join party in dbContext.Set<Party>() on task.PartyId equals party.Id
            where !party.IsSensitive
                  && task.Status != LifecycleTaskStatus.Completed
                  && task.Status != LifecycleTaskStatus.Cancelled
            select new
            {
                task.Title,
                task.Status,
                task.DueDateUtc,
                PartyName = party.DisplayName
            })
            .ToListAsync(cancellationToken);

        var dueLifecycleTasks = lifecycleTaskCandidates
            .Where(item => item.DueDateUtc.HasValue && item.DueDateUtc.Value <= now)
            .OrderBy(item => item.DueDateUtc)
            .Select(item => new
            {
                item.Title,
                item.Status,
                DueAtUtc = item.DueDateUtc!.Value,
                item.PartyName
            })
            .ToList();

        if (dueLifecycleTasks.Count > 0)
        {
            var earliestTask = dueLifecycleTasks[0];
            items.Add(new AutomationSignalItem(
                "CRM / HR",
                "Lifecycle tasks due or overdue",
                $"{dueLifecycleTasks.Count} onboarding or offboarding task(s) need follow-up. Earliest: {earliestTask.PartyName} / {earliestTask.Title} / {earliestTask.Status}.",
                "/crm-hr/recruiting",
                "warning",
                earliestTask.DueAtUtc,
                dueLifecycleTasks.Count));
        }

        return items
            .OrderBy(item => item.DueAtUtc ?? DateTimeOffset.MaxValue)
            .ToList();
    }
}
