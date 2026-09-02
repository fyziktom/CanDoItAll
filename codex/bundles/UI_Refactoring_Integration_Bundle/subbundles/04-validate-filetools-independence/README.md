# SB04 — Validate FileTools Independence

**Status:** Blocked until SB03 selects `V`  
**Outcome:** FileTools remains Components-independent and all nine packages/sandbox pass  
**Proof tier:** Behavioral + Governed

## Repository / branch

Create a focused branch from current `CanDoItAll.FileTools/main`, for example:

```text
integration/original-ui-refactoring-compat
```

## Scope

- version normalization from SB03,
- explicit Components/icon dependency audit,
- full FileTools build/test/format/package validation,
- standalone FileTools sandbox smoke.

## Non-goals

- no general UI redesign,
- no Components package/project reference,
- no host-specific CSS embedded in FileTools,
- no changes merely to make FileTools "look updated."

## Audit

Run:

```bash
rg -n "CanDoItAll\.Components|material-icons|material-symbols-rounded" \
  src samples tests tools
```

Review every result. Expected package-project Components result count is zero.

Confirm `Test-NuGetPackages.ps1` still rejects Components/main-app dependencies.

## Validation

Run the complete FileTools gate from `commands/01-validation-commands.md`, including:

- Release build with warnings as errors,
- all tests,
- formatting,
- nine-package build at `V`,
- package validator,
- package hash output,
- maintained sandbox start and focused browser smoke.

Sandbox proof should cover:

- directory navigation,
- item selection,
- file invocation callback,
- text preview/edit flow,
- PDF/browser-owned boundary if testable,
- Markdown/XLSX optional renderer registration if present in sandbox.

## Host-boundary note

Do not fix CanDoItAll host styling inside FileTools. Record host-only issues for SB06/SB08.

## Acceptance

- source audit finds no forbidden dependency,
- all nine package pairs are produced at `V`,
- package validator passes,
- sandbox has no unhandled exception,
- no unnecessary FileTools UI source change was introduced.

## Progression gate

FileTools branch is locally green and package artifacts are ready for the local feed.

## Reopen triggers

- FileTools main moves,
- package manifest changes,
- standalone sandbox reveals a real FileTools-owned regression.
