# SB03 Semantic Invariants

## Invariants

| Invariant | Proof |
| --- | --- |
| Workflow core services are workflow-owned and no longer duplicated in `AgentFramework.Core\Workflows`. | `WorkflowCoreImplementationFilesMovedOutOfAgentFrameworkCoreProject` and `moved-file-audit.txt` verify the moved files are absent from the old location and present in `CanDoItAll.AgentFramework.Workflows.Core`. |
| Workflow core does not depend on MAF, Blazor modules, plugin implementations, persistence implementations, or web projects. | `WorkflowCoreProjectDoesNotReferenceForbiddenImplementationProjects` and `dependency-boundary.txt` scan project references for forbidden dependencies. |
| Host/module startup uses explicit workflow core registration. | `WorkflowCoreRegistrationExtensionOwnsCoreServiceRegistrations`, `HostAndModuleRegistrationUseWorkflowCoreExtension`, and `di-registration-proof.txt` prove core service registration is centralized. |
| Validation and catalog behavior remains compatible. | `workflow-parity-tests.txt` runs workflow core, foundation, catalog, preview simulation, and settings schema parity tests with 0 failures. |
| Routing, payload policy, and executor observability behavior remains compatible. | `executor-routing-policy-tests.txt` runs executor, routing, policy, and observability tests with 0 failures. |
| Validation/catalog diagnostics are typed, repairable, redacted, and compatible with exact `InvalidOperationException` callers. | `CatalogValidationFailureCarriesTypedRepairableDiagnostics` proves diagnostics are attached under `WorkflowFailureDiagnosticMapper.ExceptionDataKey` while the thrown exception type remains exactly `InvalidOperationException`. |
| Typed display helpers use diagnostic context instead of exception-string parsing. | `TypedFailureDisplayUsesDiagnosticContext` proves node id, executor id, and repair hint are rendered from `WorkflowFailureDiagnosticEnvelope`. |
| SB03 did not introduce placeholders, stubs, loose object dictionaries, or unimplemented production paths. | `anti-stub-audit.txt` reports no placeholder/fake/unimplemented markers and no loose object dictionary payload patterns in SB03 source/test files. |

## Shallow-Pass Trap

A shallow implementation could add a new project and leave the old core services in place, or move files without preserving behavior. SB03 proof catches that shape:

- Leaving duplicated old files fails `WorkflowCoreImplementationFilesMovedOutOfAgentFrameworkCoreProject` and `moved-file-audit.txt`.
- Adding MAF, module, plugin, persistence, or web dependencies fails `WorkflowCoreProjectDoesNotReferenceForbiddenImplementationProjects` and `dependency-boundary.txt`.
- Keeping ad hoc startup registrations fails `HostAndModuleRegistrationUseWorkflowCoreExtension` and `di-registration-proof.txt`.
- Returning string-only catalog failures fails `CatalogValidationFailureCarriesTypedRepairableDiagnostics`.
- Breaking existing workflow validation, catalog, routing, preview, settings, payload, or executor policy behavior fails the parity subsets.

## Adversarial Negative Proof

`proof/SB03/transcripts/focused-workflow-core-tests.txt` runs `CatalogValidationFailureCarriesTypedRepairableDiagnostics` with an invalid missing-start workflow created through the explicit invalid fixture path. The catalog save operation fails with exact `InvalidOperationException`, and the attached typed diagnostic carries:

- `WorkflowFailureKind.Validation`
- missing start node context
- workflow source context
- `RetryableAfterRepair`
- a concrete repair hint
- redacted technical detail

`proof/SB03/transcripts/workflow-parity-tests.txt` and `proof/SB03/transcripts/executor-routing-policy-tests.txt` also include invalid routing, invalid settings, unsupported backend, unsafe retry/policy, and executor failure cases from the existing workflow suites.

## Semantic Positive Proof

`proof/SB03/transcripts/workflow-parity-tests.txt` proves valid workflow validation, catalog, preview simulation, and settings schema behavior after extraction. `proof/SB03/transcripts/executor-routing-policy-tests.txt` proves executor routing, payload policy, and observability behavior remains compatible after moved core services are consumed through the new project.

## Anti-Stub Audit

`proof/SB03/transcripts/anti-stub-audit.txt` scans SB03 production and focused test files for placeholder implementation markers, fake/stub markers, unimplemented exceptions, and loose object dictionary diagnostic payloads. The audit passes with no matches.

## Residual Risk

- `CanDoItAll.AgentFramework.Workflows.Core` still references `CanDoItAll.AgentFramework.Core` because runtime/store/executor contracts remain there until SB04 and SB06. This is intentional migration state, not a final target boundary.
- Moved classes retain the `CanDoItAll.AgentFramework.Core` namespace in SB03 to contain source churn. A later subbundle may rename namespaces only with explicit compatibility proof.
- Runtime manager, run/checkpoint/artifact/external-request stores, execution backend contracts, MAF adapter, executor contracts, plugin executors, template loading, API/UI, and Workbench adoption remain downstream work.

## Completed Validator Semantic Contract Addendum

- Invariant ID: SB03-final-closure
- Source raw note: R01-R18 workflow-node project isolation closure evidence for SB03.
- Expected behavior: The SB03 scope remains closed by its recorded proof artifacts and downstream SB14 final regression.
- Disallowed shallow implementation: Do not replace the recorded source/test proof with summary-only closure or silent fallback behavior.
- Failing-first test: N/A - process/no production behavior metadata addendum; adversarial negative proof remains in the SB03 transcript set where applicable.
- Passing test: See bundle://proof/SB03/transcripts/ for the SB03 passing command transcript set and SB14 final regression transcripts.
- Changed source files: See bundle://proof/SB03/manifest.md and bundle://proof/SB14/changed-file-hashes.txt for the final closure hash set.
- Production assertions: Production behavior is asserted by the SB03 proof chain and SB14 final unit/component/integration/browser regression.
- Red-team negative case: SB14 no-fallback, no-generic, anti-stub, and responsibility audits guard the final state.
- Downstream dependency check: SB14 final closure revalidated downstream workflow, executor, plugin, template, MAF adapter, API, UI, Workbench, and process integration paths.
