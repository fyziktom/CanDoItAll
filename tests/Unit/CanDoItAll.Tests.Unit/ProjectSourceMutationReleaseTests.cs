using CanDoItAll.Modules.Projects;

namespace CanDoItAll.Tests.Unit;

public sealed class ProjectSourceMutationReleaseTests {
    [Fact]
    public async Task Concurrent_disposal_waits_for_the_same_original_source_release() {
        var source = new ControlledSource();
        var owner = CreateOwner(source);
        var first = owner.DisposeAsync().AsTask();
        var second = owner.DisposeAsync().AsTask();

        Assert.Same(first, second);
        Assert.False(first.IsCompleted);
        Assert.False(second.IsCompleted);
        Assert.Equal(1, source.DisposeCalls);
        source.Release.SetResult();
        await Task.WhenAll(first, second);
        await owner.DisposeAsync();
        Assert.Equal(1, source.DisposeCalls);
    }

    [Fact]
    public async Task Failed_source_release_cannot_be_reinterpreted_as_success_by_another_disposal() {
        var source = new ControlledSource();
        var owner = CreateOwner(source);
        var originalFailure = new IOException("Source release failed.");
        var first = owner.DisposeAsync().AsTask();
        source.Release.SetException(originalFailure);

        Assert.Same(originalFailure, await Assert.ThrowsAsync<IOException>(() => first));
        var later = owner.DisposeAsync().AsTask();
        Assert.Same(first, later);
        Assert.Same(originalFailure, await Assert.ThrowsAsync<IOException>(() => later));
        Assert.Equal(1, source.DisposeCalls);
    }

    [Fact]
    public async Task Synchronously_thrown_source_release_failure_is_retained_without_repeating_release() {
        var originalFailure = new IOException("Synchronous source release failure.");
        var source = new ControlledSource { SynchronousFailure = originalFailure };
        var owner = CreateOwner(source);

        Assert.Same(originalFailure, await Assert.ThrowsAsync<IOException>(() => owner.DisposeAsync().AsTask()));
        Assert.Same(originalFailure, await Assert.ThrowsAsync<IOException>(() => owner.DisposeAsync().AsTask()));
        Assert.Equal(1, source.DisposeCalls);
    }

    private static ProjectSourceMutationScope CreateOwner(IProjectMutationSourceLease source)
        => new(null!, null, null, source, [], null!, null!);

    private sealed class ControlledSource : IProjectMutationSourceLease {
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public IReadOnlyCollection<ProjectWriteAdmission> SourceProjects => [];
        public IOException? SynchronousFailure { get; init; }
        public int DisposeCalls { get; private set; }

        public Task RequireForMutationAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Disposal must not reacquire mutation authority.");

        public ValueTask DisposeAsync() {
            DisposeCalls++;
            if (SynchronousFailure is not null) {
                throw SynchronousFailure;
            }
            return new(Release.Task);
        }
    }
}
