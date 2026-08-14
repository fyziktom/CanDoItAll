# Subbundle result — M10

## Anchor

- Repository commit: `386d8beb6038035f89a9a6961ec017d8213879a5`
- Candidate source-manifest SHA-256: `a6fe597d186252e913e88b3896faf571e9ce474ef15a2bb8e6f311a7b817461e`
- M08 artifact-manifest SHA-256: `8b164654cb1b9e08db96260847468a33fa8fcd000e24b7db5ace8ed2d9db2c4b`
- Dependency mode: package; Components `0.1.18`; FileTools `0.1.18`

## Reconciliation

M08 is a proven local Windows/Linux merge candidate with exact source and evidence manifests. M09 is an implementation-complete handoff but has no actual macOS arm64 colleague evidence. Support claims remain bounded to the hosts that actually executed the candidate.

All P0/P1 findings in the follow-up register are locally closed. The stable-suite raw P2 residuals remain disclosed. Accepted enterprise-vault, hosted-CI, Keychain-session, and macOS actual-host deferrals remain explicit.

No production source or dependency anchor changed during M09/M10 bookkeeping. Per the invalidation ledger, no product suite was rerun.

## Commands and results

| Scope | Result |
|---|---|
| M08 source/artifact hash reconciliation | PASS |
| M09 handoff hash and command review | PASS |
| Documentation validation | PASS |
| Follow-up compatibility-shape manual semantic validation | PASS; canonical legacy-scaffold validator is not applicable and reports the documented 40 shape errors |
| Original portability bundle portable structural validation | PASS |
| Follow-up and original checksums | PASS |
| `git diff --check` | PASS; existing generated-model line-ending advisory only |

## Residuals

- Actual macOS arm64 build, runtime catalog, migration, restart, launchd, and redaction evidence is absent.
- The one-shot stable suites retain classified P2 failures; their exact raw results remain in M08.
- Hosted CI and actual-session Keychain proof remain accepted deferrals.

## Decision

`NO-GO — actual macOS arm64 colleague validation is still required before MERGE READY`

This decision can change only after the exact M09 handoff runs on the frozen candidate and records `MACOS GO`, or after a new candidate is frozen if product/dependency changes are required.

## Next eligible action

Execute `templates/macos-handoff.md` on an actual macOS arm64 colleague host. Do not merge from this record alone.
