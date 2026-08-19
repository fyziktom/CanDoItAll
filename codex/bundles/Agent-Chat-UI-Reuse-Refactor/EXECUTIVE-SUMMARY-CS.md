# Shrnutí pro vlastníka projektu

Tento bundle je záměrně pouze první UI fáze.

Jeho cílem je rozdělit dnešní agentní chat UI na:

- backendově neutrální prezentační komponenty;
- agentní adaptéry, které vlastní `AgentDefinition`, session/workspace služby, approvals, nástroje, execution logy, voice, attachments, context affinity a další agentní chování.

Bundle nesmí přidat žádnou viditelnou funkcionalitu Simple Chats. Nezavádí společný seznam, přepínač Agents/Simple Chats, SSE klienta ani tlačítko pro přidání projektového kontextu. Tyto věci budou patřit do samostatné navazující fáze až po ručním ověření, že Agent Chats po refaktoringu fungují přesně jako dříve.

Testovací strategie odpovídá novému SharedInfo standardu: každý měněný subbundle používá skutečný diff a rozsahy řádků přes `code_analytics_impacted_tests_get`. Široké testy nejsou běžnou vývojovou smyčkou a případný Stable/full gate může proběhnout nejvýše jednou, pouze na základě konkrétního triggeru.
