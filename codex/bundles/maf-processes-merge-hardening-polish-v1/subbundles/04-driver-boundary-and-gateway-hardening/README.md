# Driver boundary and gateway hardening

## Status

- `Ready`

## Objective

Reinforce architecture tests and source scans so Process Core, domain drivers, the verification gateway, and MAF/process decoupling remain stable after the polishing extraction.

## Success Criteria

- Process Core references only allowed packages.
- Driver packages do not reference Modules, Infrastructure, AgentFramework, EF, UI, plugins, workspace/storage, or external connector packages.
- MAF still has no compile-time dependency on Processes.
- Verification gateway remains explicit typed read-only dispatch with no runtime host, registry, selector, DI discovery, object payload, dynamic payload, or reflection scanning.
- Driver tests have semantic names and no work-package IDs.
- Any new software-delivery domain driver is covered by the same no-mutation/no-external-call policies.

## Covered Inputs

- Current MAF decoupling objective.
- Current Process Core isolation objective.
- Current driver packages and verification gateway.
- SB03 domain extraction.

## Prerequisites

- SB01, SB02, and SB03 complete.

## Exact Source References

- `src/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj`
- `src/CanDoItAll.AgentFramework.Maf/**`
- `src/CanDoItAll.Processes.Core/CanDoItAll.Processes.Core.csproj`
- `src/CanDoItAll.Processes.Core/**`
- `src/CanDoItAll.Processes.Drivers.*/**`
- `src/CanDoItAll.Processes.Drivers.VerificationGateway/ProcessDriverVerificationGateway.cs`
- `tests/CanDoItAll.Tests.Unit/AgentRuntimeHardeningStaticRegressionTests.cs`
- `tests/CanDoItAll.Tests.Unit/ProcessDriverVerificationGatewayTests.cs`
- any new tests from SB01-SB03

## Deliverables

- Update or add architecture tests that scan project references and source content for forbidden dependencies.
- Update gateway tests after any new software-delivery lane is added.
- Keep tests semantic and behavior-oriented.
- Add a compact boundary matrix to process/module README only if docs need to reflect the new driver/adapter seam.

Suggested boundary test coverage:

1. `Maf_runtime_has_no_compile_time_processes_module_dependency` remains and includes all MAF `.cs`, `.csproj`, `.props`, `.targets`, `.md`, and `.razor` files.
2. `Process_core_has_only_contract_dependency` verifies `CanDoItAll.Processes.Core.csproj` and source text.
3. `Process_driver_projects_are_verification_only_and_module_independent` loops over `src/CanDoItAll.Processes.Drivers.*/*.csproj` and source files.
4. `Process_driver_gateway_has_only_explicit_typed_lanes` rejects:
   - `Verify(ProcessDriverVerificationGatewayLane`,
   - `object payload`, `object request`, `dynamic`,
   - `IServiceProvider`, `GetRequiredService`, `Assembly.GetTypes`, `Activator.CreateInstance`,
   - dictionaries/maps from lane to delegate if they imply generic dispatch,
   - manager/scheduler/workflow command hooks.
5. `Process_drivers_reject_side_effect_operations` covers all lanes, including software delivery if added.
6. `Repository_tests_do_not_contain_work_package_identifiers` remains green.

## Dependency Impact

- SB05 final validation depends on this subbundle for architecture confidence.

## Validation Depth

- Architecture-critical boundary closure.

## Implementation Steps

1. Run architecture source scans before editing.
2. Add/update tests listed above.
3. If SB03 added `SoftwareDeliveryEvidenceRead`, update:
   - `ProcessDriverVerificationGatewayLane`,
   - `ProcessDriverVerificationGatewayLaneRules`,
   - `ProcessDriverVerificationGateway`,
   - `ProcessDriverVerificationBatch`,
   - tests and harness operations.
4. If SB03 used fallback adapter without gateway lane, add a test documenting that software-delivery proof ownership is the only explicit pre-merge exception and that it is read-only.
5. Run focused tests:

```bash
dotnet test tests/CanDoItAll.Tests.Unit --filter "ProcessDriver|ProcessCore|Maf_runtime_has_no_compile_time_processes_module_dependency|RepositoryNamingHygiene|RepositoryTransientArtifactHygiene"
```

6. Record source scan output and test output.

## Scope Exceptions

- Do not add runtime driver hosting.
- Do not require every future domain driver to exist before merge.
- Do not rename public driver types from `Alpha` unless all references and docs can be updated safely; this is not required for merge.

## Do Not Do

- Do not use DI to resolve gateway lanes.
- Do not add generic `Verify(lane, object)` APIs.
- Do not allow a driver package to reference `CanDoItAll.Modules.Processes`.
- Do not weaken tests by only checking one file if source moved to another file.

## Acceptance Checklist

- [ ] MAF boundary test passes.
- [ ] Process Core boundary test passes.
- [ ] Driver package boundary tests pass.
- [ ] Gateway explicit typed lane tests pass.
- [ ] No work-package naming guard failures.
- [ ] No runtime host/registry/selector/DI/discovery surfaces are introduced.

## Proof Required

- Architecture source scan transcript.
- Focused unit test transcript.
- Relevant diff summary for csproj and gateway files.

## Browser Validation Logging

- N/A

## Progression Gate

SB05 may start only after all architecture boundary tests pass and source scans show no forbidden runtime host/generic dispatch/dependency leaks.

## Suggested Agent Prompt

```text
Implement subbundle 04 only. Harden architecture and gateway boundary tests after the domain cleanup. Keep the gateway explicit and typed. Keep drivers verification-only. Do not add runtime hosting, registry, selector, DI discovery, manager/scheduler/workflow hooks, or MAF -> Processes references. Capture source scans and focused unit tests.
```
