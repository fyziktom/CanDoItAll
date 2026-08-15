using CanDoItAll.AgentFramework.Persistence;

namespace CanDoItAll.Tests.Integration.AgentFramework;

public sealed class AgentFrameworkPersistenceIntegrationTests
{
    [Fact]
    public async Task WriteJsonIfChangedAsync_retries_atomic_replace_until_transient_read_lock_releases()
    {
        var workspaceRoot = Path.Combine(Path.GetTempPath(), "candoitall-agentframework-persistence", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workspaceRoot);

        try
        {
            var store = new FileSandboxWorkspaceJsonStore();
            var filePath = Path.Combine(workspaceRoot, "execution-index.json");
            await store.WriteJsonAtomicallyAsync(filePath, new SampleRecord("initial", 1), CancellationToken.None);

            using var readLock = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var releaseLockTask = Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromMilliseconds(150));
                readLock.Dispose();
            });

            var changed = await store.WriteJsonIfChangedAsync(
                filePath,
                new SampleRecord("updated", 2),
                CancellationToken.None);

            await releaseLockTask;

            Assert.True(changed);

            var persisted = await store.ReadJsonAsync<SampleRecord>(filePath, CancellationToken.None);
            Assert.NotNull(persisted);
            Assert.Equal("updated", persisted!.Name);
            Assert.Equal(2, persisted.Version);
        }
        finally
        {
            if (Directory.Exists(workspaceRoot))
            {
                Directory.Delete(workspaceRoot, recursive: true);
            }
        }
    }

    private sealed record SampleRecord(string Name, int Version);
}
