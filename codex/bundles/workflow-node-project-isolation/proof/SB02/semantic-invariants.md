# SB02 Semantic Invariants

## Invariants

| Invariant | Proof |
| --- | --- |
| Serialized workflow compatibility is preserved. | `WorkflowBuilderPreservesSerializedWorkflowFields` serializes and deserializes a workflow definition, preserving start node id, executor id, input parameter JSON path, `inputParameters`, and `runtimePolicy`. |
| Diagnostic payload compatibility is typed and repairable. | `WorkflowFailureDiagnosticEnvelopeSerializesRepairableContext` proves typed failure kind, retryability, source context, node id, executor id, repair hint, redacted technical detail, and correlation id round-trip through JSON. |
| Builder output is deterministic for representative workflow construction. | `WorkflowDefinitionBuilderCreatesDeterministicLinearLlmWorkflow` asserts node order, start id, component id, and deterministic edge ids. |
| Builders cover branching and port construction. | `WorkflowFixtureFactoryCreatesBranchingExecutorWorkflowWithPorts` asserts input/output ports, required flags, predicate routing, and default routing. |
| Invalid graph inputs are not silently normalized. | `WorkflowDefinitionBuilderRejectsMissingStartWhenBuildingValidFixture` proves `Build()` rejects missing start nodes; invalid fixtures require explicit `BuildUnchecked()`. |
| Incomplete executor nodes are not silently normalized. | `WorkflowNodeBuilderRejectsExecutorNodeWithoutExplicitExecutorContract` proves executor nodes require executor ids and whitespace executor settings are rejected. |
| Workflow abstractions and builders have no forbidden implementation dependencies. | `WorkflowAbstractionAndBuilderProjectsDoNotReferenceForbiddenImplementationProjects` and `dependency-boundary.txt` prove no MAF, UI, plugin, persistence, or web project reference exists. |
| SB02 did not introduce placeholders or loose failure dictionaries. | `anti-stub-audit.txt` reports no stub/fake/unimplemented markers and no loose dictionary/object payload patterns in SB02 source/test files. |

## Shallow-Pass Trap

A shallow implementation could add empty projects and helper methods that compile but still fail SB02. The focused tests and boundary scan catch that shape:

- Empty builder output would fail deterministic node and edge assertions.
- Missing branching/port support would fail `WorkflowFixtureFactoryCreatesBranchingExecutorWorkflowWithPorts`.
- Loose diagnostic strings or dictionaries would fail the diagnostic serialization assertions and anti-stub audit.
- Hidden defaulting would fail the missing-start and missing-executor negative tests.
- Leaky project references would fail both the unit boundary test and standalone dependency scan.

## Adversarial Negative Proof

`proof/SB02/transcripts/adversarial-negative-tests.txt` runs:

- `WorkflowDefinitionBuilderRejectsMissingStartWhenBuildingValidFixture`
- `WorkflowNodeBuilderRejectsExecutorNodeWithoutExplicitExecutorContract`

Both tests pass by proving the production builders throw explicit exceptions for invalid graph and executor inputs. Invalid fixture creation remains possible only through the deliberately named `BuildUnchecked()` path used for validator tests.

## Semantic Positive Proof

`proof/SB02/transcripts/semantic-positive-tests.txt` runs:

- `WorkflowDefinitionBuilderCreatesDeterministicLinearLlmWorkflow`
- `WorkflowFixtureFactoryCreatesBranchingExecutorWorkflowWithPorts`
- `WorkflowBuilderPreservesSerializedWorkflowFields`
- `WorkflowFailureDiagnosticEnvelopeSerializesRepairableContext`

These tests prove normal, branching, serialization, and diagnostic payload behavior against the actual production builders/contracts.

## Anti-Stub Audit

`proof/SB02/transcripts/anti-stub-audit.txt` scans the new workflow projects and focused test file for placeholder implementation markers, fake/stub markers, loose dictionaries, and unimplemented exceptions. The audit passes with no matches.

## Residual Risk

- SB02 intentionally does not move runtime manager, validators, catalog services, stores, template loader, MAF adapter, or executor contracts. Those remain in downstream subbundles.
- Existing workflow model contracts remain in `CanDoItAll.AgentFramework.Models`; downstream moves must add explicit compatibility proof before changing that decision.

## Completed Validator Semantic Contract Addendum

- Invariant ID: SB02-final-closure
- Source raw note: R01-R18 workflow-node project isolation closure evidence for SB02.
- Expected behavior: The SB02 scope remains closed by its recorded proof artifacts and downstream SB14 final regression.
- Disallowed shallow implementation: Do not replace the recorded source/test proof with summary-only closure or silent fallback behavior.
- Failing-first test: N/A - process/no production behavior metadata addendum; adversarial negative proof remains in the SB02 transcript set where applicable.
- Passing test: See bundle://proof/SB02/transcripts/ for the SB02 passing command transcript set and SB14 final regression transcripts.
- Changed source files: See bundle://proof/SB02/manifest.md and bundle://proof/SB14/changed-file-hashes.txt for the final closure hash set.
- Production assertions: Production behavior is asserted by the SB02 proof chain and SB14 final unit/component/integration/browser regression.
- Red-team negative case: SB14 no-fallback, no-generic, anti-stub, and responsibility audits guard the final state.
- Downstream dependency check: SB14 final closure revalidated downstream workflow, executor, plugin, template, MAF adapter, API, UI, Workbench, and process integration paths.
