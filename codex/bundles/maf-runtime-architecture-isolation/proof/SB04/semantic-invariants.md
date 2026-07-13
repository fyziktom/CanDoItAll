# SB04 Semantic Invariants

- Provider credentials must remain masked and promoted only through the existing environment credential mechanisms.
- OpenAI, Azure OpenAI, and Ollama transport behavior must remain semantically unchanged.
- Provider streaming must still acquire the dispatch lease before enumerating updates.
- Required finalizer capture ordering and JSON normalization must remain unchanged.
