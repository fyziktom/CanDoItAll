# Target Solution

- Preserve Process Core as deterministic, generic rules and read models.
- Keep stable runtime-host DTOs in Process Contracts.
- Keep runtime, EF, launch, dispatch, recovery, artifact, scheduler/workflow, API, and UI orchestration in the Process Module.
- Keep the runtime host verification-only and dry-run-only for this bundle.
- Use static capability descriptors and explicit provider boundaries; do not add reflection discovery, fallback selectors, or driver self-registration.
- Defer execution-capable drivers to a future approval gate.

See `architecture/01-target-architecture.md` for the expanded layer model.
