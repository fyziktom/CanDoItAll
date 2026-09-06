# Mutation and owned-effect architecture

## Accepted seam and observed defect

The panel delegates reads to its per-instance session through IAgentCapabilitiesReads. The surface receives frozen collections, presentation selection/load state and preview results; it emits typed intents and keeps search/tags/filter/tree/access-rule draft. The page parameter/callback contract stays unchanged. Reads cancel on replacement/disposal and ignore stale success/failure. Effects remain in the host with generation fences, without complete operation cancellation/recovery.

Assignment currently edits Draft.SelectedCapabilityIds before Save; known rejection leaves an uncommitted attachment visible. Existing AgentEditorDraftPolicy.Copy already deep-copies the editor and has a public completeness guard. Prefer that pure copy utility with a bounded immutable assignment request. Do not call Capture merely for copying: it also normalizes providers/image/project access. Never pass the live session editor into asynchronous work. Preserve unrelated fields and ExpectedUpdatedAtUtc.

## Actual persistence and effect boundaries

Assignment: current-profile workspace SaveAgentAsync -> Core catalog SaveAgentAsync -> ISandboxWorkspaceStore.UpdateCatalogAsync. Core checks the exact agent's ExpectedUpdatedAtUtc inside the catalog update callback and wraps expected definition validation with AgentEditorValidationException. The registered production store is FileSandboxWorkspaceStore under the active database-profile workspace. It takes an in-process gate and cross-process lock, writes the catalog, then writes a workspace index. This is not the provider registry database transaction. Catalog replacement may precede index failure. SB00 must trace SaveCatalogCoreAsync, JSON/atomic-write implementation, cancellation positions and index recovery in the real registered store.

Only after Core returns does the current-profile wrapper synchronize CRM/HR projection and reference-data invalidation. AgentDirectoryProjectionSynchronizationException proves the agent ID already committed. Existing AgentEditorSaveOutcome is relevant to this same save path, but using the broad editor command interface would drag storage normalization, Delete and unrelated reconciliation into assignment. Prefer one narrow capability mutation boundary justified by responsibility and direct production-adapter tests. Reuse common mapping only where semantics match; no interface per effect or provider-style service bag.

Verification reads agent/capability/provider, calls CapabilityProofService.VerifyAsync, then updates agent/catalog proof and timestamps. Inline skill proof is local; tool/plugin/endpoint/MCP branches may have external effects. Read every reachable implementation before calling verification idempotent or owner-cancellable. A performed diagnostic and persisted proof are different outcomes: cancellation before persistence cannot undo the diagnostic. Reconciliation must not replay verification. Use harmless inline skills or controlled loopback adapters in browser proof, never arbitrary external tool execution.

Access preview evaluates a captured selected-capability snapshot against one typed local rule, translated to transport DTO only in the host. Details/setup can persist capabilities or perform diagnostics. Audit every direct nested dialog open: cancelling one reference does not close descendants opened without its token. Curator chat creation has its own commit boundary. Preserve exact ID + TemplateKey + active/non-template/tool permissions and real backend authorization.

## Ownership and decisions

| Responsibility | Owner / lifetime | Public seam |
|---|---|---|
| Requested identity | Existing page contract | Parent echo/missing target |
| Accepted target and catalog/editor reads | Existing per-instance session | Existing read tests |
| Assignment submission/outcome/reconciliation | Target/attempt state in session or focused operation helper | Immutable request + real adapter |
| Dispatch/notifications | Existing host | Actual UI intents/dialogs |
| Preview draft/presentation | Existing controlled surface | No-service tests |
| Verification/preview/curator work | Captured operation generation and owner token | Delayed failure/cancel |
| Direct/nested overlays | Individual captured owner token | Real DialogHost + unrelated overlay |

Product decision: show the last authoritative attachment set with pending state until known commit. Known rejection preserves that state and later unrelated edits. Known commit updates identity/revision/reconciliation state before refresh. Unknown state retains exact target, expected pre-write revision, independent submitted attachment set and immutable request identity. Canonical reads may prove the submitted postcondition, unchanged precondition, or an ambiguous concurrent state. Never infer attempt identity from a name or list order; never silently unlock on target recreation. Reconciliation is read-only. Unprovable outcomes remain Unknown rather than introducing an outbox.

Allow one assignment/verification mutation at a time per target, with other targets isolated. Target change hides or supersedes UI work, never asserts rollback. Each operation owns its busy/result slot; old finally blocks cannot clear newer work. Preview can supersede preview but cannot replay writes. Preserve local search/raw rule text through reconciliation. Sanitize UI errors; log actionable non-secret target/operation classification.

A controller hierarchy would duplicate the proven session. Partial-class reshuffling would not create a test seam. A generic outbox/mutation framework is disproportionate. The narrow assignment request and existing read reconciliation are preferred, contingent on SB00's actual commit trace. Keep backend capability authority/approval and secret-binding rules.

## Future extraction blockers

AgentCapabilityList serves AgentDetailsDialog and the controlled surface in broad AgentFramework.Components. That project already references AgentFramework.UI; the reverse reference would cycle. A future move must move the real list and CSS, update both consumers, carry surface styles now attached through the host scope anchor, and retain BaseLib tree/cards/tag controls/tooltips and production Tailwind/theme/assets. Preview transport DTOs, mutable editor and effects stay outside the lightweight assembly; review the pure enum dependency explicitly.

Future specimens to inventory after SB02: loading/failed/missing/no-agents/no-capabilities; all supported kinds, attached/unattached/proof states; long names/tags; filter/reset/raw rule draft; exact curator available/unavailable; pending mutation; known warning/reconciliation and unknown state. These are proposed scenarios, not a sandbox implementation or performance claim. Do not prepare child 03 until the mutation/effect boundary is proven stable.
