# SB08 Semantic Invariants

## Invariants

| Invariant | Source raw note | Expected behavior | Disallowed shallow implementation | Failing or negative proof | Passing proof |
| --- | --- | --- | --- | --- | --- |
| SB08-I01: plugin executors have a boundary project home. | Plugins are a major executor source and must be analyzed deeply. | `CanDoItAll.AgentFramework.WorkflowExecutors.Plugins` exists and is listed in the solution. | Keeping plugin executor descriptor/runtime adapter logic inside the plugin module or MAF as the only owner. | `semantic-source-assertions.txt` and `static-ownership-check.txt` fail on missing project or solution entry. | `plugin-boundary-builds.txt`; `semantic-source-assertions.txt`. |
| SB08-I02: plugin descriptor projection is boundary-owned. | Preserve plugin manifests, source/trust metadata, grants, and catalog compatibility. | The boundary owns `PluginWorkflowExecutorDescriptorSource`; the plugin module composes it through `AddPluginWorkflowExecutorBoundary()`. | Leaving a copied module descriptor source as a hidden fallback. | Static ownership and module composition tests fail if the old module-owned descriptor source returns. | `focused-plugin-boundary-tests.txt`; `static-ownership-check.txt`. |
| SB08-I03: runtime package executor registration is boundary-owned. | Runtime package executors must still load, register, report metadata, and execute through the catalog. | Package scanning delegates executor registration and wrapper creation to `PluginWorkflowExecutorRuntimeRegistration`. | Keeping nested runtime package wrappers in package services or bypassing the executor catalog. | `RuntimePackageRegistrationWrapsExecutorWithPackageSourceMetadata` and static ownership checks fail. | `focused-plugin-boundary-tests.txt`; `plugin-catalog-email-integration-tests.txt`. |
| SB08-I04: grant availability remains strongly typed. | Grants, trust, approvals, and policy state must not be bypassed. | Boundary code depends on `IPluginWorkflowExecutorGrantEvaluator`; the plugin module implements it with existing grant evaluation. | Replacing grants with booleans, strings, or permissive defaults. | Descriptor projection tests fail on grant availability state; source assertions fail on missing bridge. | `focused-plugin-boundary-tests.txt`; `semantic-source-assertions.txt`. |
| SB08-I05: activation failures carry plugin/package/type context. | Plugin load, package dependency, DI activation, provider, grant, OAuth, host-tool, and execution failures need typed diagnostics. | Runtime package activation failures throw `PluginWorkflowExecutorActivationException` with plugin id, package id, executor type name, operation, retryability, and repair hint context. | Collapsing activation problems into generic invalid-operation errors or silently falling back. | `RuntimePackageActivationFailureIncludesPluginPackageAndTypeContext` fails. | `focused-plugin-boundary-tests.txt`; `semantic-source-assertions.txt`. |
| SB08-I06: bundled plugin behavior remains compatible. | Docker, Gmail, Office365, Email, side effects, deterministic preview, OAuth/secrets masking, and receipts must survive isolation. | Bundled plugin projects build and existing plugin regression/integration slices pass. | Compiling the boundary while breaking package discovery, catalog, email/OAuth, policy, or receipt behavior. | Build, regression, or integration transcripts fail. | `plugin-boundary-builds.txt`; `plugin-regression-tests.txt`; `plugin-catalog-email-integration-tests.txt`. |
| SB08-I07: the boundary has no MAF or plugin-module fallback dependency. | MAF and modules must not keep hidden adapter fallback paths. | Plugin boundary references executor/model/plugin abstractions only and does not depend on MAF, Modules.Plugins, Web, EF, Infrastructure, or Persistence. | A boundary project that compiles by referencing the old module or MAF implementation. | `static-ownership-check.txt` fails on disallowed references. | `static-ownership-check.txt`; `PluginExecutorBoundaryProjectHasBoundedDependencies`. |
| SB08-I08: plugin boundary code is implemented, not placeholders. | Critical proof must reject stubs and shallow wrappers. | Boundary source and SB08 tests contain no placeholder implementation markers. | Empty wrappers, `TODO`, `STUB`, or `NotImplementedException` used to satisfy project structure only. | `anti-stub-audit.txt` fails on placeholder markers. | `anti-stub-audit.txt`. |

## Changed Source Files And Hashes

- `bundle://proof/SB08/changed-file-hashes.txt`

## Production Assertions

- `bundle://proof/SB08/transcripts/semantic-source-assertions.txt` records `SB08-I01` through `SB08-I08` source-level assertions.
- `bundle://proof/SB08/transcripts/static-ownership-check.txt` records absent module-owned descriptor fallback, bounded plugin boundary dependencies, module boundary composition, and package registration delegation.
- `bundle://proof/SB08/transcripts/focused-plugin-boundary-tests.txt` records the boundary dependency, descriptor projection, runtime package adapter, diagnostic, and module composition tests.

## Red-Team Negative Cases

- Reintroducing `CanDoItAll.Modules.Plugins\Catalog\PluginWorkflowExecutorDescriptorSource.cs` fails `static-ownership-check.txt`.
- Adding a MAF, module, Web, EF, Infrastructure, or Persistence dependency to `WorkflowExecutors.Plugins` fails `static-ownership-check.txt` and the focused dependency test.
- Removing `AddPluginWorkflowExecutorBoundary()` from plugin module registration fails module composition proof.
- Bypassing `IPluginWorkflowExecutorGrantEvaluator` fails descriptor projection and source assertions.
- Replacing `PluginWorkflowExecutorActivationException` with generic activation failures fails the focused negative diagnostic test.
- Shipping placeholder boundary code fails `anti-stub-audit.txt`.

## Downstream Dependency Check

| Downstream dependency | SB08 result | Proof |
| --- | --- | --- |
| SB09 executor refactoring hardening checkpoint | Ready to start; default categories and plugin executor boundary both have proof. | `plugin-boundary-builds.txt`; `focused-plugin-boundary-tests.txt`; `static-ownership-check.txt`. |
| SB10 descriptor/template validation | Plugin descriptor source and runtime package descriptors remain catalog-compatible. | `focused-plugin-boundary-tests.txt`; `plugin-regression-tests.txt`; `plugin-catalog-email-integration-tests.txt`. |
| SB11 MAF adapter isolation | Plugin executor compatibility no longer depends on MAF-owned descriptor helpers. | `static-ownership-check.txt`. |
| SB12 UI/API adoption | Plugin source/trust/grant data remains available to the descriptor catalog; browser proof remains later and large-screen-only. | `plugin-catalog-email-integration-tests.txt`; `workbook-verification.txt`. |
| SB13/SB14 adoption hardening and closure | Plugin diagnostic and display residuals are explicitly carried forward. | `manifest.md`; workbook Plugin Consequences rows. |

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| Boundary project | Build and semantic source assertion transcripts | Plugin module build and focused tests | Solution entry plus module registration composes the boundary. | Static dependency scan fails on forbidden fallback references. |
| Descriptor source | Focused descriptor projection test | Catalog regression and integration tests | Source/trust and grant projection flow through executor abstractions. | Module fallback and grant bypass checks fail. |
| Runtime package wrapper | Runtime registration focused test | Plugin catalog/email integration proof | Package scanner delegates runtime executor adapter creation to the boundary. | Activation failure test fails on missing context. |
| Bundled plugins | Docker/Gmail/Office365/Email builds | Plugin regression and integration slices | Existing manifest/OAuth/side-effect/preview compatibility remains intact. | Regression/integration tests fail on behavior drift. |

## Semantic Adequacy Gate

| Gate | Result |
| --- | --- |
| Positive execution/materialization proof | Passed through boundary/module/bundled-plugin builds, focused boundary tests, regression tests, and plugin catalog/email integration proof. |
| Adversarial failure proof | Passed through missing package metadata and DI activation diagnostic tests plus no-fallback static checks. |
| Architecture proof | Passed through bounded dependency tests, static ownership checks, and source assertion transcript. |
| Anti-stub proof | Passed through `anti-stub-audit.txt`. |
| Workbook/traceability proof | Passed through workbook verification; prepared-stage validator is captured after closure docs are updated. |

## Residual Risk

- Combined executor/plugin diagnostic classification, retryability consistency, repair hints, and redaction hardening remain SB09 work.
- Browser-visible plugin executor display and API/UI adoption remain SB12 work and should only use large-screen browser validation per user instruction.
- Template loading, MAF adapter isolation, Workbench adoption, and final closure remain SB10-SB14.

## Completed Validator Semantic Contract Addendum

- Invariant ID: SB08-final-closure
- Source raw note: R01-R18 workflow-node project isolation closure evidence for SB08.
- Expected behavior: The SB08 scope remains closed by its recorded proof artifacts and downstream SB14 final regression.
- Disallowed shallow implementation: Do not replace the recorded source/test proof with summary-only closure or silent fallback behavior.
- Failing-first test: N/A - process/no production behavior metadata addendum; adversarial negative proof remains in the SB08 transcript set where applicable.
- Passing test: See bundle://proof/SB08/transcripts/ for the SB08 passing command transcript set and SB14 final regression transcripts.
- Changed source files: See bundle://proof/SB08/manifest.md and bundle://proof/SB14/changed-file-hashes.txt for the final closure hash set.
- Production assertions: Production behavior is asserted by the SB08 proof chain and SB14 final unit/component/integration/browser regression.
- Red-team negative case: SB14 no-fallback, no-generic, anti-stub, and responsibility audits guard the final state.
- Downstream dependency check: SB14 final closure revalidated downstream workflow, executor, plugin, template, MAF adapter, API, UI, Workbench, and process integration paths.
