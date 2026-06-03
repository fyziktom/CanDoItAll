# SB07 - Composition registration and runtime smoke

## Status

Not started.

## Objective

Prove the real app composition registers the process tool provider and that governed process automation can still execute without losing process evidence/tool receipts.

## Covered Inputs

- User request to decouple MAF from Processes in small safe steps.
- `inputs/01-source-artifacts.md`
- `analysis/01-current-state.md`
- `inventories/01-process-tool-parity-inventory.md`
- `evidence/checklists/MAF_Processes_Decoupling_Checklists.xlsx`

## Prerequisites

- SB06 closure gate passed.

## Exact Source References

- `repo://src/CanDoItAll.Composition/RuntimeHostServiceCollectionExtensions.cs`
- `repo://src/CanDoItAll.Modules.AgentFramework/Services/AgentFrameworkModuleServiceCollectionExtensions.cs`
- `repo://src/CanDoItAll.Modules.Processes/Services/ProcessesModuleServiceCollectionExtensions.cs`
- `repo://tests/CanDoItAll.Tests.Support/TestApplicationBootstrap.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessOutboxIntegrationTests.cs`
- `repo://tests/CanDoItAll.Tests.Playwright`

## Deliverables

- Runtime DI proof that process provider is registered when Processes module is loaded.
- Runtime DI proof that MAF still starts with no process provider.
- Targeted process automation smoke preserving tool receipts/evidence.
- Execution report browser/runtime analytics updated.

## Dependency Impact

- Compile-time tests are insufficient. SB08/SB09 require runtime proof before docs/final closure.


## Validation Depth

- Critical foundation. Requires semantic adequacy proof, artifact-backed manifest, source assertions, anti-stub audit, and downstream smoke where named in the progression gate.


## Implementation Steps

1. Inspect app/module composition paths for Tooling/Process provider registration.
2. Add or update integration smoke for provider registration.
3. Run a minimal process automation path that uses process read/write tools or seeded process execution.
4. Verify current-run artifact lineage/tool receipts are still present when they existed before.
5. If a UI process page is affected, run Playwright smoke and screenshot; otherwise record browser proof as N/A with reason.
6. Capture provider usage observations if provider-backed agent calls occurred.

## Scope Exceptions

- Full process-core split is intentionally out of scope.
- Full driver-pack architecture is intentionally out of scope.

## Do Not Do

- Do not change process dispatcher behavior.
- Do not start process core extraction.
- Do not introduce DotNet/SWDev/business process drivers.
- Do not remove or rename any process tool.

## Acceptance Checklist

- [ ] App composition registers process provider.
- [ ] MAF can run without process provider.
- [ ] Process automation smoke passes.
- [ ] No missing tool receipts due to migration.
- [ ] No artifact lineage regression due to migration.
- [ ] Browser logging updated or explicitly marked N/A.

## Proof Required

- `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter ProcessOutbox` or better targeted process automation transcript
- `dotnet test tests\CanDoItAll.Tests.Playwright\CanDoItAll.Tests.Playwright.csproj --filter AgentFrameworkAuditProof` if UI/runtime path impacted
- Runtime provider registration assertion transcript
- `proof/SB07/manifest.md`
- `proof/SB07/semantic-invariants.md`

## Browser Validation Logging

- No browser validation required unless runtime UI smoke reveals a rendered-regression risk. Record `N/A` in execution report if no browser route is exercised.


## Progression Gate

- Pass only when real composition proof exists, not only unit tests.


## Suggested Agent Prompt

Use `shared-prompts/implementation-prompt.md`. Focus only on SB07. Do not start the next subbundle until the SB07 closure gate passes and proof artifacts are written.
