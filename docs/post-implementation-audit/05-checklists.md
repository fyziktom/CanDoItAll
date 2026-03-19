# 05 - Recovery Checklists

Use these checklists during the repair effort.

## 1. Shell and navigation checklist

- [ ] Left rail includes workspace context, not only flat routes.
- [ ] Left rail exposes opened projects or active work items where appropriate.
- [ ] Top bar shows current project and phase context.
- [ ] Right rail follows one consistent action and inspector pattern.
- [ ] Collection screens default to cards instead of dense tables.
- [ ] Empty, loading, and error states exist on major pages.

## 2. Internal tab checklist

- [ ] Tabs are artifact-aware, not route-only.
- [ ] Opened projects can live in dedicated internal tabs.
- [ ] Prompt wizard sessions can live in dedicated internal tabs.
- [ ] Validation runs and test plans can reopen as meaningful work items.
- [ ] Pin, reorder, sleep, wake, and restore remain functional.
- [ ] Batch actions such as close-others or close-right exist.
- [ ] Overflow handling exists for many tabs.
- [ ] Restore failure degrades safely.

## 3. Project wizard checklist

- [ ] Project creation is wizard-first.
- [ ] Project editing has guided steps.
- [ ] Wizard can open in modal or dedicated tab depending on scope.
- [ ] Phase management is part of the wizard flow.
- [ ] Stack profile decisions are part of the wizard flow.
- [ ] Notes are available on all major decisions.
- [ ] Finishing the wizard leads naturally into project workbench usage.

## 4. Unified project object checklist

- [ ] A shared project-object base contract exists.
- [ ] Typed project-object kinds exist.
- [ ] Object links and relationship kinds exist.
- [ ] Object visual profiles exist.
- [ ] Resources participate in the unified project graph.
- [ ] Prompt runs and prompt steps participate in the unified project graph.
- [ ] Validation and test objects participate in the unified project graph.
- [ ] Users can create and connect project objects from the workbench.

## 5. Structure canvas checklist

- [ ] Real documented engine is wrapped.
- [ ] Placeholder button-list rendering is removed.
- [ ] Node positions persist.
- [ ] Viewport persists.
- [ ] Selection persists.
- [ ] Typed right-click grouped hex menu exists.
- [ ] Node creation works.
- [ ] Linking works.
- [ ] Branching from prompt nodes works.
- [ ] Linked artifacts open into internal tabs.
- [ ] C# remains the authority for commands and state.

## 6. Calendar checklist

- [ ] Real documented calendar engine is wrapped.
- [ ] Day, week, month, year, and list views work.
- [ ] Preferred view persists per project.
- [ ] Linked artifacts open into internal tabs.
- [ ] Phase, validation, and test events are represented clearly.

## 7. Prompt wizard checklist

- [ ] Prompt wizard is step-driven.
- [ ] Prompt sessions are restorable internal tabs.
- [ ] Shared prompt blocks are centrally managed.
- [ ] Flow templates are centrally managed.
- [ ] Auto-apply rules exist by prompt type, blueprint, or phase.
- [ ] Prompt nodes and branches are visible in the workbench graph.
- [ ] Parallel prompt branches preserve lineage.
- [ ] Prompt preview is editable.
- [ ] Save, export, and send flows remain integrated.

## 8. Resource UX checklist

- [ ] Resource editors are type-specific.
- [ ] Generic JSON is not the primary editing experience.
- [ ] Secret references are clear and safe.
- [ ] Validation, sensitivity, and preview states are visible.
- [ ] Resource objects have distinct visual profiles in the graph.

## 9. Manager and tuning checklist

- [ ] `dotnet watch` supervision still works.
- [ ] Ready-state confirmation still uses runtime readiness.
- [ ] Capsule coverage and drift still work.
- [ ] Tuning requests support screenshot or clipboard image attachment.
- [ ] Tuning requests use a real local Codex adapter or CLI path.
- [ ] Fake status simulation is removed from the main happy path.
- [ ] Ready-for-review still requires watch-ready and capsule-drift pass.
- [ ] Tuning history is traceable.

## 10. Testing checklist

- [ ] Playwright cleanup failure is fixed.
- [ ] Project wizard flow is covered.
- [ ] Prompt wizard session flow is covered.
- [ ] Real structure canvas interactions are covered.
- [ ] Real calendar interactions are covered.
- [ ] Typed resource editor flows are covered.
- [ ] Tuning flow is covered with a controlled adapter.
- [ ] Restore and sleep behavior remain covered.

## 11. Final release checklist

- [ ] The app feels like one workstation instead of a collection of forms.
- [ ] A new project can be created comfortably without raw CRUD fatigue.
- [ ] A prompt sequence can be managed visually and reopened later.
- [ ] Large project graphs remain usable.
- [ ] The shell supports daily multi-task work in one browser tab.
- [ ] The development loop is trustworthy for Codex-assisted refinement.
