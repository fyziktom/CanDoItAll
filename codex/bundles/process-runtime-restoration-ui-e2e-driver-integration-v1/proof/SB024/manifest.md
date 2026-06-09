# SB024 Proof Manifest

Status: Passed.

## Scope

Gate H covers `P08: MAF/workflow/direct-agent execution compatibility`.

No production runtime, driver, Core, UI, scheduler, workflow, shell, Office, Graph, workspace/storage, or process mutation implementation code was changed in SB022-SB024. The gate relies on existing focused integration tests: one workflow-backed process route and one deterministic direct-agent route through the process mock provider.

## Command Transcripts

- `bundle://proof/SB022/transcripts/maf-workflow-direct-agent-inventory.txt`
- `bundle://proof/SB023/transcripts/focused-maf-workflow-direct-agent-source-assertions.txt`
- `bundle://proof/SB024/transcripts/focused-maf-workflow-direct-agent-runtime-tests.txt`
- `bundle://proof/SB024/transcripts/anti-stub-maf-workflow-direct-agent-runtime-tests.txt`
- `bundle://proof/SB024/transcripts/forbidden-drift-scan.txt`
- `bundle://proof/SB024/transcripts/prepared-validator-after-sb024.txt`
- `bundle://proof/SB024/transcripts/changed-file-hashes.txt`

## Source Assertions

- SB022 inventory proves MAF runtime, workflow executor catalog, workflow process executor bridge, process workflow coordinator, workflow route handler, direct-agent route handler, and process mock deterministic provider options are all wired in current source.
- SB023 source assertions prove `DispatchAsync_runs_workflow_assignment_and_projects_process_link` covers workflow-backed process dispatch through real workflow catalog/component services, `ProcessesService`, and `IProcessRunAutomationDispatchService`.
- SB023 source assertions prove `Process_mock_workflow_process_completes_end_to_end_through_durable_outbox_dispatch` covers deterministic direct-agent route execution through process mock agents and MAF execution run records.

## Test Proof

`dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --no-restore --filter "FullyQualifiedName~DispatchAsync_runs_workflow_assignment_and_projects_process_link|FullyQualifiedName~Process_mock_workflow_process_completes_end_to_end_through_durable_outbox_dispatch"` passed with 2 tests:

- `DispatchAsync_runs_workflow_assignment_and_projects_process_link`
- `Process_mock_workflow_process_completes_end_to_end_through_durable_outbox_dispatch`

## Anti-Stub And Adversarial Proof

- The anti-stub audit confirms the workflow test starts a real `TestApplication`, resolves workflow catalog/component/process/dispatch services through DI, saves a real workflow-backed process definition, starts a real run, dispatches through `IProcessRunAutomationDispatchService`, and asserts step completion, workflow run completion, workflow assignment identity, and workflow-run artifact projection.
- The anti-stub audit confirms the direct-agent test starts a real application, resolves deterministic process mock catalog and process/outbox/workspace services through DI, executes a launch plan, drains the real outbox, and asserts MAF execution run completion and successful outcomes.
- The audit rejects bundle paths, sleeps, test-server shortcuts, direct update shortcuts, and raw SQL mutation inside both scoped runtime tests.
- The adversarial proof fails if workflow assignment resolution breaks, if workflow dispatch cannot create/project a workflow run link, if direct-agent dispatch cannot create successful MAF execution runs, or if either route stops completing through process runtime services.

## Forbidden Drift

`bundle://proof/SB024/transcripts/forbidden-drift-scan.txt` confirms:

- no production source files under `repo://src` changed in SB022-SB024;
- no transient `codex/bundles` path references exist in source/test code;
- no runtime host, registry, selector, driver DI registration, manager command, scheduler/workflow hook, shell execution, Office/Graph call, workspace/storage write, or read-only driver mutation path was introduced by this gate.

## Changed-File Hashes

See `bundle://proof/SB024/transcripts/changed-file-hashes.txt`.

## Downstream Dependency Check

SB025-SB027 can rely on process runtime compatibility with both workflow-backed process roles and deterministic direct-agent routes. Scenario phases still need representative `.NET app` create/modify proof and concrete output/evidence assertions.
