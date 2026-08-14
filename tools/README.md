# Repository Tools

Repository-specific engineering tools are grouped by purpose:

| Area | Responsibility |
|---|---|
| `App` | Local development manager |
| `dev` | PostgreSQL preparation, Tailwind watch, plugin packaging, and bounded development resets |
| `install` | Installed web app publishing and its isolated PostgreSQL setup |
| `Diagnostics` | Focused runtime and provider probes |
| `ollama` | Local Ollama model and probe support |
| `prompt_library` | Prompt component-library generation |
| `Seeding` | Maintained scenario seeding |
| `Validation` | Documentation, Docker, and deployment-artifact validation |

Run tools from the repository root unless their README states otherwise. Mutating tools
must validate targets, fail explicitly, and support `-WhatIf` where practical.

Compiled-tool category directories use PascalCase to match solution navigation and
project paths. Script-only categories use lower-case names.

The canonical Windows web app entry point is:

```powershell
& .\tools\install\Install-CanDoItAllWebApp.ps1 -StartAfterInstall
```

It invokes `tools/install/Install-CanDoItAllWebAppDatabase.ps1` unless
`-SkipDatabaseSetup` is explicitly supplied for an installation that already has a valid
managed manifest and current-user protected credential. The database script uses a
working Linux Docker engine when one is available and otherwise installs the pinned EDB
Windows x64 binaries.
Docker mode owns one labeled container and one labeled named volume; it is not a Compose
project. Neither installed-database path uses the repository's development `compose.yaml`.

`tools/Install-CanDoItAllWebApp.ps1` is retained only as a compatibility wrapper for the
former script location.

Framework-dependent Unix artifacts use `tools/install/unix/install-candoitall-web.sh`.
It installs immutable releases and switches a validated release-id state file without
elevation, database mutation, provider selection, or root-policy duplication. The same
folder contains the stable launcher, idempotent rollback entry point, and systemd/launchd
templates. Start with `docs/operations/installing-instances.md`, then use
`docs/operations/headless-web-host.md` for the detailed service lifecycle.

Local development uses sibling `CanDoItAll.Components` and `CanDoItAll.FileTools` project
references by default. Their roots can be overridden with
`CanDoItAllComponentsRepositoryRoot` and `CanDoItAllFileToolsRepositoryRoot`. Hosted CI,
Docker, and release validation must select the reproducible package graph explicitly with
`UseLocalCanDoItAllLibraries=false`.

The runtime portability gate uses `tools/Validation/RuntimePortabilityCatalog.json` as its
versioned class/FQN/count contract. Create the one Release build and durable identity stamp
with `Test-RuntimePortability.ps1 -BuildOnly`; subsequent `-SkipBuild` runs verify the
repository commit, source fingerprint, dependency mode and anchors, SDK, catalog, and
assembly hashes before starting any test process. Use `-SelfTest` for the runner's bounded
negative fixtures.

Validate both installer entry points, the generated launcher, and non-mutating previews
with:

```powershell
& .\tools\install\tests\Test-CanDoItAllWebAppInstallScripts.ps1
```
