# Current development delta from the supplied bundle

## Anchors

| Item | Supplied bundle | Updated program |
|---|---|---|
| Prepared date | `2026-07-31` | `2026-08-08` |
| Development commit | `d44faef347be128eb85856a18c6fe253ce6fc1ee` | `62ea8ee0cc42c1c06da934d126a5c18f8237a89f` |
| Commit message | `Merge branch 'processes-refactor-3' into development` | `Merge branch 'maf-refactor' into development` |
| .NET SDK | `10.0.200` | `10.0.302` |
| Branch relation observed during preparation | — | `64` commits ahead, `0` behind old anchor |
| Primary target | Linux-first | Windows regression + Linux + macOS |

## What remains valid from the old bundle

- Re-anchor before implementation.
- Treat existing Windows behavior and encrypted data as compatibility requirements.
- Use fail-safe driver/capability selection.
- Keep trusted path, process ownership, tool governance, approvals, secrets, TLS, and rollback strict.
- Require actual-host CI and restart evidence.
- Use conditional architecture and secret-recovery subbundles.
- Keep execution artifacts redacted and source-code comments in English.

## What changed materially

### 1. The solution architecture is larger and more explicit

The current solution now contains:

- `CanDoItAll.Security.Abstractions`;
- multiple MAF LLM/runtime abstraction projects;
- dedicated `Processes.Contracts`, `Abstractions`, `Core`, `Builder`, `Application`, `Projections`, `Persistence`, `Runtime`, and `Templates`;
- `Processes.Drivers.Abstractions` and `Processes.Drivers.Standard`;
- a recent MAF refactor whose ADRs explicitly keep process semantics outside MAF.

The old bundle's generic process/platform treatment would now risk violating these boundaries.

### 2. The path problem is no longer just “replace Windows slashes”

Current implementations disagree:

- Infrastructure workspace/storage policies use native separator conversion that leaves backslash untouched on Unix.
- MAF `WorkspacePathPolicy` explicitly converts backslash to `/`.
- MAF runtime containment uses `OrdinalIgnoreCase` on every OS.
- The MAF external-target alias is shaped around Windows drive letters.
- Host-bound absolute paths are persisted in profiles and preferred applications.

The new plan therefore introduces a path taxonomy, field-specific legacy readers, host-bound record versioning, and root-specific filesystem semantics.

### 3. Secrets have changed from a DPAPI-only concern to a three-system migration

The current code has:

1. legacy Data Protection secret payloads;
2. control-plane database passwords protected through the same Data Protection ring;
3. new `ISecretVault` references and provider selection.

`Auto` chooses macOS/Linux providers that are currently unsupported, and the file vault stores its AES key beside ciphertext. The updated plan treats provider implementation, key-ring bootstrap, file modes, atomicity, rotation, legacy DPAPI migration, and rollback as one security program.

### 4. The central process host needs hardening, not replacement

`LocalWorkspaceProcessHost` already uses direct typed arguments and is broadly portable. The main problems are now:

- Windows-heavy environment policy;
- inconsistent executable resolution;
- unproven Unix process-tree termination;
- duplicate process runners and direct host construction;
- Manager ownership/discovery;
- Workbench PowerShell plan representation.

The updated runtime bundle preserves the good host foundation and eliminates duplicates.

### 5. Runtime nodes and Manager need an ownership-first refactor

Workbench remains Windows-only and PowerShell-centric, while Manager has WMI plus a weak Unix fallback. These changes are too broad and ownership-sensitive to mix with path/storage/secret migration, so they now live in the second bundle after Core C4.

### 6. CI is currently disabled

The discovered workflow is under `.github/workflows-disabled/ci.yml`. Its application gate was Windows-only; Ubuntu covered container policy, not application build/runtime, and macOS was absent. The updated core bundle restores an active three-platform gate before runtime work begins.

## Superseded old ordering

The old sequence introduced platform composition before the low-level path/filesystem contract and then interleaved filesystem, runtime, MCP, hosting, and CI in one implementation bundle.

The new sequence is:

```text
paths/config
  -> filesystem semantics
  -> storage/control-plane migration
  -> secrets/key migration
  -> composition/readiness
  -> headless hosting/CI (Core C4)
  -> re-anchor
  -> process primitives
  -> Workbench/Manager/MCP/plugins
  -> Processes capability adaptation
  -> final runtime E2E (R4)
```

## Retired assumptions

- Linux and macOS are not treated as one identical “Unix driver.”
- `OperatingSystem.IsWindows()` is not a sufficient filesystem case policy.
- A broad `IPlatformCapabilities`/`IPlatformService` is not the default architecture.
- The `ProcessDriverLayer.Platform` is not a container for generic OS services.
- Global npx cache discovery is not accepted as an authoritative production Playwright MCP source.
- A file vault whose key sits beside ciphertext is not accepted as production protection.
- Terminal availability is not required for headless execution.
