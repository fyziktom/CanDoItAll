# Installation

This directory is a compatibility snapshot. Do not copy it into `codex/skills` or another active skill root.

Canonical CanDoItAll skills are maintained in the sibling `CanDoItAll.SharedInfo` repository. From this repository root, install them with:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\codex\scripts\install-candoitall-skills.ps1 -SharedInfoRepoRoot ..\CanDoItAll.SharedInfo
```

See [Codex skills](../README.md) for ownership, prerequisites, optional public-skill synchronization, and validation.
