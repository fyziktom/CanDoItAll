# Structured Input

## Raw Notes
- Codex finished and pushed work on `maf-processes-refactor`; verify real source and tests instead of trusting bundle prose.
- Long-lived tests previously referenced concrete `codex/bundles/<bundle-name>` folders; remove transient path coupling.
- Restore the ability to start processes from UI, project structure, API, scheduler, and workflow-origin paths.
- Prove the app can start again and expose process templates.
- Prove software-development and business-analysis process scenarios.
- Use OpenAI live proof only when an opt-in flag and key are available; sanitize transcripts and never log secrets.
- Clarify runtime-host, registry, selector, DI, manager command, scheduler hook, and workflow hook status without building a generic process-driver runtime host in this bundle.
- Prepare a detailed bundle and handoff artifact.

## Normalized Requirement Map
- RQ-001 through RQ-013 in `requirements/01-normalized-requirements.md`.
- Raw-note ownership is tracked in `traceability/01-requirement-traceability.md`.
- Execution order and critical gates are tracked in `plan/01-phase-plan.md`.

## Execution Boundary
- Current restoration must use `ProcessesService`, dispatch/finalizer services, MAF/workflow/direct-agent execution, scheduler/workflow start paths, and read-only driver diagnostics.
- Generic driver runtime host, driver registry, selector, driver DI auto-registration, driver manager command, scheduler driver hook, and workflow driver hook are out of scope for current implementation.
