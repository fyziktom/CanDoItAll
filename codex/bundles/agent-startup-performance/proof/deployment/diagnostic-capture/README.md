# Completed candidate diagnostic capture

The portable launcher now matches the executed script after the single Docker executable-resolution correction; see executable-resolution-review.json and executable-resolution-fix.diff for before/after hashes. The preparation contract and original review below are historical.

Capture candidate-20260831-1627 completed with seven exact HTTP-to-run matches per host. Both collectors were ready before all fourteen sequential UI sends, and exited cleanly afterward with zero unexpected arguments and empty stderr. Stop created only owned collector markers and did not signal applications. Independent verification: bundle://proof/SB03/performance/independent-result-verification.json. This is performance evidence, not the separate tool UI matrix or final closure. The rejected BusyBox clock format and corrected coreutils brackets are recorded in executable-resolution-review.json.

## Historical preparation record

# Candidate diagnostic capture preparation

This is a reviewed script snapshot, not evidence of an executed capture. The executable draft is `.artifacts/agent-startup-performance/deployment/Set-StartupCapture.ps1`; run it from that location because it resolves the repository root relative to its script directory. The copied script is byte-identical, as recorded in `preparation-contract.json`.

Before execution, root must finish deployment/verification, provide the actual candidate native PID and exact UTC start, candidate client full container ID, guest application PID and matching diagnostic socket, and confirm that builds/tests have stopped. No baseline process or publisher container can pass the target guards. The helper DLL, all eight dependency hashes and existing tar are checked against phase-0 evidence; nothing is rebuilt.

Start command shape, with actual reviewed identities replacing placeholders:

```powershell
& ./.artifacts/agent-startup-performance/deployment/Set-StartupCapture.ps1 -Start -CaptureId <unique-id> -NativeAppPid <pid> -NativeExpectedStartUtc <exact-utc> -ClientId <full-id> -ClientAppPid <guest-pid> -ClientDiagnosticSocket <exact-socket> -RootSamplingGo
```

The command transfers only the frozen helper archive through Docker exec standard input into a unique directory in the existing `/tmp` tmpfs. It does not use docker cp, replace root files, modify mounts/environment, or start/stop an application. Both host helpers launch hidden. Readiness has a 45-second bound. Root must accept both exact-target ready records before sending UI requests. Output is under `proof/SB03/performance/after/<unique-id>/`; root may retain browser/timing comparison files directly under `after/` separately.

Stop command:

```powershell
& ./.artifacts/agent-startup-performance/deployment/Set-StartupCapture.ps1 -Stop -CaptureId <same-unique-id>
```

Stop checks recorded collector PID creation times, creates only the unique owned stop markers and waits up to 35 seconds. It never removes markers, signals application processes, kills collectors or deletes Docker resources. A successful stop requires both host collector exits, both stopped records, zero unexpected arguments and empty stderr. Partial start failures also request both owned markers, wait a bounded interval and record which collectors actually exited. Missing exit/protocol evidence is a failure, not a silent success; root must resolve it before any sampling retry. Helpers retain the same independent 1800-second maximum as baseline.

Only syntax parsing was performed during preparation. No collector or start/stop command was executed. Published UI, dispatch, stage timing and aggregate comparison remain later gates.
