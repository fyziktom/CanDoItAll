# Target Solution

## Generic Transfer Layer

- Add transfer contracts in `CanDoItAll.Infrastructure.ControlPlane`.
- `IDatabaseTransferService` owns profile resolution, explicit source/target `AppDbContext` creation, handler discovery, preview, and execution.
- `IDatabaseTransferHandler` owns one transfer item and returns descriptor, preview, and result data.
- Handlers receive a context object with resolved source/target profiles and explicit source/target contexts.

## Module Handlers

- Workspace registers handlers for ProjectStructure MCP token/settings and AI providers.
- AgentFramework registers the AI agents handler so Workspace does not reference AgentFramework.
- Processes registers the process definitions handler so Workspace does not reference Processes.

## UI

- Database-management UI opens a `Dialog` with:
  - target database summary
  - source database selector
  - checkbox list of transfer items
  - preview counts/warnings
  - transfer result messages
- New database creation asks for transfer by opening the same transfer dialog after schema bootstrap, with the previous/current database preselected where possible.
- Main layout managed-SQLite creation uses the same transfer abstraction or clearly routes the user into the same transfer prompt.

## Security Boundary

- Transfer UI never renders clear token or API key values.
- Existing encrypted payload columns are copied as protected payloads.
- Decryption remains in the owning services that already perform admin/authorization behavior.
