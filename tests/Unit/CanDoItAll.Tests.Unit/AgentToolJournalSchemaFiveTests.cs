using System.Collections.Immutable;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Unit;

public sealed partial class AgentToolJournalSchemaFiveTests {
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void Legacy_journals_preserve_wire_shape_and_restart_semantics(int schema) {
        var journal = Create(schema, effectsOnly: false);
        journal.Validate();
        string json = JsonSerializer.Serialize(journal);
        Assert.DoesNotContain(nameof(AgentToolInvocationSegment.CheckpointKind), json);
        Assert.DoesNotContain(nameof(AgentToolInvocationSegment.OriginDispatchLeaseId), json);
        Assert.DoesNotContain(nameof(AgentToolInvocationSegment.ProviderEffectScope), json);
        Assert.DoesNotContain(nameof(AgentToolProposalRecord.OwnerReceipt), json);
        Assert.DoesNotContain(nameof(AgentToolJournalRecord.ToolContinuationRequirement), json);
        var restored = JsonSerializer.Deserialize<AgentToolJournalRecord>(json)!;
        restored.Validate();
        Assert.Equal(json, JsonSerializer.Serialize(restored));
        Assert.Equal(AgentToolCheckpointKind.ProviderRestart, restored.Segments[0].CheckpointKind);
        Assert.Equal(AgentToolProviderEffectScope.Unknown, restored.Segments[0].ProviderEffectScope);
        Assert.Equal(journal.Segments[0].RestartCheckpoint, restored.Segments[0].RestartCheckpoint);
        var proposal = Proposal(journal);
        var retainedSource = new AgentToolPreparedPayload(proposal.Payload.ToolName, proposal.Payload.SemanticVersion,
            proposal.Payload.Digest, proposal.Payload.ArgumentsJson, proposal.Payload.Effect, proposal.Payload.Recovery,
            Envelope(new string('x', AgentToolPreparedPayload.MaximumSourcePreparationUtf8Bytes)));
        var retained = SetProposal(journal, proposal with { Payload = retainedSource });
        retained.Validate();
        JsonSerializer.Deserialize<AgentToolJournalRecord>(JsonSerializer.Serialize(retained))!.Validate();
        Assert.Throws<InvalidDataException>(() => (journal with {
            Segments = [journal.Segments[0] with { RestartCheckpoint = null }]
        }).Validate());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void Legacy_schema_refuses_effect_only_fields_and_owner_receipt_terminal(int schema) {
        var journal = Create(schema, effectsOnly: false);
        Assert.Throws<InvalidDataException>(() => (journal with { Support = AgentToolAdmissionSupport.EffectJournalOnly }).Validate());
        Assert.Throws<InvalidDataException>(() => (journal with {
            ToolContinuationRequirement = AgentToolContinuationRequirement.ProviderInputRequired
        }).Validate());
        Assert.Throws<InvalidDataException>(() => (journal with {
            Segments = [journal.Segments[0] with { OriginDispatchLeaseId = Guid.NewGuid() }]
        }).Validate());
        Assert.Throws<InvalidDataException>(() => (journal with {
            Segments = [journal.Segments[0] with {
                CheckpointKind = AgentToolCheckpointKind.EffectsOnly, RestartCheckpoint = null, OriginDispatchLeaseId = Guid.NewGuid()
            }]
        }).Validate());
        Assert.Throws<InvalidDataException>(() => SetProposal(journal, Confirm(Proposal(journal))).Validate());
    }

    [Fact]
    public void Effects_only_checkpoint_accepts_only_the_live_origin_and_no_provider_envelopes() {
        var journal = Create();
        journal.Validate();
        var segment = journal.Segments[0];
        foreach (var invalid in new[] {
            segment with { OriginDispatchLeaseId = null },
            segment with { OriginDispatchLeaseId = Guid.Empty },
            segment with { RestartCheckpoint = Envelope() },
            segment with { ApprovalCheckpoint = Envelope() },
            segment with { CheckpointKind = AgentToolCheckpointKind.ProviderRestart },
            segment with { CheckpointKind = (AgentToolCheckpointKind)99 },
            segment with { ProviderEffectScope = AgentToolProviderEffectScope.LocalProposalsOnly },
            segment with { ProviderEffectScope = AgentToolProviderEffectScope.HostedTools }
        }) {
            Assert.Throws<InvalidDataException>(() => (journal with { Segments = [invalid] }).Validate());
        }
        Assert.Throws<AgentToolAdmissionException>(() => AgentToolJournalTransitions.ValidatePersistence(journal,
            journal with { Revision = 2, Segments = [segment with { OriginDispatchLeaseId = Guid.NewGuid() }] }));
        Assert.Throws<InvalidDataException>(() => (journal with { RuntimeContext = TypedContext() }).Validate());
        Assert.Throws<InvalidDataException>(() => (journal with { Support = AgentToolAdmissionSupport.Recoverable }).Validate());
        (journal with { ActiveDispatchLeaseId = Guid.NewGuid() }).Validate();
    }

    [Theory]
    [InlineData(AgentToolProviderEffectScope.Unknown, AgentToolProviderEffectScope.LocalProposalsOnly)]
    [InlineData(AgentToolProviderEffectScope.LocalProposalsOnly, AgentToolProviderEffectScope.Unknown)]
    [InlineData(AgentToolProviderEffectScope.LocalProposalsOnly, AgentToolProviderEffectScope.HostedTools)]
    [InlineData(AgentToolProviderEffectScope.HostedTools, AgentToolProviderEffectScope.LocalProposalsOnly)]
    public void Original_provider_scope_is_immutable_and_survives_serialization(AgentToolProviderEffectScope original,
        AgentToolProviderEffectScope replacement) {
        var journal = Create(AgentToolJournalRecord.BackgroundSchemaVersion, effectsOnly: false);
        journal = journal with { Segments = [journal.Segments[0] with { ProviderEffectScope = original }] };
        journal.Validate();
        var restored = JsonSerializer.Deserialize<AgentToolJournalRecord>(JsonSerializer.Serialize(journal))!;
        restored.Validate();
        Assert.Equal(original, restored.Segments[0].ProviderEffectScope);
        Assert.Throws<AgentToolAdmissionException>(() => AgentToolJournalTransitions.ValidatePersistence(journal,
            journal with { Revision = journal.Revision + 1,
                Segments = [journal.Segments[0] with { ProviderEffectScope = replacement }] }));
        Assert.Throws<InvalidDataException>(() => (journal with {
            Segments = [journal.Segments[0] with { ProviderEffectScope = (AgentToolProviderEffectScope)99 }]
        }).Validate());
    }

    [Fact]
    public void Local_proposal_contract_cannot_contain_a_hosted_provider_dispatch() {
        var journal = Create(AgentToolJournalRecord.ProviderDispatchSchemaVersion, effectsOnly: false);
        journal.Validate();
        var segment = journal.Segments[0] with { ProviderEffectScope = AgentToolProviderEffectScope.LocalProposalsOnly };
        Assert.Throws<InvalidDataException>(() => (journal with { Segments = [segment] }).Validate());
    }

    [Fact]
    public void Effects_only_saved_approval_requires_exact_existing_proposal_binding() {
        var journal = Create();
        var proposal = Proposal(journal) with {
            State = AgentToolProposalState.Prepared, RequiresApproval = true, ApprovalId = "saved-approval",
            ApprovalStatus = ExecutionApprovalStatus.Pending, ApprovedDigest = null, DispatchClaimId = null
        };
        journal = SetProposal(journal, proposal);
        var approval = new PendingToolApprovalRecord("saved-approval", proposal.CallId, proposal.Payload.ToolName,
            "function", "Review original proposal", proposal.Payload.ArgumentsJson) { ToolAdmission = Binding(journal) };
        journal = journal with { Segments = [journal.Segments[0] with { PendingApprovals = [approval] }] };
        journal.Validate();
        foreach (var changed in new[] {
            approval with { ToolAdmission = null },
            approval with { ToolAdmission = approval.ToolAdmission! with { Digest = Envelope("changed").Digest } },
            approval with { ArgumentsJson = "{\"changed\":true}" },
            approval with { CallId = "other-call" },
            approval with { ApprovalId = "other-approval" }
        }) {
            Assert.Throws<InvalidDataException>(() => (journal with {
                Segments = [journal.Segments[0] with { PendingApprovals = [changed] }]
            }).Validate());
        }
        var approved = AgentToolJournalTransitions.ApplyDecisions(journal, [approval], [new("saved-approval", true)], false);
        Assert.Equal(proposal.Payload.Digest, Proposal(approved).ApprovedDigest);
        Assert.Equal(journal.Segments[0].PendingApprovals, approved.Segments[0].PendingApprovals);
    }

    [Fact]
    public void Owner_receipt_confirmation_roundtrips_without_a_provider_result() {
        var journal = Create();
        var confirmed = AgentToolJournalTransitions.Replace(journal, Binding(journal), Confirm) with {
            ToolContinuationRequirement = AgentToolContinuationRequirement.ProviderInputRequired
        };
        confirmed.Validate();
        var restored = JsonSerializer.Deserialize<AgentToolJournalRecord>(JsonSerializer.Serialize(confirmed))!;
        restored.Validate();
        Assert.Equal(Proposal(confirmed), Proposal(restored));
        Assert.Null(Proposal(restored).Result);
        Assert.Null(Proposal(restored).DisclosureEvidence);
        Assert.False(restored.HasUnresolvedEffects);
        Assert.Equal(AgentToolContinuationRequirement.ProviderInputRequired, restored.ToolContinuationRequirement);
        var providerJournal = restored with {
            Support = AgentToolAdmissionSupport.Recoverable, ToolContinuationRequirement = AgentToolContinuationRequirement.None,
            Segments = [restored.Segments[0] with {
                CheckpointKind = AgentToolCheckpointKind.ProviderRestart, OriginDispatchLeaseId = null, RestartCheckpoint = Envelope()
            }]
        };
        providerJournal.Validate();
        var failure = Assert.Throws<AgentToolAdmissionException>(() => MafToolRunContext.RequiresApprovalContinuationContract(providerJournal));
        Assert.Equal("tool-admission.runtime-denied", failure.Code);
        Assert.Contains("owner receipt", failure.Message);
    }

    [Fact]
    public void Owner_receipt_confirmation_preserves_an_existing_unknown_result_and_disclosure() {
        var journal = Create();
        journal = SetProposal(journal, Proposal(journal) with {
            State = AgentToolProposalState.ReconciliationRequired, Result = Envelope("real-sdk-unknown"),
            DisclosureEvidence = Envelope("original-disclosure")
        });
        var confirmed = AgentToolJournalTransitions.Replace(journal, Binding(journal), Confirm);
        Assert.Equal(Proposal(journal).Result, Proposal(confirmed).Result);
        Assert.Equal(Proposal(journal).DisclosureEvidence, Proposal(confirmed).DisclosureEvidence);
        foreach (var changed in new[] {
            Confirm(Proposal(journal)) with { Result = null, DisclosureEvidence = null },
            Confirm(Proposal(journal)) with { Result = Envelope("fabricated-success") },
            Confirm(Proposal(journal)) with { DisclosureEvidence = null },
            Confirm(Proposal(journal)) with { DispatchClaimId = Guid.NewGuid() }
        }) {
            Assert.Throws<AgentToolAdmissionException>(() => AgentToolJournalTransitions.Replace(journal, Binding(journal), _ => changed));
        }
    }

    [Fact]
    public void Owner_receipt_confirmation_requires_original_approved_dispatched_owner_receipt_effect() {
        var journal = Create();
        var confirmed = Confirm(Proposal(journal));
        var payload = confirmed.Payload;
        foreach (var invalid in new[] {
            confirmed with { ApprovalStatus = ExecutionApprovalStatus.Pending, ApprovedDigest = null },
            confirmed with { ApprovedDigest = Envelope("different").Digest },
            confirmed with { DispatchClaimId = null },
            confirmed with { DispatchClaimId = Guid.Empty },
            confirmed with { EffectState = AgentToolEffectState.Unknown },
            confirmed with { OwnerReceipt = null },
            confirmed with { OwnerReceipt = new(new("", "target"), Envelope()) },
            confirmed with { OwnerReceipt = new(new("owner", ""), Envelope()) },
            confirmed with { OwnerReceipt = new(new(" owner", "target"), Envelope()) },
            confirmed with { OwnerReceipt = new(new("owner", "target\n"), Envelope()) },
            confirmed with { Payload = new(payload.ToolName, 1, payload.Digest, "{}", AgentToolProposalEffect.Read, AgentToolProposalRecovery.OwnerReceipt) },
            confirmed with { Payload = new(payload.ToolName, 1, payload.Digest, "{}", AgentToolProposalEffect.Mutation, AgentToolProposalRecovery.ReconcileBeforeRetry) },
            confirmed with { ProviderCall = new("native", payload.Digest, Envelope()) },
            confirmed with { Cancellation = new(AgentToolCancellationDisposition.ReceiptCommitted, AgentToolCancellationReason.ReceiptFound,
                confirmed.OwnerReceipt!.Effect, confirmed.OwnerReceipt.Receipt) }
        }) {
            Assert.Throws<InvalidDataException>(() => SetProposal(journal, invalid).Validate());
        }
        Assert.Throws<InvalidDataException>(() => SetProposal(journal, Proposal(journal) with { OwnerReceipt = confirmed.OwnerReceipt }).Validate());
        var unapproved = SetProposal(journal, Proposal(journal) with { ApprovalStatus = ExecutionApprovalStatus.Pending, ApprovedDigest = null });
        Assert.Throws<AgentToolAdmissionException>(() => AgentToolJournalTransitions.Replace(unapproved, Binding(unapproved),
            proposal => Confirm(proposal) with { ApprovalStatus = ExecutionApprovalStatus.Approved, ApprovedDigest = proposal.Payload.Digest }));
    }

    [Fact]
    public void Owner_receipt_and_prior_result_are_immutable_after_confirmation() {
        var journal = Create();
        journal = SetProposal(journal, Proposal(journal) with {
            State = AgentToolProposalState.ReconciliationRequired, Result = Envelope("unknown"), DisclosureEvidence = Envelope("evidence")
        });
        var confirmed = AgentToolJournalTransitions.Replace(journal, Binding(journal), Confirm);
        var saved = Proposal(confirmed);
        foreach (var changed in new[] {
            saved with { OwnerReceipt = new(saved.OwnerReceipt!.Effect, Envelope("another-receipt")) },
            saved with { OwnerReceipt = new(new("owner", "another-target"), saved.OwnerReceipt!.Receipt) },
            saved with { Result = Envelope("another-result") },
            saved with { DisclosureEvidence = Envelope("another-disclosure") },
            saved with { DispatchClaimId = Guid.NewGuid() },
            saved with { State = AgentToolProposalState.Executing, OwnerReceipt = null }
        }) {
            Assert.Throws<AgentToolAdmissionException>(() => AgentToolJournalTransitions.ValidatePersistence(confirmed,
                SetProposal(confirmed with { Revision = confirmed.Revision + 1 }, changed)));
        }
        AgentToolJournalTransitions.ValidatePersistence(confirmed, confirmed with { Revision = confirmed.Revision + 1 });
    }

    [Fact]
    public void Effect_segment_kind_origin_response_source_and_binding_are_append_only() {
        var journal = Create();
        var proposal = Proposal(journal);
        foreach (var changed in new[] {
            journal with { Segments = [journal.Segments[0] with { OriginDispatchLeaseId = Guid.NewGuid() }] },
            journal with { Batches = [journal.Batches[0] with { Response = Envelope("new-response") }] },
            journal with { Batches = [journal.Batches[0] with { RequestDigest = Envelope("new-request").Digest }] },
            SetProposal(journal, proposal with { IntentId = new(Guid.NewGuid()) }),
            SetProposal(journal, proposal with { Payload = new(proposal.Payload.ToolName, 2, proposal.Payload.Digest,
                proposal.Payload.ArgumentsJson, proposal.Payload.Effect, proposal.Payload.Recovery, Envelope("changed-source")) })
        }) {
            Assert.Throws<AgentToolAdmissionException>(() => AgentToolJournalTransitions.ValidatePersistence(journal, changed with { Revision = 2 }));
        }
        var legacy = Create(1, effectsOnly: false) with { Batches = [], Segments = [], Support = AgentToolAdmissionSupport.RequestScopedInput };
        Assert.Throws<AgentToolAdmissionException>(() => AgentToolJournalTransitions.ValidatePersistence(legacy,
            legacy with { Revision = 2, SchemaVersion = 5, Support = AgentToolAdmissionSupport.EffectJournalOnly }));
    }

    [Fact]
    public void Receipt_confirmation_cannot_resurrect_rejected_completed_or_cancelled_proposals() {
        foreach (var state in new[] { AgentToolProposalState.Prepared, AgentToolProposalState.Rejected,
            AgentToolProposalState.Completed, AgentToolProposalState.Cancelled }) {
            var journal = Create();
            var proposal = Proposal(journal) with {
                State = state,
                Result = state == AgentToolProposalState.Completed ? Envelope("completed") : null,
                Cancellation = state == AgentToolProposalState.Cancelled
                    ? new(AgentToolCancellationDisposition.CancelledUnreconciled, AgentToolCancellationReason.ReceiptNotObserved) : null
            };
            journal = SetProposal(journal, proposal);
            journal.Validate();
            Assert.Throws<AgentToolAdmissionException>(() => AgentToolJournalTransitions.Replace(journal, Binding(journal),
                prior => Confirm(prior) with { Cancellation = null }));
        }
    }

    [Fact]
    public void Missing_restart_checkpoint_does_not_bypass_whole_journal_bounds() {
        var journal = Create();
        var receipt = Envelope(new string('x', AgentToolProtocolEnvelope.MaximumUtf8Bytes));
        var proposal = Confirm(Proposal(journal)) with { OwnerReceipt = new(new("owner", "target"), receipt) };
        var proposals = Enumerable.Range(0, 3).Select(index => proposal with {
            IntentId = new(Guid.NewGuid()), Ordinal = index, CallId = $"call-{index}"
        }).ToImmutableArray();
        Assert.Throws<InvalidDataException>(() => (journal with {
            Batches = [journal.Batches[0] with { Proposals = proposals }]
        }).Validate());
        var escaped = Confirm(Proposal(journal)) with {
            OwnerReceipt = new(new("owner", "target"), Envelope(new string('\u0001', 5 * 1024 * 1024)))
        };
        Assert.Throws<InvalidDataException>(() => SetProposal(journal, escaped).Validate());
    }

    [Fact]
    public void Schema_five_provider_dispatches_keep_native_uncertainty_and_response_fences() {
        var journal = Create();
        var firstId = new AgentToolProviderDispatchId(Guid.NewGuid());
        var activeId = new AgentToolProviderDispatchId(Guid.NewGuid());
        var proposal = Proposal(journal) with {
            RequiresApproval = true, DispatchClaimId = activeId.Value,
            Payload = new("native-tool", 1, Envelope().Digest, "{}", AgentToolProposalEffect.Mutation, AgentToolProposalRecovery.ReconcileBeforeRetry),
            ProviderCall = new("native-tool", Envelope().Digest, Envelope())
        };
        proposal = proposal with { ApprovedDigest = proposal.Payload.Digest };
        journal = SetProposal(journal, proposal);
        journal = journal with {
            Batches = [journal.Batches[0] with { ProviderDispatchId = firstId }],
            ProviderDispatches = [
                new(firstId, 0, journal.Segments[0].Id, journal.Batches[0].RequestDigest, Envelope().Digest, [],
                    AgentToolProviderDispatchState.ResponseAdmitted, journal.Batches[0].Id),
                new(activeId, 1, journal.Segments[0].Id, Envelope("next").Digest, Envelope().Digest, [Binding(journal)],
                    AgentToolProviderDispatchState.Started)
            ]
        };
        journal.Validate();
        Assert.True(journal.HasUnresolvedProviderDispatch);
        Assert.True(journal.HasUnresolvedEffects);
        Assert.Throws<InvalidDataException>(() => SetProposal(journal, proposal with { DispatchClaimId = Guid.NewGuid() }).Validate());
        Assert.Throws<InvalidDataException>(() => (journal with {
            ProviderDispatches = journal.ProviderDispatches.SetItem(1, journal.ProviderDispatches[1] with {
                State = AgentToolProviderDispatchState.ResponseAdmitted, ResponseBatchId = new(Guid.NewGuid())
            })
        }).Validate());
        var response = new AgentToolBatchRecord(new(Guid.NewGuid()), 1, journal.ProviderDispatches[1].RequestDigest, Envelope("response"), [], activeId);
        var completed = SetProposal(journal, proposal with { State = AgentToolProposalState.Completed, Result = Envelope("native-result") });
        completed = completed with {
            Revision = 2, Batches = completed.Batches.Add(response),
            ProviderDispatches = completed.ProviderDispatches.SetItem(1, completed.ProviderDispatches[1] with {
                State = AgentToolProviderDispatchState.ResponseAdmitted, ResponseBatchId = response.Id
            })
        };
        AgentToolJournalTransitions.ValidatePersistence(journal, completed);
        Assert.False(completed.HasUnresolvedEffects);
    }

    private static AgentToolJournalRecord Create(int schema = 5, bool effectsOnly = true) {
        var background = schema == 2
            ? new AgentToolBackgroundSourceBinding("fixture-process", "step", Envelope().Digest, Envelope("execution").Digest)
            : null;
        var session = new AgentToolSessionReference(Guid.NewGuid(), background is null ? Guid.NewGuid() : Guid.Empty,
            background is null ? AgentExecutionAuthorityId.Create() : default, background);
        var source = Envelope("original-source");
        var payload = new AgentToolPreparedPayload("fixture-write", 2, Envelope().Digest, "{}",
            AgentToolProposalEffect.Mutation, AgentToolProposalRecovery.OwnerReceipt, source);
        var lease = Guid.NewGuid();
        var proposal = new AgentToolProposalRecord(new(Guid.NewGuid()), 0, "original-call", payload, false,
            AgentToolProposalState.Executing, ExecutionApprovalStatus.Approved, payload.Digest, DispatchClaimId: Guid.NewGuid());
        var segment = new AgentToolInvocationSegment(Guid.NewGuid(), 0, 0, effectsOnly ? null : Envelope(), PendingApprovals: [],
            CheckpointKind: effectsOnly ? AgentToolCheckpointKind.EffectsOnly : AgentToolCheckpointKind.ProviderRestart,
            OriginDispatchLeaseId: effectsOnly ? lease : null);
        var batch = new AgentToolBatchRecord(new(Guid.NewGuid()), 0, Envelope().Digest, Envelope(), [proposal]);
        var journal = new AgentToolJournalRecord(schema, 1,
            new(session, Guid.NewGuid(), background is null ? AgentRuntimeContextPurpose.InteractiveChat : AgentRuntimeContextPurpose.GovernedProcessAutomation,
                new(Guid.NewGuid(), "fixture-profile", new(1))), [segment], [batch], lease,
            effectsOnly ? AgentToolAdmissionSupport.EffectJournalOnly : AgentToolAdmissionSupport.Recoverable,
            RuntimeContext: schema == 4 ? TypedContext() : null,
            BackgroundInput: background is null ? null : new("Original background input"));
        if (schema == 3) {
            var dispatch = new AgentToolProviderDispatchId(Guid.NewGuid());
            journal = journal with {
                Batches = [batch with { ProviderDispatchId = dispatch }],
                ProviderDispatches = [new(dispatch, 0, segment.Id, batch.RequestDigest, Envelope().Digest, [],
                    AgentToolProviderDispatchState.ResponseAdmitted, batch.Id)]
            };
        }
        return journal;
    }

    private static AgentToolAdmittedRuntimeContext TypedContext() => new("Original context", WorkspaceScopeDescriptor.Sandbox, [
        new(AgentChatContextScopeId.Create(), new(new("fixture"), new("source")), WorkspaceScopeDescriptor.Sandbox,
            new("fixture"), new("fixture-kind"), new(1), new("content"), new("coverage"), new(1), new("fresh"),
            DateTimeOffset.UnixEpoch, null, Envelope())
    ]);

    private static AgentToolProtocolEnvelope Envelope(string payload = "{}")
        => AgentToolProtocolEnvelope.Create("fixture-protocol", 1, payload);

    private static AgentToolProposalRecord Proposal(AgentToolJournalRecord journal) => journal.Batches[0].Proposals[0];

    private static AgentToolApprovalBinding Binding(AgentToolJournalRecord journal) => new(journal.Batches[0].Id,
        Proposal(journal).IntentId, Proposal(journal).Payload.SemanticVersion, Proposal(journal).Payload.Digest);

    private static AgentToolProposalRecord Confirm(AgentToolProposalRecord proposal) => proposal with {
        State = AgentToolProposalState.OwnerReceiptConfirmed, EffectState = AgentToolEffectState.Committed,
        OwnerReceipt = new(new("fixture-owner", "original-target"), Envelope("owner-receipt"))
    };

    private static AgentToolJournalRecord SetProposal(AgentToolJournalRecord journal, AgentToolProposalRecord proposal)
        => journal with { Batches = journal.Batches.SetItem(0, journal.Batches[0] with { Proposals = [proposal] }) };
}
