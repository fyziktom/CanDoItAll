# A05 evidence report — platform composition, capabilities, and readiness

Date: 2026-08-09  
Subbundle: A05  
Gate: C3a pending independent review

## Outcome

A05 implementation is complete and eligible for independent C3a review. Composition now resolves one explicit typed host profile, projects existing path/filesystem/secret/FileTools facts into one non-authorizing capability snapshot, fails startup for unavailable mandatory capabilities, and degrades optional desktop/terminal/process-discovery capabilities independently.

The public snapshot exposes only stable IDs, typed state/reason/support values, bounded remediation text, dependency identity/version, execution boundary, profile, and observation time. It does not expose secret values or physical sensitive paths. Health, the development runtime endpoint, and `/api/runtime/capabilities` all resolve the same `IHostCapabilitySnapshotProvider`.

Actual macOS Keychain execution remains the separately tracked `MACOS-KEYCHAIN-VALIDATION-001` follow-up. The macOS profile is represented as `ActualHostUnverified`; A05 does not claim actual-host-verified Keychain support.

## Requirement evidence

| Requirement | Status before C3a | Evidence |
|---|---|---|
| PLAT-001 | Implemented | OS detection is isolated to `RuntimeHostFacts` in Composition; architecture tests reject `IPlatformService` and process-domain OS branches. Existing path, filesystem, secret, FileTools, MAF, and Processes owners remain unchanged. |
| PLAT-002 | Implemented | Profile resolution is strongly typed and rejects foreign-host, usage-profile, and non-Development Test combinations. DI tests prove one mandatory registration and zero-or-one optional desktop adapter. Both-host startup proves one ready snapshot. |
| PLAT-003 | Implemented | `HostCapabilityDescriptor` carries availability, reason, remediation, support level, dependency identity/version, execution boundary, support profile, and observation time. Redaction tests prove raw provider remediation is not copied. API/health/startup use the same provider. |
| PLAT-004 | Implemented | Linux startup is ready with FileTools desktop, desktop open, interactive terminal, and native process discovery unavailable. Their absence is visible and does not block mandatory core startup. Explicit strong secret providers retain their A04 fail-closed behavior. |
| PLAT-005 | Implemented | No project reference changed. No lower layer references Composition/Web. MAF and Processes semantics were not moved; the architecture test and CodeAnalytics cycle result are green. |

## Design and changed production surface

- `src/App/CanDoItAll.Composition/RuntimeHostProfiles.cs` owns typed profile configuration and the only new host OS selector.
- `src/App/CanDoItAll.Composition/HostCapabilities.cs` contains the descriptor contracts only.
- `src/App/CanDoItAll.Composition/HostCapabilitySnapshotProjector.cs` is the pure fact-to-descriptor projection.
- `src/App/CanDoItAll.Composition/HostCapabilityRuntime.cs` contains the provider, mandatory startup validator, exception, and health check.
- `src/App/CanDoItAll.Composition/RuntimeHostServiceCollectionExtensions.cs` resolves/registers exactly one profile and snapshot provider, then registers startup and health checks after existing modules.
- `src/App/CanDoItAll.Web/Program.cs` projects the same snapshot through development/runtime and API endpoints.
- Existing non-Web hosts now pass their explicit `IHostEnvironment`; no project or package dependency was added.

The initial single capability file was split after CodeAnalytics identified an oversized source file. Contracts, pure projection, and runtime wiring now have separate reasons to change. No partial class, service locator, registration-time `BuildServiceProvider`, broad platform manager, or duplicate path/secret/process implementation was introduced.

## Failing-first and characterization record

| Stage | Result | Meaning |
|---|---:|---|
| A05 entry focused characterization | 460/460 | Stable pre-A05 Composition/Capability/Readiness/Architecture baseline (`artifacts/unix-portability/A05/entry/A05-entry-focused.trx`). |
| Failing-first compile | Expected CS0246 | New typed profile/capability tests could not compile before production contracts existed. |
| Implementation iteration | 12/12 then 15/15 | Tests exposed and closed a profile property-shadowing defect and stale composition callers. |
| Linux first run | 481/482 | Exposed a test-only missing explicit Unix Development Data Protection profile; production correctly failed closed. Fixture corrected without weakening production policy. |

## Final validation

| Host | Validation | Result | Evidence |
|---|---|---:|---|
| Windows | Required A05 Unit filter | 482/482 | `artifacts/unix-portability/A05/final/windows/A05-windows-focused-final.trx` |
| Linux Docker | Required A05 Unit filter | 482/482 | `artifacts/unix-portability/A05/final/linux/A05-linux-focused-final.trx` |
| Windows | Affected Memory host composition tests | 14/14 | `artifacts/unix-portability/A05/final/windows/A05-windows-memory-hosting.trx` |
| Windows | Full Unit regression | 5,551/5,551 | `artifacts/unix-portability/A05/final/windows/A05-windows-full-unit-authoritative.trx` |
| Windows | Full solution Release build | 0 warnings, 0 errors | `artifacts/unix-portability/A05/final/windows/A05-windows-build-after-split.log` |
| Linux Docker | Web Release build | 0 warnings, 0 errors | `artifacts/unix-portability/A05/final/linux/A05-linux-web-build.log` |
| Windows actual host | User-reported launch command shape | HTTP 200; `WindowsInteractive`; ready; DPAPI/Stable; 7 descriptors; empty stderr | `artifacts/unix-portability/A05/final/windows/A05-windows-startup-final.out.log`; `A05-windows-health.json`; `A05-windows-capabilities.json` |
| Linux Docker | No D-Bus, Secret Service, or external wrapping key | HTTP 200; `LinuxInteractive`; ready; LocalUserFile/BasicLocal; 7 descriptors; empty stderr | `artifacts/unix-portability/A05/final/linux/A05-linux-startup-final.out.log`; `A05-linux-health.json`; `A05-linux-capabilities.json` |
| Static | Portability scan including untracked files | PASS; 4,794 files; 26,920 findings | `artifacts/unix-portability/A05/final/os-branch-scan.json` |
| Static | Secret artifact scan | 31/31 candidates scanned; 0 coverage gaps; 36 occurrences of the same six classified synthetic fingerprints | `artifacts/unix-portability/A05/A05-secret-scan.json`; `A05-secret-scan-classification.md` |
| Static | `git diff --check` | Clean except the recorded CRLF-to-LF notice for the traceability CSV | command transcript |

Docker proof used Docker Engine `linux 29.6.2`, `mcr.microsoft.com/dotnet/sdk:10.0`, an isolated Git-file-list source volume, and the existing isolated NuGet cache. The Linux startup used an explicit in-memory database and Development-only unprotected Data Protection key-ring mode so the test measured platform composition and vault readiness rather than an external PostgreSQL/certificate prerequisite. Production Unix Data Protection defaults remain fail-closed.

## Static scan classification

The only new production OS-branch findings are the three `OperatingSystem.IsWindows/IsLinux/IsMacOS` calls in `RuntimeHostFacts.DetectCurrent`, the approved Composition selector boundary. Secret-provider and external-tool lexical findings identify typed descriptors/probes and do not indicate a second implementation. Test findings are explicit profile-matrix and architecture assertions. The required architecture test found no broad platform service or process-semantic OS branch.

## C# Architecture Gate Result

Status: Pass

### Findings

| Severity | Finding | Evidence | Required action |
|---|---|---|---|
| Info | CodeAnalytics cannot resolve the concrete type of `services.AddSingleton(profile)` and reports DI0002. | Snapshot `snap-20260809235026-f67a0cd1`; source uses the typed generic overload inferred from `ResolvedRuntimeHostProfile`; registration cardinality test passes on Windows and Linux. | None; analyzer limitation is non-blocking. |
| Info | Descriptor and projector exceed the analyzer's low member-count heuristic. | Contracts are immutable typed data; projector contains only pure mapping helpers and has one reason to change. The former oversized combined file was split. | Reassess only if new responsibilities are added. |
| Existing | `RuntimeHostServiceCollectionExtensions.cs` remains a large composition-root file. | No partial was added; A05 added one bounded registration method and extracted capability behavior into dedicated files. | Track under existing modularization work; not an A05 boundary violation. |

### Dependency direction

No `.csproj` file changed. Fresh scoped CodeAnalytics snapshot `snap-20260809235026-f67a0cd1` reports two projects, 31 scoped types, nine DI registrations, zero blocking errors, and zero cycles. Composition depends on existing leaf owners; no lower layer depends on Composition or Web.

### Partial-class policy

No new partial class or nested architecture boundary exists.

### Testability proof

Profile resolution and snapshot projection are pure tests with negative host/profile/security cases. Registration, startup failure, optional degradation, redaction, public shape, and architecture constraints have focused tests. Actual Windows and Linux host startup consume the production composition root.

### Closure decision

Primary architecture review passes. A05 may proceed to the required independent C3a review. A06 remains blocked until that review records GO and canonical bundle bookkeeping is refreshed.

## Residual risks

- Genuine macOS Keychain execution is intentionally deferred to `MACOS-KEYCHAIN-VALIDATION-001`; the public descriptor remains `ActualHostUnverified` until it passes.
- Unix `LocalUserFile` is intentionally `BasicLocal`, not strong protection; stronger explicit providers remain available and fail closed when unavailable.
- Terminal presentation and native process discovery are currently truthful optional-unavailable descriptors; their concrete portable adapters belong to later runtime/tool work rather than A05.

## C3a remediation evidence

The independent review recorded C3a NO-GO on three PLAT-003 truthfulness gaps. A05 was reopened only for those findings; A06 remained blocked.

### Closed implementation findings

1. The Web application now exposes an operator-facing settings page at `/settings/runtime-capabilities`, reachable from the shell utility rail. It consumes the same `IHostCapabilitySnapshotProvider` as startup, health, and `/api/runtime/capabilities`, and renders typed availability, reason, remediation, support level, execution boundary, registration state, implementation identity, and optional truthful version. It does not infer support from the OS.
2. Infrastructure now owns `IPathFoundationReadinessProbe`. It resolves every configured purpose root, performs a create-new/write/delete probe in each owner root, and validates every root through the physical filesystem policy. Composition consumes only the typed, non-sensitive result. A regression proves that an unusable non-temporary root makes the descriptor non-available and blocks startup without exposing a root or exception message.
3. `DependencyVersion` was replaced by `ImplementationRegistration`, `ImplementationId`, and optional `ImplementationVersion`. Provider names are identities, absent adapters are `NotRegistered` with null identity/version, and blank/unknown versions are omitted rather than fabricated.

### Refreshed proof

| Host | Validation | Result | Evidence |
|---|---|---:|---|
| Windows | Required A05 Unit filter | 488/488 | `artifacts/unix-portability/A05/remediation/windows/A05-windows-focused-remediation.trx` |
| Linux Docker | Required A05 Unit filter | 488/488 | `artifacts/unix-portability/A05/remediation/linux/A05-linux-focused-remediation.trx` |
| Windows | Runtime-capability component slice | 3/3 | `artifacts/unix-portability/A05/remediation/windows/A05-windows-components-remediation.trx` |
| Linux Docker | Runtime-capability component slice | 3/3 | `artifacts/unix-portability/A05/remediation/linux/A05-linux-components-remediation.trx` |
| Windows actual host | Full Unit regression | 5,557/5,557 | `artifacts/unix-portability/A05/remediation/windows/A05-windows-full-unit-remediation-authoritative.trx` |
| Windows | Full solution Release build | 0 warnings, 0 errors | `artifacts/unix-portability/A05/remediation/windows/A05-windows-solution-build-remediation.log` |
| Linux Docker | Web Release build | 0 warnings, 0 errors | `artifacts/unix-portability/A05/remediation/linux/A05-linux-web-build-remediation.log` |
| Windows actual host | Exact reported launch command shape | Health/API/UI HTTP 200; `WindowsInteractive`; ready; owner paths ready; DPAPI/Stable; truthful implementation metadata; empty stderr | `artifacts/unix-portability/A05/remediation/windows/A05-windows-startup-remediation.out.log`; `A05-windows-health-remediation.json`; `A05-windows-capabilities-remediation.json`; `A05-windows-capability-ui-remediation.html` |
| Linux Docker | No D-Bus, Secret Service, or external wrapping key | Health/API/UI HTTP 200; `LinuxInteractive`; ready; owner paths ready; LocalUserFile/BasicLocal; optional capabilities independently unavailable; empty stderr | `artifacts/unix-portability/A05/remediation/linux/A05-linux-startup-remediation.out.log`; `A05-linux-health-remediation.json`; `A05-linux-capabilities-remediation.json`; `A05-linux-capability-ui-remediation.html` |
| Static | Portability scan including untracked files | PASS; 4,800 files; 26,991 findings; complete/non-truncated | `artifacts/unix-portability/A05/remediation/A05-portability-scan-remediation.json`; `A05-changed-source-portability-classification.md` |
| Static | Schema-3 secret scan | 56 candidates accounted; 55 text scanned including both HTML captures; 0 oversized/non-text/unreadable; 64 occurrences of the same six synthetic TRX fingerprints; scanner regression 4/4 | `artifacts/unix-portability/A05/remediation/A05-secret-scan-remediation.json`; `artifacts/unix-portability/A05/A05-secret-scan-classification.md` |
| Static | CodeAnalytics | `snap-20260810005847-cefe425c`; 3 projects; 45 types; 63 registrations; 0 cycles; 0 blocking errors; 0 Error findings | MCP snapshot record |
| Bundle | Portable validator, checksums deferred | 293 files; 0 errors; 0 warnings | command transcript |
| Static | `git diff --check` | Clean except the already-recorded traceability CSV EOL notice | command transcript |

The first sandboxed full-Unit attempt retained one expected environment-only failure because the restricted Codex sandbox account has read-only access to the actual Windows user’s AppData purpose roots. The authoritative rerun used the real current-user permissions required by that test and passed 5,557/5,557. No product policy or test was weakened.

### Remediation disposition

Primary remediation is complete and eligible for the bounded independent C3a re-review requested in `reviews/17-a05-independent-review.md`. C3a remains NO-GO and A06 remains blocked until that reviewer records GO.

## Final independent disposition

The bounded independent re-review in `reviews/17-a05-independent-review.md` recorded **C3a GO** after verifying all three remediations and the refreshed evidence. The post-review scanner follow-up added `.html` as a text artifact with a regression; the final schema-3 scan now includes both UI captures directly. A05 is complete and A06 is eligible.
