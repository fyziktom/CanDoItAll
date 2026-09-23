using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Security;

namespace CanDoItAll.Modules.AgentFramework;

public interface IAgentEditorAccessQuery {
    Task<IReadOnlyList<AgentEditorProject>> ReadProjectsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AgentEditorSecret>> ReadSecretsAsync(CancellationToken cancellationToken = default);
}

public sealed class AgentEditorAccessQuery(ProjectsService projects, SecretService secrets) : IAgentEditorAccessQuery {
    public async Task<IReadOnlyList<AgentEditorProject>> ReadProjectsAsync(CancellationToken cancellationToken = default)
        => (await projects.ListAccessListAsync(cancellationToken)).Select(project => new AgentEditorProject(project.Id, project.Name)).ToArray();

    public async Task<IReadOnlyList<AgentEditorSecret>> ReadSecretsAsync(CancellationToken cancellationToken = default)
        => (await secrets.ListForPickerAsync(cancellationToken)).Select(secret => new AgentEditorSecret(secret.Id, secret.Name, secret.Kind.ToString())).ToArray();
}
