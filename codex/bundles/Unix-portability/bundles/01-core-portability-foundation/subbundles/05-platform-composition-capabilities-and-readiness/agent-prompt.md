# Agent prompt — A05 Platform composition, capabilities, and readiness

You are the senior C# architect and implementation agent for **CanDoItAll Core Portability Foundation**.

## Objective

Wire the proven path/filesystem/storage/security implementations through narrow composition and truthful capability diagnostics.

## Required reading

1. `../../../../CODEX-EXECUTION-CONTRACT.md`
2. `../../README.md`
3. this subbundle `README.md`, `tasks.md`, `validation.md`, and `exit-criteria.md`
4. `../../requirements/requirements.json`
5. `../../analysis/01-prepared-findings.md`
6. `../../inventories/source-reference-manifest.json`
7. relevant ADRs and prior gate/session handoff

## Execution instructions

- Work only on `A05`.
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

- `{{REPO_ROOT}}/src/App/CanDoItAll.Web/Program.cs`
- `{{REPO_ROOT}}/src/Foundation/CanDoItAll.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs`
- `{{REPO_ROOT}}/src/Modules/CanDoItAll.Modules.Security/SecurityModuleServiceCollectionExtensions.cs`
- `{{REPO_ROOT}}/codex/bundles/MAF-Refactor/adrs/ADR-007-process-semantics-owned-by-processes.md`

## Tasks

- **A05-T01 — Define narrow platform facts and adapters:** Keep common code on portable .NET APIs. Add purpose-owned contracts only where behavior genuinely differs: root defaults, filesystem semantics, key/vault backend, native permission hardening, and optional capability probes.
- **A05-T02 — Select implementations at composition:** Register exactly one mandatory implementation per profile and zero-or-one optional adapters. Avoid conditional compilation unless a native reference cannot be isolated otherwise.
- **A05-T03 — Create capability/readiness descriptors:** Report availability, reason, remediation, support level, dependency version, and execution boundary without exposing secrets or full sensitive paths.
- **A05-T04 — Fail fast for mandatory security/path defects:** The host must not start in a production profile with an unsupported secret provider, unusable control-plane root, insecure key permissions, or ambiguous migration.
- **A05-T05 — Degrade optional features independently:** Desktop open, terminal presentation, native process discovery, FileTools, and other runtime capabilities can be unavailable without blocking headless core startup.
- **A05-T06 — Add architecture enforcement:** Add dependency/scan tests that prevent a broad IPlatformService, OS branching in domain/process semantics, and reverse MAF-to-product ownership.
- **A05-T07 — Prove profile matrix:** Test Windows interactive, Linux headless, Linux interactive-keyring, macOS interactive, macOS headless/service, and explicit test profiles.
- **A05-T08 — Issue composition gate C3a:** Require consistent capability UI/API/readiness snapshots and no misleading support claims.

## Exit

- Mandatory providers are selected truthfully and optional capabilities degrade independently.
- No giant platform abstraction or process-semantic leakage was introduced.
- All target profile composition tests pass.
- Gate C3a is GO.
