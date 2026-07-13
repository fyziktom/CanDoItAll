# SB04 Semantic Invariants

## Invariants

| Invariant | Proof |
| --- | --- |
| Runtime/store files are workflow-owned and no longer duplicated in `AgentFramework.Core\Workflows`. | `WorkflowRuntimeImplementationFilesMovedOutOfAgentFrameworkCoreProject` and `moved-file-audit.txt` verify moved files are absent from the old location and present in `CanDoItAll.AgentFramework.Workflows.Runtime`. |
| Workflow runtime does not depend on MAF, Blazor modules, plugin implementations, persistence implementations, or web projects. | `WorkflowRuntimeProjectDoesNotReferenceForbiddenImplementationProjects` and `dependency-boundary.txt` scan project references for forbidden dependencies. |
| Runtime startup composition is explicit. | `WorkflowRuntimeRegistrationExtensionOwnsRuntimeServiceRegistrations`, `HostAndModuleRegistrationUseWorkflowRuntimeExtension`, and `di-registration-proof.txt` prove runtime manager/store/content/checkpoint/event registrations moved behind runtime extensions. |
| Runtime lifecycle, checkpoints, artifacts, external requests, cancellation, backend selection, hosting composition, MAF event normalization, and approval paths remain compatible. | `runtime-parity-tests.txt` runs the focused runtime/foundation/event/hosting/approval subset with 0 failures. |
| API-facing persistent runtime behavior remains compatible. | `workflow-api-integration-tests.txt` runs `WorkflowApiIntegrationTests` with 0 failures across workflow start/status/events/checkpoints/artifacts/external-request/backend endpoints. |
| Runtime backend failures are typed, repairable, redacted, and tied to backend source context. | `RuntimeManagerRejectsUnregisteredBackendWithTypedDiagnostics` proves backend kind, workflow id, retryability, and repair hint are preserved on the thrown exception. |
| Runtime failure event payloads can carry typed diagnostics. | `RuntimeManagerCancellationEventCarriesTypedDiagnosticPayload` proves a cancellation event payload round-trips a typed `WorkflowFailureDiagnosticEnvelope` from inline JSON. |
| Store failures are explicit and do not fall back to in-memory storage. | `RuntimeManagerPropagatesStoreFailureWithoutInMemoryFallback` proves a store write failure propagates the original exception instance. |
| SB04 did not introduce placeholders, stubs, loose object dictionaries, or unimplemented production paths. | `anti-stub-audit.txt` reports no placeholder/fake/unimplemented markers and no loose object dictionary payload patterns in SB04 source/test files. |

## Shallow-Pass Trap

A shallow implementation could add a runtime project while keeping the old runtime manager and stores in Core, or it could move files but keep registration and diagnostics wired through old ad hoc paths. SB04 proof catches that shape:

- Leaving duplicated old files fails `WorkflowRuntimeImplementationFilesMovedOutOfAgentFrameworkCoreProject` and `moved-file-audit.txt`.
- Adding MAF, module, plugin, persistence, or web dependencies fails `WorkflowRuntimeProjectDoesNotReferenceForbiddenImplementationProjects` and `dependency-boundary.txt`.
- Keeping inline runtime registrations fails `HostAndModuleRegistrationUseWorkflowRuntimeExtension` and `di-registration-proof.txt`.
- Returning string-only backend failures fails `RuntimeManagerRejectsUnregisteredBackendWithTypedDiagnostics`.
- Dropping runtime failure payloads fails `RuntimeManagerCancellationEventCarriesTypedDiagnosticPayload`.
- Hiding a store failure behind in-memory fallback fails `RuntimeManagerPropagatesStoreFailureWithoutInMemoryFallback`.
- Breaking run lifecycle, checkpoint, artifact, external request, event, approval, or API behavior fails the parity and integration subsets.

## Adversarial Negative Proof

`proof/SB04/transcripts/focused-runtime-tests.txt` runs:

- `RuntimeManagerRejectsUnregisteredBackendWithTypedDiagnostics`
- `RuntimeManagerPropagatesStoreFailureWithoutInMemoryFallback`
- project boundary and moved-file tests
- runtime registration tests

These tests prove missing backends fail predictably with typed diagnostics and store write failures propagate instead of silently falling back to another store.

## Semantic Positive Proof

`proof/SB04/transcripts/runtime-parity-tests.txt` proves the existing runtime lifecycle, checkpoint, artifact content, external request, cancellation, MAF event normalization, hosting registration, and approval behavior remains compatible after extraction.

`proof/SB04/transcripts/workflow-api-integration-tests.txt` proves the persistent/module-backed workflow API paths still work after runtime contracts moved.

## Anti-Stub Audit

`proof/SB04/transcripts/anti-stub-audit.txt` scans SB04 production and focused test files for placeholder implementation markers, fake/stub markers, unimplemented exceptions, and loose object dictionary diagnostic payloads. The audit passes with no matches.

## Residual Risk

- `CanDoItAll.AgentFramework.Workflows.Runtime` still references `CanDoItAll.AgentFramework.Core` because executor approval, redaction, and audit contracts remain there until SB06.
- Moved classes retain the `CanDoItAll.AgentFramework.Core` namespace in SB04 to contain source churn.
- `PersistentWorkflowRunStore` remains in `CanDoItAll.Modules.AgentFramework` until persistence and module adoption can be separated safely.
- MAF backend implementation, executor contracts, plugin executor behavior, template loading, API/UI adoption, and Workbench browser-visible validation remain downstream work.

## Completed Validator Semantic Contract Addendum

- Invariant ID: SB04-final-closure
- Source raw note: R01-R18 workflow-node project isolation closure evidence for SB04.
- Expected behavior: The SB04 scope remains closed by its recorded proof artifacts and downstream SB14 final regression.
- Disallowed shallow implementation: Do not replace the recorded source/test proof with summary-only closure or silent fallback behavior.
- Failing-first test: N/A - process/no production behavior metadata addendum; adversarial negative proof remains in the SB04 transcript set where applicable.
- Passing test: See bundle://proof/SB04/transcripts/ for the SB04 passing command transcript set and SB14 final regression transcripts.
- Changed source files: See bundle://proof/SB04/manifest.md and bundle://proof/SB14/changed-file-hashes.txt for the final closure hash set.
- Production assertions: Production behavior is asserted by the SB04 proof chain and SB14 final unit/component/integration/browser regression.
- Red-team negative case: SB14 no-fallback, no-generic, anti-stub, and responsibility audits guard the final state.
- Downstream dependency check: SB14 final closure revalidated downstream workflow, executor, plugin, template, MAF adapter, API, UI, Workbench, and process integration paths.
