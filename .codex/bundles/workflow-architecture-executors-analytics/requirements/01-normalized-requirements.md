# Normalized Requirements

| ID | Requirement | Acceptance signal |
|---|---|---|
| WF-ARCH-01 | Active workflow service/runtime contracts live in `Workflows.Abstractions`; implementations and composition point inward. | Core no longer references Runtime for contracts; duplicate active interfaces are removed; dependency tests pass. |
| WF-ARCH-02 | Executor catalog and invocation use one authoritative registration/descriptor model for standard and plugin executors. | Descriptor defaults, simulation, availability, and implementation cannot drift; duplicate IDs fail deterministically. |
| WF-ARCH-03 | Large partial executor clusters are replaced by cohesive collaborators with direct tests. | No partial `SourceIngestionWorkflowExecutor`; conversion/candidate/path responsibilities have isolated tests. |
| WF-OPS-01 | Runtime tools and workflow executors share typed file, document, spreadsheet, image, and command operations where behavior is identical. | No duplicate PDF/DOCX/XLSX parsing; adapters preserve surface-specific policy and receipts. |
| WF-EXEC-01 | Add `document.to-markdown` based on the canonical ManagedCode converter. | Catalog, invoker, schema, preview, DI, and execution tests cover the node. |
| WF-EXEC-02 | Complete high-value file/spreadsheet operations including exact directory listing and spreadsheet preview. | Typed operation values execute through shared services and render in UI. |
| WF-EXEC-03 | Add deterministic image inspection and usage-aware image analysis nodes over shared services. | Both nodes have typed settings/results; vision usage reaches analytics. |
| WF-EXEC-04 | Replace planned `command.process` only with a safe typed and approval-governed implementation. | No arbitrary silent shell fallback; dangerous recipes require approval; failures carry actionable state. |
| WF-PLUGIN-01 | Plugin executors use the same contribution contract and validation detects descriptor/renderer drift. | Real Gmail/Office365/Docker catalog entries match executable defaults/simulation; negative manifest tests pass. |
| WF-LIFE-01 | API/UI, scheduler, project structure, agent tool, and process/subprocess starts share a tested launch-policy boundary while retaining typed origin. | A lifecycle matrix has passing tests for every supported entry path. |
| WF-LIFE-02 | Workflow lifecycle is persisted before, during, and after backend execution. | Running/progress are queryable before completion; cancellation and crash semantics are explicit. |
| WF-AN-01 | Persist/query workflow duration and provider observations with model, input/cached/output/reasoning/total tokens, cost provenance, and unknown status. | Projection arithmetic, deduplication, correlation, and unknown-usage tests pass. |
| WF-AN-02 | Workflow API/UI expose run and aggregate analytics comparable to processes. | Large-screen UI shows duration, cost, tokens, and provider/model breakdown from a typed query service. |
| WF-UI-01 | Every runnable standard/plugin executor appears in create UI without executor-ID switches. | Schema-to-canvas round-trip tests cover field types/defaults and plugin descriptors. |
| WF-UI-02 | Settings rendering is isolated and extensible with trusted registered renderers plus schema fallback. | Renderer key is preserved; trust/version/component contract are enforced; arbitrary type strings are never activated. |
| WF-UI-03 | UI validation targets large desktop screens only. | Maximized browser screenshots and DOM assertions pass; no small/medium deliverable exists. |
| WF-TEST-01 | Tests prove behavior, boundaries, failure paths, and integration instead of file-count or partial-class shape. | Focused unit/component/integration suites and final solution build pass. |
