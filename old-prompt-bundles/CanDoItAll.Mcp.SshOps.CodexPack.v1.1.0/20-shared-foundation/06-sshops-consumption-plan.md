# SSH Ops consumption plan

## Cíl

Zajistit, aby `CanDoItAll.Mcp.SshOps` od prvního commitu používal shared foundation místo vlastních kopií common helperů.

## Povinné shared dependencies

`CanDoItAll.Mcp.SshOps` musí z `CanDoItAll.Mcp.Core` používat:

- shared response envelope,
- shared error model,
- correlation / operation / server IDs,
- mutation gate,
- log abstractions,
- redaction,
- operation primitives,
- HTTP/TLS probe helpery.

## Co si SSH server implementuje sám

- `ISshTransport`
- `SshNetTransport`
- `HostKeyVerifier`
- `SecretResolver`
- remote path policy
- remote file services
- remote job runner
- revision backups
- docker / compose / traefik / postgres / ipfs doménu

## Praktické pravidlo pro každou novou třídu

Při zavádění nové třídy v `CanDoItAll.Mcp.SshOps` se má položit otázka:

1. Je to common primitive?
2. Je to už někde v dotnetwatch?
3. Mělo by to existovat v shared foundation?

Pokud odpověď na 1 a 2 je ano, nemá vzniknout další lokální kopie.

## Povinné první kroky v SSH projektu

1. přidej reference na shared projekty,
2. udělej `targets_list` a `target_test` přes shared envelope,
3. zaveď logging přes shared observability primitives,
4. zaveď mutation gate pro target/stack level,
5. teprve potom rozšiřuj domain services.

## Anti-patterns

- vlastní `ToolEnvelope` v SSH projektu,
- vlastní redactor v SSH projektu,
- vlastní generic operation wait loop v SSH projektu,
- kopie `ServerInstanceIdentity`,
- kopie log bufferu.

## QA gate

SSH server není připraven ani ve scaffold fázi, pokud:
- nereferencuje shared foundation,
- vrací jinou response envelope shape,
- duplikuje redaction/logging helpers.
