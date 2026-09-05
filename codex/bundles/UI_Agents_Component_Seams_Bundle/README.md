> Implementation authorized by the owner on 2026-09-04. See inputs/04-implementation-authorization.md and reviews/02-execution-status.md. Documentation-only wording below records the preparation stage and does not block this authorized execution.

# Agents UI component seams - implementation and proof

Reference: **CDA-UI-SEAMS-AGENTS-01-v2**. Shared architecture: [CDA-UI-SEAMS-BASE-v2](../UI_Component_Seams_Shared_Architecture_Bundle/README.md).

**Implementation and governed validation are complete, with explicit watch/sandbox follow-ups.** See [execution status](reviews/02-execution-status.md), [coverage](proof/SB06/coverage-map.md) and [governed proof](proof/SB07/manifest.md). Original preparation inputs remain historical evidence; the accepted revision controls the intended architecture.

The goal is to make Agents UI boundaries explicit without losing existing behavior, and prepare a credible first small sandbox that improves developer iteration. Agents is the first application UI slice, not the architecture for every later module.

## Read first

1. [Requirements](requirements/00-normalized-requirements.md), [invariants](requirements/01-invariants-and-non-goals.md), and [behavior preservation matrix](requirements/02-behavior-preservation-matrix.md).
2. [Ownership map](architecture/01-csharp-boundary-map.md), [editor session and host contract](architecture/09-editor-session-and-host-contract.md), and [UI composition](architecture/10-ui-composition.md).
3. [Rendered subtree and contract closure](inventories/04-rendered-subtree-and-contract-closure.md), [test impact](inventories/02-test-impact-and-classification.md).
4. [Seven phases](plan/00-phase-plan.md), [proof](plan/02-proof-and-validation-plan.md), [commands](commands/00-validation-commands.md), and [sandbox/navigation handoff](plan/03-sandbox-and-navigation-handoff.md).
5. [Preparation review](reviews/00-preparation-readiness.md) and [revision validation](reviews/01-revision-validation.md).

## Decisions changed by the review

- Production bookmark URLs are not a prerequisite for extracting a small UI cluster and its sandbox.
- A fakeable parent is not proof that its real children, dialogs, assets, or project graph are isolated.
- Selection, open editor target, mutable draft/session, semantic section, and serialized URL have different owners and lifetimes.
- Interface counts and prescribed type names are not architecture goals. Use cohesive operations, pure policies, and the smallest real testable boundaries.
- Preserve lazy history/overview reads, chat context readiness, persistence/concurrency, partial errors, and result channels explicitly.
- Preserve behavior case coverage rather than fixed test counts. Verify actual production composition as well as fake-backed components.
- Bookmarkability design remains a decision track. The supplied meeting pack proposes hybrid path/query navigation and routed dialogs; it is not an approved canonical route specification.

## Scope and delivery

Keep this child in the existing AgentFramework project. Extracting projects, building a sandbox host, changing sibling libraries, and implementing new URL/history behavior belong to separately prepared work. Same-project seams may remove semantic coupling while leaving the evaluated build graph heavy; report that honestly.

SB01 freezes current behavior and captures development-loop measurements. SB02-SB05 make incremental seams with tests in the same phase. SB06 audits coverage and remaining test coupling. SB07 proves integration and records six independent readiness dimensions. A catalog-first extraction candidate is assessed at SB03; its follow-up does not wait for production routing.

Actual phase evidence is delivered under proof/SB01 through proof/SB07. Preparation reviews are historical; final closure requires the current artifacts described in [proof placement](proof/README.md).
