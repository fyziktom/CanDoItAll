# Agent prompt — A04 Secrets, vault drivers, Data Protection, and migration

You are the senior C# architect and implementation agent for **CanDoItAll Core Portability Foundation**.

## Objective

Provide truthful secure secret persistence on Windows, Linux, and macOS while preserving existing encrypted data.

## Required reading

1. `../../../../CODEX-EXECUTION-CONTRACT.md`
2. `../../README.md`
3. this subbundle `README.md`, `tasks.md`, `validation.md`, and `exit-criteria.md`
4. `../../requirements/requirements.json`
5. `../../analysis/01-prepared-findings.md`
6. `../../inventories/source-reference-manifest.json`
7. relevant ADRs and prior gate/session handoff

## Execution instructions

- Work only on `A04`.
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

- `{{REPO_ROOT}}/src/Modules/CanDoItAll.Modules.Security/SecretVaults.cs`
- `{{REPO_ROOT}}/src/Modules/CanDoItAll.Modules.Security/SecurityModels.cs`
- `{{REPO_ROOT}}/src/Modules/CanDoItAll.Modules.Security/SecurityModuleServiceCollectionExtensions.cs`
- `{{REPO_ROOT}}/src/Foundation/CanDoItAll.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs`
- `{{REPO_ROOT}}/src/Foundation/CanDoItAll.Infrastructure/ControlPlane/DatabaseProfileControlPlaneService.cs`

## Tasks

- **A04-T01 — Separate provider selection from provider claims:** Probe required native service/configuration at startup. Auto must select only a proven provider; unsupported, locked, unavailable, or headless states produce distinct diagnostics.
- **A04-T02 — Implement/prove macOS secure storage:** Add a Keychain-backed adapter or approved secure alternative behind the existing abstraction. Define item identity, update/delete, access scope, user interaction, concurrency, restart, and backup expectations.
- **A04-T03 — Implement/prove Linux interactive secure storage:** Add a Secret Service adapter with explicit D-Bus/session/locked-service behavior. Do not silently fall back when no graphical/session keyring exists.
- **A04-T04 — Design the headless secure provider:** Select and implement an explicit certificate, remote-vault, or externally supplied wrapping-key profile. It must work for service accounts without an interactive keyring and must fail closed if missing.
- **A04-T05 — Protect the ASP.NET Data Protection key ring:** Choose platform/profile-specific at-rest protection whose bootstrap does not depend on that ring. Keep file permissions and rotation separate from cryptographic protection.
- **A04-T06 — Retire the insecure file-vault production path:** Remove plaintext Base64 key generation from Auto/production, version legacy payloads, and provide an explicit development/test-only or migration disposition.
- **A04-T07 — Build one migration state machine:** Inventory legacy DPAPI, legacy Data Protection secret payloads, control-plane database passwords, and vault references. Stage destination, verify read, commit reference, retain source until checkpoint, then clean idempotently.
- **A04-T08 — Implement Windows-side DPAPI migration:** Decrypt DPAPI only on an authorized Windows host and export/re-encrypt through the selected portable provider. Include dry-run, backup, interruption, resume, and rollback.
- **A04-T09 — Add atomicity, locking, modes, versioning, and rotation:** Use A02 primitives for key/payload generations; verify restrictive modes; test concurrent startup, key rotation interruption, orphan cleanup, and old-generation retention.
- **A04-T10 — Prove redaction and recovery:** Seed sentinel secrets through success and every failure path; scan logs, exceptions, receipts, CI artifacts, migration reports, and backups for leakage.
- **A04-T11 — Issue Security Gate C2:** Require independent security review plus actual Windows/Linux/macOS/headless restart and migration evidence before platform composition continues.

## Exit

- Gate C2 is GO from architect, security reviewer, and runtime validator.
- Auto never selects unsupported or insecure persistence.
- Production key material is protected at rest and permission-hardened.
- Legacy Windows secret/control-plane data has a tested migration and rollback path.
