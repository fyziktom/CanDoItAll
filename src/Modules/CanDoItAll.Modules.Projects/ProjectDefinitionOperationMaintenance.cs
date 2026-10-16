using CanDoItAll.Infrastructure.ControlPlane;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Projects;

public sealed class ProjectDefinitionOperationMaintenance(
    IServiceScopeFactory scopes,
    ICanonicalRuntimeDatabase database,
    DatasetActivationService activation,
    ILogger<ProjectDefinitionOperationMaintenance> logger) : BackgroundService {
    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        using var timer = new PeriodicTimer(ProjectDefinitionOperationOptions.MaintenanceInterval);
        do {
            if (database.Profile.Profile.ProviderKind == DatabaseProviderKind.PostgreSql) {
                using var budget = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                budget.CancelAfter(ProjectDefinitionOperationOptions.MaintenanceBudget);
                var deleted = 0;
                try {
                    var binding = await activation.ReadActiveBindingAsync(database.Profile.Profile.Id, budget.Token);
                    await using var scope = scopes.CreateAsyncScope();
                    var epochs = scope.ServiceProvider.GetRequiredService<ProjectOperationEpochService>();
                    await epochs.RotateExpiredAsync(binding, budget.Token);
                    for (var index = 0; index < ProjectDefinitionOperationOptions.MaximumMaintenanceBatches; index++) {
                        var batch = await epochs.PurgeRetiredBatchAsync(binding, budget.Token);
                        deleted += batch;
                        if (batch == 0) {
                            break;
                        }
                    }
                } catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) {
                    return;
                } catch (OperationCanceledException) when (budget.IsCancellationRequested) {
                    logger.LogInformation("Projects operation maintenance reached its bounded cycle budget after deleting {ReceiptCount} retired receipts for profile {ProfileId}.",
                        deleted, database.Profile.Profile.Id);
                } catch (Exception exception) {
                    logger.LogWarning(exception, "Projects operation maintenance did not finish for profile {ProfileId}; retained evidence remains for the next bounded maintenance pass.",
                        database.Profile.Profile.Id);
                }
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
