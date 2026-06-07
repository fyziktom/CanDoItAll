# Assumptions, Risks, and Reopen Triggers

## Assumptions
- The first Core seed is already present at `src/CanDoItAll.Processes.Core`.
- The new bundle may add more pure rules/read models to Core but must not move orchestration.
- The process module remains the owner of application behavior, persistence, AgentFramework execution, and state mutation.
- Driver work remains proposal/documentation/test-only. No production driver API is allowed in this bundle.

## Critical Path Risks
- Moving too much into Core and accidentally importing process module, infrastructure, EF, storage, workspace, AgentFramework, or finalizer dependencies.
- Weakening subprocess behavior by moving lifecycle rules without preserving parent status/reason parity.
- Weakening artifact matching/projection/validation by moving read models but losing trust, sensitivity, validation summary, external reference, or lineage semantics.
- Allowing driver terminology to become a production API, DI registration, manager command, registry, runtime selector, or execution-capable helper.
- Creating broad, vague "core cleanup" changes instead of small pure-family moves.

## Validation Risks
- A build-only proof is insufficient. Require focused parity tests for subprocess and artifact rule families.
- Architecture tests must fail on forbidden Core dependencies, production driver APIs, broad Core extraction, and UI/media drift.
- Core project public API should be scanned for only approved namespaces and types.

## Reopen Triggers
Reopen the current or earlier subbundle if:
- `CanDoItAll.Processes.Core` references anything beyond `CanDoItAll.Processes.Contracts`.
- Any Core file imports `Microsoft.EntityFrameworkCore`, `CanDoItAll.Modules`, `CanDoItAll.Infrastructure`, `CanDoItAll.AgentFramework`, `System.IO`, or storage/workspace types.
- Any production source introduces `IProcessDriverPack`, `IProcessDriverRegistry`, `ProcessDriverRegistry`, `DriverPack`, runtime driver selector, manager command, or DI registration.
- Subprocess lifecycle parity or artifact expectation/matching parity fails.
- Any UI, Razor, CSS, JS, TS, image, screenshot, mobile/small/medium viewport proof appears.
