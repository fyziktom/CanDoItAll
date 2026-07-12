# Installation Guide

## Manual installation

1. Copy `codex/skills/csharp-*` directories into `<repo>/codex/skills/`.
2. Copy `codex/skills/_csharp-architecture-shared/` into `<repo>/codex/skills/`.
3. Copy `codex/skills/bundles/candoitall-csharp-architecture-bundle-guard/` into `<repo>/codex/skills/bundles/`.
4. Read `integration/candoitall-bundle-system-integration.md`.
5. Apply the append snippets from `integration/append-to-*.md` to the matching existing bundle skills if you want the architecture gate to be automatically invoked.
6. Keep `examples/`, `templates/`, and `checklists/` either in the skill package or copied into a shared bundle-template folder.

## Optional script installation

Run the installer from this package root:

```bash
python scripts/install_csharp_architecture_skills.py --repo-root C:/repositories/CanDoItAll
```

Use `--dry-run` first to print the planned copies without changing files:

```bash
python scripts/install_csharp_architecture_skills.py --repo-root C:/repositories/CanDoItAll --dry-run
```

## Activation triggers for Codex

Tell Codex to use these skills whenever the request includes any of these signals:

- refactor large class
- split partial class
- isolate provider
- add new tool
- add new memory provider
- add new process driver
- add workflow executor
- add runtime capability
- create builder
- create factory
- fix cyclic reference
- improve testability
- prepare architecture bundle
- review architecture before implementation

## Minimal prompt addition

Add this line to bundle preparation prompts:

```text
If the bundle touches C# architecture, large-class refactoring, tool/provider/memory/process/runtime composition, or project references, load `candoitall-csharp-architecture-bundle-guard` and require a C# Architecture Gate before implementation.
```
