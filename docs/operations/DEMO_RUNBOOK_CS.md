# WB6 — spuštění, ukázka a obnova

Kandidát je připraven ke spuštění na tomto počítači. **Celé zákaznické demo zatím neprošlo.**
Workbench renderery jsou dokončené; skutečný lokální model prokázal čtení souboru, analýzu
obrázku a dokončený uložený Workflow. Zápis souborů Agentem přes schvalování, generování
obrázku, SVG/XLSX od skutečného modelu a dokončení Processu zůstávají překážkami. Připravená
data a deterministické testy tyto výsledky nenahrazují. Před prezentací nezahajovat další
oddělování modulů.

## Spuštění

Použít PowerShell 7.4+ v kořeni tohoto checkoutu a spuštěný Docker Desktop. Není potřeba IDE ani hot reload.
Neměnit image, privátní konfiguraci, certifikát, host binding ani databázi.

```powershell
$candidate = 'artifacts/workbench-completion-wb6/demo-candidate/candidate.json'
./tools/demo/workbench-wb6/Start-Demo.ps1 -CandidatePath $candidate
./tools/demo/workbench-wb6/Check-Demo.ps1 -CandidatePath $candidate
```

Otevřít [Agents](http://127.0.0.1:55558/agents) nebo
[Workbench Demo — October 2026](http://127.0.0.1:55558/projects/325160a2-c77e-4578-861c-47818cafee21/structure).
Pokud se zobrazí výběr databáze, ponechat připravený profil a stisknout **Continue**.
Ověřeno pouze v Chromium při **1920 × 1080, DPR 1**. Okno prohlížeče před kontrolou nastavit
na tuto velikost. Běžná instalace na portu 5032 zůstala nedotčená.

Kontrola musí vrátit zdravý `app`, zdravý `db` a HTTP 200. Obsazený port, změněný Compose nebo
chybějící původní privátní konfigurace znamenají zastavit přípravu a vyřešit konkrétní chybu.
Nespouštět obecné ukončení všech procesů dotnet/PostgreSQL ani `docker compose down -v`.

## Přesná verze a zachovaná data

| Položka | Identita |
|---|---|
| Produkční zdroj | `fed6291ad4deee77bc4c379818772ae1c604b8b9` |
| Components | `a3fd4d22f2c4e0432cf389f194c6468b44ab7371` |
| FileTools | `3a080ecd31068a77c1e1bd639f7a78e21c93db85` |
| Aplikační image | `sha256:f197824e4b82d181002fd3dea414e5b12f22b55e52dd75324db87b61cc9f8344` |
| PostgreSQL image | `sha256:77f585114c32fbca283dc835b0596f4e52b51b4c6662d7810b2f4084f60a1873` |
| Compose projekt | `candoitall-wb6-candidate-6e9e58cf` |
| Projekt aplikace | `325160a2-c77e-4578-861c-47818cafee21` |
| Privátní konfigurace | `artifacts/workbench-completion-wb6/candidate-runtime/.env` |
| Manifest a snapshoty | `artifacts/workbench-completion-wb6/demo-candidate/` |

Manifest obsahuje přesné cesty, SHA-256 konfigurace a názvy tří vlastních volumes. Záloha
obsahuje databázi, soubory a klíče; chránit ji stejně jako privátní konfiguraci. Do Gitu se
neukládají data, hesla, certifikáty ani celé konverzace. Pozdější commit testů a dokumentace
nemění uvedený produkční image.

## Ověřené kroky v UI

1. Ve Structure otevřít **Object index**. Vybrat `WB6 Content preview geometry`, potom
   **Expand preview**. Soubor `wb6-content-geometry.md` má 48 sekcí. Nadpis a tlačítka zůstávají
   přístupné při rolování i maximalizaci; toolbar pod dialogem nepřebírá kliknutí.
2. Vybrat `Inputs — rehearsal brief`, otevřít nový kontextový chat s **WB6 Local Artifact
   Author**. Ověřený běh čtení je `88b05ab4-ad14-434b-828b-a798052e619e`. Prohlédnout jeho
   skutečný receipt `project_structure_asset_text_get`; odpověď obsahuje canary a výpočet
   10 hodin / 960 USD. Opakování je nový modelový běh, nikoli obnovení tohoto receiptu.
3. Vybrat `Inputs — private visual canary`, otevřít nový chat s **WB6 Local Visual Analyst**.
   Ověřený běh `12442b73-cff6-457d-b79f-53403698e945` skutečně použil
   `project_structure_asset_image_analyze`: jeden červený kruh, dva modré čtverce a jeden
   zelený trojúhelník. Obrázek byl připraven nezávisle jako kontrolní vstup; nebyl vygenerován modelem.
4. Otevřít uložený Workflow **WB6 — local briefing to project asset**. Jeho publikovaná verze
   je `5d60b4ac-71fc-4945-8a13-fd82db9b3159`. Dokončený běh
   `dbc9475f-c072-4b5e-84ac-8efe6f3255b1` má čtyři dokončené kroky a skutečný Markdown výstup.
   Ve Structure je výstup `custom:d86c17c87be6406a8a37480b316fa64b`. Otevřít náhled, potom
   v **Files → Actions → Download** stáhnout soubor. Ověřený download měl 714 bajtů a SHA-256
   `50CCE0F52F4DDEAFCADFB0FCCFDC8B6B697934BB86C1EAFC10C84CF43412CE2F`.
5. V **History / Usage** lze dohledat Workflow request
   `f1a1a370-f521-4f4b-bcf9-fd59ca622746` a propojený kanonický obsah. Evidence je částečný
   cenový odhad lokálního modelu, nikoli faktura za externí API.
6. U Processu ukázat pouze skutečný zachovaný stav. Běh
   `504627d9-a18b-4b17-878c-b03884c0a809` byl ručně obsazen a přijat po review, ale skončil
   **Blocked** při zápisu. Jeden krok ze dvou prošel. To není dokončený Process.

Prompty ověřených lokálních cest (zadávat v novém kontextovém chatu nad správným uzlem):

```text
Read the selected brief with the native text tool. Report its canary and compute total hours and cost from the actual file.
```

```text
Inspect the selected image with the native image analysis tool. Report the visible shapes, their colours and exact counts. Base the answer only on image pixels.
```

Lokální provider je **Local Ollama**, model **qwen3.5:9b**, thinking vypnuté, nejvýše 2048
výstupních tokenů. Ollama musí běžet na hostiteli na portu 11434; kontejner používá
`host.docker.internal`. Nový běh může mít jiný výsledek. Zachované úspěšné běhy jsou dostupné
pro přípravu bez dalšího spouštění modelu.

## Co stále blokuje úplné demo

- Přímý Ollama provider neposkytl v běžném Agent chatu potřebné schvalované zápisové nástroje.
  Pokus přes lokální OpenAI kompatibilní endpoint skončil HTTP 400 kvůli boolean JSON schema.
  Schvalování nebylo vypnuto a oprávnění nebyla rozšířena. Skutečný Agent zápis, SVG a XLSX
  proto nelze označit jako prošlé.
- Nativní SVG náhled záměrně zobrazuje pouze metadata. Úspěšné uložení, kontrola XML a stažení
  SVG tedy neprokazují požadované vykreslení diagramu v aplikaci. Bezpečné zacházení se SVG
  nebylo kvůli demonstraci oslabeno.
- Nebyl dostupný funkční lokální raster provider. Placené volání nebylo schváleno a nebylo
  provedeno. Canary vision ověřuje analýzu, ne generování obrázku.
- Process `project-structure-note-writeback` má blokující receipt gate: model zvolil nesprávné
  ID projektu a scope souborů, nevytvořil požadovaný uzel. Jediný rework rovněž selhal.
  **Approve rework spouští okamžitě**; není to tlačítko pro otevření potvrzovacího dialogu.
  Nepoužívat je k pouhému prohlížení. Pro další pokus je nutná vědomá nová příprava.
- Pokus publikovat novou malou definici Processu skončil konfliktem verze. Skutečný modelový
  běh proto použil existující publikovanou definici; vlastní nová publikace není prokázaná.

Původní neúspěchy zůstávají v evidenci vedle následných kontrol. Deterministické zdrojové a
klientské fixture aplikace jsou oddělené od tohoto produkčního kandidáta. Jejich naprogramované
odpovědi dokazují integraci a schvalování, nikoli schopnost skutečného modelu.

V oddělené klientské fixture prošlo deset deterministických scénářů v původních nebo výslovně
označených následných bězích. SVG prošlo zápisem, stažením, kontrolou XML a novým textovým
čtením. XLSX prošlo třemi listy, náhledem, stažením a čtením původní i změněné verze:
vstup B3 se změnil z 5 na 7; nezávislý výpočet dává 960 → 1 200 USD. Samostatný skriptovaný
Process `e751ae4a-bd14-4052-a613-e36341dc6d67` dokončil 2/2 kroky a skutečně vytvořil uzel
pod svým během. Jeho nativní audit obsahuje požadované create/readback receipts. Nejde o
skutečný modelový úspěch; generování manažerského komentáře selhalo odděleně. Tento projekt
není součástí produkčního demonstračního scénáře na portu 55558.

## Zastavení, nový start a záloha

```powershell
./tools/demo/workbench-wb6/Stop-Demo.ps1 -CandidatePath $candidate
./tools/demo/workbench-wb6/Start-Demo.ps1 -CandidatePath $candidate
./tools/demo/workbench-wb6/Check-Demo.ps1 -CandidatePath $candidate
```

Stop zachová všechny volumes. Zálohování vyžaduje nový adresář a na konci ponechá kandidáta
zastaveného. Potom jej explicitně znovu spustit:

```powershell
./tools/demo/workbench-wb6/Backup-Demo.ps1 -CandidatePath $candidate -SnapshotPath 'artifacts/workbench-completion-wb6/demo-candidate/snapshot-next'
./tools/demo/workbench-wb6/Start-Demo.ps1 -CandidatePath $candidate
```

Poslední uložený snapshot je `snapshot-03`. Prošel obnovou do odděleného projektu
`candoitall-wb6-recovery-808eca6c` na portu 55561. V obnoveném UI byl nalezen původní Workflow,
zachovaný blokovaný Process a skutečné pixely kontrolního obrázku. API navíc vrátilo stejné
bajty vstupního Markdownu, obrázku a Workflow výstupu. Recovery je zastavená, její data zůstala
zachována. Starší Recovery 02 na portu 55560 také prošla UI a stažením Workflow souboru.

```powershell
$recovery = 'artifacts/workbench-completion-wb6/demo-candidate/recovery-03/candidate.json'
./tools/demo/workbench-wb6/Start-Demo.ps1 -CandidatePath $recovery
./tools/demo/workbench-wb6/Check-Demo.ps1 -CandidatePath $recovery
```

Pro další nezávislou obnovu použít nový adresář, volný port a dvě nepřekrývající se sítě.
Následující hodnoty musí být stále volné; skript případný konflikt odmítne:

```powershell
./tools/demo/workbench-wb6/Restore-Demo.ps1 -CandidatePath $candidate -SnapshotPath 'artifacts/workbench-completion-wb6/demo-candidate/snapshot-03' -TargetDirectory 'artifacts/workbench-completion-wb6/demo-candidate/recovery-next' -Port 55562 -FrontendSubnet '10.231.240.160/28' -BackendSubnet '10.231.240.176/28'
```

Při chybě aktuálního spuštění lze použít ověřenou Recovery 03 se stejným image a posledním
snapshotem. Původní baseline image `sha256:7b578acd941bb8fac91d2031fe85defa73702b188ee3cbce3532821613b33cbb`
a jeho snapshoty jsou rovněž zachované; baseline nemá nové opravy a nové demonstrační výstupy.
Nepřepisovat jí současné volumes. Při chybě dešifrování obnovit původní konfiguraci a klíče,
nevytvářet náhradní hesla ani nový host binding.

Podrobné lokální důkazy: `artifacts/workbench-completion-wb6/evidence.json`, `genuine-ledger.json`,
`execution.md` a jednotlivé původní TRX/logy. Rozhodnutí `demo_ready` musí zůstat `false`,
dokud neprojdou všechny požadované skutečné modelové cesty a závěrečný souvislý průchod.
