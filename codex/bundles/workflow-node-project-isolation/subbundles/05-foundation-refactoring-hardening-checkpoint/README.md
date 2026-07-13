# SB05 - Foundation Refactoring Hardening Checkpoint

## Status

- `Completed`

## Objective

Force a refactoring-hardening checkpoint after workflow abstractions, builders, core services, runtime services, and stores are extracted. This checkpoint must prove that the base workflow layer is clean before any executor extraction starts.

## Success Criteria

- Workflow abstraction, builder, core, runtime, and store projects compile with the allowed dependency graph.
- Architecture tests prove no MAF, UI, plugin implementation, or persistence implementation dependency leaked into abstractions/builders/core.
- Performance and maintainability scan findings are recorded, triaged, and either fixed or explicitly deferred with ownership.
- Diagnostics, logging, and error behavior remain typed, explicit, actionable, redacted, and free of generic runtime/validation messages.
- Moved foundation files do not remain oversized mixed-responsibility classes under new project names.

## Covered Inputs

- R03, R05, R06, R13, R14, R15, R17, R18.
- Architect note requiring forced refactoring-hardening subbundles after logical blocks.
- Performance skill findings from the preparation scan.

## Prerequisites

- SB02 completed with proof.
- SB03 completed with proof.
- SB04 completed with proof.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Workflows`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Models\Workflows`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf\Runtime\Workflows`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Hosting\AgentFrameworkServiceCollectionExtensions.cs`
- `C:\repositories\CanDoItAll\tests`
- `C:\repositories\CanDoItAll\codex\bundles\workflow-node-project-isolation\architecture\02-project-map-and-adoption-boundary.md`

## Deliverables

- Refactoring-hardening report for the foundation projects.
- Architecture tests or dependency checks for the base project graph.
- Focused performance scan result for workflow core/runtime code.
- No-generic-error and typed diagnostic payload review for validation/runtime/store/artifact failures.
- File-size/responsibility review for moved validator, catalog, runtime, event, artifact, and payload-policy code.
- Cleanup commits limited to foundation quality issues found by the checkpoint.
- Updated execution report gate status.

## Dependency Impact

- SB06 cannot start until this checkpoint passes. Executor abstractions will depend on these workflow contracts and runtime services. If the foundation has unclear ownership, executor projects will inherit the coupling and the migration will fail its main maintainability goal.

## Validation Depth

- `Critical foundation hardening`
- Build, unit, architecture, diagnostics, and focused performance proof.

## Implementation Steps

1. Run focused builds and tests for all workflow foundation projects.
2. Run architecture checks for forbidden references and circular dependencies.
3. Re-run focused performance scans for async misuse, LINQ allocations in hot paths, repeated `JsonSerializerOptions`, regex usage, unbounded collection growth, and string comparison issues.
4. Run no-generic-error assertions for validation, catalog, runtime, store, artifact, checkpoint, cancellation, and external request failures.
5. Review diagnostics/logging in moved services for actionable state, repair hints, retryability, and no sensitive data exposure.
6. Run file-size/responsibility scans and require split helpers when moved classes still mix unrelated responsibilities.
7. Fix only defects inside the foundation extraction scope.
8. Record deferred findings with owning later subbundle when they are outside foundation scope.
9. Update proof manifests, semantic invariants, and execution report gate.

## Scope Exceptions

- Executor-specific issues are deferred to SB06-SB09.
- Template/UI adoption issues are deferred to SB10-SB13.

## Do Not Do

- Do not begin executor extraction.
- Do not use this checkpoint for broad unrelated cleanup.
- Do not waive a forbidden dependency without updating the architecture and getting explicit rationale into the execution report.
- Do not pass the checkpoint if validation/runtime failures still require parsing exception text to recover node, operation, or repair context.

## Acceptance Checklist

- [x] Foundation build and test subset passes.
- [x] Forbidden-reference checks pass.
- [x] Performance scan findings are fixed or explicitly deferred with owners.
- [x] Diagnostics are typed, explicit, repairable, and do not expose sensitive data.
- [x] Moved foundation files pass responsibility/file-size review or have approved helper splits.
- [x] Execution report marks SB05 as passed before SB06 starts.

## Execution Notes

- Split obvious mixed-responsibility foundation files without changing namespaces or public behavior:
  - `InMemoryWorkflowCatalogStore` moved out of `WorkflowCatalogServices.cs`.
  - `WorkflowTestRunner` moved out of `WorkflowCatalogServices.cs`.
  - `WorkflowDefinitionValidationOptions`, `WorkflowRuntimeBackendCatalog`, and `WorkflowRuntimePolicyValidator` moved out of `WorkflowDefinitionValidator.cs`.
  - `InMemoryWorkflowRunStore` and `NullWorkflowEventSink` moved out of `WorkflowRuntimeManager.cs`.
  - `InMemoryWorkflowArtifactContentStore` moved out of `WorkflowArtifactContentStores.cs`.
- Added `WorkflowFoundationHardeningCheckpointTests` to guard the approved foundation dependency graph, forbidden downstream references, typed diagnostics, no loose object diagnostics, and moved-file responsibility limits.
- Performance scan found no critical or moderate open foundation issues. LINQ/list allocation candidates were triaged as informational in validation and in-memory listing paths, owned by SB14 final profiling only if measured hot.
- Browser validation remains N/A for SB05. The user requested large-screen-only UI validation for later UI subbundles.

## Proof Required

- `proof/SB05/manifest.md` with build/test transcripts, architecture check transcript, performance scan transcript, changed file hashes, and deferred finding table.
- `proof/SB05/semantic-invariants.md` covering dependency cleanliness, typed explicit failures, redaction, repair hints, no fallback ownership, file responsibility, and foundation service parity.
- Semantic Adequacy Gate proof including shallow-pass trap, adversarial dependency violation proof, positive workflow foundation proof, anti-stub audit, and raw-note literal closure.

## Browser Validation Logging

- `N/A`. This checkpoint is backend architecture and service hardening.

## Progression Gate

- SB06 is blocked until SB05 passes. Any failed architecture, test, or unresolved foundation performance issue must be fixed or explicitly reassigned before executor abstractions begin.

## Suggested Agent Prompt

```text
Implement SB05 only. Harden the workflow foundation extracted by SB02-SB04. Run builds, tests, architecture checks, typed-diagnostics/no-generic-error review, file-size/responsibility review, and focused performance scans. Fix only foundation-scope defects and record proof. Do not start executor extraction.
```
