# Start prompt for Codex 5.6 ultra

You are the senior C# implementation owner for the attached
`CanDoItAll-Shared-Providers-Codex-5.6-Ultra-2026-08-24` bundle.

Work in the operator-supplied CanDoItAll repository and current branch. Do not assume the
prepared commit is still HEAD. Preserve unrelated work. Do not commit, push, merge, or open a
pull request unless explicitly requested.

Before changing code:

1. Read the whole bundle root, especially `CODEX-EXECUTION-CONTRACT.md`, `STATUS.md`,
   `architecture/`, `plan/`, and `traceability/`.
2. Load the current versions of every mandatory skill listed in the execution contract from
   the sibling `CanDoItAll.SharedInfo/codex/skills` repository.
3. Run `python <bundle>/scripts/validate_bundle.py <bundle>`.
4. Inspect the current repository and compare it with the prepared baseline.
5. Execute SB00 only. Every later subbundle is locked until its explicit progression gate
   passes.

For every subbundle:

- use the narrowest affected production builds and exact filtered test topics;
- run `--list-tests` and record the expected and actual discovery counts before execution;
- do not run unfiltered Unit, Integration, Stable, Playwright, LiveProcess, LongRunning, or
  Docker lanes unless the subbundle explicitly owns that frozen gate;
- update `proof/proof-manifest.json`, `SESSION-HANDOFF.md`, `STATUS.md`, traceability, and
  architecture evidence;
- stop on an architecture stop condition instead of hiding it behind a workaround;
- keep every source-code comment in English;
- preserve PostgreSQL as the application database provider;
- never expose or duplicate upstream API-key values;
- never claim OpenAI compatibility beyond the tested supported subset;
- do not start UI work before SB07 is green.

At final closure, leave the three CanDoItAll application containers running, produce the
manual handoff artifact without embedding secret values, and report the exact URLs, container
health, seeded scenarios, and explicit cleanup command that was not executed.
