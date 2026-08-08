# Core portability requirements

## Requirement catalog

| ID | Priority | Owner | Requirement | Acceptance |
|---|---|---|---|---|
| PREP-001 | P0 | A00 | The execution checkout is anchored, dirty-state-safe, and compared with the prepared commit before edits. | Anchor report records branch, HEAD, merge-base/delta, SDK, OS, architecture, and preserved working-tree changes. |
| PREP-002 | P0 | A00 | Every platform-sensitive source/project is inventoried and classified by path, filesystem, secret, process, desktop, hosting, test, or external-dependency concern. | Generated inventory covers all projects and has no unclassified P0/P1 item at gate C0. |
| PREP-003 | P0 | A00 | Every persisted path/key/secret record and migration boundary affected by portability is mapped before schema or provider changes. | Persistence map identifies writer, reader, format, migration owner, rollback, and host affinity. |
| PREP-004 | P1 | A00 | Existing stable Windows behavior and available Linux/macOS baselines are captured before implementation. | Baseline evidence includes exact commands, exit codes, logs, and known pre-existing failures. |
| PATH-001 | P0 | A01 | Persisted logical workspace/storage/artifact paths use '/' as their canonical serialized separator. | New writers emit only canonical logical paths; round-trip tests pass on all target OSes. |
| PATH-002 | P0 | A01 | Known legacy logical-path fields accept Windows backslashes on read without treating every Unix backslash as a separator. | Compatibility is field-scoped, versioned, tested, and does not rewrite arbitrary physical paths or filenames. |
| PATH-003 | P0 | A01 | Logical paths, physical host paths, routes/URIs, executable identifiers, and opaque command/script text have distinct contracts. | Inventory and APIs prevent a normalization routine from crossing categories. |
| PATH-004 | P1 | A01 | Portable configuration roots support '~' and an explicit environment-token syntax, while legacy Windows tokens remain readable where required. | Windows, Linux, and macOS tests cover expansion, unset variables, escaping, and fail-safe diagnostics. |
| PATH-005 | P0 | A01 | Foreign absolute path syntax is never silently reinterpreted as a relative path on another OS. | Windows drive/UNC and Unix absolute paths are detected, marked host-bound/unresolved, and require explicit rebind or migration. |
| PATH-006 | P0 | A01 | Infrastructure, storage, MAF workspace, and MAF runtime containment share compatible semantics without creating reverse project references. | Contract tests feed identical logical/physical cases through each owner and prove equivalent outcomes. |
| PATH-007 | P1 | A01 | External-root aliases use a versioned platform-neutral format. | Aliases round-trip Unix roots, Windows roots, and multiple allowed roots without exposing physical paths to untrusted output. |
| PATH-008 | P0 | A01 | Traversal, rooted input, dot segments, empty segments, and invalid logical names fail consistently. | Named negative tests cover '/', '\', '..', drive roots, UNC, symlink candidates, and Unicode edge cases. |
| PATH-009 | P1 | A01 | All persisted logical path writers are deterministic and independent of host separators. | Golden serialization fixtures are byte-equivalent across Windows, Linux, and macOS. |
| PATH-010 | P1 | A01 | Path conversion is localized to typed boundaries rather than a repository-wide string replacement. | Architecture scan shows no blanket slash rewrite in URLs, project files, scripts, or opaque command content. |
| FS-001 | P0 | A02 | Logical identifiers use ordinal semantics; physical path equality uses a root/volume-specific filesystem policy. | Case-sensitive and case-insensitive volume fixtures or actual-host probes are covered, including macOS uncertainty. |
| FS-002 | P1 | A02 | All decision-bearing or persisted filesystem enumeration is explicitly sorted by normalized logical key. | Determinism tests randomize source enumeration and assert identical results. |
| FS-003 | P0 | A02 | Managed roots and trusted operations have one documented symlink/reparse policy applied to reads, writes, opens, process targets, and migrations. | Linux symlink and Windows reparse/junction tests prove containment; unsupported ambiguous cases fail closed. |
| FS-004 | P0 | A02 | Path checks and file operations minimize time-of-check/time-of-use gaps and document residual platform limits. | Security review accepts the implementation and failure cases cannot escape a managed root in test fixtures. |
| FS-005 | P0 | A02 | Catalog, storage, control-plane, key, and vault writes use a shared atomic same-volume commit pattern where applicable. | Crash/failure injection leaves either the previous complete generation or the new complete generation, never truncation. |
| FS-006 | P1 | A02 | Concurrent application processes coordinate writes with bounded cross-process locking or a transactional authority. | Two-process contention tests prove timeout, recovery, and no corruption. |
| FS-007 | P0 | A02 | Secret-bearing directories and files receive restrictive Unix modes and are verified after creation/migration. | Linux/macOS tests verify expected modes; hardening failure blocks secret use with actionable diagnostics. |
| FS-008 | P1 | A02 | Physical artifact filenames follow an application-level portable policy independent of Path.GetInvalidFileNameChars. | Golden cases produce the same physical name on all OSes while preserving original display names. |
| FS-009 | P1 | A02 | File watchers are treated as hints and recover through deterministic rescan/fingerprint or polling. | Overflow, duplicate, rename, and dropped-event tests converge to correct state. |
| FS-010 | P1 | A02 | Temporary files, locks, and migration markers are cleaned safely without following links or deleting outside owned roots. | Failure-injection and malicious-link fixtures prove bounded cleanup. |
| STO-001 | P0 | A03 | Windows, Linux, and macOS have explicit default roots for workspace, control-plane data, keys, logs/state, and temporary runtime artifacts. | Root matrix is documented and actual-host tests confirm writable, owned, non-repository locations. |
| STO-002 | P1 | A03 | Interactive-user and service/headless profiles can override roots without changing persisted logical locators. | systemd/launchd/service-user scenarios use explicit configuration and retain identical application semantics. |
| STO-003 | P0 | A03 | Storage locators and related JSON/database records are versioned and migrate legacy separator formats transactionally. | Migration can dry-run, back up, resume, roll back, and produce a redacted report. |
| STO-004 | P0 | A03 | Absolute workspace roots, storage roots, repository paths, and executable preferences are explicitly host-bound. | Foreign records become unresolved with a safe UX/API status rather than being executed or opened. |
| STO-005 | P0 | A03 | Database profile control-plane records preserve password decryptability while workspace roots are migrated/rebound. | Profile selection, restart, old-data read, and rollback tests pass. |
| STO-006 | P1 | A03 | Preferred application records are versioned by platform/host and never auto-launch a foreign executable path. | Cross-host import requires explicit confirmation/rebind and desktop launch remains disabled by default. |
| STO-007 | P0 | A03 | FileSystemStorageDriver Save/Replace semantics are atomic, conflict-aware, and consistent across processes. | Concurrent writes, revision conflicts, cancellation, and crash simulation pass on Windows/Linux/macOS. |
| STO-008 | P1 | A03 | Storage bootstrap authority compares canonical roots using the owning filesystem semantics and migration state. | Copied or rebound profiles select the intended authoritative storage and reject ambiguous duplicates. |
| STO-009 | P0 | A03 | A pre-migration backup and rollback path exists for control-plane files, storage catalog records, and path-bearing database data. | Operator rehearsal restores the previous application version and data without loss. |
| SEC-001 | P0 | A04 | Auto secret-provider selection never returns an unsupported provider. | Startup either registers a proven provider or fails fast with a non-secret remediation message. |
| SEC-002 | P0 | A04 | The macOS interactive profile has a real Keychain-backed secret implementation or an explicitly supported secure alternative. | Create/read/update/delete/restart/concurrency tests run on macOS and prove access-control behavior. |
| SEC-003 | P0 | A04 | The Linux interactive profile has a real Secret Service implementation with explicit session/availability semantics. | Tests cover available service, locked/unavailable service, and headless absence; no silent fallback occurs. |
| SEC-004 | P0 | A04 | Linux/macOS headless operation has an explicit secure provider that does not depend on an interactive keyring. | A configured certificate, remote vault, or externally supplied wrapping-key mode is documented and restart-tested. |
| SEC-005 | P0 | A04 | The Data Protection key ring is protected at rest in supported production profiles. | The selected protector is bootstrapped independently, restart-tested, and its files/keys meet permission policy. |
| SEC-006 | P0 | A04 | Key-ring protection has no circular dependency on secrets encrypted by that same key ring. | Architecture test/review traces bootstrap dependencies and detects cycles. |
| SEC-007 | P0 | A04 | The current plaintext Base64 file-vault master key is removed from production paths and cannot be selected by Auto. | Legacy development data has a named migration or explicit discard path; production startup rejects insecure configuration. |
| SEC-008 | P0 | A04 | Legacy DPAPI-protected data has a Windows-side transactional export/re-encryption path before Unix migration. | Dry-run, interruption, retry, verification, and rollback preserve every source record. |
| SEC-009 | P0 | A04 | Legacy Data Protection secret records and control-plane database passwords remain readable through migration and restart. | Golden fixtures from the old key ring decrypt before and after staged migration. |
| SEC-010 | P0 | A04 | Vault payloads and key metadata are versioned, atomic, cross-process safe, permission-hardened, and rotatable. | Rotation interruption/recovery tests prove no key/payload orphaning and old generation retention until commit. |
| SEC-011 | P0 | A04 | Secret values never enter logs, exceptions, receipts, migration reports, scanner output, or test artifacts. | Automated redaction tests and artifact scan pass with seeded sentinel secrets. |
| SEC-012 | P1 | A04 | Provider capability and health diagnostics reveal only provider type/state and remediation, never identifiers or values. | Readiness/UI/API snapshots are security-reviewed. |
| SEC-013 | P0 | A04 | Secret migration and deletion are auditable without exposing values and have explicit orphan cleanup semantics. | Staged reference cleanup and old-payload deletion are idempotent and tested. |
| PLAT-001 | P0 | A05 | Portability uses narrow purpose-owned ports/adapters rather than a broad IPlatformService or scattered feature branches. | Dependency and code scan show OS checks only in composition, root resolvers, and leaf native adapters unless justified. |
| PLAT-002 | P0 | A05 | Composition selects exactly one implementation for mandatory path/secret primitives and truthful zero-or-one implementations for optional desktop/runtime capabilities. | Startup tests cover every target profile and missing-dependency path. |
| PLAT-003 | P1 | A05 | Capability descriptors include availability, reason, remediation, execution boundary, and tested support level. | Workbench/settings/readiness surfaces consume descriptors without inferring support from OS name alone. |
| PLAT-004 | P1 | A05 | External package and native-service capabilities can be quarantined independently without blocking headless core startup. | FileTools, Secret Service, Keychain, terminal, and process-discovery failures degrade only their declared optional capabilities. |
| PLAT-005 | P0 | A05 | Current MAF/process ownership boundaries remain intact after platform composition changes. | Architecture tests preserve no reverse MAF-to-product/process semantic dependency. |
| HOST-001 | P0 | A06 | The Web host starts headlessly on Windows, Ubuntu, and macOS with desktop launch disabled. | Bounded startup/health/shutdown smoke tests pass from clean publish output. |
| HOST-002 | P1 | A06 | Framework-dependent publish is proven for win-x64, linux-x64, osx-x64, and osx-arm64; additional RIDs are claims only after proof. | Artifacts run outside the repository and include a support manifest. |
| HOST-003 | P1 | A06 | Linux systemd and macOS launchd runbooks define service user, roots, environment, restart, logs, permissions, upgrade, and rollback. | Runbooks are rehearsed or clearly mark unexecuted optional desktop steps. |
| HOST-004 | P1 | A06 | Installation logic separates cross-platform publish/configuration from Windows shortcuts and Unix service integration. | The existing PowerShell installer remains valid while Unix entry points do not duplicate security/path policy. |
| HOST-005 | P1 | A06 | Operational diagnostics report platform, configured roots, provider/capability state, and support profile without exposing secrets or unnecessary absolute paths. | Redacted diagnostic bundle is validated on each target OS. |
| DOC-001 | P1 | A06 | Developer and operator documentation no longer presents %LOCALAPPDATA%, PowerShell, or Windows desktop behavior as universal. | Documentation includes Linux/macOS prerequisites, limitations, migrations, and explicit unsupported features. |
| CI-001 | P0 | A07 | An active GitHub Actions workflow restores, builds, and runs the stable test gate on Windows, Ubuntu, and macOS. | All three jobs are required checks or otherwise protected by repository policy evidence. |
| CI-002 | P0 | A07 | Actual-host tests cover path, filesystem, storage, secret-provider selection, permissions, and headless startup. | Tests cannot pass solely through a mocked OS enum. |
| CI-003 | P0 | A07 | Linux and macOS restart tests prove protected state, storage catalogs, and migrated records remain readable. | At least one old-format fixture and one new-format fixture survive restart on each supported migration route. |
| CI-004 | P1 | A07 | Publish artifacts and service/headless smoke tests run from clean directories outside the repository. | No success depends on bin/obj, developer profile, global npm cache, or repository-relative mutable state. |
| CI-005 | P0 | A07 | Windows stable behavior remains green after every core portability phase. | No compatibility change is accepted with unexplained Windows regression. |
| CI-006 | P1 | A07 | Static scanning blocks new unowned OS checks, raw Windows-only tokens, unsafe absolute-path persistence, and insecure secret fallbacks. | Allowlist entries are narrow, reviewed, and source-linked. |
| CI-007 | P0 | A07 | Core Gate C4 produces a versioned handoff anchor and evidence pack that is sufficient to start the runtime bundle. | Runtime bundle remains blocked until C4 is GO and its source references are refreshed. |

## Status rules

- `Planned` during preparation.
- `In progress` only while the owning subbundle is active.
- `Solved` only with linked validation evidence and a GO gate.
- `Blocked` must name the gate/finding/dependency.
- A later source or evidence change reopens the requirement.
