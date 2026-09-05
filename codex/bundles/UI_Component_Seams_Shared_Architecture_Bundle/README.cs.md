# Společná architektura hranic UI komponent — revize 2

Reference: CDA-UI-SEAMS-BASE-v2. Jde o neprováděcí architektonický podklad.
Aktuální požadavek povoluje pouze úpravu obou bundles; implementace nezačala.

Směr zůstává: zachovat funkčnost, nejprve prokázat oddělení odpovědností na místě a teprve
potom přesouvat komponenty. Components a FileTools zůstávají živé sibling závislosti.

Revize odstraňuje závislost sandboxu na dokončeném routingu. Po ověřené hranici mohou
samostatně následovat lehký UI projekt se sandboxem a napojení bookmarkovatelnosti.
Výkon vývojové smyčky se měří před změnou a po fyzickém oddělení.

Hodnocení nyní zahrnuje potomky, další dialogy, služby, datové typy a jejich assembly,
CSS a JS. Render s falešnými službami není důkaz lehkého build grafu. Stav editoru má
vlastní životnost; circuit-scoped služba nesmí automaticky vlastnit všechny drafty.

Není předepsaný počet interfaces ani univerzální controller. Oprávněný host nebo malá
projekce je vhodná, pokud vytváří skutečnou hranici. AppComponents nesmí absorbovat
feature význam ani závislosti na implementacích modulů.

Podklady bookmarkovatelnosti zůstávají návrhem. URL, historie a Workbench rozhodnutí nejsou
tímto schválená. Zachování chování se dokládá konkrétní maticí scénářů, ne shodným počtem testů.

Úplná pravidla a mapa dokumentů jsou v [anglickém README](README.md).
