using CanDoItAll.AgentFramework.Workflows.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Workbench;

public sealed class ProjectStructureWorkflowDeliveryWorker(
    IServiceScopeFactory scopes, TimeProvider clock, ILogger<ProjectStructureWorkflowDeliveryWorker> logger) : BackgroundService {
    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        while (!stoppingToken.IsCancellationRequested) {
            try {
                await RunBatchAsync(stoppingToken);
            } catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) {
                break;
            } catch (Exception exception) {
                logger.LogWarning(exception, "Workflow Structure delivery batch failed; durable admissions and receipts remain pending.");
            }

            try {
                await Task.Delay(TimeSpan.FromSeconds(10), clock, stoppingToken);
            } catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) {
                break;
            }
        }
    }

    public async Task RunBatchAsync(CancellationToken cancellationToken = default) {
        await using var scope = scopes.CreateAsyncScope();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var outputs = scope.ServiceProvider.GetRequiredService<IWorkflowStructureOutputStore>();
        foreach (var output in await outputs.ListPendingAsync(128, cancellationToken)) {
            try {
                var receipt = await workbench.FindWorkflowContributionAsync(output.Plan.Identity, cancellationToken);
                if (receipt is null) {
                    await scope.ServiceProvider.GetRequiredService<WorkbenchProjectStructureRuntimeGateway>()
                        .ReconcileWorkflowOutputAsync(output.Plan.Identity, cancellationToken);
                    receipt = await workbench.FindWorkflowContributionAsync(output.Plan.Identity, cancellationToken);
                }
                if (receipt is not null) {
                    await outputs.CompleteAsync(receipt.Receipt!, cancellationToken);
                }
            } catch (Exception exception) when (exception is not OperationCanceledException) {
                logger.LogWarning(exception, "Workflow output receipt reconciliation failed for {RunId}/{Occurrence}/{Slot}.",
                    output.Plan.Identity.Occurrence.RunId, output.Plan.Identity.Occurrence.Path, output.Plan.Identity.Slot);
            } finally {
                await outputs.DeferInspectionAsync(output.Plan.Identity, cancellationToken);
            }
        }

        var workflows = scope.ServiceProvider.GetRequiredService<ProjectStructureWorkflowNodeService>();
        foreach (var admission in await workbench.ListWorkflowAdmissionsForDeliveryAsync(128, cancellationToken)) {
            try {
                await workflows.ReconcileAsync(admission.Binding.IntentId, cancellationToken);
            } catch (Exception exception) when (exception is not OperationCanceledException) {
                logger.LogWarning(exception, "Workflow Structure projection remains pending for intent {IntentId} and run {RunId}.",
                    admission.Binding.IntentId, admission.Binding.RunId);
                await workbench.DeferWorkflowAdmissionDeliveryAsync(admission.Binding.IntentId, cancellationToken);
            }
        }
    }
}
