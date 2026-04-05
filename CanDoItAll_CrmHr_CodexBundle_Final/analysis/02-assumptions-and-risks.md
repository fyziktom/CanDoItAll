# Assumptions And Risks

## Working Assumptions

- The original uppercase bundle remains the canonical source package, while the new lowercase workflow files are the executable repair overlay.
- The CRM/HR module will be added as a new module project instead of overloading an existing module.
- Party linkage will extend current Workbench metadata rather than deleting project-local participant behavior.

## Critical Path Risks

- B01 is a critical foundation because schema, module registration, and Party identity choices unlock every later phase.
- B10 is a critical integration foundation because weak project/workbench linkage would invalidate staffing, CRM conversion, and AI-agent reuse proof.
- B11 and B12 can expose weak earlier assumptions because search, activity, privacy, and audit behavior operate across module boundaries.

## Validation Risks

- UI proof will require real app startup plus Playwright coverage over brand-new routes.
- Migration and schema proof will require both clean startup and targeted persistence tests.
- Storage-aware or attachment-heavy flows may need additional integration validation once CRM/HR assets exist.

## Reopen Triggers

- Reopen B01 if later subbundles reveal schema gaps, broken module composition, or invalid Party identity boundaries.
- Reopen B03 or B10 if project-local participant flows, meeting participants, or work-item assignees lose fidelity during central-party linkage.
- Reopen any UI subbundle if browser screenshots show clipping, inconsistent BaseLib composition, or broken route navigation.
