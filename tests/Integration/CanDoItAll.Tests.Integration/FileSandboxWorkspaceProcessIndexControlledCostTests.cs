using Xunit.Abstractions;

namespace CanDoItAll.Tests.Integration.Runtime;

[Trait("Category", "FileSystemPortability")]
[Trait("Category", "HostPlatform")]
public sealed class FileSandboxWorkspaceProcessIndexControlledCostTests(ITestOutputHelper output) {
    [Fact]
    public Task Small_history_real_owner_cost_sample_retains_every_accepted_revision()
        => new FileSandboxWorkspaceProcessIndexCostTests(output)
            .Actual_owner_writes_and_concurrent_pages_preserve_each_accepted_revision(0, 1);
}
