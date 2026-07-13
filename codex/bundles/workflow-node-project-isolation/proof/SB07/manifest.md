# SB07 Proof Manifest

## Scope

Implemented `SB07 - Default Executor Category Projects`.

## Owned Requirements And Raw Notes

- Requirements: R08, R13, R14, R15, R17, and R18.
- Raw note coverage: executor abstraction/category split, base-up execution order, exception/error-state diagnostics, and avoiding copied monoliths during isolation.
- Semantic invariant contract: `bundle://proof/SB07/semantic-invariants.md`.

## Source Changes

- Added seven default executor category projects: Control, Transforms, Workspace, Network, Documents, Media, and ProjectStructure.
- Added `CanDoItAll.AgentFramework.WorkflowExecutors.Standard` as a small aggregate registration project for category composition.
- Moved default executor implementations out of `CanDoItAll.AgentFramework.Maf\Runtime\Workflows`.
- Moved built-in descriptor compatibility ownership and `WorkflowInputPayloadText` into executor core.
- Replaced MAF built-in registration with `AddStandardWorkflowExecutors(ServiceLifetime.Singleton)`.
- Replaced `CanDoItAll.Modules.AgentFramework` direct default executor registrations with `AddStandardWorkflowExecutors(ServiceLifetime.Scoped)`.
- Split `SourceIngestionWorkflowExecutor` into reader, path, candidate, and model helpers.
- Split `ProjectStructureWorkflowExecutor` into task-node, input-resolution, and support helpers.
- Updated unit tests, integration references, architecture docs, traceability, workbook rows, and execution-report status for SB07.

## Build And Test Transcripts

| Artifact | Result |
| --- | --- |
| `bundle://proof/SB07/transcripts/standard-category-builds.txt` | Passed; standard aggregate, MAF, Hosting, and AgentFramework module built with 0 warnings and 0 errors. |
| `bundle://proof/SB07/transcripts/focused-category-isolation-tests.txt` | Passed; no-dependencies unit build and `WorkflowExecutorCategoryIsolationTests` ran 5 tests with 0 failures. |
| `bundle://proof/SB07/transcripts/executor-regression-tests.txt` | Passed; executor, policy observability, foundation, hosting, and preview regression slice ran 61 tests with 0 failures. |
| `bundle://proof/SB07/transcripts/plugin-catalog-integration-tests.txt` | Passed; plugin catalog integration slice ran 29 tests with 0 failures from an alternate output directory. |
| `bundle://proof/SB07/transcripts/static-ownership-check.txt` | Passed; MAF contains only adapter/compiler/backend workflow files and no concrete default executor references. |
| `bundle://proof/SB07/transcripts/semantic-source-assertions.txt` | Passed; SB07-I01 through SB07-I06 source assertions tie semantic invariants to project, registration, descriptor, fallback, split-helper, and regression proof. |
| `bundle://proof/SB07/transcripts/anti-stub-audit.txt` | Passed; no `TODO`, `STUB`, `NotImplementedException`, or placeholder invalid-operation markers in standard executor source/tests. |
| `bundle://proof/SB07/transcripts/workbook-verification.txt` | Passed; workbook contains SB07 category rows and no stale MAF concrete-executor paths. |
| `bundle://proof/SB07/transcripts/dependency-output-caveat.txt` | Informational; existing Web app processes on `localhost:5032` lock default output, so integration proof used an alternate output path. |
| `bundle://proof/SB07/transcripts/prepared-validator.txt` | Passed; bundle remains valid for prepared stage after SB07 closure edits. |
| `bundle://proof/SB07/transcripts/closure-audit.txt` | Passed; required SB07 proof files exist, transcripts carry `EXIT_CODE: 0`, and stale moved-file references are absent. |

## Changed File Hashes

- `bundle://proof/SB07/changed-file-hashes.txt`

## Source Assertion Evidence

- `SB07-I01`: category project homes exist and are listed in `CanDoItAll.slnx`.
- `SB07-I02`: MAF and module registration delegate to the standard aggregate with explicit lifetime.
- `SB07-I03`: category descriptor sources partition the stable built-in descriptor set.
- `SB07-I04`: no MAF fallback bucket remains for moved default executors.
- `SB07-I05`: Source Ingestion and Project Structure moved executors are split by responsibility.
- `SB07-I06`: existing behavior regression slices remain tied to the moved executor surface.

Evidence path: `bundle://proof/SB07/transcripts/semantic-source-assertions.txt`.

## Deferred Finding Table

| Finding | Severity | Owner | Rationale |
| --- | --- | --- | --- |
| Plugin executor package adapters remain under existing plugin/module ownership. | Expected transition | SB08 | SB07 only moved built-in default executor categories. Plugin descriptor/source/grant/package behavior is the next subbundle. |
| Combined executor/plugin diagnostic hardening remains open. | Expected transition | SB09 | SB07 preserved current default executor behavior and structure; SB09 is the forced hardening gate for default and plugin executor diagnostics. |
| Default Web output path is locked by running app processes. | Validation caveat | Current environment | Integration proof used `artifacts\sb07-integration-output\` without stopping user-owned Web processes. |

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| Standard default executor categories | `standard-category-builds.txt`; `semantic-source-assertions.txt` | MAF and module consumer builds in `standard-category-builds.txt` | `focused-category-isolation-tests.txt` proves category registrations compose built-in executors and descriptor sources. | `static-ownership-check.txt` fails if MAF retains concrete default executor files or references. |
| Category descriptor sources | `focused-category-isolation-tests.txt`; `semantic-source-assertions.txt` | Executor catalog and regression slices in `executor-regression-tests.txt` | Descriptor source registration assertions prove all seven categories are available through DI. | Descriptor partition test fails on missing, duplicate, or extra built-in descriptor ids. |
| MAF/module standard registration | `semantic-source-assertions.txt` | `standard-category-builds.txt`; `focused-category-isolation-tests.txt` | MAF uses singleton standard registration and module uses scoped standard registration. | Static no-match check fails if direct concrete default registrations reappear in MAF/module startup. |
| Source Ingestion and Project Structure helper splits | `static-ownership-check.txt`; `semantic-source-assertions.txt` | `executor-regression-tests.txt` preserves executor behavior after the split. | Split helper files separate input/path/candidate/task-node/support responsibilities before SB09 hardening. | `WorkflowExecutorCategoryIsolationTests.LargeMovedExecutorsAreSplitByResponsibility` fails if the moved executors collapse back into copied monoliths. |
| Workbook and bundle status | `workbook-verification.txt`; `prepared-validator.txt` | Execution report and traceability cite SB07 as completed and SB08 as next. | Prepared-stage validator proves bundle shape remains valid after execution-time repair. | Workbook verification fails on missing SB07 category rows or stale MAF concrete-executor paths. |

## Notes

- Browser validation is not applicable for SB07. UI validation remains deferred to SB12, SB13, and SB14 and is large-screen-only per user instruction.
- SB07 intentionally did not migrate plugins, templates, MAF compiler/backend adapter isolation, API/UI, or Workbench adoption.
- Existing namespaces were changed only where required by project ownership; behavior compatibility is carried by the focused regression slices.

