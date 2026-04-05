# Master Codex prompt

You are implementing the CanDoItAll CRM / HR bundle.

## Source-of-truth rule

Treat this bundle as the normalized specification for the feature. Do not invent a second architecture or silently narrow the requested scope.

## Required reading order before each bundle

1. `03_ARCHITECTURE/TARGET_ARCHITECTURE.md`
2. `03_ARCHITECTURE/DATA_MODEL_DESIGN.md`
3. `03_ARCHITECTURE/INTEGRATION_ARCHITECTURE.md`
4. `02_REQUIREMENTS/SCOPE_AND_NON_FUNCTIONAL_DECISIONS.md`
5. the target bundle folder:
   - `README.md`
   - `SPECIFICATION.md`
   - `FILE_REFERENCES.md`
   - `ACCEPTANCE_CRITERIA.md`
   - `CHECKLIST.md`
   - `ASCII_LAYOUTS.md`
   - `SCREENSHOT_REQUIREMENTS.md`

## Non-negotiable rules

1. Implement bundles in the dependency order from `04_PLAN/IMPLEMENTATION_SEQUENCE.md`.
2. The module name is **`CanDoItAll.Modules.CrmHr`**.
3. The domain root is **Party**. Do not create disconnected CRM and HR identity tables.
4. **Do not use canvas-related UI components.** Use BaseLib plus normal Razor/HTML/Tailwind only.
5. Reuse existing shared infrastructure:
   - `ISearchIndexService`
   - `IActivityStream`
   - `ProjectsService`
   - `ProjectWorkbenchService`
   - `WorkspaceService`
   - `ResourcesService`
6. Workbench participants are **project projections**, not dead legacy. Keep project-side participant flows operational.
7. AI agents must reuse `Workspace` provider profiles. Do not create a second provider registry.
8. Comments in code must be in English.
9. Do not close a UI bundle without Playwright validation, screenshots, and semantic review notes.
10. Do not close the whole implementation unless user-story traceability still holds.

## Expected implementation style

- follow existing repository patterns for service registration and EF configuration,
- keep page layout consistent with Projects and Resources pages,
- use sectioned editors instead of giant unstructured forms,
- keep deletes safe when historical links exist,
- add targeted indexes for common filters,
- add tests close to the changed behavior instead of relying only on smoke tests.

## Required evidence per bundle

For every bundle:

1. changed file list
2. automated tests executed
3. screenshot paths for UI changes
4. short semantic review of what the screenshots prove
5. unresolved risks, if any

## Anti-patterns to avoid

- creating separate `CustomerPerson`, `EmployeePerson`, and `AiAgentRecord` roots
- moving project structure concepts fully into CRM/HR
- storing provider secrets or model credentials in party notes
- replacing project participants with hard requirements for global directory records
- importing canvas libs because they feel convenient
- declaring success because tests pass while the page looks broken
- skipping cross-module search/activity integration until “later”

## Final completion gate

The implementation is only complete when:

- all bundles are implemented in order,
- project/workbench integration works,
- CRM and HR both operate from the same identity model,
- search/activity/accountability are integrated,
- privacy/audit rules are in place,
- and Playwright-backed screenshots have been semantically reviewed.
