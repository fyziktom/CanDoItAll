# contract-fidelity-and-static-output

## Status

- `Ready`

## Objective

Prevent downstream implementation and validation from silently drifting away from the upstream delivery contract. For the Tetris run, `WASM`, `static website`, `no backend`, and the selected run root are hard constraints unless a new contract artifact explicitly changes them.

## Covered Inputs

- N002, N003, N006.
- Requirements R003, R004.

## Prerequisites

- SB01 may run in parallel for implementation, but SB04 cannot start until this subbundle passes.
- Read `bundle://evidence/01-blazor-delivery-contract.md` and `bundle://evidence/03-blazor-runtime-evidence-pack.md`.

## Exact Source References

- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ExecutionPrompt.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Grounding.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ProjectPaths.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ImplementationProof.cs`
- `repo://src/CanDoItAll.AgentFramework.Core/ToolPolicy/AgentToolInvocationPolicy.cs`
- `repo://src/CanDoItAll.AgentFramework.Persistence/SeedAssets/instructions/skills/dotnet-app-delivery.md`
- `repo://src/CanDoItAll.AgentFramework.Persistence/SeedAssets/instructions/skills/blazor-ssr-delivery.md`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`
- `repo://tests/CanDoItAll.Tests.Unit/AgentToolInvocationPolicyTests.cs`

## Deliverables

- Prompt/policy/runtime hardening that carries selected contract mode, product root, run folder, and exclusions into implementation and validation.
- Detection or rejection of server-hosted Blazor output for a static/WASM/no-backend contract.
- Detection or rejection of alternate shadow roots that are not the contracted root.
- Focused tests or source assertions covering the Tetris contract drift case.

## Dependency Impact

- SB03 depends on this because browser proof must inspect the correct application shape.
- SB04 depends on this because rerun success is invalid if the output violates the contract.

## Validation Depth

- Critical foundation.
- Requires Semantic Adequacy Gate proof in `bundle://proof/SB02/manifest.md` and `bundle://proof/SB02/semantic-invariants.md`.

## Implementation Steps

1. Add/adjust contract grounding so downstream prompts treat selected mode/root/exclusions as hard constraints.
2. Add proof validation that reads actual produced project files, not only agent summaries.
3. Reject `Microsoft.NET.Sdk.Web`, `AddInteractiveServerComponents`, `AddInteractiveServerRenderMode`, or server-host-only output when contract says WASM/static/no backend.
4. Reject output roots that differ from the contract unless an updated contract artifact is present.
5. Add focused tests with a contract like `01-blazor-delivery-contract.md` and a bad output like the observed `MainApp` server host.

## Scope Exceptions

- Do not force all future Blazor tasks to be WASM; enforce the selected contract for the current run.
- Do not remove supported SSR delivery paths for tasks whose contract selects SSR.

## Do Not Do

- Do not rely on generated validation prose that says acceptance passed.
- Do not let `Blazor app delivery` imply SSR when the contract says WASM/static.
- Do not infer product root from the project name when the contract names a run folder.

## Acceptance Checklist

- [ ] Downstream prompts/policies include selected mode/root/static constraints.
- [ ] A server-hosted output is rejected for the Tetris static/WASM contract.
- [ ] Alternate-root output is rejected or requires a contract revision.
- [ ] Focused tests/source assertions pass.

## Proof Required

- `bundle://proof/SB02/manifest.md` with changed-file hashes, validation transcripts, source assertions, anti-stub audit, and production behavior artifact matrix if new validation signals are introduced.
- `bundle://proof/SB02/semantic-invariants.md` with shallow-pass trap, adversarial negative proof, semantic positive proof, anti-stub audit, and raw-note literal closure.

## Browser Validation Logging

- N/A for this subbundle unless a small proof app is launched to demonstrate validation. If launched, record route, viewport, screenshot, and result in `reviews/01-execution-report.md`.

## Progression Gate

- SB03 and SB04 may proceed only after a test or source assertion proves the observed `MainApp` server-host drift would not be accepted for the Tetris contract.

## Suggested Agent Prompt

```text
Implement only SB02. Make the delivery contract authoritative for downstream implementation and validation. Prove that the observed server-hosted MainApp/root drift would fail under a WASM/static/no-backend contract while legitimate SSR contracts remain allowed.
```
