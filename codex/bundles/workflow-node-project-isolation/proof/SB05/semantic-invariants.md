# SB05 Semantic Invariants

## Invariants

| Invariant | Proof |
| --- | --- |
| Workflow foundation projects have an explicit, acyclic, approved dependency graph. | `architecture-check.txt` and `FoundationProjectsUseAllowedDependencyGraph`. |
| No MAF, UI, plugin implementation, persistence implementation, or web dependency leaked into workflow foundation projects. | `FoundationProjectsRejectForbiddenDownstreamReferences` and `architecture-check.txt`. |
| Foundation helper splits preserve validation, catalog, runtime, event, hosting, policy, and API behavior. | `foundation-unit-tests.txt` and `workflow-api-integration-tests.txt`. |
| Moved oversized files no longer keep unrelated public owners in the same implementation file. | `LargeMovedImplementationFilesHaveSinglePublicOwner` and `diagnostics-and-responsibility-review.txt`. |
| Diagnostics remain typed, repairable, redacted, and attached through explicit mapper ownership. | `FoundationDiagnosticsRemainTypedRepairableAndRedacted` and `diagnostics-and-responsibility-review.txt`. |
| Foundation source does not use loose object diagnostic payloads or generic error phrases. | `FoundationCodeDoesNotUseLooseObjectDiagnosticPayloadsOrGenericErrors` and `diagnostics-and-responsibility-review.txt`. |
| No critical or moderate performance issue blocks SB06. | `performance-scan.txt` records exact recipe counts and an owner for the informational deferred findings. |
| SB05 did not introduce placeholders, fake implementations, stubs, or unimplemented paths. | `anti-stub-audit.txt`. |

## Shallow-Pass Trap

A shallow checkpoint could only run existing tests while leaving the extracted foundation as copied monoliths with unclear ownership. SB05 proof catches that shape:

- Keeping unrelated public owners in `WorkflowCatalogServices.cs`, `WorkflowDefinitionValidator.cs`, `WorkflowRuntimeManager.cs`, or `WorkflowArtifactContentStores.cs` fails `LargeMovedImplementationFilesHaveSinglePublicOwner`.
- Adding downstream MAF/module/plugin/persistence/web references fails `FoundationProjectsRejectForbiddenDownstreamReferences` and `architecture-check.txt`.
- Returning loose object diagnostic payloads or generic messages fails `FoundationCodeDoesNotUseLooseObjectDiagnosticPayloadsOrGenericErrors`.
- Dropping typed diagnostic mapper ownership or redaction calls fails `FoundationDiagnosticsRemainTypedRepairableAndRedacted`.
- Breaking existing behavior fails the 90-test foundation unit subset or the 14-test workflow API integration subset.

## Adversarial Negative Proof

`proof/SB05/transcripts/focused-hardening-tests.txt` proves negative cases through guard tests:

- Forbidden project references fail the foundation dependency tests.
- Large moved implementation files with extra public owners fail the responsibility test.
- Loose diagnostic payload patterns and generic error phrases fail the no-generic-error test.
- Missing typed diagnostic envelope, repair hint, redacted technical detail, exception data keys, or redaction calls fail the diagnostics test.

## Semantic Positive Proof

`proof/SB05/transcripts/foundation-unit-tests.txt` proves the workflow foundation still works across abstraction/builder fixtures, core extraction, runtime extraction, foundation/catalog/preview/settings behavior, MAF event normalization, hosting registration, and workflow executor policy observability.

`proof/SB05/transcripts/workflow-api-integration-tests.txt` proves the API-facing persistent workflow runtime path still works after SB05 file splits.

## Performance Proof

`proof/SB05/transcripts/performance-scan.txt` scanned critical, async, string/memory, collections/LINQ, I/O/serialization, regex, and structural recipes against the workflow foundation source. It found no critical or moderate open issues. Informational LINQ/list allocation candidates are deferred to SB14 final profiling only if runtime profiling identifies those validation or in-memory listing paths as hot.

## Anti-Stub Audit

`proof/SB05/transcripts/anti-stub-audit.txt` scans SB05-changed source and test files for placeholder/fake/stub/unimplemented markers and loose object diagnostic payloads. The audit passes with no matches.

## Residual Risk

- `WorkflowCatalogServices.cs` and `WorkflowDefinitionValidator.cs` remain large but cohesive under the SB05 file-size budget. Further splitting without measured pain would increase churn before executor extraction.
- `CanDoItAll.AgentFramework.Workflows.Core` and `CanDoItAll.AgentFramework.Workflows.Runtime` still reference `CanDoItAll.AgentFramework.Core` for SB06-owned executor approval/redaction/audit transition contracts.
- Executor abstractions, default executor categories, plugin adapters, template extraction, MAF adapter isolation, API/UI/Workbench adoption, and final browser validation remain downstream SB06-SB14 work.

## Completed Validator Semantic Contract Addendum

- Invariant ID: SB05-final-closure
- Source raw note: R01-R18 workflow-node project isolation closure evidence for SB05.
- Expected behavior: The SB05 scope remains closed by its recorded proof artifacts and downstream SB14 final regression.
- Disallowed shallow implementation: Do not replace the recorded source/test proof with summary-only closure or silent fallback behavior.
- Failing-first test: N/A - process/no production behavior metadata addendum; adversarial negative proof remains in the SB05 transcript set where applicable.
- Passing test: See bundle://proof/SB05/transcripts/ for the SB05 passing command transcript set and SB14 final regression transcripts.
- Changed source files: See bundle://proof/SB05/manifest.md and bundle://proof/SB14/changed-file-hashes.txt for the final closure hash set.
- Production assertions: Production behavior is asserted by the SB05 proof chain and SB14 final unit/component/integration/browser regression.
- Red-team negative case: SB14 no-fallback, no-generic, anti-stub, and responsibility audits guard the final state.
- Downstream dependency check: SB14 final closure revalidated downstream workflow, executor, plugin, template, MAF adapter, API, UI, Workbench, and process integration paths.
