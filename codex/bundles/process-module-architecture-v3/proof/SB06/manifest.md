# SB06 Proof Manifest

## Status

Complete for instance builder and immutable plan compiler foundations.

## Public Surface Added

- `CanDoItAll.Processes.Builder`
  - `ProcessPlanCompileSource`, `ProcessInstancePlanCompileRequest`, and subprocess compile source contracts.
  - `ProcessInstancePlan` with header, definition snapshot, driver stack, strategy bindings, steps, artifact plan, branch route table, subprocess refs, manager plan, budget plan, monitoring plan, security plan, and semantic plan hash.
  - `ProcessInstancePlanCompiler` pipeline split into orchestration, validation, and plan-building partials.
  - `ProcessPlanHasher` for deterministic semantic plan hashes.
  - `IProcessInstancePlanStore` and `PersistedProcessInstancePlan` persistence handoff port.

## Validation

| Gate | Proof |
| --- | --- |
| Unit project build | `transcripts/build-unit-sb06-05.txt` |
| Full solution build | `transcripts/build-solution-sb06-04.txt` |
| Builder/driver/template/core/boundary tests | `transcripts/test-unit-sb06-04.txt` |
| Builder forbidden dependency scan | `transcripts/builder-forbidden-dependency-scan-02.txt` |
| Builder concrete driver name scan | `transcripts/builder-concrete-driver-name-scan-02.txt` |
| Runtime composition logic scan | `transcripts/runtime-composition-logic-scan-02.txt` |
| Builder domain opacity scan | `transcripts/builder-domain-opacity-scan-02.txt` |
| Anti-stub audit | `transcripts/anti-stub-audit.txt` |
| Bundle prepared-stage validator | `transcripts/bundle-validator-prepared-sb06-01.txt` |
| Plan snapshot example | `plan-snapshot-example.md` |
| Scan summary | `transcripts/scan-summary.json` |
| Changed file hashes | `transcripts/changed-file-hashes.txt` |
| CodeAnalytics MCP snapshot | `transcripts/codeanalytics-snapshot-summary.txt` |

## Test Coverage Added

- Golden immutable plan includes driver stack, strategy bindings, artifact slots, branch routes, loop budgets, manager, monitoring, security, and stable hash.
- Missing executable step strategy fails during build.
- Driver capability conflicts fail during build.
- Subprocess cycles fail during build.
- Subprocess depth budget failures are enforced from the root request.
- Valid subprocess definitions compile recursively into child plan refs.
- Backward branch routes without loop budgets fail before runtime.
- Plan hash changes when security policy changes.

## Semantic Adequacy Gate

- Shallow-pass trap: a builder that only copies graph nodes into a plan without binding strategies, checking driver conflicts, enforcing subprocess constraints, or hashing semantic sections would pass shape-only tests but recreate runtime rediscovery.
- Adversarial negative proof: missing strategy, driver conflict, subprocess cycle/depth, and backward-branch-without-budget tests prove invalid plans are rejected before runtime.
- Semantic positive proof: golden plan and recursive subprocess tests prove the compiler resolves drivers/strategies, builds complete plan sections, persists child-plan references, and produces deterministic semantic hashes.
- Anti-stub proof: `transcripts/anti-stub-audit.txt` reports no unresolved stub markers in builder source or SB06 tests.
- Dependent-flow smoke: `transcripts/build-solution-sb06-04.txt` proves Runtime/Application consumers compile against the new immutable plan contracts without adding runtime composition logic.
- Bundle consistency proof: `transcripts/bundle-validator-prepared-sb06-01.txt` proves subbundle source references resolve after removed legacy active paths were repaired to point at the SB01 archive.

## Handoff To SB07

SB07 can consume `ProcessInstancePlan`, `StrategyBindingSet`, `StepInstancePlan`, `ArtifactPlan`, `BranchRouteTable`, `BudgetPlan`, and `IProcessInstancePlanStore` as the runtime input contract. Runtime must require a persisted plan and must not reselect drivers or strategies.
