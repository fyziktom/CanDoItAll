using CanDoItAll.FileTools.FileInteraction;
using CanDoItAll.FileTools.Desktop;
using CanDoItAll.FileTools.Integration;
using CanDoItAll.Infrastructure.Storage;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Tests.Unit;

public sealed class FileToolsDownloadLeaseTests
{
    [Fact]
    public async Task Desktop_launcher_is_unavailable_until_the_host_explicitly_enables_it()
    {
        var launcher = new ConfiguredDesktopFileLauncher(
            Options.Create(new FileToolsDesktopLaunchOptions { Enabled = false }));
        var request = new DesktopFileLaunchRequest(
            Path.Combine(Path.GetTempPath(), "not-launched.xlsx"));

        DesktopFileLaunchResult result = await launcher.LaunchAsync(request);

        Assert.False(launcher.IsAvailable);
        Assert.False(result.Succeeded);
        Assert.Equal(DesktopFileLaunchFailureCode.DesktopUnavailable, result.Failure?.Code);
    }

    [Fact]
    public async Task Desktop_launcher_does_not_delegate_when_runtime_profile_is_headless()
    {
        var inner = new RecordingDesktopFileLauncher();
        var launcher = new ConfiguredDesktopFileLauncher(
            Options.Create(new FileToolsDesktopLaunchOptions
            {
                Enabled = true,
                HostProfileAllowsDesktop = false
            }),
            inner);
        var request = new DesktopFileLaunchRequest(
            Path.Combine(Path.GetTempPath(), "not-launched-headless.xlsx"));

        DesktopFileLaunchResult result = await launcher.LaunchAsync(request);

        Assert.False(launcher.IsAvailable);
        Assert.False(result.Succeeded);
        Assert.Equal(DesktopFileLaunchFailureCode.DesktopUnavailable, result.Failure?.Code);
        Assert.Equal(0, inner.LaunchCount);
    }

    [Fact]
    public async Task Desktop_launcher_delegates_when_the_host_enables_it()
    {
        var inner = new RecordingDesktopFileLauncher();
        var launcher = new ConfiguredDesktopFileLauncher(
            Options.Create(new FileToolsDesktopLaunchOptions
            {
                Enabled = true,
                HostProfileAllowsDesktop = true
            }),
            inner);
        var request = new DesktopFileLaunchRequest(
            Path.Combine(Path.GetTempPath(), "direct-source-launch.xlsx"));

        DesktopFileLaunchResult result = await launcher.LaunchAsync(request);

        Assert.True(launcher.IsAvailable);
        Assert.True(result.Succeeded);
        Assert.Equal(1, inner.LaunchCount);
    }

    [Fact]
    public async Task Lease_opens_content_and_revokes_the_authorization_exactly_once()
    {
        var file = new FileReference(AuthorizedFileReference.SourceId, "download-handle");
        var coordinator = new RecordingAuthorizationCoordinator();
        var lease = new AuthorizedFileToolsDownloadLease(
            file,
            "report.xlsx",
            new StaticContentSource(file),
            coordinator);

        await using FileContentLease content = await lease.OpenReadAsync();
        using var reader = new StreamReader(content.Stream);
        Assert.Equal("payload", await reader.ReadToEndAsync());

        await lease.DisposeAsync();
        await lease.DisposeAsync();

        Assert.Equal(file, coordinator.RevokedFile);
        Assert.Equal(1, coordinator.RevokeCount);
        Assert.Throws<ObjectDisposedException>(() => lease.OpenReadAsync());
    }

    private sealed class StaticContentSource(FileReference expectedFile) : IFileContentSource
    {
        public ValueTask<FileContentLease> OpenReadAsync(
            FileContentReadRequest request,
            CancellationToken cancellationToken = default)
        {
            Assert.Equal(expectedFile, request.File);
            return ValueTask.FromResult(new FileContentLease(
                new MemoryStream("payload"u8.ToArray()),
                "application/octet-stream",
                7));
        }
    }

    private sealed class RecordingAuthorizationCoordinator : IStorageFileAccessAuthorizationCoordinator
    {
        public int RevokeCount { get; private set; }

        public FileReference? RevokedFile { get; private set; }

        public ValueTask<FileReference> GrantAsync(
            FileAccessGrantRequest request,
            StorageObjectReference reference,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public ValueTask<AuthorizedStorageFile> ResolveAsync(
            FileReference file,
            FileAccessContext context,
            FileAccessOperation operation,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public ValueTask RevokeAsync(
            FileReference file,
            CancellationToken cancellationToken = default)
        {
            RevokeCount++;
            RevokedFile = file;
            return ValueTask.CompletedTask;
        }

        public ValueTask RevokeAllAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class RecordingDesktopFileLauncher : IDesktopFileLauncher
    {
        public bool IsAvailable => true;

        public int LaunchCount { get; private set; }

        public ValueTask<DesktopFileLaunchResult> LaunchAsync(
            DesktopFileLaunchRequest request,
            CancellationToken cancellationToken = default)
        {
            LaunchCount++;
            return ValueTask.FromResult(DesktopFileLaunchResult.Success(request.TargetPath));
        }
    }
}
