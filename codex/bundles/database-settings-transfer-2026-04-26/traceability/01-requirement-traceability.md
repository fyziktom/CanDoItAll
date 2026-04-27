# Requirement Traceability

| Requirement | Owning subbundle | Source input | Planned proof |
| --- | --- | --- | --- |
| R1 ProjectStructure MCP token/settings transfer | `02-02-workspace-transfer-handlers`, `03-03-database-management-ui` | User request sentence about copying token between databases | Handler test/build plus modal preview/transfer proof |
| R2 New database creation asks for basic settings transfer | `03-03-database-management-ui` | User request sentence "If we are creating new db in ui..." | Browser proof of creation flow prompting transfer options |
| R3 Generic transfer system | `01-01-transfer-foundation` | User request sentence "this process ... generic system" | New Infrastructure abstractions and module handler registrations |
| R4 Initial checkbox items | `02-02-workspace-transfer-handlers`, `03-03-database-management-ui` | User examples: project structure MCP token, AI providers, AI agents, processes | UI checkbox list and registered descriptors |
| R5 Runtime DB/profile isolation | `01-01-transfer-foundation` | User problem after switching DB at runtime | Source/target context creation through profile factory |
| R6 Secret/token protection | `02-02-workspace-transfer-handlers` | Token/right setup concern | No cleartext token rendering or logs; encrypted payload copy only |
| R7 Process definitions only | `02-02-workspace-transfer-handlers` | "processes" transfer item | Process handler excludes runtime tables |
| R8 Maintainability/testability | all subbundles | User requested best-practice refactoring | Split files, isolated services, validation proof |

## Raw Note Closure Plan

| Raw note | Normalized requirement | Planned status gate |
| --- | --- | --- |
| ProjectStructure MCP setup is difficult because rights/token setup are DB-scoped | R1, R5, R6 | Closed when token/settings can be copied without exposing token |
| Token disappears after runtime DB switch | R1, R5 | Closed when target DB can receive copied ProjectStructure profile/settings |
| DB management should show modal with source DB list | R1, R2, R3 | Closed by UI browser proof |
| New DB creation should ask to transfer basic settings | R2 | Closed by creation-flow proof |
| Checkboxes should include ProjectStructure token, AI providers, AI agents, processes | R4 | Closed by descriptor/handler list in UI |
| Transfer must be generic for different settings/records | R3, R8 | Closed by generic service and handlers |
