# Dependency rules

## 1. Povolený dependency graph

```text
CanDoItAll.Mcp.Core
        ^
        |
CanDoItAll.Mcp.LocalRuntime
        ^
        |
CanDoItAll.Mcp.DotNetWatch

CanDoItAll.Mcp.Core
        ^
        |
CanDoItAll.Mcp.SshOps
```

## 2. Co je zakázané

### Zakázané reference ze shared foundation
Shared projekty nesmí referencovat:

- `CanDoItAll.Web`
- `CanDoItAll.Infrastructure`
- `CanDoItAll.Application`
- `CanDoItAll.SharedKernel` jen proto, „že už tam něco podobného je“
- `CanDoItAll.Mcp.SshOps`
- modulové projekty
- technologicky specifické balíčky typu `SSH.NET`

### Zakázané reference ze `CanDoItAll.Mcp.SshOps`
`CanDoItAll.Mcp.SshOps` nesmí referencovat `CanDoItAll.Mcp.DotNetWatch`.  
Sdílení smí jít jen přes shared foundation.

### Zakázané reference ze `CanDoItAll.Mcp.DotNetWatch`
`CanDoItAll.Mcp.DotNetWatch` nesmí referencovat `CanDoItAll.Mcp.SshOps`.

## 3. Co je povolené

### `CanDoItAll.Mcp.Core`
Smí obsahovat:
- common contracts,
- common observability,
- common concurrency,
- common operation primitives,
- common HTTP/TLS helpery.

### `CanDoItAll.Mcp.LocalRuntime`
Smí obsahovat:
- local child process runtime,
- stale process cleanup,
- ownership markers.

### `CanDoItAll.Mcp.DotNetWatch`
Smí obsahovat:
- dotnet watch doménovou logiku,
- dotnet build/test orchestration,
- dotnet-specific diagnostics.

### `CanDoItAll.Mcp.SshOps`
Smí obsahovat:
- SSH transport,
- host key model,
- remote file deployment,
- docker/compose/traefik/postgres/ipfs doménu.

## 4. Praktické QA pravidlo

Kdykoli se při review objeví věta:

> „Tohle by mohl někdy použít i jiný server.“

tak to **ještě automaticky neznamená**, že to patří do shared foundation.

Do shared foundation patří až tehdy, když:

1. je to dost stabilní,
2. není to příliš doménové,
3. extrakce skutečně snižuje duplicitu už teď,
4. lze to otestovat odděleně.

## 5. Důsledek pro Codex

Když si Codex není jistý, zda něco extrahovat, má preferovat:
- shared pro stabilní primitives,
- server-specific pro technologickou a doménovou logiku.
