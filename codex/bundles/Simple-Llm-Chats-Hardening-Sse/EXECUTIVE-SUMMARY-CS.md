# Exekutivní shrnutí

První implementace jednoduchých LLM chatů je architektonicky výrazně dál než původní stav. Vznikl
samostatný produktový modul, persistence, API, verzované definice a durable operace. Tuto práci není
vhodné zahodit ani přepisovat jako agenta bez nástrojů.

Před další fází však musí proběhnout hardening. Nejvážnější problém je, že deklarovaná transakce nad
produktovou konverzací a generickým transcript storem není skutečně společná. Transcript store si vytváří
vlastní `AppDbContext` a vlastní transakci, takže po chybě může zůstat orphan transcript nebo se mohou
rozejít dvě uložené pravdy o konverzaci. Do samostatných commitů jsou rozdělené také admission turnu,
evidence, assistant commit, kompenzace a terminal operation state.

Další blokující mezery jsou cancellation race do `Succeeded`, tiché vyčerpání kompenzace, idempotentní
replay závislý na pozdější archivaci, neúplný profile fence a procesově lokální rozhodování o tom, zda je
operace ještě živá. Při více instancích by druhá instance mohla považovat práci první za opuštěnou. HTTP
request navíc čeká na celou odpověď a jeho disconnect může zrušit placené nebo pomalé lokální volání.

Bundle proto nejprve opravuje kanonický model, skutečné transakční commandy, state machine, profilový
lifecycle, distribuovaný execution lease, background dispatcher a bounded SQL reads. Teprve po checkpointu
CP1 přidává skutečný tokenový streaming pro OpenAI, Azure OpenAI a Ollama, durable event journal a SSE
endpoint s replayem přes `Last-Event-ID`, heartbeat, gap eventy a ukončením po terminal stavu.

Výsledné API bude přijímat turn jako durable operaci a vracet `202 Accepted`. Provider práce poběží
nezávisle na životnosti requestu. Klient se bude moci znovu připojit bez druhého provider volání a stav
vždy ověřit přes operation resource.

UI, floating chaty, izolace společných komponent a Project Structure kontext zůstávají mimo rozsah.
Odemknou se až po zeleném finálním gate.
