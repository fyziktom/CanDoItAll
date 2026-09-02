# SB07 — Refresh Source Pins And Operations Docs

**Status:** Completed locally — exact signed pins, both asset guards, three CI tests, documentation and Docker policy pass
**Outcome:** Clean CI/Docker source mode is reproducible and Podman docs are current  
**Proof tier:** Standard + Behavioral

## CI source pins

Update CanDoItAll CI constants/checkouts to exact candidate commits:

- Components integration/version commit,
- FileTools integration/version commit.

Do not pin moving branches.

Add an explicit post-checkout assertion that the Components source contains:

```text
src/CanDoItAll.Components.BaseLib/wwwroot/css/material-symbols.css
src/CanDoItAll.Components.BaseLib/wwwroot/css/output.css
```

Use cross-platform `pwsh`. The main CI should not rely on an untracked developer-generated file.

Update tests that assert CI workflow structure.

## Docker/source contexts

Confirm the Docker build copies final sibling sources and receives the committed BaseLib CSS.
Do not add Node to the application Dockerfile merely to compensate for a missing owned source
asset.

Run/update Docker validation tests only when needed.

## Podman/macOS documentation

Create:

```text
docs/operations/podman-macos-development.md
```

Migrate still-valid instructions from the original root document and reconcile them with current:

- `docs/operations/containers.md`,
- `docs/operations/installing-instances.md`,
- `docs/README.md`,
- current `.env.example`, Compose files, secrets, ports, and source layout.

Required corrections:

- sibling Components and FileTools are required in default source mode,
- package mode must be explicit and consistent,
- destructive commands are clearly labeled,
- credentials are development-only examples and match current files,
- machine/provider commands are not claimed as executed when macOS proof is unavailable,
- no contradictory `brew trust/untrust` statement,
- root `PODMAN.md` is removed or becomes a short pointer only if repository policy requires it.

## Local package-feed setup

Document the exact temporary local-feed layout used in SB08. Do not add it permanently to
`NuGet.config`.

## Acceptance

- CI pins exact final sibling commits,
- CI tests assert those pins and source mode,
- source asset assertion exists,
- docs validation passes,
- Podman docs no longer contradict current source mode,
- Docker validation remains green.

## Progression gate

A clean CI checkout plan can build without hidden local assets.

## Reopen triggers

- sibling candidate commit changes,
- current Compose/security docs change,
- BaseLib generated asset policy changes.
