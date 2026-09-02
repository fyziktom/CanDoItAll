# SB08 — Cross-Repository Build, Test, Browser, And Container Proof

**Status:** Completed locally — source/package/browser/container proof passes; one original timing failure passed unchanged on retry and remains disclosed
**Outcome:** End-to-end evidence for source mode and package fallback  
**Proof tier:** Governed

## Strategy

Run in stages. Do not rerun every broad gate after every small fix.

## Stage A — Components final candidate

From a clean checkout/output state:

1. npm root and Tailwind installs,
2. Tailwind build,
3. asset verification,
4. deterministic BaseLib CSS diff check,
5. restore/build,
6. full tests,
7. package build at `V`,
8. package static-asset and version inspection.

## Stage B — FileTools final candidate

1. restore,
2. warning-as-error Release build,
3. full tests,
4. format verification,
5. package build at `V`,
6. package validator,
7. standalone sandbox browser smoke.

## Stage C — CanDoItAll source-reference mode

Use clean outputs and exact sibling commits:

1. product restore/build,
2. stable solution restore/build,
3. stable non-browser test filter,
4. documentation validation,
5. targeted component tests,
6. relevant integration tests,
7. Playwright build/host preparation.

## Stage D — Large-desktop browser proof

Use existing supported UI scope. Prefer the existing test viewport; otherwise use a clearly
recorded large-desktop viewport such as `1600x1000`.

Representative surfaces:

- application shell/home,
- Agents overview/catalog/team icon dialog,
- Workflows,
- Projects and project files,
- Resources/file browser,
- Project Structure workbench,
- Settings/provider configuration,
- Plugins,
- one chart-heavy surface,
- one dialog/tooltip/notification surface.

Assertions:

- no unhandled page errors,
- no failed BaseLib static asset request,
- no `.rz-icon-fallback`,
- text/icon controls retain accessible labels,
- no obvious global preflight collapse,
- primary interactions work,
- FileBrowser and FileInteraction flows work.

Capture screenshots only where they provide review value. Do not turn screenshot differences into
new design work.

## Stage E — Package-reference mode

1. combine locally built Components and FileTools packages at `V` in an ignored feed,
2. create a temporary NuGet config below `.artifacts`,
3. clean all relevant `bin/obj`,
4. restore/build/tests with `UseLocalCanDoItAllLibraries=false` consistently,
5. prove the package graph resolves only `V`,
6. verify static assets from packages load in a browser smoke.

## Stage F — Container source mode

Run repository Docker validation, then build with both sibling contexts from clean sources.
Start the container where feasible and request the health/readiness endpoint plus one UI asset.

## Evidence

For every command record:

- repository and SHA,
- dependency mode,
- exact command,
- start/end or duration,
- exit code,
- result summary,
- log/evidence path,
- skip reason if unavailable.

## Acceptance

- all required stages pass,
- source and package modes use separate clean outputs,
- browser proof shows assets/icons/layout functioning,
- FileTools standalone and host flows pass,
- container source build passes,
- no v2 contamination.

## Progression gate

`reviews/02-readiness-gate.md` can be marked ready with no critical/high open blocker.

## Reopen triggers

- candidate SHA changes,
- selected version changes,
- source/package/browser/container proof diverges.
