# Memory

The Memory area provides a provider-neutral operation model, source ingestion, provider
drivers, and persistence. Providers and workers are disabled until explicitly configured.

| Project | Responsibility |
|---|---|
| [Abstractions](CanDoItAll.Memory.Abstractions/README.md) | Identifiers, capabilities, operations, ledgers, and result contracts |
| [Application](CanDoItAll.Memory.Application/README.md) | Provider registry, operation handling, workers, and source ingestion |
| [Source gateway abstractions](CanDoItAll.Memory.SourceGateway.Abstractions/README.md) | Secure paged source snapshot contracts |
| [HTTP contracts](CanDoItAll.Memory.Http.Contracts/README.md) | Typed HTTP provider request and response contracts |
| [HTTP driver](CanDoItAll.Memory.Http/README.md) | HTTP transport, validation, and response mapping |
| [MCP driver](CanDoItAll.Memory.Mcp/README.md) | MCP-backed provider transport |
| [Mock driver](CanDoItAll.Memory.Mock/README.md) | Deterministic test provider |
| [Persistence](CanDoItAll.Memory.Persistence/README.md) | EF Core ledgers, profiles, leases, and retention |
| [Drivers](Drivers/README.md) | External provider adapters |

The application layer owns operation semantics. Drivers translate provider protocols and
must not redefine ledger, retention, or authorization rules.
