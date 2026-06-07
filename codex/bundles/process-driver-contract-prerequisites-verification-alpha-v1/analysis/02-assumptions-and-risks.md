# Assumptions And Risks

## Assumptions

- The current `maf-processes-refactor` branch contains the completed `process-core-evidence-descriptors-driver-contract-roadmap-v1` bundle.
- Runtime/service/Core work remains backend-only unless UI files unexpectedly change.
- Domain drivers must eventually support at least `.NET/Rust`, Office, and business-analysis helper lanes.
- The first practical alpha should be verification-only, most likely `.NET/Rust transcript verifier`.

## Critical Path Risks

1. **Premature production driver API**
   - Risk: a driver interface, registry, DI registration, runtime selector, or manager command appears before permission/audit/sandbox rules are enforceable.
   - Mitigation: source scans and architecture tests must fail on production driver tokens.

2. **Core grows into runtime orchestration**
   - Risk: EF, workspace/storage, AgentFramework execution, transitions, claims, finalizer, or file IO leaks into Core.
   - Mitigation: preserve Core forbidden dependency tests and add public API owner-classification updates.

3. **Permission modes are prose-only**
   - Risk: docs claim read-only behavior without executable denial tests.
   - Mitigation: define negative tests for missing mode, verification-only, manager-readonly, and execution-capable denied mode.

4. **Driver lanes imply hidden execution**
   - Risk: .NET/Rust lane accidentally implies `dotnet test`, shell, package restore, or file writes.
   - Mitigation: current bundle must keep alpha lane as transcript/proof inspection only.

## Validation Risks

- Build-only proof is insufficient.
- Focused tests must cover both positive descriptors and negative denial behavior.
- Full unit tests should be retained because architecture guards are unit-level.
- Integration coverage should focus on process dispatch, subprocess, projection/artifact, finalizer, and execution descriptor paths.

## Reopen Triggers

- Any production driver API token appears.
- Any Core forbidden dependency appears.
- Any UI/media file changes unexpectedly.
- Any subbundle rows collapse into a single row.
- Any permission mode lacks negative tests.
- Any verification-only lane can mutate process state or write artifacts.
