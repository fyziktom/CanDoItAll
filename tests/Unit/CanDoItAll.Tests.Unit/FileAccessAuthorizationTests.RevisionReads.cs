using CanDoItAll.FileTools.FileInteraction;
using CanDoItAll.FileTools.Integration;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Unit.Storage;

public sealed partial class FileAccessAuthorizationTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Native_mutable_read_uses_actual_current_size_and_refuses_growth_beyond_the_bound(bool oversized) {
        await using var fixture = CreateFixture();
        var file = await fixture.GrantAsync(FileAccessOperation.View);
        await using var scope = fixture.Services.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<IFileToolsKnownFileSessionFactory>();
        var session = await factory.CreateAsync(new(fixture.Scope, file, FileToolsKnownFileIntent.ReadOnly));
        fixture.Driver.SetPeerContent(oversized ? new byte[1024 * 1024 + 1] : "new"u8.ToArray());
        if (oversized) {
            var failure = await Assert.ThrowsAsync<FileAccessDeniedException>(async () => {
                await using var lease = await session.ContentSource.OpenReadAsync(new(file));
            });
            Assert.Equal(FileAccessFailureCode.ContentTooLarge, failure.Code);
        } else {
            await using var lease = await session.ContentSource.OpenReadAsync(new(file));
            using var reader = new StreamReader(lease.Stream);
            Assert.Equal("new", await reader.ReadToEndAsync());
            Assert.Equal(3, lease.Length);
            Assert.Equal(new FileContentRevision("r2"), lease.Revision);
        }
        Assert.Equal(0, fixture.Driver.ReplaceCalls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Native_file_read_does_not_tag_an_old_open_stream_with_a_new_peer_revision(bool peerReplacement) {
        await using var fixture = CreateFixture();
        var file = await fixture.GrantAsync(FileAccessOperation.View);
        await using var scope = fixture.Services.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<IFileToolsKnownFileSessionFactory>();
        var session = await factory.CreateAsync(new(fixture.Scope, file, FileToolsKnownFileIntent.ReadOnly));
        fixture.Driver.ReplaceAfterReadOpened = peerReplacement;
        if (peerReplacement) {
            var failure = await Assert.ThrowsAsync<FileAccessDeniedException>(async () => {
                await using var lease = await session.ContentSource.OpenReadAsync(new(file));
            });
            Assert.Equal(FileAccessFailureCode.SourceUnavailable, failure.Code);
            Assert.Equal("peer-content"u8.ToArray(), fixture.Driver.Content);
        } else {
            await using var lease = await session.ContentSource.OpenReadAsync(new(file));
            using var reader = new StreamReader(lease.Stream);
            Assert.Equal("initial-content", await reader.ReadToEndAsync());
            Assert.Equal(new FileContentRevision("r1"), lease.Revision);
        }
        Assert.Equal(1, fixture.Driver.OpenReadCalls);
        Assert.Equal(0, fixture.Driver.ReplaceCalls);
    }
}
