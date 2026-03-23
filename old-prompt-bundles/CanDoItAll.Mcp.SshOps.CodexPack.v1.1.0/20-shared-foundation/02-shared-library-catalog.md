# Shared library catalog

## Přehled

Doporučené shared knihovny pro MCP servery v solution `CanDoItAll` jsou dvě:

1. `CanDoItAll.Mcp.Core`
2. `CanDoItAll.Mcp.LocalRuntime`

Třetí vrstva `CanDoItAll.Mcp.RemoteOps` je zatím jen rezervovaný budoucí kandidát a **nemá** se vytvářet v první vlně.

---

## 1. `CanDoItAll.Mcp.Core`

### Účel
Obsahuje stabilní cross-server primitives, které mají nebo velmi pravděpodobně budou mít více MCP serverů společné.

### Co do ní patří

#### 1. Contracts
- `McpToolEnvelope<T>`
- `ToolError`
- `ToolInvocationException`
- případně `ToolDiagnostic`

#### 2. Identity
- `CorrelationIdFactory`
- `OperationIdFactory`
- `ServerInstanceIdentity`

#### 3. Concurrency
- `ResourceMutationGate`
- případně keyed varianty locků

#### 4. Observability
- `LogEntry`
- `LogReadResult`
- `RingLogBuffer`
- `FileLogStore`
- `SecretRedactor`
- redaction rules/options

#### 5. Operations
- `AsyncOperationState`
- `OperationRecordBase`
- `OperationRegistry<TRecord>`
- `OperationWaitEngine`

#### 6. Net validation helpers
- `HttpProbeService`
- `TlsCertificateInspector`

### Co do ní nepatří
- MCP tool attributes a konkrétní tool class implementation,
- dotnet-specific app lifecycle model,
- SSH transport,
- Docker / Traefik / PostgreSQL / IPFS doménová logika,
- web application infrastructure z jiných vrstev solution.

---

## 2. `CanDoItAll.Mcp.LocalRuntime`

### Účel
Obsahuje vše, co souvisí s lokálně spawnovanými child processy.

### Co do ní patří
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

### Primární konzumenti
- `CanDoItAll.Mcp.DotNetWatch`
- budoucí lokální MCP servery, které budou řídit child procesy

### Co do ní nepatří
- SSH,
- remote file transfer,
- Docker/Compose přes SSH,
- remote job runner,
- Traefik / PostgreSQL / IPFS validace.

---

## 3. Rezervovaný budoucí kandidát: `CanDoItAll.Mcp.RemoteOps`

### Teď nevytvářet
Je lákavé hned udělat i třetí shared projekt pro remote servery, ale teď by to bylo předčasné.

### Proč ne teď
Zatím existuje jen jeden plánovaný remote server.  
Dokud nebude druhý reálný consumer, hrozí, že by se do shared vrstvy vytlačila příliš mnoho SSH-specific logiky.

### Co se může později přesunout
- `ISshTransport`
- `TargetCatalog`
- `RemoteJobRunner`
- revision backup / remote bundle primitives

Až tehdy, když se objeví druhý remote MCP server se stejnými potřebami.

---

## 4. Přesná doporučená boundary pravidla

### Core může záviset na:
- BCL
- `Microsoft.Extensions.*` podle potřeby
- čistých utility packages, pokud jsou opravdu společné

### Core nesmí záviset na:
- `CanDoItAll.Web`
- `CanDoItAll.Infrastructure`
- module projektech
- `SSH.NET`
- Docker/Traefik/IPFS/PostgreSQL specific packages

### LocalRuntime může záviset na:
- `CanDoItAll.Mcp.Core`
- BCL
- `Microsoft.Extensions.*`

### LocalRuntime nesmí záviset na:
- `SSH.NET`
- Modely a služby z `CanDoItAll.Mcp.SshOps`
- webové projekty solution

### DotNetWatch má záviset na:
- `CanDoItAll.Mcp.Core`
- `CanDoItAll.Mcp.LocalRuntime`
- `ModelContextProtocol`

### SshOps má záviset na:
- `CanDoItAll.Mcp.Core`
- `ModelContextProtocol`
- `SSH.NET`
- případně další remote/domain-specific packages

---

## 5. Praktické pravidlo pro rozhodování

Typ nebo helper patří do shared vrstvy tehdy, pokud platí všechny body:

1. není vázaný na jedinou technologii nebo doménu,
2. dává smysl pro více než jeden MCP server,
3. jeho přesun sníží duplicitu nebo regresní riziko,
4. jeho API je dost stabilní, aby nebylo potřeba jej hned příští týden rozbíjet.

Pokud některý bod selže, nech jej zatím server-specific.
