# SB09 Semantic Invariants

## Invariants

| Invariant | Source raw note | Expected behavior | Disallowed shallow implementation | Failing or negative proof | Passing proof |
| --- | --- | --- | --- | --- | --- |
| SB09-I01: combined executor descriptors are stable and source-aware. | SB09 must prove executor isolation is real before templates consume descriptors. | Default category, bundled plugin, runtime package, and Cognitive Memory feature-module descriptors have stable ids, no duplicates, and explicit source context. | Running only category tests or only plugin tests without a combined descriptor set. | `CombinedExecutorDescriptorsKeepStableIdsAndSourceContext` fails on duplicates or missing source kinds. | `focused-hardening-tests.txt`; `semantic-source-assertions.txt`. |
| SB09-I02: plugin invocation diagnostics remain repairable. | No plugin/module executor failure may collapse to a generic message. | Plugin execution failures carry node id, executor id, plugin id, package id, retryability, repair hint, and redacted technical detail. | Throwing raw provider exceptions or hiding plugin/package context behind generic executor text. | `PluginExecutorFailureDiagnosticsPreserveContextRepairHintRetryabilityAndRedaction` fails. | `combined-hardening-regression-tests.txt`; `no-generic-error-and-security-review.txt`. |
| SB09-I03: runtime package activation diagnostics are typed. | Package load and DI activation errors need package/type/dependency context. | `PluginWorkflowExecutorActivationException` exposes failure kind, retryability, repair hint, and redacted technical detail. | Keeping only plugin/package/type fields with no repairability or redaction. | `PluginActivationDiagnosticsExposeRetryabilityRepairHintAndRedactedTechnicalDetail` fails. | `focused-hardening-tests.txt`; `semantic-source-assertions.txt`. |
| SB09-I04: bundled plugin serializer options are not repeated per executor class. | Performance review called out repeated serializer/options helper patterns. | Gmail and Office365 workflow executors share one static serializer-options helper per plugin workflow file. | Each workflow executor class creates its own options instance. | `BundledPluginWorkflowExecutorsShareSerializerOptions` and `performance-scan.txt` fail. | `focused-hardening-tests.txt`; `performance-scan.txt`. |
| SB09-I05: MAF has no executor fallback and category files stay bounded. | MAF must be a thin adapter and moved code must not become copied monoliths. | MAF workflow folder contains only adapter/compiler/backend files; standard executor files stay within the SB09 responsibility limit. | Reintroducing MAF concrete default executor files or category-local monoliths. | `ExecutorOwnershipAuditHasNoMafFallbackOrCategoryMonolith` and static ownership transcript fail. | `static-ownership-and-responsibility-check.txt`; `combined-hardening-regression-tests.txt`. |
| SB09-I06: plugin compatibility remains intact after hardening. | Plugin consequences must not regress while diagnostics/performance are hardened. | Bundled plugin builds, plugin boundary tests, plugin policy tests, and plugin/email integration pass. | Diagnostic/performance cleanup that breaks manifests, grants, OAuth payloads, side-effect receipts, or package loading. | Combined unit or integration transcripts fail. | `executor-hardening-builds.txt`; `combined-hardening-regression-tests.txt`; `plugin-catalog-email-integration-tests.txt`. |
| SB09-I07: no generic or secret-leaking production executor/plugin source remains in checkpoint scope. | Logs and diagnostics must include actionable state and mask sensitive data. | Production executor/plugin source contains no generic failure phrases or raw test secret literals. | "plugin failed", "something went wrong", or raw token/secret literals in production source. | `no-generic-error-and-security-review.txt` fails. | `no-generic-error-and-security-review.txt`. |
| SB09-I08: checkpoint proof is artifact-backed and workbook-traced. | Critical subbundles require Semantic Adequacy Gate proof. | SB09 proof files, transcripts, workbook rows, traceability, and execution report identify SB09 as completed and SB10 as next. | Marking SB09 complete without proof or leaving stale pending workbook/docs. | Workbook verification, prepared validator, and closure audit fail. | `workbook-verification.txt`; `prepared-validator.txt`; `closure-audit.txt`. |

## Changed Source Files And Hashes

- `bundle://proof/SB09/changed-file-hashes.txt`

## Production Assertions

- `bundle://proof/SB09/transcripts/semantic-source-assertions.txt` records `SB09-I01` through `SB09-I08` source-level assertions.
- `bundle://proof/SB09/transcripts/static-ownership-and-responsibility-check.txt` records no MAF fallback, no moved default executor files in MAF, category file-size limits, and plugin-boundary dependency limits.
- `bundle://proof/SB09/transcripts/performance-scan.txt` records serializer-options cleanup and deterministic runtime package discovery.

## Red-Team Negative Cases

- Reintroducing a default executor file under `AgentFramework.Maf\Runtime\Workflows` fails the ownership audit.
- Duplicating descriptor ids across default, plugin, runtime package, and Cognitive Memory descriptors fails the combined descriptor test.
- Removing plugin/package source context from invocation diagnostics fails the plugin invocation diagnostic test.
- Returning runtime package activation errors without retryability, repair hint, or redacted technical detail fails the activation diagnostic test.
- Reintroducing per-class Gmail/Office365 serializer options fails the performance test and transcript.
- Adding generic failure text or raw test secrets to production executor/plugin source fails the no-generic-error/security review.

## Downstream Dependency Check

| Downstream dependency | SB09 result | Proof |
| --- | --- | --- |
| SB10 template loading | Ready to start; descriptor ids/source metadata are stable across default, plugin, runtime package, and feature-module sources. | `focused-hardening-tests.txt`; `semantic-source-assertions.txt`. |
| SB11 MAF adapter isolation | Ready after SB10; MAF has no concrete default executor fallback and plugin executor boundary is independent. | `static-ownership-and-responsibility-check.txt`. |
| SB12 API/UI/Workbench adoption | Ready after SB11; executor/plugin diagnostics carry repairable, redacted context for UI/API rendering. | `combined-hardening-regression-tests.txt`; `no-generic-error-and-security-review.txt`. |
| SB13 adoption hardening | Remaining hardening concerns are adoption/UI/API/Workbench display and no-fallback proof. | `manifest.md`; workbook Validation Matrix. |
| SB14 final closure | Workbook and traceability are current through SB09. | `workbook-verification.txt`; `prepared-validator.txt`. |

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| Combined descriptor set | Focused hardening tests and semantic assertions | SB10 template loading and SB12 display adoption | Descriptor ids and source contexts are proven before consumption. | Duplicate/missing descriptor source tests fail. |
| Plugin activation diagnostic fields | Source assertions and focused tests | Runtime package loading and future UI/API display | Activation failures include context, retryability, repair hint, and redacted technical detail. | Redaction/repairability test fails on missing fields or leaked secrets. |
| Plugin invocation diagnostics | Combined hardening regression tests | Executor invoker/audit paths and future display | Invocation failures keep node/executor/plugin/package context. | No-generic and redaction tests fail on unsafe output. |
| Executor category responsibility | Static ownership transcript | MAF adapter and template materialization | MAF stays adapter-only and category files stay bounded. | Static scan fails on fallback files or monoliths. |
| Performance cleanup | Performance transcript | Gmail/Office365 executor payload generation | Shared static options avoid repeated options setup in workflow executor files. | Performance scan fails on repeated options. |

## Semantic Adequacy Gate

| Gate | Result |
| --- | --- |
| Positive execution/materialization proof | Passed through builds, focused hardening tests, combined executor/plugin regression tests, and plugin/email integration proof. |
| Adversarial failure proof | Passed through plugin activation redaction/repairability tests, plugin invocation redaction/source-context tests, no-MAF-fallback static checks, and no-generic-error scans. |
| Architecture proof | Passed through static ownership/dependency checks and combined descriptor source proof. |
| Anti-stub proof | Passed through `anti-stub-audit.txt`. |
| Workbook/traceability proof | Passed through workbook verification; prepared-stage validator is captured after closure docs are updated. |

## Residual Risk

- Template loader/materializer extraction remains SB10.
- MAF compiler/backend adapter isolation remains SB11.
- UI/API/Workbench diagnostic rendering and large-screen browser validation remain SB12/SB13.
- Final regression and cleanup remain SB14.

## Completed Validator Semantic Contract Addendum

- Invariant ID: SB09-final-closure
- Source raw note: R01-R18 workflow-node project isolation closure evidence for SB09.
- Expected behavior: The SB09 scope remains closed by its recorded proof artifacts and downstream SB14 final regression.
- Disallowed shallow implementation: Do not replace the recorded source/test proof with summary-only closure or silent fallback behavior.
- Failing-first test: N/A - process/no production behavior metadata addendum; adversarial negative proof remains in the SB09 transcript set where applicable.
- Passing test: See bundle://proof/SB09/transcripts/ for the SB09 passing command transcript set and SB14 final regression transcripts.
- Changed source files: See bundle://proof/SB09/manifest.md and bundle://proof/SB14/changed-file-hashes.txt for the final closure hash set.
- Production assertions: Production behavior is asserted by the SB09 proof chain and SB14 final unit/component/integration/browser regression.
- Red-team negative case: SB14 no-fallback, no-generic, anti-stub, and responsibility audits guard the final state.
- Downstream dependency check: SB14 final closure revalidated downstream workflow, executor, plugin, template, MAF adapter, API, UI, Workbench, and process integration paths.
