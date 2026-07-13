# Implementation Prompt

You are implementing the process runtime branch-aware QA hardening bundle.

Use the bundle files as the source of truth. Do not implement from the GPTPro raw notes alone. Start each subbundle by reading its README, prerequisites, exact source references, architecture sections, and open reopen triggers.

Hard constraints:

- Do not hardcode software-delivery branch names, QA step keys, .NET tool names, Blazor scaffold names, or Tetris terms in generic process runtime/application logic.
- Preserve legacy receipt formats before adding structured object rules.
- Add failing-first or characterization tests before behavior changes.
- Keep components small and testable; do not add permanent adapter partial files as fake separation.
- Record proof under `proof/SBxx/` for every critical subbundle.

Before closing a critical subbundle:

- capture changed-file SHA-256 hashes;
- capture command transcripts with command line and exit code;
- include failing-first and passing proof where behavior changed;
- run source assertions proving behavior lives in production code;
- run anti-stub audit for `TODO`, `NotImplemented`, fixture-specific branching, and template-only output;
- update `reviews/01-execution-report.md`.
