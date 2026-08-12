# Subbundle result — M00

## Anchor

- Prepared commit: `e282446daa2b775b93f2d70ea7fc0e282e26d802`
- Re-anchored clean starting commit: `386d8beb6038035f89a9a6961ec017d8213879a5`
- Components: clean `8372c1d55f21b349f8e859470b02eeb4421e96ca`
- FileTools: `f31e20d054003348c7557b9634e0838fc5996ae0` plus the three reviewed dirty files
- Authoritative dependency mode: package (`UseLocalCanDoItAllLibraries=false`)
- Host: Windows x64; SDK `10.0.303` selected by `global.json` roll-forward

## Changed files

- `.gitignore`
- `.local/share/NuGet/Migrations/1` (deleted)
- `tools/Validation/Test-Documentation.ps1`
- `tests/Support/CanDoItAll.McpTestHost/README.md`
- bundle readiness, architecture, re-anchor, execution, and ledger records

## Implemented behavior

The compact bundle now has explicit proof tiers, prerequisite/progression/reopen semantics, architecture checks, raw-input closure state, and a durable re-anchor report. Local `.local` state is ignored and the tracked migration artifact is deleted; the repository validator rejects future tracked `.local` files.

## Commands and results

| Command | Exit | Duration | Evidence |
|---|---:|---:|---|
| `git rev-parse HEAD; git status --porcelain=v1; git log -3` | 0 | 0.8 s | Re-anchor report |
| sibling `git rev-parse HEAD; git status --short --branch` | 0 | 0.6 s each | Re-anchor report |
| package-mode evaluated FileTools integration graph | 0 | 1.5 s | Package `0.1.18`; no sibling FileTools project references |
| source-mode evaluated FileTools integration graph | 0 | 1.1 s | Dirty sibling project references and direct-source constant confirmed |
| `git diff --check` | 0 | 0.7 s | Clean |
| `tools/Validation/Test-Documentation.ps1 -RepositoryPath .` | 0 | 3.9 s | 172 maintained Markdown files passed |
| deleted-artifact and ignore probe | 0 | 0.8 s | Deletion present; `.local/` ignore rule matched |

## Validation reuse and invalidation

No product test evidence was reused. The pre-anchor provider UX delta is carried into M08. Bundle checksums/index are intentionally invalidated until M07/M10 reconciliation.

## Residuals

The source-mode graph remains non-reproducible and falsely implies validation; M02 owns that confirmed blocker. Actual macOS evidence remains explicitly deferred to M09.

## Decision

`GO`

## Next eligible subbundle

M01
