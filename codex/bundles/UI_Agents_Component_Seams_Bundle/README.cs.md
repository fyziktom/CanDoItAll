# Agents UI — revidovaný plán oddělení komponent

Verze **CDA-UI-SEAMS-AGENTS-01-v2** používá společnou architekturu v2.

Aktuální požadavek povoluje pouze úpravu těchto dvou bundlů. Implementace nezačala. Další kroky v dokumentaci jsou plán pro pozdější realizaci.

Základní směr zůstává správný: oddělit stav, vykreslování a aplikační operace bez změny současných funkcí. Revize zpřesňuje životní cyklus editoru, uložený versus rozpracovaný stav, závislosti vnořených komponent a ověření skutečného produkčního zapojení. Počet rozhraní ani počet testů není cílem.

Malý sandbox a fyzické oddělení UI nemají čekat na finální bookmarkovatelné URL. Tento bundle připraví konkrétní předání a změří výchozí vývojovou smyčku; samostatný sandbox se realizuje v navazujícím úkolu.

Před realizací projděte [matici zachování chování](requirements/02-behavior-preservation-matrix.md), [sedm fází](plan/00-phase-plan.md), [smlouvu editoru](architecture/09-editor-session-and-host-contract.md) a [výsledek revizní validace](reviews/01-revision-validation.md). Každá implementační fáze musí dodat vlastní důkazy zachování funkcí.
