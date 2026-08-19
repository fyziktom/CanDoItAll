# A03 evidence report

## Decision state

- Implementation: complete for A03 scope.
- Evidence: complete and independently reviewed.
- Gate result: C2a GO; A04 is eligible after checksum-enforcing closure.
- Source anchor: `a2856070e7303de077088fc7f2f7e96a5bcf0e70` on `unix-adoption`, plus the reviewed A01/A02/A03 working tree.

## Design result

A03 makes physical storage/control-plane paths explicitly owned by one host while keeping logical storage identities portable:

- `ApplicationPurposeRootPolicy` defines distinct Windows, Linux XDG/service, and macOS roots for workspace, control plane, Data Protection keys, state, logs, and runtime temporary data. Explicit configuration overrides remain typed physical paths.
- `HostBoundPathRecord` carries format version, platform, syntax, opaque host binding, state, and validation time. Foreign platform/host data becomes `NeedsRebind` or fails resolution; diagnostics do not contain the physical path.
- Database profiles use schema 2 host-bound workspace records. Legacy schema 1 data remains readable, migration preserves encrypted-password bytes and profile identity, and explicit rebind is required before foreign activation.
- Preferred application records use schema 2 host-bound executable records. Foreign preferences remain listable as `NeedsRebind` but are never resolved or launched. Desktop launch policy remains independently disabled by default.
- Storage references use format 2 canonical logical locators. Legacy backslash migration is field-scoped; traversal and rooted/remote values in relative locators fail closed.
- Unproven legacy physical paths always import as `NeedsRebind`; existence on the current platform never grants authority. Only an explicit typed rebind can activate a database root, storage root, repository root, startup root, or preferred executable.
- Storage catalog roots persist host-binding metadata in PostgreSQL. Bootstrap authority accepts exactly one reserved filesystem record with a current-host canonical root, preserves unrelated defaults, and rejects missing, foreign, or ambiguous authority.
- Catalog/profile/preference migrations use a durable backup plus an independent integrity manifest, staged checksum, commit marker, restart repair, and checksum-verified rollback. A stale or modified backup fails before mutation even when the final commit marker is absent. Redacted results expose IDs, counts, states, and hashes rather than roots or secrets.
- `FileSystemStorageDriver` retains A02's portable allocation and atomic create-new/no-replace behavior. A03 tests preserve revision conflict, cancellation, concurrent-write, and crash semantics.

Workbench project-structure metadata path migration (`WB-001`) remains owned by B00/B02. A03 does not normalize or reinterpret those fields; the shared host-bound record contract is ready for the later owner.

## Requirement evidence

| Requirement | Result | Principal proof |
|---|---|---|
| STO-001 | Verified candidate | Purpose-root matrix for Windows/Linux/macOS; actual Windows and Linux Release probes resolve, create, write, read, and clean all six current-host roots outside the repository. Linux additionally asserts owner read/write and directory execute permissions. |
| STO-002 | Verified candidate | Linux service-account test works with XDG config/data/state/runtime roots and no home directory; explicit overrides retain logical locator semantics. |
| STO-003 | Verified candidate | Format-2 logical storage references; field-scoped legacy separator reader; format-1/format-2 negative matrices reject Unix roots, drive roots, both UNC spellings, URIs, leading separators, traversal, dot, and empty segments before normalization. |
| STO-004 | Verified candidate | Workspace, storage, repository, startup, and executable host-binding contracts reject foreign platform/host values and unproven same-platform legacy values with redacted rebind diagnostics. |
| STO-005 | Verified candidate | Database profile legacy migration, profile switch, restart repair, encrypted-password byte preservation, rebind, failure injection, and checksum-verified rollback. |
| STO-006 | Verified candidate | Preferred application migration/rebind/restart/rollback tests; foreign executable never resolves; launch remains policy-controlled. |
| STO-007 | Verified candidate | A02 atomic/cross-process storage contracts remain green; A03 focused runs include storage conflict and filesystem atomicity coverage. |
| STO-008 | Verified candidate | Exact reserved filesystem bootstrap selection, pre-mutation authoritative-policy check, unrelated remote-default preservation, ambiguous duplicate rejection, explicit root rebind, and concurrent first-use PostgreSQL proof. |
| STO-009 | Verified candidate | Private backup integrity manifests independent of final commit markers, stale-generation rejection, staged/backup checksums, direct pre-marker rollback tamper rejection, restart repair, rollback methods, and 0-finding artifact redaction scan. |

## Final commands and results

| Host | Scope | Result | Evidence |
|---|---|---:|---|
| Windows | Full Unit Release regression | 5,499/5,499 | `artifacts/unix-portability/A03/windows-baseline/A03-windows-full-unit-release-remediation.trx` |
| Windows | Required A03 Release filter, including backup-integrity contracts | 275/275 | `artifacts/unix-portability/A03/windows-baseline/A03-windows-release-focused-remediation.trx` |
| Windows | `Category=StorageMigration`, including PostgreSQL transaction/restart/rollback and unrelated-default preservation | 3/3 | `artifacts/unix-portability/A03/windows-baseline/A03-windows-storage-migration-integration-remediation.trx` |
| Windows | Web Release build | 0 warnings/errors | `artifacts/unix-portability/A03/windows-baseline/A03-windows-web-release-build-remediation.log` |
| Linux Docker | Required A03 Release filter, excluding only `RequiresHostDocker=true` nested-daemon tests | 273/273 | `artifacts/unix-portability/A03/linux/A03-linux-release-focused-remediation.trx` |
| Linux Docker | Portable `Category=StorageMigration` integration | 1/1 | `artifacts/unix-portability/A03/linux/A03-linux-storage-migration-integration-remediation.trx` |
| Linux Docker | Web Release build | 0 warnings/errors | `artifacts/unix-portability/A03/linux/A03-linux-web-release-build-remediation.log` |
| Windows/Linux | Filesystem atomicity preservation | 27/27 on each host | `artifacts/unix-portability/A03/windows-baseline/A03-windows-filesystem-atomicity.trx`; `artifacts/unix-portability/A03/linux/A03-linux-filesystem-atomicity.trx` |

Linux used Docker Engine `linux 29.6.2` and `mcr.microsoft.com/dotnet/sdk:10.0`, with stable `CANDOITALL_HOST_BINDING_ID=a03-linux-container`. The two tests marked `RequiresHostDocker=true` start PostgreSQL through a sibling Docker daemon and are therefore executed on the Windows Docker host, not recursively inside the Linux container.

## Failure evidence and remediation

- The named failing-first Windows contract log captured absent purpose-root and host-binding types before implementation.
- The first Linux combined run exposed a raw escaped-JSON assertion that was host-format-sensitive and a test that attempted nested Docker. The assertion now parses JSON structurally; host-Docker tests carry an explicit trait and retain real PostgreSQL proof on Windows.
- The first exact Windows Release filter found 12 synthetic records that bypassed the production catalog migration and constructed unbound physical roots. Test builders now bind current-host roots and mirror production workspace creation. Production fail-closed behavior was not weakened.
- The first full Unit run exposed three security fixtures trying to emit malformed locators through the new canonical writer. Those fixtures now inject explicit legacy persisted JSON; the production resolver distinguishes blank optional metadata from non-empty malformed data and denies malformed locators as forbidden.
- The first completed broad run was 5,459/5,460 because a positive watchdog test assumed a 30 ms producer would always beat a 70 ms wall-clock deadline under suite contention. Its positive-path idle margin is now 500 ms with a 3-second absolute bound; focused proof and the authoritative full rerun are green.
- Independent C2a review then identified four constructive gaps. Remediation now rejects rooted logical locators before legacy conversion, quarantines all unproven legacy physical paths, prevents bootstrap initialization from repurposing remote or ambiguous defaults, and verifies backup identity independently of the final commit marker.
- The first remediation full-unit run exposed 15 test fixtures that directly constructed inactive filesystem catalog records plus two timing-sensitive cases. The fixtures now use the same current-host binding contract as production; all 17 former failures passed together (38/38), followed by the authoritative 5,499/5,499 rerun.

All failed/intermediate artifacts are preserved and are not presented as authoritative gate results.

## Architecture evidence

- Scoped CodeAnalytics snapshot: `snap-20260809161615-8b90a6ae`; four A03 owners, no blocking errors.
- Snapshot scope: 1,449 types, 11,305 members, 138 service registrations, and 14 entities.
- Analyzer residuals are two existing intra-project module cycles, one existing Workbench type cycle, generated duplicate-attribute diagnostics, and size/complexity warnings. A03 adds no project-reference edge or abstraction project.
- The detailed classification is `artifacts/unix-portability/A03/A03-static-audit-final.md`.

## Redaction evidence

`artifacts/unix-portability/A03/A03-secret-scan-remediation-final.json` scanned 63 generated proof files, including TRX and build logs, and found 0 secret patterns. Migration reports and diagnostics are separately asserted not to expose workspace roots, executable paths, encrypted passwords, or connection strings.

## Residuals

- Actual macOS execution is unavailable locally and remains mandatory before core Gate C4.
- Host identity defaults to a protected opaque digest of platform and machine name; container/service operators should set stable `CANDOITALL_HOST_BINDING_ID` as documented.
- Managed filesystem APIs retain the A02 documented final link-swap interval.
- Workbench persisted project-structure path fields remain B00/B02 work; A03 neither claims nor performs that migration.
- Large existing control-plane services remain architecture hotspots. Splitting them during the migration would enlarge risk; their new responsibilities remain within the existing Infrastructure boundary and are covered by focused recovery tests.

## Review result

Independent C2a review and remediation re-review are recorded in `reviews/12-a03-independent-review.md`. The final decision is GO with no remaining A03 blocker.
