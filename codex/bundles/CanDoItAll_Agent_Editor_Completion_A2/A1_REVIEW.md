# A1 implementation review

## Scope and provenance

Implementation: `ed64d4edf868cb26c749c31a94ff918683e0f4a0`.
Observed branch HEAD: `0aad5360b4ac037ed4471ed44fc6083b7f09fb85`, immediate child containing only
`codex/bundles/CanDoItAll_Agent_Editor_Core_UI_Decoupling/*`. The archive commit is intentionally
retained history, not product work to rerun. See `review-provenance.json`.

The review read the A1 record, source contract/surface/host, shared identity control, native
round-trip test, scenario implementation, Projects operation fence and actual remaining sections.
R01–R09 and R14–R17 are the principal evidence. No product build/test was executed by the reviewer.

## Preserve these changes

* Actual core rendering is in Editor.UI. Host session, commands, provider policy and six still-
  deferred sections remain distinct. Safe provider/effort projections avoid the broad MAF component
  dependency. The section fragments use the current presentation at execution, without recreating
  the whole tab/form for each metadata change.
* Whole-agent request capture and native optimistic update remain. The native A1 test preserves
  project lifetime bindings, roots, storage, Memory, capabilities, secret references, hidden flags,
  Favorite and unknown JSON for both an agent and a template, while checking an unrelated agent.
* The shared identity control's new Immediate option defaults off for other consumers; A1 opts in.
  Empty/whitespace name admission is guarded in the actual editor Save lane.
* The former Files stale error now checks both activation and operation ownership. Old cleanup and
  cancellation remain operation-owned. Do not replace this with a queue or generic catch suppression.
* Published Components repair is an existing dependency, not new work or an unpublished blocker.

No new critical defect attributable to the selected A1 extraction was established by this source
review. That is not a runtime certification or proof that all legacy editor paths are correct.

## Bounded carry-over to repair

A2-R1 is an existing **verification versus save reconciliation** error in the remaining capability
integration. A successful Verify can discard edits already present when Verify began. It is not an
unconfirmed model-provider issue, and no wrong backend write is established. The direct control flow
and native version semantics are detailed in [S0](S0_VERIFICATION_RECONCILIATION.md). Repair it before
lifting the capability section, then carry the control through the final extracted renderer.

Do not accumulate unrelated speculative findings. The source review identifies additional A2 risk
paths (per-read generations, missing references, Memory load ownership, wizard create/assign stages)
as characterization requirements, not as independently reproduced product failures.

## Disposition

Preserve A1 and Projects/Workspace boundaries. Proceed with the single A2 continuation after S0.
No provider-admin, Workflow-canvas or full application release claim follows from this review.
