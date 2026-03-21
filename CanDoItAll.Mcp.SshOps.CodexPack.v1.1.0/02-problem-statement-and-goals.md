# Problem statement and goals

## 1. Problem statement

Potřebuješ MCP server, který dá Codexu schopnost bezpečně a opakovatelně spravovat vzdálený Ubuntu server přes SSH, se zaměřením na Docker-based deployment stack:

- .NET aplikace,
- PostgreSQL,
- Traefik,
- HTTPS certifikáty,
- IPFS Kubo v privátním swarm režimu.

Na úrovni implementace nejde jen o „pustit příkaz přes SSH“.  
Jde o to odstranit tyto reálné provozní problémy:

### P1 – Bypass orchestrace
Codex má tendenci:
- použít přímé `ssh`,
- ručně přepsat soubor,
- pustit `docker compose up` mimo koordinovaný flow,
- zkoušet ad-hoc fixy, které nevytváří audit trail.

### P2 – Dlouhé a nejisté operace
Remote build, pull image, start stacku, health check, ACME issuance nebo IPFS init mohou trvat různě dlouho.  
Bez řízeného wait/poll modelu vznikají:
- false negatives,
- race conditions,
- opakované deploye,
- zbytečné přerušení fungujícího stacku.

### P3 – Bezpečnost a identita hostu
SSH key sám o sobě nestačí. Je nutné řešit:
- host key verification,
- zákaz interaktivních promptů,
- bezpečné zacházení se secret hodnotami,
- omezení povolených remote paths a povolených operací.

### P4 – Síťová topologie a expozice služeb
Typický failure mód:
- PostgreSQL omylem mapovaný ven,
- Kubo RPC API omylem zveřejněné,
- Traefik routery špatně nalabelované,
- interní sítě neoddělené od proxy sítě.

### P5 – Nejasná validační hranice
Nestačí vědět, že `docker compose up` proběhlo bez chyby.
Potřebuješ vědět:
- že kontejner je healthy,
- že Traefik vidí router,
- že certifikát je skutečně vydaný,
- že .NET app dojde na PostgreSQL,
- že app dojde na Kubo API,
- že IPFS privátní swarm není připojený na veřejný bootstrap list.

### P6 – Obnova po chybě
Když deploy selže, potřebuješ:
- logy,
- poslední známou dobrou revizi,
- rollback tool,
- znovupřipojení k dlouhé operaci i po restartu MCP serveru.

## 2. Cíle

### G1 – Safe by default
Server má být bezpečný už v základním nastavení:
- host key pinning,
- non-interactive auth,
- allow-listed targety,
- allow-listed remote roots,
- raw exec vypnutý.

### G2 – Deterministic remote orchestration
Každá mutace má mít:
- jasný tool contract,
- korelační identifikátor,
- auditovatelný výsledek,
- možnost čekání a čtení logů.

### G3 – Idempotent deployment
Opakované spuštění stejné operace nemá vést k chaosu.  
`compose_apply`, `network_ensure`, `volume_ensure`, `fs_apply_bundle` musí být navržené jako bezpečně opakovatelné.

### G4 – Clear separation of public and private surfaces
Veřejné plochy:
- Traefik port 80/443,
- veřejná doména aplikace,
- případně chráněný dashboard.

Privátní plochy:
- PostgreSQL,
- Kubo RPC API,
- interní backend síť,
- secrets a state adresáře.

### G5 – Explicit waiting and validation
Codex má mít k dispozici:
- `operation_wait`,
- `http_wait`,
- `postgres_ready`,
- `cert_check`,
- `ipfs_private_validate`,
- `target_audit`.

### G6 – Rollback readiness
Každý stack deployment musí mít:
- backup původních compose artefaktů,
- záznam nové revize,
- rollback na poslední stabilní revizi.

### G7 – Technology grounding for Codex
Balík musí obsahovat i technologické podklady, aby Codex při implementaci a použití:
- chápal Docker networking,
- rozuměl Traefik labelům,
- věděl, kdy použít ACME HTTP vs DNS challenge,
- správně izoloval PostgreSQL a IPFS,
- nerozbíjel privátní IPFS swarm.

## 3. Ne-funkční cíle

### NFG1 – Přehlednost
Tool response musí být:
- strojově čitelná,
- stručná,
- ale s dostatkem detailu pro rozhodování.

### NFG2 – Robustnost
Server musí přežít:
- timeouty,
- přerušení klienta,
- restart MCP procesu,
- chybný deploy artefakt,
- host key mismatch,
- chybějící docker compose plugin.

### NFG3 – Rozšiřitelnost
Architektura musí umožnit později doplnit:
- OpenSSH CLI adapter,
- registry deploy workflow,
- více targetů a prostředí,
- DNS challenge providers,
- port-forward a debugging tools,
- DB backup workflow.

## 4. Měřítka úspěchu

Balík je úspěšný, pokud z něj Codex zvládne navrhnout a implementovat server, který v praxi splní:

- žádná interaktivní SSH autentizace,
- žádný neautorizovaný write mimo remote allow roots,
- žádné slepé čekání v klientovi,
- každá dlouhá operace má `operationId`,
- Traefik deploy lze validovat do úrovně „router ready + cert ready + HTTPS 200/301“,
- PostgreSQL a IPFS API nejsou veřejně mapované,
- rollback poslední revize je proveditelný jedním toolem,
- IPFS validace odhalí veřejný bootstrap list nebo chybějící `swarm.key`.
