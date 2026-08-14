# A03 independent Gate C2a review

## Decision

**NO-GO — Gate C2a is blocked.**

A03 must reopen before A04 starts. The frozen Windows/Linux evidence is green for its
named scopes, but four production behaviors violate STO-003, STO-004, STO-006, STO-008,
and STO-009. A separate STO-001 actual-host proof gap also remains.

## C# architecture gate result

Status: **Blocked**

### Findings

| Severity | Requirement | Finding and evidence | Required action |
|---|---|---|---|
| Blocking / Critical | STO-004, STO-006 | Legacy same-platform paths can authorize themselves on a different host. `HostBoundPathPolicy.ImportLegacy` binds a native absolute path to the current host whenever `File.Exists`/`Directory.Exists` succeeds (`HostBoundPathPolicy.cs:121-140`). Schema-1 database roots, startup roots, preferred executables, and catalog roots all use this path. A copied legacy record has no source-host identity, yet a coincidentally existing path becomes `Active`; `FileApplicationPreferences.ResolveForFile` then returns it (`FileApplicationPreferences.cs:230-240`). This contradicts the architecture rule that only explicit rebind creates a current-host active record and STO-006's explicit cross-host confirmation requirement. Existing tests cover cross-platform syntax and versioned records with different host IDs, not a same-platform schema-1 import whose path exists. | Make unproven legacy physical paths `NeedsRebind` by default. Any trusted in-place upgrade exception must carry explicit, verifiable provenance rather than infer authority from existence. Add same-platform cross-host fixtures for database roots, preferred executables, repository paths, and storage roots; prove existing destination paths remain unresolved until explicit rebind. |
| Blocking / High | STO-003, PATH-008 | Rooted logical locators are silently reinterpreted. `StorageJson.NormalizeLogicalLocator` replaces separators and calls `TrimStart('/')` before validation (`StorageJson.cs:201-213`), so a relative-path locator such as `/etc/passwd` becomes `etc/passwd` rather than failing closed. The writer uses the same normalization. Tests cover backslash migration and `..`, but not Unix-rooted, drive-rooted, or UNC locators. This also contradicts the evidence report's rooted-input rejection claim. | Detect and reject rooted/foreign physical syntax before separator migration. Keep legacy conversion explicitly format- and field-scoped, and add format-1/format-2 negative fixtures for Unix roots, drive roots, UNC paths, leading separators, empty/dot segments, and remote-locator variants. |
| Blocking / Critical | STO-008, STO-009 | Bootstrap initialization can destroy an unrelated catalog entry. `EnsureBootstrapFileSystemStorageAsync` selects the first `IsSystemDefault` record without checking provider, canonical root, binding state, or reserved bootstrap identity (`StorageCatalogService.cs:49-64`). `RefreshBootstrapStorage` then overwrites its name, provider, connection mode, root, binding, capabilities, and health (`StorageCatalogService.cs:416-440`); concurrent recovery uses the similarly broad `IsSystemDefault || Name == BootstrapStorageName` predicate (`StorageCatalogService.cs:395-413`). The authoritative bootstrap policy is not used here. A legacy/default FTP or IPFS record can therefore be silently repurposed and its configuration/credential association corrupted. | Resolve only a positively identified canonical FileSystem bootstrap record using the authoritative policy and current-host binding state. Preserve unrelated defaults, reject ambiguity, and constrain concurrent recovery to the same bootstrap identity. Add unit and PostgreSQL upgrade/concurrency tests seeded with remote system-default records and ambiguous/mismatched bootstrap candidates, proving no record or configuration is mutated. |
| Blocking / Critical | STO-005, STO-006, STO-009 | Rollback integrity depends on the post-commit marker. Database-profile, preferred-application, and storage-catalog rollback validate the backup checksum only when `commit.json` exists (`DatabaseProfileControlPlaneService.cs:286-311`, `FileApplicationPreferences.cs:182-192`, `StorageCatalogService.cs:163-172`). In the supported interrupted state before marker creation, a modified but syntactically valid backup is accepted and restored. Migration retries also reuse a pre-existing backup without proving it belongs to the current source generation (`StorageCatalogService.cs:497-519`, with equivalent file-catalog flows). Current tests delete the commit marker and invoke restart repair before rollback, so they never exercise direct rollback from the marker-absent crash state. | Persist and durably verify backup identity before target/database mutation, independently of the final commit marker. Bind backup, staged payload, and source generation together; fail closed on stale or modified backup. Add failure-injection tests for direct marker-absent rollback, valid-JSON tampering, stale-backup retry, restart repair, and lossless rollback across all three migration owners, including password ciphertext byte equality. |

### Architecture and dependency direction

No new project-reference cycle or reverse dependency was found in the reviewed A03
scope. Host probing, physical paths, persistence, and migrations remain in
Infrastructure/EF owners, while the PostgreSQL migration assembly follows the existing
dependency direction. The supplied scoped CodeAnalytics result and unchanged A02 graph
are therefore adequate for dependency direction, but they cannot approve the behavioral
boundary violations above. The bootstrap policy currently being bypassed by its owning
service is an architecture correctness failure, not a need for another abstraction.

### Validation and evidence assessment

- Independently parsed the authoritative TRX files: Windows full unit `5,460/5,460`,
  focused `236/236`, storage migration `2/2`, and filesystem atomicity `27/27`; Linux
  focused `234/234`, storage migration `1/1`, and filesystem atomicity `27/27`.
- The frozen Windows and Linux Web Release logs contain no nonzero warning/error summary.
- The artifact redaction report covers 46 files and reports zero findings. No direct
  secret/root disclosure was found in the reviewed diagnostics or artifacts.
- `git diff --check` passed with only the two recorded CRLF-to-LF warnings.
- A02 atomic create-new/no-clobber behavior remains covered by the two `27/27` slices;
  none of the findings above requires weakening it.
- The green suites do not cover the constructive blocker cases above. The evidence
  report and static audit presently overstate rooted-locator rejection, explicit-only
  activation, authoritative bootstrap selection, and checksum-verified rollback.

### STO-001 evidence gap

`ApplicationStoragePortabilityContractTests` verifies a synthetic root string matrix
and the headless XDG mapping, but does not resolve the actual current-host defaults,
create/write every purpose root, verify ownership/permissions, or prove the resolved
roots are outside the checkout. The Windows/Linux TRX files only repeat those synthetic
tests. Add an actual-host probe for workspace, control plane, Data Protection keys,
state, logs, and runtime roots on both available hosts. Actual macOS execution may remain
the already-recorded Gate C4 obligation.

## Closure decision

A03 and Gate C2a remain open, and A04 must remain blocked. Independent re-review is
required after the four source blockers, STO-001 proof gap, affected documentation, and
fresh Windows/Linux evidence are stable. The primary executor must regenerate the bundle
index/checksums and rerun checksum-enforcing portable validation only after the review
text and subsequent remediation evidence are final.

## Residuals not causing this decision

- Actual macOS execution remains mandatory before core Gate C4.
- The documented managed-API link-swap interval remains the accepted A02 residual.
- Workbench project-structure metadata migration remains owned by B00/B02.
- The recorded pre-existing intra-project analyzer cycles remain downstream cleanup
  inputs; no A03 project-reference cycle was found.

## Re-review

### Decision

**GO — Gate C2a.**

All four source blockers and the STO-001 actual-host proof gap from the initial review
are closed. No new blocking correctness, security, portability, dependency-direction,
atomicity, or evidence finding was found. A04 becomes eligible only after the primary
executor completes the post-review canonical and checksum-enforcing closure described
below.

### Blocker closure

- `HostBoundPathPolicy.ImportLegacy` now always creates `NeedsRebind` state and never
  accepts existence as authority (`HostBoundPathPolicy.cs:121-137`). All production
  import call sites use that contract. Existing same-platform database roots, preferred
  executables, startup paths, and storage roots remain unresolved; only the typed rebind
  methods create a current-host active record. The existing-path and end-to-end
  migration/rebind regressions pass on Windows and Linux.
- `StorageJson.NormalizeLogicalLocator` classifies the original value before conversion
  and rejects non-relative physical/URI syntax and leading backslashes
  (`StorageJson.cs:199-219`). Format-1 reads and format-2 writes cover RelativePath and
  RemotePath values for Unix roots, drive paths, drive-relative paths, both UNC forms,
  URIs, leading separators, empty segments, dot segments, and traversal. The former
  `/etc/passwd` reinterpretation is no longer possible.
- Bootstrap initialization now loads every system default, requires exactly one
  reserved FileSystem bootstrap identity, verifies current-host canonical-root authority
  before calling `RefreshBootstrapStorage`, and applies the same checks during
  concurrent recovery (`StorageCatalogService.cs:50-68`, `441-503`). Unrelated remote
  defaults and ambiguous defaults fail closed without mutation. Unit tests preserve
  provider/configuration/credential state, and the new PostgreSQL integration test
  proves the same behavior against the relational catalog.
- `MigrationBackupIntegrity` writes a private create-new integrity manifest before
  target mutation, verifies a pre-existing backup against the current source generation,
  and makes rollback/marker repair verify that manifest independently of `commit.json`.
  Database-profile, preferred-application, and storage-catalog owners all consume the
  helper before rollback. Direct marker-absent rollback, syntactically valid backup
  modification, missing manifest, stale source generation, restart repair, rollback,
  and encrypted-password byte preservation are covered.
- `Current_host_purpose_roots_are_owned_writable_and_outside_the_repository` resolves
  the real current-host policy, creates/writes/reads/cleans probes under all six purpose
  roots, proves they are fully qualified and outside the checkout, and checks Unix user
  access bits. The named test is present and passing in both authoritative host TRX
  files. Synthetic Windows/Linux/macOS and service-account XDG matrices remain covered.

### Independent evidence checks

- Parsed the remediation TRX artifacts: Windows Unit `5,499/5,499`, focus `275/275`,
  StorageMigration `3/3`; Linux focus `273/273`, portable StorageMigration `1/1`.
  The retained filesystem atomicity artifacts are `27/27` on each host.
- The remediation Windows and Linux Web Release logs contain no nonzero warning/error
  summary.
- Queried CodeAnalytics snapshot `snap-20260809161615-8b90a6ae`: four projects, 1,449
  types, 11,305 members, 138 registrations, 14 entities, and zero Error findings. Its
  five project-reference edges show Infrastructure consumed by the PostgreSQL migration
  and Workbench projects, with Web composing those owners; no reverse edge exists. The
  two module cycles and one type cycle are the recorded pre-existing intra-project
  residuals.
- Parsed `A03-secret-scan-remediation-final.json`: 63 files, zero findings. The new
  failure messages remain path- and secret-free.
- Independently reran `git diff --check`: only the two recorded line-ending warnings.
- Independently reran `python scripts/validate_bundle.py --bundle-root .
  --skip-checksums`: 285 files, zero errors, zero warnings.

### Required closure actions and residuals

- `reviews/A03-HANDOFF.md` still lists the pre-remediation `green`/`frozen` artifact
  names, the old 46-file secret scan, and CodeAnalytics snapshot
  `snap-20260809145206-ce54de6a`. This does not invalidate the current source or the
  independently verified remediation artifacts, but the primary executor must update
  those entry points to the remediation files and snapshot before A04 starts.
- Add this re-review text and the refreshed handoff/canonical records to
  `bundle-index.json`, regenerate `CHECKSUMS.sha256`, and rerun portable validation
  without skipped checksums before recording A04 entry.
- Actual macOS execution remains mandatory before core Gate C4. The accepted managed-API
  link-swap interval, B00/B02 Workbench metadata ownership, and pre-existing
  intra-project analyzer cycles remain unchanged residuals.
