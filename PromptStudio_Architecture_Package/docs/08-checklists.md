# 08 — Checklists

This document contains implementation, review, QA, and release checklists intended for repeated use during development.

## 1. Architecture checklist

### 1.1 Module boundary checklist
- [ ] Each module has a clearly stated responsibility.
- [ ] Cross-module references are intentional and minimal.
- [ ] SharedKernel contains only truly shared primitives.
- [ ] Infrastructure does not absorb domain rules.
- [ ] UI pages do not own business logic.
- [ ] Module services expose clear contracts.
- [ ] Future extraction to a sidecar/service is still plausible.

### 1.2 Persistence checklist
- [ ] Each entity is owned by one module.
- [ ] Table names are module-prefixed.
- [ ] `IDbContextFactory` is used for runtime operations.
- [ ] `IDesignTimeDbContextFactory` exists and works.
- [ ] SQLite and PostgreSQL configuration paths both work.
- [ ] Transactions and save flows are explicit where required.
- [ ] Audit records are written for key state transitions.

### 1.3 Security checklist
- [ ] Secrets are centralized as secret records.
- [ ] Secret payloads are encrypted at rest.
- [ ] Logs redact sensitive values.
- [ ] UI redacts sensitive fields by default.
- [ ] Export/send flows warn when sensitive data may leave the machine.
- [ ] Execution-capable actions require approval.
- [ ] Secret references are used instead of raw duplication.

### 1.4 Background processing checklist
- [ ] Long-running work is not done directly in page logic.
- [ ] Background jobs are visible to the user.
- [ ] Failures are actionable and traceable.
- [ ] Job inputs avoid raw secret duplication.
- [ ] Jobs publish activity/status updates.

### 1.5 Development manager checklist
- [ ] `CanDoItAll.Manager` exists as a separate local tool.
- [ ] `dotnet watch` is supervised with normalized states.
- [ ] Ready status requires runtime readiness, not console parsing alone.
- [ ] Loopback-only API boundaries are enforced.
- [ ] SSE or equivalent event streaming exists for watch and tuning events.
- [ ] Manager artifacts are excluded from self-triggering rebuild loops.
- [ ] Capsule coverage and drift reporting are visible.

## 2. UX and UI checklist

### 2.1 Shell checklist
- [ ] The shell shows current workspace/project context clearly.
- [ ] Main navigation is stable across pages.
- [ ] The internal tab strip is visible and understandable.
- [ ] Internal tabs can be reordered and pinned.
- [ ] Dirty-state and sleeping-state indicators are visible.
- [ ] Search is accessible from the shell.
- [ ] Background task state is visible.
- [ ] Provider health is visible.
- [ ] Right-side drawer follows one consistent pattern.

### 2.2 Page checklist
- [ ] Every page has a title and purpose summary.
- [ ] Primary actions are obvious.
- [ ] Empty states exist.
- [ ] Loading states exist.
- [ ] Error states exist.
- [ ] Filter/sort/search controls exist where needed.
- [ ] Lists and detail views are consistent.

### 2.3 Form checklist
- [ ] Required fields are visibly marked.
- [ ] Validation messages are clear.
- [ ] Save state is obvious.
- [ ] Advanced options are progressively disclosed.
- [ ] Notes can be attached where the design expects them.
- [ ] Sensitive inputs are handled carefully.
- [ ] Cancel/reset flows are predictable.

### 2.4 Accessibility checklist
- [ ] Keyboard navigation works for primary workflows.
- [ ] Labels exist for interactive controls.
- [ ] Contrast is acceptable.
- [ ] Focus states are visible.
- [ ] Error summaries are screen-reader friendly.
- [ ] Icons are not the only source of meaning.

### 2.5 Workbench restore checklist
- [ ] Internal tab state persists through browser storage.
- [ ] Refresh or reconnect restores the previous workbench session.
- [ ] Partial restore failure does not lose the entire session.
- [ ] Sleeping tabs can wake safely.
- [ ] Linked artifacts reopen inside internal tabs.
- [ ] Prompt-flow node state restores without duplicating or silently dropping branches.

### 2.6 Development tuning checklist
- [ ] Tuning mode is hidden outside development mode.
- [ ] Tunable components use one shared boundary pattern.
- [ ] Tuning handles are visually distinct from business actions.
- [ ] The tuning panel shows capsule, route, and project or tab context before submission.
- [ ] Clipboard image paste works or is intentionally replaced with an equivalent flow.
- [ ] "Codex finished" is distinct from "watch ready for review".

## 3. Project module checklist

- [ ] Project creation wizard exists.
- [ ] Project editing exists.
- [ ] Phase timeline exists.
- [ ] Status handling exists.
- [ ] Primary and secondary language selection exists.
- [ ] Generalized option selections exist.
- [ ] Notes per option exist.
- [ ] Project overview summarizes current phase and next actions.

## 4. Resource module checklist

- [ ] Generalized `ProjectResource` model exists.
- [ ] Descriptor registry exists.
- [ ] Required resource types all have editors.
- [ ] Resource detail panel exists.
- [ ] Validation state is tracked.
- [ ] Preview/indexing capability is tracked.
- [ ] Sensitivity classification is tracked.
- [ ] Secret references work correctly.
- [ ] Unsupported file types still have graceful metadata handling.

## 5. Prompt module checklist

- [ ] Prompt draft model exists.
- [ ] Prompt version model exists.
- [ ] Finalization creates immutable versions.
- [ ] Collections/galleries exist.
- [ ] Tags exist.
- [ ] Search exists.
- [ ] Usage history exists.
- [ ] Clone flow exists.

## 5A. Workbench and orchestration checklist

- [ ] Workbench module exists.
- [ ] Internal tab host service exists.
- [ ] Tab snapshot and restore model exists.
- [ ] Tab sleep and wake policy exists.
- [ ] Project structure canvas wrapper exists.
- [ ] Project calendar wrapper exists.
- [ ] Canvas and calendar open linked artifacts into internal tabs.
- [ ] Prompt sessions can be represented in the structure surface.
- [ ] Flow-template and prompt-run nodes can be represented in the structure surface.
- [ ] The grouped hexagonal context menu exists on the structure canvas.
- [ ] Canvas commands are routed into typed C# handlers instead of mutating state in JavaScript.

## 5B. Development acceleration checklist

- [ ] Manager watch status endpoint exists.
- [ ] Manager wait-ready endpoint or SSE exists.
- [ ] Runtime readiness endpoint exists in the main app.
- [ ] Capsule parser and artifact generator exist.
- [ ] Capsule skip marker exists for approved exemptions.
- [ ] Capsule coverage and drift contract exists.
- [ ] A tuning request is not marked ready if changed files introduce unreported capsule drift.
- [ ] Tuning request model exists.
- [ ] Tuning request history is traceable.

## 6. Prompt factory checklist

- [ ] Shared prompt block catalog exists.
- [ ] Prompt-flow template catalog exists.
- [ ] Prompt-run and prompt-run-node models exist.
- [ ] Parallel prompt branches are supported with clear lineage.
- [ ] Project phase selection exists.
- [ ] Flow-template selection exists.
- [ ] Shared-block selection exists.
- [ ] Shared blocks can be auto-applied by prompt type and then customized safely.
- [ ] Blueprint selection exists.
- [ ] Context assembly service exists.
- [ ] Prompt preview is editable.
- [ ] Missing-input warnings exist.
- [ ] Save-as-draft exists.
- [ ] Save-as-final exists.
- [ ] Copy/export exists.
- [ ] Provider send flow exists.
- [ ] Prompt build session is recorded.

## 7. Validation checklist

### 7.1 Core validation engine
- [ ] Validation run model exists.
- [ ] Finding model exists.
- [ ] Severity is explicit.
- [ ] Checklist version is tracked.
- [ ] Review decisions are storable.
- [ ] Validation can link to project artifacts.

### 7.2 Review coverage
- [ ] Story validation exists.
- [ ] Layout validation exists.
- [ ] Architecture validation exists.
- [ ] Plan validation exists.
- [ ] Prototype validation path exists.
- [ ] Test coverage planning exists.

## 8. Test lab checklist

- [ ] Test plan model exists.
- [ ] Test case linkage exists.
- [ ] Screenshot evidence model exists.
- [ ] Playwright linkage exists.
- [ ] Results can be stored.
- [ ] Story/phase linkage exists.
- [ ] Test status categories are clear.

## 9. Provider integration checklist

- [ ] Provider abstraction exists.
- [ ] OpenAI profile path works.
- [ ] Ollama local profile path works.
- [ ] Ollama remote profile path works.
- [ ] Health checks exist.
- [ ] Capability flags are tracked.
- [ ] Provider failures produce actionable errors.
- [ ] Request logs are redacted.
- [ ] Send/export paths clearly identify provider and model.

## 10. Testing checklist

### 10.1 Unit testing
- [ ] Domain rules are unit-tested.
- [ ] Prompt rendering logic is unit-tested.
- [ ] Validation rules are unit-tested.
- [ ] Mapping and conversion helpers are unit-tested.
- [ ] Prompt-flow branching and node-state rules are unit-tested.
- [ ] Canvas command routing can be tested without the JavaScript renderer.

### 10.2 Integration testing
- [ ] SQLite integration tests exist.
- [ ] PostgreSQL path is covered in at least smoke/integration form.
- [ ] Secret storage round-trip is tested.
- [ ] Provider adapter contract tests exist.
- [ ] Resource persistence and retrieval are tested.
- [ ] Manager readiness confirmation is tested against build, fault, and recovery transitions.
- [ ] Capsule generation and drift detection are integration-tested.

### 10.3 Component testing
- [ ] Key form components are covered.
- [ ] Wizard components are covered.
- [ ] Status and detail panels are covered.
- [ ] Error/loading states are covered.
- [ ] Shared-block and flow-template selection surfaces are covered.
- [ ] Hexagonal context menu behavior is covered.

### 10.4 End-to-end testing
- [ ] Startup smoke test exists.
- [ ] Project creation flow exists.
- [ ] Add-resource flow exists.
- [ ] Development manager ready signal flow exists.
- [ ] Internal tab restore flow exists.
- [ ] Project structure canvas flow exists.
- [ ] Project calendar flow exists.
- [ ] Prompt-flow branching flow exists.
- [ ] Prompt factory flow exists.
- [ ] Validation center flow exists.
- [ ] Test lab flow exists.
- [ ] Dev-only tuning request flow exists with a fake or controlled Codex adapter.

## 11. Documentation checklist

- [ ] Module responsibilities are documented.
- [ ] Setup instructions are documented.
- [ ] Storage root behavior is documented.
- [ ] Database configuration is documented.
- [ ] Provider configuration is documented.
- [ ] Secret handling behavior is documented.
- [ ] Known limitations are documented.
- [ ] Code comments are in English.
- [ ] Significant handwritten components and types have capsules or explicit skip markers.
- [ ] Generated capsule index and coverage outputs are current.

## 12. Release readiness checklist

### 12.1 Technical readiness
- [ ] Solution builds from clean checkout.
- [ ] Database setup path is documented.
- [ ] First-run experience works.
- [ ] Development manager startup path is documented.
- [ ] Workbench restore after interruption works.
- [ ] Logs are reviewable.
- [ ] Migration path is stable.
- [ ] Provider settings survive restart.
- [ ] Workspace storage path behavior is stable.

### 12.2 Product readiness
- [ ] A user can complete the primary end-to-end journey.
- [ ] A user can work through internal tabs instead of many browser tabs.
- [ ] Project structure workbench is usable.
- [ ] Project calendar is usable.
- [ ] Prompt factory is usable without hidden setup.
- [ ] Validation center stores results reliably.
- [ ] Test lab stores evidence reliably.
- [ ] Required resource types are available.
- [ ] Screens are not placeholder-quality.

### 12.3 Safety readiness
- [ ] No known plain-text secret leak exists.
- [ ] Approval gates are enforced where promised.
- [ ] Export/send warnings are present.
- [ ] Dangerous actions are clearly labeled.
- [ ] Secret reveal/copy behavior is intentional and reviewable.
- [ ] Tuning mode is loopback-only, dev-only, and workspace-bounded.
- [ ] Manager diagnostics do not expose raw secrets or unsafe payloads.

## 13. Executive “do not compromise” checklist

These items must not be traded away for speed:
- [ ] secret safety
- [ ] prompt traceability
- [ ] typed resource extensibility
- [ ] validation persistence
- [ ] UI consistency
- [ ] test baseline
- [ ] module boundaries
- [ ] trustworthy watch-ready loop
- [ ] capsule freshness and drift visibility
