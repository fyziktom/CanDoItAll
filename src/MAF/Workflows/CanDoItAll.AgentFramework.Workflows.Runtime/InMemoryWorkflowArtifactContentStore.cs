using System.Collections.Concurrent;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public sealed class InMemoryWorkflowArtifactContentStore : IWorkflowArtifactContentStore
{
    private readonly ConcurrentDictionary<WorkflowArtifactId, WorkflowArtifactContent> contents = new();

    public Task SaveContentAsync(
        WorkflowArtifactRecord artifact,
        string content,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        cancellationToken.ThrowIfCancellationRequested();

        contents[artifact.Id] = new WorkflowArtifactContent(artifact, content ?? string.Empty);
        return Task.CompletedTask;
    }

    public Task<WorkflowArtifactContent?> ReadContentAsync(
        WorkflowArtifactRecord artifact,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        cancellationToken.ThrowIfCancellationRequested();

        contents.TryGetValue(artifact.Id, out var content);
        return Task.FromResult(content);
    }
}
