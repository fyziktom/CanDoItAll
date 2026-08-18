# Preparation review verdict

## Verdict

Ready for phased execution.

The requested consolidation is architecturally justified, but a direct move into CanDoItAll.Modules.AgentFramework would make an already broad product module responsible for roughly sixteen thousand additional lines spanning domain, application, EF, provider runtime, and Razor concerns.

The implementation-ready strategy is:

1. establish a typed neutral usage projection;
2. extract Simple Chat Core and Application into MAF;
3. split provider runtime from EF persistence;
4. move reusable feature UI into a Components library;
5. compose the feature from the Agent module;
6. scope the existing dashboard over Agent and Simple Chat usage sources;
7. remove all legacy Modules.LlmChats project and namespace residue.

## Critical decisions

- Feature namespace: CanDoItAll.AgentFramework.Llm.SimpleChats.*.
- Feature folder: src/MAF/SimpleChats.
- Generic LLM abstractions/helpers remain in the existing AgentFramework.Llm.* projects.
- Usage analytics is a neutral CanDoItAll.AgentFramework.Usage library.
- The Agent file projection and Simple Chat EF invocation ledger remain independent authoritative stores.
- No cross-store write transaction is introduced.
- New Simple Chat invocations capture immutable pricing evidence at execution time.
- Legacy unpriced rows remain unpriced.
- /agents?tab=simple-chats is canonical; /chats is redirect-only compatibility.
- One unfiltered Stable run is reserved for SB11; named Playwright plus Playwright MCP provides browser closure.

## Preparation gaps that are not blockers

- CanDoItAll Components MCP transport closed during both library discovery and compact recommendation. Existing BaseLib/Charts usage and the shared compact composition standard provide enough evidence to prepare the UI contract; SB07 must retry the MCP before changing reusable component code.
- Three scoped CodeAnalytics runs reported slightly different broader-scope health counts, but all agreed on the relevant facts: no current LlmChats project cycle and only pre-existing AgentFramework cycles outside the proposed dependency path. SB01 must take the definitive execution snapshot.

## Stop conditions

Execution stops if:

- target project references would create a cycle;
- Core/Application require EF, Web, Razor, or Agent-module dependencies;
- runtime cannot be separated from persistence through existing/narrow ports without changing behavior;
- legacy cost needs to be guessed or repriced;
- table/route/scope compatibility would be broken;
- any checkpoint cannot prove Agent and Simple Chat conversation parity.

