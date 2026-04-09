# Development Seed Plan

## Seed Objectives

- Make authoring, runtime, approval, exception, refusal, conformance, and management views testable without handcrafted one-off data.
- Reuse existing app services where possible instead of inventing a separate seeding framework.
- Keep the seed data rich enough for Playwright, integration tests, demo review, and post-phase repair bundles.

## Existing Repo Helpers To Reuse

- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Support\TestProfileSeedHelper.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectWorkbenchModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Infrastructure\Storage\WorkspaceStorage.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Infrastructure\Storage\Placement\StoragePlacementService.cs`

## Foundational Seed Packs

| Seed pack | Required contents | Purpose |
| --- | --- | --- |
| Workspace baseline | database profile, provider profiles, storage baseline, environment modes | Supports process, runtime, and evidence tests. |
| CRM-HR baseline | human parties, suppliers, AI profiles, role templates, staffing managers, approvers, reviewers | Supports role-first process authoring and assignment resolution. |
| Project baseline | at least three projects, shared-project example, workbench object references, delivery context | Supports project-linked processes and projection tests. |
| Artifact baseline | managed artifacts, validation evidence, reference documents, quarantined or outdated samples, future IPFS descriptor placeholders | Supports trust, retention, and evidence tests. |

## Required Scenario Seeds

| Scenario | What it must prove |
| --- | --- |
| Human-first approval workflow | publishable role-first process, approval wait, and escalation path |
| Human plus AI collaboration | role template resolves to AI-capable assignee but stays governed by process policy |
| Supplier handoff | non-human internal-external baton handling |
| Input-quality rejection and rework | exception and refusal outcomes with explicit reasons |
| High-assurance mode | stricter approvals, artifact sensitivity, and autonomy restrictions |
| Shared-project process | project-linked process without shadow duplication |
| Conformance drift review | recorded deviations, unofficial loops, and improvement candidate generation |
| Management review | bottleneck, cost, queue, and capability-gap views over seeded runtime history |

## Seed Data Shapes That Must Exist

- process owners, sponsors, stewards, triage leads, implementers, validators, approvers, observers
- reusable role templates for human, AI, hybrid, supplier, and fallback variants
- eligible pools and backup routes
- published process definitions and archived predecessors
- draft definitions that cannot publish because required governance data is missing
- runtime runs in active, waiting, blocked, refused, completed, and escalated states
- artifact samples with trust-state differences:
  draft, validated, approved, quarantined, outdated

## Evidence Storage Direction

- Wave 1 uses existing managed artifact storage and relative-path contracts.
- The seed plan must reserve optional fields for:
  content hash, external evidence URI, pin state, and IPFS content identifier.
- When the IPFS seam is implemented later, the same scenario data should be able to point to either managed storage or IPFS-backed evidence without changing process truth.
