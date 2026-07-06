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

The `/memory` page can create and edit common profile metadata and demo/mock profiles. Transport-specific extension keys for HTTP, MCP, and native-remote providers must currently be supplied by seeded/imported profile data or a small admin path that writes `MemoryProviderProfile` through `IMemoryProviderProfileStore`.

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
| `host.candoitall.memory.http.apiKey` | No | Secret value; do not log it. |
| `host.candoitall.memory.http.authHeaderName` | No | Defaults to `Authorization`. |
| `host.candoitall.memory.http.authScheme` | No | Defaults to `Bearer`. |
| `host.candoitall.memory.http.timeoutMilliseconds` | No | Positive integer override. |
| `host.candoitall.memory.http.maxRetryAttempts` | No | Non-negative integer override. |

The current HTTP driver dispatches context queries and reads provider health. If the profile advertises async operation status, feedback, source ingestion, or event polling without a matching driver implementation, workers will record typed driver-unavailable diagnostics.

Expected query response shape is `HttpMemoryProviderResponse`, carrying either a `MemoryContextPack`, a `MemoryOperationAccepted`, a provider error, or an unsupported-capability response.

## MCP Provider

The MCP driver is available through `AddMcpMemoryProviderDriver`, but the base appsettings file does not enable it by default. Host-specific composition must call the extension before MCP profiles can dispatch.

Create an enabled profile with `DriverKind` set to `Mcp` and include:

| Extension key | Required | Notes |
| --- | --- | --- |
| `host.candoitall.memory.mcp.serverKey` | Yes | Stable MCP server key. |
| `host.candoitall.memory.mcp.descriptorKind` | No | `remote-http` by default; `internal-hosted` is also supported. |
| `host.candoitall.memory.mcp.remoteEndpoint` | Required for `remote-http` | Absolute URI. |
| `host.candoitall.memory.mcp.implementationKey` | Required for `internal-hosted` | Internal hosted implementation key. |
| `host.candoitall.memory.mcp.displayName` | No | Defaults to profile display name. |
| `host.candoitall.memory.mcp.description` | No | Defaults to a generic MCP provider description. |
| `host.candoitall.memory.mcp.tools.contextQuery` | Capability-driven | Tool used for context query. |
| `host.candoitall.memory.mcp.tools.ingestion` | Capability-driven | Tool used for snapshot ingestion. |
| `host.candoitall.memory.mcp.tools.sourceRequest` | Capability-driven | Tool used for provider-requested sources. |
| `host.candoitall.memory.mcp.tools.feedback` | Capability-driven | Tool used for immediate or delayed feedback. |
| `host.candoitall.memory.mcp.tools.eventPoll` | Capability-driven | Tool used for host-poll events. |
| `host.candoitall.memory.mcp.tools.operationStatus` | Capability-driven | Tool used for async operation status. |

Declare only capabilities with a configured tool. Missing tool names are reported as unsupported capability by the MCP driver.

## Native Remote Provider

Native Cognitive Memory is an optional service-owned provider. Build and run it from:

```powershell
C:\repositories\CanDoItAll.CognitiveMemory
```

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
| `native.cognitiveMemory.remote.apiKey` | No | Secret value; do not log it. |
| `native.cognitiveMemory.remote.authHeaderName` | No | Defaults to `Authorization`. |
| `native.cognitiveMemory.remote.authScheme` | No | Defaults to `Bearer`. |
| `native.cognitiveMemory.remote.timeoutMilliseconds` | No | Positive integer override. |
| `native.cognitiveMemory.remote.maxRetryAttempts` | No | Non-negative integer override. |

The native remote driver adapts these keys into the generic HTTP driver. Native service startup, native DB migrations, Qdrant projection, model execution, and advanced native UI remain owned by the native service repository.

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
