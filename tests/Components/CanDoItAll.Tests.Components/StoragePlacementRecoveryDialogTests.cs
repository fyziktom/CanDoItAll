using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Workspace.Pages.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.Workspace;

public sealed class StoragePlacementRecoveryDialogTests {
    [Fact]
    public void Loading_selecting_and_refreshing_only_observe_both_pending_feeds() {
        var source = new RecoverySource();
        using var context = CreateContext(source);
        var dialog = context.Render<StoragePlacementRecoveryDialog>();
        Inspect(dialog, source.Pending);
        dialog.Find("[data-testid='storage-recovery-refresh']").Click();

        dialog.WaitForAssertion(() => Assert.True(source.PendingReads >= 2 && source.ContinuationReads >= 2));
        Assert.Empty(source.Commands);
        Assert.Empty(source.Verifications);
        Assert.Empty(source.OwnerCommands);
        Assert.Contains(source.Pending.Identity.IntentId.Value.ToString(), dialog.Markup, StringComparison.Ordinal);
        Assert.Contains(source.Completed.Identity.IntentId.Value.ToString(), dialog.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Reconcile_keeps_the_original_intent_and_context_and_refreshes_both_feeds() {
        var source = new RecoverySource();
        using var context = CreateContext(source);
        var dialog = context.Render<StoragePlacementRecoveryDialog>();
        Inspect(dialog, source.Pending);
        dialog.Find("[data-testid='storage-recovery-reconcile']").Click();

        dialog.WaitForAssertion(() => Assert.Single(source.Commands));
        Assert.Equal(new StoragePlacementRecoveryCommand(source.Pending.Context, source.Pending.Identity.IntentId), source.Commands[0]);
        Assert.True(source.PendingReads >= 2 && source.ContinuationReads >= 2);
        Assert.Contains(source.Pending.Identity.IntentId.Value.ToString(),
            dialog.Find("[data-testid='storage-recovery-selected-intent']").TextContent, StringComparison.Ordinal);
        Assert.Empty(source.OwnerCommands);
    }

    [Fact]
    public void External_termination_requires_explicit_attestation_before_the_exact_command() {
        var source = new RecoverySource();
        source.Pending = source.Pending with { AvailableAction = StoragePlacementRecoveryAction.VerifyExternalTermination };
        using var context = CreateContext(source);
        var dialog = context.Render<StoragePlacementRecoveryDialog>();
        Inspect(dialog, source.Pending);
        var command = dialog.Find("[data-testid='storage-recovery-verify-external']");
        Assert.True(command.HasAttribute("disabled"));
        Assert.Empty(source.Verifications);

        dialog.Find("[data-testid='storage-recovery-external-stopped']").Change(true);
        dialog.Find("[data-testid='storage-recovery-verify-external']").Click();
        dialog.WaitForAssertion(() => Assert.Single(source.Verifications));
        Assert.Equal(new StoragePlacementExternalTerminationVerification(source.Pending.Context, source.Pending.Identity.IntentId, true), source.Verifications[0]);
        Assert.Empty(source.Commands);
        Assert.Empty(source.OwnerCommands);
    }

    [Fact]
    public void Cancelled_run_receipts_require_their_own_explicit_action() {
        var source = new RecoverySource { OwnerAction = StoragePlacementOwnerContinuationAction.ReconcileCancelledRunReceipts };
        using var context = CreateContext(source);
        var dialog = context.Render<StoragePlacementRecoveryDialog>();
        Inspect(dialog, source.Completed);
        Assert.Empty(source.OwnerCommands);
        dialog.Find("[data-testid='storage-recovery-reconcile-cancelled']").Click();

        dialog.WaitForAssertion(() => Assert.Single(source.OwnerCommands));
        Assert.Equal(new StoragePlacementRecoveryCommand(source.Completed.Context, source.Completed.Identity.IntentId), source.OwnerCommands[0]);
        Assert.Empty(source.Commands);
        Assert.Empty(source.Verifications);
    }

    [Fact]
    public void A_stale_context_refreshes_observation_without_redirecting_or_repeating_the_command() {
        var source = new RecoverySource { ChangeContextOnCommand = true };
        var original = source.Context;
        using var context = CreateContext(source);
        var dialog = context.Render<StoragePlacementRecoveryDialog>();
        Inspect(dialog, source.Pending);
        dialog.Find("[data-testid='storage-recovery-reconcile']").Click();

        dialog.WaitForAssertion(() => Assert.Contains("database selection changed",
            dialog.Find("[data-testid='storage-recovery-error']").TextContent, StringComparison.Ordinal));
        Assert.Equal(original, Assert.Single(source.Commands).Context);
        Assert.NotEqual(original, source.Context);
        Assert.True(source.PendingReads >= 2 && source.ContinuationReads >= 2);
        Assert.Empty(dialog.FindAll("[data-testid='storage-recovery-selected-intent']"));
        Assert.Empty(source.OwnerCommands);
    }

    [Fact]
    public void Read_only_status_exposes_no_mutation_actions_and_an_empty_scanned_page_can_continue() {
        var source = new RecoverySource { EmptyContinuationPage = true };
        source.Pending = source.Pending with {
            AvailableAction = StoragePlacementRecoveryAction.None, Block = StoragePlacementRecoveryBlock.ReadOnlyAuthority
        };
        using var context = CreateContext(source);
        var dialog = context.Render<StoragePlacementRecoveryDialog>();
        Inspect(dialog, source.Pending);
        Assert.Empty(dialog.FindAll("[data-testid='storage-recovery-reconcile']"));
        Assert.Empty(dialog.FindAll("[data-testid='storage-recovery-verify-external']"));
        Assert.Empty(dialog.FindAll("[data-testid='storage-recovery-reconcile-cancelled']"));
        dialog.Find("[data-testid='storage-recovery-next-owners']").Click();

        dialog.WaitForAssertion(() => Assert.Contains(8, source.ContinuationOffsets));
        Assert.Contains(source.Completed.Identity.IntentId.Value.ToString(), dialog.Markup, StringComparison.Ordinal);
        Assert.Empty(source.Commands);
        Assert.Empty(source.OwnerCommands);
    }

    private static void Inspect(IRenderedComponent<StoragePlacementRecoveryDialog> dialog, StoragePlacementRecoveryItem item) {
        dialog.WaitForElement($"[data-testid='storage-recovery-inspect-{item.Identity.IntentId.Value:N}']").Click();
        dialog.WaitForElement("[data-testid='storage-recovery-selected-intent']");
    }

    private static BunitContext CreateContext(RecoverySource source) {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
        context.Services.AddLogging();
        context.Services.AddSingleton<IStoragePlacementRecovery>(source);
        context.Services.AddSingleton<IStoragePlacementOwnerContinuation>(source);
        return context;
    }

    private sealed class RecoverySource : IStoragePlacementRecovery, IStoragePlacementOwnerContinuation {
        internal RecoverySource() {
            var now = new DateTimeOffset(2026, 9, 11, 0, 0, 0, TimeSpan.Zero);
            Pending = new(Context, new(new(Guid.NewGuid()), Guid.NewGuid(), Guid.NewGuid(), StorageProviderKind.FileSystem),
                StorageStablePlacementState.Uncertain, now, now, false, StoragePlacementRecoveryOwner.ProcessAsset, false,
                StoragePlacementRecoveryAction.Reconcile, StoragePlacementRecoveryBlock.None);
            Completed = Pending with {
                Identity = Pending.Identity with { IntentId = new(Guid.NewGuid()) }, StorageState = StorageStablePlacementState.Completed,
                StorageReceiptPresent = true, NativeReceiptPresent = true, AvailableAction = StoragePlacementRecoveryAction.None
            };
        }

        internal StoragePlacementRecoveryContext Context { get; private set; } = new(Guid.NewGuid(), 1);
        internal StoragePlacementRecoveryItem Pending { get; set; }
        internal StoragePlacementRecoveryItem Completed { get; }
        internal StoragePlacementOwnerContinuationAction OwnerAction { get; init; }
        internal bool ChangeContextOnCommand { get; init; }
        internal bool EmptyContinuationPage { get; init; }
        internal int PendingReads { get; private set; }
        internal int ContinuationReads { get; private set; }
        internal List<int> ContinuationOffsets { get; } = [];
        internal List<StoragePlacementRecoveryCommand> Commands { get; } = [];
        internal List<StoragePlacementExternalTerminationVerification> Verifications { get; } = [];
        internal List<StoragePlacementRecoveryCommand> OwnerCommands { get; } = [];

        public Task<StoragePlacementRecoveryContext> GetCurrentContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Context);

        public Task<StoragePlacementRecoveryPage> ListPendingAsync(StoragePlacementRecoveryQuery query,
            CancellationToken cancellationToken = default) {
            Assert.Equal(Context, query.Context);
            PendingReads++;
            return Task.FromResult(new StoragePlacementRecoveryPage([Pending with { Context = Context }], null));
        }

        public Task<StoragePlacementContinuationPage> ListPendingContinuationsAsync(StoragePlacementRecoveryQuery query,
            CancellationToken cancellationToken = default) {
            Assert.Equal(Context, query.Context);
            ContinuationReads++;
            ContinuationOffsets.Add(query.Offset);
            return Task.FromResult(EmptyContinuationPage && query.Offset == 0
                ? new StoragePlacementContinuationPage([], query.Take)
                : new StoragePlacementContinuationPage([new(Completed with { Context = Context }, StoragePlacementContinuationPhase.CoreCheckpoint)], null));
        }

        public Task<StoragePlacementRecoveryItem> GetAsync(StoragePlacementRecoveryCommand request,
            CancellationToken cancellationToken = default) {
            Assert.Equal(Context, request.Context);
            return Task.FromResult(request.IntentId == Pending.Identity.IntentId ? Pending : Completed);
        }

        Task<StoragePlacementOwnerContinuationObservation> IStoragePlacementOwnerContinuation.GetAsync(
            StoragePlacementRecoveryCommand request, CancellationToken cancellationToken)
            => Task.FromResult(new StoragePlacementOwnerContinuationObservation(
                request.IntentId == Pending.Identity.IntentId ? Pending : Completed, OwnerAction, StoragePlacementOwnerContinuationState.Ready));

        public Task<StoragePlacementRecoveryItem> ReconcileAsync(StoragePlacementRecoveryCommand request,
            CancellationToken cancellationToken = default) {
            Commands.Add(request);
            if (ChangeContextOnCommand) {
                Context = new(Guid.NewGuid(), 2);
                throw new StoragePlacementRecoveryException(StoragePlacementRecoveryFailure.StaleContext);
            }
            return GetAsync(request, cancellationToken);
        }

        public Task<StoragePlacementRecoveryItem> RecordOperatorVerifiedExternalDispatchTerminationAsync(
            StoragePlacementExternalTerminationVerification request, CancellationToken cancellationToken = default) {
            Verifications.Add(request);
            return GetAsync(new(request.Context, request.IntentId), cancellationToken);
        }

        public Task<StoragePlacementOwnerContinuationObservation> ReconcileCancelledRunReceiptsAsync(
            StoragePlacementRecoveryCommand request, CancellationToken cancellationToken = default) {
            OwnerCommands.Add(request);
            return ((IStoragePlacementOwnerContinuation)this).GetAsync(request, cancellationToken);
        }

        public Task<StoragePlacementOwnerContinuationObservation> ReconcileWorkflowAssetAsync(StoragePlacementWorkflowContinuationCommand request,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("This Process receipt fixture does not offer a Workflow action.");
    }
}
