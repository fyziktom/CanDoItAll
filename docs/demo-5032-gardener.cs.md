# Gardener: generování a čtení obrázků — 7. října 2026

Oprava patří běžné instanci 5032 / 7271 a projektu
[Garden](https://localhost:7271/projects/ca0d9457-4222-4b9a-9fa2-e5356ec3f904/structure).
Navazuje na [ověření instance 5032](demo-5032-overeni.cs.md).
Původní zmrazený kandidát WB6 a jeho 173 souborů důkazů mají zachované hashe.

## Příčina a opravy

Gardener v běhu `13cf1198-8705-4ebe-868c-a04a1af1ca48` předal původní SVG návrh
jako obrazový vstup nástroji `image_generation_create`. Volání skončilo při kontrole
HTTP odpovědi poskytovatele. Nástroj dosud přijímal libovolné `image/*`, přestože
[OpenAI image edit](https://developers.openai.com/api/reference/resources/images/methods/edit)
pro GPT Image požaduje PNG, WebP nebo JPEG. Přesný HTTP stav a tělo původní chyby
nejsou v dostupném bezpečném logu zachyceny.

Commit `822b79ad8c2ea2159e3b25501ef1e17728f9f5a9` ověřuje typ a signaturu vstupu
před placeným voláním. SVG, GIF nebo nesouhlasící obsah vrací opravitelné
`SourceImageFormatUnsupported` s účinkem `NotCommitted`. Popis nástroje vysvětluje,
že se SVG čte přes `project_structure_asset_text_get`; jeho rozvržení lze popsat
v promptu nebo dodat předem rasterizovaný obrázek. Nejde o automatickou rasterizaci.

U Gardenera bylo generování povolené, ale `workspaceToolAccess.canTransformArtifacts`
bylo vypnuté. Zapnutí tohoto existujícího oprávnění umožnilo nástroji
`project_structure_asset_image_analyze` načíst skutečné pixely uloženého uzlu.
Ostatní nastavení, přístup k projektům a poskytovatelé zůstali zachované.

Při ověření se reprodukovala také chyba tlačítka **New thread**: vznikla relace,
ale shell nezaměřil její nové okno. Commit
`145205fd2e46b8e5f5aeabf9742c458d57514bb0` používá existující `IAgentChatLauncher`,
který spojí vytvoření relace s otevřením jejího okna. Oba podpisy ověřil `git verify-commit`.
Po posledním restartu UI potvrdilo změnu identity okna a viditelný prázdný composer.
Uložený obrázek se znovu načetl v nativním náhledu a jeho hash se nezměnil.

## Skutečný model a uložená data

Oba běhy byly odeslány přes plovoucí chat Gardenera v kontextu projektu Garden,
v Chromium při 1920 × 1080 a DPR 1. Použily nakonfigurované OpenAI poskytovatele
a chatový model `gpt-5.6-terra`; nemají deterministické odpovědi fixture.
Uložený nativní výsledek generování potvrzuje poskytovatele **OpenAI image generation**
a model `gpt-image-2`.
Modelové běhy patří commitu `822b79ad8c2ea2159e3b25501ef1e17728f9f5a9`.
Následný commit mění pouze volání launcheru a jeho testy; generování a analýza
obrázků zůstávají stejné. Finální UI smoke proběhl po tomto druhém commitu a restartu.

| Účel | Běh | Výsledek |
|---|---|---|
| Přečtení SVG, jeden nový obrázek a uložení uzlu | `abb2a04f-ed8e-4bac-affd-d09fd5212816` | Completed / Succeeded |
| Nezávislá analýza uložených pixelů v nové konverzaci | `eac4e9b3-e6f2-418d-9263-a55d2179185c` | Completed / Succeeded |

Nový uzel `custom:93e904861af84f88955b28930100207e` má název
**Gardener image repair verification — south view** a leží pod původním uzlem
`images` (`custom:2a58e8eca063401891e09eca4cae7e0f`). PNG má 1024 × 1024 pixelů,
2 655 734 bajtů a SHA-256
`9596c754ef5dc2f64aadf9a77f46ef74a54fde14890bb029db531fa2026e83e0`.

První běh má potvrzené účinky generování a vytvoření assetu. Druhý běh použil
`project_structure_asset_image_analyze` právě nad tímto uzlem. Popsal čtyři záhony,
křížící se cesty, cihlový vstup, zadní plot, opěrnou mříž a fialové květy; tyto
detaily odpovídají nezávisle prohlédnutému PNG. Druhá konverzace neobsahovala
generační prompt. Obrázek je koncept, přesná geometrie a druhy rostlin nejsou zárukou.

Souhrny běhů uvádějí USD 0,060944 a USD 0,020232. Zahrnutí vnořeného generování
obrázku a vision volání do těchto částek není doložené; celková účtovaná cena zůstává
neověřená. Původní neúspěšný běh zůstává Failed, jeho nejistý účinek nebyl přehrán.
Vznikl jeden nový obrázek; původně požadované čtyři pohledy nejsou tímto testem dokončené.

## Deterministické ověření

Před opravou čtyři případy reprodukovaly chybějící odmítnutí nepodporovaných zdrojů.
Po opravě prošlo 41 unit a 27 integračních testů obrázků, oprávnění, uložení a obnovy,
dále 90 komponentových testů chatu včetně nové regrese zaměření okna. Celkem
158 průchodů, nula selhání a nula přeskočených případů. Discovery odpovídá těmto počtům.

Přímé buildy modulu AgentFramework prošly bez varování a chyb. Obnovené testovací
assembly mají pouze stávající upozornění analyzérů. Finální no-write portability
enforcement prošel s 15 306 nezměněnými schválenými nálezy; baseline se neměnila.
Databázové testy používaly samostatný PostgreSQL 18 kontejner, který byl poté zastaven.
Nový široký Stable běh neproběhl: jde o opravy vstupní kontroly nástroje a volání
existujícího launcheru bez změn společného buildu, registrací, schématu nebo závislostí.
Původní široký checkpoint a jeho kvalifikace zůstávají historické.

## Ruční kontrola a restart

1. Otevřete Garden na velkém desktopu. Po restartu potvrďte **Continue** u aktivní
   databáze `candoitall_development`.
2. V **Object index** vyberte nový obrázek, otevřete **Inspector** a **Expand preview**.
   Náhled musí načíst skutečný obrázek 1024 × 1024.
3. Přes **Chats** otevřete historii Gardenera. Dva výše uvedené běhy obsahují
   generační výsledek a samostatnou analýzu. **New thread** slouží pro nový záměr;
   neopakuje původní chybný běh.
4. Při dalším generování ze SVG nechte agenta přečíst jeho text nebo poskytněte PNG,
   JPEG či WebP. Pro čtení uloženého obrázku musí zůstat povolené čtení projektu
   a uvedené oprávnění k analýze artefaktů.

Pro zastavení a spuštění použijte [existující postup pro sdílený backend](demo-5032-overeni.cs.md#spuštění-a-restart)
s explicitními URL 5032 a 7271. Start může pokračovat i po timeoutu HTTP požadavku;
nejprve zkontrolujte relaci, nevytvářejte druhou instanci. Úspěšný health check
neznamená ověřený hot reload. Tento záznam používá úplný build a řízený restart.

Soukromé důkazy v `artifacts/gardener-image-repair-20261007/` oddělují TRX a původní
neúspěšné pokusy od `genuine-model-ledger.json`, výsledků skutečných běhů, PNG
a snímků UI. [Manifest](demo-5032-gardener-evidence.json) identifikuje zdroje a důkazy.
Archiv nových výstupů není úplná záloha běžné databáze. Předchozí kvalifikace
nedokončeného Blazor Procesu v záznamu 5032 zůstává platná.
Startovací log navíc zachovává blokovaný historický Process
`30442e6a-a462-4f1d-a908-3559b2fb23ff`; jeho diagnostika nepovoluje automatickou obnovu.
Tato oprava jeho stav nemění.
