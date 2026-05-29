# SB07 semantic invariants

Status: Completed

## Backend Availability

- Invariant ID: `SB07-BACKEND-AVAILABILITY`
- Source raw note: R9 requires backend catalog and UI/API surfaces to align with actually registered/runnable backends.
- Expected behavior: only registered backends are runnable; planned durable backends are visible but unavailable with an availability reason.
- Disallowed shallow implementation: listing DurableTask or AzureFunctions as runnable without a registered implementation.
- Failing-first test: `bundle://proof/SB07/transcripts/failing-first-backend-honesty-unit-tests.txt` failed before availability fields and validation existed.
- Passing test: `bundle://proof/SB07/transcripts/unit-backend-honesty-after-implementation.txt`, `bundle://proof/SB07/transcripts/integration-backend-honesty-after-implementation.txt`, and `bundle://proof/SB07/transcripts/component-backend-honesty-after-implementation.txt` passed.
- Changed source files: `repo://src/CanDoItAll.AgentFramework.Models/Workflows/WorkflowModels.cs`, `repo://src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowDefinitionValidator.cs`, `repo://src/CanDoItAll.Modules.AgentFramework/Pages/Components/WorkflowCanvasEditor.razor`, and tests listed in `bundle://proof/SB07/manifest.md`.
- Production assertions: `bundle://proof/SB07/transcripts/source-assertions-backend-honesty.txt` verifies availability descriptors, policy validation, API/UI surfaces, DI registration, and defaults.
- Red-team negative case: unavailable DurableTask/AzureFunctions save, test-run, and start requests are rejected before runtime dispatch.
- Downstream dependency check: `bundle://proof/SB07/transcripts/integration-workflow-api-class-after-implementation.txt`, `bundle://proof/SB07/transcripts/component-workflows-page-class-after-implementation.txt`, and `bundle://proof/SB07/transcripts/build-after-sb07.txt` passed.

- Runtime backend descriptors must distinguish registered/runnable backends from planned/unregistered backends.
- The current host registers `InProcess` as runnable by default.
- `DurableTask` and `AzureFunctions` must remain planned and non-runnable unless an implementation explicitly registers them.
- Planned backends must carry an availability reason suitable for API and UI display.

## Runtime Policy Honesty

- Invariant ID: `SB07-RUNTIME-POLICY-HONESTY`
- Source raw note: R9 requires users not to believe durable production workflows are supported when only in-process execution is registered.
- Expected behavior: unavailable durable backend policies fail save, test-run, and start without falling back to in-process.
- Disallowed shallow implementation: silently substituting `InProcess` for a requested durable backend.
- Failing-first test: `bundle://proof/SB07/transcripts/failing-first-backend-honesty-unit-tests.txt`.
- Passing test: `bundle://proof/SB07/transcripts/integration-backend-honesty-after-implementation.txt`.
- Changed source files: `repo://src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowCatalogServices.cs`, `repo://src/CanDoItAll.Modules.AgentFramework/Persistence/PersistentWorkflowStores.cs`, and `repo://src/CanDoItAll.Web/Api/WorkflowsApi.cs`.
- Production assertions: `bundle://proof/SB07/transcripts/source-assertions-backend-honesty.txt`.
- Red-team negative case: `Workflow_api_rejects_unregistered_durable_backend_start_request` rejects DurableTask while unregistered.
- Downstream dependency check: `bundle://proof/SB07/transcripts/unit-workflow-backend-class-slices-after-implementation.txt`.

- A workflow runtime policy that prefers an unavailable backend must fail validation before save, test-run, or start.
- A workflow runtime policy that requires durable production execution must fail when no durable backend is registered.
- The runtime must not silently substitute `InProcess` for `DurableTask` or `AzureFunctions`.
- Default workflow settings, seed settings, and template metadata must use in-process preview defaults until durable production execution exists.

## API And UI Visibility

- Route `api/workflows/runtime-backends` must expose availability fields for all cataloged backends.
- The workflow editor runtime selector must disable planned durable backends and display their availability reason.
- Selecting an unavailable backend in the editor must be rejected with an explicit warning.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative-test citations |
| --- | --- | --- | --- | --- |
| Backend availability descriptor fields | `WorkflowRuntimeBackendCatalog` | API, UI, policy validator | Built from registered backend kinds when services are constructed. | `bundle://proof/SB07/transcripts/unit-backend-honesty-after-implementation.txt`; `bundle://proof/SB07/browser-workflow-runtime-backends.json` |
| Unavailable backend validation issues | `WorkflowRuntimePolicyValidator` | catalog save, test-run, start request | Emitted before persistence or execution. | `bundle://proof/SB07/transcripts/integration-backend-honesty-after-implementation.txt` |
| Disabled runtime backend options | `WorkflowCanvasEditor` | Workflow author | Rendered from catalog descriptors; disabled when `IsRunnable` is false. | `bundle://proof/SB07/transcripts/component-backend-honesty-after-implementation.txt`; `bundle://proof/SB07/browser-workflow-runtime-backends-visible.png` |
| In-process preview default policy | `WorkflowSettings.Default`, example seed, template manifest | new settings and templates | Persisted only as non-durable preview default. | `bundle://proof/SB07/transcripts/source-assertions-backend-honesty.txt` |
