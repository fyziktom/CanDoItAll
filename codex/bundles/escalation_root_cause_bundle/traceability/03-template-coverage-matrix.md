# Template Coverage Matrix

| Scope | Required subbundle | Closure proof |
| --- | --- | --- |
| All process definitions | SB09 | Full audit table with migrated/already typed/exception/blocked disposition. |
| All process step markdown | SB09 | Prose-only hard gates removed or mirrored into typed metadata. |
| All validation JSON | SB08, SB09 | Typed schema validation and missing metadata negative tests. |
| All prompt JSON | SB09 | Prompt text no longer owns hard gates without typed metadata. |
| All subprocess parents | SB06, SB08, SB09 | Accepted child output, no-go output, and child diagnostic proof. |
| .NET solution setup | SB01, SB07, SB11 | Resolved script refs, helper receipt proof, runtime-owned executor proof. |
| Blazor delivery/repair templates | SB09, SB12 | Runtime proof and repair loop audit rows. |
| Screenshot/writeback templates | SB06, SB09, SB12 | Tool receipt and artifact slot proof. |
| Business artifact templates | SB05, SB09, SB12 | Semantic acceptance and ledger-slot audit rows. |
