# A06 independent Gate C3b/Hosting review

Date: 2026-08-09  
Reviewer: independent C3b/Hosting review  
Scope: A06 / HOST-001 through HOST-005 and DOC-001 only

## Decision

**C3b/Hosting: NO-GO.** A07 remains blocked.

The typed support manifest, bounded macOS claims, four framework-dependent publishes, direct sibling-library development bridge, Windows/Linux lifecycle behavior, Unix release activation, redaction, and dependency direction are substantially sound. Two implementation findings and one actual-host provenance gap still prevent A06 closure.

## Blocking findings

### P1 — HOST-003 does not bind a macOS system daemon to the declared service account

The launchd template contains no `UserName` or `GroupName` keys (`tools/install/unix/com.candoitall.web.plist.in:4`). The runbook nevertheless offers installation as a dedicated system daemon in `/Library/LaunchDaemons` (`docs/operations/headless-web-host.md:86`, `docs/operations/headless-web-host.md:90`) and tells the operator to use the `system` domain (`docs/operations/headless-web-host.md:100`). A system-domain LaunchDaemon without an explicit user identity runs with the default system identity, normally root. That contradicts HOST-003's required service-user boundary and the runbook's dedicated-account guidance.

This remains blocking even though macOS actual-host execution is correctly deferred: A06 owns the static launchd/service contract, while A07 owns genuine execution of that contract.

Required action: either provide a distinct LaunchDaemon template that requires validated `UserName`/`GroupName` substitutions and owned purpose roots, or scope the existing template strictly to a per-user LaunchAgent and remove the unsupported system-daemon instructions. Preserve a separately explicit headless-daemon route if that is the intended supported deployment. Add static rendering assertions for the selected identity model and keep secrets out of the plist.

### P1 — HOST-005 operations diagnostics do not report configured roots

HOST-005 requires operational diagnostics to report configured roots without exposing unnecessary absolute paths. `RuntimeOperationsSnapshot` contains only overall state, a database-ready flag, deployment support, and `HostCapabilitySnapshot` (`src/App/CanDoItAll.Composition/RuntimeDeploymentSupport.cs:60`). The nested host snapshot contains only profile/OS/interactive/readiness metadata plus capability descriptors (`src/App/CanDoItAll.Composition/HostCapabilities.cs:89`). Those descriptors collapse all purpose roots into the aggregate `ControlPlanePaths` and `PhysicalFileSystem` capabilities.

The retained Windows and Linux `/api/runtime/operations` captures consequently prove aggregate path readiness but contain no typed inventory of configured workspace, control-plane, Data Protection, state, logs, or runtime-temporary roots and no redacted indication of how each root was selected. The runbook accurately describes the current endpoint as reporting only `path-readiness` (`docs/operations/headless-web-host.md:82`), which confirms the normalized `configured roots` requirement was narrowed in implementation rather than satisfied.

Required action: project an owner-produced, redacted purpose-root diagnostic contract. At minimum it should identify each typed purpose and its individual readiness/configuration source without returning the physical path, environment value, host-binding identifier, or exception text. Use the same owner facts already used for startup readiness; do not duplicate path resolution or probing in Composition. Add contract/redaction tests plus refreshed Windows and Linux operations captures.

### P1 evidence blocker — the frozen actual-host package does not durably prove the named Ubuntu host

HOST-001 and the validation matrix require Windows and Ubuntu actual-host proof, and the universal gate requires the exact command/exit code plus OS, profile, architecture, and tool versions. The Linux operations captures prove `LinuxHeadless`, `linux-x64` support metadata, Ready state, and the expected LocalUserFile/`BasicLocal` capability. The startup logs prove execution outside the repository, restart/rollback, and clean shutdown. They do not record the container image reference/digest, `/etc/os-release`, architecture, runtime version, UID/GID, launch commands, or command exit codes. The evidence report's UID and Linux-host statement (`reviews/18-a06-evidence-report.md:37`) is therefore not independently traceable to the retained artifacts.

A read-only inspection during this review found the currently local `mcr.microsoft.com/dotnet/aspnet:10.0` image to be Ubuntu 24.04.4 x64 with .NET runtime 10.0.10, but no retained artifact binds that image ID to the three captured A06 launches. Current machine state cannot substitute for durable gate provenance.

Required action: refresh or augment the actual-host proof with a redacted provenance record tying the executed artifact and container/host to its immutable image reference or ID, OS release, architecture, .NET runtime, effective UID/GID, relevant exact commands, and exit statuses. Do the equivalent bounded provenance capture for Windows. Do not capture environment dumps, connection strings, certificates, or physical purpose-root values.

## Requirement disposition

| Requirement | Result | Review disposition |
|---|---|---|
| HOST-001 | **Fail — evidence** | Windows and Linux behavior is successful and bounded, but the frozen package does not independently establish that the retained Linux run is the required Ubuntu actual host. |
| HOST-002 | Pass | All four framework-dependent RID publishes completed outside the checkout, contain the Web payload and identical `runtime-support.json`, and report 68 Release local-library references with zero Debug references. macOS targets remain `ActualHostUnverified`. |
| HOST-003 | **Fail — implementation** | Linux systemd ownership/hardening and lifecycle guidance are adequate; the launchd system-daemon route lacks a service-account binding. |
| HOST-004 | Pass | Unix scripts own artifact installation/activation only, do not elevate or select security/path policy, preserve the Windows installer boundary, validate release IDs, atomically replace state files, and keep rollback non-destructive and idempotent. |
| HOST-005 | **Fail — implementation** | Platform/profile, support, database startup state, provider/capability state, and aggregate path readiness are redacted, but configured-purpose-root reporting is missing. |
| DOC-001 | Pass subject to HOST-003 correction | Developer/operator docs distinguish Windows, XDG/Linux, and macOS behavior and state prerequisites, limitations, rollback, migration caution, unsupported desktop capabilities, and both macOS deferrals. The system-daemon identity instructions must be corrected with HOST-003. |

## Architecture and dependency review

- `CanDoItAll.Composition` owns the versioned support manifest and combines existing owner-produced facts without introducing a broad platform service or a second database/path/vault probe. Web remains the endpoint/composition root.
- The codec requires the exact four target declarations and exact three baseline profiles, rejects schema/product/profile drift, and requires both `A07-MACOS-HEADLESS-ACTUALHOST-001` and `MACOS-KEYCHAIN-VALIDATION-001`. Both macOS RIDs and the macOS headless profile are `ActualHostUnverified`; no verified macOS claim was found.
- Independent MSBuild evaluation confirmed the default local mode removes the corresponding NuGet items and resolves representative Web Components references and FileTools integration references to the sibling projects. Explicit `UseLocalCanDoItAllLibraries=false` restores those package references. The sibling `ShouldUnsetParentConfigurationAndPlatform=false` changes and Release logs support configuration propagation; published local-library references are Release-only.
- CodeAnalytics snapshot `snap-20260810030857-d2eab7f6` reconciles to three scoped projects, 1,732 types, 13,406 members, 135 registrations, no Error finding, and no Error diagnostic. Its unresolved concrete-profile `AddSingleton` diagnostic is an analyzer limitation already covered by runtime registration tests. Two reported type cycles are confined to test helpers and do not cross production project boundaries.

## Independent evidence checks

- Parsed frozen counters reconcile to Windows focused 31/31, Linux focused 31/31, and authoritative Windows full Unit 5,560/5,560, all executed and passing. The five isolated narrative reruns are each 1/1.
- Windows, Components, and FileTools Release build logs end with zero warnings and zero errors. The four publish rows contain the required payload, source-matching manifest hash, and no Debug local output.
- Windows operations captures report `WindowsHeadless`, DPAPI/Stable, Ready, migrations ready, and desktop/terminal/process capabilities unavailable without blocking core. Linux reports `LinuxHeadless`, LocalUserFile/BasicLocal, the same optional degradation, and Ready across install, upgrade, and rollback.
- Operations JSON contains no physical purpose-root values, connection strings, secret values, vault paths, or host-binding IDs. Schema-3 scanning accounts for 46 candidates, 45 scanned text artifacts plus one control output, two private sentinels with zero matches, and only the six classified synthetic fingerprints in the two full-suite TRXs.
- The portability inventory is complete and classified at 27,045/27,045 with zero unclassified findings. The changed-source review correctly identifies script permission/atomic operations and the sibling FileTools bridge as intentional.
- Portable bundle validation with checksums skipped passed after this review file was added: 297 files, zero errors, zero warnings. Final index/checksum regeneration is normal post-review bookkeeping.
- `git diff --check` reports only the already-recorded traceability CSV line-ending notice.

## Non-blocking residuals

- `A07-MACOS-HEADLESS-ACTUALHOST-001` remains the correct next-platform proof boundary, and `MACOS-KEYCHAIN-VALIDATION-001` remains separately operator-deferred. Neither is an A06 blocker because every current macOS claim is explicitly unverified.
- `systemd-analyze` was unavailable. The present directive contract, complete rendering, POSIX syntax checks, and actual launcher lifecycle are reasonable A06 evidence; add real unit loading/verification at the later actual-service gate.
- All three Linux startup logs contain optional native GSSAPI loader noise (`Cannot load library` / `Error: libgssapi_krb5.so.2`) and the HTTP-only host logs an HTTPS-redirection warning. The process still reaches Ready and shuts down normally, so these do not independently invalidate the lifecycle proof, but their cause/prerequisite or suppression should be resolved before final C4 operational support closure.
- The installer validates normal install/upgrade/rollback behavior but does not attest crash interruption at every filesystem mutation. Preserve unique release IDs and retained prior releases; broader installer transaction hardening can remain follow-up work unless failure injection exposes active-release corruption.

## Re-entry evidence required

Re-review may be bounded to the three blockers above. Provide the corrected launchd identity contract and static tests, redacted per-purpose configured-root diagnostics with both-host captures, and durable Windows/Ubuntu provenance. Refresh affected focused tests, builds/startups, static/secret scans, CodeAnalytics snapshot if C# shape changes, portable validation, and the primary report/handoff. Do not advance A07 until an independent C3b/Hosting review records GO.

## Bounded re-review — 2026-08-10

### Decision

**C3b/Hosting: GO.** The three blocking findings above are closed. A07 may advance after the normal canonical gate/status, bundle index, and checksum bookkeeping is regenerated; this review does not perform that bookkeeping.

### Blocker closure

1. **HOST-003 launchd service identity — closed.** The supplied plist is now unambiguously a system LaunchDaemon and requires explicit `UserName` and `GroupName` substitutions (`tools/install/unix/com.candoitall.web.plist.in:7-10`). The runbook distinguishes it from a per-user LaunchAgent, requires a dedicated service account and owned roots, requires all tokens to be resolved, and retains installation/loading in the system domain (`docs/operations/headless-web-host.md:86-92`). The focused unit contract asserts both identity keys/tokens and rejects an embedded root identity (`tests/Unit/CanDoItAll.Tests.Unit/RuntimeHostPlatformCapabilityTests.cs:514-524`). The refreshed static artifact records valid launchd XML, both required service-identity keys, six rendered tokens, and zero unresolved tokens. This closes the static A06 service-user boundary without claiming actual macOS execution.
2. **HOST-005 configured-root diagnostics — closed.** Infrastructure now owns a typed seven-purpose inventory and resolves Workspace, ControlPlane, DatabaseProfiles, DataProtectionKeys, State, Logs, and RuntimeTemporary through the existing workspace/control-plane owners (`src/Foundation/CanDoItAll.Infrastructure/Readiness/PathFoundationReadiness.cs:29-38`, `src/Foundation/CanDoItAll.Infrastructure/Readiness/PathFoundationReadiness.cs:112-131`). Each owner-resolved root is independently create-new/write/flush/delete probed before readiness is reported (`src/Foundation/CanDoItAll.Infrastructure/Readiness/PathFoundationReadiness.cs:56-107`, `src/Foundation/CanDoItAll.Infrastructure/Readiness/PathFoundationReadiness.cs:137-159`). Composition projects those owner-produced facts without re-resolving paths (`src/App/CanDoItAll.Composition/HostCapabilitySnapshotProjector.cs:90`). Tests cover seven distinct successful roots, an unusable non-temporary root while the other six remain Ready, configuration-source truthfulness, and operations redaction. Independent parsing of all two Windows and three Ubuntu operations captures found exactly seven unique purposes, all Ready, with only typed purpose/source/state/reason data; no physical root, environment value, host-binding value, connection string, password, certificate, or exception text is exposed.
3. **HOST-001 durable actual-host provenance — closed.** The Windows provenance binds the run to Windows `10.0.26200`, X64 OS/process, .NET `10.0.9`, PowerShell `7.6.3`, current-user/non-system identity, `win-x64` framework-dependent payload hashes, two successful health/operations cycles, normalized commands, and explicit statuses. The Ubuntu provenance binds all three install/upgrade/rollback launches to `mcr.microsoft.com/dotnet/aspnet:10.0-noble`, immutable image ID/digest, Ubuntu `24.04.4 LTS`, x86_64, .NET `10.0.10`, effective UID/GID `10001`, the PostgreSQL image identity, exact payload hashes, seven Ready roots, and zero launch exits. The retained Linux harness is the executable exact-command source; the provenance deliberately replaces secret-bearing launch arguments and physical roots with redacted command descriptions while retaining each command status. Independent SHA-256 checks of the retained Windows and Linux publish payloads matched their provenance records, including the common support-manifest hash. This is durable, host/artifact-bound proof rather than inference from current Docker state.

### Refreshed evidence reconciliation

- Parsed TRX counters are Windows focused 32/32, Ubuntu focused 32/32, and authoritative Windows full Unit 5,561/5,561, with zero failures. The Ubuntu TRX identifies container `bc2bcc90e128` and Linux `/repositories/...` test binaries. The adjacent 61-byte log is a post-run missing-container cleanup message, not a test failure; the authoritative TRX completed before it and remains internally consistent.
- Main, Components, FileTools, and Web/local-reference Release build logs end with zero warnings and zero errors. The five runtime operations captures are Ready and reconcile to the provenance lifecycle records.
- CodeAnalytics snapshot `snap-20260810044522-a1246e1e` independently reconciles to three scoped projects, 1,733 types, 13,417 members, 135 registrations, 667 findings with no Error severity, and 11 diagnostics with no Error severity. Its two cycles remain test-helper type cycles and do not cross production project boundaries.
- The portability inventory contains 27,094 findings and 27,094 classifications with zero unclassified entries. Schema-3 secret scanning accounts for 52 candidates, 51 scanned text artifacts plus one control output, zero oversized/non-text/unreadable gaps, two private sentinels with zero matches, and 24 occurrences of the same six classified synthetic fingerprints confined to the two Windows full-suite TRXs.
- Independent portable validation with checksums intentionally skipped passed at 298 files, zero errors, and zero warnings. `git diff --check` still reports only the recorded traceability CSV line-ending notice.

### Residual risks and follow-up boundaries

- `A07-MACOS-HEADLESS-ACTUALHOST-001` still owns genuine launchd/macOS headless execution, and `MACOS-KEYCHAIN-VALIDATION-001` remains operator-deferred. Current macOS claims remain `ActualHostUnverified`; neither is an A06 blocker.
- `systemd-analyze` remains unavailable in the validation image. Preserve the current static directive/rendering checks and add actual service-manager unit loading at the later actual-service gate.
- The Linux startup GSSAPI loader noise and HTTP-only HTTPS-redirection warning remain operational cleanup items for C4. The Windows harness also records intentional process-tree termination rather than a graceful service-manager stop. These are accurately disclosed and do not negate the successful A06 readiness/restart proof, but final service operations should exercise graceful Windows stop behavior.
- Make future Linux harness cleanup idempotent so an already-removed container does not leave the standalone `No such container` cleanup line beside an otherwise successful TRX.
