# Input coverage

| User input | Normalized requirement | Owning subbundles | Closure proof |
| --- | --- | --- | --- |
| central instance owns real provider access | FR-001–FR-010 | SB02–SB04 | catalog/redaction/E2E |
| only explicitly shared providers visible | FR-001, FR-002, FR-004, FR-009 | SB02, SB03, SB08 | policy/unit/API/UI |
| shared CanDoItAll driver on user app | FR-041–FR-045 | SB05, SB06 | runtime projection/E2E |
| shared and personal drivers together | FR-037, FR-043 | SB06, SB07, SB09 | catalog/runtime/UI |
| add shared source and download list | FR-025–FR-027, FR-055 | SB05, SB08 | sync/UI |
| select providers and configure locally | FR-028–FR-040 | SB05, SB08 | reconciliation/UI |
| central may share multiple providers | FR-006–FR-008, FR-020–FR-021 | SB03, SB04 | catalog/model routing |
| stay close to OpenAI/Ollama standards | FR-011–FR-024 | SB01, SB04 | contract tests/OpenAPI |
| future EGCP access-object reference | FR-046–FR-053 | SB01, SB04, SB07 | header/audit/usage |
| detailed current implementation review | NFR-015–NFR-022 | SB00 | inventory/architecture gate |
| avoid reverse references/DTO/helper mistakes | NFR-015–NFR-021 | SB00–SB06 | dependency/guardrails |
| backend and frontend | full feature | SB01–SB10 | backend/UI gates |
| backend proven before UI | backend acceptance | SB07 before SB08 | gate status |
| two or three Docker instances | FR-058 | SB07, SB12 | three app containers |
| leave instances running | FR-059 | SB12 | handoff/container status |
| careful long tests/credits | NFR-033–NFR-037 | all | test manifests/budget |
| detailed docs | delivery | SB10 | docs validators |
| SharedInfo and OpenAPI | FR-060 | SB11 | snapshot/skill validators |
| ZIP bundle | preparation output | bundle root | generated ZIP/hash |
