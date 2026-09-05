> Implementation authorized by the owner on 2026-09-04. See inputs/04-implementation-authorization.md and reviews/02-execution-status.md. Documentation-only wording below records the preparation stage and does not block this authorized execution.

# Agents UI — implementace oddělení komponent a důkazy

Verze **CDA-UI-SEAMS-AGENTS-01-v2** používá společnou architekturu v2.

Vlastník následně povolil implementaci. Fáze SB01–SB07 jsou dokončené a ověřené; omezení měření watch a navazujícího sandboxu zůstávají výslovně uvedená. Aktuální stav a důkazy jsou v [přehledu implementace](reviews/02-execution-status.md) a [závěrečném manifestu](proof/SB07/manifest.md).

Základní směr zůstává správný: oddělit stav, vykreslování a aplikační operace bez změny současných funkcí. Revize zpřesňuje životní cyklus editoru, uložený versus rozpracovaný stav, závislosti vnořených komponent a ověření skutečného produkčního zapojení. Počet rozhraní ani počet testů není cílem.

Malý sandbox a fyzické oddělení UI nemají čekat na finální bookmarkovatelné URL. Bundle dodává konkrétní předání pro první sandbox katalogu. Výchozí prostředí, graf a watch seznam jsou doložené; spolehlivé měření teplé vývojové smyčky chybí kvůli rozpornému stavu připravenosti watch. Zrychlení se netvrdí; samostatný sandbox a měření patří navazujícímu úkolu.

Před realizací projděte [matici zachování chování](requirements/02-behavior-preservation-matrix.md), [sedm fází](plan/00-phase-plan.md), [smlouvu editoru](architecture/09-editor-session-and-host-contract.md) a [výsledek revizní validace](reviews/01-revision-validation.md). Každá implementační fáze musí dodat vlastní důkazy zachování funkcí.
