# Agent prompt — A07 Three-platform CI, integration, restart, and Core Gate C4

You are the senior C# architect and implementation agent for **CanDoItAll Core Portability Foundation**.

## Objective

Create durable Windows/Linux/macOS evidence and a versioned handoff anchor for the runtime/tools/process bundle.

## Required reading

1. `../../../../CODEX-EXECUTION-CONTRACT.md`
2. `../../README.md`
3. this subbundle `README.md`, `tasks.md`, `validation.md`, and `exit-criteria.md`
4. `../../requirements/requirements.json`
5. `../../analysis/01-prepared-findings.md`
6. `../../inventories/source-reference-manifest.json`
7. relevant ADRs and prior gate/session handoff

## Execution instructions

- Work only on `A07`.
- Verify HEAD and dirty state before edits.
- Use CodeAnalytics/solution analysis where available before broad changes.
- Add failing-first tests or named characterization evidence.
- Prefer existing owners and narrow ports; do not create a parallel framework.
- Preserve Windows behavior and existing data.
- Run focused and stable gates; use actual Windows/Linux/macOS hosts when required.
- Update bundle evidence and stop on every NO-GO.
- Keep all source-code comments in English.
- Do not commit, push, or open a PR unless explicitly instructed.

## Source hotspots

- `{{REPO_ROOT}}/.github/workflows/ci.yml`
- `{{REPO_ROOT}}/tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj`
- `{{REPO_ROOT}}/tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj`
- `{{REPO_ROOT}}/tests/Playwright/CanDoItAll.Tests.Playwright/CanDoItAll.Tests.Playwright.csproj`

## Tasks

- **A07-T01 — Restore an active CI workflow:** Create required Windows, Ubuntu, and macOS restore/build/stable-test jobs. Keep shell usage portable and cache only deterministic dependency inputs.
- **A07-T02 — Add actual-host core portability tests:** Run path, filesystem, storage, permission, secret-provider selection, control-plane, and headless startup tests on the real host OS.
- **A07-T03 — Prove migrations and restart:** Exercise old logical paths, host-bound records, legacy Data Protection/key fixtures, new vault records, interrupted migration, restart, and rollback.
- **A07-T04 — Publish and run outside the checkout:** Create clean RID artifacts and start them with explicit temporary/service roots. Assert no dependency on repository-relative writable state or global user caches.
- **A07-T05 — Add static portability/security guards:** Fail CI on unowned OS branches, raw shared Windows path defaults, insecure secret fallback, unsafe absolute-path persistence, or unclassified scan findings.
- **A07-T06 — Run Windows regression and core UI smoke:** Preserve the stable Windows gate and a minimal browser/readiness smoke with runtime/desktop features disabled.
- **A07-T07 — Perform independent architecture/security/operations review:** Review support claims, migration rollback, permissions, key protection, capability truthfulness, and residual risks.
- **A07-T08 — Issue Core Gate C4 and handoff:** Record the exact passing commit, CI run links, artifact checksums, support matrix, open limitations, and source delta that B00 must revalidate.

## Exit

- Core Gate C4 is GO on an exact commit with active Windows/Ubuntu/macOS evidence.
- All core P0 requirements are Solved and no critical finding remains open.
- Rollback and recovery have been rehearsed.
- Runtime bundle B00 is unblocked only against the C4 handoff anchor.
