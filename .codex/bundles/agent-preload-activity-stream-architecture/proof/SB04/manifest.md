# SB04 Behavioral Proof Manifest

## Identity

- Subbundle: `SB04 Module Runtime Context Snapshot Adapters`
- Status: `Complete — A4 Pass`
- Date: `2026-07-27`
- Owned requirements: R05, R06, and the module half of R08.
- Raw-note ownership: use already-loaded Project Structure and Process Manager state
  first; snapshots must have explicit lifetime/update policy and must never overwrite
  canonical truth.
- Decision: `bundle://proof/SB04/a4-decision.md`
- Upstream gate: `bundle://proof/SB03/a3-decision.md`
- Downstream gate: `bundle://proof/SB05/a5-decision.md`

## Shipped behavior

- Workbench maps the already-held project surface into a bounded, redacted,
  defensively immutable `ProjectStructureInvocationSnapshot`.
- A covered `InvocationSnapshot` project read enforces exact attachment identity,
  profile, freshness, and coverage and performs zero canonical reads. Coverage miss,
  expiry, profile mismatch, and unavailable context fail explicitly.
  `CanonicalCurrent` is a separate explicit source and performs the deeper read.
- Processes maps held Workspace and Live surfaces into immutable
  `ProcessInvocationSnapshot` values carrying typed present/absent provenance,
  freshness, and coverage components rather than one aggregate maximum timestamp.
- Core transports contributor-owned attachment types opaquely through the existing
  registry. It assigns monotonic publication revisions under the registry lock, binds
  publication/content/coverage/profile/freshness identity into the context digest, and
  keeps the exact captured envelope for the invocation.
- Snapshot contracts are read-only attachments. Project mutations still accept their
  canonical command inputs and services; no mutation or completion contract accepts a
  snapshot payload or stamp.

## Behavioral evidence

| Behavior | Positive/negative proof | Result |
| --- | --- | --- |
| Pure, bounded project mapping | `Mapper_defensively_copies_and_redacts_the_held_surface_without_deeper_fields`; `Mapper_bounds_large_surfaces_and_preserves_selected_exact_nodes` | Pass |
| Independent identity stamps | `Mapper_fingerprints_are_deterministic_and_independent_by_responsibility`; `Digest_changes_for_each_attachment_identity_stamp_independently` | Pass |
| Explicit freshness/profile policy | `Invocation_rejects_the_context_at_its_attachment_deadline`; `Invocation_rejects_an_attachment_from_another_profile_generation`; component expiry tests perform no implicit read | Pass |
| Zero-I/O covered dispatch | `Eligible_exact_snapshot_read_performs_zero_canonical_reads`; coverage/freshness failures perform zero reads; `Canonical_current_performs_exactly_one_canonical_read` | Pass |
| Exact old-or-new invocation capture | `Concurrent_captures_observe_only_complete_old_or_new_publications`; `Invocation_keeps_the_exact_captured_attachment_after_registry_update` | Pass |
| Opaque typed attachments | `Atomic_publication_round_trips_multiple_opaque_attachment_types`; exact-type mismatch and duplicate-type negative tests | Pass |
| Process provenance vector | `Every_process_context_field_has_an_explicit_provenance_component`; `Changing_each_source_component_changes_only_that_vector_component`; present/absent invariant tests | Pass |
| Workspace/Live parity | `Workspace_and_live_surfaces_copy_the_same_held_runtime_snapshot`; Manager chat sends only the user prompt and consumes the published runtime snapshot | Pass |
| Approval integrity | `Approval_context_must_match_the_run_digest_and_is_unavailable_after_release` | Pass |
| HTTP boundary | `Structure_read_enforces_http_source_policy` | Pass |

The current parent validation handoff reports the focused architecture unit suite at
140/140. The selected downstream component suite passed 95/95 and includes Project
Structure context publication and Process Manager snapshot consumption. Their original
140/140 command stream was not retained in this proof directory, so this manifest does
not present a reconstructed transcript.

## Semantic adequacy

- Shallow-pass trap: a context fragment containing selected IDs while the tool reloads
  current storage would appear populated but would observe newer state and hide
  coverage/freshness failures.
- Adversarial negative: concurrent publication tests accept only one complete old or
  new publication; snapshot coverage/expiry/profile mismatch returns a typed failure
  with zero canonical reads.
- Semantic positive: an eligible exact project read returns the captured held-surface
  nodes from the invocation with zero persistence calls; Process Workspace and Live
  surfaces copy the same held runtime state with explicit provenance.
- Anti-stub result: the positive path is implemented by production mappers, registry
  envelopes, digest calculation, runtime-tool context, and read dispatcher. It is not a
  fixture-only branch, object bag, string-key lookup, `TODO`, or
  `NotImplementedException`.

## Production behavior artifact matrix

| Artifact | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| Project snapshot envelope | `ProjectStructureAgentChatContextProvider.razor` and `ProjectStructureInvocationSnapshotMapper` | `AgentChatContextRegistry`, invocation factory, runtime-tool provider, snapshot read dispatcher | atomic contributor publication, invocation capture, freshness deadline | coverage miss, expiry/profile mismatch, source-policy, concurrent old/new tests |
| Process snapshot envelope | `ProcessWorkspaceShell.razor`, `LiveProcessesDashboard.razor`, and `ProcessInvocationSnapshotMapper` | floating/Manager context and exact invocation attachment | held projection publication with freshness lease and typed provenance | non-present provenance suppresses residual data; expired context performs no implicit projection read |
| Context digest/approval lease | `AgentChatContextDigest` and invocation factory | execution/approval continuation | captured run digest retained only for the run and released afterward | independent stamp changes alter digest; mismatched/released approval context rejects |

## Source and test surfaces

Production:

- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Models/Conversations/FloatingAgentChatModels.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Context/AgentChatContextRegistry.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Context/AgentChatContextDigest.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Context/AgentChatContextInvocationFactory.cs`
- `repo://src/MAF/Tools/CanDoItAll.AgentFramework.Tooling/AgentRuntimeToolProviderContext.cs`
- `repo://src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureInvocationSnapshot.cs`
- `repo://src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureInvocationSnapshotReadDispatcher.cs`
- `repo://src/Modules/CanDoItAll.Modules.Workbench/AgentTools/ProjectStructureAgentRuntimeToolProvider.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/AgentChat/ProcessInvocationSnapshot.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/AgentChat/ProcessAgentChatFreshnessLease.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceShell.razor`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Components/LiveProcessesDashboard.razor`

Tests:

- `repo://tests/Unit/CanDoItAll.Tests.Unit/ProjectStructureInvocationSnapshotTests.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessInvocationSnapshotTests.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessWorkspaceProvenanceContractTests.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/AgentChatContextAttachmentFreshnessPolicyTests.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/FloatingAgentChatArchitectureTests.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/MafAgentRuntimeToolProviderCompositionTests.cs`
- `repo://tests/Components/CanDoItAll.Tests.Components/ProjectStructureAgentChatContextProviderTests.cs`
- `repo://tests/Components/CanDoItAll.Tests.Components/ProcessWorkspaceShellTests.cs`
- `repo://tests/Integration/CanDoItAll.Tests.Integration/ProjectStructureReadHttpBoundaryTests.cs`

## Scope exceptions and residual risks

- Process-launch preview/execute drift and the incomplete `processContext` navigation
  handoff remain outside this chat-preload initiative.
- Deep facts outside declared coverage require the caller to choose
  `CanonicalCurrent`; no fallback is automatic.
- Publication stamps are not canonical mutation concurrency tokens. A future
  Project Structure row-version contract must be shared by UI and agent writers rather
  than reusing an invocation snapshot.
- A5 retains three P2 follow-ups: synchronous database-switch subscriber delay,
  physical WAL power-loss durability, and the final provider cross-host revision
  window. None weakens A4 source, freshness, coverage, redaction, or no-write-back
  rules.

## Architecture and downstream disposition

CodeAnalytics snapshot `snap-20260728014834-63e19a8b` reports the affected project
graph as acyclic. Concrete module snapshots remain in Workbench/Processes; Core owns
only the typed opaque transport. No lower layer depends on a module implementation.
A5 returned `GO with three P2 follow-ups`, so the A4 foundation is trusted for
downstream UI/runtime use.

## Closure

A4 is `PASS`; SB05 progression was authorized. Reopen A4 and downstream gates on a
mixed publication, unauthorized or unredacted detail, invocation drift to newer UI
state, hidden persistence fallback, attachment-to-mutation write-back, or an emitted
process field without an explicit provenance component.
