# SB02 — Stabilize Components Main

**Status:** Completed locally — 409 tests pass; deterministic source/package assets and sandbox proof recorded
**Outcome:** Green Components integration branch with deterministic source-consumer assets  
**Proof tier:** Governed

## Repository / branch

Create a focused branch from current `CanDoItAll.Components/main`, for example:

```text
integration/original-ui-refactoring-release
```

Do not work from an old pre-merge UI branch.

## Scope

- reproduce and resolve the three recorded failing governed tests,
- review public API/source/static-asset changes,
- make BaseLib output CSS available in clean source checkout,
- add deterministic generated-CSS drift enforcement,
- full Components CI-equivalent validation.

## Non-goals

- no application v2 work,
- no general redesign of component APIs,
- no broad preflight rewrite unless browser proof later demonstrates a concrete defect,
- no package publish.

## Steps

### A. Reproduce

1. Install root and Tailwind npm dependencies.
2. Generate Tailwind and owned runtime assets.
3. Restore/build.
4. Run the three failing tests without update.
5. Capture actual-versus-approved differences.

### B. Review governed diffs

For public API:

- identify added, removed, and signature-changed types/members,
- confirm intended additions,
- treat any removal/signature change as a blocker requiring explicit justification.

For source snapshots:

- verify changed files correspond to the merged UI work,
- ensure no generated transient output or secrets enter the snapshot.

For Canvas assets:

- locate `CanvasPackageStaticAssetsMatchExpectedManifest`,
- compare each added/removed asset,
- verify runtime import paths and ownership,
- update the expected manifest only after review.

### C. Repair source-consumer CSS contract

1. Add an exception to `.gitignore` for:
   `src/CanDoItAll.Components.BaseLib/wwwroot/css/output.css`.
2. Run the canonical BaseLib Tailwind build.
3. Add the generated file to source control.
4. Keep sandbox output files ignored.
5. Add a CI step after generation that fails when the committed BaseLib output differs.
6. Update the relevant build/Tailwind documentation.
7. Add or strengthen a test/package assertion that BaseLib exposes both:
   - `css/material-symbols.css`,
   - `css/output.css`.

Do not hand-edit generated CSS.

### D. Update approvals deliberately

Use `CDA_UPDATE_STANDARD_APPROVALS=1` only after the semantic review. Update the Canvas manifest
through its owning mechanism. Capture the final approval diff in the execution report.

### E. Full gate

Run:

- npm installs,
- Tailwind generation,
- asset verification,
- restore,
- Release build,
- full tests,
- deterministic CSS drift check,
- package build and package content inspection.

## Acceptance

- all Components tests pass,
- package build runs,
- clean checkout contains BaseLib output CSS,
- regeneration produces no diff,
- BaseLib package contains Material Symbols and output CSS assets,
- approval review is documented,
- no v2 content or app redesign is present.

## Progression gate

Components integration branch is green locally and, when pushed/authorized, in CI.

## Reopen triggers

- Components main moves,
- approval diff contains an unintended removal,
- generated CSS is nondeterministic,
- package omits either required asset.
