# SB03 Provider Snapshot Lifecycle Matrix

| Event | Canonical state before | Work | Published state | Consumer behavior |
| --- | --- | --- | --- | --- |
| Process construction | None | Subscribe to database-switch notification | `NotReady` | Typed fail-closed exception |
| Current-profile readiness | `NotReady` | One scoped, no-tracking DB load; map and freeze all profiles | `Ready` for exact profile ID/fingerprint/generation | O(1) list/get/capture |
| Caller cancels initialization | `NotReady` | Loader observes cancellation | Remains `NotReady` | No partial data |
| Database profile changes during load | Loading old identity | Notification advances publication fence | `NotReady` for new identity | Old result cannot publish |
| Provider save commits | `Ready` | Observer reloads committed DB row and freezes it | `Ready` with one entry replaced | Selected provider fingerprint changes |
| Unrelated provider save commits | `Ready` | Replace only unrelated entry | `Ready` with selected lease unchanged | Selected blueprint remains reusable |
| Provider delete commits | `Ready` | Remove provider ID | `Ready` without provider | Missing selected provider is typed stale |
| Committed row cannot be projected | `Ready` | Observer captures mapping/load exception | Empty `Faulted` state with cause | All provider reads fail closed |
| Host disposal | Any | Unsubscribe switch event and dispose rebuild gate | No further service use | No notification leak |

## Retained data audit

| Resource | Retained? | Reason |
| --- | --- | --- |
| Normalized provider runtime configuration | Yes | Immutable execution descriptor |
| Provider configuration fingerprint | Yes | Typed use-time invalidation |
| Secret reference identity | Yes | Non-secret configuration identity |
| Secret value/current authorization | No | Resolved per dispatch |
| `DbContext` | No | Scoped to loader call and disposed before publication |
| Provider client / MAF agent / tool / MCP session | No | Created per execution |
| Chat session / approval / current context contribution | No | Request-specific/live state |
| File catalog provider shadow | No | Not canonical for integrated runtime |
