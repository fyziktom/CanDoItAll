# CanDoItAll Workflow Executor Catalog Follow-up Bundle

Status: `Completed`
Prepared date: `2026-05-29`  
Repository: `fyziktom/CanDoItAll`  
Branch: `processes-hardening`  
Comparison baseline: `0c5876df0fe42ffe3ecd2757257770683a9fb041`

## Validation Summary

- Bundle preparation status: `Prepared after structural repair`
- Bundle readiness gate: `Passed prepared-stage validator on 2026-05-29`
- Execution status: `Completed`
- Subbundle gate review: `SB01-SB10 passed`
- Final closure gate: `Completed-stage validator passed on 2026-05-29`
- Browser validation analytics: `Captured for agents/workflows desktop and narrow viewports`

## Mission

Review the pushed Codex implementation after the workflow MAF hardening follow-up and prepare the next execution bundle focused on:

1. fixing remaining runtime/catalog correctness issues,
2. expanding workflow executors and helper nodes users will obviously need,
3. making local workspace/folder/file workflows practical,
4. improving workflow authoring UX and template coverage,
5. keeping MAF 1.8 alignment stable without overbuilding durable production runtime too early.

## Current review summary

Codex completed significant work:

- `CanDoItAll.AgentFramework.Maf.csproj` now references `Microsoft.Agents.AI`, `Microsoft.Agents.AI.OpenAI`, and `Microsoft.Agents.AI.Workflows` version `1.8.0`; A2A preview packages are aligned to `1.8.0-preview.260528.1`.
- `MafWorkflowCompiler` keeps the CanDoItAll graph model as canonical and compiles dynamic graphs through MAF `WorkflowBuilder` / `BindAsExecutor`.
- `HumanInput` nodes are now execution-position-aware; they create an external request only when reached.
- Approval-required workflow executors are routed through `WorkflowExternalRequestApprovalGate`.
- `MafWorkflowEventNormalizer`, payload envelopes, metadata checkpoints, payload policy, and plugin observer composition were introduced.
- Backend catalog honesty improved: only `InProcess` is marked runnable in the current host; durable backends remain planned/unavailable.

## Priority findings for this follow-up

### P0 - Validator/catalog injection regression risk

`WorkflowDefinitionValidator` supports `IWorkflowExecutorCatalog` and only validates executor registration/runnability if the catalog is non-null. The current service registrations explicitly create `new WorkflowDefinitionValidator()` without the catalog. This likely disables validation of unknown, planned, or unavailable executor IDs during save/import/publish in the main product path.

Codex must fix this first before expanding the executor catalog.

### P0 - Artifact metadata does not appear to persist payload content

`WorkflowPayloadPolicyService` creates `WorkflowArtifactRecord` metadata and references `workflow-runs/<run>/payloads/...`, but the record model contains only metadata fields (`StoragePath`, `Summary`, etc.). I did not find evidence that the payload bytes/text are actually written to workspace/blob/database storage by the policy. This means truncated “artifact” references may point to content that does not exist.

Codex must either prove there is an existing writer, or add a workflow artifact content writer/reader boundary.

### P1 - Local folder/file support exists but is incomplete for normal users

There is a `storage.file` executor with list/stat/read/write/append/search/diff operations and a `source.ingest` executor that can ingest folders and selected files. However users will expect additional operations:

- create folder / ensure folder,
- delete file / delete folder safely,
- copy / move / rename,
- enumerate tree with metadata,
- glob with include/exclude patterns,
- file existence checks,
- read/write binary or file-reference payloads,
- zip/unzip or package folder,
- save HTTP/downloaded content to workspace,
- import a local folder as workflow sources with clear sandbox boundaries.

### P1 - Several obvious helper executors are still planned only

The descriptor catalog still lists `json.transform`, `markdown.render`, `utility.delay`, `human.approval`, and `command.process` as planned, not implemented. These should be phased carefully, because users will quickly need data shaping, markdown/report output, waits/schedules, explicit approval steps, and bounded command execution.

### P1 - Non-executor helper node kinds currently pass through

The model already has node kinds such as `Artifact`, `AgentStep`, `Subworkflow`, `StrictLogic`, and `Triage`. In `MafWorkflowCompiler`, only `LlmCall`, `Executor`, and `HumanInput` have special execution behavior; otherwise the node returns input unchanged. This can be acceptable for visual placeholders, but not for active nodes in user-authored workflows. The next bundle must either implement, validate-as-pass-through, or block unsupported active helper nodes.

## Recommended execution order

1. `subbundles/01-validator-catalog-and-runtime-guardrails`
2. `subbundles/02-artifact-content-store-and-payload-retrieval`
3. `subbundles/03-workspace-file-and-folder-executor-expansion`
4. `subbundles/04-json-transform-and-data-shaping-executor`
5. `subbundles/05-markdown-render-and-report-output-executor`
6. `subbundles/06-delay-approval-and-control-helper-nodes`
7. `subbundles/07-http-download-and-document-ingestion-expansion`
8. `subbundles/08-agent-subworkflow-and-artifact-node-policy`
9. `subbundles/09-workflow-template-and-ui-authoring-pack`
10. `subbundles/10-regression-scenario-harness-and-final-review`

## Closure expectation

This is a follow-up tuning bundle, not a durable production-runtime bundle. DurableTask/AzureFunctions remain future work unless explicitly selected by the owner. The immediate goal is to make the current in-process workflow authoring/runtime path safe, useful, and honest.
