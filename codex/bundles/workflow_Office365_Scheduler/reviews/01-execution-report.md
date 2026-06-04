# Execution Report

Codex must update this file after each subbundle.

## Status

- Status: `Completed with SB09 repair`

| Subbundle | Status | Commit(s) | Proof | Notes |
| --- | --- | --- | --- | --- |
| SB01 | Completed | Working tree | `bundle://proof/SB01/manifest.md`; `bundle://proof/SB01/semantic-invariants.md` | Restore/build/unit/component/integration baselines passed after clearing a local web process file lock; missing Office365/Scheduler capabilities captured as red proof. |
| SB02 | Completed | Working tree | `bundle://proof/SB02/manifest.md`; `bundle://proof/SB02/semantic-invariants.md` | Office365 address/unprocessed executor, no-message success route, and add-only processed-category mutation passed fake Graph proof. |
| SB03 | Completed | Working tree | `bundle://proof/SB03/manifest.md`; `bundle://proof/SB03/semantic-invariants.md` | Office365 email-watch summary/task templates load from the manifest, branch no-message to no-op, and enforce write-before-mark ordering. |
| SB04 | Completed | Working tree | `bundle://proof/SB04/manifest.md`; `bundle://proof/SB04/semantic-invariants.md` | Typed workflow input descriptors, template metadata parsing, saved-definition preservation, Scheduler schema validation, defaults, and raw JSON fallback passed proof. |
| SB05 | Completed | Working tree | `bundle://proof/SB05/manifest.md`; `bundle://proof/SB05/semantic-invariants.md` | Scheduler typed workflow input UX, CRM/manual email path, project/node selectors, raw JSON sync, validation, and browser proof passed. |
| SB06 | Completed | Working tree | `bundle://proof/SB06/manifest.md`; `bundle://proof/SB06/semantic-invariants.md` | Scheduler NoMessages terminal success, Office365 project-write idempotency keys, retry replay, and concurrent duplicate prevention passed proof. |
| SB07 | Completed | Working tree | `bundle://proof/SB07/manifest.md`; `bundle://proof/SB07/semantic-invariants.md` | Scheduler route/status observability, retry categories, no-message LastError preservation, waiting-for-approval non-retry, and Office365 external-write approval policy passed proof. |
| SB08 | Completed | Working tree | `bundle://proof/SB08/manifest.md`; `bundle://proof/SB08/semantic-invariants.json` | Final restore/build/test matrix, fake Graph proof, scenario harness, browser proof, raw-note audit, and completed-stage validation passed. |
| SB09 | Completed | Working tree | `bundle://proof/SB09/manifest.md`; `bundle://proof/SB09/semantic-invariants.md` | Repaired missed-approval email workflows by making only Office365/Gmail processed-marker executors unattended under an explicit idempotent marker capability. |

## Subbundle Gate Results

| Subbundle | Entry gate | Closure gate | Downstream dependencies checked | Progression result | Notes |
| --- | --- | --- | --- | --- | --- |
| SB01 | Passed | Passed | Passed | Passed to SB02 | Closure proof is in `bundle://proof/SB01/manifest.md`; targeted workflow/template/Scheduler baseline passed and missing capabilities are red before implementation. |
| SB02 | Passed | Passed | Passed | Passed to SB03 | Closure proof is in `bundle://proof/SB02/manifest.md`; build and 20 targeted integration tests passed. |
| SB03 | Passed | Passed | Passed | Passed to SB04 | Closure proof is in `bundle://proof/SB03/manifest.md`; build, loader/graph unit tests, and Office365 input path integration tests passed. |
| SB04 | Passed | Passed | Passed | Passed to SB05 | Closure proof is in `bundle://proof/SB04/manifest.md`; build, unit tests, and Scheduler integration tests passed. |
| SB05 | Passed | Passed | Passed | Passed to SB06 | Closure proof is in `bundle://proof/SB05/manifest.md`; build, focused component tests, source assertions, anti-stub audit, hashes, and Scheduler browser proof passed. |
| SB06 | Passed | Passed | Passed | Passed to SB07 | Closure proof is in `bundle://proof/SB06/manifest.md`; build, focused unit tests, and focused integration tests passed. |
| SB07 | Passed | Passed | Passed | Passed to SB08 | Closure proof is in `bundle://proof/SB07/manifest.md`; focused integration/component tests, web build, EF check, source assertions, anti-stub audit, hashes, and Scheduler browser proof passed. |
| SB08 | Passed | Passed | Passed | Completed | Final closure proof is in `bundle://proof/SB08/manifest.md`; all prior subbundle evidence stayed consistent with final tests and browser proof. |
| SB09 | Passed | Passed | Passed | Completed repair | Closure proof is in `bundle://proof/SB09/manifest.md`; failing-first approval-policy regression, focused integration proof, manifest validator proof, source assertions, anti-stub audit, and completed-stage validation passed. |

## Browser Validation Analytics

| Subbundle | Route | Viewport | Playwright MCP evidence | Screenshots | Result |
| --- | --- | --- | --- | --- | --- |
| SB01 | N/A | N/A | N/A | N/A | Not required unless baseline exposes UI regression. |
| SB02 | N/A | N/A | N/A | N/A | Not required; backend/plugin executor slice. |
| SB03 | agents/workflows | Desktop | Deferred to SB08; loader proof captured in `bundle://proof/SB03/transcripts/unit-template-loader-after-sb03.txt` | N/A | Automated file-backed catalog visibility passed; browser route proof is closed in SB08. |
| SB04 | N/A | N/A | N/A | N/A | Passed by component/schema service proof; no visible Scheduler rendering changed. |
| SB05 | scheduler | Desktop and narrow | `bundle://proof/SB05/browser/scheduler-office365-watch-browser-proof.json` | `bundle://proof/SB05/browser/scheduler-office365-watch-typed-form-desktop.png`; `bundle://proof/SB05/browser/scheduler-office365-watch-validation-narrow.png` | Passed: typed fields render, manual email/project/node values sync into JSON, every-two-hours preset applies, and clearing required email blocks save. |
| SB06 | N/A | N/A | N/A | N/A | Passed by backend Scheduler/runtime-gateway proof; visible Scheduler history/status proof belongs to SB07/SB08. |
| SB07 | scheduler | Desktop | `bundle://proof/SB07/transcripts/browser-history-snapshot.md` | `bundle://proof/SB07/transcripts/browser-history-page.png` | Passed: history surface renders, no-action/waiting counters are visible, and status filter exposes `WaitingForApproval`; row-level route/policy labels are covered by component proof. |
| SB08 | scheduler; agents/workflows | Desktop and narrow | `bundle://proof/SB08/browser/browser-proof.md`; `bundle://proof/SB08/transcripts/completed-browser-proof-index.txt` | `bundle://proof/SB08/browser/scheduler-office365-configured-desktop.png`; `bundle://proof/SB08/browser/scheduler-office365-form-narrow.png`; `bundle://proof/SB08/browser/workflows-templates-desktop.png`; `bundle://proof/SB08/browser/workflows-templates-narrow.png`; `bundle://proof/SB08/browser/workflows-office365-toolbox-expanded-desktop.png` | Passed: Scheduler typed Office365 setup, raw JSON sync, validation, Workflows template visibility, and Office365 toolbox executors are browser-visible. |
| SB09 | N/A | N/A | N/A | N/A | Backend/plugin policy repair only; no visible Scheduler or Workflows UI changed. |

## Analytics Review

- Browser validation is required for Scheduler typed-input UX and final scenario proof.
- Backend-only subbundles may use component/API tests unless they expose new browser-visible behavior.
- Each browser row must be replaced with route, viewport, action/assertion evidence, screenshot paths, and pass/fail result while proof is fresh.

## Raw Note Closure

| Raw note | Status | Proof |
| --- | --- | --- |
| R1: New Office365 executor downloads at most one unprocessed email matching a configured address. | Solved | `bundle://proof/SB08/transcripts/passing-integration-office365-plugin-tests.txt` covers one-message address polling and descriptor visibility. |
| R2: Matching excludes messages already carrying the processed category. | Solved | `bundle://proof/SB08/transcripts/passing-integration-office365-plugin-tests.txt` asserts processed-category exclusion in fake Graph filters and fallback. |
| R3: No matching email returns no-op success by default, not exception/failure. | Solved | `bundle://proof/SB08/transcripts/passing-integration-office365-plugin-tests.txt`; `bundle://proof/SB08/transcripts/passing-integration-scheduler-tests.txt`. |
| R4: Mark processed step can add processed category without requiring a source category. | Solved | `bundle://proof/SB08/transcripts/passing-integration-office365-plugin-tests.txt` covers add-only Office365 category mutation. |
| R5: Summary workflow stores Markdown summary asset under configured project/node and then marks message processed. | Solved | `bundle://proof/SB08/transcripts/passing-unit-workflow-template-executor-tests.txt`; `bundle://proof/SB08/transcripts/passing-integration-scheduler-tests.txt`; `bundle://proof/SB08/final-verifier.md`. |
| R6: Task workflow creates project task nodes under configured project/node and then marks message processed. | Solved | `bundle://proof/SB08/transcripts/passing-unit-workflow-template-executor-tests.txt`; `bundle://proof/SB08/transcripts/passing-component-scheduler-workflows-tests.txt`; `bundle://proof/SB08/final-verifier.md`. |
| R7: Project writes are idempotent by Office365 message id. | Solved | `bundle://proof/SB06/transcripts/completed-proof-index.txt`; `bundle://proof/SB08/transcripts/passing-integration-scheduler-tests.txt`. |
| R8: Scheduler can select a workflow and configure typed input fields. | Solved | `bundle://proof/SB08/transcripts/passing-component-scheduler-workflows-tests.txt`; `bundle://proof/SB08/browser/browser-proof.md`. |
| R9: Scheduler can pick email from CRM while allowing manual email entry. | Solved | `bundle://proof/SB05/transcripts/completed-proof-index.txt`; `bundle://proof/SB08/browser/browser-proof.md`. |
| R10: Scheduler dispatch records NoMessages separately from failures. | Solved | `bundle://proof/SB08/transcripts/passing-integration-scheduler-tests.txt`; `bundle://proof/SB08/transcripts/passing-component-scheduler-workflows-tests.txt`. |
| R11: Approval/preapproval semantics for scheduled Office365 category mutation are explicit and auditable. | Repaired | `bundle://proof/SB09/transcripts/failing-first-office365-processed-marker-approval-policy.txt`; `bundle://proof/SB09/transcripts/passing-office365-processed-marker-policy.txt`; `bundle://proof/SB09/transcripts/passing-plugin-manifest-tests.txt`. |
| R12: Templates are file-backed under `Templates/Workflows` and loaded through the manifest. | Solved | `bundle://proof/SB08/transcripts/passing-unit-workflow-template-executor-tests.txt`; `bundle://proof/SB08/transcripts/passing-component-scheduler-workflows-tests.txt`; `bundle://proof/SB08/browser/browser-proof.md`. |

## SB01 Semantic Adequacy Evidence

- Raw note owned: R1-R12 baseline readiness plus red proof for missing Office365 address executor and Scheduler typed-input capability.
- Shipped behavior: No production behavior changed; the repaired bundle is executable and the current workflow/template/Scheduler baseline passed restore/build/unit/component/integration proof.
- Source proof: `bundle://proof/SB01/transcripts/source-assertions-baseline.txt`; `repo://src/plugins/CanDoItAll.Plugin.Office365/Office365WorkflowExecutor.cs`; `repo://src/CanDoItAll.Modules.SchedulerPlanner/SchedulerPlannerService.cs`.
- Test proof: `bundle://proof/SB01/transcripts/restore-baseline.txt`; `bundle://proof/SB01/transcripts/build-baseline-after-unlocking-web.txt`; `bundle://proof/SB01/transcripts/unit-workflow-baseline.txt`; `bundle://proof/SB01/transcripts/component-scheduler-workflows-baseline.txt`; `bundle://proof/SB01/transcripts/integration-scheduler-project-workflow-baseline.txt`.
- Shallow-pass trap: Treating category-based Office365 download or raw Scheduler `InputJson` as if it already satisfied address polling and typed Scheduler setup.
- Adversarial negative proof: `bundle://proof/SB01/transcripts/failing-first-missing-office365-scheduler-capabilities.txt`.
- Semantic positive proof: `bundle://proof/SB01/semantic-invariants.md`; `bundle://proof/SB01/transcripts/semantic-invariant-evidence.txt`.
- Anti-stub audit: no scoped Office365/Scheduler/template production stubs found in `bundle://proof/SB01/transcripts/anti-stub-audit-baseline.txt`.

## SB02 Semantic Adequacy Evidence

- Raw note owned: R1, R2, R3, and R4.
- Shipped behavior: new Office365 address/unprocessed executor registered in descriptor/DI, fake Graph address polling excludes processed category, bounded fallback handles unsupported server filters, no-message output succeeds by default, and mark-processed supports add-only category mutation.
- Source proof: `bundle://proof/SB02/transcripts/source-assertions-office365-address.txt`; `repo://src/plugins/CanDoItAll.Plugin.Office365/Office365GraphClient.cs`; `repo://src/plugins/CanDoItAll.Plugin.Office365/Office365WorkflowExecutor.cs`.
- Test proof: `bundle://proof/SB02/transcripts/build-after-sb02.txt`; `bundle://proof/SB02/transcripts/integration-office365-address-after-implementation.txt`.
- Shallow-pass trap: treating the existing category executor, preview JSON, or unbounded mailbox retrieval as address-based unprocessed email polling.
- Adversarial negative proof: `bundle://proof/SB02/transcripts/failing-first-office365-address-before-implementation.txt`; processed-message, wrong-address, invalid-address, and no-message cases in the passing transcript.
- Semantic positive proof: `bundle://proof/SB02/semantic-invariants.md`; `bundle://proof/SB02/transcripts/semantic-invariant-evidence.txt`.
- Anti-stub audit: no scoped Office365 production stubs found in `bundle://proof/SB02/transcripts/anti-stub-audit-office365-address.txt`.

## SB03 Semantic Adequacy Evidence

- Raw note owned: R5, R6, and R12, with input-path groundwork for R8.
- Shipped behavior: new file-backed Office365 email-watch summary/task templates are registered in the workflow manifest, skip side effects on no-message route, write project output before mark-processed, and resolve Scheduler-supplied Office365 settings through explicit JSON paths.
- Source proof: `bundle://proof/SB03/transcripts/source-assertions-office365-watch-templates.txt`; `repo://Templates/Workflows/workflows/office365-email-watch-workflows.yaml`; `repo://src/plugins/CanDoItAll.Plugin.Office365/Office365WorkflowExecutor.cs`.
- Test proof: `bundle://proof/SB03/transcripts/build-after-sb03.txt`; `bundle://proof/SB03/transcripts/unit-template-loader-after-sb03.txt`; `bundle://proof/SB03/transcripts/integration-office365-scheduler-input-paths-after-sb03.txt`.
- Shallow-pass trap: adding example YAML that is not manifest-loaded, routing no-message through LLM/project/mark nodes, or marking the Office365 message before the project write succeeds.
- Adversarial negative proof: `bundle://proof/SB03/transcripts/failing-first-office365-watch-templates-before-implementation.txt`; graph assertions reject missing no-message branches and wrong write-before-mark ordering.
- Semantic positive proof: `bundle://proof/SB03/semantic-invariants.md`; `bundle://proof/SB03/transcripts/semantic-invariant-evidence.txt`.
- Anti-stub audit: no scoped Office365/template production stubs found in `bundle://proof/SB03/transcripts/anti-stub-audit-office365-watch-templates.txt`.

## SB04 Semantic Adequacy Evidence

- Raw note owned: R8 and R12.
- Shipped behavior: file-backed `inputParameters` metadata maps into strongly typed descriptors, descriptors are saved on workflow definitions, Scheduler resolves schema and rejects missing required values, optional defaults are normalized into saved input JSON, and no-descriptor workflows retain raw JSON fallback.
- Source proof: `bundle://proof/SB04/transcripts/source-assertions-workflow-input-schema.txt`; `repo://src/CanDoItAll.AgentFramework.Models/Workflows/WorkflowInputParameterModels.cs`; `repo://src/CanDoItAll.Modules.SchedulerPlanner/SchedulerWorkflowInputSchemaService.cs`.
- Test proof: `bundle://proof/SB04/transcripts/build-after-sb04.txt`; `bundle://proof/SB04/transcripts/unit-template-catalog-schema-after-sb04.txt`; `bundle://proof/SB04/transcripts/integration-scheduler-workflow-schema-after-sb04.txt`.
- Shallow-pass trap: encoding metadata in descriptions, resolving schema only from template files, or adding UI-only defaults that are not saved into plan input JSON.
- Adversarial negative proof: `bundle://proof/SB04/transcripts/failing-first-missing-workflow-input-schema-before-implementation.txt`; missing required `emailAddress` schedule-save rejection in the passing integration transcript.
- Semantic positive proof: `bundle://proof/SB04/semantic-invariants.md`; `bundle://proof/SB04/transcripts/semantic-invariant-evidence.txt`.
- Anti-stub audit: no scoped workflow schema production stubs found in `bundle://proof/SB04/transcripts/anti-stub-audit-workflow-input-schema.txt`.

## SB05 Semantic Adequacy Evidence

- Raw note owned: R8 and R9.
- Shipped behavior: Scheduler resolves workflow input descriptors into typed controls, loads options through narrow providers for CRM contacts, Office365 connections, projects, and project nodes, keeps advanced raw JSON synchronized, provides an every-two-hours CRON preset, and validates required typed fields before save.
- Source proof: `bundle://proof/SB05/transcripts/source-assertions-scheduler-typed-input-after-sb05-final.txt`; `repo://src/CanDoItAll.Modules.SchedulerPlanner/Pages/SchedulerPlannerPage.razor`; `repo://src/CanDoItAll.Composition/SchedulerPlannerWorkflowInputOptionProviders.cs`.
- Test proof: `bundle://proof/SB05/transcripts/build-scheduler-planner-after-sb05-final.txt`; `bundle://proof/SB05/transcripts/component-seed-and-scheduler-after-sb05-final.txt`.
- Browser proof: `bundle://proof/SB05/browser/scheduler-office365-watch-browser-proof.json`.
- Shallow-pass trap: leaving users to edit raw JSON, hard-coding Office365 template fields in the page, or allowing stale raw JSON after a required field is cleared.
- Adversarial negative proof: `bundle://proof/SB05/transcripts/failing-first-typed-scheduler-form-before-implementation.txt`; component regression `Scheduler_typed_workflow_input_clearing_required_value_removes_json_and_blocks_save`.
- Semantic positive proof: `bundle://proof/SB05/semantic-invariants.md`; browser proof shows JSON sync and blocked save after clearing email.
- Anti-stub audit: no scoped Scheduler typed-input production stubs found in `bundle://proof/SB05/transcripts/anti-stub-audit-scheduler-typed-input-after-sb05-final.txt`.

## SB06 Semantic Adequacy Evidence

- Raw note owned: R3, R5, R6, R7, and R10.
- Shipped behavior: Scheduler maps completed workflow no-message payloads to `SchedulerPlanRunDispatchStatus.NoMessages`, treats that state as terminal success for dedupe, and leaves run/plan errors empty. Office365 watch project writes now resolve idempotency keys from message processing context and persist replay metadata through the project runtime gateway.
- Source proof: `bundle://proof/SB06/transcripts/source-assertions-after-sb06-final.txt`; `repo://src/CanDoItAll.Modules.SchedulerPlanner/SchedulerPlannerService.cs`; `repo://src/CanDoItAll.Modules.Workbench/ProjectStructure/WorkbenchProjectStructureRuntimeGateway.cs`; `repo://Templates/Workflows/workflows/office365-email-watch-workflows.yaml`.
- Test proof: `bundle://proof/SB06/transcripts/build-web-after-sb06-final.txt`; `bundle://proof/SB06/transcripts/unit-idempotency-and-template-after-sb06-final.txt`; `bundle://proof/SB06/transcripts/integration-no-message-idempotency-after-sb06-with-launcher-final.txt`.
- Shallow-pass trap: recording no-message only in test fakes, adding idempotency keys only to template text without gateway replay, or relying on one process dispatch while concurrent duplicate writes can still create two nodes.
- Adversarial negative proof: `bundle://proof/SB06/transcripts/failing-first-no-message-idempotency-before-implementation.txt`; intermediate failed integration transcript caught the metadata-normalization gap before closure.
- Semantic positive proof: `bundle://proof/SB06/semantic-invariants.md`; integration proof covers handler no-message dedupe, production launcher no-message parsing, duplicate asset replay, and concurrent node replay.
- Anti-stub audit: scoped matches in `bundle://proof/SB06/transcripts/anti-stub-audit-after-sb06-final.txt` are existing API/UI placeholder terms or legitimate optional `return null` paths, not introduced stubs.

## SB07 Semantic Adequacy Evidence

- Raw note owned: R3, R10, and R11.
- Shipped behavior: Scheduler runs now persist route and retry category, map workflow `no_messages` to terminal no-action success, map approval-waiting workflows to terminal `WaitingForApproval`, preserve existing `LastError` on no-message/no-retry runs, classify Graph/network and project-write failures separately, and keep Office365 mark-processed behind approval-required external-write policy.
- Source proof: `bundle://proof/SB07/transcripts/source-assertions-observability-retry.txt`; `repo://src/CanDoItAll.Modules.SchedulerPlanner/SchedulerPlannerService.cs`; `repo://src/CanDoItAll.Modules.SchedulerPlanner/SchedulerPlannerModels.cs`; `repo://src/plugins/CanDoItAll.Plugin.Office365/Office365WorkflowExecutor.cs`.
- Test proof: `bundle://proof/SB07/transcripts/passing-scheduler-retry-observability-integration.txt`; `bundle://proof/SB07/transcripts/passing-scheduler-history-component-tests.txt`; `bundle://proof/SB07/transcripts/passing-web-build.txt`; `bundle://proof/SB07/transcripts/passing-ef-no-pending-model-changes.txt`.
- Browser proof: `bundle://proof/SB07/transcripts/browser-history-snapshot.md`; `bundle://proof/SB07/transcripts/browser-history-page.png`.
- Shallow-pass trap: treating no-message as a generic success, retrying approval waits as failures, or letting Scheduler launch bypass Office365 external-write approval.
- Adversarial negative proof: `bundle://proof/SB07/transcripts/failing-first-observability-retry-before-implementation.txt`.
- Semantic positive proof: `bundle://proof/SB07/semantic-invariants.md`; integration proof covers no-message LastError preservation, waiting approval dedupe, Graph retry, project-write retry, live workflow approval waiting, and Office365 approval policy.
- Anti-stub audit: no scoped Scheduler/Office365 production code stub markers found in `bundle://proof/SB07/transcripts/anti-stub-audit-code-only.txt`.

## SB08 Semantic Adequacy Evidence

- Raw note owned: R1-R12 final closure.
- Shipped behavior: final proof confirms Office365 address polling, processed-category exclusion/addition, no-message success, write-before-mark templates, idempotent project writes, typed Scheduler setup, route/retry observability, approval waiting, and file-backed template visibility agree across source, tests, and browser proof.
- Source proof: `bundle://proof/SB08/transcripts/source-assertions-final.txt`; `repo://src/plugins/CanDoItAll.Plugin.Office365/Office365WorkflowExecutor.cs`; `repo://Templates/Workflows/workflows/office365-email-watch-workflows.yaml`; `repo://src/CanDoItAll.Modules.SchedulerPlanner/Pages/SchedulerPlannerPage.razor`.
- Test proof: `bundle://proof/SB08/transcripts/passing-unit-workflow-template-executor-tests.txt`; `bundle://proof/SB08/transcripts/passing-integration-office365-plugin-tests.txt`; `bundle://proof/SB08/transcripts/passing-integration-scheduler-tests.txt`; `bundle://proof/SB08/transcripts/passing-component-scheduler-workflows-tests.txt`.
- Shallow-pass trap: closing from template-loader proof only, from browser screenshots only, or from fake Graph tests that do not also prove Scheduler retry/idempotency and approval semantics.
- Adversarial negative proof: `bundle://proof/SB08/transcripts/completed-failing-first-index.txt`; `bundle://proof/SB08/final-verifier.md`.
- Semantic positive proof: `bundle://proof/SB08/semantic-invariants.json`; `bundle://proof/SB08/transcripts/completed-proof-index.txt`; `bundle://proof/SB08/browser/browser-proof.md`.
- Anti-stub audit: no scoped Office365/Scheduler/Workflow template production stubs found in `bundle://proof/SB08/transcripts/anti-stub-audit-final.txt`.

## SB09 Semantic Adequacy Evidence

- Raw note repaired: R11 plus the reopened live feedback that a late approval reported success without changing the email category.
- Root cause: Office365 mark-processed was modeled as approval-required external write; the runtime approval response completed the waiting run without re-entering the skipped executor, so the category mutation never happened after delayed approval.
- Shipped behavior: Office365 and Gmail processed-marker executors now declare `IdempotentExternalMarker` and `ApprovalRequirement.NotRequired`; the manifest validator allows that narrow policy while still rejecting generic unattended external writes.
- Source proof: `bundle://proof/SB09/transcripts/source-assertions-email-marker-policy.txt`; `repo://src/CanDoItAll.AgentFramework.Models/Workflows/WorkflowExecutorModels.cs`; `repo://src/CanDoItAll.Plugins.Abstractions/PluginManifestValidation.cs`; `repo://src/plugins/CanDoItAll.Plugin.Office365/Office365WorkflowExecutor.cs`; `repo://src/plugins/CanDoItAll.Plugin.Gmail/GmailWorkflowExecutor.cs`.
- Test proof: `bundle://proof/SB09/transcripts/failing-first-office365-processed-marker-approval-policy.txt`; `bundle://proof/SB09/transcripts/passing-office365-processed-marker-policy.txt`; `bundle://proof/SB09/transcripts/passing-plugin-manifest-tests.txt`; `bundle://proof/SB09/transcripts/passing-plugin-simulation-tests.txt`.
- Shallow-pass trap: making all scheduler-launched external writes unattended or relying on approval continuation without proving the skipped marker executor resumes.
- Adversarial negative proof: `bundle://proof/SB09/transcripts/failing-first-office365-processed-marker-approval-policy.txt`; `bundle://proof/SB09/transcripts/plugin-catalog-broad-run-unrelated-failures.txt` records unrelated package-install test failures from an intentionally broad run.
- Semantic positive proof: `bundle://proof/SB09/semantic-invariants.md`; `bundle://proof/SB09/transcripts/file-hashes-email-marker-policy.txt`.
- Anti-stub audit: scoped matches in `bundle://proof/SB09/transcripts/anti-stub-audit-email-marker-policy.txt` are existing deterministic fake-mode test names, not production stubs introduced by this repair.
