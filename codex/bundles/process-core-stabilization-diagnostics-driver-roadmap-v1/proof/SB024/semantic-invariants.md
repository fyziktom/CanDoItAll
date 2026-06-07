# SB024 Semantic Invariants

## Raw Note Closure
- Raw note owned: stabilize Process Core by making module Core consumers explicit and dependency-guarded.
- Literal closure: Core remains a pure rules/read-model project; module runtime orchestration, persistence, storage, UI, and driver concerns stay outside Core.

## Shallow-Pass Trap
- A shallow pass would keep the project-wide Core global using or use a broad dispatch-directory exemption.
- This gate requires an exact file allow-list, no Core global using, Core forbidden-dependency scans, project-reference scans, build proof, and dispatch integration proof.

## Semantic Positive Proof
- `Process_core_stabilization_SB022_INV_001_limits_process_core_consumers_to_explicit_call_site_map` proves dispatch Core consumers exactly match `architecture/05-core-consumer-allowed-call-site-map.md`.
- `Process_core_stabilization_SB023_INV_001_hardens_core_dependency_guard_against_runtime_and_driver_dependencies` proves Core dependency scans reject runtime, storage, workspace, EF, driver, service-provider, and logger tokens.
- `ProcessRunAutomationDispatchServiceTests` passed with 539 tests after removing the global Core routing using.

## Adversarial Negative Proof
- Hidden Core consumption through `GlobalUsings.cs` is rejected.
- New dispatch files containing `CanDoItAll.Processes.Core` fail unless added to the exact allow-list and map.
- Core package references, non-contract project references, driver tokens, EF tokens, file IO, storage, workspace path services, and logger/service-provider dependencies are rejected.

## Anti-Stub Audit
- `bundle://proof/SB024/transcripts/anti-stub-audit.txt` found no TODO, NotImplemented, stub, or fixture-specific markers in changed Core consumer boundary production files.

## Boundary Proof
- No production process driver API was introduced.
- No new Core side effects were added.
- No UI, browser, mobile, or media files were changed.
