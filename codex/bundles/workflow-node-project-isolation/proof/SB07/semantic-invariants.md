# SB07 Semantic Invariants

## Invariants

| Invariant | Source raw note | Expected behavior | Disallowed shallow implementation | Failing or negative proof | Passing proof |
| --- | --- | --- | --- | --- | --- |
| SB07-I01: default executors have category project homes. | Executor implementations are mixed together and must be split by logical category. | Control, Transforms, Workspace, Network, Documents, Media, ProjectStructure, and aggregate Standard projects exist and are in the solution. | A single new monolith project, or category folders not backed by projects. | `semantic-source-assertions.txt` and `static-ownership-check.txt` fail on missing category projects. | `standard-category-builds.txt`; `semantic-source-assertions.txt`. |
| SB07-I02: MAF and module registration delegate to category composition. | MAF must be a thin adapter and not own default executor registrations. | MAF uses singleton standard registration and module composition uses scoped standard registration. | Keeping direct concrete default executor registrations in MAF or module startup as a fallback. | `static-ownership-check.txt` fails on concrete default executor names in MAF/module registration. | `focused-category-isolation-tests.txt`; `semantic-source-assertions.txt`. |
| SB07-I03: descriptor ids remain stable while descriptor ownership is partitioned. | Preserve executor ids, labels, settings schemas, descriptors, and UI/API compatibility. | Category descriptor sources produce exactly the built-in descriptor id set. | Rebuilding descriptors with missing ids, duplicates, changed ids, or UI constants. | `CategoryDescriptorSourcesPartitionBuiltInDescriptors` fails on missing, duplicate, or extra ids. | `focused-category-isolation-tests.txt`; `executor-regression-tests.txt`. |
| SB07-I04: no MAF fallback bucket remains for moved defaults. | Do not leave default executor implementation in MAF as a fallback. | MAF `Runtime/Workflows` contains only adapter/compiler/backend workflow files. | Keeping copied concrete executors in MAF or reintroducing MAF-owned built-in descriptor helpers. | `static-ownership-check.txt` fails on moved file existence or concrete executor references. | `static-ownership-check.txt`; `semantic-source-assertions.txt`. |
| SB07-I05: large moved executors are split by responsibility. | Avoid copied monoliths during isolation. | Source Ingestion and Project Structure have helper files for input/path/candidate/task-node/support responsibilities. | Copying old oversized classes whole into new category projects. | `LargeMovedExecutorsAreSplitByResponsibility` and line-count/source assertions fail on collapsed files. | `focused-category-isolation-tests.txt`; `static-ownership-check.txt`. |
| SB07-I06: existing default executor behavior remains compatible. | Preserve deterministic preview, side-effect descriptors, failure behavior, policy limits, and plugin catalog compatibility. | Existing executor/preview/policy/hosting/plugin catalog slices pass after the move. | Category extraction that compiles but changes descriptor, preview, policy, or integration behavior. | Existing regression tests fail on behavior drift; integration proof fails if plugin catalog composition breaks. | `executor-regression-tests.txt`; `plugin-catalog-integration-tests.txt`. |
| SB07-I07: category projects are implemented code, not placeholders. | Critical proof must reject stubs and shallow moves. | Standard category source and SB07 tests contain no placeholder implementation markers. | Empty wrappers, `TODO`, `STUB`, or `NotImplementedException` used to satisfy project structure only. | `anti-stub-audit.txt` fails on placeholder markers. | `anti-stub-audit.txt`. |

## Changed Source Files And Hashes

- `bundle://proof/SB07/changed-file-hashes.txt`

## Production Assertions

- `bundle://proof/SB07/transcripts/semantic-source-assertions.txt` records `SB07-I01` through `SB07-I06` source-level assertions.
- `bundle://proof/SB07/transcripts/static-ownership-check.txt` records MAF remaining file ownership, absent moved concrete executor files, no direct default executor references, no MAF references from standard categories, and split helper file line counts.
- `bundle://proof/SB07/transcripts/focused-category-isolation-tests.txt` records the category registration, descriptor partition, dependency boundary, and split-helper guard tests.

## Red-Team Negative Cases

- Reintroducing a concrete default executor file under `AgentFramework.Maf\Runtime\Workflows` fails `static-ownership-check.txt`.
- Removing any category project from the solution fails `semantic-source-assertions.txt` and `WorkflowExecutorCategoryIsolationTests.StandardCategoryProjectsUseAllowedDependencyBoundaries`.
- Registering concrete default executors directly in MAF or module startup fails `BuiltInRegistrationDelegatesToCategoryRegistrations`.
- Dropping or duplicating a built-in descriptor id fails `CategoryDescriptorSourcesPartitionBuiltInDescriptors`.
- Collapsing Source Ingestion or Project Structure helpers back into single copied files fails `LargeMovedExecutorsAreSplitByResponsibility`.
- Shipping placeholder category code fails `anti-stub-audit.txt`.

## Downstream Dependency Check

| Downstream dependency | SB07 result | Proof |
| --- | --- | --- |
| SB08 plugin executor boundary | Ready to start; default categories no longer depend on MAF concrete executor ownership. | `standard-category-builds.txt`; `static-ownership-check.txt`. |
| SB09 executor hardening checkpoint | Ready after SB08; default category structure and split helpers are in place for hardening. | `focused-category-isolation-tests.txt`; `static-ownership-check.txt`. |
| SB10 descriptor/template validation | Descriptor ids remain stable and category descriptor sources are partitioned. | `focused-category-isolation-tests.txt`; `executor-regression-tests.txt`. |
| SB11 MAF adapter isolation | MAF no longer owns concrete default executors, leaving adapter/compiler/backend files as the remaining MAF-owned workflow surface. | `static-ownership-check.txt`. |
| SB12 UI/API adoption | UI/API descriptor display can continue to consume the descriptor catalog; browser proof remains later and large-screen-only. | `executor-regression-tests.txt`; `plugin-catalog-integration-tests.txt`. |

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| Standard default executor categories | `standard-category-builds.txt`; `semantic-source-assertions.txt` | MAF and module consumer builds in `standard-category-builds.txt` | `focused-category-isolation-tests.txt` proves category registration composition. | `static-ownership-check.txt` fails if MAF keeps concrete default executor files or references. |
| Category descriptor sources | `focused-category-isolation-tests.txt`; `semantic-source-assertions.txt` | Executor regression proof in `executor-regression-tests.txt` | DI assertions prove all seven category descriptor sources are registered. | Descriptor partition test fails on changed descriptor id set. |
| MAF/module standard registration | `semantic-source-assertions.txt` | Consumer builds and category isolation tests | MAF singleton and module scoped lifetimes are explicit. | Direct-registration static scan fails on concrete default executor registrations. |
| Split helper files | `static-ownership-check.txt`; `semantic-source-assertions.txt` | Regression tests preserve behavior after split. | Helpers separate reader/path/candidate/task-node/input/support responsibilities for SB09. | Split-helper tests fail on collapsed moved monoliths. |

## Semantic Adequacy Gate

| Gate | Result |
| --- | --- |
| Positive execution/materialization proof | Passed through category registration, descriptor partition, executor regression, and plugin catalog integration proof. |
| Adversarial failure proof | Passed through no-MAF-fallback, no-direct-registration, descriptor duplicate/missing-id, split-helper, and anti-stub guard tests. |
| Architecture proof | Passed through static ownership checks, bounded dependency category tests, and source assertion transcript. |
| Anti-stub proof | Passed through `anti-stub-audit.txt`. |
| Workbook/traceability proof | Passed through workbook verification and prepared-stage validator. |

## Residual Risk

- Plugin executor adapters and runtime package compatibility remain SB08 work.
- Combined executor/plugin diagnostic hardening remains SB09 work.
- Template loading, MAF adapter isolation, API/UI/Workbench adoption, and browser validation remain SB10-SB14.
- Browser-visible validation is deferred and should run only on large-screen viewports per user instruction.
