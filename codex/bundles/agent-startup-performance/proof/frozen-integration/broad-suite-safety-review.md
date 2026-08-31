# Broad suite safety review

Reviewer: startup_runtime_analysis. Status: read-only audit; no build, discovery, test execution, browser action, host replacement or test-infrastructure modification performed for this review. Unit/Components independent boundary audit was supplied by startup_code_analysis and is recorded below. Findings below distinguish a safe execution envelope from a passing test result.

## Why a broad gate needs a named decision

The normal bundle plan defers broad tests. Root reports that the current CodeAnalytics impact query returned healthy Unit, Integration and Components workspaces but `AllSuppliedSuites` because the5000-member budget and reflection prevented a closed selection. The CodeAnalytics skill treats that result as an instruction to execute every returned workspace, not permission to turn an empty selector list into no tests. Root must record this conservative-analysis limitation as the named Frozen Integration broad-gate trigger and update the bundle decision before execution. This safety review does not change public contracts, test infrastructure or approved implementation scope.

Each returned solution contains exactly one test project:

| Workspace | Source files | Source Fact/Theory attributes counted |
|---|---:|---:|
| `tests/Solutions/CanDoItAll.Tests.Unit.slnx` |576|5626|
| `tests/Solutions/CanDoItAll.Tests.Integration.slnx` |179|1072|
| `tests/Solutions/CanDoItAll.Tests.Components.slnx` |199|1078|

These7776 source attributes are a scope estimate, not runner discovery counts. Theory rows, inherited/custom attributes and disabled platform/live cases change the actual count. No trustworthy total runtime estimate was measured. This is a full repository-sized test gate rather than a short smoke run; record real per-suite discovery, execution, skip and elapsed totals. Integration disables xUnit test parallelization at assembly level. Prefer three serial unfiltered invocations after one frozen candidate build, not concurrent suites competing for the test database or live timing samples.

## Required isolated environment

Dot-source the owned `.artifacts/agent-startup-performance/test-postgres/Enter-IsolatedPostgresTestEnvironment.ps1` in the exact PowerShell process launching the gate. It verifies the owned disposable PostgreSQL container identity/label, health and127.0.0.1:52049 binding, then sets `CANDOITALL_TESTS_POSTGRES_CONNECTION` privately. Never print that value.

`PostgresTestAvailability.EnsureAvailableAsync` checks the override at lines26–33 and returns its availability result directly. Therefore an explicitly set but unavailable52049 fails rather than proceeding to the dangerous default5432/Compose branch. Without the override, lines35–76 probe the workstation default and can run `docker compose up -d postgres`; an unfiltered run without the checked override is rejected.

`CanDoItAllTestEnvironment` creates GUID temporary roots, explicit control-plane/profile/workspace/secret paths, and unique PostgreSQL database leases. The lease creates and drops only its generated database on the configured server. Default `TestApplication` uses that environment/profile. `TestApplicationBootstrap.BuildConfiguration` uses the supplied in-memory fixture configuration, not the user's persisted profile. API host fixtures use their temporary content root and127.0.0.1:0; live5032/5214 are not their destinations.

Use a dedicated test child environment, not the live app's captured environment. Explicitly set the following flags to a disabled value (`false` for boolean-text switches, `0` for numeric opt-ins), even though all were absent in the audit shell:

- `CANDOITALL_RUN_LIVE_AGENT_VALIDATION=false`
- `CANDOITALL_ENABLE_LIVE_OPENAI_SMOKE=false`
- `CANDOITALL_RUN_LIVE_OLLAMA_VALIDATION=false`
- `CANDOITALL_RUN_DOCKER_PROOF=0`
- `CANDOITALL_RUN_LIVE_COMFYUI_FLUX_PROOF=0`
- `CANDOITALL_SECRET_SERVICE_INTEGRATION=0`
- `CANDOITALL_KEYCHAIN_INTEGRATION=0`
- `CANDOITALL_REQUIRE_DOCKER_INTEGRATION=0`

Clear inherited real-provider credentials from this dedicated test child where they are not needed by deterministic fixtures; do not log their values. Clear URL/port host overrides (`ASPNETCORE_URLS`, `ASPNETCORE_HTTP_PORTS`, `ASPNETCORE_HTTPS_PORTS`, `DOTNET_URLS`, `URLS`) so fixture WebApplication defaults cannot acquire a live port. No such URL override was present in the audit shell. Set `CANDOITALL_TEST_CONFIGURATION` to the actual frozen build configuration and preserve a validated repository root for source-reading tests.

Clear these optional output overrides, or point them only at distinct directories inside the owned gate proof root: `CANDOITALL_LLMCHAT_RETENTION_QUERY_PLAN_DIR`, `CANDOITALL_Scenario05_QUERY_PLAN_DIR`, `CANDOITALL_LIVE_COMFYUI_FLUX_PROOF_DIR`. They otherwise permit arbitrary inherited output paths. Setting process-only variables for test isolation is not a production configuration change.

## Integration external boundaries reviewed

| Area | Actual behavior | Control/conclusion |
|---|---|---|
| `LiveSpecialistAgentScenarioIntegrationTests` | Can run real seeded agents/OpenAI calls when BOTH live-agent and live-OpenAI flags equal true. | Disable both. It returns without provider work otherwise; that return is not live proof. |
| `LiveLocalOllamaThinkingEffortIntegrationTests` | Real HTTP to configured/default11434 and installed models. | Custom Fact skips unless its explicit true flag is set. Disable it. |
| `ProjectStructureAgentIntegrationTests` live ComfyUI Flux case | Real image-provider request and optional generated-image/proof files. | Disable its numeric opt-in; no inherited proof destination. |
| `PluginCatalogIntegrationTests.Docker_qdrant_plugin_workflow_live_proof` | Can execute real Docker plugin workflow/container operations. | Disable `CANDOITALL_RUN_DOCKER_PROOF`; do not select a live external acceptance suite. |
| `PluginDesktopPortabilityIntegrationTests.Docker_probe_uses_the_real_canonical_host_and_reports_each_dependency` | Real Docker executable resolution, `context show`, `version --format {{.Server.Version}}`. | Read-only daemon/context probe; no create/start/stop/pull/delete. It still runs in an unfiltered suite. Requiring readiness is optional and disabled. |
| `ExecutionFoundationPortabilityIntegrationTests` | Real `dotnet --version`, `git --version`. | Read-only child commands; no application build or Git mutation. |
| `ManagerProcessDiscoveryIntegrationTests` | Windows reads its own current test-process identity; Linux-only cases launch/terminate exact owned child processes. | Windows gate returns from Linux-only cases; no arbitrary existing process termination. |
| `McpExternalToolPortabilityIntegrationTests` | Real owned deterministic local stdio/MCP child processes, including cancellation cleanup. | Runs `typeof(McpTestHostMarker).Assembly.Location`, temporary workspaces and owned PID/ready files; compatible with isolated compiled outputs. This is expected test-process mutation, not a live agent call. |
| `SecretPortabilityIntegrationTests` | Default secret/migration fixtures use isolated temporary files and test database. Linux Secret Service/macOS Keychain CRUD requires OS plus numeric opt-in. | Disable both interactive-store opt-ins; neither executes on this Windows host. |
| Gmail/Office365/email tests | Inspected clients receive fake HTTP handlers or deterministic workflow invokers. | No outgoing email, real mailbox edits or provider sends required. |
| Watch supervisor tests mentioning5032/7271 | Fake HTTP factory/RuntimeProbeHandler supplies fixture readiness responses. | Port strings are test data, not live requests. |
| API/authorization/shared-provider tests | Private test hosts or fake handlers plus isolated profiles/databases. | Do not substitute the separate external acceptance workspace or Playwright auto-launch fixture. Neither is part of these three solution files. |

`IntegrationTestPaths.ResolveProjectOutputAssembly` still has an old conventional bin path implementation but has no callers in this Integration project. Actual MCP fixtures use marker assembly locations, so it does not force building over the running native5032 output. The source/repository walkers still require isolated artifacts to remain under the intended repository or an explicitly prepared source mirror.

## One fixed output path needs preservation

Two methods in `ProjectStructureWorkflowScenarioHarnessTests` unconditionally generate fixtures/results in:

```text
artifacts/codex-bundles/project-structure-workflow-runs/proof/scenarios
```

The path is derived from the repository root at source lines26–33 and61–68, then files are written at137–138 and by `PrepareSyntheticInputsAsync`. This is not redirected by `--artifacts-path` or the query-plan environment overrides. The current directory contains26 ignored files totaling61913bytes, including two29.6KB result JSON files from00:58/00:59 UTC on2026-08-31. No reparse points were found. These are older generated test artifacts, not live workspace data, but silently overwriting prior evidence is unacceptable.

Before unfiltered Integration, root must either (a) explicitly preserve this exact existing subtree with an owned byte/hash manifest and copy, then retain the new output separately, or (b) execute from a frozen isolated source mirror whose repository-root walker resolves into that mirror. Do not change test infrastructure or use a filter to hide these two cases while calling the result `AllSuppliedSuites`. Preservation must validate the resolved exact target; no generic recursive cleanup is needed. Restoration/deletion of old generated outputs is not automatic authorization to touch other bundle evidence.

`EmailWorkflowSwitchScenarioTests` has similarly named proof files but writes under its temporary host root, so it is already isolated.

## Gate shape and decision

Build into a fresh owned artifact root after source is frozen and all current focused tests have completed. Keep the running application's original bin/obj untouched. For each returned solution, discover the complete suite and run unfiltered from the same compiled output with `--no-build --no-restore`, the actual frozen configuration, `--artifacts-path` and a distinct owned results directory. Capture full exit codes/TRX counts. Do not run builds/tests during candidate timing windows and do not start/stop live5032/5214/5210 as part of this gate.

The Integration safety verdict is **conditionally safe**, provided the explicit database override, disabled live/interactive opt-ins, isolated child environment and preserved fixed proof subtree above are verified immediately before execution. It is not safe as an unreviewed invocation inheriting arbitrary workstation environment. The Unit/Components independent review below reaches the same conditional verdict, so the complete three-workspace gate is conditionally safe under these controls. A failing or hanging test must be reported, not repaired by changing production host configuration or enabling a live flag. Deterministic fixture failures do not authorize external provider or mailbox operations.

This is a source-boundary audit, not a formal proof that every dynamic call is side-effect free. It covers the concrete external/configuration/process entry points identified by source searches and the shared fixture roots; exact frozen hashes, discovered counts and resulting execution evidence remain root-owned gates.
## Independent Unit/Components boundary audit

Reviewer: startup_code_analysis, read-only source assessment; no builds or tests run for the audit. Scope:576Unit and199Components C# files, their project references and common/component fixture setup. Conclusion: **conditional pass for all three returned workspaces**, with the same checked52049 bootstrap applied to every separate test process, sequential execution and no overlap with live measurements. This review is not an exhaustive proof of network hermeticity.

- Unit `ProfileTestSupportTests` and `StorageCatalogServiceTests` include `RequiresHostDocker` cases that actually acquire the same unique PostgreSQL lease; the checked override still applies. Their trait name does not imply permission to start/reconfigure arbitrary Docker workloads. Components' default `ComponentTestHarness` creates a PostgreSQL profile at line47, and several component tests create additional explicit profiles, so Components also needs the52049 bootstrap.
- Default temporary fixture profiles explicitly set control-plane, workspace and secret-vault roots. Shared bootstrap reads in-memory profile configuration, not default appsettings/user secrets.
- `LocalWorkspaceProcessHostTests` run real owned process wait/termination scenarios in temporary working directories with cleanup and serial collection controls. `DurableFileWriterTests` uses its own dotnet helper. Repository hygiene checks run read-only `git ls-files`. These are expected test-owned process effects, not application-host replacement. Respect their normal runner collection boundaries.
- `DockerHostToolServicePortabilityTests` uses a RecordingHost/temporary fake Docker executable, not a real Docker API mutation. Integration's separately documented read-only Docker probe remains the one normal daemon contact identified in these three suites.
- `OpenAiChatCompletionsRealClientWireTests` and `WorkflowExecutorTests` create and dispose ephemeral `IPAddress.Loopback:0` listeners. Other inspected provider/SDK client tests use explicit capturing/fake handlers. Registering a real plugin client in a DI test does not itself call it; executable plugin tests use fake ports.
- Residual boundary: ComponentTestHarness line60 registers real `DevelopmentManagerClient`; its default test profile points at127.0.0.1:6407 with ReviewBeforeSend enabled. No component test references/calls its send methods or tuning controls were found. Do not describe the suite as network-hermetic solely because it is bUnit. No bare HttpClient or explicit external socket target was found in that scan.

Root may run the single named frozen broad gate once after preserving the fixed Integration proof subtree and verifying the child-environment controls. Failure of any identity/isolation check blocks the run rather than allowing a fallback to live profiles, default5432, live provider flags or an external acceptance workspace.