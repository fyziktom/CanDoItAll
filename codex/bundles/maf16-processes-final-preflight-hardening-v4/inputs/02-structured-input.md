# Structured Input

## Objectives

- Verify the current source and existing proof before changing behavior.
- Distinguish MAF 1.6 package compatibility, direct adoption, fallback adoption, and explicit deferral.
- Prove runtime behavior for context, finalizer, tool-loop, sessions, handoff, workflow, telemetry, and approval policy.
- Expand artifact validation status/read-model parity for every finalizer validation status, not only `ContentUnavailable`.
- Keep invalid recorded artifacts visible to API/UI/operators with actionable diagnostics.
- Harden artifact dedupe/content-hash and recovery/operator approval paths.
- Add a controlled step0 live smoke preflight gate and produce a go/no-go report for full real UI testing.

## Hard Constraints

- Do not run a full live process test before the step0 live smoke gate passes.
- Do not display `Satisfied` or `AutoProjected` for any recorded artifact rejected by finalizer validation.
- Do not treat a package upgrade as direct feature adoption.
- Keep process runtime generic; do not special-case only Blazor/business/agent-training flows.
- Proof must be artifact-backed with command transcripts, source assertions, changed-file hashes, and anti-stub audit output.
