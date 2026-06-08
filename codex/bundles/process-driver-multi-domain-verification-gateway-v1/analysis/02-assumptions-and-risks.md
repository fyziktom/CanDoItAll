# Assumptions And Risks

## Assumptions
- The current branch already contains transcript and runtime evidence verification alpha packages.
- Process Core remains dependency-clean and deterministic.
- The next step may add read-only domain verifiers and a controlled verification gateway, but not runtime host infrastructure.
- Existing UI surfaces are not touched.

## Critical Path Risks
- A controlled verification gateway turns into a generic runtime registry or selector.
- Additional domain verifiers accidentally read files, call external services, or mutate process state.
- Full-unit debt is ignored because focused tests pass.
- Driver packages leak dependencies on Modules, Infrastructure, AgentFramework, workspace, storage, EF, UI, or external connectors.
- Core starts referencing driver abstractions.
- Proof uses status-only rows or non-empty diagnostics instead of artifact-backed semantic adequacy.

## Validation Risks
- Build-only proof misses permission, audit, redaction, hash, no-mutation, and fake-proof failures.
- Focused tests pass while full unit debt accumulates.
- Source scans are too broad or too narrow and miss new runtime/DI/registry/selector tokens.
- Read-only Office/business lanes accidentally allow Graph, task, email, document, or business-record mutation.

## Reopen Triggers
- Any Core reference to driver abstractions.
- Any production driver package reference to Modules, Infrastructure, AgentFramework, EF, workspace, storage, UI, or external connector packages.
- Any production code token suggesting registry, selector, DI registration, manager command, scheduler/workflow hook, shell execution, Office/Graph call, workspace/storage write, process mutation, claim, transition, finalizer, provider repair, or retry scheduling.
- Full unit debt changes from known external debt to current-scope failure.
- Any proof manifest lacks changed-file hashes, source assertions, semantic positive proof, adversarial negative proof, or anti-stub audit.
