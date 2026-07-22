---
name: candoitall-bundle-preparation
description: "Prepare or repair an implementation-ready CanDoItAll bundle from requests, feedback, documents, screenshots, existing bundle artifacts, or multi-repo context. Use when work is broad, phased, risky, UI-heavy, architecture-heavy, long-running, or ambiguous enough to need durable requirements, dependencies, work units, proof tiers, and closure mapping before implementation."
---

# CanDoItAll Bundle Preparation

Create the smallest durable coordination artifact that removes execution guesswork. Do not implement feature code during preparation.

## GPT-5.6 Preparation Contract

- Start from the user-visible outcome, constraints, evidence, completion bar, and stop conditions.
- Inspect current repositories before freezing scope or source references.
- Preserve explicit user values and literal scope. Use decision rules where judgment is required.
- Keep reusable context in bundle files; omit repeated prose and process that do not change execution behavior.
- Make validation proportional to risk through explicit proof tiers.

## Decide Whether A Bundle Is Needed

Do not create a bundle for a small coherent change that one agent can implement and validate without durable decomposition. Create or repair one when at least one is true:

- multiple work units have real dependency order;
- several repositories, products, or evidence sources are involved;
- work is likely to span long-running or resumed sessions;
- failure would invalidate substantial downstream work;
- architecture, migration, UI/host proof, security, or production orchestration needs explicit gates;
- the user asks for a bundle.

## Preserve Existing Shapes

For an existing bundle, map its files to these semantic roles before changing structure:

- raw inputs;
- normalized requirements and constraints;
- current-state evidence;
- dependency and execution plan;
- independently actionable work units;
- proof and status;
- input closure.

If all roles are usable, preserve the shape. Add a compatibility map to the root README only when needed. Repair missing meaning in place. Do not force a legacy or external bundle through the canonical scaffold solely to satisfy folder names.

## Content Profiles

- `feedback`: QA notes, screenshots, review documents, concrete defects, or short issue lists. Emphasize literal note closure and observable regression proof.
- `initiative`: features, migrations, refactors, cross-repo programs, or architecture changes. Emphasize inventories, boundaries, state/data flow, failure behavior, and dependency gates.

Profiles describe content, not proof depth. Select `Standard`, `Behavioral`, or `Governed` proof separately for every subbundle using the coordinator skill’s proof-tier rules.

## Canonical Preparation Flow

Use this flow for a new canonical bundle:

1. Save the raw request and source artifacts under `inputs`. Extract `.docx` text with `scripts/extract_docx_feedback.py` before summarizing it.
2. Normalize objectives, constraints, explicit non-goals, assumptions, risks, UI target, and validation expectations.
3. Build input coverage so every raw note remains visible through closure.
4. Inspect the real repo or workspace and record current state, source ownership, package boundaries, and relevant tests.
5. Split work by coherent outcome and ownership. Avoid arbitrary file-count or “misc” phases.
6. Model prerequisites, critical foundations, parallel-safe work, reopen triggers, and downstream invalidation.
7. Give each subbundle a proof tier and observable progression gate.
8. Add domain overlays only where applicable: C# architecture, UI/browser, host behavior, production workflow/process E2E, memory/lifecycle artifacts, security, or migration rollback.
9. Complete traceability from input to requirement, owner, proof, and closure.
10. Run `scripts/validate_bundle.py --stage prepared` for canonical bundles, then `candoitall-bundle-validator`. For compatible non-canonical bundles, record a manual semantic readiness gate.

## Canonical Bundle Contract

Required semantic surfaces:

- root status and validation summary;
- raw inputs and source artifacts;
- current-state evidence;
- normalized requirements;
- dependency/phase plan;
- requirement/input traceability;
- numbered subbundles;
- execution report and closure table.

Use `architecture/`, `shared-prompts/`, `inventories/`, `templates/`, `evidence/`, and `proof/` only when the task or proof tier needs them. The scaffold may create these folders, but empty ceremony is not completion evidence.

## Subbundle Contract

Every work unit must state, under any clear headings:

- status and outcome;
- owned inputs/requirements and explicit non-goals;
- prerequisites, dependency impact, and reopen triggers;
- exact source references or discovery instructions;
- implementation boundary and acceptance criteria;
- proof tier and required validation;
- progression decision;
- browser/host proof only when applicable.

Suggested agent prompts must be outcome-first: goal, success criteria, constraints, relevant tools, required output/proof, and stop rules. Do not restate the whole bundle.

## Dependency Planning

- Use a Mermaid graph or a compact dependency table when there are meaningful branches; a linear list is enough for a truly linear plan.
- Mark a phase as a critical foundation when a wrong result would invalidate dependent work.
- Critical foundations require a meaningful downstream check, but only `Governed` phases require full manifests/hashes/transcripts.
- State which later proof becomes untrustworthy when a prerequisite is reopened.
- Keep parallel candidates explicit, but never parallelize work whose source ownership overlaps unsafely or whose result selects the next action.

## CanDoItAll UI Planning

- CanDoItAll application UI targets a maximized or explicitly named large-screen desktop viewport, including sibling application repos.
- Do not plan small/medium/tablet/mobile tuning or validation for application pages unless explicitly requested.
- For reusable basic `CanDoItAll.Components.BaseLib` components, plan small, medium, and large viewport behavior and proof.
- Preserve existing responsive behavior in other shared libraries when touched; expanding it is separate scope.
- For CanDoItAll UI, plan `candoitall-components-mcp` before custom structure or CSS and use its `references/compact-ui-composition.md` as the detailed composition contract.
- Record the primary surface, supporting-content placement, stats treatment, list/editor dialog-or-tab decision, textarea and dialog sizing rationale, first-viewport target, and scroll owner in each applicable work unit.
- Plan normal-state and open-overlay screenshots. Review menus, tooltips, dropdowns, dialogs, and floating windows for clipping, layering, internal scrolling, action visibility, and lateral overflow at the target viewport.

## C# Architecture Overlay

When work changes large classes, partial classes, project references, runtime composition, providers, tools, processes, workflows, factories, builders, catalogs, or memory protocols:

- load `candoitall-csharp-architecture-bundle-guard`, `csharp-architecture-governor`, and `candoitall-codeanalytics-mcp`;
- record current ownership, target boundaries, dependency direction, pattern decisions, testability seams, composition impact, and partial-class policy;
- create only the architecture artifacts that those decisions need;
- require architecture review before dependent feature work proceeds.

## Quality Gate

Reject preparation when execution would still need to guess the intended outcome, scope, owner, prerequisite, proof tier, validation, or reopen condition. Do not reject it merely because optional canonical files are absent or headings use different names.

## References

- Read [references/bundle-profiles.md](references/bundle-profiles.md) when selecting content emphasis.
- Read [references/subbundle-contract.md](references/subbundle-contract.md) while splitting work.
- Read [references/bundle-validation-rubric.md](references/bundle-validation-rubric.md) before readiness review.
- For UI bundles, read `candoitall-components-mcp/references/compact-ui-composition.md`; do not duplicate its detailed heuristics in bundle files.
- Read [../candoitall-bundle-execution/references/semantic-adequacy-proof.md](../candoitall-bundle-execution/references/semantic-adequacy-proof.md) for `Behavioral` or `Governed` proof.
- Read [../candoitall-bundle-execution/references/artifact-backed-proof-manifest.md](../candoitall-bundle-execution/references/artifact-backed-proof-manifest.md) only for `Governed` proof.
- Use `scripts/scaffold_bundle.py` for a new canonical bundle and `scripts/validate_bundle.py` for canonical validation.

## Exit Condition

Preparation is complete when another agent can execute the bundle without rediscovering the request, repository ownership, dependency order, validation bar, or closure rules—and without being blocked by structure that adds no semantic value.
