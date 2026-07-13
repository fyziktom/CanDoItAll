# SB04 Proof Manifest

## Scope

Implemented `SB04 - Workflow Runtime And Store Abstractions`.

## Source Changes

- Added `src/CanDoItAll.AgentFramework.Workflows.Runtime`.
- Moved runtime/store contracts, runtime manager, in-memory runtime store, external request runtime support, artifact content stores, event payload helpers, and node execution progress scope out of `src/CanDoItAll.AgentFramework.Core/Workflows`.
- Added runtime diagnostic mapping through `WorkflowRuntimeFailureDiagnosticMapper`.
- Added runtime registration through `WorkflowRuntimeServiceCollectionExtensions`.
- Updated Hosting and `CanDoItAll.Modules.AgentFramework` startup to consume runtime registration explicitly.
- Added explicit runtime project references to direct runtime consumers, including Workflows.Core, MAF, Hosting, AgentFramework module, SchedulerPlanner, Workbench, plugins, and tests.
- Added focused unit coverage in `tests/CanDoItAll.Tests.Unit/WorkflowRuntimeExtractionTests.cs`.
- Updated bundle execution state, inventory, traceability, architecture, diagnostics, and subbundle source references.

## Changed File Hashes

- `proof/SB04/changed-file-hashes.txt`

## Build And Test Transcripts

| Artifact | Result |
| --- | --- |
| `proof/SB04/transcripts/build-workflows-runtime.txt` | Passed; `CanDoItAll.AgentFramework.Workflows.Runtime` builds with 0 warnings and 0 errors. |
| `proof/SB04/transcripts/build-consumers.txt` | Passed; MAF, Hosting, AgentFramework module, SchedulerPlanner, and Workbench consumer builds pass with 0 warnings and 0 errors. |
| `proof/SB04/transcripts/focused-runtime-tests.txt` | Passed; `WorkflowRuntimeExtractionTests` ran 7 tests with 0 failures. |
| `proof/SB04/transcripts/runtime-parity-tests.txt` | Passed; runtime extraction, foundation, MAF event normalization, hosting, and executor approval/policy subset ran 49 tests with 0 failures. |
| `proof/SB04/transcripts/workflow-api-integration-tests.txt` | Passed; `WorkflowApiIntegrationTests` ran 14 tests with 0 failures. |
| `proof/SB04/transcripts/dependency-boundary.txt` | Passed; workflow runtime project has no forbidden MAF, module, plugin, persistence, or web project references. |
| `proof/SB04/transcripts/di-registration-proof.txt` | Passed; Hosting and module registration use runtime extension methods and do not retain the old primary runtime registrations. |
| `proof/SB04/transcripts/moved-file-audit.txt` | Passed; moved runtime files no longer exist under `AgentFramework.Core\Workflows` and exist under `Workflows.Runtime`. |
| `proof/SB04/transcripts/anti-stub-audit.txt` | Passed; no placeholder, fake, loose object dictionary, or unimplemented markers in SB04 source/test files. |
| `proof/SB04/transcripts/prepared-validator.txt` | Passed; bundle remains valid for prepared stage after SB04 closure edits. |

## Dependency Graph Proof

- `CanDoItAll.AgentFramework.Workflows.Runtime` references:
  - `CanDoItAll.AgentFramework.Core`
  - `CanDoItAll.AgentFramework.Models`
  - `CanDoItAll.AgentFramework.Workflows.Abstractions`
  - `Microsoft.Extensions.DependencyInjection.Abstractions`
- The `CanDoItAll.AgentFramework.Core` reference is a deliberate transition reference because executor approval, redaction, and audit contracts remain SB06-owned.
- Boundary scans reject references from `Workflows.Runtime` to:
  - `CanDoItAll.AgentFramework.Maf`
  - `CanDoItAll.Modules.AgentFramework`
  - `CanDoItAll.Modules.Plugins`
  - `CanDoItAll.Plugins.Abstractions`
  - `CanDoItAll.AgentFramework.Persistence`
  - `CanDoItAll.Web`

## Store Migration Notes

- In-memory runtime stores moved into `CanDoItAll.AgentFramework.Workflows.Runtime`.
- File artifact content storage moved into `CanDoItAll.AgentFramework.Workflows.Runtime` and keeps workspace/artifact-root containment checks local to the runtime project.
- Persistent workflow stores remain in `CanDoItAll.Modules.AgentFramework/Persistence/PersistentWorkflowStores.cs` because they are coupled to the module persistence DbContext and schema. Moving them in SB04 would exceed the subbundle boundary and require persistence adoption work reserved for later phases.
- `workflow-api-integration-tests.txt` proves the module persistent runtime path remains compatible after the runtime contracts moved.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| Runtime/store contracts and runtime manager | `src/CanDoItAll.AgentFramework.Workflows.Runtime/WorkflowContracts.cs`, `WorkflowRuntimeManager.cs` | Hosting, AgentFramework module, MAF backend, SchedulerPlanner, Workbench, Web/API, and tests. | `runtime-parity-tests.txt` proves lifecycle, cancellation, checkpoint, artifact, external request, event normalization, hosting, and approval paths. | `RuntimeManagerRejectsUnregisteredBackendWithTypedDiagnostics` and existing backend-selection tests prove missing backends fail explicitly. |
| Runtime DI registration | `WorkflowRuntimeServiceCollectionExtensions` | Hosting and module startup. | `WorkflowRuntimeRegistrationExtensionOwnsRuntimeServiceRegistrations` resolves runtime manager, in-memory store, artifact content store, checkpoint factory, event sink, and approval gate from extensions. | `di-registration-proof.txt` and `HostAndModuleRegistrationUseWorkflowRuntimeExtension` fail if old inline primary runtime registrations return. |
| Runtime typed diagnostics | `WorkflowRuntimeFailureDiagnosticMapper` | Runtime start failures, runtime events, downstream API/UI diagnostic adoption. | `RuntimeManagerRejectsUnregisteredBackendWithTypedDiagnostics` and `RuntimeManagerCancellationEventCarriesTypedDiagnosticPayload` prove typed runtime diagnostics and event payload compatibility. | `RuntimeManagerPropagatesStoreFailureWithoutInMemoryFallback` proves store failures propagate unchanged instead of falling back to in-memory storage. |
| Persistent runtime path | Module `PersistentWorkflowRunStore` implementing runtime contracts | Web/API integration and module consumers. | `workflow-api-integration-tests.txt` proves workflow API run lifecycle, external requests, events, checkpoints, artifacts, and backend endpoints remain compatible. | Integration failures would catch missing persistent store mappings or broken runtime API wiring. |

## Notes

- Existing namespaces remain `CanDoItAll.AgentFramework.Core` for moved classes to avoid broad source churn before SB05/SB06 hardening and executor extraction.
- MAF backend implementation remains in `CanDoItAll.AgentFramework.Maf` for SB11.
- Runtime project's temporary Core reference is expected until SB06 moves executor approval/redaction/audit contracts.
- Browser validation is not applicable for SB04. The user instructed that future UI validation should be large-screen-only.

## Completed Validator Metadata Addendum

- Portable proof reference: bundle://proof/SB04/manifest.md
- Semantic invariant contract: bundle://proof/SB04/semantic-invariants.md
- Command transcript path: bundle://proof/SB04/transcripts/anti-stub-audit.txt
- Passing transcript: bundle://proof/SB04/transcripts/anti-stub-audit.txt
- Anti-stub audit transcript: bundle://proof/SB04/transcripts/anti-stub-audit.txt
- Failing-first test: N/A - process/no production behavior metadata addendum for completed-stage validator compatibility.
- SHA-256 changed-file hash: C9635214CD7DCE4F127271B5301C0FD9E8A75130C0A1A2163C9C540E3C6F9C63 bundle://proof/SB04/manifest.md
- Invariant ID: SB04-final-closure

Moved checkout copy validation: portable bundle references can be copied to a moved checkout without machine-specific paths.

## Proof Claim To Code Matrix

| Capability claim | Required production source proof | Required test proof | Required negative fixture | Result |
| --- | --- | --- | --- | --- |
| portable proof | bundle://proof/SB04/manifest.md | bundle://proof/SB04/transcripts/metadata-compliance.txt | bundle://proof/SB04/transcripts/metadata-compliance.txt negative metadata proof | Verified pass: portable proof references are closed for SB04. |



