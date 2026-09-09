using Bunit;
using CanDoItAll.AgentFramework.Components;
using CanDoItAll.Components.BaseLib;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class AvatarPickerAdversarialTests {
    [Fact]
    public async Task Default_source_failure_is_public_and_the_picker_can_retry() {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
        var gateway = new Gateway { SourceFailure = true };
        context.Services.AddSingleton<IAvatarGenerationGateway>(gateway);
        var cut = context.Render<AvatarPicker>();
        await cut.Find("[data-testid='avatar-picker-open']").ClickAsync();
        Assert.DoesNotContain("AVATAR_PRIVATE_SENTINEL", cut.Markup, StringComparison.Ordinal);
        var messages = context.Services.GetRequiredService<NotificationService>().Messages;
        Assert.Contains(messages, message => message.Summary == "Avatar picker unavailable");
        Assert.DoesNotContain(messages, message => (message.Detail ?? "").Contains("AVATAR_PRIVATE_SENTINEL", StringComparison.Ordinal));
        gateway.SourceFailure = false;
        await cut.Find("[data-testid='avatar-picker-open']").ClickAsync();
        Assert.NotNull(cut.Find("[data-testid='avatar-picker-ai-generate']"));
    }
    private const string Poison = "AVATAR_PRIVATE_SENTINEL bearer=private-avatar-token /srv/private/avatar at Internal.Generate()";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Generation_failure_or_disposal_cannot_publish_private_detail(bool dispose) {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
        var gateway = new Gateway();
        context.Services.AddSingleton<IAvatarGenerationGateway>(gateway);
        var values = new List<string>();
        var cut = context.Render<AvatarPicker>(parameters => parameters
            .Add(component => component.EntityName, "Safe avatar owner")
            .Add(component => component.ValueChanged, values.Add));
        await cut.Find("[data-testid='avatar-picker-open']").ClickAsync();
        var generate = cut.Find("[data-testid='avatar-picker-ai-generate']").ClickAsync();
        cut.WaitForAssertion(() => Assert.True(gateway.Started));
        if (dispose) {
            await context.DisposeComponentsAsync();
            Assert.True(gateway.Token.IsCancellationRequested);
            var callbacks = 0;
            using var registration = gateway.Token.Register(() => callbacks++);
            Assert.Equal(1, callbacks);
            Assert.True(gateway.Token.WaitHandle.WaitOne(0));
        }
        gateway.Pending.SetException(new IOException(Poison));
        await generate;
        Assert.Empty(values);
        var notifications = context.Services.GetRequiredService<NotificationService>().Messages;
        Assert.DoesNotContain(notifications, message => (message.Detail ?? "").Contains("AVATAR_PRIVATE_SENTINEL", StringComparison.Ordinal));
        if (dispose) {
            Assert.Empty(notifications);
            Assert.Throws<ObjectDisposedException>(() => gateway.Token.WaitHandle);
        } else {
            Assert.DoesNotContain("AVATAR_PRIVATE_SENTINEL", cut.Markup, StringComparison.Ordinal);
            Assert.Contains(notifications, message => message.Summary == "AI avatar generation failed");
            Assert.False(cut.Find("[data-testid='avatar-picker-ai-generate']").HasAttribute("disabled"));
        }
    }

    [Fact]
    public async Task Upload_callback_failure_does_not_expose_backend_detail() {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
        context.Services.AddSingleton<IAvatarGenerationGateway>(new Gateway());
        var cut = context.Render<AvatarPicker>(parameters => parameters
            .Add(component => component.ValueChanged, _ => Task.FromException(new IOException(Poison))));
        await cut.Find("[data-testid='avatar-picker-open']").ClickAsync();
        cut.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromBinary(
            Convert.FromBase64String(AgentDetailsDialogAvatarGenerationTests.ValidSquareJpegBase64), "avatar.jpg", contentType: "image/jpeg"));
        cut.WaitForAssertion(() => Assert.Contains(context.Services.GetRequiredService<NotificationService>().Messages,
            message => message.Summary == "Avatar upload failed"));
        Assert.DoesNotContain(context.Services.GetRequiredService<NotificationService>().Messages,
            message => (message.Detail ?? "").Contains("AVATAR_PRIVATE_SENTINEL", StringComparison.Ordinal));
    }

    private sealed class Gateway : IAvatarGenerationGateway {
        public bool SourceFailure { get; set; }
        public bool Started { get; private set; }
        public CancellationToken Token { get; private set; }
        public TaskCompletionSource<AvatarGenerationResult> Pending { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<AvatarGenerationSource?> GetDefaultSourceAsync(CancellationToken cancellationToken = default)
            => SourceFailure ? Task.FromException<AvatarGenerationSource?>(new IOException(Poison))
                : Task.FromResult<AvatarGenerationSource?>(new(Guid.NewGuid(), "Local fixture", "fixture"));
        public Task<AvatarGenerationResult> GenerateAsync(AvatarGenerationRequest request, CancellationToken cancellationToken = default) {
            Started = true;
            Token = cancellationToken;
            return Pending.Task;
        }
    }
}
