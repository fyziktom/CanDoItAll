# Implementation checklist

## 1. Kódová disciplína
- [ ] Všechny komentáře ve zdrojových kódech jsou v angličtině.
- [ ] Public API má smysluplné názvy a XML docs tam, kde dávají hodnotu.
- [ ] Nejsou použité magické stringy pro stavy a error codes bez centralizace.
- [ ] Není použitý `Thread.Sleep` pro workflow waits.
- [ ] Async flow používá `CancellationToken`.

## 2. Hosting
- [ ] MCP tooly jsou registrované přes assembly scanning nebo explicitní registraci.
- [ ] Server buildí na `.NET 10`.
- [ ] Žádný startup code nepíše na stdout.
- [ ] Když konfigurace selže, chyba je akční.

## 3. Process supervision
- [ ] Child procesy mají capture stdout i stderr.
- [ ] Při stopu se killuje celý strom procesů.
- [ ] Parent exit event se propíše do session/operation stavu.
- [ ] PIDs jsou logované korelačně.
- [ ] Force kill má timeout a není nekonečný.

## 4. App lifecycle
- [ ] `WatchRun` používá `dotnet watch --non-interactive`.
- [ ] `RunOnce` používá `dotnet run`.
- [ ] Session reuse porovnává kompatibilitu správně.
- [ ] Start vrací initial cursor.
- [ ] Stop je idempotentní.

## 5. Build/test
- [ ] Build používá `dotnet build`.
- [ ] Testy používají `dotnet test`.
- [ ] MVP nepoužívá `dotnet watch test`.
- [ ] Policy `StopAndResume` je implementovaná end-to-end.
- [ ] Resume outcome je vrácen v resultu.

## 6. Logs a waits
- [ ] Log entries mají monotónní sequence.
- [ ] Log cursor funguje i přes restart session.
- [ ] `Healthy` wait používá health probe nebo fallback signály.
- [ ] `QuietSinceCursor` je implementované korektně.
- [ ] Timeout response vrací poslední známý snapshot.

## 7. Security a privacy
- [ ] Path guard blokuje cesty mimo workspace.
- [ ] Health URL host whitelist je enforced.
- [ ] Env whitelist je enforced.
- [ ] Redaction kryje tokeny, hesla, connection stringy a Bearer patterny.
- [ ] Response nevracejí tajné hodnoty z konfigurace.

## 8. Observability
- [ ] Session a operation mají correlation ID.
- [ ] File logger ukládá diagnosticky použitelné záznamy.
- [ ] Systémové eventy jsou odlišitelné od process stdout/stderr.
- [ ] `diagnose_start_failure` vrací evidence.
- [ ] Logy jsou čitelné i pro člověka, ne jen pro stroj.
