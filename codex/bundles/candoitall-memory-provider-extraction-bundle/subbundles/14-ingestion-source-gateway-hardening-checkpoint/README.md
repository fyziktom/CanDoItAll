# 14 Ingestion Source Gateway Hardening Checkpoint

## Status

- `Completed`

## Objective

- Harden Source Gateway adapters, redaction, provenance, permission failures, and manual ingestion before MAF can depend on them.

## Success Criteria

- The subbundle outcome is implemented behind the intended boundary and does not leak downstream responsibilities.
- Positive and negative proof exercise production code paths, not only hand-built DTOs or stubs.
- Downstream phases can rely on the produced contracts/runtime behavior without guessing or compensating for missing seams.

## Covered Inputs

- R07
- R08
- R20

## Prerequisites

- SB11-SB13 completed

## Exact Source References

- `bundle://plan/02-checkpoints.md`
- `bundle://requirements/03-non-negotiable-boundaries.md`
- `bundle://inventories/02-dependency-and-removal-inventory.md`
- `bundle://analysis/04-live-repo-reentry-alignment.md`
- `bundle://requirements/01-normalized-requirements.md`
- `bundle://plan/01-phase-plan.md`

## Deliverables

- Audit SB11-SB13 for direct DbContext/entity leakage, missing redaction, weak provenance, duplicated snapshot mappers, and inconsistent manual ingestion flows.
- Extract common source snapshot builders and redaction helpers while keeping module-specific mapping in adapter-owned files.
- Add architecture guards preventing provider projects from referencing source module projects directly.
- Add fail-closed tests for missing adapter, missing permission, unsupported source kind, stale record, and redaction failure.
- Update Source Gateway docs and reopen the owning adapter subbundle when a gap cannot be fixed inside the checkpoint.
- Add an explicit duplicate-contract audit for `MemorySourceSnapshot*` and any newly introduced source snapshot DTOs.

## Dependency Impact

- Blocks MAF integration if providers can bypass the gateway or receive EF entities.

## Validation Depth

- `Critical checkpoint`

## Implementation Steps

1. Run a source audit for `AppDbContext`, EF entity types, and module project references from provider driver projects.
2. Inspect snapshot mapper files for duplicated policy/redaction logic and extract shared helpers where appropriate.
3. Strengthen tests that only verify snapshot creation so they also check provenance, sensitivity, and denied-field behavior.
4. Verify manual ingestion and provider-requested source ingestion share operation/source ledger paths.
5. Record checkpoint result and block SB15 if any provider can bypass Source Gateway.
6. Block SB15 if Workbench, workflow/process, CRM/resource, or manual adapters do not share the same source snapshot contract family.

## Scope Exceptions

- No known scope exceptions for this subbundle at preparation time.
- If implementation discovers an exception, document it in `reviews/01-execution-report.md` and stop before downstream work if the exception affects a phase gate.

## Do Not Do

- Do not implement downstream subbundles early.
- Do not introduce direct generic-memory or MAF references to native Cognitive Memory implementation types.
- Do not add Qdrant as a base runtime dependency.
- Do not expose host EF entities or DbContext instances to memory providers.
- Do not duplicate memory operation dispatch logic outside the shared handler.

## Acceptance Checklist

- The implemented surface is observable through focused tests or explicit proof artifacts.
- Dependency boundaries from `requirements/03-non-negotiable-boundaries.md` remain intact.
- No downstream subbundle work is silently implemented or assumed.
- Execution report is updated with proof paths, command transcripts, and gate result.
- No provider-facing path can access source module data without Source Gateway and policy evaluation.
- All source snapshots include provenance, source ids, sensitivity, redaction status, and adapter identity.
- The source gateway is stable enough for MAF and UI consumers to use without knowing source module internals.
- Semantic proof would fail against a stub, renamed old implementation, in-memory-only shortcut, or test-only manually seeded signal.

## Proof Required

- Create `proof/SB14/manifest.md` with changed-file hashes, failing-first transcript, passing transcript, source assertions, and anti-stub audit output.
- Create `proof/SB14/semantic-invariants.md` covering raw-note closure, shipped behavior, shallow-pass trap, adversarial negative proof, semantic positive proof, and downstream dependency check.
- Add a `Production Behavior Artifact Matrix` in `proof/SB14/manifest.md` and `proof/SB14/semantic-invariants.md` for every new state, event, ledger record, worker signal, or provider-visible behavior introduced here.
- Run `dotnet build CanDoItAll.slnx` unless the subbundle README documents a narrower build gate with justification.
- Run focused unit tests, integration tests, or architecture guard tests that directly exercise this subbundle, not only broad happy-path smoke tests.
- Capture dependency audit and source-leak audit output.
- Run source adapter negative tests and record explicit checkpoint closure or reopened subbundles.
- Capture duplicate source snapshot contract audit output and the migration decision for the existing `MemorySourceSnapshot*` contracts.

## Browser Validation Logging

- N/A. This subbundle has no browser-visible surface. Record N/A in the execution report unless implementation touches a host-visible or browser-visible surface.

## Progression Gate

- Downstream subbundles may start only after SB14 proof is recorded, the acceptance checklist passes, and no phase-gate blocker remains.

## Suggested Agent Prompt

```text
Implement subbundle SB14 only. Start by reading this README and the Exact Source References. Preserve the generic memory boundaries, avoid downstream work, capture the required proof, update reviews/01-execution-report.md, and stop if the progression gate cannot pass honestly.
```
