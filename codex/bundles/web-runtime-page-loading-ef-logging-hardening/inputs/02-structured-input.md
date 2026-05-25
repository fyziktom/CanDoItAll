# Structured Input

| ID | User concern | Desired behavior | Owning subbundle |
| --- | --- | --- | --- |
| `REQ-PROC-001` | Processes page loads too slowly. | Processes workspace defers runtime, analytics, party, and template-dependent data until the related section needs it. | `SB02` |
| `REQ-PROJ-001` | Project-structure node creation is slow to appear on canvas. | Creating a node updates the in-memory canvas surface after persistence instead of forcing a full surface reload. | `SB03` |
| `REQ-WF-001` | Workflows page load is very slow and appears to reload templates repeatedly. | Workflows page initialization stops eagerly seeding/loading the component/template catalog and loads it only for tabs/actions that require it. | `SB04` |
| `REQ-EF-001` | EF console output is noisy and resource-heavy. | EF console command/infrastructure logging is disabled by default and controlled by a strongly typed config option. | `SB05` |
