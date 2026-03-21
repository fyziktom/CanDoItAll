> **Revize 1.1.0 – shared foundation gate**  
> Validační strategie nově obsahuje ještě nultý release gate:  
> `CanDoItAll.Mcp.DotNetWatch` musí po extrakci shared knihoven projít regresní validací dřív, než se začne implementovat `CanDoItAll.Mcp.SshOps`.

# Validační strategie

## 1. Princip
Validace musí dokazovat dvě věci zároveň:

1. že MCP server bezpečně a deterministicky řídí vzdálený Ubuntu host,
2. že Codex dostává stabilní, čitelný a opakovatelný tool surface pro Docker, Traefik, PostgreSQL a privátní IPFS.

Nejde jen o unit testy. Potřebujeme **layered validation**:

- statická validace konfigurace,
- unit testy doménové logiky,
- contract testy DTO a tool response envelope,
- integration testy s fake SSH backendem,
- end-to-end testy proti reálnému test hostu,
- failure-injection scénáře,
- release gates.

## 2. Testovací pyramidy

### 2.1 Unit
Cíl:
- validátory,
- path guard,
- host key matching,
- redakce logů,
- compose parser wrappery,
- operation state reducer,
- retry policy,
- timeout policy,
- mapping tool DTO -> domain command.

Požadavek:
- rychlé,
- bez sítě,
- bez reálného SSH,
- bez Docker daemonu.

### 2.2 Contract
Cíl:
- stabilita veřejného MCP API.

Ověřit:
- JSON schema-like shape requestů,
- povinná pole,
- standardní error codes,
- `operationId` chování,
- serializace `status`, `summary`, `nextSteps`,
- backward-compatible rozšiřování response.

### 2.3 Integration
Cíl:
- interakce mezi vrstvami.

Doporučené integration modes:
- fake `ISshTransport`,
- stub remote filesystem,
- simulovaný detached job runner,
- simulovaný Docker/Compose výstup.

Ověřit:
- locking,
- concurrency,
- rollback orchestrace,
- bundle upload/apply flow,
- parse logů,
- cancel/wait flow.

### 2.4 End-to-end against real host
Cíl:
- skutečné SSH,
- skutečný Ubuntu server,
- skutečný Docker Engine a Compose plugin,
- skutečný Traefik stack,
- skutečný app stack,
- skutečný PostgreSQL container,
- skutečný Kubo/IPFS container.

Ověřit:
- bootstrap hostu,
- nasazení Traefiku,
- vystavení .NET app přes HTTPS,
- issuance/obnova certifikátu,
- DB readiness,
- IPFS privátní swarm chování,
- rollback poslední revize,
- reconnect po přerušení klienta.

## 3. Testovací prostředí

### 3.1 Local developer harness
Použít:
- fake SSH,
- lokální test data,
- snapshoty logů,
- golden files.

### 3.2 CI integration harness
Použít:
- disposable Ubuntu VM nebo dedikovaný ephemeral host,
- izolovanou DNS zónu nebo test doménu,
- staging ACME,
- test registry nebo veřejné image s pinem tagu,
- samostatný target config.

### 3.3 Staging host
Použít:
- topologii co nejbližší produkci,
- reálný Traefik,
- reálné porty 80/443,
- oddělené secrets,
- privátní IPFS swarm s test peerem.

## 4. Validační osy

### 4.1 Bezpečnost
Ověřit:
- host key mismatch vede k hard fail,
- raw exec disabled by default,
- secret redaction v logu,
- path traversal je odmítnut,
- zápis mimo allowed roots je odmítnut,
- IPFS API není veřejně vystavené,
- PostgreSQL není veřejně vystavená,
- dashboard Traefiku není veřejně vystaven bez autentizace.

### 4.2 Spolehlivost
Ověřit:
- dlouhá operace přežije reconnect klienta,
- `operation_wait` umí timeout a polling,
- `operation_logs` vrací inkrementální logy,
- rollback vrátí předchozí soubory a compose stav,
- locky zabrání souběžnému konfliktu.

### 4.3 Použitelnost pro Codex
Ověřit:
- summary je stručné a akční,
- next steps jsou jasné,
- chybové stavy jsou deterministické,
- tooly nevyžadují interaktivní shell,
- návratové payloady mají stálou strukturu.

### 4.4 Provozní správnost
Ověřit:
- compose config validace proběhne před apply,
- health checks čekají na readiness,
- Traefik route je dostupná přes HTTPS,
- certifikát je vystaven pro očekávaný hostname,
- app komunikuje s PostgreSQL,
- app komunikuje s IPFS RPC jen interně.

## 5. Release gates

Release není připraven, pokud není splněno vše:

- žádný blocker v threat modelu,
- žádný kritický únik secretů v logu,
- žádný failing unit/contract/integration test,
- minimálně jeden green E2E deploy proti test hostu,
- green rollback test,
- green host key mismatch test,
- green IPFS private validation test,
- green path guard negative test,
- green ACME staging test,
- zdokumentované known risks,
- aktualizovaný manifest a reference.

## 6. Negativní scénáře
Povinně testovat:

- špatný SSH key,
- špatný host key fingerprint,
- timeout při image pull,
- compose config syntax error,
- chybějící external network,
- nevalidní Traefik labels,
- obsazený port 80/443,
- Let's Encrypt rate limit / staging fallback,
- PostgreSQL volume permissions problem,
- IPFS swarm key mismatch,
- IPFS default bootstrap peers nebyly odstraněny,
- nedostupný backend service,
- rollback na neexistující revision.

## 7. Důkazní artefakty
Každý E2E běh má uložit:

- target identifier,
- timestamp,
- git revision serveru,
- použitou target konfiguraci bez secretů,
- operation journal,
- relevantní remote logy,
- probe výsledky,
- cert summary,
- compose ps snapshot,
- rollback snapshot.

## 8. Metriky kvality
Doporučené minimum:

- unit + contract coverage na kritických třídách: >= 85 %,
- všechny security sensitive cesty pokryté explicitními testy,
- 0 známých blockerů,
- 0 TODO v shipping path,
- 100 % toolů s acceptance scénářem,
- 100 % mutujících toolů s rollback nebo fail-safe chováním.

## 9. Doporučené test projekty
- `CanDoItAll.Mcp.SshOps.Tests.Unit`
- `CanDoItAll.Mcp.SshOps.Tests.Contract`
- `CanDoItAll.Mcp.SshOps.Tests.Integration`
- `CanDoItAll.Mcp.SshOps.Tests.E2E` (volitelně mimo default CI)

## 10. Exit criteria pro Codex implementaci
Codex smí považovat práci za hotovou až když:

- kód buildí na .NET 10,
- všechny tooly mají dokumentovanou request/response ukázku,
- validace konfigurace běží při startu,
- základní compose/Traefik/PostgreSQL/IPFS E2E scénář prošel,
- QA checklist je odškrtnut,
- závěrečná self-review zpráva je přiložená v PR.
