# Core execution report

## Overall status

- Execution: `In progress`
- Active subbundle: `A07 three-platform CI, integration, restart, and Core Gate C4`
- Final gate: `C4 pending exact-commit hosted evidence`

## Subbundle progression

| Subbundle | Entry gate | Closure gate | Downstream dependencies checked | Progression result | Notes |
|---|---|---|---|---|---|
| A00 | Program entry | C0 | A01 dependency/architecture entry checked | Completed — GO | Exact source anchor, reviewed scan, inventories, Windows/Linux baseline, and architecture gate complete. |
| A01 | C0 | C1a | C0 GO | Completed — GO | Independent review recorded in `reviews/08-a01-independent-review.md`; PATH-001..PATH-010 closed. |
| A02 | C1a | C1 | C1a GO | Completed — GO | Independent review and two remediation passes are recorded in `reviews/10-a02-independent-review.md`; FS-001..FS-010 are closed. |
| A03 | C1 | C2a | C1 GO | Completed — GO | STO-001..STO-009 evidence is in `reviews/11-a03-evidence-report.md`; independent decision is in `reviews/12-a03-independent-review.md`. |
| A04 | C2a | C2 | C2a GO | Completed — GO | Windows DPAPI/Strong Auto and Unix LocalUserFile/BasicLocal Auto have both-host HTTP startup, restart/mode tests, stable regression, complete redaction coverage, and independent implementation GO. Operator-deferred genuine Keychain execution remains `MACOS-KEYCHAIN-VALIDATION-001` with no verified-support claim. |
| A05 | C2 | C3a | C2 GO | Completed — GO | The initial NO-GO and bounded independent GO are in `reviews/17-a05-independent-review.md`; refreshed Windows/Linux UI, owner-readiness, implementation-metadata, startup, regression, static, and architecture proof is in `reviews/16-a05-evidence-report.md`. |
| A06 | C3a | Hosting gate | C3a GO | Completed — GO | The initial NO-GO and bounded independent GO after remediation are in `reviews/19-a06-independent-review.md`; refreshed proof is in `reviews/18-a06-evidence-report.md`. |
| A07 | Hosting gate | C4 | C3b/Hosting GO | Local readiness GO | Independent review 21 accepted the active workflow, package/direct-local builds, Windows/Ubuntu 7,459/7,459 stable results, both-host PostgreSQL 10/10, two-cycle headless proof, browser smoke, and static enforcement. C4 remains pending an exact committed/pushed anchor, hosted Windows/Ubuntu/macOS evidence, repository check policy, and final independent review; macOS Keychain execution is separately deferred. |

## Requirement status

| Requirement | Status | Evidence |
|---|---|---|
| PREP-001 | Satisfied | `reviews/04-a00-rebase-report.md` |
| PREP-002 | Satisfied | `inventories/01-execution-portability-scan-review.md` and reviewed artifacts |
| PREP-003 | Satisfied | `inventories/path-field-inventory.csv` and `inventories/persistence-migration-inventory.csv` |
| PREP-004 | Satisfied | `reviews/05-a00-baseline-report.md` and `reviews/06-a00-linux-failure-classification.md` |
| PATH-001..PATH-010 | Satisfied — C1a GO | `reviews/07-a01-evidence-report.md`, `reviews/08-a01-independent-review.md`, `inventories/01-execution-portability-scan-review.md`, and A01 TRX/build/graph artifacts |
| FS-001..FS-010 | Satisfied — C1 GO | `reviews/09-a02-evidence-report.md`, `reviews/10-a02-independent-review.md`, `architecture/08-a02-filesystem-semantics.md`, and A02 Windows/Linux test, build, graph, audit, and redaction artifacts |
| STO-001..STO-009 | Satisfied — C2a GO | `reviews/11-a03-evidence-report.md`, `reviews/12-a03-independent-review.md`, `architecture/03-storage-and-host-bound-records.md`, and A03 Windows/Linux migration, build, audit, and redaction artifacts |
| SEC-001, SEC-003..SEC-014 | Satisfied — C2 GO | `reviews/13-a04-evidence-report.md`, `reviews/14-a04-independent-review.md`, `architecture/04-secrets-and-key-bootstrap.md`, and refreshed A04 Windows/Linux test, build, architecture, rollback, and metadata-only redaction artifacts |
| SEC-002 | Implemented; actual macOS validation deferred without verified-support claim — C2 GO | Keychain Security-framework adapter and injected-native contracts are complete. `reviews/15-a04-macos-keychain-validation-deferral.md` tracks actual execution as `MACOS-KEYCHAIN-VALIDATION-001`. |
| PLAT-001..PLAT-005 | Satisfied — C3a GO | `reviews/16-a05-evidence-report.md`, `reviews/17-a05-independent-review.md`, Windows/Linux 488/488 focused and 3/3 UI tests, Windows 5,557/5,557 full Unit, both-host HTTP health/API/UI captures, zero-warning builds, and snapshot `snap-20260810005847-cefe425c`. |
| HOST-001..HOST-005, DOC-001 | Satisfied — C3b/Hosting GO | `reviews/18-a06-evidence-report.md` and `reviews/19-a06-independent-review.md`; durable Windows/Ubuntu provenance, seven redacted purpose roots, explicit launchd service identity, four-RID publish matrix, 5,561/5,561 regression, 32/32 both-host focus, snapshot `snap-20260810044522-a1246e1e`, and complete portability/redaction scans. macOS remains `ActualHostUnverified` pending `A07-MACOS-HEADLESS-ACTUALHOST-001`. |
| CI-001..CI-006 | Implemented/local candidate ready; hosted closure pending | `reviews/20-a07-evidence-report.md`; active workflow, Windows/Ubuntu 7,459/7,459 stable results, outside-checkout two-cycle headless evidence, current 2/2 CI/headless regression, local static enforcement, and complete schema-3 artifact scan. Genuine macOS and exact-commit required-check evidence remain pending. |
| CI-007 | Blocked pending C4 exact-commit evidence | Runtime bundle B00 remains blocked until hosted Windows/Ubuntu/macOS jobs and final independent review complete `reviews/CORE-C4-HANDOFF.md`. |

## Migration evidence

| Migration | Backup | Dry-run | Commit | Restart | Rollback | Result |
|---|---|---|---|---|---|---|
| Logical paths/external aliases | Opaque protected binding retained | Legacy edit/write/reload and package redaction tests | Versioned writer enabled | Protected binding reload tested | Legacy reader retained; unbound alias fails explicitly | Passed |
| Storage/control plane | Private durable backup with independent integrity manifest | Typed migration plans and redacted summaries | Durable file target or PostgreSQL transaction plus commit marker | Marker repair validates staged/committed values | Backup integrity verified independently of the final marker before restore | Passed — C2a GO |
| DPAPI/Data Protection/vault | Private durable migration journal plus source retained through checkpoint | Redacted typed plan and Windows DPAPI dry-run | Destination stage/read-back then optimistic source pointer commit | Exact destination re-verification before source cleanup | Source verified before rollback publication; post-save interruption resumes idempotently; tamper/source drift rejected | Passed on Windows/Linux; C2 GO with actual macOS validation deferred |

## Actual-host evidence

| OS/profile | Build/test | Filesystem | Secrets | Headless start/restart | Publish | Result |
|---|---|---|---|---|---|---|
| Windows headless | A07 stable 7,459/7,459; current CI/headless regression 2/2; package and sibling-source Release builds 0 warnings/errors | Seven owner-resolved purpose roots are Ready and redacted | DPAPI/Strong selected by Auto | Outside-checkout publish started twice; health/operations/migrations Ready and browser smoke passed; forced process termination is recorded rather than described as graceful | win-x64 framework-dependent artifact complete | Local A07 pass; exact hosted candidate pending |
| Ubuntu headless | A07 stable 7,459/7,459 on native Ubuntu; prior unprivileged service proof retained | Seven XDG/control-plane purpose roots are Ready and redacted | LocalUserFile/BasicLocal selected by Auto with explicit limitation warning; certificate-backed Data Protection profile retained | Outside-checkout publish started twice; health/operations/migrations Ready; both SIGTERM exits 0 | linux-x64 framework-dependent artifact complete | Local A07 pass; exact hosted candidate pending |
| macOS interactive/headless | Cross-publish and injected-native contract tests only; actual host unavailable | Portable path/root contracts and launchd rendering validated | Security.framework adapter implemented; actual Keychain access not executed; headless baseline is LocalUserFile/BasicLocal | launchd/install/rollback contract validated; actual start deferred | osx-x64 and osx-arm64 artifacts complete and labeled `ActualHostUnverified` | `A07-MACOS-HEADLESS-ACTUALHOST-001` and non-blocking `MACOS-KEYCHAIN-VALIDATION-001`; no verified-support claim |

## Raw request closure

| Raw note | Status | Proof |
|---|---|---|
| Basic slash/path and filesystem work first | Implemented — C1 GO | A01/A02 evidence reports |
| Secrets and storage before tools/runtime | Storage complete — C2a GO; secrets complete — C2 GO with actual macOS validation deferred | A03 through A06 complete; A07 is active and the sibling runtime bundle remains blocked pending C4 |
| Basic vault fallback must allow first launch without an external or interactive vault | Implemented and independently verified | SEC-014 GO; Windows uses built-in DPAPI/`Strong`, while Unix `LocalUserFile` reports `BasicLocal`, warns without values/paths, and serves without an external/session vault. |
| Consider prerequisite refactoring | Planned | A00/A05 and B00 |
| Consider separate runtime bundle | Solved in preparation | Two-bundle program |
| Output Codex ZIP | Prepared | Program archive |
