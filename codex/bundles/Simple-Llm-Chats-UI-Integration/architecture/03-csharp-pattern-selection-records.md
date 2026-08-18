# Pattern Selection Records

## PSR-01 — Adapter / Anti-Corruption Layer

- Use Agent and Simple Chat mappers to produce neutral presentation records.
- Rejected: making presentation components generic over domain records or adding `IsSimpleChat` branches.

## PSR-02 — UI Gateway

- `LlmChats.Ui` owns narrow gateways over definition, conversation, operation, provider-option, event-session, and authorization contracts.
- Rejected: server-side loopback HTTP client and direct EF repositories.

## PSR-03 — Reducer For Durable Operation Events

- A pure reducer consumes accepted/claimed/attempt/delta/completed/terminal events into transient UI state and cursor.
- Rejected: appending deltas directly to the canonical message list or scattering event switches across Razor files.

## PSR-04 — Contributor Registry For Floating Surfaces

- Agent and Simple Chat sources implement a neutral UI contributor contract.
- Rejected: adding Simple Chat dependencies and conditionals into `FloatingAgentChatHost` or making the Simple Chat module depend on the Agent module.

## PSR-05 — Dynamic Component Descriptor

- Source contributors provide a component type and bounded parameter dictionary for focused windows; the shell owns overlay geometry.
- Rejected: service-owned arbitrary markup strings, reflection discovery, or RenderFragment state retained beyond the circuit.

## PSR-06 — Facade Preservation

- Existing Agent components remain compatible facades while their internals use hardened neutral components.
- Rejected: mass-renaming public Agent components during feature integration.
