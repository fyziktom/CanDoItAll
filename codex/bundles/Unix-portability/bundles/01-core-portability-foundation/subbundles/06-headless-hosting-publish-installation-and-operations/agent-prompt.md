# Agent prompt — A06 Headless hosting, publish, installation, and operations

You are the senior C# architect and implementation agent for **CanDoItAll Core Portability Foundation**.

## Objective

Turn code-level portability into repeatable Linux/macOS headless deployment and operator guidance without prematurely coupling to desktop runtime features.

## Required reading

1. `../../../../CODEX-EXECUTION-CONTRACT.md`
2. `../../README.md`
3. this subbundle `README.md`, `tasks.md`, `validation.md`, and `exit-criteria.md`
4. `../../requirements/requirements.json`
5. `../../analysis/01-prepared-findings.md`
6. `../../inventories/source-reference-manifest.json`
7. relevant ADRs and prior gate/session handoff

## Execution instructions

- Work only on `A06`.
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

- `{{REPO_ROOT}}/tools/install/Install-CanDoItAllWebApp.ps1`
- `{{REPO_ROOT}}/src/App/CanDoItAll.Web/CanDoItAll.Web.csproj`
- `{{REPO_ROOT}}/src/App/CanDoItAll.Web/appsettings.json`
- `{{REPO_ROOT}}/docs/development-runtime.md`
- `{{REPO_ROOT}}/docs/operations/installed-web-app.md`

## Tasks

- **A06-T01 — Define supported core profiles:** Separate headless Web host support from optional desktop/runtime claims. State database, secret backend, architecture/RID, and external dependency prerequisites.
- **A06-T02 — Publish clean artifacts:** Prove framework-dependent win-x64, linux-x64, osx-x64, and osx-arm64 publishes outside the repository. Do not add trimming, single-file, or self-contained changes without separate evidence.
- **A06-T03 — Create Linux service/runbook:** Define service user, XDG/data/control-plane roots, environment file, PostgreSQL dependency/readiness, systemd hardening, logs, restart, upgrade, backup, and rollback.
- **A06-T04 — Create macOS service/runbook:** Define interactive and launchd/headless profiles, Application Support/state/log roots, Keychain or headless provider requirements, restart, upgrade, backup, and rollback.
- **A06-T05 — Refactor installation boundaries:** Keep the existing Windows PowerShell installer working. Share publish/config generation where safe; implement Unix entry scripts or a small .NET installer without duplicating security/root logic.
- **A06-T06 — Add redacted diagnostics and health:** Expose bounded platform/root/provider/capability state, health/readiness, and support profile. Avoid secret values and minimize full absolute paths.
- **A06-T07 — Update developer/operator docs:** Replace universal Windows assumptions; document Linux/macOS setup, migrations, limitations, Docker/PostgreSQL, permissions, service profiles, and troubleshooting.
- **A06-T08 — Rehearse clean install/start/restart/rollback:** Use a clean user/service account and artifact directory, not the repository checkout; preserve logs and redacted evidence.

## Exit

- Clean headless startup/restart succeeds on Windows and Ubuntu; macOS publish/service contracts remain `ActualHostUnverified` pending `A07-MACOS-HEADLESS-ACTUALHOST-001`.
- Publish/support claims are bounded to proven RIDs and profiles.
- Linux/macOS service and rollback runbooks are complete and rehearsed where required.
- Documentation no longer treats Windows behavior as universal.
