# Target Solution

## New Allowed Production Surface
A new contract-only project may be introduced, for example:

`src/CanDoItAll.Processes.Drivers.Abstractions`

Allowed contents:
- immutable request/response records
- permission mode enum
- capability scope enum/value model
- denial reason enum/value model
- audit fact record
- evidence reference record
- verification diagnostic record
- version/compatibility metadata

Denied contents:
- runtime selector
- registry
- DI extension
- manager command
- shell command execution
- Graph/Office connector integration
- workspace/storage/process mutation
- claim/transition/finalizer/retry ownership
- direct dependency on `CanDoItAll.Modules.*`, Infrastructure, AgentFramework, EF, UI, or storage

## Dependency Direction
- `CanDoItAll.Processes.Core` must not reference driver abstractions.
- Driver abstractions may reference `CanDoItAll.Processes.Core` and `CanDoItAll.Processes.Contracts` only if the dependency stays descriptor/read-model only.
- Process module may later consume driver abstractions through adapters, but not in this bundle unless explicitly required for tests.

## First Alpha Lane
The `.NET/Rust transcript verifier` remains a test-only rehearsal in this bundle. It may inspect transcript text fixtures and return diagnostics. It must not run commands or mutate any state.
