using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public sealed partial class AgentToolAdmissionJournal {
    public static async Task<AgentHistoricalReceiptReview> ReadHistoricalReceiptReviewAsync(
        ISandboxWorkspaceHistoricalReadStore? historicalReader, AgentToolProfileBinding profile,
        AgentHistoricalReceiptReviewRequest request, WorkspaceScopeDescriptor workspaceScope,
        Func<AgentToolSessionObservation, AgentHistoricalReceiptReadFence, CancellationToken, ValueTask<AgentHistoricalReceiptReadPolicy>> currentRead,
        IReadOnlyList<IAgentToolHistoricalBackgroundSourcePolicy> sourceReaders,
        IReadOnlyList<IAgentToolHistoricalReceiptReadProvider> providers, AgentHistoricalCatalogBasis basis, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(workspaceScope);
        ArgumentNullException.ThrowIfNull(currentRead);
        ArgumentNullException.ThrowIfNull(providers);
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(sourceReaders);
        ArgumentNullException.ThrowIfNull(basis);
        var backgroundReaders = sourceReaders.ToDictionary(item => item.SourceKind, StringComparer.OrdinalIgnoreCase);
        var fence = new AgentHistoricalReceiptReadFence(profile, workspaceScope);
        if (basis.Fence != fence) {
            throw new ArgumentException("The historical catalog basis belongs to a different read fence.", nameof(basis));
        }
        AgentHistoricalReceiptReview Refuse(AgentHistoricalReceiptReviewStop stop) => new(request, fence.Context, null, null, stop, []);
        if (historicalReader is null) {
            basis.Retire();
            return Refuse(AgentHistoricalReceiptReviewStop.Unsupported);
        }
        if (request.Cursor is { } cursorContext && cursorContext.CurrentContext != fence.Context) {
            basis.Retire();
            return Refuse(AgentHistoricalReceiptReviewStop.ContextChanged);
        }
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(ReceiptSliceBudget);
        try {
            var first = await historicalReader.ReadHistoricalRunAsync(request.ExecutionRunId, budget.Token);
            if (first.Status != AgentHistoricalStorageReadStatus.Ready) {
                return Refuse(HistoricalStorageStop(first.Status));
            }
            var original = first.Snapshot ?? throw new InvalidDataException("The historical reader returned no validated snapshot.");
            if (!HistoricalIdentityMatches(request, original, fence)) {
                return Refuse(AgentHistoricalReceiptReviewStop.ReadDenied);
            }
            var run = original.Run;
            var journal = run.ToolAdmission!;
            journal.Validate();
            if (journal.SchemaVersion is not (AgentToolJournalRecord.EffectJournalSchemaVersion or AgentToolJournalRecord.PlanningSchemaVersion or
                    AgentToolJournalRecord.InheritedPlanningSchemaVersion) ||
                journal.Support != AgentToolAdmissionSupport.EffectJournalOnly) {
                return Refuse(AgentHistoricalReceiptReviewStop.Unsupported);
            }
            var observation = AgentToolSessionObservation.From(run);
            async ValueTask RequireReadCoreAsync(CancellationToken token) {
                var decision = await currentRead(observation, fence, token);
                if (decision != AgentHistoricalReceiptReadPolicy.Allowed) {
                    throw new HistoricalReadRefusal(decision switch {
                        AgentHistoricalReceiptReadPolicy.Denied => AgentHistoricalReceiptReviewStop.ReadDenied,
                        AgentHistoricalReceiptReadPolicy.ContextChanged => AgentHistoricalReceiptReviewStop.ContextChanged,
                        _ => AgentHistoricalReceiptReviewStop.Unsupported
                    });
                }
                if (journal.Session.Reference.BackgroundSource is { } background) {
                    if (!backgroundReaders.TryGetValue(background.SourceKind, out var sourcePolicy)) {
                        throw new HistoricalReadRefusal(AgentHistoricalReceiptReviewStop.Unsupported);
                    }
                    var observed = await sourcePolicy.ObserveHistoricalReadAsync(run, fence, token);
                    if (!observed.ReadAllowed || observed.OwnerFingerprint != background.OwnerFingerprint) {
                        throw new HistoricalReadRefusal(AgentHistoricalReceiptReviewStop.ReadDenied);
                    }
                    var finalRead = await currentRead(observation, fence, token);
                    if (finalRead != AgentHistoricalReceiptReadPolicy.Allowed) {
                        throw new HistoricalReadRefusal(finalRead switch {
                            AgentHistoricalReceiptReadPolicy.Denied => AgentHistoricalReceiptReviewStop.ReadDenied,
                            AgentHistoricalReceiptReadPolicy.ContextChanged => AgentHistoricalReceiptReviewStop.ContextChanged,
                            _ => AgentHistoricalReceiptReviewStop.Unsupported
                        });
                    }
                }
                token.ThrowIfCancellationRequested();
            }
            async ValueTask RequireReadAsync(CancellationToken token) {
                try {
                    await RequireReadCoreAsync(token);
                } catch (Exception exception) when (exception is not (HistoricalReadRefusal or AgentToolReceiptAccessDeniedException or
                    AgentToolReceiptReadUnavailableException or AgentHistoricalReceiptContextChangedException or AgentHistoricalCatalogChangedException or
                    NotSupportedException or OperationCanceledException or OutOfMemoryException)) {
                    throw new AgentToolReceiptReadUnavailableException(exception);
                }
            }
            await basis.CaptureExecutorAsync(journal.Session.AgentId, budget.Token);
            await RequireReadAsync(budget.Token);
            basis.Seal(budget.Token);
            var proposalCount = 0;
            foreach (var batch in journal.Batches) {
                proposalCount = checked(proposalCount + batch.Proposals.Length);
            }
            var position = request.Cursor?.NextProposalOrdinal ?? 0;
            if (request.Cursor is { } cursor && (cursor.ExecutionRunId != request.ExecutionRunId ||
                cursor.RunRevision != run.Revision || cursor.JournalRevision != journal.Revision || position > proposalCount)) {
                return Refuse(AgentHistoricalReceiptReviewStop.SnapshotChanged);
            }
            var observations = new List<AgentHistoricalReceiptObservation>(AgentHistoricalReceiptReview.MaximumObservations);
            var stop = AgentHistoricalReceiptReviewStop.ExhaustedSnapshot;
            var ordinal = 0;
            var finished = false;
            foreach (var batch in journal.Batches) {
                foreach (var proposal in batch.Proposals) {
                    if (ordinal++ < position) {
                        continue;
                    }
                    if (proposal.Payload.Recovery != AgentToolProposalRecovery.OwnerReceipt) {
                        position = ordinal;
                        continue;
                    }
                    if (observations.Count == AgentHistoricalReceiptReview.MaximumObservations) {
                        stop = AgentHistoricalReceiptReviewStop.LookupLimit;
                        finished = true;
                        break;
                    }
                    var binding = Binding(batch.Id, proposal);
                    var matches = providers.Where(provider => provider.Supports(proposal.Payload.ToolName)).Take(2).ToArray();
                    if (matches.Length > 1) {
                        throw new InvalidDataException("Multiple historical receipt readers claim the same original tool.");
                    }
                    if (matches.Length == 0 || proposal.Payload.SemanticVersion < 2 || proposal.Payload.SourcePreparation is null) {
                        observations.Add(new(binding, AgentHistoricalReceiptKnowledge.Unsupported));
                        position = ordinal;
                        continue;
                    }
                    var admission = new AgentToolHistoricalReceiptReadAdmission(request, journal.Session, binding, proposal.Payload,
                        fence, run.Revision, journal.Revision) { Planning = observation.Planning };
                    var claim = new AgentToolHistoricalReceiptReadClaim(admission, (_, token) => {
                        basis.RequireCurrent(token);
                        return ValueTask.CompletedTask;
                    }, new(journal.Session, binding, run.Revision, journal.Revision,
                        AgentToolPriorInvocationEvidence.Capture(journal, binding), null));
                    AgentToolHistoricalReceiptObservation observed;
                    try {
                        observed = await claim.ReadAsync(matches[0], budget.Token);
                    } catch (Exception exception) when (exception is not (HistoricalReadRefusal or AgentToolReceiptAccessDeniedException or
                            AgentToolReceiptReadUnavailableException or AgentHistoricalReceiptContextChangedException or AgentHistoricalCatalogChangedException or
                            NotSupportedException or OperationCanceledException or OutOfMemoryException)) {
                        stop = AgentHistoricalReceiptReviewStop.ReadUnavailable;
                        finished = true;
                        break;
                    }
                    var knowledge = observed.Knowledge;
                    var confirmedEffect = proposal.OwnerReceipt?.Effect ?? proposal.Cancellation?.CommittedEffect;
                    var confirmedReceipt = proposal.OwnerReceipt?.Receipt ?? proposal.Cancellation?.Receipt;
                    if (confirmedEffect is not null) {
                        if (observed.Receipt is { } receipt &&
                            (receipt.CommittedEffect != confirmedEffect || receipt.Receipt != confirmedReceipt)) {
                            throw new InvalidDataException("The current owner receipt differs from the retained original confirmation.");
                        }
                        knowledge = knowledge switch {
                            AgentHistoricalReceiptKnowledge.Accepted => knowledge,
                            AgentHistoricalReceiptKnowledge.ReadDenied => AgentHistoricalReceiptKnowledge.AcceptedDetailDenied,
                            _ => AgentHistoricalReceiptKnowledge.AcceptedDetailUnavailable
                        };
                    }
                    observations.Add(new(binding, knowledge));
                    position = ordinal;
                }
                if (finished) {
                    break;
                }
            }
            var final = await historicalReader.ReadHistoricalRunAsync(request.ExecutionRunId, budget.Token);
            if (final.Status != AgentHistoricalStorageReadStatus.Ready) {
                return Refuse(HistoricalStorageStop(final.Status));
            }
            if (final.Snapshot is null || !HistoricalWitnessMatches(original, final.Snapshot, request, fence)) {
                return Refuse(AgentHistoricalReceiptReviewStop.SnapshotChanged);
            }
            await RequireReadAsync(budget.Token);
            await basis.ObserveFinalAsync(budget.Token);
            budget.Token.ThrowIfCancellationRequested();
            var next = stop == AgentHistoricalReceiptReviewStop.ExhaustedSnapshot ? null :
                new AgentHistoricalReceiptCursor(run.Id, run.Revision, journal.Revision, position, fence.Context);
            return new(request, fence.Context, run.Revision, journal.Revision, stop, observations, next);
        } catch (HistoricalReadRefusal refusal) {
            return Refuse(refusal.Stop);
        } catch (AgentHistoricalCatalogChangedException) {
            return Refuse(AgentHistoricalReceiptReviewStop.SnapshotChanged);
        } catch (AgentHistoricalReceiptContextChangedException) {
            return Refuse(AgentHistoricalReceiptReviewStop.ContextChanged);
        } catch (NotSupportedException) {
            return Refuse(AgentHistoricalReceiptReviewStop.Unsupported);
        } catch (AgentToolReceiptAccessDeniedException) {
            return Refuse(AgentHistoricalReceiptReviewStop.ReadDenied);
        } catch (OperationCanceledException) when (budget.IsCancellationRequested) {
            return Refuse(cancellationToken.IsCancellationRequested ? AgentHistoricalReceiptReviewStop.Cancelled :
                AgentHistoricalReceiptReviewStop.CooperativeBudget);
        } catch (Exception exception) when (exception is not (OperationCanceledException or OutOfMemoryException)) {
            return Refuse(AgentHistoricalReceiptReviewStop.ReadUnavailable);
        } finally {
            basis.Retire();
        }
    }

    private static AgentHistoricalReceiptReviewStop HistoricalStorageStop(AgentHistoricalStorageReadStatus status) => status switch {
        AgentHistoricalStorageReadStatus.Missing => AgentHistoricalReceiptReviewStop.ReadDenied,
        AgentHistoricalStorageReadStatus.SnapshotChanged => AgentHistoricalReceiptReviewStop.SnapshotChanged,
        AgentHistoricalStorageReadStatus.ReadBudgetExceeded => AgentHistoricalReceiptReviewStop.CooperativeBudget,
        AgentHistoricalStorageReadStatus.UnsupportedLayout => AgentHistoricalReceiptReviewStop.Unsupported,
        _ => AgentHistoricalReceiptReviewStop.ReadUnavailable
    };

    private static bool HistoricalIdentityMatches(AgentHistoricalReceiptReviewRequest request,
        AgentHistoricalRunSnapshot detail, AgentHistoricalReceiptReadFence fence) {
        var run = detail.Run;
        if (run.Id != request.ExecutionRunId || run.AgentId != request.AgentId || run.ChatSessionId != request.ChatSessionId ||
            run.CreatedAtUtc != request.ExecutionCreatedAtUtc || run.ToolAdmission is not { } journal ||
            journal.Session is not { } session || session.Profile.ProfileId != fence.Profile.ProfileId ||
            session.Profile.Fingerprint != fence.Profile.Fingerprint || session.Reference.ExecutionRunId != run.Id || session.AgentId != run.AgentId) {
            return false;
        }
        if (session.Reference.BackgroundSource is { } background) {
            return session.Purpose == AgentRuntimeContextPurpose.GovernedProcessAutomation && run.ChatSessionId is null &&
                detail.Chat is null && session.Reference.ChatSessionId == Guid.Empty && session.Reference.AuthorityId.IsEmpty &&
                run.SourceKind == background.SourceKind && run.SourceId == background.SourceId &&
                BackgroundExecutionFingerprint(run) == background.ExecutionFingerprint &&
                !AgentTurnContextMetadata.ContainsTurnContextReference(run.MetadataJson) &&
                AgentTurnContextMetadata.TryReadExecutionGovernanceSnapshot(run.MetadataJson) is null;
        }
        var governance = AgentTurnContextMetadata.TryReadExecutionGovernanceSnapshot(run.MetadataJson);
        return session.Purpose == AgentRuntimeContextPurpose.InteractiveChat && run.ChatSessionId == session.Reference.ChatSessionId &&
            detail.Chat is { } chat && chat.Id == session.Reference.ChatSessionId && chat.AgentId == session.AgentId &&
            governance is not null && governance.AuthorityId == session.Reference.AuthorityId && governance.AgentId == session.AgentId &&
            governance.DatabaseProfileId == session.Profile.ProfileId && governance.DatabaseProfileGeneration == session.Profile.Generation && governance.ReadAllowed;
    }

    private static bool HistoricalWitnessMatches(AgentHistoricalRunSnapshot original, AgentHistoricalRunSnapshot current,
        AgentHistoricalReceiptReviewRequest request, AgentHistoricalReceiptReadFence fence) {
        return HistoricalIdentityMatches(request, current, fence) && original.FileDigest == current.FileDigest && original.Chat == current.Chat;
    }

    private sealed class HistoricalReadRefusal(AgentHistoricalReceiptReviewStop stop) : Exception {
        internal AgentHistoricalReceiptReviewStop Stop { get; } = stop;
    }
}
