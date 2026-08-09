# Core execution report

## Overall status

- Execution: `In progress`
- Active subbundle: `A04 conditionally stopped — SEC-014 GO; C2 blocked solely by genuine macOS proof`
- Final gate: `C4 not started`

## Subbundle progression

| Subbundle | Entry gate | Closure gate | Downstream dependencies checked | Progression result | Notes |
|---|---|---|---|---|---|
| A00 | Program entry | C0 | A01 dependency/architecture entry checked | Completed — GO | Exact source anchor, reviewed scan, inventories, Windows/Linux baseline, and architecture gate complete. |
| A01 | C0 | C1a | C0 GO | Completed — GO | Independent review recorded in `reviews/08-a01-independent-review.md`; PATH-001..PATH-010 closed. |
| A02 | C1a | C1 | C1a GO | Completed — GO | Independent review and two remediation passes are recorded in `reviews/10-a02-independent-review.md`; FS-001..FS-010 are closed. |
| A03 | C1 | C2a | C1 GO | Completed — GO | STO-001..STO-009 evidence is in `reviews/11-a03-evidence-report.md`; independent decision is in `reviews/12-a03-independent-review.md`. |
| A04 | C2a | C2 | C2a GO | SEC-014 independently GO; conditional stop at C2 | Windows DPAPI/Strong Auto and Unix LocalUserFile/BasicLocal Auto have both-host HTTP startup, restart/mode tests, stable regression, complete redaction coverage, and independent GO. C2/A05 remain blocked solely by genuine macOS Keychain proof. |
| A05 | C2 | C3a | Pending | Blocked | |
| A06 | C3a | Hosting gate | Pending | Blocked | |
| A07 | Hosting gate | C4 | Pending | Blocked | |

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
| SEC-001, SEC-003..SEC-006, SEC-008..SEC-013 | Implemented and independently verified — C2 NO-GO solely pending macOS | `reviews/13-a04-evidence-report.md`, `reviews/14-a04-independent-review.md`, `architecture/04-secrets-and-key-bootstrap.md`, and refreshed A04 Windows/Linux test, build, architecture, rollback, and metadata-only redaction artifacts |
| SEC-002 | Blocked — actual macOS validation required for C2 | Keychain Security-framework adapter and injected-native contracts are complete; genuine macOS execution is unavailable locally and cannot be replaced by Docker. |
| SEC-007, SEC-014 | Implemented and independently verified — C2 NO-GO solely pending macOS | Windows `Auto` uses DPAPI/`Strong`; Unix `Auto` uses `LocalUserFile`/`BasicLocal`; exact Windows launch and Linux no-session-vault smoke both return HTTP 200; both-host 99/99 focus, Windows 5,529/5,529 full Unit, Unix `0700`/`0600` plus restart continuity, complete zero-sentinel scan, and independent GO are under `artifacts/unix-portability/A04/remediation-2/` and `reviews/14-a04-independent-review.md`. |

## Migration evidence

| Migration | Backup | Dry-run | Commit | Restart | Rollback | Result |
|---|---|---|---|---|---|---|
| Logical paths/external aliases | Opaque protected binding retained | Legacy edit/write/reload and package redaction tests | Versioned writer enabled | Protected binding reload tested | Legacy reader retained; unbound alias fails explicitly | Passed |
| Storage/control plane | Private durable backup with independent integrity manifest | Typed migration plans and redacted summaries | Durable file target or PostgreSQL transaction plus commit marker | Marker repair validates staged/committed values | Backup integrity verified independently of the final marker before restore | Passed — C2a GO |
| DPAPI/Data Protection/vault | Private durable migration journal plus source retained through checkpoint | Redacted typed plan and Windows DPAPI dry-run | Destination stage/read-back then optimistic source pointer commit | Exact destination re-verification before source cleanup | Source verified before rollback publication; post-save interruption resumes idempotently; tamper/source drift rejected | Passed on Windows/Linux; C2 blocked by macOS |

## Actual-host evidence

| OS/profile | Build/test | Filesystem | Secrets | Headless start/restart | Publish | Result |
|---|---|---|---|---|---|---|
| Windows | A04 full Unit 5,529/5,529; security/database Unit 99/99; secret/migration integration 4/4; Web Release build 0 warnings/errors | A02/A03 private durable filesystem and migration contracts retained | Actual DPAPI, external wrapping-key, legacy Development compatibility, rollback recovery, and zero sentinel findings passed | Exact reported Development command served HTTP 200 under `Auto`; clean publish/service proof remains A06 | Not in A04 | Locally passed and independently reviewed; macOS C2 input pending |
| Ubuntu headless | A04 security/database Unit 99/99; secret portability 3/3 and migration 1/1; actual Secret Service 1/1 retained; Web Release build 0 warnings/errors | Private modes, atomic writes, and host-bound roots retained | Actual D-Bus/GNOME Keyring, external wrapping-key, BasicLocal restart/`0700`/`0600`, and zero sentinel findings passed | Auto without a session vault served HTTP 200 with an explicit same-user warning; clean publish/service proof remains A06 | Not in A04 | Locally passed and independently reviewed; macOS C2 input pending |
| macOS interactive/headless | Keychain injected-native contract tests pass on Windows/Linux; actual host unavailable | A01-A03 host-neutral/macOS contract coverage retained | Security.framework adapter implemented; actual Keychain access not executed | Contract-level restart/concurrency passed; actual host pending | Not in A04 | C2 blocker — actual host required now |

## Raw request closure

| Raw note | Status | Proof |
|---|---|---|
| Basic slash/path and filesystem work first | Implemented — C1 GO | A01/A02 evidence reports |
| Secrets and storage before tools/runtime | Storage complete — C2a GO; secret work blocked at C2 by actual macOS proof | A03/A04; A05 and runtime remain blocked pending C2/C4 |
| Basic vault fallback must allow first launch without an external or interactive vault | Implemented and independently verified | SEC-014 GO; Windows uses built-in DPAPI/`Strong`, while Unix `LocalUserFile` reports `BasicLocal`, warns without values/paths, and serves without an external/session vault. |
| Consider prerequisite refactoring | Planned | A00/A05 and B00 |
| Consider separate runtime bundle | Solved in preparation | Two-bundle program |
| Output Codex ZIP | Prepared | Program archive |
