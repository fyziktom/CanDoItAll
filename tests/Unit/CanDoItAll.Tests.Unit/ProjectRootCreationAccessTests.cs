using CanDoItAll.Editing;
using CanDoItAll.Infrastructure.Editing;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Unit;

public sealed class ProjectRootCreationAccessTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Held_private_read_never_returns_a_body_after_access_or_opening_retirement(bool stop) {
        var changes = new EditingChangeFeed();
        var access = new EditingUiTestAccess();
        var projectChanges = new ProjectChanges(changes, new(EditingScopeKind.Dataset, Guid.NewGuid(), 1), new(), []);
        await using var opening = new ProjectRootCreationAccess(changes, access, projectChanges, NullLogger<EditingObservation>.Instance);
        await opening.OpenAsync();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reading = opening.ReadAsync(async _ => {
            entered.TrySetResult();
            await release.Task;
            return "Private retained operation body";
        }, default);
        await entered.Task;

        if (stop) {
            opening.Stop();
        } else {
            access.Allowed = false;
        }
        release.TrySetResult();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => reading);
        Assert.Equal(0, changes.ActiveSubscriptions);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => opening.AuthorizeWriteAsync(default).AsTask());
    }
}
