using CanDoItAll.FileTools.FileInteraction;
using CanDoItAll.FileTools.Integration;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Unit.Storage;

public sealed partial class FileAccessAuthorizationTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Original_file_handle_cannot_retarget_a_new_source_before_dispatch(bool write) {
        await using var fixture = CreateFixture();
        var file = await fixture.GrantAsync(FileAccessOperation.View | FileAccessOperation.Edit);
        await using var scope = fixture.Services.CreateAsyncScope();
        var session = await scope.ServiceProvider.GetRequiredService<IFileToolsKnownFileSessionFactory>()
            .CreateAsync(new(fixture.Scope, file, FileToolsKnownFileIntent.Edit));
        fixture.Driver.ResetEffectCalls();
        fixture.Storage.EndpointOrRoot = "replacement-before-dispatch";
        await Assert.ThrowsAsync<FileAccessDeniedException>(async () => {
            if (write) {
                await session.SaveTarget!.SaveAsync(new(file, 1, new BufferedFileSaveContent("denied"u8.ToArray()), new FileContentRevision("r1")));
            } else {
                await using var denied = await session.ContentSource.OpenReadAsync(new(file));
            }
        });
        Assert.Equal(0, fixture.Driver.EffectCalls);
        Assert.Equal("initial-content"u8.ToArray(), fixture.Driver.Content);
    }

    [Fact]
    public async Task Native_file_read_withholds_its_body_when_original_binding_retires_during_read() {
        await using var fixture = CreateFixture();
        var file = await fixture.GrantAsync(FileAccessOperation.View);
        await using var scope = fixture.Services.CreateAsyncScope();
        var session = await scope.ServiceProvider.GetRequiredService<IFileToolsKnownFileSessionFactory>()
            .CreateAsync(new(fixture.Scope, file, FileToolsKnownFileIntent.ReadOnly));
        fixture.Driver.AfterReadOpened = () => fixture.BindingProvider.IsAvailable = false;
        await Assert.ThrowsAsync<FileAccessDeniedException>(async () => {
            await using var denied = await session.ContentSource.OpenReadAsync(new(file));
        });
        Assert.Equal(1, fixture.Driver.OpenReadCalls);
        Assert.Equal(0, fixture.Driver.ReplaceCalls);
    }

    public enum NativeFileAuthorityChange { None, ReadOnly, Binding, Endpoint }

    [Theory]
    [InlineData(NativeFileAuthorityChange.None)]
    [InlineData(NativeFileAuthorityChange.ReadOnly)]
    [InlineData(NativeFileAuthorityChange.Binding)]
    [InlineData(NativeFileAuthorityChange.Endpoint)]
    public async Task Native_file_owner_revalidates_its_original_source_after_submission_reads(NativeFileAuthorityChange change) {
        await using var fixture = CreateFixture();
        var file = await fixture.GrantAsync(FileAccessOperation.View | FileAccessOperation.Edit);
        await using var scope = fixture.Services.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<IFileToolsKnownFileSessionFactory>();
        var session = await factory.CreateAsync(new(fixture.Scope, file, FileToolsKnownFileIntent.Edit));
        var original = fixture.Driver.Content.ToArray();
        var content = new NativeAuthorityChangingContent(() => {
            switch (change) {
                case NativeFileAuthorityChange.ReadOnly:
                    fixture.Storage.IsReadOnly = true;
                    break;
                case NativeFileAuthorityChange.Binding:
                    fixture.BindingProvider.IsAvailable = false;
                    break;
                case NativeFileAuthorityChange.Endpoint:
                    fixture.Storage.EndpointOrRoot = "replacement-native-source";
                    break;
            }
        });
        var request = new FileSaveRequest(file, 1, content, new FileContentRevision("r1"));
        if (change == NativeFileAuthorityChange.None) {
            var accepted = await session.SaveTarget!.SaveAsync(request);
            Assert.Equal(new FileContentRevision("r2"), accepted.PersistedRevision);
            Assert.Equal("submitted native bytes"u8.ToArray(), fixture.Driver.Content);
        } else {
            var failure = await Record.ExceptionAsync(async () => await session.SaveTarget!.SaveAsync(request));
            Assert.Equal(original, fixture.Driver.Content);
            Assert.Equal("r1", fixture.Driver.Revision);
            Assert.Equal(0, fixture.Driver.ReplaceCalls);
            Assert.IsType<FileAccessDeniedException>(failure);
        }
    }

    private sealed class NativeAuthorityChangingContent(Action change) : IFileSaveContent {
        public long? Length => "submitted native bytes"u8.Length;
        public ValueTask<Stream> OpenReadAsync(CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();
            change();
            return ValueTask.FromResult<Stream>(new MemoryStream("submitted native bytes"u8.ToArray(), writable: false));
        }
    }
}
