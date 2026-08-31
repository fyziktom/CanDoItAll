# Candidate deployment procedure — inspected, not executed

Status: read-only preparation. No application build, process signal, replacement, deployment, browser action or provider request was performed for this procedure. Root must pass the SB01/SB02/SB03 and FrozenIntegration gates before executing any step. This document is a target-specific procedure, not an authorization to replace additional services.

## Exact baseline targets

| Role | Baseline identity | Preservation boundary |
|---|---|---|
| Native5032 | app PID58036, parent22496; ordinary `dotnet run --project src/App/CanDoItAll.Web -c Release --no-build --launch-profile http`; app created2026-08-31T10:41:53.092315Z | Same port, Development/http launch profile, complete inherited environment, PostgreSQL profile, content root and file workspace. No watcher. |
| Client5214 | `candoitall-shared-providers-manual-client-a-1`, ID`fb12806ab50b7bdadb68175ce79d6efb8596b3f4f62329f07f445ae49074226e`, image`sha256:168dc1535d4134a6621db8e3eae8bdc0628c3439e62fc7daade24340691b6bdc` | Exact environment/mounts/network/resource/security settings; user1654:1654; read-only root; loopback5214→8080. |
| Publisher5210 — NOT a target | `candoitall-shared-providers-manual-central-1`, ID`000fadde7e6757f7afd413e3102fa58568e18da4d9a7361d8057bda40c9b966d`, image`sha256:b6b502a5487bd8ba7b21a2c3afe18e4846d273fa0445b387a98bf85acda73089`, started2026-08-31T10:43:03.645154542Z | Must retain the same container ID, image, StartedAt, mounts, environment hash and security configuration throughout. |

The baseline native executable is `src/App/CanDoItAll.Web/bin/Release/net10.0/CanDoItAll.Web.exe`. Baseline DLL SHA256 is `60F188E37C58754076D6F462C236120EA7B63FB55ADC55C0A8924428F603A83D`, product version`1.0.0+aadd953150e7f659e4060ced6505621c705ea61f`, runtime10.0.11. Docker runtime remains10.0.10 as specified by the existing Dockerfile. A port number alone is never sufficient authority: recheck PID start time, executable, parent command and current container identities immediately before replacement. A mismatch blocks this procedure; do not silently retarget.

Public allowlisted frozen metadata is in `../phase-0/host-preflight.json` and `../phase-0/baseline-configuration.json`. Do not put raw `docker inspect`, inherited environment, launch settings or connection strings in proof.

## Build isolation, before the idle replacement window

Build only after the test gate and never during a baseline/candidate timing window. The approved native build uses the fresh owned short artifact root `.artifacts/asn-20260831`; the initial longer deployment/native-build root exceeded Windows MAX_PATH during template copying. Preserve that failed command/log and the successful short-root retry. Record HEAD plus the actual dirty-source fingerprint and relevant sibling-library fingerprints. HEAD alone does not identify these uncommitted candidate changes.

Native command shape, executed from repository root with absolute derived artifact paths:

```powershell
dotnet publish src/App/CanDoItAll.Web/CanDoItAll.Web.csproj --configuration Release --artifacts-path $nativeArtifactsPath --output $nativePublishPath -p:UseLocalCanDoItAllLibraries=true
```

Both installed `dotnet publish` and `dotnet run` support `--artifacts-path`. Keep this option global so referenced projects also avoid the live native bin/obj directories. Record the published and build-output Web DLL hashes; both must identify the same candidate. Preserve the original native bin directory and its baseline hash for rollback. Do not publish over it. Inspect the resulting static-web-assets manifest and copy layout before replacement.

Preferred launch preserves the repository-owned launch contract and project content root while reading the isolated candidate build output:

```powershell
dotnet run --project src/App/CanDoItAll.Web --configuration Release --no-build --launch-profile http --artifacts-path $nativeArtifactsPath
```

Do not execute this until the old host has stopped and5032 is free. `--no-build` implies no restore. Verify that this launch actually resolves the isolated executable/assembly; otherwise stop and correct the launch before sending any agent request. Do not assume direct execution of a publish DLL from a different content root preserves the development static asset/configuration behavior.

Docker build context is the repository root; the Dockerfile requires two explicitly named sibling contexts:

```powershell
docker build --file src/App/CanDoItAll.Web/Dockerfile --build-context "components=$componentsRoot" --build-context "filetools=$fileToolsRoot" --build-arg "BUILD_DATE=$buildUtc" --build-arg "BUILD_REVISION=$sourceRevision" --build-arg "BUILD_SOURCE_FINGERPRINT=$sourceFingerprint" --tag $candidateImage .
```

Derive sibling roots from the repository parent and validate their identities. Keep `COPY --from=components --exclude=**/[Bb]in --exclude=**/[Oo]bj` and the matching filetools line unchanged; these exclusions are part of the earlier Docker CSS fix. Preserve SDK10.0.302/runtime10.0.10 defaults. Do not run Compose up/down or rebuild/recreate publisher5210. Retain the candidate image ID and all build logs separately from private configuration.

## Native5032 environment and graceful replacement

The current process environment was successfully inspected read-only using the already cached `Microsoft.Diagnostics.NETCore.Client`0.2.510501 `DiagnosticsClient(58036).GetProcessEnvironment()`. It contains127 entries. Public checks confirmed Development, `ASPNETCORE_URLS=http://localhost:5032`, http launch profile, PostgreSQL and the existing desktop/agent-automation settings. Values were not copied to proof. Original stdout explicitly reports content root `src/App/CanDoItAll.Web` resolved under this repository.

Before stopping, capture this exact dictionary again after validating PID/start time. If it must cross processes, protect the JSON bytes with Windows DPAPI CurrentUser and restrict the ignored private file ACL to the current user. Never write plaintext environment values, pass them as command-line arguments or copy them to bundle proof. Record only an environment hash and allowlisted identity fields. Preserve a protected snapshot for rollback.

Launch through a hidden wrapper with `ProcessStartInfo.UseShellExecute=false`, `CreateNoWindow=false`, `WindowStyle=Hidden` and the dedicated hidden wrapper console inherited by the child, explicit working directory, redirected stdout/stderr and `ProcessStartInfo.Environment.Clear()` followed by the captured dictionary. This prevents the isolated-test database environment from leaking into the real host. Drain both redirected streams asynchronously. Do not rely on a generic current-shell environment or on `Start-Process -Environment` PATH-merging behavior to reproduce the baseline exactly. The wrapper itself must use `Start-Process -WindowStyle Hidden` if started in the background.

Root subsequently validated console attachment read-only: the isolated hidden helper observed exactly application58036, parent22496 and itself44596, then detached without signaling. Evidence is in `.artifacts/agent-startup-performance/deployment/console-inspection.json` and `Inspect-NativeConsole.ps1`. Graceful exit itself remains unexecuted. There is no inspected application shutdown endpoint. The bounded signal procedure must freshly repeat the exact same exclusivity guard: attach to the target console, inspect its process list and send CTRL_C only if it contains the expected application, its known `dotnet run` parent and the helper, with no unrelated processes. The signal helper ignores CTRL_C for itself. The hidden launcher handles its own CTRL_C while continuing to drain child output; it does not set an inheritable ignore flag on the application. If console attachment or that exclusivity check fails, abort instead of signaling a shared console or falling back to `Stop-Process`/taskkill. Wait up to40seconds for the exact app and parent to exit and5032 to become free. A timeout is a blocked graceful replacement, not permission for force termination.

Immediately before signaling, coordinate no new user or validation requests and re-read every run JSON under the frozen native scope. Root reviewed one exact historical paused-run exception: `e966b321-979a-465e-9f26-59bb004a83d8`, WaitingOnTool(3), one durable pending approval, last updated2026-08-13T13:59:25Z, predating the current application process by18days. Its durable approval and workflow checkpoint files exist, with no pending commit journal; all120other native runs are terminal. This exact unchanged persisted pause may remain while the idle application host is replaced. Before and after replacement, verify the run/approval/checkpoint SHA256 values, state and timestamps are unchanged. Do not resume, approve, deny, cancel, reset or otherwise mutate it. Any additional nonterminal entry, changed exception payload/hash, active provider/tool operation or pending journal blocks replacement. All other runs require terminal state and completion timestamp. Recheck the exact process/run identities immediately before signaling to narrow the admission race. This exception is reviewed durable-paused-state preservation, not a blanket nonterminal-run bypass or permission to manufacture idleness.

After starting the candidate, verify unique5032 ownership, exact new process/assembly, preserved profile/content root/workspace/environment hash, readiness and unchanged publisher identity. Record first-start warm-up separately; do not mix it into the five warmed candidate measurements. If launch/readiness fails, stop only the exact candidate using the same graceful protocol, then relaunch the original command without `--artifacts-path`, with the saved environment and untouched original binary. No data rollback or workspace replacement is part of application rollback.

## Docker5214 client-only replacement

`.artifacts/agent-label-startup-analysis/Restart-AffectedInstance.ps1` is a useful inspected implementation example, not a command to run unchanged. It copies the current Config/HostConfig and network aliases through the Docker Desktop named pipe API, stops only the client, renames it as rollback and creates a replacement. Its tag/label and backup suffix are from the earlier label fix. The two-host script under the providers premerge bundle is prohibited here because it also replaces5210.

Before adapting/running the client-only procedure:

1. Validate the exact baseline client and publisher identities above, Docker endpoint`npipe:////./pipe/dockerDesktopLinuxEngine`, candidate image ID and an unused rollback name. Require the same mounts/configuration hashes recorded at baseline, including secret mounts, /tmp tmpfs, cap/drop/security options, user1654:1654, resource limits and loopback publication. Preserve read-only root.
2. Validate every bind path exists and resolves inside the exact `.artifacts/shared-providers-e2e` root; retain mount destination, read/write flag and propagation. Reject unknown mount types or unreviewed paths. Capture raw configuration only in memory.
3. Require all run JSON files under client data's scoped execution/runs directory terminal with completion timestamps; coordinate no new work. Recheck publisher unchanged and client identity immediately before stop.
4. Stop only the full client ID, bounded40seconds, and verify exit. Docker may force-kill at the timeout: exit137/forced termination must be reported as such and blocks a claim of graceful deployment. Do not run old and candidate containers against the same data concurrently.
5. Rename the stopped original as rollback. Clone Config/HostConfig in memory, replace only image and candidate provenance labels, retain secret/env values exactly, and recreate network aliases/driver options/gateway priority. Review IPAMConfig: preserve configured static assignment if any; do not blindly carry runtime-assigned IP/MAC IDs. Do not print the create payload.
6. Start only the new client; verify bounded health/readiness, mount/env/security/network equality and publisher ID/image/StartedAt equality before declaring deployment complete. If create/start/readiness fails, stop/remove only the newly created client, rename/start the retained original, and verify restored identity/readiness. Never remove volumes or alter the data bind.

The old example lacks strict original-ID/publisher guards and does not wait for candidate health before success. Those gaps must be fixed in an adapted execution script, not accepted because the previous deployment happened to succeed.

## Candidate timing and cleanup boundary

All live functional/performance validation remains root-owned genuine MCP UI interaction on these two exact hosts. Reattach the same sanitized diagnostic binary using new actual app PIDs/sockets, new stop markers and new proof paths; old Phase0 stop markers already exist. Preserve model, reasoning, agent, scope, capability inventory and prompt. Do not run application builds/tests alongside sampling.

The owned isolated PostgreSQL test container on127.0.0.1:52049 was created after Phase0. Once all tests finish, stop/remove only that owned container before candidate sampling so the baseline background load is restored. Do not touch default5432, live app databases, publisher5210 or unrelated8080. Retain stopped application rollback containers until acceptance; cleanup of those is a separate explicit action.

References inspected: existing client-only restart script; Web Dockerfile; Directory.Build.targets local-library mapping; Web Program.cs/static-assets setup; installed publish/run CLI option help; frozen process/container proof. SharedInfo documentation/tooling standards supplied the portable-path, private-artifact, inspection-first and exact-target conventions. This preparation does not claim deployment, shutdown or rollback runtime validation.