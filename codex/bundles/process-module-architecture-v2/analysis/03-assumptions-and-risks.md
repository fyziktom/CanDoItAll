# Assumptions And Risks

## Assumptions

- The rewrite can introduce new projects under `src/`.
- The current Process UI can be kept as a visual and interaction reference while backend contracts are replaced.
- Templates can move toward file-first storage with database indexes.
- Git is available in supported runtime environments where versioned configuration is edited.
- Existing process runs do not need seamless runtime continuation across the rewrite unless a later migration requirement explicitly says so.
- Existing templates should be migrated, not discarded.

## Critical Path Risks

- If the generic core receives domain-specific terms, driver layering will become fake and every future domain will require runtime edits.
- If process instance composition is not persisted before execution, runtime behavior will remain implicit and hard to debug.
- If the artifact ledger is underspecified, recovery/resupply will remain unreliable.
- If branch loops are not modeled as runtime budgets, backward routing will create uncontrolled retry cycles.
- If monitoring stays query-first, live/history views will keep competing with runtime execution for data.
- If templates remain database-only or projection-driven, global component updates and conflict resolution will be too expensive and too opaque.
- If old Process code is not copied before deletion, useful UI, rules, and test evidence will be lost.

## Validation Risks

- Pure unit tests can pass while process orchestration fails under concurrent runs. Integration tests must cover claims, outbox, event emission, and snapshot projections.
- Static architecture tests can preserve file shape while behavior regresses. Behavioral tests must cover instance composition and runtime transitions.
- Driver tests can prove a single driver but miss layered-driver selection. Contract tests must exercise driver stacks.
- UI component tests can prove rendering while missing live snapshot freshness and time-range correctness. Playwright tests must verify live/history behavior.

## Reopen Triggers

- A future implementation phase adds domain vocabulary to generic core/runtime contracts.
- A step execution strategy is selected dynamically inside the dispatcher instead of by the instance builder.
- A subprocess is started without going through recursive instance composition.
- A manager recovery action mutates process state without an auditable event and budget check.
- A branch backward route lacks a loop budget.
- A template migration skips an intermediate schema version.
- Markdown or Mermaid becomes canonical source for template behavior.
- A Git operation is implemented by ad hoc file copying instead of the Git wrapper.

