# Repository artifact hygiene and bundle leak cleanup

## Status

- `Ready`

## Objective

Remove transient Codex work-package artifacts from tracked repo content and tighten ignore rules so future bundles/exports remain local helper material instead of stable repository content.

## Success Criteria

- `git ls-files` returns no tracked paths under `codex/bundles/`.
- `git ls-files` returns no tracked paths under `codex/bundle-exports/`.
- `git ls-files` returns no root `01-execution-report.md`.
- `git ls-files` returns no transient Codex ZIP exports.
- `.gitignore` ignores broad transient Codex work-package output paths while preserving `codex/skills/bundles/**`.
- Existing bundle-preparation skill files remain present and usable.

## Covered Inputs

- User requirement that bundles are development helpers and should not remain as repo concern.
- Observed root `01-execution-report.md` reference to `codex/bundles/maf-processes-provider-hardening-followup-v1`.
- Observed compare artifact `codex/bundle-exports/process-runtime-live-openai-verification-host-alpha-v1.zip`.
- Observed `.gitignore` only partially ignores bundle-related paths.

## Prerequisites

- none

## Exact Source References

- `.gitignore`
- `01-execution-report.md`
- `codex/bundles/**`
- `codex/bundle-exports/**`
- `codex/skills/bundles/**`
- `tests/CanDoItAll.Tests.Unit/SecretScanningTests.cs`

## Deliverables

- Remove tracked transient root report and ZIP/export artifacts.
- Remove tracked `codex/bundles/**` content if any remains, excluding `codex/skills/bundles/**` because that is not under `codex/bundles` and is valid tooling.
- Update `.gitignore` with broad transient entries, for example:

```gitignore
# Codex transient work-package outputs. The bundle preparation skill under
# codex/skills/bundles is source tooling and must remain tracked.
codex/bundles/**
codex/bundle-exports/**
codex/**/*.zip
.codex/runlogs/**
.codex/tmp/**
.codex/temp/**
.codex-artifacts/**
.codex-tmp/**
.codex-temp/**
```

- Add or prepare a tracked-file hygiene test/helper that rejects forbidden tracked artifact paths.

## Dependency Impact

- SB02 relies on this cleanup because naming scans should not be polluted by deleted bundle artifacts.
- SB05 relies on this cleanup for final merge readiness.

## Validation Depth

- Critical repository hygiene foundation.

## Implementation Steps

1. Run `git status --short` and record current state.
2. Run:

```bash
git ls-files | rg '(^01-execution-report\.md$|^codex/(bundles|bundle-exports)/|^codex/.*\.zip$)'
```

3. Delete or untrack each forbidden path. Use `git rm` for tracked files. Do not delete `codex/skills/bundles/**`.
4. Update `.gitignore` with broad transient Codex work-package output patterns.
5. Review `SecretScanningTests.cs`. Keep local transient helper paths out of secret scans if necessary, but add a separate tracked-file guard so tracked work-package artifacts cannot remain hidden.
6. Add a small unit test, e.g. `RepositoryTransientArtifactHygieneTests`, that enumerates tracked files via `git ls-files` and fails for:
   - `01-execution-report.md`,
   - `codex/bundles/`,
   - `codex/bundle-exports/`,
   - `codex/*.zip` or `codex/**/*.zip` transient proof exports,
   - `.codex/runlogs/`, `.codex/tmp/`, `.codex/temp/`, `.codex-artifacts/`, `.codex-tmp/`, `.codex-temp/`.
7. Make the tracked-file helper robust:
   - Locate repo root by `CanDoItAll.slnx`.
   - Run `git ls-files` with `ProcessStartInfo`.
   - If Git is unavailable, fall back to scanning the physical tree but skip `.git`, `bin`, `obj`, `.artifacts`, `.playwright-mcp`, and ignored local Codex dirs.
   - Prefer a clear failure message listing the first 20 forbidden tracked paths.
8. Run the new hygiene test.

## Scope Exceptions

- This subbundle does not remove `codex/skills/bundles/**`.
- This subbundle does not rename tests yet; SB02 owns naming cleanup.

## Do Not Do

- Do not delete bundle-preparation skill scripts/templates.
- Do not rewrite process runtime code.
- Do not add broad `codex/**` ignore that hides source tooling.
- Do not rely only on `.gitignore`; verify tracked files.

## Acceptance Checklist

- [ ] `git ls-files` forbidden path scan returns no matches.
- [ ] `.gitignore` blocks future transient Codex work-package outputs.
- [ ] Bundle skill paths still exist under `codex/skills/bundles/**`.
- [ ] New or updated hygiene test passes.
- [ ] Execution report records deleted paths and scan output.

## Proof Required

- `git status --short`
- `git ls-files | rg '(^01-execution-report\.md$|^codex/(bundles|bundle-exports)/|^codex/.*\.zip$)'` with no matches after cleanup.
- `dotnet test tests/CanDoItAll.Tests.Unit --filter RepositoryTransientArtifactHygiene`

## Browser Validation Logging

- N/A

## Progression Gate

Downstream subbundles may start only after forbidden tracked artifact scan returns no matches and the bundle-preparation skill still exists.

## Suggested Agent Prompt

```text
Implement subbundle 01 only. Clean tracked transient Codex work-package artifacts and ignore rules without touching codex/skills/bundles. Add a tracked-file repository hygiene guard. Capture git status, tracked path scan output, and focused unit test output. Stop if any codex/skills/bundles file would be deleted or ignored.
```
