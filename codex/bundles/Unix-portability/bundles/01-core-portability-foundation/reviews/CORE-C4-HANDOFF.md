# Core Gate C4 handoff to runtime bundle

## Status

- `Provisional implementation handoff approved — C4 remains deferred`

## M07 reconciliation — 2026-08-12

The historical pushed anchor below remains the Core-to-Runtime entry record. The current follow-up checkout is commit `386d8beb6038035f89a9a6961ec017d8213879a5` plus reviewed M00-M07 working-tree changes in package mode. Windows and Linux local proof is green through C2, including PostgreSQL, actual Chromium, process ownership, MCP, Docker, workspace-path, and executable-authority slices. M08 still owns the immutable local merge-candidate proof.

This reconciliation does not upgrade C4 to complete: hosted workflow execution and genuine macOS arm64 evidence remain absent. The Components and FileTools package versions are `0.1.18`; explicit source mode remains a separate fail-closed development path with exact clean anchors.

## Exact anchor

- Repository: `fyziktom/CanDoItAll`
- Branch: `unix-adoption`
- Commit: `dd78ffa9769ba1d125b8be81a4b303df37c32505` (pushed)
- Commit message: `phase2`
- SDK: `.NET 10.0.302`
- Dirty state: all three repositories were clean when the pushed anchors were verified; later bundle bookkeeping is intentionally uncommitted
- Components source dependency: `8372c1d55f21b349f8e859470b02eeb4421e96ca` (`development`, pushed)
- FileTools source dependency: `f31e20d054003348c7557b9634e0838fc5996ae0` (`development`, pushed)

## Proven support profiles

| Profile | OS/architecture | Database | Secret/key profile | Publish artifact | CI/evidence |
|---|---|---|---|---|---|
| Windows headless | Windows x64 | PostgreSQL 16 | DPAPI/Strong Auto | win-x64 framework-dependent | Local 7,459/7,459, PostgreSQL 10/10, two Healthy/Ready cycles and browser smoke; hosted evidence deferred |
| Ubuntu headless | Ubuntu 24.04 x64 | PostgreSQL 16 | LocalUserFile/BasicLocal Auto plus certificate-backed Data Protection profile | linux-x64 framework-dependent | Local 7,459/7,459, PostgreSQL 10/10, two Healthy/Ready SIGTERM-exit-0 cycles; hosted evidence deferred |
| macOS headless | macOS 15 arm64 | PostgreSQL 16 | LocalUserFile/BasicLocal Auto; Keychain is a separate deferred interactive proof | osx-arm64 framework-dependent | `ActualHostUnverified`; hosted evidence deferred |

## Core invariants

- [x] canonical logical path contract
- [x] foreign host-bound path behavior
- [x] filesystem case/link/atomic/locking/mode behavior
- [x] storage/control-plane migration and rollback on Windows/Ubuntu; macOS hosted execution pending
- [x] secret provider/key-ring migration/restart/redaction on Windows/Ubuntu; Keychain actual-host proof separately deferred
- [x] headless startup and capability degradation on Windows/Ubuntu; macOS hosted execution pending
- [ ] active Windows/Ubuntu/macOS hosted execution (`HOSTED-PORTABILITY-VALIDATION-001`)
- [x] Windows local regression; hosted confirmation deferred

## Runtime-impacting changed contracts/files

- Canonical logical workspace/storage locators use `/`; physical paths remain host-owned and foreign syntax fails closed.
- External physical targets cross trusted boundaries through opaque versioned aliases and scoped host bindings.
- Purpose roots are typed, host-specific, outside the checkout, independently write-probed, and exposed only through redacted readiness facts.
- File writes, allocation, cross-process locking, case sensitivity, link handling, and Unix modes use the reviewed physical-filesystem policies.
- Persisted database/storage/application paths use versioned host-bound records with explicit rebind, rollback, and integrity checks.
- Secret-vault selection is typed: Windows Auto is DPAPI/Strong; Unix Auto is LocalUserFile/BasicLocal with an explicit warning; stronger explicit providers fail closed when unavailable.
- Hosted and package builds set `UseLocalCanDoItAllLibraries=false`; local development conditionally substitutes sibling Components/FileTools project references.
- Runtime process/tool ownership remains intentionally outside this core bundle and must be refreshed by B00 before B01.

## Open limitations

- General macOS actual-host build/test/PostgreSQL migration/publish/headless restart is deferred under `HOSTED-PORTABILITY-VALIDATION-001`.
- `MACOS-KEYCHAIN-VALIDATION-001` remains non-blocking and `ActualHostUnverified`; no verified Keychain support claim is permitted yet.
- Windows local headless validation records forced process termination, not graceful Windows service stop.
- Sibling Components/FileTools configuration-propagation edits are committed and pushed at the anchors above.
- Workflow actions currently use mutable major tags; full SHA pinning is a non-blocking supply-chain hardening follow-up unless repository policy requires it.

## Gate decision

- Result: `PROVISIONAL IMPLEMENTATION HANDOFF — C4 DEFERRED`
- Reviewers: primary executor; Dalton independent local-readiness review; explicit operator progression override
- Evidence: `reviews/20-a07-evidence-report.md`; `reviews/21-a07-independent-review.md`; `reviews/22-a07-hosted-validation-deferral.md`
- Runtime bundle first eligible subbundle: `B00`, against the exact anchors above
