# SB06 Semantic Invariants

## Invariants

| Invariant | Proof |
| --- | --- |
| Executor contracts and audit contracts have executor-owned project homes. | `executor-static-ownership-check.txt` and `WorkflowExecutorContractsMovedOutOfAgentFrameworkCore`. |
| Executor abstraction/core projects avoid MAF, UI, plugin implementation, module implementation, and Web dependencies. | `executor-static-ownership-check.txt` and `ExecutorProjectsHaveBoundedDependencies`. |
| Built-in and Cognitive Memory descriptor shapes remain compatible while construction moves to executor-owned helpers. | `DescriptorFactoryCreatesStableImplementedAndPlannedDescriptors` and `BuiltInAndCognitiveMemoryDescriptorsUseExecutorOwnedFactory`. |
| Executor ids remain stable for current built-in and Cognitive Memory descriptor examples. | `focused-executor-foundation-tests.txt` descriptor parity assertions. |
| Missing executors fail explicitly with typed diagnostics instead of fallback invocation. | `MissingExecutorProducesTypedDiagnosticWithSourceContext`. |
| Invocation failures produce redacted, retryable or non-retryable, repairable diagnostics with node/executor/source context. | `ExecutorFailureDiagnosticsAreRedactedRetryableAndRepairable`. |
| Approval denial is explicit and contains actionable policy context. | `ApprovalDeniedProducesExplicitDiagnostic`. |
| Shared executor helpers are implemented code, not placeholders. | `anti-stub-audit.txt`. |
| Feature modules and plugins can consume executor contracts without depending on MAF-owned executor contracts. | `FeatureModulesAndPluginsReferenceExecutorAbstractionsDirectly`, `plugin-and-module-validation.txt`, and `plugin-catalog-integration-tests.txt`. |

## Shallow-Pass Trap

A shallow SB06 pass could compile by leaving MAF/Core as the real executor owner and adding wrapper projects with duplicate or unused types. SB06 proof catches that shape:

- Leaving `WorkflowExecutorContracts.cs`, `WorkflowExecutorObservability.cs`, or `WorkflowExecutorJson.cs` in the old owners fails `executor-static-ownership-check.txt`.
- Adding MAF, UI, Web, module implementation, or plugin implementation references to executor-owned projects fails `ExecutorProjectsHaveBoundedDependencies`.
- Keeping descriptor schema reflection/serialization in MAF fails the static ownership transcript.
- Passing builds while losing descriptor/id compatibility fails the built-in and Cognitive Memory descriptor parity tests.
- Converting missing executors, approval denial, or invocation exceptions to generic messages fails the focused diagnostic tests.

## Adversarial Negative Proof

`proof/SB06/transcripts/focused-executor-foundation-tests.txt` proves negative cases through guard tests:

- A missing executor id produces typed diagnostic context and does not use a fallback path.
- An executor throwing with sensitive data is mapped to a redacted diagnostic.
- Approval denial produces explicit non-generic diagnostic state.
- Plugin/module projects missing executor abstraction references fail the project-reference assertions.

`proof/SB06/transcripts/executor-static-ownership-check.txt` proves negative ownership cases:

- Old executor-owned files must be absent.
- Executor projects must not reference MAF, Web, component UI, module implementation, or plugin implementation projects.
- MAF descriptor source must not contain the old reflection/schema serialization helper ownership markers.

## Semantic Positive Proof

`proof/SB06/transcripts/executor-builds.txt` proves executor-owned abstractions/core and key consumers compile together.

`proof/SB06/transcripts/focused-executor-foundation-tests.txt` proves positive descriptor materialization, schema output, built-in descriptor examples, Cognitive Memory descriptor examples, DI registration, and feature-module/plugin consumption.

`proof/SB06/transcripts/plugin-catalog-integration-tests.txt` proves existing plugin catalog integration behavior still works after executor contracts move.

## Semantic Adequacy Gate

| Gate | Result |
| --- | --- |
| Positive execution/materialization proof | Passed through descriptor factory, built-in descriptor, Cognitive Memory descriptor, DI registration, plugin catalog, and plugin build proof. |
| Adversarial failure proof | Passed through missing executor, approval denied, and redacted invocation failure tests. |
| Architecture proof | Passed through static ownership and bounded dependency checks. |
| Anti-stub proof | Passed through `anti-stub-audit.txt`. |
| Workbook/traceability proof | Passed through workbook verification and prepared validator. |

## Residual Risk

- Default executor implementations still live under MAF until SB07 moves them into category projects.
- Plugin adapters and package loading isolation remain SB08 work.
- The unit project dependency-building path was not rerun because an existing `CanDoItAll.Web` process locked Web output DLLs. Focused SB06 unit validation used `--no-dependencies` and `--no-build`, and integration/plugin proofs were rerun successfully.
- Browser-visible executor display validation remains deferred to SB12 and should be large-screen-only per user instruction.

## Completed Validator Semantic Contract Addendum

- Invariant ID: SB06-final-closure
- Source raw note: R01-R18 workflow-node project isolation closure evidence for SB06.
- Expected behavior: The SB06 scope remains closed by its recorded proof artifacts and downstream SB14 final regression.
- Disallowed shallow implementation: Do not replace the recorded source/test proof with summary-only closure or silent fallback behavior.
- Failing-first test: N/A - process/no production behavior metadata addendum; adversarial negative proof remains in the SB06 transcript set where applicable.
- Passing test: See bundle://proof/SB06/transcripts/ for the SB06 passing command transcript set and SB14 final regression transcripts.
- Changed source files: See bundle://proof/SB06/manifest.md and bundle://proof/SB14/changed-file-hashes.txt for the final closure hash set.
- Production assertions: Production behavior is asserted by the SB06 proof chain and SB14 final unit/component/integration/browser regression.
- Red-team negative case: SB14 no-fallback, no-generic, anti-stub, and responsibility audits guard the final state.
- Downstream dependency check: SB14 final closure revalidated downstream workflow, executor, plugin, template, MAF adapter, API, UI, Workbench, and process integration paths.
