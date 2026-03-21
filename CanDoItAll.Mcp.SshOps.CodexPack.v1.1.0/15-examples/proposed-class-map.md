# Navržená mapa tříd a namespace

## 1. Shared foundation

### 1.1 `CanDoItAll.Mcp.Core`
- `McpToolEnvelope<T>`
  - společná wire-level response envelope
- `ToolError`
  - common error payload
- `ToolInvocationException`
  - common exception for deterministic tool failures
- `CorrelationIdFactory`
- `OperationIdFactory`
- `ServerInstanceIdentity`
- `ResourceMutationGate`
  - keyed nebo single-resource mutation lock
- `LogEntry`
- `LogReadResult`
- `RingLogBuffer`
- `FileLogStore`
- `SecretRedactor`
- `AsyncOperationState`
- `OperationRecordBase`
- `OperationRegistry<TRecord>`
- `OperationWaitEngine`
- `HttpProbeService`
- `TlsCertificateInspector`

### 1.2 `CanDoItAll.Mcp.LocalRuntime`
- `ManagedProcessStartInfo`
- `ManagedProcess`
- `ProcessStopResult`
- `IProcessCommandRunner`
- `ProcessCommandRunner`
- `IProcessTreeTerminator`
- `WindowsProcessTreeTerminator`
- `UnixProcessTreeTerminator`
- `ProcessTreeTerminator`
- `ProcessSupervisor`
- `ManagedProcessMarkers`
- `ManagedProcessRecord`
- `StaleProcessRegistry`

## 2. `CanDoItAll.Mcp.DotNetWatch` po refaktoru

### Zůstává lokální
- `SessionCoordinator`
- `AppRuntimeManager`
- `AppSession`
- `AppStartTemplate`
- `StartFailureDiagnoser`
- `DotNetWatchToolContracts`
- `DotNetWatchHealthAdapter`
- `ResolveTestRunner` logika

### Přestává být lokální
- response envelope
- log buffer
- file log store
- redaction
- server instance identity
- mutation gate
- process supervision
- stale process registry

## 3. `CanDoItAll.Mcp.SshOps`

### Lokální pouze pro SSH
- `TargetCatalog`
- `SecretResolver`
- `HostKeyVerifier`
- `RemotePathGuard`
- `CommandPolicy`
- `ISshTransport`
- `SshNetTransport`
- `RemoteJobRunner`
- `RevisionBackupService`
- `BundleApplyService`
- `DockerComposeService`
- `TraefikValidationService`
- `PostgresValidationService`
- `IpfsValidationService`

### Musí používat shared foundation
- response envelope
- error model
- IDs
- mutation gate
- log abstractions
- redaction
- operation primitives
- HTTP/TLS probe helpers

## 4. Boundary pravidlo

Když daný typ nese jednu z těchto vlastností, **nepatří do shared foundation**:

- je silně závislý na `dotnet watch`,
- je silně závislý na SSH / SFTP / host key modelu,
- obsahuje Docker / Traefik / PostgreSQL / IPFS business rules,
- pravděpodobně bude mít jen jednoho konzumenta.
