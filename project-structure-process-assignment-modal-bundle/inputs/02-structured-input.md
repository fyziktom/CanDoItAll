# Structured Input

## Objectives

- O-001: Present project-structure process staffing as the first-class full-screen assignment experience shown in the design.
- O-002: Preserve the current process launch lifecycle: create launch plan, assign roles, review assignments, start only when required gaps are resolved.
- O-003: Reuse the existing agent switch dialog behavior for manual AI-agent selection.
- O-004: Validate with browser screenshots and record proof in the bundle.

## Hard Constraints

- The staffing-stage modal must be full-screen, not the current narrow generic dialog.
- Manual assignment must expose search, tag filtering, and favorites-first behavior through the existing agent switcher/card path.
- The existing `ProcessLaunchPlan` and candidate selection APIs must remain compatible with current processes UI flows.
- UI proof must include the open modal state and the manual agent-picker open state.

## Assumptions

- The existing role/candidate state projected into `ProjectStructureProcessStartDialogState` is the source of truth for displayed role assignments.
- Bound technical agents may already appear in launch-plan candidates through the CRM-HR AI resource directory. If a manually selected agent is absent from the launch-plan candidates, execution must add a safe launch candidate or document the blocker.

## Risks

- Full-screen modal styling may be affected by the existing overlay dialog inline styles.
- Adding manual agents may need backend support beyond selecting an existing candidate.
- Browser proof may require seed data that reaches a staffing-stage modal without starting an actual external process.
