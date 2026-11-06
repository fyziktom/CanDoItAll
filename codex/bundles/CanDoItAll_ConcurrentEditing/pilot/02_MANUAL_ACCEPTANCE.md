# Manuální akceptace pilotu na 5032

Tento checklist je pro uživatele po tom, co Codex dodá test-ready stav. Testovat jen na vlastních testovacích položkách, nikoli mazáním cizích dat nebo přepínáním produkčního profilu. Základ stačí ve dvou oknech; třetí okno se použije pro závod během merge. Stejný přihlášený uživatel je záměrně relevantní scénář.

## Příprava

Otevři dvě nezávislá okna hlavní aplikace 5032, označ je A a B. Vytvoř prompt `Concurrency manual pilot` s krátkým názvem, summary `Výchozí shrnutí`, třemi řádky obsahu a jedním tagem. Obě okna otevřou stejný modal. Pro jednotlivé případy obnov původní stav běžným uložením nebo použij kopii, ne přepisem databáze. Codex má uvést použitý build/commit a potvrdit, že nejde o staré assets.

| ID | Co udělat | Co musí být vidět / zůstat zachováno |
|---|---|---|
| M01 | A uloží změnu názvu, B pouze kouká na otevřený detail. | B dostane srozumitelné upozornění. Modal se nezavře, jeho obsah se bez rozhodnutí nepřepíše. Notifikace je vidět nad modalem. |
| M02 | B rozepsalo summary, A mezitím uloží jiný název. B otevře porovnání. | U názvu je remote change, u summary moje změna. Preview obsahuje obě. Apply upraví formulář, ale samo neuloží. Následný Save uloží obě hodnoty. |
| M03 | A i B upraví stejné summary různě. | Konflikt nemá potichu vybraného vítěze. Lze zvolit Moje/Aktuální nebo napsat výslednou hodnotu. Save před vyřešením je blokovaný bez ztráty textu. |
| M04 | A změní první řádek obsahu, B třetí; pak oba stejný řádek. | Oddělené změny jsou čitelně nabídnuté ke spojení; překryv potřebuje rozhodnutí. Žádný dump JSON. Výsledek zachovává text, unicode a nové řádky. |
| M05 | A přidá tag, B odstraní původní tag a přidá jiný. | Srozumitelné chips/řádky a správné zachování additions/removals; ne bezmyšlenkovitý union všech původních tagů. |
| M06 | Obě strany přidají jiný model a každý označí jako preferred. | Výsledek upozorní na konflikt preference; neuloží dva preferované modely. Žádná z přidaných položek se bez rozhodnutí neztratí. |
| M07 | B napíše neplatnou numerickou hodnotu, např. `-`, A uloží jinou změnu. | B si uchová skutečný raw text i validační chybu. Merge/cancel ho nenahradí poslední validní parsed hodnotou. Save zůstane korektně blokovaný. |
| M08 | V rozepsaném B klikni X, Cancel, Escape, přechod na jinou stránku a zpět; zkus změnit vybraný record. | Nabídka zachovat nebo vědomě zahodit změny. Volba pokračovat ponechá přesně draft, target i URL v konzistentním stavu. Žádný opožděný close. |
| M09 | B otevře merge, udělá volby a klikne Zrušit. | Zavře se jen floating merge, ne rodičovský modal. Původní B draft zůstane. Focus se vrátí rozumně do formuláře. |
| M10 | Zatímco B má list s filtrem a jinou stránkou, A vytvoří/upraví/archivuje položku. | Seznam se potichu aktualizuje bez prázdného flash/overlay; filtr, řazení a editor target zůstávají. Smazání poslední položky stránky nezanechá nesmyslnou prázdnou stránku. |
| M11 | Otevři Gallery editor přes existující picker v chatu/workflow. Vyvolej konflikt a toast. | Stejný merge funguje i v nested kontextu, toast je vidět a lze ho zavřít. Nezavře jiný dialog, nevloží prompt dvakrát a neodhodí chat text. |
| M12 | A uloží, B má vlastní text; v B se pokus běžně uložit před vyřešením změn. | Žádné přepsání A. Zůstane B draft a trvalá informace co řešit, i když toast mezitím zmizí. |
| M13 | B review právě načetlo R, třetí okno C provede další změnu. | Review oznámí novější změnu nebo finální Save narazí na další konflikt. Výsledek nesmí bez podmínky přepsat C. Dosavadní práce B nezmizí. |
| M14 | A záznam archivuje, B má rozepsaný obsah. | B neprovede implicitní restore jen kvůli převzetí timestampu. Lifecycle je jasně vysvětlen, případné obnovení je samostatné vědomé rozhodnutí. |
| M15 | Toast už je zobrazený; otevři další modal/merge a zase jej zavři. | Toast nezmizí za novým oknem, nepřibude kopie a neresetuje se nekonečně jeho timeout. Dismiss funguje, psaní do formuláře zůstává normální. |
| M16 | Opakovaně otevři/zavři a přepni několik promptů; zadej delší text. | Žádné narůstající duplicate notices, samovolné refresh, ztráta focus nebo citelné zpomalení psaní. Podrobné leak/perf proof dodává Codex, uživatel hodnotí reálný pocit. |

## Developer-only scénáře, které se nemají improvizovat na živých datech

Lost event, offline/unknown commit, DB commit failure/outer rollback, context/profile revocation, deleted-lifetime reincarnation, malicious payload, projection lag a přetížení queue jsou v automatizovaných fault-injection tests. Na 5032 se kvůli nim nevypíná náhodně databáze, nesnižuje ochrana ani nemažou volumes. Codex je doloží v T11–T24/T27/T33/T37.

## Záznam a schválení

Codex dodá tabulku M01–M16 se sloupcem `uživatel zatím neověřil` a svou vlastní test evidence zvlášť. **Nesmí vyplnit uživatelův souhlas za něj.** Při připomínce opraví pilot, rebuildne a zastaví se znovu.

Jednoznačný příklad následného souhlasu: „Pilot Prompt Gallery schvaluji. Pokračuj v dalších modulech podle plánu.“ Samotné „tohle tlačítko posuň“ nebo „vypadá lépe“ není svolení k rollout celé aplikace.
