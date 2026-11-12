using System.Text;
using CanDoItAll.AgentFramework.CapabilityAuthoring.UiSandbox;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.BaseLib;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.AgentFramework.CapabilityAuthoring.UI.Tests;

public sealed class CapabilityUploadTests {
    [Fact]
    public async Task Bounded_upload_decodes_BOM_and_Unicode_without_saving_or_testing() {
        var fixture = new CapabilityAuthoringScenario(AuthoringScenario.NewSkill);
        using var session = Create(fixture);
        var file = new File("SKILL.md", [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes("# Review Ω\nŽluťoučký")]);
        await session.UploadAsync(file);
        Assert.Equal(CapabilityAuthoringSession.MaximumSkillUploadBytes, file.RequestedLimit);
        Assert.Equal("# Review Ω\nŽluťoučký", session.Draft.Skill.InlineInstructions);
        Assert.Equal("SKILL.md", session.Draft.UploadedFileName);
        Assert.Equal(0, fixture.Saves);
        Assert.Equal(0, fixture.Setups);
    }

    [Fact]
    public async Task Oversized_file_is_refused_and_retains_existing_content() {
        using var session = Create(new(AuthoringScenario.NewSkill));
        session.Draft.Skill.InlineInstructions = "Existing";
        var file = new File("oversized.md", new byte[CapabilityAuthoringSession.MaximumSkillUploadBytes + 1]);
        await session.UploadAsync(file);
        Assert.Equal("Existing", session.Draft.Skill.InlineInstructions);
        Assert.Empty(session.Draft.UploadedFileName);
        Assert.False(session.Busy);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Pending_file_cannot_replace_later_text_or_mode(bool changeMode) {
        using var session = Create(new(AuthoringScenario.NewSkill));
        var file = new File("late.md", Encoding.UTF8.GetBytes("Late"), held: true);
        var upload = session.UploadAsync(file);
        Assert.True(session.Busy);
        session.Draft.Skill.InlineInstructions = "Later user text";
        if (changeMode) {
            session.Draft.SkillMode = CapabilitySkillInputMode.Registered;
        }
        session.Draft.Changed();
        file.Release();
        await upload;
        Assert.Equal("Later user text", session.Draft.Skill.InlineInstructions);
        Assert.Empty(session.Draft.UploadedFileName);
    }

    [Fact]
    public async Task Second_upload_cancels_first_without_clearing_second_busy_state() {
        using var session = Create(new(AuthoringScenario.NewSkill));
        var first = new File("first.md", Encoding.UTF8.GetBytes("First"), held: true);
        var second = new File("second.md", Encoding.UTF8.GetBytes("Second"), held: true);
        var firstUpload = session.UploadAsync(first);
        var secondUpload = session.UploadAsync(second);
        await firstUpload;
        Assert.True(first.Token.IsCancellationRequested);
        Assert.True(session.Busy);
        second.Release();
        await secondUpload;
        Assert.Equal("Second", session.Draft.Skill.InlineInstructions);
        Assert.Equal("second.md", session.Draft.UploadedFileName);
        Assert.False(session.Busy);
    }

    [Fact]
    public async Task Disposal_cancels_the_live_stream_until_it_unwinds() {
        var session = Create(new(AuthoringScenario.NewSkill));
        var file = new File("closed.md", Encoding.UTF8.GetBytes("Stale"), held: true);
        var upload = session.UploadAsync(file);
        session.Dispose();
        Assert.True(file.Token.IsCancellationRequested);
        using var registration = file.Token.Register(() => { });
        await upload;
        Assert.Empty(session.Draft.UploadedFileName);
    }

    private static CapabilityAuthoringSession Create(CapabilityAuthoringScenario fixture) {
        var session = new CapabilityAuthoringSession(fixture.Operations, null, CapabilityKind.Skill, default, new NotificationService(), NullLogger.Instance);
        session.Draft.SkillMode = CapabilitySkillInputMode.Upload;
        return session;
    }

    private sealed class File(string name, byte[] bytes, bool held = false) : IBrowserFile {
        private readonly TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public string Name => name;
        public DateTimeOffset LastModified => DateTimeOffset.UnixEpoch;
        public long Size => bytes.Length;
        public string ContentType => "text/markdown";
        public long RequestedLimit { get; private set; }
        public CancellationToken Token { get; private set; }
        public void Release() => release.TrySetResult();
        public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default) {
            RequestedLimit = maxAllowedSize;
            Token = cancellationToken;
            if (Size > maxAllowedSize) {
                throw new IOException("File exceeds the requested limit.");
            }
            return held ? new HeldStream(bytes, release.Task) : new MemoryStream(bytes);
        }
    }

    private sealed class HeldStream(byte[] bytes, Task released) : MemoryStream(bytes) {
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) {
            await released.WaitAsync(cancellationToken);
            return await base.ReadAsync(buffer, cancellationToken);
        }
    }
}
