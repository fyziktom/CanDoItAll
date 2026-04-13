namespace CanDoItAll.SharedKernel;

/* codex-capsule
kind: contract
name: ActivityWriteRequest
summary: Describes one user-visible activity entry emitted by feature modules.
owns: category, action, title, route, artifact-link
deps: none
risks: missing-route, low-detail-entry
tests: unit:ActivityServiceTests
inputs: module action payloads
outputs: activity timeline writes
*/
public sealed record ActivityWriteRequest(
    string Category,
    string Action,
    string Title,
    string? Description = null,
    Guid? ProjectId = null,
    string? ArtifactKind = null,
    Guid? ArtifactId = null,
    string? Route = null,
    string? Actor = null,
    string? IdempotencyKey = null);

/* codex-capsule
kind: contract
name: IActivityStream
summary: Accepts cross-module activity events without forcing direct module references.
owns: activity-write boundary
deps: ActivityWriteRequest
risks: missed-instrumentation
tests: unit:ActivityServiceTests
inputs: ActivityWriteRequest
outputs: persisted activity entries
*/
public interface IActivityStream
{
    Task RecordAsync(ActivityWriteRequest request, CancellationToken cancellationToken = default);
}

public sealed class NullActivityStream : IActivityStream
{
    public Task RecordAsync(ActivityWriteRequest request, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
