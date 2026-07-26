# C# Boundary Map

| Concern | Contract owner | Implementation owner | Composition |
| --- | --- | --- | --- |
| Remote package import | AgentFramework Models/Core contract | Persistence archive inspector/import service | Modules.AgentFramework/Hosting |
| External agent identity | AgentFramework Models/Core contract | Core catalog provisioning service using workspace store | Modules.AgentFramework/Hosting |
| Portable schema request/result | Web public DTO plus Models portable contract | Core schema validator and execution evidence assembler | Agent hosting/runtime |
| Workflow provenance lookup | Workflows Abstractions | Workflows Core catalog service/store | Modules.AgentFramework |
| Workflow launch idempotency | existing Models/Workflows Abstractions | existing Core launch service and persistent store | Modules.AgentFramework |
| Agent recruiting evidence | AgentFramework Models/Core contract | cohesive Core service backed by workspace persistence | Modules.AgentFramework/Web |
| HTTP/OpenAPI DTOs | Web | endpoint mapping only | Web |

## Old Responsibilities To Remove Or Leave

- Leave internal `.NET Type` structured-output helpers for trusted in-process calls; public
  endpoints convert from a portable DTO.
- Leave server-local `/api/agents/import` only if explicitly documented as local/admin
  compatibility; remote flows use the multipart route.
- Leave CRM-HR recruitment interview records as hiring-application data; do not pretend
  they are canonical agent evaluation evidence.
- Move no product implementation into SharedInfo.

## Temporary Bridges

- Existing workspace service facade may expose new methods while delegating to focused
  services. Any bridge must have a direct focused-service test and cannot own new logic.
