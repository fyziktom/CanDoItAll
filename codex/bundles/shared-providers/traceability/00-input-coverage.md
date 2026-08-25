# Input coverage

| User input | Normalized requirement | Owning subbundles | Closure proof |
| --- | --- | --- | --- |
| central instance owns real provider access | FR-001–FR-010 | SB02–SB04 | SB02–SB04 PASS: canonical Workspace persistence, explicit eligibility/publication, sanitized catalog/auth/ETag, current persisted-route/secret resolution, five production adapter rows, metadata-only audit, and hosted stale-row recovery; deterministic neutral dispatch is used in proof and client/multi-host E2E remains SB05–SB07 |
| only explicitly shared providers visible | FR-001, FR-002, FR-004, FR-009 | SB02, SB03, SB08 | SB02/SB03 PASS: unpublished default, explicit eligible publication, unpublish removal, and fail-closed discovery/routing; UI remains SB08 |
| shared CanDoItAll driver on user app | FR-041–FR-045 | SB05, SB06 | SB05 source/import state and SB06 runtime projection/no-fallback are complete; SB06's frozen 18/16/10 lanes were freshly revalidated in genuine Release |
| shared and personal drivers together | FR-037, FR-043 | SB06, SB07, SB09 | SB06 proves catalog/runtime coexistence and no fallback; SB07 real multi-host proof and SB09 UI proof remain |
| add shared source and download list | FR-025–FR-027, FR-055 | SB05, SB08 | SB05 PASS: safe source lifecycle, test, identity pinning, conditional catalog sync; UI remains SB08 |
| select providers and configure locally | FR-028–FR-040 | SB02, SB05, SB08 | SB02/SB05 PASS: real multi-selection, stable identity/local intent, replacement retirement, authoritative missing/reappearance, and atomic source propagation; UI remains SB08 |
| central may share multiple providers | FR-006–FR-008, FR-020–FR-021 | SB03, SB04 | SB03/SB04 PASS: canonical multi-publication catalog, distinct duplicate-model routing IDs, exact publication/model re-resolution, and unambiguous real adapter dispatch |
| stay close to OpenAI/Ollama standards | FR-011–FR-024 | SB01, SB03, SB04 | SB01 contracts, SB03 discovery/errors, and SB04 exact surface-specific Chat/Responses tool, choice, schema, text/image shapes plus Base64-only Images, SSE/cancellation, capability intersection, and sanitized inference errors pass; Ollama production support is Chat-only and final exported contract remains SB11 |
| future EGCP access-object reference | FR-046–FR-053 | SB01, SB02, SB03, SB04, SB07 | SB01 bounded auth-independent header, SB02 metadata-only audit/usage shape, SB03 discovery handling, and SB04 central-hop capture, upstream exclusion, operation-disjoint token/image projection, idempotent terminalization, and hosted stale-row recovery pass; real multi-hop propagation remains SB07 |
| detailed current implementation review | NFR-015–NFR-022 | SB00 | SB00 PASS: governed inventory, 8+6 characterization tests, before/after CodeAnalytics and architecture gate |
| avoid reverse references/DTO/helper mistakes | NFR-015–NFR-021 | SB00–SB06 | force-refreshed SB06 graph remains acyclic at 14 projects/34 edges; Workspace uses neutral ports, Http only Abstractions, Composition concrete wiring, no new partial declaration/responsibility expansion, and the August 25 SB04/SB06 revalidation added no project edge or alternate runtime |
| backend and frontend | full feature | SB01–SB10 | backend/UI gates |
| backend proven before UI | backend acceptance | SB07 before SB08 | gate status |
| two or three Docker instances | FR-058 | SB07, SB12 | three app containers |
| leave instances running | FR-059 | SB12 | handoff/container status |
| careful long tests/credits | NFR-033–NFR-037 | all | SB01–SB06 use exact focused selections and no broad gate; SB07 preserves seven failed lifecycle attempts/seven image builds and is blocked from another Docker run pending explicit one-lane/one-build authority and a durable 9/9 amendment; the single aggregate remains SB12-owned |
| detailed docs | FR-061 | SB10 | docs, link, tooling, and handoff validators |
| SharedInfo and OpenAPI | FR-060 | SB11 | snapshot/skill validators |
| ZIP bundle | preparation output | bundle root | generated ZIP/hash |
