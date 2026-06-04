# Contract Extraction Principles

- Extract only stable, implementation-neutral contracts.
- Prefer records that carry IDs, names, source/correlation metadata, and small policy snapshots.
- Do not extract EF entities.
- Do not extract Razor view models.
- Do not create contracts that depend on AgentFramework Core/Models unless clearly marked as temporary and internal to the module.
- Any new contract project must have architecture tests forbidding references to:
  - `CanDoItAll.AgentFramework.Maf`
  - `CanDoItAll.Modules.Processes`
  - `CanDoItAll.Modules.Workbench`
  - `CanDoItAll.Modules.AgentFramework`
  - Razor/component packages
  - EF Core
