# Target Architecture

## Process Core
Pure deterministic rules/read-models only. No driver packages, process module runtime, EF, UI, AgentFramework, OpenAI, scheduler, workflow, workspace, storage, or domain-specific template concepts.

## Process Module Runtime
Owns template catalog, import/publish, launch plans, process runs, outbox, dispatch, claims, finalizer, artifacts, recovery, manager diagnostics, scheduler/workflow-origin starts, project/project-structure bridges, and UI/API readback.

## Runtime Host / Driver Boundary
Current allowed state:
- verification-only runtime host,
- dry-run-only execution planning host,
- static capability catalog,
- no reflection discovery,
- no self-registration,
- no execution-capable side effects.

Future execution-capable state remains blocked until a separate approval bundle proves sandbox, allowlist, authorization, audit persistence, lifecycle ownership, cancellation/timeout/failure handoff, emergency stop, and red-team coverage.
