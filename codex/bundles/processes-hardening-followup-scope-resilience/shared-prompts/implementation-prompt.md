# Shared Implementation Prompt For Codex

You are implementing the `processes-hardening-followup-scope-resilience` bundle.

Read this bundle root first:

- `README.md`
- `analysis/03-verified-findings.md`
- `requirements/01-normalized-requirements.md`
- `plan/01-phase-plan.md`

Then execute one subbundle at a time in dependency order.

Hard constraints:

- Keep `Processes` and `Workflows` separate. A workflow may execute a process role, but process-owned artifact contracts and state transitions must remain in `CanDoItAll.Modules.Processes`.
- Keep the runtime generic. Do not hardcode Blazor, .NET, JavaScript, QA, or software-only assumptions into the generic process core.
- Do not reintroduce SQLite. This branch is PostgreSQL-only.
- Prefer explicit typed process policies over title/text heuristics.
- Prompt instructions are not enough. Enforce step operation boundaries through invocation metadata and tool policy.
- Do not close a critical subbundle without failing-first proof, passing proof, source assertions, anti-stub audit, changed-file hashes, and semantic invariant files.

When implementing SB01, start with the smallest production boundary that lets tools reject out-of-scope mutation for architecture/planning/review steps.

When implementing SB02, fix workflow-backed role candidates and subprocess parent finalization before expanding artifact validation.

When implementing SB03, add disposition routing so negative findings choose repair/no-go branches when the process model supports that.

When implementing SB04, ensure downstream steps resume after upstream artifact materialization succeeds.

When implementing SB05, make validation less heuristic and more explicit.

When implementing SB06, add no-progress fingerprints before adding retry count changes.

When implementing SB07, add process definition lint and simulation.

When implementing SB08, run red-team scenarios including the Blazor architecture-step incident and at least three non-software process examples.
