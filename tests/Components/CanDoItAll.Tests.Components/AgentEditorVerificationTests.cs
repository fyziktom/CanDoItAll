using System.Text.Json;
using Bunit;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.FileSystem;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using CanDoItAll.Components.BaseLib;

namespace CanDoItAll.Tests.Components.AgentFramework;

[Trait("Category", "HostPlatform")]
public sealed class AgentEditorVerificationTests {
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Native_verify_preserves_unsaved_before_click_and_context_without_saving(bool dirty, bool template) {
        var diagnostic = new CountingInlineDiagnostic();
        await using var harness = await ComponentTestHarness.CreateAsync(services =>
            services.AddSingleton<ICapabilityProofService>(diagnostic));
        var workspace = harness.Context.Services.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var capabilityId = await workspace.SaveCapabilityAsync(new() {
            Kind = CapabilityKind.Skill, Key = "editor-verification-fixture", Name = "Editor verification fixture",
            EndpointOrPath = "inline://editor-verification-fixture",
            ConfigurationJson = """{"inlineSkill":{"instructions":"Review this safe local fixture."}}"""
        });
        var id = await workspace.SaveAgentAsync(new() {
            Name = "Persisted verification agent", Instructions = "Persisted instructions", SelectedCapabilityIds = [capabilityId], IsTemplate = template
        });
        var before = await workspace.GetAgentEditorAsync(id);
        var cut = harness.Context.Render<AgentDetailsDialog>(parameters => parameters
            .Add(component => component.AgentId, id)
            .Add(component => component.InitialProviders, Array.Empty<ProviderProfile>()));
        cut.WaitForElement("[data-testid='agents-catalog-name']");
        var context = cut.FindComponent<EditForm>().Instance.EditContext!;
        var draft = (AgentEditorModel)context.Model;
        var messages = new ValidationMessageStore(context);
        if (dirty) {
            await cut.InvokeAsync(() => cut.Find("[data-testid='agents-catalog-name']").Input("Unsaved Žluťoučký 東京"));
            await cut.InvokeAsync(() => cut.Find("[data-testid='agents-catalog-instructions']").Input("Unsaved instructions before Verify"));
            await Tab(AgentEditorSection.ProcessAccess);
            await cut.InvokeAsync(() => cut.Find("[data-testid='agents-catalog-process-read']").Change(true));
            messages.Add(new FieldIdentifier(draft, nameof(draft.Summary)), "Retain this raw validation marker");
            await cut.InvokeAsync(context.NotifyValidationStateChanged);
        }
        var expected = JsonSerializer.Serialize(draft);
        await Tab(AgentEditorSection.Capabilities);
        await cut.InvokeAsync(() => cut.FindAll("[data-testid='agents-details-capability-verify']")
            .Single(button => !button.HasAttribute("disabled")).ClickAsync());
        var persisted = await workspace.GetAgentEditorAsync(id);
        var published = (await workspace.ListAgentsAsync()).Single(agent => agent.Id == id);
        Assert.Equal(1, diagnostic.Calls);
        Assert.Equal(CapabilityProofStatus.Verified, Assert.Single(published.Capabilities).ProofStatus);
        Assert.NotEqual(before.ExpectedUpdatedAtUtc, persisted.ExpectedUpdatedAtUtc);
        Assert.Equal(before.Name, persisted.Name);
        Assert.Equal(before.Instructions, persisted.Instructions);
        Assert.Equal(before.ProcessAccess.CanRead, persisted.ProcessAccess.CanRead);
        Assert.Equal(draft.Name, ((AgentEditorModel)cut.FindComponent<EditForm>().Instance.EditContext!.Model).Name);
        Assert.Equal(draft.Instructions, ((AgentEditorModel)cut.FindComponent<EditForm>().Instance.EditContext!.Model).Instructions);
        Assert.Same(context, cut.FindComponent<EditForm>().Instance.EditContext);
        Assert.Same(draft, cut.FindComponent<EditForm>().Instance.EditContext!.Model);
        Assert.Equal(AgentEditorSection.Capabilities, cut.Instance.Section);
        Assert.Equal(persisted.ExpectedUpdatedAtUtc, draft.ExpectedUpdatedAtUtc);
        var comparison = AgentEditorDraftPolicy.Copy(draft);
        comparison.ExpectedUpdatedAtUtc = before.ExpectedUpdatedAtUtc;
        Assert.Equal(expected, JsonSerializer.Serialize(comparison));
        Assert.Equal(dirty ? 1 : 0, context.GetValidationMessages().Count());
        messages.Clear();
        await cut.InvokeAsync(context.NotifyValidationStateChanged);
        await cut.InvokeAsync(() => cut.Find("form").SubmitAsync());
        Assert.Equal(draft.Name, (await workspace.GetAgentEditorAsync(id)).Name);
        Assert.Equal(draft.ProcessAccess.CanRead, (await workspace.GetAgentEditorAsync(id)).ProcessAccess.CanRead);
        Assert.Equal(1, diagnostic.Calls);

        async Task Tab(AgentEditorSection section) {
            await cut.InvokeAsync(() => cut.FindAll("[role='tab']")[AgentEditorSections.IndexOf(section)].ClickAsync());
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Failed_readback_retries_only_reads_and_retains_edits_during_each_await(bool competingEdit) {
        var diagnostic = new CountingInlineDiagnostic { Release = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        VerificationCommands? commands = null;
        await using var harness = await ComponentTestHarness.CreateAsync(services => {
            services.AddSingleton<ICapabilityProofService>(diagnostic);
            services.AddScoped<IAgentEditorCommands>(provider => commands = new(ActivatorUtilities.CreateInstance<AgentEditorCommands>(provider)));
        });
        var workspace = harness.Context.Services.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var capabilityId = await workspace.SaveCapabilityAsync(new() {
            Kind = CapabilityKind.Skill, Key = "held-editor-verification", Name = "Held editor verification",
            EndpointOrPath = "inline://held-editor-verification",
            ConfigurationJson = """{"inlineSkill":{"instructions":"Review this safe local fixture."}}"""
        });
        var id = await workspace.SaveAgentAsync(new() { Name = "Original", SelectedCapabilityIds = [capabilityId] });
        var cut = harness.Context.Render<AgentDetailsDialog>(parameters => parameters.Add(component => component.AgentId, id)
            .Add(component => component.InitialProviders, Array.Empty<ProviderProfile>())
            .Add(component => component.Section, AgentEditorSection.Capabilities));
        cut.WaitForElement("[data-testid='agents-details-capability-verify']");
        var context = cut.FindComponent<EditForm>().Instance.EditContext!;
        var draft = (AgentEditorModel)context.Model;
        var originalVersion = draft.ExpectedUpdatedAtUtc;
        draft.Name = "Unsaved before verification";
        commands!.FailRead = true;
        var verify = cut.InvokeAsync(() => cut.FindAll("[data-testid='agents-details-capability-verify']")
            .Single(button => !button.HasAttribute("disabled")).ClickAsync());
        await diagnostic.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await cut.InvokeAsync(() => draft.Instructions = "Typed during diagnostic");
        diagnostic.Release.SetResult();
        await verify;
        cut.WaitForElement("[data-testid='agents-editor-retry-verification']");
        Assert.Equal(1, diagnostic.Calls);
        Assert.Equal(0, commands.Writes);
        Assert.True(cut.Find("[data-testid='agents-catalog-save']").HasAttribute("disabled"));
        var proofVersion = (await workspace.GetAgentEditorAsync(id)).ExpectedUpdatedAtUtc;
        Assert.NotEqual(originalVersion, proofVersion);
        if (competingEdit) {
            var concurrent = await workspace.GetAgentEditorAsync(id);
            concurrent.Name = "Another operator's change";
            await workspace.SaveAgentAsync(concurrent);
        }
        commands.FailRead = false;
        commands.ReadRelease = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var review = cut.InvokeAsync(() => cut.Find("[data-testid='agents-editor-retry-verification']").ClickAsync());
        await commands.ReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await cut.InvokeAsync(() => draft.Summary = "Typed during read-back");
        commands.ReadRelease.SetResult();
        await review;
        Assert.Same(context, cut.FindComponent<EditForm>().Instance.EditContext);
        Assert.Equal("Unsaved before verification", draft.Name);
        Assert.Equal("Typed during diagnostic", draft.Instructions);
        Assert.Equal("Typed during read-back", draft.Summary);
        Assert.Equal(1, diagnostic.Calls);
        Assert.Equal(0, commands.Writes);
        Assert.Equal(competingEdit ? originalVersion : proofVersion, draft.ExpectedUpdatedAtUtc);
        if (competingEdit) {
            Assert.Contains("changed elsewhere", cut.Find("[data-testid='agents-editor-verification-result']").TextContent);
            Assert.True(cut.Find("[data-testid='agents-catalog-save']").HasAttribute("disabled"));
            await cut.InvokeAsync(() => cut.Find("form").SubmitAsync());
            Assert.Equal(0, commands.Writes);
            Assert.Equal("Another operator's change", (await workspace.GetAgentEditorAsync(id)).Name);
        } else {
            await cut.InvokeAsync(() => cut.Find("form").SubmitAsync());
            Assert.Equal(1, commands.Writes);
            var saved = await workspace.GetAgentEditorAsync(id);
            Assert.Equal(draft.Name, saved.Name);
            Assert.Equal(draft.Summary, saved.Summary);
            Assert.Equal(draft.Instructions, saved.Instructions);
        }
    }

    [Fact]
    public async Task Another_editor_keeps_its_version_and_native_conflict_after_own_proof_update() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var workspace = harness.Context.Services.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var capabilityId = await workspace.SaveCapabilityAsync(new() {
            Kind = CapabilityKind.Skill, Key = "independent-editor-proof", Name = "Independent editor proof",
            EndpointOrPath = "inline://independent-editor-proof",
            ConfigurationJson = """{"inlineSkill":{"instructions":"Review fixture."}}"""
        });
        var id = await workspace.SaveAgentAsync(new() { Name = "Two editors", SelectedCapabilityIds = [capabilityId] });
        var first = harness.Context.Render<AgentDetailsDialog>(p => p.Add(x => x.AgentId, id).Add(x => x.Section, AgentEditorSection.Capabilities));
        var second = harness.Context.Render<AgentDetailsDialog>(p => p.Add(x => x.AgentId, id));
        first.WaitForElement("[data-testid='agents-details-capability-verify']");
        second.WaitForElement("[data-testid='agents-catalog-name']");
        var context = second.FindComponent<EditForm>().Instance.EditContext!;
        var draft = (AgentEditorModel)context.Model;
        var originalVersion = draft.ExpectedUpdatedAtUtc;
        await second.InvokeAsync(() => second.Find("[data-testid='agents-catalog-name']").Input("Other unsaved editor"));
        await first.InvokeAsync(() => first.FindAll("[data-testid='agents-details-capability-verify']")
            .Single(button => !button.HasAttribute("disabled")).ClickAsync());
        Assert.Equal(originalVersion, draft.ExpectedUpdatedAtUtc);
        await second.InvokeAsync(() => second.Find("form").SubmitAsync());
        Assert.Equal("Two editors", (await workspace.GetAgentEditorAsync(id)).Name);
        Assert.Same(context, second.FindComponent<EditForm>().Instance.EditContext);
        Assert.Equal("Other unsaved editor", draft.Name);
        Assert.Contains(harness.Context.Services.GetRequiredService<NotificationService>().Messages,
            message => message.Summary == "Agent changed elsewhere");
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Retired_verification_cannot_publish_into_clear_or_reopened_same_agent(bool holdRead, bool reopen) {
        var diagnostic = new CountingInlineDiagnostic {
            Release = holdRead ? null : new(TaskCreationOptions.RunContinuationsAsynchronously)
        };
        VerificationCommands? commands = null;
        await using var harness = await ComponentTestHarness.CreateAsync(services => {
            services.AddSingleton<ICapabilityProofService>(diagnostic);
            services.AddScoped<IAgentEditorCommands>(provider => commands = new(ActivatorUtilities.CreateInstance<AgentEditorCommands>(provider)));
        });
        var workspace = harness.Context.Services.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var capabilityId = await workspace.SaveCapabilityAsync(new() {
            Kind = CapabilityKind.Skill, Key = "retired-editor-proof", Name = "Retired editor proof",
            EndpointOrPath = "inline://retired-editor-proof",
            ConfigurationJson = """{"inlineSkill":{"instructions":"Review fixture."}}"""
        });
        var id = await workspace.SaveAgentAsync(new() { Name = "Retired editor", SelectedCapabilityIds = [capabilityId] });
        var otherId = await workspace.SaveAgentAsync(new() { Name = "Independent target" });
        var cut = harness.Context.Render<AgentDetailsDialog>(p => p.Add(x => x.AgentId, id).Add(x => x.Section, AgentEditorSection.Capabilities));
        cut.WaitForElement("[data-testid='agents-details-capability-verify']");
        if (holdRead) {
            commands!.ReadRelease = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        var pending = cut.InvokeAsync(() => cut.FindAll("[data-testid='agents-details-capability-verify']")
            .Single(button => !button.HasAttribute("disabled")).ClickAsync());
        await (holdRead ? commands!.ReadStarted.Task : diagnostic.Started.Task).WaitAsync(TimeSpan.FromSeconds(10));
        if (reopen) {
            cut.Render(p => p.Add(x => x.AgentId, otherId));
            cut.WaitForAssertion(() => Assert.Equal(otherId, ((AgentEditorModel)cut.FindComponent<EditForm>().Instance.EditContext!.Model).Id));
            cut.Render(p => p.Add(x => x.AgentId, id));
            cut.WaitForAssertion(() => Assert.Equal(id, ((AgentEditorModel)cut.FindComponent<EditForm>().Instance.EditContext!.Model).Id));
        } else {
            await cut.InvokeAsync(() => cut.FindComponent<StickyActionFooter>().FindAll("button")
                .Single(button => button.TextContent.Trim() == "Clear").ClickAsync());
        }
        var successor = cut.FindComponent<EditForm>().Instance.EditContext!;
        var draft = (AgentEditorModel)successor.Model;
        draft.Name = "Successor input";
        if (holdRead) {
            commands!.ReadRelease!.SetResult();
        } else {
            diagnostic.Release!.SetResult();
        }
        await pending;
        Assert.Same(successor, cut.FindComponent<EditForm>().Instance.EditContext);
        Assert.Equal("Successor input", draft.Name);
        Assert.Empty(cut.FindAll("[data-testid='agents-editor-verification-result']"));
        Assert.DoesNotContain(harness.Context.Services.GetRequiredService<NotificationService>().Messages,
            message => message.Summary is "Capability verified" or "Capability proof review failed");
        Assert.False(cut.Find("[data-testid='agents-catalog-save']").HasAttribute("disabled"));
        Assert.Equal(0, commands!.Writes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Lost_publication_acknowledgement_requires_exact_receipt_and_never_replays(bool retainReceipt) {
        DiagnosticAcknowledgementFault? fault = null;
        await using var harness = await ComponentTestHarness.CreateAsync(services =>
            services.AddScoped<IAgentCapabilityCommands>(provider => fault = new(
                ActivatorUtilities.CreateInstance<AgentCapabilityCommands>(provider), retainReceipt)));
        var workspace = harness.Context.Services.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var capabilityId = await workspace.SaveCapabilityAsync(new() {
            Kind = CapabilityKind.Skill, Key = "unconfirmed-editor-proof", Name = "Unconfirmed editor proof",
            EndpointOrPath = "inline://unconfirmed-editor-proof",
            ConfigurationJson = """{"inlineSkill":{"instructions":"Review fixture."}}"""
        });
        var id = await workspace.SaveAgentAsync(new() { Name = "Original", SelectedCapabilityIds = [capabilityId] });
        var cut = harness.Context.Render<AgentDetailsDialog>(p => p.Add(x => x.AgentId, id).Add(x => x.Section, AgentEditorSection.Capabilities));
        cut.WaitForElement("[data-testid='agents-details-capability-verify']");
        var context = cut.FindComponent<EditForm>().Instance.EditContext!;
        var draft = (AgentEditorModel)context.Model;
        var version = draft.ExpectedUpdatedAtUtc;
        draft.Name = "Retained local input";
        await cut.InvokeAsync(() => cut.FindAll("[data-testid='agents-details-capability-verify']")
            .Single(button => !button.HasAttribute("disabled")).ClickAsync());
        var persisted = await workspace.GetAgentEditorAsync(id);
        Assert.NotEqual(version, persisted.ExpectedUpdatedAtUtc);
        Assert.Equal("Original", persisted.Name);
        Assert.Same(context, cut.FindComponent<EditForm>().Instance.EditContext);
        Assert.Equal("Retained local input", draft.Name);
        Assert.Equal(retainReceipt ? persisted.ExpectedUpdatedAtUtc : version, draft.ExpectedUpdatedAtUtc);
        if (!retainReceipt) {
            await cut.InvokeAsync(() => cut.Find("[data-testid='agents-editor-retry-verification']").ClickAsync());
            Assert.Contains("No proof receipt", cut.Find("[data-testid='agents-editor-verification-result']").TextContent);
            Assert.True(cut.Find("[data-testid='agents-catalog-save']").HasAttribute("disabled"));
        }
        Assert.Equal(1, fault!.Calls);
    }

    private sealed class DiagnosticAcknowledgementFault(IAgentCapabilityCommands inner, bool retainReceipt) : IAgentCapabilityCommands {
        public int Calls { get; private set; }
        public async Task<CapabilityVerificationOutcome> DiagnoseAsync(Guid agentId, Guid capabilityId, CancellationToken cancellationToken = default) {
            Calls++;
            var committed = await inner.DiagnoseAsync(agentId, capabilityId, cancellationToken);
            Assert.Equal(CapabilityVerificationDisposition.Committed, committed.Disposition);
            return new(CapabilityVerificationDisposition.Unconfirmed, retainReceipt ? committed.Receipt : null);
        }
        public Task<AgentCapabilityOperationStatus> AssignAsync(AgentCapabilityAssignmentAttempt attempt, CancellationToken cancellationToken = default)
            => inner.AssignAsync(attempt, cancellationToken);
    }

    private sealed class VerificationCommands(IAgentEditorCommands inner) : IAgentEditorCommands {
        public bool FailRead { get; set; }
        public int Writes { get; private set; }
        public TaskCompletionSource ReadStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource? ReadRelease { get; set; }
        public Task<AgentEditorSaveOutcome> SaveAsync(AgentEditorModel request, CancellationToken cancellationToken = default) {
            Writes++;
            return inner.SaveAsync(request, cancellationToken);
        }
        public async Task<AgentEditorCatalogRefresh> ReconcileAsync(Guid agentId, IReadOnlyList<ProviderProfile> providers,
            CancellationToken cancellationToken = default) {
            if (FailRead) {
                throw new IOException("Controlled verification read failure");
            }
            if (ReadRelease is { } held) {
                ReadStarted.TrySetResult();
                await held.Task;
            }
            return await inner.ReconcileAsync(agentId, providers, cancellationToken);
        }
        public Task DeleteAsync(Guid agentId, CancellationToken cancellationToken = default) => inner.DeleteAsync(agentId, cancellationToken);
        public Task VerifyCapabilityAsync(Guid agentId, Guid capabilityId, CancellationToken cancellationToken = default)
            => inner.VerifyCapabilityAsync(agentId, capabilityId, cancellationToken);
    }

    private sealed class CountingInlineDiagnostic : ICapabilityProofService {
        public int Calls { get; private set; }
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource? Release { get; set; }

        public async Task<CapabilityVerificationResult> VerifyAsync(AgentDefinition agent, ProviderProfile? provider,
            CapabilityCatalogItem capability, CancellationToken cancellationToken = default) {
            Calls++;
            Started.TrySetResult();
            if (Release is { } held) {
                await held.Task;
            }
            return await new CapabilityProofService(new PhysicalFileSystemPathPolicyFactory())
                .VerifyAsync(agent, provider, capability, cancellationToken);
        }
    }
}
