# Structured Input

## Goals

- Replace placeholder cost logic with token-price based cost calculations where usage metrics are available.
- Make provider pricing configurable per model and preserve it in provider metadata.
- Identify private-style providers and surface a clear badge wherever an agent is presented as a card.

## Non-Goals

- Do not introduce a separate billing subsystem.
- Do not add a fallback path that silently prices unknown models at zero.
- Do not refactor unrelated provider configuration or process analytics surfaces.

## Constraints

- Keep changes strongly typed across models, metadata, validation, and UI parameters.
- Keep OpenAI prices sourced from the official pricing page.
- Keep private/Ollama defaults editable and visibly distinct from OpenAI defaults.
