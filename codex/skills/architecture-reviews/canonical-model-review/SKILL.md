---
name: canonical-model-review
description: Deep architectural review focused on canonical model integrity, source-of-truth boundaries, projections vs canonical entities, invariants, and architecture stabilization in a C# / Blazor solution with agentic integrations. Use explicitly after major feature additions, before a new feature wave, before release, or whenever the model feels overloaded or internally fragile.
---

# Purpose

Use this skill to perform a **deep architectural review** of the repository with a strong focus on the **canonical model**.

The review should answer:

1. What is the actual **canonical truth** in this system?
2. Which types own that truth?
3. Which things are only **projections, views, summaries, caches, exports, snapshots, AI proposals, UI state, or integration adapters**?
4. Where is the architecture becoming internally fragile even if it still looks powerful from the outside?
5. What must be stabilized **now**, what can wait until the **next feature wave**, and what belongs to **later cleanup**?

This skill is especially suitable for systems that behave like a **project operating system**: graph-like project models, many node types, relations, derived views such as Gantt/CRM/activity feeds, AI/agent workflows, snapshots, storage backends, runtime tooling, and multiple persistence modes.

# Core review stance

Be a skeptical, evidence-backed architect.

- Do **not** start with refactor ideas.
- First map the codebase and gather evidence.
- Do **not** assume intent that the code does not clearly express.
- Separate:
  - current facts
  - inferred intent
  - recommended changes

Always treat the following as different categories until proven otherwise:

- canonical entity
- relation / link
- value object
- event
- projection / view
- integration DTO / adapter model
- runtime state
- UI-only state
- snapshot / export / import representation
- AI-generated proposal
- execution or tooling metadata
- policy / authorization model

# Prefer these tools

If available, prefer **CanDoItAll CodeAnalytics MCP** for:

- solution / project graph analysis
- symbol search and reference tracing
- dependency inspection
- document and symbol inspection
- focused context around architectural hotspots

Use SharpTools only as a backup if CodeAnalytics has a real unresolved capability gap. Do not switch to SharpTools merely because CodeAnalytics transport needs a restart or reinstall.

If the optional custom agents from this skillset are installed, you may explicitly spawn:

- `arch_mapper`
- `canonical_model_skeptic`
- `runtime_validator`

Use them only when they materially improve evidence quality.

# Required workflow

## 1. Establish scope

If the user named a feature block, branch, folder, project, or module, use that as the primary scope.

If not, infer scope from one or more of:

- current branch diff vs main
- recent commits
- recently changed folders
- major projects in the solution

State the inferred scope near the start of the review.

## 2. Create or reuse a review folder

If the repository has `architecture/reviews/`, create a new timestamped review folder.

You may use:

- `python codex/skills/architecture-reviews/canonical-model-review/scripts/new_review.py canonical-model-review --scope "<scope>" --template codex/skills/architecture-reviews/canonical-model-review/assets/review-report-template.md`

If the repo does not use review folders yet, continue without blocking.

## 3. Build a solution and architecture map

Collect evidence for:

- solution files
- projects / assemblies
- project references
- namespaces
- domain models
- application services / workflows
- infrastructure adapters
- UI / Razor / Blazor components
- persistence layers
- serialization / mapping code
- MCP / agent / auth / token / policy related code
- tests and test fixtures
- startup / composition root

You may use:

- CanDoItAll CodeAnalytics MCP
- `python codex/skills/architecture-reviews/canonical-model-review/scripts/solution_inventory.py --root . --output architecture/reviews/_inventory.json`

Do not trust the inventory script as the source of truth. It is a heuristic helper only.

## 4. Extract candidate canonical concepts

Build an explicit table of candidate concepts. For each concept, try to classify it into exactly one primary kind:

- canonical entity
- relation / edge
- value object
- event
- projection / derived view
- integration adapter / DTO
- policy / authorization object
- runtime state
- UI state
- snapshot / import-export representation
- unknown / ambiguous

For each candidate, capture:

- concept name
- current owner namespace / project
- identity / key if any
- who can mutate it
- where it is persisted
- who reads it
- what other things are derived from it
- whether it is time-varying
- evidence files / symbols

If a concept seems to occupy multiple roles, flag it as **overloaded**.

## 5. Review the canonical model

For each important concept, ask these questions:

### Identity and ownership
- What business truth does this concept own?
- Does it have a stable identity?
- Which layer has authority to mutate it?
- Is ownership clear, or can multiple layers rewrite the same truth?

### Source of truth
- Is this concept the single source of truth for its concern?
- Is the same truth stored elsewhere under a different name?
- Is a projection treated like a canonical entity?
- Is an import/export/snapshot object mistaken for live state?

### Boundary clarity
- Does this type belong in domain core, policy, workflow, integration, UI, or runtime tooling?
- Is it physically placed in the wrong project / namespace?
- Is infrastructure leaking into the core model?
- Is UI state leaking into domain code?

### Relations and many-to-many structures
- Who owns many-to-many links?
- Are dependency edges first-class or hidden as ad hoc collections?
- Are invariants around relations enforced anywhere?
- Are time semantics attached to the right concept?

### Derived views
- Are Gantt, Mermaid, CRM dashboards, activity feeds, testing summaries, AI summaries, or search indexes treated as derived views?
- If users can edit those views, does the system write back into canonical truth in a controlled way?

### AI / agent concerns
- Is AI output stored as a **proposal** or incorrectly treated as canonical truth?
- Are embeddings, semantic vectors, summaries, heuristics, and caches clearly treated as derived data?
- Are MCP tokens, access scopes, and agent rights part of policy / authorization rather than mixed into core domain entities?

### Runtime and operational state
- Are watch/runtime/dev-tool states mixed with project truth?
- Are snapshot / IPFS / export / sync concerns clearly separated from live canonical state?
- Does DB switching or storage abstraction change semantics, or only persistence mechanisms?

## 6. Apply the CanDoItAll-specific lens

Use these checks aggressively in systems of this style:

- Is `Node` a strong universal domain primitive, or an all-purpose box that keeps absorbing unrelated semantics?
- Is `NodeType` merely a label, or does it really define allowed fields, relations, time semantics, actor semantics, and execution semantics?
- Are `Relation` / `Dependency` / `ParentChild` semantics explicit, or blurred together?
- Are project graph semantics distinct from execution-block semantics?
- Are `Actor`, `Artifact`, `Event`, `Snapshot`, and `Projection` separate concepts, or just different flavors of node stuffing?
- Are Gantt, Mermaid, CRM, testing, and activity feeds projections over canonical data, or parallel truths?
- Are snapshots and IPFS-based storage treated as archival / transport / backup state rather than live canonical truth?
- Are permissions and agent rights enforced in a policy layer rather than smuggled into arbitrary domain objects?
- Are runtime tool helpers, watch managers, and diagnostics coupled too tightly to the core model?

## 7. Validate with builds, tests, and safe runtime checks

Use the smallest safe validation that can confirm or falsify a concern.

Prefer:

- targeted solution/project build
- targeted tests
- safe startup checks
- configuration validation
- serialization / migration sanity checks

Capture exact commands and concrete failures.

Do **not** edit code during the review unless the user explicitly asks for fixes.

## 8. Produce the report

Use the template in:

- `codex/skills/architecture-reviews/canonical-model-review/assets/review-report-template.md`

Always include:

1. Executive summary
2. Scope and evidence gathered
3. Canonical model map
4. Single-source-of-truth table
5. Key findings by severity
6. Stability risks that could compound in the next feature wave
7. Stabilization plan:
   - now
   - next wave
   - later
8. Open questions / assumptions

## 9. Scoring

Use the scorecard from:

- `codex/skills/architecture-reviews/canonical-model-review/assets/scorecard-template.yaml`

Score from 1 to 5:

- source_of_truth_integrity
- boundary_clarity
- invariant_enforcement
- projection_discipline
- integration_isolation
- runtime_state_separation
- ai_policy_separation
- testable_architecture
- change_safety
- overall_stability

Do not fake precision. If evidence is weak, say so.

# Findings rubric

Use these severity levels:

- **Critical**: architecture can silently corrupt truth, permissions, or cross-module consistency
- **High**: likely to create major fragility during the next feature wave
- **Medium**: localized design debt that meaningfully increases complexity
- **Low**: cleanup or clarity issue
- **Open Question**: not enough evidence yet

Every finding must include:

- claim
- evidence
- why it matters
- recommended stabilization action
- recommended timing (`now`, `next_wave`, or `later`)

# References

Read these before final synthesis if the task is deep enough:

- `references/canonical-model-glossary.md`
- `references/deep-review-checklist.md`
- `references/candoitall-specific-lens.md`
