# Proof Claim To Code Matrix Template

| Capability claim | Required production source proof | Required test proof | Required negative fixture | Result |
|---|---|---|---|---|
| `embedding-backed` | Source injects/calls embedding/vector/ranker provider and lexical fallback is separately named | Fake embedding provider creates paraphrase pair without shared exact/alias tokens | Lexical-only fixture must fail this claim | Pending |
| `Czech/diacritic` | Source has Czech signal model and diacritic-insensitive matching that preserves original text | Czech diacritic and no-diacritic Q&A tests pass | English-only keyword fixture must fail this claim | Pending |
| `claim-specific` | Source maps each aggregate claim only to linked evidence for that claim | Unrelated evidence anchor excluded from aggregate claim source map | Record-level broad map fixture must fail | Pending |
| `automatic accepted-use` | Real outcome/feedback event invokes emitter | Event path emits signal, direct seed is absent | Service-only/direct-test fixture must fail | Pending |
| `provider-backed` | Source injects and calls a provider abstraction instead of using a class name as proof | Fake provider/ranker path is exercised by tests | Class-name-only fixture must fail this claim | Pending |
| `scheduled` | Source has a scheduler or maintenance lifecycle that calls the behavior automatically | Scheduled runner or maintenance test passes | Manual-only service call fixture must fail this claim | Pending |
| `line-level` | Source persists statement or line lineage to exact supporting claims/sources | Reference resolver proves sentence-specific support | Broad recall source-map fixture must fail this claim | Pending |
| `domain synthesis` | Source builds domain claims without source-map or support-count meta text | Dream synthesis positive test produces canonical domain text | Template/meta-text fixture must fail this claim | Pending |
| `portable proof` | Proof uses `repo://` and `bundle://` artifact references and validates from a moved checkout | Original and moved-checkout completed validation pass | Machine-specific path fixture must fail this claim | Pending |

