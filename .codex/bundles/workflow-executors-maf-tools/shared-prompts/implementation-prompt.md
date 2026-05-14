# Implementation Prompt

Implement the assigned subbundle only.

Read the subbundle README first, then check `analysis/01-current-state.md`, `architecture/01-target-solution.md`, `requirements/01-normalized-requirements.md`, and `traceability/01-requirement-traceability.md`.

Rules:

- Preserve existing CanDoItAll boundaries: UI orchestration in Blazor components, workflow contracts in Models/Core, MAF binding in the MAF project, spreadsheet implementation behind `CanDoItAll.Tools.Documents`.
- Use strongly typed ids, enums, records, and options. Do not dispatch executor behavior through raw magic strings.
- Do not add a plugin loader in this bundle. Add stable contracts and setup renderer keys that a later plugin runtime can consume.
- Do not silently hide failures. Invalid settings, unavailable host services, timeouts, and exhausted retries must fail predictably and include actionable sanitized state.
- Keep implementation small and compatible with existing workflow definitions.

Before closing the subbundle, update `reviews/01-execution-report.md` with commands, proof, gate status, and blockers.
