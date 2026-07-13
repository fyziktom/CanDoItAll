# QA Prompt

Validate the completed bundle.

Check these gates:

- Repository hygiene tests pass without disabling the scanner or hiding broad path families.
- Runtime launcher tests use current `src/App/CanDoItAll.Web` paths consistently.
- Watch restore stale-reference proof uses a realistic `ProjectReference` path and verifies stale referenced assets omit `--no-restore`.
- Process-template tests assert durable behavior invariants, not only exact prose.
- Branch-signal tests prove explicit line, heading-plus-next-line, and title-inference cases, plus at least one ambiguous/invalid negative case.
- Database proof includes isolated runtime-switch test, pending-model check, and any order-specific reproduction or fix proof.
- `5032` proof uses a fresh rebuild/start and a real HTTP/browser smoke against `http://localhost:5032`.

Reject closure if any critical subbundle passes only because tests were skipped, filtered out of normal runs, or given broad allowlists.
