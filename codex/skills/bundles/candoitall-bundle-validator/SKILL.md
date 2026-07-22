---
name: candoitall-bundle-validator
description: "Gate a CanDoItAll bundle for readiness, re-entry, or final closure by validating semantic role coverage, dependencies, proof tiers, raw-input closure, and current repo alignment. Use for canonical bundles and compatible legacy/external bundle shapes without forcing unnecessary structural migration."
---

# CanDoItAll Bundle Validator

Decide from evidence whether execution or closure may proceed. Structure supports the decision; structure is not the outcome.

## Gate Result

Return `Pass`, `Fail`, or `Blocked`, followed by:

- the evidence that determines the result;
- exact repairs required;
- downstream work that must wait or be rechecked.

## Compatibility First

Map the bundle to these semantic roles: inputs, requirements, current state, dependency plan, work units, status/proof, and closure.

- If the roles are recoverable, validate the existing shape.
- Run `scripts/validate_bundle.py` for canonical CanDoItAll bundles.
- For compatible legacy/external shapes, perform the same semantic checks manually and record why the structural script is not applicable.
- Fail structure only when missing or contradictory structure prevents execution, durable recovery, automation, or auditability.

## Readiness And Re-entry

Confirm:

- raw inputs and explicit constraints are preserved;
- each input maps to a requirement, owning work unit, planned proof, and closure path;
- current repository evidence supports source references and assumptions;
- dependency order, critical foundations, parallel-safe work, and reopen triggers are operational;
- each subbundle has an observable outcome, boundary, acceptance criteria, proof tier, and progression decision;
- applicable domain overlays exist without imposing unrelated ones;
- UI target policy is correct: CanDoItAll apps are large-screen desktop only by default; reusable basic BaseLib components cover small, medium, and large.
- applicable UI work records the compact composition decisions defined by `candoitall-components-mcp/references/compact-ui-composition.md`, including first-viewport target and scroll owner.

## Final Closure

Confirm:

- no executed work unit remains ambiguously ready/in-progress;
- affected builds/tests and applicable browser/host checks passed or a blocker is explicit;
- UI closure includes inspected normal and relevant open-overlay screenshots, with primary-surface, sizing, first-viewport, and scroll-owner findings recorded;
- Standard proof records commands/results, Behavioral proof contains realistic positive and meaningful negative evidence, and Governed proof contains valid manifests/artifacts;
- later evidence has not invalidated an earlier foundation;
- raw inputs are closed as `Solved`, `Partially solved`, or `Not solved` with meaningful evidence;
- bundle state, code state, and proof reach the same conclusion.

## Proof Tier Rules

- Do not demand Governed artifacts from Standard or Behavioral phases.
- Do not let a phase lower its declared tier after implementation merely to pass closure.
- For Governed proof, use [../candoitall-bundle-execution/references/artifact-backed-proof-manifest.md](../candoitall-bundle-execution/references/artifact-backed-proof-manifest.md) and require existing portable artifacts, hashes, transcripts, semantic invariants, and applicable red-team/downstream evidence.
- For Behavioral/Governed proof, use [../candoitall-bundle-execution/references/semantic-adequacy-proof.md](../candoitall-bundle-execution/references/semantic-adequacy-proof.md). Status/count/file-existence-only checks cannot prove behavior.

## Domain Rules

Apply specialized proof only when relevant:

- C# architecture: ownership, dependency direction, testability, composition, partial-class policy, and CodeAnalytics/review evidence;
- UI: rendered desktop proof, actual normal/open-overlay screenshot inspection, and agreement with the recorded compact composition; multi-viewport proof only for reusable basic BaseLib or explicit scope;
- host integration: host-level evidence;
- production workflows/processes/memory/lifecycle: real producer, consumer, dispatch/lifecycle, lineage, and provider evidence required by the claimed behavior;
- skill/validator changes: repo-to-active synchronization and hashes.

## Stop Rules

- Fail when a required fact or proof is missing and repair is possible.
- Return Blocked when the same gate cannot be resolved without external state or authority.
- Do not convert missing proof into residual risk.
- Do not fail merely for optional folders, alternate headings, absent mobile proof for desktop-only apps, or lack of governed artifacts at a lower proof tier.

## References

- Read [references/readiness-and-closure-checks.md](references/readiness-and-closure-checks.md) for the concise checklist.
- For UI gates, read `candoitall-components-mcp/references/compact-ui-composition.md` rather than duplicating its decision rules here.
- Use `candoitall-subbundle-validator` for work-unit gates.

## Exit Condition

Pass only when the next action can proceed without guessing scope, dependency trust, proof sufficiency, or closure state.
