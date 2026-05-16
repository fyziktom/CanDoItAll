# CTO shrnutí pro CanDoItAll Cognitive Memory patch

Původní návrh není špatný. Naopak má dobře vyřešený základ: Qdrant je pouze projekce, surové zdroje jsou autorita, recall má trace, probing nedělá přímou mutaci pravdy a Epistemic Drive není jen jednoduché skóre.

Hlavní slabina je v tom, že architektura zatím popisuje paměť jako kombinaci zdrojů, canonical memory, recallu, konsolidace a probing evidence, ale chybí jí několik klíčových mezivrstev, které u lidského mozku dělají rozdíl mezi „databází znalostí“ a kognitivním systémem:

1. **Pracovní paměť a pozornost**  
   `RecallContextPack` není totéž jako pracovní paměť. Chybí aktivní pracovní rámec, focus slots, cíl úlohy, inhibice rušivých asociací, rozpočet kontextu a rozhodnutí, co se má vůbec dostat do odpovědi nebo workflow kroku.

2. **Predikční chyba a salience**  
   Systém potřebuje trvale ukládat signály typu: „tohle mě překvapilo“, „tady jsem byl přehnaně sebejistý“, „tady vznikl rework“, „toto bylo velmi užitečné“, „toto je rizikové“. Tyto signály pak mají řídit aktivaci, replay, Epistemic Drive i kalibraci confidence.

3. **Claim/evidence/belief ledger**  
   `MemoryItem` je příliš hrubý objekt. Pro enterprise paměť je bezpečnější atomizovat znalosti na tvrzení, důkazy, proti-důkazy, scope, časovou platnost a stav validace. Jinak hrozí, že canonical summary schová rozpor nebo smíchá podobné kontexty.

4. **Entity a context binding**  
   Produkční Docker, testovací Docker a lokální Docker jsou sémanticky blízké, ale operačně rozdílné. Scope/tagy nestačí; je potřeba explicitní registry entit, aliasů a kontextových rámců.

5. **Časová a sekvenční episodická paměť**  
   Process/workflow run není jen záznam události. Má sekvenci, příčiny, rozhodnutí, výsledek, chyby, rework a validitu v čase. Tohle je potřeba přidat, aby systém uměl odpovídat „proč jsme to udělali takhle?“.

6. **Replay a rehearsal scheduler**  
   Konsolidace je v původním návrhu dobrá, ale chybí prioritizovaný replay: co opakovat, co validovat, co otestovat regresně, co uspat, co držet aktivní.

7. **Procedural skill memory**  
   Procedurální paměť musí být víc než textový runbook. Má mít preconditions, postconditions, kroky, failure modes, validační důkazy, automatizační binding a maturity.

8. **Metamemory / answer gate**  
   Systém musí před odpovědí rozhodnout: mám odpovědět, odpovědět s varováním, zeptat se na upřesnění, požádat o source audit, vytvořit probe, nebo raději přiznat, že nevím?

Patch bundle je navržený tak, aby Codex původní architekturu rozšířil, ne zahodil. Doporučuji ho použít jako další architektonický refactor před samotnou implementací Cognitive Memory.
