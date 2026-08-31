# Named validation expansion: CodeAnalytics AllSuppliedSuites

Decision owner: root. Safety reviewers: startup_runtime_analysis and startup_code_analysis. State: runner preparation complete; **execution remains deferred until root records focused-gate PASS, source freeze and artifact-tree handoff**. This is a validation-only expansion and does not authorize additional production changes, test-infrastructure changes or application host operations.

The bundle normally defers broad testing. Current CodeAnalytics impact output is healthy for the supplied Unit/Integration/Components workspaces but cannot close its conservative impacted-test selection within the5000-member budget and reflection boundaries. It therefore returned `AllSuppliedSuites`. The applicable CodeAnalytics skill requires every returned workspace to run, with no narrowing filter. This named limitation justifies one serial broad gate at the Frozen Integration checkpoint; it does not claim a new public-contract/schema change occurred.

Safety preconditions and concrete exceptions are recorded in `broad-suite-safety-review.md`. In particular, all three suites require the owned52049 PostgreSQL bootstrap; live provider/Docker/ComfyUI/interactive-secret opt-ins are disabled; only owned test child processes and ephemeral test listeners may execute. The two fixed scenario-output cases require preservation of their existing ignored26-file subtree. No live5032/5214/5210 API, data, process or configuration mutation is part of this gate.

The6407 manager-client residual was investigated further: `DevelopmentManagerClient` has no network work in its constructor and exposes network work only through `CreateTuningRequestAsync` and `SubmitTuningRequestAsync`. Repository-wide source searches found no callers of either method, and only two client registrations (Web Program and ComponentTestHarness). No component injection, service resolution, tuning click path or TuningCoordinator dependency on this client was found. Registration alone cannot issue the6407 mutation. This is specific source-boundary evidence, not a claim that bUnit universally prohibits networking.

## Prepared execution tools

Owned ignored scripts:

- `.artifacts/agent-startup-performance/frozen-validation/Run-FrozenTests.ps1`
- `.artifacts/agent-startup-performance/frozen-validation/Invoke-FrozenBroadGate.ps1`

The runner preserves its caller environment in memory, applies test-only isolation, invokes the checked52049 bootstrap for every suite and restores its environment in finally. It clears inherited app URL/profile overrides and standard provider credential variables without printing values. Optional proof destinations point inside the owned phase directory. The AllTests path requires NoBuild and the execution count obtained during discovery. It uses Release output from the handed-over `sb01-tests` tree, never the live native bin/obj. Release is the actual MSBuild configuration; Frozen Integration is the gate name.

The serial wrapper is inert unless `-Execute` is supplied. It additionally requires a matching gate record with `SourceFrozen`, `FocusedGatePassed`, `ArtifactTreeHandedOver` and `RootGoRecorded` all true, plus hashes of at least the three test assemblies. That record must be created from the real completed root handoff, not prefilled to pass a check. It verifies the hashes, preserves the exact legacy scenario subtree with per-file SHA256 checks, discovers all three complete suites, then executes each once with its observed count. If discovery fails, no suite executes. If a suite execution fails, remaining already-discovered suites still run serially and retain independent results. No repeated run is silently substituted for the first result.

The runner saves command/exit/time metadata, full transcript, TRX and explicit total/executed/passed/failed/notExecuted counts even when dotnet returns a failure code. Non-passing names/outcomes are retained separately from any narrative. Existing phase transcripts cannot be overwritten. A file lock prevents overlapping invocations of this runner. No generic process kill, Docker mutation, application deployment or cleanup command is present.

Only PowerShell AST parsing has run for these scripts; it passed. No test discovery, test execution, evidence backup, subprocess start or database bootstrap was invoked while preparing them. Canonical application tests, exact dynamic discovery counts and actual gate result remain pending.

## Deferred invocation

After explicit root GO and storage-owner handoff, create the gate record in the owned frozen-validation directory, record the real source fingerprint and frozen binary paths/hashes, then invoke:

```powershell
& .artifacts/agent-startup-performance/frozen-validation/Invoke-FrozenBroadGate.ps1 -GateId frozen-integration -GateRecordPath $gateRecordPath -Execute -Confirm:$false
```

No `--filter` is used for the three returned solution paths. Each complete suite is discovered before any executes. The exact binaries are Release from the owned isolated artifact tree. Retain the first run's failures/skips honestly and report any later targeted investigation separately. Keep builds/tests away from all candidate timing windows and leave live hosts unchanged throughout.