using CanDoItAll.FileTools.Integration;
using Xunit.Abstractions;

namespace CanDoItAll.Tests.Unit.Storage;

public sealed partial class FileToolsBrowseItemActionServiceTests {
    private readonly ITestOutputHelper authorityOutput;
    public FileToolsBrowseItemActionServiceTests(ITestOutputHelper output) => authorityOutput = output;
    public enum LaunchAdmission { Allowed, StructureReadDenied, NativeFileDenied }

    [Theory]
    [InlineData(false, LaunchAdmission.Allowed)]
    [InlineData(false, LaunchAdmission.StructureReadDenied)]
    [InlineData(false, LaunchAdmission.NativeFileDenied)]
    [InlineData(true, LaunchAdmission.Allowed)]
    [InlineData(true, LaunchAdmission.StructureReadDenied)]
    [InlineData(true, LaunchAdmission.NativeFileDenied)]
    public async Task Native_local_launch_rechecks_original_read_and_native_file_owner_before_OS_dispatch(bool known, LaunchAdmission admission) {
        var fixture = new ActionFixture("report.xlsx", recordOwnedRoot: path => authorityOutput.WriteLine("Owned native file action root before creation: " + path));
        try {
            var allowed = true;
            fixture.Preferences.AfterResolve = () => {
                allowed = admission != LaunchAdmission.StructureReadDenied;
                if (admission == LaunchAdmission.NativeFileDenied) {
                    fixture.Authorization.ResolveFailure = new(FileAccessFailureCode.Forbidden, "Native file permission changed during launch preparation.");
                }
            };
            ValueTask AuthorizeReadAsync(CancellationToken token) {
                token.ThrowIfCancellationRequested();
                if (!allowed) {
                    throw new UnauthorizedAccessException("The original Structure read grant retired.");
                }
                return ValueTask.CompletedTask;
            }
            var pending = known ? fixture.Sut.LaunchAsync(fixture.Scope,
                new FileToolsKnownFileOccurrence(fixture.Storage.Id, FileToolsKnownFileOccurrenceKind.RelativePath,
                    "files/report.xlsx", "report.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", size: null),
                FileToolsLocalFileAction.OpenInPreferredApplication, authorizeRead: AuthorizeReadAsync).AsTask()
                : fixture.Sut.LaunchAsync(fixture.Scope, fixture.ItemKey, FileToolsLocalFileAction.OpenInPreferredApplication,
                    authorizeRead: AuthorizeReadAsync).AsTask();
            if (admission == LaunchAdmission.Allowed) {
                Assert.True((await pending).IsSuccess);
                Assert.Equal(1, fixture.Launcher.CallCount);
            } else {
                if (admission == LaunchAdmission.StructureReadDenied) {
                    await Assert.ThrowsAsync<UnauthorizedAccessException>(() => pending);
                } else {
                    await Assert.ThrowsAsync<FileAccessDeniedException>(() => pending);
                }
                Assert.Equal(0, fixture.Launcher.CallCount);
            }
            Assert.Equal(1, fixture.Authorization.GrantCount);
            Assert.Equal(1, fixture.Authorization.RevokeCount);
        } finally {
            fixture.Dispose();
            Assert.False(Directory.Exists(fixture.OwnedRootPath));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Native_download_lease_keeps_original_collection_read_for_later_body_reads(bool denyAfterIssue) {
        var fixture = new ActionFixture("report.xlsx", recordOwnedRoot: path => authorityOutput.WriteLine("Owned native download root before creation: " + path));
        try {
            var allowed = true;
            await using var lease = await fixture.Sut.AuthorizeDownloadAsync(fixture.Scope, fixture.ItemKey, authorizeRead: token => {
                token.ThrowIfCancellationRequested();
                if (!allowed) {
                    throw new UnauthorizedAccessException("The original collection read retired after download issue.");
                }
                return ValueTask.CompletedTask;
            });
            allowed = !denyAfterIssue;
            if (denyAfterIssue) {
                await Assert.ThrowsAsync<UnauthorizedAccessException>(() => lease.OpenReadAsync().AsTask());
            } else {
                await using var body = await lease.OpenReadAsync();
                using var reader = new StreamReader(body.Stream);
                Assert.Equal("download payload", await reader.ReadToEndAsync());
            }
            await lease.DisposeAsync();
            Assert.Equal(1, fixture.Authorization.RevokeCount);
        } finally {
            fixture.Dispose();
            Assert.False(Directory.Exists(fixture.OwnedRootPath));
        }
    }
}
