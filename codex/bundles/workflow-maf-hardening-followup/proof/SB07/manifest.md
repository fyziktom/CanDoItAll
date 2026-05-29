# SB07 proof manifest

Status: Completed

Semantic invariant contract: `bundle://proof/SB07/semantic-invariants.md`

## Summary

- Split workflow runtime backend descriptors into registered/runnable and planned/unregistered states.
- Registered `InProcess` as the only runnable backend in the current host; `DurableTask` and `AzureFunctions` are visible as planned but unavailable unless explicitly registered.
- Runtime policy validation now rejects unavailable preferred backends and durable-production requirements before save, test-run, or API start.
- Workflow editor and route `api/workflows/runtime-backends` expose unavailable durable backends as disabled with an availability reason.
- Default workflow settings and template metadata now remain in in-process preview mode instead of implying durable production support.

## Source Changes

- `repo://src/CanDoItAll.AgentFramework.Models/Workflows/WorkflowModels.cs`
- `repo://src/CanDoItAll.AgentFramework.Models/Workflows/WorkflowCatalogModels.cs`
- `repo://src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowDefinitionValidator.cs`
- `repo://src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowCatalogServices.cs`
- `repo://src/CanDoItAll.AgentFramework.Hosting/AgentFrameworkServiceCollectionExtensions.cs`
- `repo://src/CanDoItAll.Modules.AgentFramework/Persistence/PersistentWorkflowStores.cs`
- `repo://src/CanDoItAll.Modules.AgentFramework/Services/AgentFrameworkModuleServiceCollectionExtensions.cs`
- `repo://src/CanDoItAll.Modules.AgentFramework/Catalog/WorkflowExampleCatalogSeedService.cs`
- `repo://src/CanDoItAll.Modules.AgentFramework/Pages/Components/WorkflowCanvasEditor.razor`
- `repo://src/CanDoItAll.Modules.AgentFramework/Pages/Components/WorkflowCanvasEditor.razor.cs`
- `repo://src/CanDoItAll.Web/Api/WorkflowsApi.cs`
- `repo://Templates/Workflows/manifest.yaml`
- `repo://tests/CanDoItAll.Tests.Unit/WorkflowFoundationTests.cs`
- `repo://tests/CanDoItAll.Tests.Unit/WorkflowCatalogTests.cs`
- `repo://tests/CanDoItAll.Tests.Unit/AgentFrameworkHostingServiceCollectionTests.cs`
- `repo://tests/CanDoItAll.Tests.Integration/WorkflowApiIntegrationTests.cs`
- `repo://tests/CanDoItAll.Tests.Components/WorkflowsPageTests.cs`

Hash sample: `4b4a8bd0bb440103c22918347420790f95bd335f51d2f64fe30ca0d8058a9465`.

## Proof

- `bundle://proof/SB07/transcripts/failing-first-backend-honesty-unit-tests.txt`
  - Command: targeted backend honesty unit tests before implementation.
  - Result: failed before implementation because backend availability fields and validation did not exist.
- `bundle://proof/SB07/transcripts/unit-backend-honesty-after-implementation.txt`
  - Command: targeted unit filter for backend catalog, policy validation, catalog save rejection, and hosting DI scope validation.
  - Result: 4 passed, 0 failed, 0 skipped.
- `bundle://proof/SB07/transcripts/integration-backend-honesty-after-implementation.txt`
  - Command: targeted workflow API filter for unavailable durable backend test-run, save, catalog, and start rejection.
  - Result: 4 passed, 0 failed, 0 skipped.
- `bundle://proof/SB07/transcripts/component-backend-honesty-after-implementation.txt`
  - Command: targeted workflow canvas component test for planned backend disabled state.
  - Result: 1 passed, 0 failed, 0 skipped.
- `bundle://proof/SB07/transcripts/unit-workflow-backend-class-slices-after-implementation.txt`
  - Command: workflow template, catalog, foundation, and hosting service unit slices.
  - Result: 40 passed, 0 failed, 0 skipped.
- `bundle://proof/SB07/transcripts/integration-workflow-api-class-after-implementation.txt`
  - Command: `WorkflowApiIntegrationTests` class.
  - Result: 13 passed, 0 failed, 0 skipped.
- `bundle://proof/SB07/transcripts/component-workflows-page-class-after-implementation.txt`
  - Command: `WorkflowsPageTests` class.
  - Result: 14 passed, 0 failed, 0 skipped.
- `bundle://proof/SB07/browser-workflow-runtime-backends.json`
  - Route: local workflow editor route `agents/workflows`
  - Result: runtime selector exists; `InProcess` is enabled; `DurableTask (Planned)` and `AzureFunctions (Planned)` are disabled with "planned but not registered" title text.
- `bundle://proof/SB07/browser-workflow-runtime-backends-visible.png`
  - Result: browser screenshot with the runtime selector visible in the workflow editor.
- `bundle://proof/SB07/transcripts/source-assertions-backend-honesty.txt`
  - Command: `rg` source assertion for availability model, policy validation, UI selector, API route, DI registration, and in-process defaults.
  - Result: expected source assertions found.
- `bundle://proof/SB07/changed-file-hashes.txt`
  - Result: SHA-256 hashes captured for SB07 source and test touchpoints.
- `bundle://proof/SB07/transcripts/anti-stub-audit-backend-honesty.txt`
  - Command: narrow anti-stub scan across SB07 touchpoints.
  - Result: no SB07 product TODO or `NotImplementedException`; matches are existing test helper `NotSupportedException` stubs and local "fallback" parameter names unrelated to runtime backend fallback.
- `bundle://proof/SB07/transcripts/build-after-sb07.txt`
  - Command: `dotnet build CanDoItAll.slnx --no-restore`
  - Result: build passed with 0 errors and existing EF Core Relational assembly-version warnings.
- `bundle://proof/SB07/transcripts/git-diff-check.txt`
  - Command: `git diff --check`
  - Result: passed; Git reported line-ending normalization warnings only.
- `bundle://proof/SB07/transcripts/bundle-validator-prepared-after-sb07.txt`
  - Command: `python bundle-preparation validate_bundle.py codex\bundles\workflow-maf-hardening-followup --stage prepared`
  - Result: bundle is valid for stage `prepared`.
- Passing transcript: `bundle://proof/SB07/transcripts/unit-backend-honesty-after-implementation.txt`
- Anti-stub transcript: `bundle://proof/SB07/transcripts/anti-stub-audit-backend-honesty.txt`
- `bundle://proof/SB07/transcripts/semantic-invariant-evidence.txt`
  - Command: semantic invariant transcript index.
  - Result: invariant ids are indexed for completed-stage validation.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| `WorkflowRuntimeBackendDescriptor.Availability`, `IsRegistered`, `IsRunnable`, `AvailabilityReason` | `WorkflowRuntimeBackendCatalog` | API, UI, policy validator, tests | Created from registered backend kinds at service startup; current host registers only `InProcess`. | `unit-backend-honesty-after-implementation.txt`; `integration-backend-honesty-after-implementation.txt`; `browser-workflow-runtime-backends.json` |
| Runtime policy availability issues | `WorkflowRuntimePolicyValidator` | catalog save, test-run, API start | Produced before persistence or execution; unavailable durable policy fails instead of falling back. | `integration-backend-honesty-after-implementation.txt`; `unit-workflow-backend-class-slices-after-implementation.txt` |
| Runtime backend API list | route `api/workflows/runtime-backends` | workflow editor and API clients | Lists registered and planned backends with explicit availability fields. | `bundle://proof/SB07/transcripts/integration-workflow-api-class-after-implementation.txt`; `bundle://proof/SB07/browser-workflow-runtime-backends.json` |
| In-process preview defaults | `WorkflowSettings.Default`, seed service, template manifest | workflow catalog, example seed, template loader | Defaults remain runnable in current host without promising durable production support. | `source-assertions-backend-honesty.txt`; `unit-workflow-backend-class-slices-after-implementation.txt` |

## Skipped

- Real DurableTask and Azure Functions backend execution proof was not run because those backends are intentionally not registered in the current host.
- Live external-effect plugin proof remains outside this subbundle; SB06 covers deterministic fake-mode plugin proof.
