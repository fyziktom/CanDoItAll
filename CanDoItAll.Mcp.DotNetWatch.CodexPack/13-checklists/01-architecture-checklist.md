# Architecture checklist

Použij při návrhu i code review.

## A. Hosting a bootstrap
- [ ] Server je stdio MCP server.
- [ ] `Program.cs` používá `Host.CreateEmptyApplicationBuilder(settings: null)`.
- [ ] Na stdout se nepíše nic mimo MCP protokol.
- [ ] Logging jde do stderr a/nebo souboru.
- [ ] Konfigurace se fail-fast validuje při startu.

## B. API design
- [ ] Tooly mají jednoznačný namespace prefix.
- [ ] Requesty nepřijímají raw shell command string.
- [ ] Response mají konzistentní envelope.
- [ ] Error codes jsou normalizované a akční.
- [ ] `workspace_info` vrací dost metadata pro klienta.

## C. Runtime orchestrace
- [ ] Existuje centrální koordinátor.
- [ ] Mutující operace jsou serializované per workspace.
- [ ] Start/stop/build/test neobcházejí koordinátor.
- [ ] Existuje kompatibilita a reuse model pro session.
- [ ] Build/test mají policy layer pro běžící app session.

## D. Waiting a logs
- [ ] Logy mají cursor/sequence model.
- [ ] `app_wait` a `operation_wait` existují.
- [ ] Wait podmínky nepředpokládají klientské sleepy.
- [ ] Quiet period je navázaná na cursor/log activity.
- [ ] Health probe je oddělená služba.

## E. Recovery
- [ ] Neočekávaný exit procesu je zachycený.
- [ ] Existuje stale process registry.
- [ ] Startup cleanup umí uklidit osiřelé managed procesy.
- [ ] Cleanup je možné spustit i ručně.
- [ ] Diagnostika failů je read-only.

## F. Security
- [ ] Cesty jsou normalizované a omezené na workspace.
- [ ] Environment overlay je whitelistovaný.
- [ ] Health probe je defaultně omezený na loopback.
- [ ] Logy se redigují.
- [ ] Neexistuje nechtěná cesta k command injection.

## G. Testability
- [ ] Komponenty jsou testovatelné bez plného hostu.
- [ ] Procesní chování lze simulovat fixturemi.
- [ ] Stavové přechody jsou explicitní a testovatelné.
- [ ] Architektura podporuje integrační testy pro P0 scénáře.
