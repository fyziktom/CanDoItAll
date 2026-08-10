# Core Gate C4 handoff to runtime bundle

## Status

- `Pending exact-commit hosted evidence — A07 local readiness GO`

## Exact anchor

- Repository: `fyziktom/CanDoItAll`
- Branch: `unix-adoption`
- Commit: pending operator-authorized commit/push; current base `27527039dd05299b3ed54ed2c3bc129cec2aeecf`
- Commit message: pending
- SDK: `.NET 10.0.302`
- Dirty state: reviewed A00-A07 working tree; must be committed before C4
- Components source dependency: `f5c477980316f4f7c8363945eb9624db1ab6e867` plus an uncommitted `Directory.Build.props` configuration-propagation edit
- FileTools source dependency: `47ea01b61a174c435775504724e2922ae54769e5` plus an uncommitted `Directory.Build.props` configuration-propagation edit

## Proven support profiles

| Profile | OS/architecture | Database | Secret/key profile | Publish artifact | CI/evidence |
|---|---|---|---|---|---|
| Windows headless | Windows x64 | PostgreSQL 16 | DPAPI/Strong Auto | win-x64 framework-dependent | Local 7,459/7,459, PostgreSQL 10/10, two Healthy/Ready cycles and browser smoke; exact hosted evidence pending |
| Ubuntu headless | Ubuntu 24.04 x64 | PostgreSQL 16 | LocalUserFile/BasicLocal Auto plus certificate-backed Data Protection profile | linux-x64 framework-dependent | Local 7,459/7,459, PostgreSQL 10/10, two Healthy/Ready SIGTERM-exit-0 cycles; exact hosted evidence pending |
| macOS headless | macOS 15 arm64 | PostgreSQL 16 | LocalUserFile/BasicLocal Auto; Keychain is a separate deferred interactive proof | osx-arm64 framework-dependent | `ActualHostUnverified`; exact hosted evidence pending |

## Core invariants

- [x] canonical logical path contract
- [x] foreign host-bound path behavior
- [x] filesystem case/link/atomic/locking/mode behavior
- [x] storage/control-plane migration and rollback on Windows/Ubuntu; macOS hosted execution pending
- [x] secret provider/key-ring migration/restart/redaction on Windows/Ubuntu; Keychain actual-host proof separately deferred
- [x] headless startup and capability degradation on Windows/Ubuntu; macOS hosted execution pending
- [ ] active Windows/Ubuntu/macOS CI
- [x] Windows local regression; exact hosted confirmation pending

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

- General macOS actual-host build/test/PostgreSQL migration/publish/headless restart is pending the hosted matrix.
- `MACOS-KEYCHAIN-VALIDATION-001` remains non-blocking and `ActualHostUnverified`; no verified Keychain support claim is permitted yet.
- Windows local headless validation records forced process termination, not graceful Windows service stop.
- Sibling Components/FileTools configuration-propagation edits require committed anchors or removal of their necessity before final provenance is complete.
- Workflow actions currently use mutable major tags; full SHA pinning is a non-blocking supply-chain hardening follow-up unless repository policy requires it.

## Gate decision

- Result: `PENDING — local readiness GO`
- Reviewers: primary executor; Dalton independent local-readiness review
- Evidence: `reviews/20-a07-evidence-report.md`; `reviews/21-a07-independent-review.md`; exact hosted run links/artifact checksums pending
- Runtime bundle first eligible subbundle after GO: `B00`
