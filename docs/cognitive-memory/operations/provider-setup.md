# Generic Memory Provider Setup

The base CanDoItAll host starts with no memory provider enabled. This is intentional. Provider-backed memory work requires both:

1. a registered driver; and
2. an enabled provider profile whose manifest declares the requested capability.

Enabling a driver in `appsettings.json` does not create a provider profile. Creating a provider profile does not register a driver. Both must be present before dispatch is allowed.

## Zero-Provider Startup

The default base configuration is:

```json
{
  "Memory": {
    "Providers": {
      "DeterministicMock": {
        "Enabled": false
      },
      "Http": {
        "Enabled": false
      },
      "NativeRemote": {
        "Enabled": false
      },
      "Mcp": {
        "Enabled": false
      }
    }
  }
}
```

In this mode:

- `/memory` renders provider management and ledger surfaces.
- MAF tools, workflow execution, context contribution, and UI actions return typed no-provider or capability-unavailable results.
- The host does not call native Cognitive Memory, Qdrant, OpenAI, HTTP providers, MCP providers, or mock providers.
- Operation ledgers record the failed selection/dispatch state where a memory operation was attempted.

## Provider Profiles

Profiles are stored in the generic memory provider profile store and surfaced through `/memory`. A profile contains:

- `InstanceId`: stable provider instance id, such as `provider.business-memory`.
- `DisplayName`: operator-facing name.
- `DriverKind`: `Http`, `Mcp`, `NativeRemote`, or `Mock`.
- `IsEnabled`: selection gate.
- `HealthState`: UI and selection signal.
- `WorkspaceScope`: current scope metadata.
- `DefaultPolicy`: fallback behavior, defaulting to `DenyImplicitFallback`.
- `Manifest`: protocol version, provider kind, supported capabilities, UI surfaces, limits, and extension data.

The `/memory` provider editor supports HTTP and native-remote transport fields plus remote-HTTP MCP fields. Preserved vendor-specific extensions outside those managed fields still require seed, import, or admin tooling that writes `MemoryProviderProfile` through `IMemoryProviderProfileStore`.

Supported capability ids are:

| Capability | Meaning |
| --- | --- |
| `context.query.sync` | Synchronous context-pack query. |
| `context.query.async` | Accepted async context query. |
| `ingestion.snapshot` | Source snapshot ingestion. |
| `ingestion.provider-requested-source` | Provider-requested source capture. |
| `feedback.immediate` | Immediate feedback delivery. |
| `feedback.delayed` | Delayed feedback delivery by worker. |
| `events.provider-push` | Provider-pushed event acknowledgements. |
| `events.host-poll` | Host polling for provider events. |
| `operations.status` | Async operation status polling. |
| `ui.rcl` | Provider Razor component surface. |
| `ui.iframe` | Provider iframe or external URL surface. |

Do not declare capabilities the registered driver cannot support. The runtime returns typed unsupported-operation or no-driver diagnostics instead of silently falling back.

## Deterministic Mock Provider

The mock driver is for tests and development proof only. Enable it explicitly:

```powershell
$env:Memory__Providers__DeterministicMock__Enabled = "true"
```

or:

```json
{
  "Memory": {
    "Providers": {
      "DeterministicMock": {
        "Enabled": true
      }
    }
  }
}
```

Then create an enabled provider profile with:

- `DriverKind`: `Mock`
- `ProviderKind`: `memory.mock`
- one or more supported generic capabilities

Do not use the mock driver as a production fallback. Tests that need it must enable it explicitly, as the Playwright memory provider host does.

## HTTP Provider

Enable the HTTP driver:

```json
{
  "Memory": {
    "Providers": {
      "Http": {
        "Enabled": true,
        "ClientName": "CanDoItAll.Memory.Http",
        "DefaultTimeout": "00:00:30",
        "MaxRetryAttempts": 0
      }
    }
  }
}
```

Create an enabled profile with `DriverKind` set to `Http` and include the required extension:

| Extension key | Required | Notes |
| --- | --- | --- |
| `host.candoitall.memory.http.baseUrl` | Yes | Absolute HTTP(S) base URL. |
| `host.candoitall.memory.http.queryPath` | No | Defaults to `/memory/query`. |
| `host.candoitall.memory.http.healthPath` | No | Defaults to `/memory/health`. |
| `host.candoitall.memory.http.apiKeyEnvironmentVariable` | No | Environment-variable name containing the secret; the secret is never stored in the profile. |
| `host.candoitall.memory.http.authHeaderName` | No | Defaults to `Authorization`. |
| `host.candoitall.memory.http.authScheme` | No | Defaults to `Bearer`. |
| `host.candoitall.memory.http.timeoutMilliseconds` | No | Positive integer override. |
| `host.candoitall.memory.http.maxRetryAttempts` | No | Non-negative integer override. |

The current HTTP driver dispatches synchronous context queries and reads provider health. It rejects accepted asynchronous responses because HTTP profiles do not have a status-poll driver. Profiles cannot advertise feedback, source ingestion, event polling, or asynchronous query support through this driver.

Expected query response shape is `HttpMemoryProviderResponse`, carrying a `MemoryContextPack`, provider error, or unsupported-capability response. `MemoryOperationAccepted` remains in the versioned wire contract for transports that implement status polling, but is rejected by this driver.

## MCP Provider

The MCP driver is registered by composition only when `Memory:Providers:Mcp:Enabled` is true. It uses the official MCP HTTP client and supports remote HTTP servers only.

Create an enabled profile with `DriverKind` set to `Mcp` and include:

| Extension key | Required | Notes |
| --- | --- | --- |
| `host.candoitall.memory.mcp.serverKey` | Yes | Stable MCP server key. |
| `host.candoitall.memory.mcp.descriptorKind` | No | `remote-http` by default. `internal-hosted` is rejected because it has no executable runtime path. |
| `host.candoitall.memory.mcp.remoteEndpoint` | Required for `remote-http` | Absolute URI. |
| `host.candoitall.memory.mcp.displayName` | No | Defaults to profile display name. |
| `host.candoitall.memory.mcp.description` | No | Defaults to a generic MCP provider description. |
| `host.candoitall.memory.mcp.authHeaderName` | No | HTTP header name; defaults to `Authorization` when a binding is present. |
| `host.candoitall.memory.mcp.authHeaderEnvironmentVariable` | No | Environment-variable name containing the complete header value, such as `Bearer <token>`. |
| `host.candoitall.memory.mcp.tools.contextQuery` | Capability-driven | Tool used for context query. |
| `host.candoitall.memory.mcp.tools.operationStatus` | Capability-driven | Tool used for async operation status. |

The shipped MCP adapter supports context query and, when configured, operation-status polling. Ingestion, provider-source requests, feedback, and event-poll tool keys are rejected during profile decoding. Declare only capabilities with an implemented configured tool.

## Native Remote Provider

Native Cognitive Memory is an optional service-owned provider. Build, configure,
and run it from the separately cloned
[CanDoItAll.CognitiveMemory repository](https://github.com/fyziktom/CanDoItAll.CognitiveMemory).
The base-host adapter is isolated in
`src/Memory/Drivers/CanDoItAll.Memory.Drivers.CognitiveMemory`; it depends on the
generic Memory contracts and HTTP transport, not on native implementation source.

Enable the native remote driver in the base host:

```json
{
  "Memory": {
    "Providers": {
      "NativeRemote": {
        "Enabled": true,
        "ClientName": "CanDoItAll.Memory.NativeRemote",
        "DefaultTimeout": "00:00:30",
        "MaxRetryAttempts": 0
      }
    }
  }
}
```

Create an enabled profile with `DriverKind` set to `NativeRemote` and include:

| Extension key | Required | Notes |
| --- | --- | --- |
| `native.cognitiveMemory.remote.serviceBaseUrl` | Yes | Absolute HTTP(S) base URL of the native service. |
| `native.cognitiveMemory.remote.queryPath` | No | Defaults to `/memory/query`. |
| `native.cognitiveMemory.remote.healthPath` | No | Defaults to `/memory/health`. |
| `native.cognitiveMemory.remote.apiKeyEnvironmentVariable` | No | Environment-variable name containing the secret; the secret is never stored in the profile. |
| `native.cognitiveMemory.remote.authHeaderName` | No | Defaults to `Authorization`. |
| `native.cognitiveMemory.remote.authScheme` | No | Defaults to `Bearer`. |
| `native.cognitiveMemory.remote.timeoutMilliseconds` | No | Positive integer override. |
| `native.cognitiveMemory.remote.maxRetryAttempts` | No | Non-negative integer override. |

The native remote driver adapts these keys into the generic synchronous HTTP driver. Native service startup, database migrations, projection, model execution, and advanced native UI remain owned by that external repository.

## UI Surface Setup

Generic provider UI surfaces are optional. For iframe or external URL surfaces, the configured URL must be HTTPS or loopback HTTP and must not include user info. The current generic UI uses `provider.vendor.uiUrl` for iframe URL projection.

For RCL surfaces, the provider surface must declare a component key and a host module must register a matching `MemoryProviderUiSurfaceComponentRegistration`. Missing or unsafe UI surfaces are rendered as unavailable diagnostics, not blank frames.

## Rollback

To roll back provider-backed memory dispatch:

1. Disable the provider profile or remove its assignment.
2. Disable the corresponding driver configuration.
3. Restart the host if driver registration changed.
4. Verify `/memory` renders zero-provider or disabled-provider state.

Rollback does not require dropping generic memory ledgers or legacy main database `CognitiveMemory_*` tables.
