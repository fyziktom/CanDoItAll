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
- persistent desktop navigation while docked
- viewport-anchored maximize behavior
- radial-menu density, submenu clarity, and zoom-floor reach
- picker-upload visibility on canvas and preview persistence in the inspector

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
6. Does the docked route still retain the main desktop menu?
7. Does maximize start at the actual viewport origin instead of an inner shell wrapper?
8. Do the radial-menu hexes use their space well, and is the numeric priority submenu free of duplicate text?
9. Can the operator zoom out far enough to review a large map without resorting to browser zoom?
10. Does the file-chooser image flow create a visibly media-backed node on the canvas, and does reselecting it reopen the same uploaded preview in the inspector?

## Required Output

- findings first
- call out any unverified behavior explicitly
- if no issues are found, still mention which tests or checks were relied on
