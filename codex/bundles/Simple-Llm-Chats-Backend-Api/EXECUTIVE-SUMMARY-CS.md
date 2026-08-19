# Stručné shrnutí pro architekta

Bundle implementuje pouze backendovou a API část jednoduchých LLM chatů. UI je záměrně mimo rozsah,
protože mu bude předcházet samostatný refaktoring společných chatových komponent a následný UI
integrační bundle.

Hlavní architektonické rozhodnutí je, že jednoduchý chat nebude agent s vypnutými nástroji. Využije
existující lightweight `ILlmInvocationPort` a robustní ordinary-conversation engine, ale dostane vlastní
produktovou doménu:

- definice chatu a její neměnné revize;
- conversation metadata oddělená od technického transcriptu;
- PostgreSQL store s cross-process CAS;
- operace s idempotencí, zrušením, obnovou a usage auditem;
- profilově oplocené provider volání;
- samostatné HTTP API.

Vzniknou dva projekty:

- `CanDoItAll.Modules.LlmChats` — doména, aplikační kontrakty a use cases;
- `CanDoItAll.Modules.LlmChats.Persistence` — EF Core konfigurace, PostgreSQL store, unit of work a
  runtime/profile adaptéry.

Současný file-backed `Llm.Conversations` store zůstane knihovním a testovacím adaptérem. Produkční
composition root nebude globálně registrovat dormantní `ILlmConversationService`; nový modul vytvoří
vlastní úzce pojmenovaný conversation engine nad EF storem a profilově oploceným invocation portem.

Bundle také řeší aktuální ownership detail provider vrstvy: jednoduché chaty nesmí získat závislost na
AgentFramework Core ani na aktivaci workflow nodes. Úzké read-only provider/capability kontrakty mají
být v provider-neutral `AgentFramework.Providers` a registrace `ILlmInvocationPort` v
`Llm.ProviderRuntime`; stávající AgentFramework a Workflows vrstvy je pouze implementují nebo používají.

Testy jsou rozdělené do levných cílených gates. Během subbundlů je zakázané opakovaně pouštět celé
Unit/Integration/solution suites. Celý stabilní Release gate se spustí pouze jednou v posledním
subbundlu. Multiplatformní důkaz poté poskytne existující CI matrix na Windows, Ubuntu a macOS.

Budoucí běžný enterprise chatbot je pokryt architektonicky, nikoliv předčasnou implementací. Konverzace
má původ a stabilní turn/operation identity; definice je verzovaná; budoucí deployment bude samostatný
aggregate, který připne konkrétní revision k webovému widgetu, API kanálu nebo jinému transportu.
Moderace, anonymní identity, rate limiting, human handoff, streaming a channel adapters jsou explicitně
odložené, ale nemají vyžadovat změnu kanonického transcriptu.
