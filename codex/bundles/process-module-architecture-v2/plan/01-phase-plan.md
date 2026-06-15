# Phase Plan

This is an architecture phase plan. It describes future implementation packages, but v2 does not execute them and does not claim implementation readiness.

## Subbundle Dependency Map

```mermaid
flowchart TD
    FP01["FP01 Archive Old Process Module"]
    FP02["FP02 New Core And Contracts"]
    FP03["FP03 Template And Git Foundation"]
    FP04["FP04 Instance Builder"]
    FP05["FP05 Runtime And Dispatcher"]
    FP06["FP06 Drivers And Strategies"]
    FP07["FP07 Artifacts, Manager, Subprocess"]
    FP08["FP08 Monitoring And Snapshots"]
    FP09["FP09 UI Rebuild"]
    FP10["FP10 Migration, Compatibility, Final Proof"]

    FP01 --> FP02
    FP01 --> FP03
    FP02 --> FP04
    FP03 --> FP04
    FP04 --> FP05
    FP02 --> FP06
    FP06 --> FP05
    FP05 --> FP07
    FP06 --> FP07
    FP05 --> FP08
    FP07 --> FP08
    FP08 --> FP09
    FP04 --> FP09
    FP09 --> FP10
    FP08 --> FP10
    FP07 --> FP10
```

## Critical Subbundles

This heading is retained for current bundle-validator compatibility. The items below are future phase packages, not executable v2 subbundles.

- FP01 is critical because the old implementation must be preserved before removal.
- FP02 is critical because every future project depends on the generic core boundary.
- FP03 is critical because templates and Git-backed source control define source-of-truth behavior.
- FP04 is critical because process instance composition determines selected drivers, strategies, subprocesses, artifacts, branches, manager behavior, and monitoring configuration.
- FP05 is critical because runtime/dispatcher boundaries determine reliability and concurrency behavior.
- FP06 is critical because domain extensibility depends on driver/strategy layering.
- FP07 is critical because artifact recovery, manager behavior, subprocess communication, and loop protection are core reliability requirements.
- FP08 is critical because live/history UX depends on non-blocking event and snapshot projection.

## Phase Gates

| Gate | Required Before | Required Proof |
| --- | --- | --- |
| G01 Reference archive complete | Any deletion | Archive manifest with source paths and hashes. |
| G02 Old Process removed | New implementation projects | Build fails only for intentionally removed references, then new skeleton build restores solution health. |
| G03 Core semantic boundary | Builder/runtime work | Architecture tests prove no EF/Razor/driver/domain references in core. |
| G04 Template/Git source-of-truth | Builder uses templates | JSON schema/migration tests and Git wrapper contract tests pass. |
| G05 Instance composition | Runtime execution | Builder tests prove strategies, drivers, subprocess plans, artifacts, branches, managers, and monitoring config are persisted in plan. |
| G06 Runtime reliability | Manager/artifact workflows | Concurrency, claim lease, event emission, cancellation, retry, and transition invariant tests pass. |
| G07 Driver layering | Domain execution flows | Driver stack selection tests pass without domain terms in core. |
| G08 Artifact/manager/subprocess | Monitoring and UI | Recovery, resupply, parent/child manager communication, and loop protection tests pass. |
| G09 Snapshot projections | UI rebuild | Live/history projection tests prove time filtering and cache semantics. |
| G10 End-to-end quality | Completion | Unit, integration, component, Playwright, migration, and red-team tests pass. |

## Rewrite Order

1. Create rewrite branch.
2. Copy old Process implementation and process tests to bundle/reference material.
3. Remove old Process projects, modules, and process-specific tests.
4. Add new core/contracts projects.
5. Add template and Git foundation.
6. Add builder.
7. Add runtime and dispatcher.
8. Add driver/strategy layer.
9. Add artifact, manager, subprocess, recovery, and loop protection.
10. Add monitoring event stream and snapshot projections.
11. Rebuild UI against projections.
12. Migrate templates and validate compatibility.

## Execution Order

1. Phase 0 Reference Archive And Removal.
2. Core And Contracts.
3. Template And Git Foundation.
4. Instance Builder.
5. Runtime And Dispatcher.
6. Drivers And Strategies.
7. Artifacts, Manager, Subprocess.
8. Monitoring And Snapshots.
9. UI Rebuild.
10. Migration And Final Proof.

Future Codex runs must create fresh implementation bundles from this architecture. They must not execute the v1 implementation subbundle files as if they were approved work packages.
