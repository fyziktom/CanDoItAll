# Structured Input

## Objectives

- Keep the existing process-management bundle execution-ready and truthfully reopened when new scope arrives.
- Add a simple MCP server that exposes process definitions and process runtime data without duplicating process truth outside `CanDoItAll.Modules.Processes`.
- Update install, reinstall, settings, Codex config, and skill sync so the new MCP can be installed locally and used after restart.
- Preserve the repaired architecture so MCP access becomes another projection over canonical process services, not a second API or domain.

## Hard Constraints

- Preserve `CanDoItAll.Modules.Processes` as canonical owner of process definitions, runs, handoffs, work briefs, decision records, and journals.
- Prefer a simple local stdio MCP over a new remote process-agent API unless a real requirement forces the extra surface.
- Keep CRM-HR as the canonical owner of business roles, staffing templates, workforce identities, supplier identities, and durable AI identities.
- Keep Workspace as the canonical owner of provider profile truth.
- Keep Projects as the canonical owner of project scope and hierarchy.
- Keep Workbench and canvas overlays as projections, never the source of process truth.
- Avoid compile-time dependency on `CanDoItAll.AgentFramework` during the first process-module merge.

## Bundle Repair Targets

- Add a dedicated phase07 for process-MCP delivery.
- Keep the bundle aligned with actual repo state after the already-shipped process module and canvas remediation.
- Add a post-phase07 repair-bundle generation gate before claiming closure again.

## Cross-Repo Convergence Expectations

- Resolve process roles through role requirements and staffing intent first, then bind to real executors.
- Treat `CanDoItAll.AgentFramework` runtime-side provider and agent models as overlap risk, not as automatic truth.
- Use `CanDoItAll.IPFS` as a planned evidence-storage seam, not as a forced first-wave runtime dependency.

## Validation Expectations

- Use `candoitall-bundle-validator` and `validate_bundle.py --stage prepared` for bundle readiness.
- Future execution must use `candoitall-subbundle-validator` before and after each subbundle.
- Future UI execution must use `candoitall-components-mcp`, `playwright`, and large-screen screenshot review before subbundle closure.
- MCP execution must include focused unit tests, integration tests, real stdio transport proof, and local install/config proof.

## Deferred But Architecturally Mandatory Concerns

- Explainability and decision transparency
- Artifact trust, provenance, retention, and allowed-usage policy
- Forensic reconstruction and operating modes
- Autonomy governance and safe refusal
- Decision intelligence, capability-gap analytics, and execution economics
- Simulation-ready contracts and replay-friendly runtime evidence
- Restart-bound tool discovery after Codex config changes
