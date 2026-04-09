# Structured Input

## Objectives

- Produce an execution-ready plan for the process-management module without touching product code.
- Repair the stale architect bundle so it passes the current bundle-workflow readiness gate.
- Expand the architecture so future execution does not paint the process module into a corner on trust, explainability, governance, replay, and cross-repo convergence.

## Hard Constraints

- Preserve `CanDoItAll.Modules.Processes` as canonical owner of process definitions, runs, handoffs, work briefs, decision records, and journals.
- Keep CRM-HR as the canonical owner of business roles, staffing templates, workforce identities, supplier identities, and durable AI identities.
- Keep Workspace as the canonical owner of provider profile truth.
- Keep Projects as the canonical owner of project scope and hierarchy.
- Keep Workbench and canvas overlays as projections, never the source of process truth.
- Avoid compile-time dependency on `CanDoItAll.AgentFramework` during the first process-module merge.

## Bundle Repair Targets

- Add the validator-required root folders and documents.
- Reframe the old feature pack into phase-based execution subbundles.
- Add mandatory post-phase repair-bundle generation gates.
- Add explicit development/test seed packs.
- Add shared post-phase validation roles and required skill guidance.

## Cross-Repo Convergence Expectations

- Resolve process roles through role requirements and staffing intent first, then bind to real executors.
- Treat `CanDoItAll.AgentFramework` runtime-side provider and agent models as overlap risk, not as automatic truth.
- Use `CanDoItAll.IPFS` as a planned evidence-storage seam, not as a forced first-wave runtime dependency.

## Validation Expectations

- Use `candoitall-bundle-validator` and `validate_bundle.py --stage prepared` for bundle readiness.
- Future execution must use `candoitall-subbundle-validator` before and after each subbundle.
- Future UI execution must use `candoitall-components-mcp`, `playwright`, and large-screen screenshot review before subbundle closure.

## Deferred But Architecturally Mandatory Concerns

- Explainability and decision transparency
- Artifact trust, provenance, retention, and allowed-usage policy
- Forensic reconstruction and operating modes
- Autonomy governance and safe refusal
- Decision intelligence, capability-gap analytics, and execution economics
- Simulation-ready contracts and replay-friendly runtime evidence
