using Bunit;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class AgentEditorAdversarialTests {
    private const string Poison = "EDITOR_PRIVATE_SENTINEL api_key=test-only-private-editor-key C:\\private\\editor.txt /srv/private/editor at Secret.Load()\nBearer private-editor-token\u202e";

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task Editor_save_and_committed_followup_errors_are_public_and_do_not_replay_the_write(int lane) {
        SaveProbe? probe = null;
        await using var harness = await ComponentTestHarness.CreateAsync(services =>
            services.AddScoped<IAgentEditorCommands>(provider => probe = new(
                ActivatorUtilities.CreateInstance<AgentEditorCommands>(provider), lane)));
        var cut = harness.Context.Render<AgentDetailsDialog>(parameters => parameters
            .Add(component => component.InitialProviders, Array.Empty<ProviderProfile>())
            .Add(component => component.TargetChanged, EventCallback.Factory.Create<AgentEditorTarget>(this,
                target => lane == 2 && !target.IsNew ? Task.FromException(new IOException(Poison)) : Task.CompletedTask))
            .Add(component => component.Saved, EventCallback.Factory.Create<AgentDetailsDialogResult>(this,
                _ => lane == 3 ? Task.FromException(new IOException(Poison)) : Task.CompletedTask)));
        cut.WaitForElement("[data-testid='agents-catalog-name']").Change("Retained adversarial draft");
        var editor = cut.FindComponent<EditForm>().Instance.EditContext!;
        await cut.Find("form").SubmitAsync();
        Assert.Equal(1, probe!.Writes);
        Assert.Equal("Retained adversarial draft", ((AgentEditorModel)cut.FindComponent<EditForm>().Instance.EditContext!.Model).Name);
        Assert.DoesNotContain("EDITOR_PRIVATE_SENTINEL", cut.Markup, StringComparison.Ordinal);
        var messages = harness.Context.Services.GetRequiredService<NotificationService>().Messages;
        Assert.Contains(messages, message => message.Severity == NotificationSeverity.Error);
        Assert.DoesNotContain(messages, message => (message.Detail ?? "").Contains("EDITOR_PRIVATE_SENTINEL", StringComparison.Ordinal));
        if (lane == 0) {
            Assert.True(cut.Instance.CurrentTarget.IsNew);
            Assert.Same(editor, cut.FindComponent<EditForm>().Instance.EditContext);
        } else {
            Assert.NotNull(cut.Instance.CurrentTarget.AgentId);
            if (lane == 1) {
                Assert.True(cut.Find("[data-testid='agents-catalog-save']").HasAttribute("disabled"));
                await cut.Find("[data-testid='agents-editor-retry-refresh']").ClickAsync();
                Assert.Equal(1, probe.Writes);
                Assert.False(cut.Find("[data-testid='agents-catalog-save']").HasAttribute("disabled"));
            }
        }
    }

    private sealed class SaveProbe(IAgentEditorCommands inner, int lane) : IAgentEditorCommands {
        public int Writes { get; private set; }
        private int reads;
        public Task<AgentEditorSaveOutcome> SaveAsync(AgentEditorModel request, CancellationToken cancellationToken = default) {
            Writes++;
            return lane == 0 ? Task.FromException<AgentEditorSaveOutcome>(new IOException(Poison)) : inner.SaveAsync(request, cancellationToken);
        }
        public Task<AgentEditorCatalogRefresh> ReconcileAsync(Guid agentId, IReadOnlyList<ProviderProfile> providers, CancellationToken cancellationToken = default) {
            reads++;
            return lane == 1 && reads == 1 ? Task.FromException<AgentEditorCatalogRefresh>(new IOException(Poison)) : inner.ReconcileAsync(agentId, providers, cancellationToken);
        }
        public Task DeleteAsync(Guid agentId, CancellationToken cancellationToken = default) => inner.DeleteAsync(agentId, cancellationToken);
        public Task VerifyCapabilityAsync(Guid agentId, Guid capabilityId, CancellationToken cancellationToken = default) => inner.VerifyCapabilityAsync(agentId, capabilityId, cancellationToken);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task Editor_failure_lanes_do_not_publish_internal_messages(int lane) {
        var reads = new AgentEditorReadFixture {
            Draft = new() { Name = "Retained product content", ProjectStructureAccess = new() { CanRead = true } },
            ProviderError = lane == 1 ? Poison : null,
            SecretError = lane == 2 ? Poison : null
        };
        if (lane == 0) {
            reads.Load = (_, _) => Task.FromException<AgentEditorLoadResult>(new IOException(Poison));
        }
        if (lane == 3) {
            reads.ReadProjects = _ => Task.FromException<IReadOnlyList<AgentEditorProject>>(new IOException(Poison));
        }
        await using var harness = await AgentEditorSessionTests.CreateHarnessAsync(reads);
        var cut = harness.Context.Render<AgentDetailsDialog>();
        if (lane == 0) {
            cut.WaitForElement("[data-testid='agents-details-load-failed']");
            Assert.Empty(cut.FindAll("form"));
        } else {
            cut.WaitForElement("[data-testid='agents-catalog-name']");
            Assert.Equal("Retained product content", ((AgentEditorModel)cut.FindComponent<EditForm>().Instance.EditContext!.Model).Name);
            if (lane == 3) {
                cut.Render(parameters => parameters.Add(component => component.Section, AgentEditorSection.ProjectStructureAccess));
                await cut.Find("[data-testid='agents-catalog-project-structure-load']").ClickAsync();
                Assert.Contains(cut.FindAll("button"), button => button.TextContent.Trim() == "Retry project list");
            }
        }
        Assert.DoesNotContain("EDITOR_PRIVATE_SENTINEL", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("private-editor", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain(harness.Context.Services.GetRequiredService<NotificationService>().Messages,
            message => (message.Detail ?? "").Contains("EDITOR_PRIVATE_SENTINEL", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(false, 0)]
    [InlineData(false, 1)]
    [InlineData(false, 2)]
    [InlineData(true, 0)]
    [InlineData(true, 1)]
    [InlineData(true, 2)]
    public async Task Editor_read_token_survives_target_disposal_until_operation_completion(bool dispose, int outcome) {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var pending = new TaskCompletionSource<AgentEditorLoadResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var reads = new AgentEditorReadFixture();
        CancellationToken token = default;
        reads.Load = (target, request) => {
            if (target.AgentId == first) {
                token = request;
                return pending.Task;
            }
            return Task.FromResult(reads.Result(new() { Id = second, Name = "Current target" }));
        };
        await using var harness = await AgentEditorSessionTests.CreateHarnessAsync(reads);
        var cut = harness.Context.Render<AgentDetailsDialog>(parameters => parameters.Add(component => component.AgentId, first));
        if (dispose) {
            await cut.InvokeAsync(cut.Instance.Dispose);
        } else {
            cut.Render(parameters => parameters.Add(component => component.AgentId, second));
            cut.WaitForElement("[data-testid='agents-catalog-name']");
        }
        Assert.True(token.IsCancellationRequested);
        var callbacks = 0;
        using var delayed = token.Register(() => callbacks++);
        Assert.Equal(1, callbacks);
        Assert.True(token.WaitHandle.WaitOne(0));
        await cut.InvokeAsync(() => {
            switch (outcome) {
                case 0:
                    pending.SetResult(reads.Result(new() { Id = first, Name = "Stale target" }));
                    break;
                case 1:
                    pending.SetException(new IOException(Poison));
                    break;
                default:
                    pending.SetException(new OperationCanceledException(token));
                    break;
            }
        });
        cut.WaitForAssertion(() => Assert.Throws<ObjectDisposedException>(() => token.WaitHandle));
        Assert.DoesNotContain("Stale target", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("EDITOR_PRIVATE_SENTINEL", cut.Markup, StringComparison.Ordinal);
        if (!dispose) {
            Assert.Equal(second, cut.Instance.CurrentTarget.AgentId);
            Assert.Equal("Current target", ((AgentEditorModel)cut.FindComponent<EditForm>().Instance.EditContext!.Model).Name);
        }
    }
}
