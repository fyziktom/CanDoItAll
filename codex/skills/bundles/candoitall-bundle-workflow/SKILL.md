---
name: candoitall-bundle-workflow
description: "Run or recover an end-to-end CanDoItAll bundle: select a compatible bundle shape, prepare or repair it, validate dependencies, execute phases, capture risk-proportionate proof, and close raw inputs. Use for broad, multi-repo, risky, UI-heavy, architecture-heavy, long-running, or resumed work that needs durable coordination."
---

# CanDoItAll Bundle Workflow

Coordinate preparation, execution, validation, recovery, and closure. The bundle is durable working state, not a document-production exercise.

## GPT-5.6 Operating Contract

- Define the outcome, hard constraints, available evidence, completion bar, and stop rules. Let Codex choose the efficient path inside those boundaries.
- State each invariant once. Prefer decision rules over universal process rules.
- Resolve required discovery before action. Parallelize independent reads; keep dependent decisions sequential; synthesize before editing.
- Retry an empty or suspiciously narrow lookup with one or two meaningful alternatives before concluding evidence is absent.
- Validate the changed behavior and affected packages before finishing. If a check cannot run, record why and the next-best check.
- Update the user at phase changes or when a finding changes the plan; do not narrate routine tool calls.

This operating contract follows the official [GPT-5.6 prompt guidance](https://developers.openai.com/api/docs/guides/prompt-guidance-gpt-5p6).

## Completion Contract

Finish only when:

- source inputs are preserved and mapped to requirements, owning work, proof, and closure status;
- dependencies, critical foundations, and reopen triggers agree with the current repositories;
- every executed phase has passed its applicable entry and closure gates or is honestly blocked;
- code, tests, browser or host proof, and bundle status support the same conclusion;
- each raw note is `Solved`, `Partially solved`, or `Not solved` with evidence;
- remaining work is an explicit blocker or follow-up, not hidden residual-risk prose.

## Bundle Compatibility Gate

Do not reject or migrate an existing bundle merely because its folders or headings differ from the canonical scaffold.

1. Locate the files that serve these semantic roles: source inputs, requirements, dependency plan, work units, execution status, proof, and closure.
2. If every role is recoverable, preserve the existing shape and add a short compatibility map to its root README only when the mapping is not obvious.
3. Repair missing meaning in place. Do not create a second parallel bundle shape during execution.
4. Run `validate_bundle.py` when the bundle uses the canonical CanDoItAll contract. For a compatible legacy or external shape, run the same semantic gate manually and record that the structural validator was not applicable.
5. Migrate structure only when the current shape prevents durable state, automation, or handoff; record the old-to-new map.

Read [references/workflow-decision-tree.md](references/workflow-decision-tree.md) when selecting direct work, preparation, repair, or execution. Read [references/handoff-rules.md](references/handoff-rules.md) when recovering or transferring work.

## Proof Tiers

Select proof per subbundle. Dependency-critical does not automatically mean governed evidence.

- `Standard`: affected build or static checks, targeted tests when present, and a concise execution-report result. Use for low-risk mechanical or local changes.
- `Behavioral`: Standard proof plus a realistic positive case and a meaningful negative, boundary, or regression case. Use when behavior or user-visible state changes.
- `Governed`: Behavioral proof plus `proof/SBxx/manifest.md`, durable transcripts, hashes, source assertions, semantic invariants, and any required red-team or downstream evidence. Use for security/privacy boundaries, migrations, destructive or irreversible behavior, production orchestration, disputed acceptance, high-cost rework, cross-agent auditability, or when the user explicitly requests forensic proof.

Increase the tier when evidence shows risk; do not increase it because the task is merely large. Read [../candoitall-bundle-execution/references/artifact-backed-proof-manifest.md](../candoitall-bundle-execution/references/artifact-backed-proof-manifest.md) only for `Governed` proof.

## Workflow

1. Find an existing bundle and run the compatibility gate.
2. Use `candoitall-bundle-preparation` when no usable bundle exists or material meaning is missing.
3. Re-anchor stale or resumed work on current repo state, owned inputs, dependency gates, captured proof, blockers, and the next validation action.
4. Activate only the domain skills that the affected phase needs. Architecture-heavy C# work uses the architecture guard/governor and CodeAnalytics; CanDoItAll UI work uses the Components MCP.
5. Run readiness validation. Repair failures that affect execution; do not churn structure that is already semantically complete.
6. Execute one dependency-ready subbundle at a time with `candoitall-bundle-execution`.
7. Run `candoitall-subbundle-validator` before and after each subbundle at its selected proof tier.
8. Reopen a prerequisite when later evidence invalidates it.
9. Audit original inputs note by note, run final closure validation, and synchronize all status and proof surfaces.
10. If bundle skills or validators changed, synchronize the repo-owned copy to the active Codex skill root and verify hashes before relying on the new contract.

## CanDoItAll UI Target Policy

- CanDoItAll applications, including sibling application repositories, target large-screen desktop use. Plan and prove the named large-screen viewport or maximized desktop window.
- Do not spend implementation or validation time tuning application pages for tablet, mobile, small, or medium breakpoints unless the user explicitly expands scope or a regression breaks an already-required contract.
- The exception is reusable basic components in `CanDoItAll.Components.BaseLib`: they must remain prepared and validated for small, medium, and large viewports.
- Other shared libraries preserve existing responsive behavior when touched, but new cross-viewport work is not implied unless the user requests it.
- UI proof still covers readability, clipping, scroll ownership, overlays, interaction states, and visual consistency at the target desktop viewport.

## Domain Overlays

Keep domain-specific proof out of the generic bundle contract. Add specialized matrices or evidence only when the phase actually involves that domain—for example production workflow dispatch, provider usage, lifecycle signals, memory synthesis, architecture boundaries, browser UI, or host integration.

## Stop And Reopen Rules

- Stop before a dependent phase when prerequisite proof is missing or contradicted.
- Repair the bundle when implementation reality changes scope, dependencies, success criteria, or proof—not for harmless wording differences.
- Ask only for a user decision that materially changes scope or authorization and cannot be inferred safely.
- Stop retrieval when the core decision has sufficient evidence. Do not search again only to improve phrasing.

## Exit Condition

Exit when implementation and durable bundle state agree, every applicable gate passes or is honestly blocked, original inputs are closed with evidence, and no required work is disguised as a caveat.
