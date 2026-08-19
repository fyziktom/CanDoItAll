# B05 independent Gate R3b review

## Decision

`NO-GO for Gate R3b.`

B05 does not yet close PLUG-002 through PLUG-004 or B05-T02 through B05-T06/T08/T09. Five product/evidence blockers remain. B06 stays blocked. Actual macOS execution and NuGet publication/re-pinning are operator-deferred and are not themselves blockers; the blockers below concern the current Windows/Linux/local-source behavior and the truthfulness of the retained fallback.

## Blocking findings

### B05-IND-001 — The typed Docker capability snapshot has no production consumer (`P1`)

- `src/plugins/Implementations/CanDoItAll.Plugin.Docker/DockerHostToolService.cs:23-56` defines the typed executable/context/daemon/endpoint snapshot and probe, and `DockerPluginServiceCollectionExtensions.cs:21-28` registers it.
- A repository-wide source search finds no production reference to `IDockerHostCapabilityProbe`, `DockerHostCapabilitySnapshot`, or `DockerHostDependencyState` outside that producer/registration. Only unit and integration tests call `ProbeAsync`.
- `src/plugins/Implementations/CanDoItAll.Plugin.Docker/DockerWorkflowExecutors.cs:21-24` and lines 119-130 still derive public workflow availability only from grants. Missing executable, invalid context, denied socket, timed-out probe, and unavailable daemon therefore do not reach any production capability/status consumer.

This is the Governed-proof shallow-pass for PLUG-002: a typed producer and direct tests exist, but the product never reports or consumes the state. It also makes the evidence report's runtime-capability claim stronger than the shipped behavior.

Required correction: connect the typed probe to one explicit product capability/availability consumer with a bounded lifecycle and non-sensitive projection. Prove missing, invalid, denied, timed-out, remote, unavailable, and ready states through that production flow; do not synchronously block descriptor getters or introduce a second probing authority.

### B05-IND-002 — Docker start preflights fail open into mutating operations (`P0`)

- `DockerHostToolService.cs:212-234` treats every container-inspect result other than started/exit-zero as “container absent”; a failed or timed-out running-state inspection becomes `docker start`.
- Lines 238-259 similarly treat every image-inspect start failure, timeout, permission error, or nonzero result as “image absent” and proceed to `docker pull`.
- Lines 262-276 then return `docker run` after an indeterminate container preflight. The current timeout/permission evidence exercises `ProbeAsync`, not this mutating start-recipe path.

An unavailable/denied/timed-out authority must not be reinterpreted as absence and followed by pull/start/run. This violates the stated fail-closed failure matrix and makes the mutation path borrow trust from a different probe test.

Required correction: distinguish authoritative not-found from start failure, timeout, cancellation, permission denial, malformed state, and other nonzero results at each container/image/running-state preflight. Only authoritative absence may advance to pull/run; every indeterminate result must return/throw a redacted typed failure without another mutating command. Add deterministic request-sequence tests for each branch on both host slices.

### B05-IND-003 — `DOCKER_HOST` is neither a complete typed endpoint contract nor robustly redacted (`P0`)

- `DockerHostToolService.cs:503-508` validates `DOCKER_HOST` only by calling the scheme classifier at lines 557-572. Any absolute `unix`, `npipe`, `tcp`, `ssh`, `http`, or `https` URI is accepted without scheme-specific authority.
- Unlike `DOCKER_CONFIG`, `DOCKER_CERT_PATH`, and `SSH_AUTH_SOCK` at lines 481-545, a `unix://` socket payload never passes through the physical path/link policy. Named-pipe grammar, URI query/fragment, and credential-bearing userinfo are not constrained either.
- Redaction at lines 607-629 replaces only four complete raw environment values with `StringComparison.Ordinal`. A Windows path emitted with normalized casing/separators or without a trailing separator will not match. A credential-bearing endpoint emitted without its scheme/full raw form can also escape because the shared assignment redactor does not recognize URI userinfo.
- `DockerHostToolServicePortabilityTests.cs:46-75` proves only exact-string replacement of the whole endpoint and exact-case configuration path, so it cannot close these variants.

This contradicts B05-T03/ADR-R15's socket-path authority and endpoint-credential non-disclosure contract.

Required correction: parse a scheme-specific typed endpoint, reject unsupported components and secret-bearing userinfo, validate local socket paths with the host physical/link authority, validate named-pipe syntax explicitly, and build redaction tokens from normalized path/endpoint/credential forms using the correct host identity semantics. Add Windows case/separator variants, Unix socket/link cases, partial-userinfo diagnostics, and invalid URI-component regressions.

### B05-IND-004 — The automatic FileTools package fallback is unverified but remains runnable (`P0`)

- `Directory.Build.targets:6-7` silently selects package mode whenever either sibling source tree is absent; lines 11-33 select direct projects only in the local two-sibling shape.
- `evidence/b05-filetools-compatibility.md:5-13` correctly says all executed compatibility proof applies to direct source at FileTools commit `f31e20d...` plus three working-tree files, not to NuGet `0.1.18`. The seven FileTools test/build artifacts and governed hashes likewise bind direct source only.
- Nevertheless, `subbundles/05-plugins-filetools-and-host-integrations/tasks.json:91-101` marks B05-T04 implemented while still saying package `0.1.18` was tested across Windows, Ubuntu, and macOS. It was not.
- The retained package is not behaviorally equivalent to the reviewed source: the installed `0.1.18` assembly identifies an older build and lacks the new host-specific desktop-session and foreign-path boundary types. Nothing in product composition disables or labels desktop capability when the automatic fallback is selected.

Deferring publication/re-pinning is acceptable; silently running an unverified fallback while marking package compatibility implemented is not. It violates PLUG-003/T04/T05 and makes support depend on incidental sibling-directory presence.

Required correction: until the reviewed source is published and re-pinned, make the direct-source mode explicit and fail closed when its exact prerequisites are absent, or truthfully disable the affected desktop capability in package mode. Alternatively, separately prove the exact package behavior. Synchronize `tasks.json`, the compatibility report, ledger, support claims, and evidence to the selected policy without claiming actual macOS execution.

### B05-IND-005 — FileTools cancellation is checked too early to support the recorded contract (`P1`)

- `../CanDoItAll.FileTools/src/CanDoItAll.FileTools.Desktop/DesktopFileLauncher.cs:26-59` checks cancellation only at method entry, then evaluates session availability and target/application filesystem state before calling the OS process starter without another cancellation checkpoint.
- Cancellation that arrives during those pre-delegation checks can therefore still launch the application. The compatibility report at lines 19-25 says cancellation before OS delegation prevents process start.
- The only regression, `DesktopFileLauncherTests.Cancellation_before_desktop_delegation_does_not_start_a_process`, supplies an already-cancelled token, so it does not exercise the interval the report claims.

Required correction: recheck cancellation immediately before the irreversible `processStarter.Start` handoff (while retaining the documented fire-and-forget post-acceptance boundary) and add a deterministic test that cancels during an injected preflight/availability stage and proves zero starts. Refresh both-host direct-source evidence.

## Architecture gate result

The intended dependency direction is otherwise sound:

- Docker production contains no direct `Process.Start`, `ProcessStartInfo`, `new Process`, process enumeration, or `new LocalWorkspaceProcessHost`; it receives the B01 path, executable, environment, workspace, timeout, cancellation, output, and tree-cleanup authorities through composition.
- One scoped `DockerHostToolService` instance is exposed behind the execution and probe interfaces. No mutable process-global Docker registry or second low-level process owner was introduced.
- FileTools remains the OS-delegation adapter. Application composition owns explicit feature/profile gating, host-bound preferences, and trusted workspace path authority.
- The independently recomputed main-repository graph contains 106 projects and 635 literal in-repository `ProjectReference` edges, with zero missing literal targets and zero cyclic projects. B05 adds no backward Core-to-plugin dependency.

These architecture positives do not close the orphan capability producer, fail-open mutation path, or unverified package fallback.

## Independently reconciled evidence

- Governed proof contains nine failing-first/correction records and ten passing source assertions. All 23 source hashes, including the three sibling FileTools files, and all 16 test/build/host hashes recompute exactly; zero files are missing and zero hashes differ.
- All seven TRXs are completed with total equal to executed and passed: Windows 144/144 unit, 2/2 integration, and 19/19 FileTools; Linux 62/62 unit, 4/4 path, 2/2 integration, and 19/19 FileTools. Failed, error, timeout, aborted, inconclusive, and not-executed counters are zero.
- The seven governed build logs contain no compiler warning/error diagnostic and no failed-build marker.
- The source-reference manifest reconciles to 129 records, 129 unique IDs, 129 unique paths, and zero missing paths.
- Schema-3 coverage reconciles all 18 B05 files as 17 scanned text artifacts plus one scanner control, with zero oversized/non-text/unreadable gaps and zero findings. No private sentinel was loaded, so this proves the configured rules and coverage accounting rather than arbitrary-secret non-disclosure; source review found the endpoint variant gap above.
- Windows host evidence records actual Docker client/server 29.6.2 readiness. Linux evidence truthfully records pinned Ubuntu 24.04.4, exact image digest, prebuilt portable assemblies, headless desktop, and no asserted Docker daemon. Neither artifact claims actual macOS.
- FileTools is exactly at branch `development`, commit `f31e20d054003348c7557b9634e0838fc5996ae0`, with only the three governed B05 files modified. Components is clean at `8372c1d55f21b349f8e859470b02eeb4421e96ca`. The current build logs show the direct FileTools project output.
- `git diff --check` exits successfully with only the three recorded traceability-CSV CRLF notices. The portable runtime validator independently passes before this review file at 331 files, zero errors, and zero warnings with checksums skipped.

No broad/full suite or broad build was rerun during this independent pass.

## Non-blocking deferred boundaries

- Actual macOS Docker/desktop execution remains operator-deferred. Deterministic macOS session/path fixtures are not actual-host proof and must not become a support claim.
- Publishing and re-pinning the corrected FileTools source may remain deferred, provided the unverified package fallback is explicitly unavailable/fail-closed meanwhile.
- Hosted CI, the final broad Windows/Linux aggregate, and Gate R4 remain B07 scope.
- Desktop launch remains intentionally fire-and-forget after OS acceptance; no post-delegation cancellation or lifecycle ownership is claimed.

## Re-entry criteria

Re-review may be bounded to the five blockers plus refreshed evidence consistency. It must include production consumption/projection of the typed Docker capability, fail-closed container/image preflight sequences, scheme-specific endpoint/path/redaction adversarial cases, an explicit truthful package/direct-source mode policy, and the cancellation-during-preflight FileTools regression. Refresh affected Windows/Linux TRXs, build/hash/source-reference/redaction records, sibling identity, and the portable validator. Do not advance B06 until independent R3b GO is recorded.

## Re-review

### Decision

`GO for Gate R3b.`

The bounded remediation closes B05-IND-001 through B05-IND-005. PLUG-001 through PLUG-005 and B05-T01 through B05-T09 are adequate for the operator-approved local Windows/Linux boundary. B06 may become eligible only after the executor completes the canonical gate/status, index, checksum, and post-review validation bookkeeping.

### Finding closure

- **B05-IND-001 closed.** Docker executors now implement the typed asynchronous availability evaluator. The production runtime catalog projects that state through `/api/workflows/executor-catalog`, while `WorkflowExecutorImplementation<TExecutor>` reevaluates it and fails closed before invoking the concrete executor. The scoped snapshot provider is the single probe consumer per scope; static descriptor enumeration remains non-blocking.
- **B05-IND-002 closed.** Container inventory, running state, and image inventory must now complete with a started, non-timed-out, exit-zero result. Only an exact empty successful inventory authorizes absence. Ambiguous container output, malformed running state, process-start failure, timeout, denial, and nonzero results stop before `start`, `pull`, or `run`; the authoritative success branches retain the intended behavior.
- **B05-IND-003 closed.** `DOCKER_HOST` now has scheme-specific validation: Unix sockets use native physical/link authority, named pipes use the Windows-only strict grammar, and remote endpoints reject credentials, query, fragment, and unsupported path/port shapes. Protected filesystem values generate full, trailing-separator-trimmed, and host-separator-equivalent redaction tokens; Unix local-socket payloads are included. The final cross-host regression specifically supplies a trailing separator and verifies that the trimmed emitted spelling is absent.
- **B05-IND-004 closed.** Desktop launching is compiled as validated only when direct sibling FileTools source mode is selected. Package mode remains build-compatible but reports typed `DesktopUnavailable` and cannot delegate, even when feature and interactive-profile flags are enabled. The task, compatibility, ledger, and evidence records now state that NuGet `0.1.18` was not executed and do not attribute the reviewed sibling changes to that package.
- **B05-IND-005 closed.** The sibling `DesktopFileLauncher` rechecks cancellation immediately before the irreversible process-start handoff. A deterministic cancellation-during-availability-preflight test proves zero process starts while preserving the truthful fire-and-forget boundary after OS acceptance.

### Independent evidence reconciliation

- The governed proof contains 15 failing-first/correction records and 16 source assertions. All 29 source hashes and all 25 TRX/build/host artifact hashes independently recompute with zero missing files and zero mismatches.
- The refreshed remediation TRXs are green on both hosts: Windows and pinned-Ubuntu Docker each report 38/38 unit, 20/20 FileTools, and 2/2 integration. The retained earlier B05 TRXs also remain internally complete and green.
- All ten governed build logs contain zero compiler diagnostic or failed-build hits. The source-reference manifest reconciles to 135 records, 135 unique IDs, 135 unique paths, and zero missing paths.
- Schema-3 coverage reconciles 27 candidates as 26 scanned text artifacts plus one scanner control, with zero oversized, non-text, unreadable, or other coverage gaps and zero findings. As before, no private sentinel was supplied; the result proves the configured rules and complete artifact accounting, not arbitrary-secret detection.
- `git diff --check` exits successfully and emits only the three already-recorded traceability-CSV line-ending notices. The portable runtime validator independently passes before this append with 332 files, zero errors, and zero warnings using `--skip-checksums`.

### Residual boundaries

- Actual macOS Docker/desktop execution remains explicitly operator-deferred; deterministic macOS fixtures are not actual-host evidence and must not become a verified-support claim.
- Publishing and re-pinning the corrected FileTools source remains deferred. Package-mode desktop capability must stay unavailable until the exact published identity receives separate compatibility proof.
- Desktop launch remains intentionally unowned after OS acceptance. Hosted CI, the broad aggregate, and final Gate R4 remain B07 scope.

No broad/full suite or broad build was run during this re-review. Product source, canonical records, evidence artifacts, index, and checksums were not modified by the reviewer; only this appended independent decision was added.
