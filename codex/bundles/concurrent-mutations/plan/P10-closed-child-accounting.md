# P10: Recover Process accounting for a completed Agent child

Status: implemented and qualified by [134 passing cases](../validation/2026-10-21-closed-child-accounting.md), including 55 new correctness cases and three existing diagnostic profiles. The initial parser assertion failure and test-only correction remain retained. Finite latency and sustained capacity remain unqualified and block producer activation; this accounting action does not establish a performance guarantee.

## Problem and scope

A manager may lose the original approval request after the Agent has completed its saved effect, while the Process outcome and work accounting remain pending. The retained v1 approval digest cannot reconstruct that request. Recreating an approval would risk a second effect. Introduce a separate, explicit accounting action over a verified closed original child. It performs no Agent mutation, provider invocation or business publication.

Root cancellation/terminal reconciliation and shared-snapshot refresh remain different transitions. This action preserves the existing waiting-review admission and cannot reopen a terminal/cancelling parent. Producer activation stays gated until those transitions and capacity are qualified.

## Responsibility and dependencies

Workbench already owns the product-specific Process/Agent bridge. Add one small ProjectTaskPlanProcessReconciliationService there, composed with the narrow AgentInheritedPlanningRunReader, current runtime store, existing ProcessDecisionReviewCoordinator and current manager/source/profile checks. It must not depend on the full Agent workspace, inherited execution service, runtime factory or saved-effect dispatcher.

Extract the duplicated profile-before, current-manager, current-source, profile-after authority window from the current reader and review service into an internal overload on ProcessPlanningManagerAuthority. This is an existing cohesive boundary, not a new trivial interface. Preserve ordering and cancellation behavior. The two existing owners shrink; no new partial or project is introduced. No project reference changes are needed. Web maps typed requests/results and uses the same permission boundary.

CodeAnalytics baseline snap-20261021211104-88466e8e covers Workbench: 402 documents, 1167 types, 10124 members, 2192 edges, 402 findings and 73 diagnostics. One module and five type cycles already exist in this scoped graph. Inspect actual project references as well; scope excludes external edges. Existing selected findings concern codec/authority/participant member counts and the review data record. Direct builds remain the compilation authority.

## Admission, recovery and atomicity

The accounting request has the exact displayed AgentPlanningDecisionExpectation and a ProcessDecisionReviewRequest with a retained action/completion ID. Validate exact child address and nonempty identity. Read through the narrow authorized child reader, resolve the original parent using the existing schema-7 codec, and require the displayed Agent expectation. Reuse ProjectTaskPlanProcessOutcome.Read to classify closed work/effects; pending, unknown or inconsistent work cannot become a settlement.

Authorize current manager/source/profile before entering Process completion and again before returning data. Call the existing CompleteAsync; its registered child-outcome reader verifies actual durable original outcome and its transaction atomically writes runtime result, decision closure and work settlement. Never weaken its fences or replace its original receipt identity. It checks original accounting receipts before current phase admission, so the same Process action can recover after acknowledgement loss. Reload current runtime after completion; do not return a historical sibling snapshot.

The displayed Agent expectation is an admission fence. A changed Agent run requires a fresh read; clients must retain the original accounting request for uncertain-response recovery. The Process receipt binds child, decision, budget, strategy, waiting result, completion ID, original accepted material and actor. A different actor or action cannot adopt that receipt; concurrent new actions compete on the existing revision fences and cannot settle twice. Test the boundary explicitly.

## Read model and HTTP

Keep the existing approval expectation limited to AwaitingChoice. Add a separate accounting expectation only when the current original Agent child is verifiably closed and Process observation still has a pending review. A settled or cancelling parent has no new accounting affordance. Existing review choices and retained approval bytes stay unchanged.

Add POST /review/accounting under the same exact Process/run/step/child route. Require existing Process, Agent and Project grants. Use a separate typed submission and compact result: action, child identity/state/revision, Process completion, refusal codes and runtime-reload requirement. A refused action does not claim that accounting is still pending; the caller reloads current state, which may already have been settled by another action. Do not label it an Agent approval replay. Revisions remain canonical Int64 strings.

Extract the existing bounded JSON read into a feature-local helper and reuse it for both writes: 24 KiB including unknown-length bodies, depth 16, unique properties, strict case, required constructor values and no unknown members. Preserve content-type/size/status mappings, no-store responses and explicit uncertain-result errors. No UI or SPA merge in this slice.

## Testability and validation

Isolated tests exercise the shared authority window using current-access/profile/generation and lease fakes, with denial, cancellation and profile changes across awaits. Pure request/parser tests cover route identity, canonical revisions and malformed/oversized input without a full runtime.

Real SQL/file integration must prove both snapshot modes and approved/rejected closed outcomes after loss of the original approval request, exact Process acknowledgement recovery, competing action CAS, unchanged Agent files/provider count/Project effects, pending/unknown work refusal, stale Agent and Process expectations, current grants/profile changes, cancelled-parent refusal and fresh sibling runtime on replay. Actual HTTP tests prove affordance, authorization, strict transport and restart recovery using registered services.

Build changed Workbench/Web projects directly and refresh Unit/Integration in standard Release, serially. State and verify exact discovery before execution. Include the existing review and narrow-reader regressions, full portability scan/review/final enforcement, rendering register, docs, architecture comparison, frozen source/runtime identity and owned-resource reconciliation. Reuse the existing isolated PostgreSQL fixture and retain failed attempts. Capacity remains a separate measured gate.

The final selection confirms all 55 new cases, including explicit renewed-credential accounting replay and different-actor refusal after settlement. Built discovery and the complete passing run are retained in the validation record.


## Continued latency diagnosis without weakened checks

P09 G measures seed/normalization at 58â€“105 ms, so leave its behavior unchanged.
Add bounded existing-ActivitySource spans around the API review handler, the product
review reader, current manager/source observations, and test-only scoped reader
construction. The last probe wraps the actual registered implementation type using
ActivatorUtilities; it does not substitute domain behavior or execution. This distinguishes
DI cost from owner work and current authorization. No paths, payloads or credentials
are recorded. Reuse the existing bounded three-case tracing tests after the accounting
correctness cases. These traces do not qualify finite capacity or alter its one-second
limit. The proposed selection is 131 correctness cases (55 new) plus 3 existing profiles;
actual build-backed discovery must confirm 134 before execution.


## Entry decision

Pass for implementation and Governed affected-scope proof. The retained original-child
and atomic Process completion/receipt prerequisites remain trusted. P09 capacity is a
separate open activation gate and is not used as evidence for accounting correctness.
No new project references, schema change, producer activation or UI surface is planned.
Run the 134-case selection (131 correctness, 3 diagnostic), directly build Workbench/Web,
refresh Unit/Integration, and enforce full portability/rendering/docs. New results must
confirm the proposed counts; failed attempts remain visible. A broad Stable run is
reserved for final frozen activation/merge closure after affected gates pass.
