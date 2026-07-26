# Requirement Traceability

| Input or requirement | Bundle location | Owning subbundle | Planned proof | Notes |
| --- | --- | --- | --- | --- |
| N001 / R001 | `inputs/raw/001-remote-agent-package-import.md` | `01-remote-agent-package-import` | **Solved:** Web build; 6 archive unit tests; 3 direct Core policy tests; 2 multipart retry/authorization integration tests | Reopen if SB02 external binding or SB07 schema contradicts runtime |
| N002 / R002 | `inputs/raw/002-agent-upsert-by-external-key.md` | `02-agent-external-key-provisioning` | **Solved:** 2 direct Core tests; 2 real-store concurrent HTTP tests; GET/ETag/stale/conflict/archive/auth proof; zero-cycle architecture snapshot | Critical foundation passed; package-import dependent flow revalidated |
| N003 / R003 | `inputs/raw/003-agent-json-schema-output-contract.md` | `03-portable-json-schema-output` | **Solved:** 11 direct processor, 102 compatibility, and portable provider/API/OpenAPI integration tests | Exact schema/hash/raw output and distinct validation statuses persist |
| N004 / R004 | `inputs/raw/004-workflow-lookup-by-template-key.md` | `04-workflow-stable-key-lookup` | **Solved:** 8 direct stable-identity tests plus 4 persistent host/OpenAPI/auth/isolation cases | Display name is never identity; runnable version is pinned |
| N005 / R005 | `inputs/raw/005-workflow-run-idempotency.md` | `05-workflow-run-idempotency` | **Solved:** 25 unit and 5 persistent ledger/API cases, including 1-created/7-replayed concurrency and changed-fingerprint conflicts | Critical retry foundation passed |
| N006 / R006 | `inputs/raw/006-openapi-response-schemas.md` | `07-openapi-response-contracts` | **Solved:** 4 focused contract tests over 22 operations; typed success/errors, required/nullability/enums, portable-schema isolation, representative runtime/auth parity | SB08 canonical capture unlocked |
| N007 / R007 | `inputs/raw/007-agent-interview-evidence-api.md` | `06-agent-recruiting-evidence` | **Solved:** 13 service/projector, 5 full-host, and 24 concrete target-resolver cases | Human-gated readiness never activates an agent |
| R008 | User request and SharedInfo standard | `08-sharedinfo-api-skills-and-docs` | **Solved:** byte-identical host capture; OpenAPI/SharedInfo/skill validation; five-package recursive hash parity | Baseline HEAD plus explicit `workingTreeClean: false` limitation recorded |
