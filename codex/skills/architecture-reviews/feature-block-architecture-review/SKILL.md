---
name: feature-block-architecture-review
description: Focused architecture review for a newly added or significantly changed feature block. Use explicitly after landing a major module or capability to classify the new code into domain core, policy, workflow, projection, integration, UI state, or runtime tooling, then identify what must be stabilized before the next feature wave.
---

# Purpose

Use this skill right after a larger feature block lands.

The goal is **not** to redesign the whole system. The goal is to answer:

1. What exactly did this block add or change?
2. Which parts belong to canonical domain truth and which do not?
3. Which architecture boundaries did the block respect or violate?
4. What must be stabilized before more features stack on top?

This skill is optimized for **wave-based development** where features land quickly and the architecture is periodically stabilized.

# Output contract

Produce a concise but deep review with:

- block summary
- classification table
- boundary violations
- duplicated truths or projections
- required stabilization work
- recommended ADRs
- recommended timing:
  - before merge / before next wave / later

# Review workflow

## 1. Scope the block

Prefer one of:

- current diff vs main
- recently changed projects / folders
- a user-named feature block
- recent commits

Write a short scope statement.

## 2. Create a review folder if desired

You may use:

- `python codex/skills/architecture-reviews/feature-block-architecture-review/scripts/new_review.py feature-block-review --scope "<scope>" --template codex/skills/architecture-reviews/feature-block-architecture-review/assets/review-report-template.md`

## 3. Inventory the changed types and files

Map the changed files into these categories:

- Domain Core
- Policy / Authorization
- Application Workflow
- Projection / View / Read Model
- Integration Adapter / DTO
- UI State / View Layer
- Runtime / Dev Tooling / Operational Support

Do not let a type stay unclassified if you can avoid it.

If a type appears to belong to multiple categories, mark it as **mixed-role** and explain why.

## 4. Review fit against the canonical model

For each important changed type, ask:

- Did the block introduce new canonical truth?
- If yes, where is the single source of truth?
- If not, is this only a projection / adapter / workflow?
- Is it stored in the right place?
- Does it introduce a second truth that duplicates existing data?
- Does it overload an existing type such as `Node` or a catch-all manager/service?
- Does it push integration details into the core model?
- Does it mix UI state or runtime concerns into canonical objects?

## 5. Review boundaries and coupling

Check for:

- direct UI -> persistence coupling
- infrastructure types in domain projects
- domain entities annotated or shaped for specific integrations
- projection types being reused as canonical write models
- policy / auth concerns buried inside unrelated entities
- execution/runtime helpers reaching into domain core
- new project references that widen dependency surfaces too far

## 6. Review invariants and lifecycle

For each new or changed canonical concept, ask:

- What owns it?
- What lifecycle does it have?
- What invariants must always hold?
- Where are those invariants enforced?
- Can agents or integrations modify it safely?
- Is it event-like, state-like, relation-like, or projection-like?

If the answers are unclear, that is itself a finding.

## 7. Validate with the smallest safe commands

Run the smallest safe set of:

- build
- targeted tests
- startup checks

Capture exact commands and errors if any.

Do not edit code unless the user explicitly asks for implementation.

## 8. Produce recommendations in wave form

Split recommendations into:

- **Must stabilize now**
- **Before the next feature wave**
- **Could wait until later**

Prefer small, high-leverage stabilizations over broad rewrites.

# Findings to look for

Raise findings when you see:

- new truth stored in multiple places
- projection treated as writable truth
- a new feature shoved into a universal type instead of modeled cleanly
- naming that hides role confusion
- missing invariants
- unclear ownership of many-to-many relations
- unclear time semantics
- policy leakage
- integration leakage
- runtime/dev tooling leakage
- missing tests around new canonical logic

# References

Read these before final synthesis if the block is substantial:

- `references/feature-block-checklist.md`
- `references/layer-classification.md`
- `references/wave-stabilization-heuristics.md`
