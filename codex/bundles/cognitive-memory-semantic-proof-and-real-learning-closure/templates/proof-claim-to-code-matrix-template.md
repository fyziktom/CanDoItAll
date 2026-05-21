# Proof Claim To Code Matrix Template

| Capability claim | Required production source proof | Required test proof | Required negative fixture | Result |
|---|---|---|---|---|
| `embedding-backed` | Source injects/calls embedding/vector/ranker provider and lexical fallback is separately named | Fake embedding provider creates paraphrase pair without shared exact/alias tokens | Lexical-only fixture must fail this claim | Pending |
| `Czech/diacritic` | Source has Czech signal model and diacritic-insensitive matching that preserves original text | Czech diacritic and no-diacritic Q&A tests pass | English-only keyword fixture must fail this claim | Pending |
| `claim-specific` | Source maps each aggregate claim only to linked evidence for that claim | Unrelated evidence anchor excluded from aggregate claim source map | Record-level broad map fixture must fail | Pending |
| `automatic accepted-use` | Real outcome/feedback event invokes emitter | Event path emits signal, direct seed is absent | Service-only/direct-test fixture must fail | Pending |
