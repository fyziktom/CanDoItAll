# Scope Inventory

## In Scope

- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj`
- `repo://src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.MafAdapter/CanDoItAll.AgentFramework.Workflows.MafAdapter.csproj`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Hosting/CanDoItAll.AgentFramework.Hosting.csproj` for A2A hosting preview and dependency-floor decision checks.
- `repo://src/MAF/Tools/CanDoItAll.AgentFramework.Tooling/CanDoItAll.AgentFramework.Tooling.csproj` for dependency-floor warning checks only.
- Existing adapter source under `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/`.
- Existing workflow adapter source under `repo://src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.MafAdapter/`.
- Existing unit, integration, component, and Playwright tests that prove current behavior.
- Evidence documentation under `repo://docs/maf-1.13-update-evidence.md`.

## Out Of Scope

- New process direct runtime tools.
- New `/api/processes` routes.
- Foundry hosting adoption.
- Durable workflow adoption.
- DevUI adoption.
- FileMemory/FileAccess product feature exposure.
- Central Package Management introduction.
- Broad MAF runtime responsibility refactor.
- Memory provider architecture redesign.
- Provider model/credential policy redesign.

## Package Inventory

The package inventory source of truth for preparation is:

- `bundle://inputs/original-prep/data/package-update-matrix.json`
- `bundle://analysis/01-current-state.md`
- `bundle://checklists/maf-1.13-phase-checklists.xlsx`

Implementation must regenerate package evidence before edits because preview package availability may change.
