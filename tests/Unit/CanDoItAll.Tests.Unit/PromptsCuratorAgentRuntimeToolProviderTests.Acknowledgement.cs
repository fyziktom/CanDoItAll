using System.Reflection;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Prompts;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.AI;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public enum CuratorOwnerAcknowledgementFault { BeforeOwner, BeforeReceipt, AfterReceipt }

public sealed partial class PromptsCuratorAgentRuntimeToolProviderTests {
    [Theory]
    [InlineData(CuratorOwnerAcknowledgementFault.BeforeOwner)]
    [InlineData(CuratorOwnerAcknowledgementFault.BeforeReceipt)]
    [InlineData(CuratorOwnerAcknowledgementFault.AfterReceipt)]
    public async Task Prompt_acknowledgement_distinguishes_owner_failure_lost_return_and_failed_editor_read(
        CuratorOwnerAcknowledgementFault fault) {
        var gallery = PromptGalleryTestSupport.CreateService(PromptGalleryTestSupport.CreateFactory(
            $"{nameof(Prompt_acknowledgement_distinguishes_owner_failure_lost_return_and_failed_editor_read)}-{fault}"));
        var service = DispatchProxy.Create<IPromptGalleryService, PromptAcknowledgementProxy>();
        var owner = (PromptAcknowledgementProxy)(object)service;
        owner.Inner = gallery;
        owner.Fault = fault;
        var harness = CreateHarness(service);
        var tool = Assert.IsAssignableFrom<AIFunction>((await harness.Provider.CreateToolsAsync(harness.Context, default))
            .Single(item => item.Name == PromptGalleryToolPolicy.PromptGalleryDraftCreate));
        using var capture = AgentToolInvocationEffectScope.Begin();
        await Assert.ThrowsAsync<IOException>(() => tool.InvokeAsync(new AIFunctionArguments {
            ["request"] = CreateDraftInput("Acknowledged draft", "Exact persisted content.")
        }).AsTask());
        Assert.Equal(1, owner.Calls);
        if (fault == CuratorOwnerAcknowledgementFault.BeforeOwner) {
            Assert.Null(owner.Saved);
        } else {
            var saved = Assert.IsType<PromptDraftSaveReceipt>(owner.Saved);
            var retained = await gallery.GetItemAsync(saved.PromptArtifactId);
            Assert.True(retained.IsSuccess);
            Assert.Equal("Exact persisted content.", retained.Value!.DraftContent);
        }
        Assert.Equal(fault == CuratorOwnerAcknowledgementFault.AfterReceipt
            ? new AgentToolCommittedEffect("prompt-artifact", Assert.IsType<PromptDraftSaveReceipt>(owner.Saved).PromptArtifactId.ToString("D"))
            : null, capture.CommittedEffect);
    }

    private static void AssertOwnerAcknowledgement<T>(string toolName, T result, AgentToolCommittedEffect? actual) {
        AgentToolCommittedEffect? expected = toolName switch {
            PromptGalleryToolPolicy.PromptGalleryDraftCreate or PromptGalleryToolPolicy.PromptGalleryDraftUpdate =>
                new("prompt-artifact", Assert.IsType<PromptsCuratorItemEditorResult>(result).PromptArtifactId.ToString("D")),
            PromptGalleryToolPolicy.PromptGalleryVersionCreate =>
                new("prompt-version", Assert.IsType<PromptVersionSnapshot>(result).PromptVersionId.ToString("D")),
            _ => null
        };
        Assert.Equal(expected, actual);
    }

    private class PromptAcknowledgementProxy : DispatchProxy {
        public IPromptGalleryService Inner { get; set; } = null!;
        public CuratorOwnerAcknowledgementFault Fault { get; set; }
        public PromptDraftSaveReceipt? Saved { get; private set; }
        public int Calls { get; private set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => targetMethod?.Name switch {
            nameof(IPromptGalleryService.SaveDraftAsync) => SaveAsync((PromptGalleryDraft)args![0]!, (CancellationToken)args[1]!),
            nameof(IPromptGalleryService.GetItemAsync) => throw new IOException("Read after the returned Prompt receipt failed."),
            _ => throw new NotSupportedException(targetMethod?.Name)
        };

        private async Task<Result<PromptDraftSaveReceipt>> SaveAsync(PromptGalleryDraft request, CancellationToken token) {
            Calls++;
            if (Fault == CuratorOwnerAcknowledgementFault.BeforeOwner) {
                throw new IOException("Prompt save was not started.");
            }
            var result = await Inner.SaveDraftAsync(request, token);
            Assert.True(result.IsSuccess);
            Saved = result.Value!;
            if (Fault == CuratorOwnerAcknowledgementFault.BeforeReceipt) {
                throw new IOException("The Prompt receipt was lost.");
            }
            return result;
        }
    }
}
