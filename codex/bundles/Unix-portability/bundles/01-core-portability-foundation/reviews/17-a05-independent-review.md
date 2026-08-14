# A05 independent Gate C3a review

Date: 2026-08-09  
Reviewer: independent C3a review  
Scope: A05 / PLAT-001 through PLAT-005 only

## Decision

**C3a: NO-GO.** A06 remains blocked.

The composition boundary, registration cardinality, optional degradation, MAF/Processes ownership, both-host execution proof, and macOS support labeling are acceptable. Three PLAT-003/readiness findings remain blocking because the current public capability contract is incomplete or misleading at the point where operators consume it.

## Blocking findings

### P1 — PLAT-003/A05-T08 has no UI consumer

PLAT-003 requires a Workbench/settings/readiness surface to consume capability descriptors, and A05-T08 explicitly requires consistent UI/API/readiness snapshots. The product reference audit finds `IHostCapabilitySnapshotProvider` and `HostCapabilitySnapshot` only in Composition, `Program.cs`, and the focused unit test. `Program.cs` publishes the provider through the Development endpoint and `/api/runtime/capabilities`, and registers `/health` (`src/App/CanDoItAll.Web/Program.cs:112`, `src/App/CanDoItAll.Web/Program.cs:875`, `src/App/CanDoItAll.Web/Program.cs:881`), but no Razor, Workbench, or settings component consumes either the provider or that API. The focused test file likewise has no component/UI assertion.

This does not satisfy `requirements/01-normalized-requirements.md:56` or `subbundles/05-platform-composition-capabilities-and-readiness/tasks.md:52`. It also contradicts the architecture rule that UI strategies consume the descriptor (`architecture/05-composition-and-capabilities.md:54`). The evidence report proves API/startup/health consistency but silently omits the required UI leg (`reviews/16-a05-evidence-report.md:21`).

Required action: add a bounded operator-facing Workbench/settings/readiness UI projection of the same provider or public contract. It must render typed availability, reason, remediation, boundary, and support level; it must not derive support from the OS name. Add component coverage for unavailable optional capabilities, `BasicLocal`, and `ActualHostUnverified`, plus both-host surface proof and redaction checks.

### P1 — Mandatory path/filesystem descriptors are asserted, not observed

`HostCapabilitySnapshotProjector.Create` always emits `ControlPlanePaths` and `PhysicalFileSystem` as `Available` and `Stable` (`src/App/CanDoItAll.Composition/HostCapabilitySnapshotProjector.cs:28`). The runtime provider accepts `IControlPlanePathResolver` and `IPhysicalFileSystemPathPolicyFactory`, but only null-checks them and passes no path/filesystem probe state to the projector (`src/App/CanDoItAll.Composition/HostCapabilityRuntime.cs:19`). Readiness is then calculated from those unconditional descriptors (`src/App/CanDoItAll.Composition/HostCapabilitySnapshotProjector.cs:73`).

DI presence proves selection/cardinality; it does not prove that the selected control-plane root is usable, private, migration-safe, or otherwise ready. Existing Web bootstrap may fail later for some root/database faults, but that does not make these public descriptors truthful, and it does not prove the same invariant for every host that registers `HostCapabilityStartupValidator`. The only focused mandatory-failure test covers an absent secret-vault probe (`tests/Unit/CanDoItAll.Tests.Unit/RuntimeHostPlatformCapabilityTests.cs:99`).

Required action: consume an owner-produced, non-sensitive path/filesystem readiness result rather than probing or duplicating Infrastructure policy in Composition. Until that result exists, report the capabilities as unverified instead of available. Add failure tests showing an unusable mandatory root makes the descriptor non-available and blocks the startup validator without disclosing the path; preserve actual Windows/Linux startup proof.

### P1 — `dependencyVersion` is not a truthful version field

The public contract names the field `DependencyVersion` (`src/App/CanDoItAll.Composition/HostCapabilities.cs:65`), but the projector stores the secret provider enum name in it (`src/App/CanDoItAll.Composition/HostCapabilitySnapshotProjector.cs:121`) and stores `not-registered` for absent terminal/process capabilities (`src/App/CanDoItAll.Composition/HostCapabilitySnapshotProjector.cs:191`). The actual-host artifacts therefore publish values such as `Dpapi`, `LocalUserFile`, and `not-registered` under `dependencyVersion`. The profile-matrix test intentionally locks in the provider-name mismatch (`tests/Unit/CanDoItAll.Tests.Unit/RuntimeHostPlatformCapabilityTests.cs:147`).

Those values can be useful dependency identities or states, but they are not versions. This violates the typed descriptor truthfulness required by PLAT-003 and A05-T03 and makes the API schema misleading.

Required action: separate typed dependency identity from an optional truthful version, or rename/model the field so its meaning matches every capability. Do not encode absence as a version. Add public-contract tests for provider identity, known/unknown version, and unregistered state.

## Requirement and architecture disposition

| Area | Result | Notes |
|---|---|---|
| PLAT-001 | Pass | New OS detection is confined to the Composition profile resolver. No broad platform service, conditional compilation, partial-class split, or registration-time service locator was found. |
| PLAT-002 | Pass with PLAT-003 blocker above | The service collection test proves one resolver, filesystem policy factory, vault, profile, and snapshot provider, with at most one desktop launcher. Foreign host/profile and Test-environment mismatches fail explicitly. |
| PLAT-003 | **Fail** | UI consumption is absent, two mandatory facts are unprobed, and dependency metadata is mislabeled. Redaction and typed state/reason/support modeling otherwise pass. |
| PLAT-004 | Pass | Windows/Linux artifacts show optional desktop/terminal/process-discovery failures do not block mandatory core readiness. Explicit strong secret-provider behavior remains fail-closed. |
| PLAT-005 | Pass | No project file changed. The A05 diff introduces no MAF/process semantic ownership move or reverse Composition/Web dependency. The scoped CodeAnalytics snapshot reports no cycle. |

The CodeAnalytics snapshot `snap-20260809235026-f67a0cd1` is internally consistent with the report: two scoped projects, 31 types, nine discovered registrations, no cycles, and no blocking diagnostic. Its unresolved `AddSingleton(profile)` item is an analyzer limitation; the runtime cardinality test proves the concrete registration. Because the snapshot is intentionally scoped, the no-project-file-change audit and the earlier full-graph proof remain the evidence for repository-wide dependency direction.

## Independent evidence checks

- Parsed final TRX counters: Windows focused 482/482, Linux focused 482/482, Memory hosting 14/14, and Windows full Unit 5,551/5,551, all with zero failed or skipped tests.
- Re-ran `RuntimeHostPlatformCapabilityTests` from the frozen Release output: 22/22 passed.
- Build logs end with zero warnings and zero errors for the Windows solution and Linux Web build.
- Actual-host snapshots contain seven descriptors, no full physical paths or secret values, Windows DPAPI/Stable, and Linux LocalUserFile/BasicLocal. Both health captures are `Healthy`.
- Secret scan schema 3 covers 31/31 candidate text artifacts with no coverage gaps; all 36 findings are the six previously classified synthetic fingerprints and findings are metadata-only.
- The portability scan is complete and non-truncated. A05-owned OS, secret-provider, and external-tool hits match the documented selector/descriptor/probe boundaries. The full scan still reports baseline `Unassigned` ownership, so future gate evidence should prefer an explicit changed-source delta classification rather than labeling the raw full-repository inventory `PASS` without that qualifier.
- Portable validator rerun with checksums skipped: 292 files, zero errors, zero warnings. Final index/checksum regeneration remains normal post-review bookkeeping.
- `git diff --check` has only the recorded traceability CSV EOL notice; no `.csproj`, solution, props, or targets file changed.

## Non-blocking residuals

- Genuine macOS Keychain execution remains operator-deferred under `MACOS-KEYCHAIN-VALIDATION-001`. The macOS Keychain descriptor is `ActualHostUnverified` with `ActualHostValidationDeferred`; no verified-support claim was found. This is not a C3a blocker under the approved deferral.
- Unix `LocalUserFile` remains deliberately `BasicLocal`; startup logs preserve the same-user warning and the public descriptor does not call it Strong.
- Terminal presentation and native process discovery remain truthful optional-unavailable placeholders pending their later owner subbundles.
- The pre-existing oversized composition-root file remains maintainability debt, not an A05 boundary regression.

## Re-entry evidence required

Re-review should be bounded to the three blockers above. Refresh the focused Windows/Linux suite, UI/component proof, actual-host capability captures, both builds, changed-source static classification, secret scan, CodeAnalytics snapshot, and portable validation. Do not advance A06 until an independent C3a review records GO.

## Re-review

Date: 2026-08-09  
Scope: bounded review of the three C3a blockers above  
Decision: **C3a GO**

All three blockers are closed on the stable remediation snapshot.

1. **Operator UI consumption — closed.** `RuntimeCapabilities.razor` is a real operator-facing settings page at `/settings/runtime-capabilities`, is reachable from the shell utility rail, and directly injects the same `IHostCapabilitySnapshotProvider` used by startup, health, and API. It renders the reported availability, reason, remediation, support level, execution boundary, registration state, identity, and optional version. Its only use of the OS value is descriptive; support is never inferred from it. Component tests cover optional-unavailable behavior, `BasicLocal` without a Strong claim or path disclosure, and `ActualHostUnverified` with the macOS follow-up visible. Both Windows and Linux captured the rendered page successfully.
2. **Owner-produced path/filesystem readiness — closed.** Infrastructure now owns `IPathFoundationReadinessProbe`; Composition consumes only its typed `Ready`/`Unavailable` and reason values. The probe resolves every owner purpose root, performs create-new/write/flush/delete validation in every resolved root, and applies the existing physical-path policy to every root. Failures are reduced to typed reasons; paths and exception text do not cross the boundary. The focused regression proves an unusable second owner root fails even when runtime-temporary remains usable, and the projected failure makes mandatory readiness false and causes `HostCapabilityStartupValidator` to fail without disclosing the root.
3. **Implementation metadata truthfulness — closed.** The misleading `DependencyVersion` field is gone. `ImplementationRegistration` is typed, `ImplementationId` carries identity, and nullable `ImplementationVersion` contains an actual assembly version only when one exists. Missing optional adapters are `NotRegistered` with null identity/version; blank or unknown versions remain null. The actual-host API and UI captures reflect this schema.

### Evidence reconciliation

- Parsed TRX counters match the refreshed report: Windows focused 488/488, Linux focused 488/488, Windows UI 3/3, Linux UI 3/3, and authoritative Windows full Unit 5,557/5,557, with no failed or skipped results.
- Independent targeted reruns from frozen Release outputs passed: `RuntimeHostPlatformCapabilityTests` 28/28 and `RuntimeCapabilitiesPageTests` 3/3.
- Windows solution and Linux Web Release build logs end with zero warnings and zero errors. Both startup stderr files are empty; health captures are `Healthy`; API captures contain seven typed descriptors with owner paths ready and truthful implementation metadata. Windows reports DPAPI/Stable; Linux reports LocalUserFile/BasicLocal and independently unavailable optional capabilities.
- The changed-source portability classification accounts for the remediation’s path/atomic, secret-provider, external-tool, UI, and pre-existing shell lexical hits. The underlying scan is complete and non-truncated at 4,800 files and 26,991 findings.
- Schema-3 secret evidence accounts for all 56 candidates: 53 scanned text files, two UI HTML captures categorized by the current suffix-based tool as non-text, and one scanner control output; zero oversized or unreadable text files. All 64 stored findings are the same six classified synthetic fingerprints and occur only in TRX files. Because `.html` is textual despite that tool classification, I independently ran the scanner's exact seven rule patterns over both UI captures and found zero matches; a separate full-path/secret-token search also found none.
- CodeAnalytics snapshot `snap-20260810005847-cefe425c` reconciles: three scoped projects, 45 types, 63 registrations, zero cycles, no Error finding, and no blocking diagnostic. It discovers both the singleton owner probe and singleton capability provider. The existing factory-analysis notices and inferred `AddSingleton(profile)` warning are non-blocking analyzer limitations.
- No project/solution/props/targets file changed. No service locator, new OS branch, broad platform abstraction, reverse dependency, or new partial boundary was introduced by remediation.
- Portable validator rerun with checksums skipped: 293 files, zero errors, zero warnings. `git diff --check` reports only the already-recorded traceability CSV EOL notice.

### Residuals and handoff

- Add `.html` to the artifact scanner's text suffixes, with a regression, before relying on it for future captured UI responses. This does not block this gate because both current captures were independently scanned with the same rules and contained no disclosure.
- Genuine macOS Keychain execution remains the operator-deferred `MACOS-KEYCHAIN-VALIDATION-001` follow-up. Its descriptor remains `ActualHostUnverified`/`ActualHostValidationDeferred`; no verified-support claim exists, so it is not a C3a blocker.
- Unix LocalUserFile remains deliberately `BasicLocal`, and terminal/native process discovery remain optional-unavailable pending their owner work.

The A05 C3a exit condition is satisfied. Canonical status, gate log, handoff, bundle index, and checksums may now be refreshed, after which A06 is eligible to start.
