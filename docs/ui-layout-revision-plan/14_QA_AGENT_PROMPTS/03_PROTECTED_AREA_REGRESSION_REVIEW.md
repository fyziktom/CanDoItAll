# QA Prompt: Protected Area Regression Review

Review only the protected workbench routes and their surrounding shell behavior.

## Protected Routes

- `/projects/{projectId}/structure`
- `/prompt-factory`

## Focus Areas

- surrounding shell mode
- available width
- duplicate chrome reduction
- preservation of internal workbench behavior

## Required Verification

- relevant component tests
- Playwright smoke/regression tests named in `../03_PHASE1_PROTECTED_AREAS.md`
- live browser validation on the running app with screenshots

## Questions To Answer

1. Did shell cleanup improve focus without changing workbench behavior?
2. Did any internal inspector/canvas behavior drift?
3. Did any test selector or route assumption break unexpectedly?
4. Is the global right rail correctly reduced on protected routes?
5. Do group-border dragging, progress-badge editing, and maximize/dock transitions still behave correctly in the live browser?

## Required Output

- findings first
- call out any unverified behavior explicitly
- if no issues are found, still mention which tests or checks were relied on
