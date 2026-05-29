using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Plugins;
using CanDoItAll.Modules.SchedulerPlanner;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Composition;

internal static class SchedulerPlannerWorkflowInputOptionProviderRegistrations
{
    public static IServiceCollection AddSchedulerPlannerWorkflowInputOptionProviders(this IServiceCollection services)
    {
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ISchedulerWorkflowInputOptionProvider, CrmContactEmailSchedulerWorkflowInputOptionProvider>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ISchedulerWorkflowInputOptionProvider, ProjectStructureSchedulerWorkflowInputOptionProvider>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ISchedulerWorkflowInputOptionProvider, ProjectStructureNodeSchedulerWorkflowInputOptionProvider>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ISchedulerWorkflowInputOptionProvider, Office365ConnectionSchedulerWorkflowInputOptionProvider>());
        return services;
    }
}

internal sealed class CrmContactEmailSchedulerWorkflowInputOptionProvider(
    PartyDirectoryService partyDirectoryService) : ISchedulerWorkflowInputOptionProvider
{
    public WorkflowInputParameterOptionSourceKind SourceKind => WorkflowInputParameterOptionSourceKind.CrmContacts;

    public async Task<IReadOnlyList<WorkflowInputParameterOption>> ListOptionsAsync(
        SchedulerWorkflowInputOptionQuery query,
        CancellationToken cancellationToken = default)
    {
        var parties = await partyDirectoryService.ListDirectoryAsync(cancellationToken);
        return parties
            .Where(party => !string.IsNullOrWhiteSpace(party.PrimaryEmail))
            .Select(party => new WorkflowInputParameterOption(
                party.PrimaryEmail.Trim(),
                $"{party.DisplayName} <{party.PrimaryEmail.Trim()}>",
                party.IsSensitive ? "Sensitive CRM contact" : $"{party.PartyType} / {party.LifecycleStatus}"))
            .ToArray();
    }
}

internal sealed class ProjectStructureSchedulerWorkflowInputOptionProvider(
    IProjectStructureRuntimeGateway projectStructureRuntimeGateway) : ISchedulerWorkflowInputOptionProvider
{
    public WorkflowInputParameterOptionSourceKind SourceKind => WorkflowInputParameterOptionSourceKind.ProjectStructureProjects;

    public async Task<IReadOnlyList<WorkflowInputParameterOption>> ListOptionsAsync(
        SchedulerWorkflowInputOptionQuery query,
        CancellationToken cancellationToken = default)
    {
        var projects = await projectStructureRuntimeGateway.ListProjectsAsync(cancellationToken);
        return projects
            .Select(project => new WorkflowInputParameterOption(
                project.Id.ToString("D"),
                project.Name,
                $"{project.Status} / {project.CurrentPhase}".TrimEnd(' ', '/')))
            .ToArray();
    }
}

internal sealed class ProjectStructureNodeSchedulerWorkflowInputOptionProvider(
    IProjectStructureRuntimeGateway projectStructureRuntimeGateway) : ISchedulerWorkflowInputOptionProvider
{
    public WorkflowInputParameterOptionSourceKind SourceKind => WorkflowInputParameterOptionSourceKind.ProjectStructureNodes;

    public async Task<IReadOnlyList<WorkflowInputParameterOption>> ListOptionsAsync(
        SchedulerWorkflowInputOptionQuery query,
        CancellationToken cancellationToken = default)
    {
        return await ListNodesAsync(query, cancellationToken);
    }

    private async Task<IReadOnlyList<WorkflowInputParameterOption>> ListNodesAsync(
        SchedulerWorkflowInputOptionQuery query,
        CancellationToken cancellationToken)
    {
        var dependsOnKey = query.Parameter.OptionSource.DependsOnParameterKey;
        if (string.IsNullOrWhiteSpace(dependsOnKey) ||
            !query.CurrentValues.TryGetValue(dependsOnKey, out var projectIdText) ||
            !Guid.TryParse(projectIdText, out var projectId) ||
            projectId == Guid.Empty)
        {
            return [];
        }

        var structure = await projectStructureRuntimeGateway.ReadStructureAsync(
            projectId,
            new ProjectStructureRuntimeReadRequest(Take: 500),
            cancellationToken);
        return structure.Nodes
            .Select(node => new WorkflowInputParameterOption(
                node.Id,
                node.Title,
                $"{node.ObjectType} / {node.Status}"))
            .ToArray();
    }
}

internal sealed class Office365ConnectionSchedulerWorkflowInputOptionProvider(
    PluginConnectionStore connectionStore) : ISchedulerWorkflowInputOptionProvider
{
    public WorkflowInputParameterOptionSourceKind SourceKind => WorkflowInputParameterOptionSourceKind.Office365Connections;

    public async Task<IReadOnlyList<WorkflowInputParameterOption>> ListOptionsAsync(
        SchedulerWorkflowInputOptionQuery query,
        CancellationToken cancellationToken = default)
    {
        var connections = await connectionStore.ListAsync(Office365PluginConstants.PluginId, cancellationToken);
        return connections
            .Where(connection => connection.IsEnabled)
            .Where(connection => connection.ConnectionKey == Office365PluginConstants.ConnectionKey)
            .Select(connection => new WorkflowInputParameterOption(
                connection.Id.Value.ToString("D"),
                connection.DisplayName,
                string.IsNullOrWhiteSpace(connection.HealthStatus) ? "Enabled Office365 connection" : connection.HealthStatus))
            .ToArray();
    }
}
