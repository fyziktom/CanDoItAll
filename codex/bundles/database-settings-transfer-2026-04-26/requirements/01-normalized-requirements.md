# Normalized Requirements

| ID | Requirement | Acceptance Signal |
| --- | --- | --- |
| R1 | Users can copy ProjectStructure MCP token/settings from one saved database profile into another from database management. | Modal lists source DBs and a ProjectStructure MCP token/settings checkbox; transfer populates target ProjectStructure profile/settings records without exposing token cleartext. |
| R2 | New database creation in UI asks whether basic settings should be transferred. | The creation flow opens or includes transfer controls with checkboxes before/after bootstrapping a new DB. |
| R3 | The transfer mechanism is generic and supports multiple settings/record groups. | Infrastructure exposes `IDatabaseTransferService` plus module-registered handlers; UI renders item descriptors rather than hard-coded copy logic. |
| R4 | Initial transfer items include ProjectStructure MCP token/settings, AI providers, AI agents, and Processes. | Each item is backed by a handler or explicit completion proof. |
| R5 | Transfer respects database profile isolation and runtime switching. | Service opens explicit source and target contexts via profile resolution, not the ambient active `AppDbContext`. |
| R6 | Secrets and tokens stay protected. | UI only shows labels/summaries/counts; copied encrypted payloads remain encrypted and are not logged. |
| R7 | Process transfer avoids runtime history. | Process handler copies definition/configuration records and excludes process runs, step runs, launch plans, and outbox records. |
| R8 | The implementation is testable and maintainable. | New code is split into small model/service/handler files with targeted tests or at least build plus focused validation. |
