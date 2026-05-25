You are Codex working in the `fyziktom/CanDoItAll` repository.

The user asked for another follow-up hardening pass on the `processes-hardening` branch after phase5. You must execute the bundle:

`codex/bundles/processes-hardening-followup-runtime-correctness-v6`

## Required Working Rules

1. Work on the current `processes-hardening` branch unless the repository has an explicitly newer branch with the same work.
2. Do not confuse Processes and Workflows.
   - Processes own lifecycle, artifact contracts, transitions, blockers, recovery, and governance.
   - Workflows can be assigned as a process role executor, but Workflow output must map through process-owned contracts.
3. Do not add SQLite support or SQLite migrations.
4. Use PostgreSQL-only migrations and tests.
5. Keep the process core generic.
6. Do not solve remaining defects with prompt-only wording.
7. Implement production behavior first, then tests, then proof manifests.
8. After subbundles 03, 06, and 10 perform the required refactoring checkpoint.
9. Every subbundle must provide:
   - failing-first or red-team proof,
   - passing proof,
   - source assertions,
   - anti-stub audit,
   - changed-file hashes.
10. Final closure must run:
   - focused unit tests,
   - focused integration tests,
   - full unit tests,
   - full integration tests when feasible,
   - `dotnet build CanDoItAll.slnx --no-restore`,
   - PostgreSQL-only audit,
   - completed-stage bundle validator.

## First Commands

```powershell
git status
git branch --show-current
python codex/skills/bundles/candoitall-bundle-preparation/scripts/validate_bundle.py codex/bundles/processes-hardening-followup-runtime-correctness-v6 --stage prepared --repo-root .
```

Then begin with `subbundles/01-alias-ledger-overlap-and-readonly-autopromotion`.
