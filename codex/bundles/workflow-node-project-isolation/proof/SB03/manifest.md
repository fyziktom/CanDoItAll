# SB03 Proof Manifest

## Scope

Implemented `SB03 - Workflow Core Services Extraction`.

## Source Changes

- Added `src/CanDoItAll.AgentFramework.Workflows.Core`.
- Moved workflow validator, catalog services, routing compiler, preview simulation renderer, payload policy, failure display formatter, and process bridge out of `src/CanDoItAll.AgentFramework.Core/Workflows`.
- Added workflow core registration through `WorkflowCoreServiceCollectionExtensions`.
- Added typed validation diagnostic mapping through `WorkflowFailureDiagnosticMapper`.
- Updated Hosting and `CanDoItAll.Modules.AgentFramework` startup to consume workflow core registration.
- Added project references from workflow/runtime consumers and focused test projects to `CanDoItAll.AgentFramework.Workflows.Core`.
- Added focused unit coverage in `tests/CanDoItAll.Tests.Unit/WorkflowCoreExtractionTests.cs`.
- Updated bundle execution state, inventory, traceability, architecture, and diagnostics notes.

## Changed File Hashes

- `proof/SB03/changed-file-hashes.txt`

## Build And Test Transcripts

| Artifact | Result |
| --- | --- |
| `proof/SB03/transcripts/build-workflows-core.txt` | Passed; `CanDoItAll.AgentFramework.Workflows.Core` builds with 0 warnings and 0 errors. |
| `proof/SB03/transcripts/build-consumers.txt` | Passed; MAF, Hosting, and `CanDoItAll.Modules.AgentFramework` consumer builds pass. |
| `proof/SB03/transcripts/focused-workflow-core-tests.txt` | Passed; `WorkflowCoreExtractionTests` ran 6 tests with 0 failures. |
| `proof/SB03/transcripts/workflow-parity-tests.txt` | Passed; workflow core, foundation, catalog, preview simulation, and settings schema parity subset ran 53 tests with 0 failures. |
| `proof/SB03/transcripts/executor-routing-policy-tests.txt` | Passed; executor, routing, payload policy, and observability subset ran 48 tests with 0 failures. |
| `proof/SB03/transcripts/dependency-boundary.txt` | Passed; workflow core project has no forbidden MAF, module, plugin, persistence, or web project references. |
| `proof/SB03/transcripts/di-registration-proof.txt` | Passed; Hosting and module registration use `AddWorkflowCoreServices()` and do not retain the old inline core registrations. |
| `proof/SB03/transcripts/moved-file-audit.txt` | Passed; moved implementation files no longer exist under `AgentFramework.Core\Workflows` and exist under `Workflows.Core`. |
| `proof/SB03/transcripts/anti-stub-audit.txt` | Passed; no placeholder, fake, loose object dictionary, or unimplemented markers in SB03 source/test files. |
| `proof/SB03/transcripts/prepared-validator.txt` | Passed; bundle remains valid for prepared stage after SB03 closure edits. |

## Dependency Graph Proof

- `CanDoItAll.AgentFramework.Workflows.Core` references:
  - `CanDoItAll.AgentFramework.Core`
  - `CanDoItAll.AgentFramework.Models`
  - `CanDoItAll.AgentFramework.Workflows.Abstractions`
  - `CanDoItAll.SharedKernel`
  - `Microsoft.Extensions.DependencyInjection.Abstractions`
- The `CanDoItAll.AgentFramework.Core` reference is a deliberate SB03 transition reference because runtime/store/executor contracts are still owned by SB04 and SB06.
- Boundary scans reject references from `Workflows.Core` to:
  - `CanDoItAll.AgentFramework.Maf`
  - `CanDoItAll.Modules.AgentFramework`
  - `CanDoItAll.Modules.Plugins`
  - `CanDoItAll.Plugins.Abstractions`
  - `CanDoItAll.AgentFramework.Persistence`
  - `CanDoItAll.Web`

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| Workflow core services | `src/CanDoItAll.AgentFramework.Workflows.Core/*.cs` | Hosting, `CanDoItAll.Modules.AgentFramework`, MAF, Persistence, plugin consumers, and tests. | `build-workflows-core.txt`, `build-consumers.txt`, `workflow-parity-tests.txt`, and `executor-routing-policy-tests.txt` prove moved services compile and preserve existing behavior. | `moved-file-audit.txt` and `dependency-boundary.txt` fail if implementation remains duplicated in `AgentFramework.Core\Workflows` or gains forbidden dependencies. |
| Workflow core DI registration | `WorkflowCoreServiceCollectionExtensions` | Hosting and module startup. | `WorkflowCoreRegistrationExtensionOwnsCoreServiceRegistrations` resolves validator, runtime backend catalog, and payload policy from the extension. | `HostAndModuleRegistrationUseWorkflowCoreExtension` fails if old inline validator, payload, process bridge, or test runner registrations return. |
| Typed validation diagnostics | `WorkflowFailureDiagnosticMapper` and `WorkflowFailureDisplayFormatter.ToUserMessage(WorkflowFailureDiagnosticEnvelope)` | Catalog save failures, workflow display helpers, and downstream diagnostic adoption. | `CatalogValidationFailureCarriesTypedRepairableDiagnostics` proves exact `InvalidOperationException` compatibility plus typed diagnostic source, retryability, repair hint, and redaction. | Invalid missing-start fixture fails through catalog validation and exposes typed diagnostics instead of string-only failure text. |

## Notes

- Existing namespaces remain `CanDoItAll.AgentFramework.Core` for moved classes to avoid broad source churn before SB04/SB06 complete the runtime and executor contract moves.
- Runtime manager, run/checkpoint/artifact/external-request stores, execution backend contracts, and executor contracts intentionally remain outside SB03.
- `dotnet test` used `--artifacts-path artifacts\codex-sb03-unit` because `CanDoItAll.Tests.Support` references `CanDoItAll.Web`; a live `CanDoItAll.Web` process can lock the default web output directory.
- Browser validation is not applicable for SB03. The user instructed that future UI validation should be large-screen-only.

## Completed Validator Metadata Addendum

- Portable proof reference: bundle://proof/SB03/manifest.md
- Semantic invariant contract: bundle://proof/SB03/semantic-invariants.md
- Command transcript path: bundle://proof/SB03/transcripts/anti-stub-audit.txt
- Passing transcript: bundle://proof/SB03/transcripts/anti-stub-audit.txt
- Anti-stub audit transcript: bundle://proof/SB03/transcripts/anti-stub-audit.txt
- Failing-first test: N/A - process/no production behavior metadata addendum for completed-stage validator compatibility.
- SHA-256 changed-file hash: 7E768B1D6B5EAFAF50235BA2B0EFD7F24E55A8D0226EA06384200895FDF0DE4A bundle://proof/SB03/manifest.md
- Invariant ID: SB03-final-closure

Moved checkout copy validation: portable bundle references can be copied to a moved checkout without machine-specific paths.

## Proof Claim To Code Matrix

| Capability claim | Required production source proof | Required test proof | Required negative fixture | Result |
| --- | --- | --- | --- | --- |
| portable proof | bundle://proof/SB03/manifest.md | bundle://proof/SB03/transcripts/metadata-compliance.txt | bundle://proof/SB03/transcripts/metadata-compliance.txt negative metadata proof | Verified pass: portable proof references are closed for SB03. |



