using System.Reflection;
using Bunit;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Editing;
using CanDoItAll.Infrastructure.Editing;
using CanDoItAll.Modules.AgentFramework.ProviderManagement;
using ProviderProfile = CanDoItAll.AgentFramework.Models.ProviderProfile;
using ProviderProfileEditorModel = CanDoItAll.AgentFramework.Models.ProviderProfileEditorModel;
using ProviderKind = CanDoItAll.AgentFramework.Models.ProviderKind;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using CanDoItAll.Tests.Support;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.AgentFramework;

[Trait("Category", "HostPlatform")]
public sealed class ProviderUiAdmissionTests {
    [Fact]
    public async Task A_source_child_cannot_capture_a_new_actor_and_expose_its_parents_credential_metadata() {
        var access = new SwitchActorWhenSourceOpens();
        await using var harness = await ComponentTestHarness.CreateAsync(services => {
            services.AddSingleton<IEditingUiAccess>(access);
            services.AddScoped(provider => {
                var service = DispatchProxy.Create<ISharedProviderManagementService, SharedProviderSourceSeamTests.NativeSourceProxy>();
                ((SharedProviderSourceSeamTests.NativeSourceProxy)(object)service).Owner = ActivatorUtilities.CreateInstance<SharedProviderManagementService>(provider);
                return service;
            });
        });
        var saved = await harness.Context.Services.GetRequiredService<CanDoItAll.Modules.Security.SecretService>().SaveAsync(new() {
            Name = "Protected parent source credential", Kind = CanDoItAll.Modules.Security.SecretKind.Token,
            SecretValue = "owned-unused-source-actor-test-value"
        });
        Assert.True(saved.IsSuccess);
        var proxy = (SharedProviderSourceSeamTests.NativeSourceProxy)(object)harness.Context.Services.GetRequiredService<ISharedProviderManagementService>();
        var cut = harness.Context.Render<AgentProviderProfilesPanel>();
        cut.WaitForElement("[data-testid='providers-name-input']");
        await cut.Find("[data-testid='providers-connections']").ClickAsync();
        Assert.Equal(1, access.SourceOpens);
        Assert.Equal(0, proxy.ListReads);
        Assert.DoesNotContain("Protected parent source credential", cut.Markup, StringComparison.Ordinal);
    }

    private sealed class SwitchActorWhenSourceOpens : IEditingUiAccess {
        private readonly EditingUiTestAccess inner = new();
        public int SourceOpens { get; private set; }
        public Task<EditingUiGrant> OpenAsync(EditingTopic topic, CancellationToken cancellationToken) {
            if (topic.Owner == EditingOwner.SharedProviderSource) {
                SourceOpens++;
                inner.Principal = "different-child-source-actor";
            }
            return inner.OpenAsync(topic, cancellationToken);
        }
        public ValueTask ValidateAsync(EditingUiGrant grant, bool write, CancellationToken cancellationToken) =>
            inner.ValidateAsync(grant, write, cancellationToken);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Recovery_current_review_preserves_raw_input_and_does_not_replay_or_claim_a_previous_commit(bool unconfirmed) {
        var reads = new Reads();
        var commands = DispatchProxy.Create<IProviderEditorCommands, CommandsProxy>();
        var proxy = (CommandsProxy)(object)commands;
        proxy.Save = _ => Task.FromResult(new ProviderWriteResult(unconfirmed ? ProviderWriteDisposition.Unconfirmed : ProviderWriteDisposition.Committed, reads.Editor.Id));
        await using var harness = await ComponentTestHarness.CreateAsync(services => {
            services.AddSingleton<IProviderProfilesReads>(reads);
            services.AddSingleton(commands);
        });
        var cut = harness.Context.Render<AgentProviderProfilesPanel>();
        cut.WaitForElement("[data-testid='providers-name-input']").Input("Retained recovery draft");
        await cut.FindComponent<ProviderProfileEditorForm>().Find("form").SubmitAsync();
        cut.WaitForElement("[data-testid='providers-review-current']");
        cut.Find("[data-testid='providers-tags-input']").Input("Unfinished recovery tag");
        await cut.FindAll("[role='tab']").Single(tab => tab.TextContent.Contains("Runtime", StringComparison.Ordinal)).ClickAsync();
        cut.Find("[data-testid='providers-config-json']").Input("{unfinished recovery");
        var context = cut.FindComponent<ProviderProfileEditorForm>().Instance.Context;
        reads.Editor.Name = "Current saved peer";
        reads.Editor.ExpectedConcurrencyToken = Guid.NewGuid();

        await cut.Find("[data-testid='providers-review-current']").ClickAsync();

        var review = cut.WaitForElement("[data-testid='providers-recovery-review']");
        Assert.Contains("Current saved peer", review.TextContent, StringComparison.Ordinal);
        Assert.Contains("does not perform a three-way merge", review.TextContent, StringComparison.Ordinal);
        Assert.True(cut.Find("[data-testid='providers-recovery-apply']").HasAttribute("disabled"));
        await review.QuerySelector("input[type='checkbox']")!.ChangeAsync(new ChangeEventArgs { Value = true });
        await cut.Find("[data-testid='providers-recovery-apply']").ClickAsync();

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[data-testid='providers-review-current']")));
        Assert.Same(context, cut.FindComponent<ProviderProfileEditorForm>().Instance.Context);
        Assert.Equal("Retained recovery draft", ((ProviderProfileEditorModel)context.Model).Name);
        Assert.Equal("{unfinished recovery", cut.Find("[data-testid='providers-config-json']").GetAttribute("value"));
        Assert.Equal("Unfinished recovery tag", CanDoItAll.AgentFramework.Providers.UI.ProviderEditorDraft.For(context).PendingTag);
        Assert.Equal(reads.Editor.ExpectedConcurrencyToken, ((ProviderProfileEditorModel)context.Model).ExpectedConcurrencyToken);
        Assert.Equal(1, proxy.Calls);
        Assert.Equal(unconfirmed ? 1 : 0, harness.Context.Services.GetRequiredService<ProviderEditorRecovery>().ReviewedUnconfirmed.Count);
        await cut.FindComponent<ProviderProfileEditorForm>().Find("form").SubmitAsync();
        Assert.Equal(1, proxy.Calls);
    }

    [Theory]
    [InlineData(EditingChangeKind.MetadataChanged)]
    [InlineData(EditingChangeKind.LifecycleChanged)]
    public async Task Metadata_or_sharing_lifecycle_hint_does_not_invalidate_the_editable_original_or_query_a_body(EditingChangeKind kind) {
        var reads = new Reads();
        await using var harness = await ComponentTestHarness.CreateAsync(services => services.AddSingleton<IProviderProfilesReads>(reads));
        var cut = harness.Context.Render<AgentProviderProfilesPanel>();
        cut.WaitForElement("[data-testid='providers-name-input']").Input("My draft survives health hints");
        var context = cut.FindComponent<ProviderProfileEditorForm>().Instance.Context;
        var calls = reads.Calls;
        var topic = new EditingTopic(harness.Context.Services.GetRequiredService<ProviderEditingChanges>().Scope,
            EditingOwner.ProviderProfile, reads.Editor.Id);
        harness.Context.Services.GetRequiredService<EditingChangeFeed>().Publish(topic, kind);
        cut.WaitForElement("[data-testid='providers-catalog-changed']");
        Assert.Equal(calls, reads.Calls);
        Assert.Empty(cut.FindAll("[data-testid='providers-changed']"));
        Assert.False(cut.Find("[data-testid='providers-save']").HasAttribute("disabled"));
        Assert.Equal("My draft survives health hints", cut.Find("[data-testid='providers-name-input']").GetAttribute("value"));
        Assert.Same(context, cut.FindComponent<ProviderProfileEditorForm>().Instance.Context);
        Assert.Equal(reads.Editor.ExpectedConcurrencyToken, ((ProviderProfileEditorModel)context.Model).ExpectedConcurrencyToken);
    }

    [Fact]
    public async Task Unconfirmed_health_has_an_explicit_metadata_acknowledgement_that_keeps_the_draft_and_never_replays() {
        var reads = new Reads();
        var commands = DispatchProxy.Create<IProviderEditorCommands, CommandsProxy>();
        ((CommandsProxy)(object)commands).Health = () => Task.FromResult(new ProviderHealthCheckOutcome(null,
            new(ProviderWriteDisposition.Unconfirmed, reads.Editor.Id)));
        await using var harness = await ComponentTestHarness.CreateAsync(services => {
            services.AddSingleton<IProviderProfilesReads>(reads);
            services.AddSingleton(commands);
        });
        var cut = harness.Context.Render<AgentProviderProfilesPanel>();
        cut.WaitForElement("[data-testid='providers-name-input']").Input("Retained diagnostic draft");
        cut.Find("[data-testid='providers-tags-input']").Input("Pending diagnostic tag");
        var context = cut.FindComponent<ProviderProfileEditorForm>().Instance.Context;
        await cut.FindAll("button").Single(button => button.TextContent.Trim() == "Health").ClickAsync();
        cut.WaitForElement("[data-testid='providers-write-unconfirmed']");
        await cut.Find("[data-testid='providers-acknowledge-diagnostic']").ClickAsync();
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[data-testid='providers-write-unconfirmed']")));
        Assert.Equal(1, ((CommandsProxy)(object)commands).Calls);
        Assert.Same(context, cut.FindComponent<ProviderProfileEditorForm>().Instance.Context);
        Assert.Equal("Retained diagnostic draft", cut.Find("[data-testid='providers-name-input']").GetAttribute("value"));
        Assert.Equal("Pending diagnostic tag", cut.Find("[data-testid='providers-tags-input']").GetAttribute("value"));
        Assert.Equal(reads.Editor.ExpectedConcurrencyToken, ((ProviderProfileEditorModel)context.Model).ExpectedConcurrencyToken);
        Assert.False(cut.Find("[data-testid='providers-save']").HasAttribute("disabled"));
        Assert.Single(harness.Context.Services.GetRequiredService<ProviderEditorRecovery>().AcknowledgedDiagnostics);
    }

    [Fact]
    public async Task Provider_peer_hints_do_not_query_and_a_newer_hint_invalidates_review_without_losing_raw_input() {
        var reads = new Reads();
        await using var harness = await ComponentTestHarness.CreateAsync(services => services.AddSingleton<IProviderProfilesReads>(reads));
        var cut = harness.Context.Render<AgentProviderProfilesPanel>();
        cut.WaitForElement("[data-testid='providers-name-input']");
        var context = cut.FindComponent<ProviderProfileEditorForm>().Instance.Context;
        cut.Find("[data-testid='providers-tags-input']").Input("unfinished provider tag");
        await cut.FindAll("[role='tab']").Single(tab => tab.TextContent.Contains("Runtime", StringComparison.Ordinal)).ClickAsync();
        cut.Find("[data-testid='providers-config-json']").Input("{unfinished");
        var topic = new EditingTopic(harness.Context.Services.GetRequiredService<ProviderEditingChanges>().Scope,
            EditingOwner.ProviderProfile, reads.Editor.Id);
        var publisher = harness.Context.Services.GetRequiredService<EditingChangeFeed>();
        var calls = reads.Calls;
        publisher.Publish(topic, EditingChangeKind.EditableContentChanged);
        cut.WaitForElement("[data-testid='providers-changed']");
        Assert.Equal(calls, reads.Calls);
        Assert.True(cut.Find("[data-testid='providers-save']").HasAttribute("disabled"));
        reads.Editor.Name = "Saved peer name";
        reads.Editor.ExpectedConcurrencyToken = Guid.NewGuid();
        await cut.Find("[data-testid='providers-compare']").ClickAsync();
        cut.WaitForElement("[data-testid='providers-comparison']");
        Assert.Equal(calls + 1, reads.Calls);
        Assert.Equal("{unfinished", cut.Find("[data-testid='providers-config-json']").GetAttribute("value"));
        publisher.Publish(topic, EditingChangeKind.EditableContentChanged);
        cut.WaitForElement("[data-testid='providers-comparison-stale']");
        Assert.True(cut.Find("[data-testid='providers-comparison-apply']").HasAttribute("disabled"));
        Assert.Equal(calls + 1, reads.Calls);
        Assert.Same(context, cut.FindComponent<ProviderProfileEditorForm>().Instance.Context);
        Assert.Equal("unfinished provider tag", CanDoItAll.AgentFramework.Providers.UI.ProviderEditorDraft.For(context).PendingTag);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Provider_workspace_reads_no_protected_data_during_prerender_or_without_access(bool interactive) {
        var reads = new Reads();
        await using var harness = await ComponentTestHarness.CreateAsync(services => {
            services.AddSingleton<IEditingUiAccess>(new EditingUiTestAccess { Allowed = false });
            services.AddSingleton<IProviderProfilesReads>(reads);
        });
        harness.Context.SetRendererInfo(new RendererInfo(interactive ? "Server" : "Static", interactive));
        var cut = harness.Context.Render<AgentProviderProfilesPanel>();
        Assert.Empty(cut.FindAll("form"));
        Assert.Equal(0, reads.Calls);
    }

    [Fact]
    public async Task A_held_provider_read_cannot_reveal_data_to_a_different_actor() {
        var access = new EditingUiTestAccess();
        var pending = new TaskCompletionSource<ProviderProfileEditorModel>(TaskCreationOptions.RunContinuationsAsynchronously);
        var reads = new Reads { Pending = pending.Task };
        await using var harness = await ComponentTestHarness.CreateAsync(services => {
            services.AddSingleton<IEditingUiAccess>(access);
            services.AddSingleton<IProviderProfilesReads>(reads);
        });
        var cut = harness.Context.Render<AgentProviderProfilesPanel>();
        cut.WaitForAssertion(() => Assert.Equal(2, reads.Calls));
        access.Principal = "different-provider-actor";
        await cut.InvokeAsync(() => pending.SetResult(reads.Editor));
        cut.WaitForElement("[data-testid='providers-load-failed']");
        Assert.Empty(cut.FindAll("form"));
        Assert.DoesNotContain(reads.Editor.Name, cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Actor_loss_before_provider_save_dispatches_no_write_and_keeps_the_draft() {
        var access = new EditingUiTestAccess();
        var reads = new Reads();
        var commands = DispatchProxy.Create<IProviderEditorCommands, CommandsProxy>();
        await using var harness = await ComponentTestHarness.CreateAsync(services => {
            services.AddSingleton<IEditingUiAccess>(access);
            services.AddSingleton<IProviderProfilesReads>(reads);
            services.AddSingleton(commands);
        });
        var cut = harness.Context.Render<AgentProviderProfilesPanel>();
        cut.WaitForElement("[data-testid='providers-name-input']").Input("Unsubmitted provider draft");
        var context = cut.FindComponent<ProviderProfileEditorForm>().Instance.Context;
        access.Principal = "different-provider-actor";
        await cut.FindComponent<ProviderProfileEditorForm>().Find("form").SubmitAsync();
        cut.WaitForElement("[data-testid='providers-load-failed']");
        Assert.Equal(0, ((CommandsProxy)(object)commands).Calls);
        Assert.Equal("Unsubmitted provider draft", ((ProviderProfileEditorModel)context.Model).Name);
        Assert.Empty(cut.FindAll("form"));
    }

    public class CommandsProxy : DispatchProxy {
        public int Calls { get; private set; }
        public Func<Task<ProviderHealthCheckOutcome>>? Health { get; set; }
        public Func<ProviderEditorSubmission, Task<ProviderWriteResult>>? Save { get; set; }
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) {
            Calls++;
            if (targetMethod?.Name == nameof(IProviderEditorCommands.CheckHealthAsync) && Health is not null) {
                return Health();
            }
            if (targetMethod?.Name == nameof(IProviderEditorCommands.SaveAsync) && Save is not null) {
                return Save((ProviderEditorSubmission)args![0]!);
            }
            throw new InvalidOperationException("A retired provider opening must not dispatch a command.");
        }
    }

    private sealed class Reads : IProviderProfilesReads {
        public int Calls { get; private set; }
        public ProviderProfileEditorModel Editor { get; } = new() {
            Id = Guid.NewGuid(), Name = "Protected provider name", DefaultModel = "model",
            SuggestedModels = ["model"], ExpectedConcurrencyToken = Guid.NewGuid()
        };
        public Task<ProviderProfileEditorModel>? Pending { get; init; }
        public Task<ProviderProfilesCatalog> LoadCatalogAsync(CancellationToken cancellationToken = default) {
            Calls++;
            return Task.FromResult(new ProviderProfilesCatalog([new ProviderProfile(Editor.Id!.Value, Editor.Name,
                ProviderKind.OpenAi, "", "", "model", ProviderTransportKind.Responses, true, true, true,
                false, true, "{}", "", "", null, ["model"])], new([])));
        }
        public Task<ProviderProfileEditorModel> LoadEditorAsync(Guid id, CancellationToken cancellationToken = default) {
            Calls++;
            return Pending ?? Task.FromResult(ProviderEditorSubmission.Copy(Editor));
        }
    }
}
